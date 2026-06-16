using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.IO.HtmlTemplates;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Chronokeep.Network
{
    internal class HttpServer
    {
        private Thread? serverThread;
        private HttpListener? listener;
        private int port;
        private IDBInterface? database;
        private Event? theEvent;
        private readonly List<TimeResult> finishResults = [];
        private readonly Dictionary<string, TimeResult> finishDictionary = [];
        private readonly Dictionary<string, List<TimeResult>> participantResults = [];

        private byte[]? resultsCache;
        private readonly Dictionary<string, byte[]> participantCache = [];
        private readonly Dictionary<string, byte[]> emailCache = [];

        private readonly Dictionary<string, Participant> participantDictionary = [];
        private readonly HashSet<string> distanceNames = [];
        private readonly Dictionary<int, ApiObject> apiDictionary = [];

        private readonly Lock infoLock = new();

        private bool keepAlive = true;

        public HttpServer(IDBInterface database, int port)
        {
            Initialize(database, port);
        }

        public void UpdateInformation()
        {
            Log.D("Network.HttpServer", "Updating information for HttpServer.");
            if (!infoLock.TryEnter(3000))
            {
                Log.D("Network.HttpServer", "Unable to get lock.");
                return;
            }
            try
            {
                theEvent = database!.GetCurrentEvent();
                finishResults.Clear();
                finishDictionary.Clear();
                participantResults.Clear();
                foreach (TimeResult r in database.GetTimingResults(theEvent!.Identifier))
                {
                    if (!finishDictionary.TryGetValue(r.Bib, out TimeResult? finRes) || finRes.SystemTime.CompareTo(r.SystemTime) < 0)
                    {
                        finishDictionary[r.Bib] = r;
                    }
                    if (!participantResults.TryGetValue(r.Bib, out List<TimeResult>? pResList))
                    {
                        pResList = [];
                        participantResults[r.Bib] = pResList;
                    }

                    pResList.Add(r);
                }
                distanceNames.Clear();
                foreach (Distance d in database.GetDistances(theEvent.Identifier).Where(d => d.LinkedDistance == Constants.Timing.DISTANCE_NO_LINKED_ID))
                {
                    distanceNames.Add(d.Name);
                }
                finishResults.AddRange(finishDictionary.Values);
                finishResults.RemoveAll(r => string.IsNullOrEmpty(r.Bib));
                finishResults.RemoveAll(r => r.DistanceName.Length < 1);
                // clear response caches whenever we update information
                resultsCache = null;
                participantCache.Clear();
                participantDictionary.Clear();
                foreach (Participant p in database.GetParticipants(theEvent.Identifier))
                {
                    participantDictionary[p.Identifier.ToString()] = p;
                }
                apiDictionary.Clear();
                foreach (ApiObject api in database.GetAllAPI())
                {
                    apiDictionary[api.Identifier] = api;
                }
            }
            finally
            {
                infoLock.Exit();
            }
        }

        public void Stop()
        {
            keepAlive = false;
            listener?.Stop();
        }

        private void Listen()
        {
            while (keepAlive)
            {
                try
                {
                    HttpListenerContext context = listener!.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {
                    Log.E("Network.HttpServer", "Exception caught trying to serve something.\n" + ex.Message);
                }
            }
        }

        private void Process(HttpListenerContext context)
        {
            string filename = context.Request.Url!.AbsolutePath;
            Log.D("Network.HttpServer", "'" + filename + "' requested.");
            filename = filename[1..];
            string partBib = "";
            if (filename.StartsWith("part/", StringComparison.OrdinalIgnoreCase))
            {
                filename = filename[5..];
                partBib = filename;
            }
            string emailBib = "";
            if (filename.StartsWith("email/", StringComparison.OrdinalIgnoreCase))
            {
                filename = filename[6..];
                emailBib = filename;
            }

            byte[] message = Encoding.Default.GetBytes("");
            bool answer = false;
            if (string.IsNullOrEmpty(filename) || filename.Equals("results.htm", StringComparison.OrdinalIgnoreCase) || filename.Equals("results.html", StringComparison.OrdinalIgnoreCase))
            {
                answer = true;
                // Serve up HtmlResultsTemplate
                if (!infoLock.TryEnter(3000))
                {
                    Log.D("Network.HttpServer", "Unable to get lock for outputting results page.");
                    message = Encoding.Default.GetBytes("");
                }
                else
                {
                    try
                    {
                        if (resultsCache == null)
                        {
                            HtmlResultsTemplate results = new(
                                theEvent!,
                                finishResults,
                                true
                                );
                            resultsCache = Encoding.Default.GetBytes(results.TransformText());
                        }
                        message = resultsCache;
                        context.Response.ContentType = "text/html";
                        Log.D("Network.HttpServer", "Results html");
                    }
                    finally
                    {
                        infoLock.Exit();
                    }
                }
            }
            else if (filename.StartsWith("css/", StringComparison.OrdinalIgnoreCase) || filename.StartsWith("js/", StringComparison.OrdinalIgnoreCase))
            {
                Log.D("Network.HttpServer", "Fetching " + filename);
                answer = true;
                // Serve up the file requested.
                string newName = filename.Replace('/', '.');
                Log.D("Network.HttpServer", "Newname is " + newName);
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.IO.HtmlTemplates." + newName)!)
                {
                    message = new byte[stream.Length];
                    stream.ReadExactly(message);
                }
                if (filename.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "text/css";
                }
                else if (filename.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "text/javascript";
                }
                else if (filename.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".html"))
                {
                    context.Response.ContentType = "text/html";
                }
            }
            else if (partBib.Length > 0)
            {
                answer = true;
                // Serve up HtmlParticipantTemplate
                if (!infoLock.TryEnter(3000))
                {
                    Log.D("Network.HttpServer", $"Unable to get lock for outputting participant page for bib {partBib}.");
                    message = Encoding.Default.GetBytes("");
                }
                else
                {
                    try
                    {
                        if (!participantResults.TryGetValue(partBib, out List<TimeResult>? resList))
                        {
                            resList = [];
                            participantResults[partBib] = resList;
                        }
                        if (!participantCache.TryGetValue(partBib, out byte[]? partCache))
                        {
                            HtmlParticipantTemplate results = new(theEvent!, resList);
                            partCache = Encoding.Default.GetBytes(results.TransformText());
                            participantCache[partBib] = partCache;
                        }
                        message = partCache;
                        context.Response.ContentType = "text/html";
                        Log.D("Network.HttpServer", "Participant html");
                    }
                    finally
                    {
                        infoLock.Exit();
                    }
                }
            }
            else if (emailBib.Length > 0)
            {
                answer = true;
                // Serve up the HtmlCertificateEmailTemplate
                if (!infoLock.TryEnter(3000))
                {
                    Log.D("Network.HttpServer", $"Unable to get lock for outputting email page for bib {partBib}.");
                    message = Encoding.Default.GetBytes("");
                }
                else
                {
                    try
                    {
                        message = Encoding.Default.GetBytes("");
                        if (finishDictionary.TryGetValue(emailBib, out TimeResult? finishResult) && participantDictionary.TryGetValue(finishResult.ParticipantId, out Participant? finPart))
                        {
                            if (!emailCache.TryGetValue(emailBib, out byte[]? cachedEmail))
                            {
                                HtmlCertificateEmailTemplate email = new(
                                    theEvent!,
                                    finishResult,
                                    finPart.Email,
                                    distanceNames.Count == 1,
                                    apiDictionary.GetValueOrDefault(theEvent!.ApiId)
                                    );
                                cachedEmail = Encoding.Default.GetBytes(email.TransformText());
                                emailCache[emailBib] = cachedEmail;
                            }
                            Log.D("Network.HttpServer", "Email html");
                            message = cachedEmail;
                            context.Response.ContentType = "text/html";
                        }
                        Log.D("Network.HttpServer", "Email html");
                    }
                    finally
                    {
                        infoLock.Exit();
                    }
                }
            }
            if (answer)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            context.Response.ContentLength64 = message.Length;
            try
            {
                context.Response.OutputStream.Write(message, 0, message.Length);
                context.Response.OutputStream.Flush();
            }
            catch (Exception ex)
            {
                Log.E("Network.HttpServer", "Error attempting to write response.\n" + ex.Message);
            }
        }

        private void Initialize(IDBInterface iDatabase, int iPort)
        {
            database = iDatabase;
            port = iPort;
            keepAlive = true;
            UpdateInformation();

            // Test to ensure we can listen.
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");
            listener.Start();

            serverThread = new Thread(Listen);
            serverThread.Start();
        }
    }
}
