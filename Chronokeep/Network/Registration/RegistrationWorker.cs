/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Participant = Chronokeep.Objects.Registration.Participant;

namespace Chronokeep.Network.Registration
{
    public partial class RegistrationWorker(IdbInterface database, IMainWindow mWindow)
    {
        private bool running;
        private bool keepalive;

        private readonly Lock threadLock = new();

        private Socket? server;
        private bool updateDistanceDictionary = true;
        private readonly List<Socket> clients = [];
        private readonly List<Socket> readList = [];
        private readonly Dictionary<Socket, StringBuilder> bufferDictionary = [];
        private readonly Dictionary<string, Distance> distanceDictionary = [];

        [GeneratedRegex(@"^[^\n]*\n")]
        private static partial Regex Msg();

        public bool IsRunning()
        {
            bool output = false;
            if (!threadLock.TryEnter(3000)) return output;
            try
            {
                output = running;
            }
            finally
            {
                threadLock.Exit();
            }
            return output;
        }

        public void Stop()
        {
            Log.D("Network.Registration.RegistrationWorker", "Instructed to stop registration.");
            if (!threadLock.TryEnter(3000)) return;
            try
            {
                keepalive = false;
            }
            finally
            {
                threadLock.Exit();
            }
        }

        public void UpdateDistances()
        {
            if (!threadLock.TryEnter(3000)) return;
            try
            {
                updateDistanceDictionary = true;
            }
            finally
            {
                threadLock.Exit();
            }
        }

        public void Run()
        {
            Log.D("Network.Registration.RegistrationWorker", "Starting Registration thread.");
            Event? theEvent = database.GetCurrentEvent();
            if (theEvent == null || theEvent.Identifier < 1)
            {
                return;
            }
            if (threadLock.TryEnter(3000))
            {
                try
                {
                    keepalive = true;
                    running = true;
                }
                finally
                {
                    threadLock.Exit();
                }
            }
            else
            {
                return;
            }
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Bind(new IPEndPoint(IPAddress.Any, NetCore.GetTcpPort()));
            server.Listen(10);
            clients.Add(server);
            while (running)
            {
                readList.Clear();
                readList.AddRange(clients);
                try
                {
                    // 5 seconds
                    Socket.Select(readList, null, null, 5_000_000);
                }
                catch (Exception e)
                {
                    Log.D("Network.Registration.RegistrationWorker", $"Exception raised while using select. {e.Message}");
                }
                bool update = false;
                if (threadLock.TryEnter(3000))
                {
                    try
                    {
                        update = updateDistanceDictionary;
                        updateDistanceDictionary = false;
                    }
                    finally
                    {
                        threadLock.Exit();
                    }
                }
                if (update)
                {
                    foreach (Distance d in database.GetDistances(theEvent.Identifier))
                    {
                        distanceDictionary[d.Name] = d;
                    }
                    SendParticipants(theEvent);
                }
                foreach (Socket sock in readList)
                {
                    if (sock == server)
                    {
                        Log.D("Network.Registration.RegistrationWorker", "New incoming connection to registration.");
                        Socket newSock = sock.Accept();
                        clients.Add(newSock);
                        bufferDictionary[newSock] = new StringBuilder();
                    }
                    else
                    {
                        byte[] recvd = new byte[4096];
                        try
                        {
                            int numRecvd = sock.Receive(recvd);
                            if (numRecvd == 0)
                            {
                                Log.D("Network.Registration.RegistrationWorker", "Client disconnected.");
                                clients.Remove(sock);
                                bufferDictionary.Remove(sock);
                                sock.Close();
                            }
                            else
                            {
                                string msg = Encoding.UTF8.GetString(recvd, 0, numRecvd);
                                StringBuilder buffer = bufferDictionary[sock];
                                buffer.Append(msg);
                                Log.D("Network.Registration.RegistrationWorker", $"Message received: {msg.Trim()}");
                                Match m = Msg().Match(buffer.ToString());
                                while (m.Success)
                                {
                                    buffer.Remove(m.Index, m.Length);
                                    string message = m.Value;
                                    try
                                    {
                                        Request res = JsonSerializer.Deserialize<Request>(message)!;
                                        switch (res.Command)
                                        {
                                            case Request.CONNECT:
                                                Log.D("Network.Registration.RegistrationWorker", "Received connect message.");
                                                AppSetting nameSetting = database.GetAppSetting(Constants.Settings.SERVER_NAME)!;
                                                string nameString = nameSetting.Value;
                                                SendMessage(sock, JsonSerializer.Serialize(new ConnectionSuccessfulResponse
                                                {
                                                    Name = nameString,
                                                    Type = Constants.Network.CHRONOKEEP_REGISTRATION_TYPE,
                                                    Version = Constants.Network.CHRONOKEEP_REGISTRATION_VERS
                                                }));
                                                break;
                                            case Request.GET_PARTICIPANTS:
                                                Log.D("Network.Registration.RegistrationWorker", "Received get participant message.");
                                                SendMessage(sock, JsonSerializer.Serialize(new ParticipantsResponse
                                                {
                                                    Participants = GetParticipants(theEvent),
                                                    Distances = GetDistances(),
                                                }));
                                                break;
                                            case Request.ADD_PARTICIPANT:
                                                Log.D("Network.Registration.RegistrationWorker", "Received add participant message.");
                                                try
                                                {
                                                    ModifyParticipant addReq = JsonSerializer.Deserialize<ModifyParticipant>(message)!;
                                                    if (distanceDictionary.TryGetValue(addReq.Participant.Distance, out Distance? oDist))
                                                    {
                                                        Objects.Participant newPart = new(
                                                            addReq.Participant.FirstName,
                                                            addReq.Participant.LastName,
                                                            "", // street
                                                            "", // city
                                                            "", // state
                                                            "", // zip
                                                            addReq.Participant.Birthdate,
                                                            new EventSpecific(
                                                                theEvent.Identifier,
                                                                oDist.Identifier,
                                                                addReq.Participant.Distance,
                                                                addReq.Participant.Bib,
                                                                0,  // checked-in
                                                                "", // comments
                                                                "", // owes
                                                                "", // other
                                                                false,
                                                                addReq.Participant.SmsEnabled,
                                                                "",
                                                                ""
                                                                ),
                                                            "", // email
                                                            "", // phone
                                                            addReq.Participant.Mobile,
                                                            "", // parent
                                                            "", // country
                                                            "", // street2
                                                            addReq.Participant.Gender,
                                                            "", // emergency name
                                                            ""  // emergency phone
                                                            );
                                                        newPart.Trim();
                                                        newPart.FormatData(theEvent.UseMaleFemale);
                                                        database.AddParticipant(newPart);
                                                        SendParticipants(theEvent);
                                                        mWindow.UpdateParticipantsFromRegistration();
                                                    }
                                                    else
                                                    {
                                                        SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                        {
                                                            Error = RegistrationError.DISTANCE_NOT_FOUND
                                                        }));
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    Log.E("Network.Registration.RegistrationWorker", $"Error deserializing json for add participant. {e.Message}");
                                                    SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                    {
                                                        Error = RegistrationError.PARTICIPANT_NOT_FOUND
                                                    }));
                                                }
                                                break;
                                            case Request.UPDATE_PARTICIPANT:
                                                Log.D("Network.Registration.RegistrationWorker", "Received update participant message.");
                                                try
                                                {
                                                    ModifyParticipant addReq = JsonSerializer.Deserialize<ModifyParticipant>(message)!;
                                                    if (!int.TryParse(addReq.Participant.Id, out int eventSpecId))
                                                    {
                                                        eventSpecId = -1;
                                                    }
                                                    Objects.Participant updatedPart = database.GetParticipantEventSpecific(theEvent.Identifier, eventSpecId)!;
                                                    if (!updatedPart.IsSimilar(addReq.Participant))
                                                    {
                                                        SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                        {
                                                            Error = RegistrationError.PARTICIPANT_NOT_FOUND
                                                        }));
                                                    }
                                                    else if (!distanceDictionary.TryGetValue(addReq.Participant.Distance, out Distance? tDist))
                                                    {
                                                        SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                        {
                                                            Error = RegistrationError.DISTANCE_NOT_FOUND
                                                        }));
                                                    }
                                                    else
                                                    {
                                                        updatedPart.Update(
                                                            addReq.Participant.FirstName,
                                                            addReq.Participant.LastName,
                                                            addReq.Participant.Gender,
                                                            addReq.Participant.Birthdate,
                                                            tDist,
                                                            addReq.Participant.Bib,
                                                            addReq.Participant.SmsEnabled,
                                                            addReq.Participant.Mobile
                                                            );
                                                        database.UpdateParticipant(updatedPart);
                                                        SendParticipants(theEvent);
                                                        mWindow.UpdateParticipantsFromRegistration();
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    Log.E("Network.Registration.RegistrationWorker", $"Error deserializing json for add participant. {e.Message}");
                                                    SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                    {
                                                        Error = RegistrationError.PARTICIPANT_NOT_FOUND
                                                    }));
                                                }
                                                break;
                                            case Request.ADD_UPDATE_PARTICIPANT:
                                                Log.D("Network.Registration.RegistrationWorker", "Received add/update participant message.");
                                                try
                                                {
                                                    ModifyMultipleParticipants addReq = JsonSerializer.Deserialize<ModifyMultipleParticipants>(message)!;
                                                    List<Objects.Participant> newParts = [];
                                                    List<Objects.Participant> updParts = [];
                                                    Dictionary<(string, string, string, string), Objects.Participant> partDictionary = [];
                                                    Dictionary<string, Objects.Participant> partEsDict = [];
                                                    foreach (Objects.Participant p in database.GetParticipants(theEvent.Identifier))
                                                    {
                                                        partEsDict[p.EventSpecific.Identifier.ToString()] = p;
                                                        partDictionary[(p.FirstName, p.LastName, p.Birthdate, p.Distance)] = p;
                                                    }
                                                    foreach (Participant part in addReq.Participants)
                                                    {
                                                        Log.D("Network.Registration.RegistrationWorker", $"Participant ID: {part.Id}");
                                                        if (!distanceDictionary.TryGetValue(part.Distance, out Distance? distance)) continue;
                                                        if (part.Id.Length < 1)
                                                        {
                                                            Log.D("Network.Registration.RegistrationWorker", $"New Part - Bib: {part.Bib}");
                                                            Objects.Participant newPart = new(
                                                                part.FirstName,
                                                                part.LastName,
                                                                "", // street
                                                                "", // city
                                                                "", // state
                                                                "", // zip
                                                                part.Birthdate,
                                                                new EventSpecific(
                                                                    theEvent.Identifier,
                                                                    distance.Identifier,
                                                                    part.Distance,
                                                                    part.Bib,
                                                                    0,  // checked-in
                                                                    "", // comments
                                                                    "", // owes
                                                                    "", // other
                                                                    false,
                                                                    part.SmsEnabled,
                                                                    "",
                                                                    ""
                                                                ),
                                                                "", // email
                                                                "", // phone
                                                                part.Mobile,
                                                                "", // parent
                                                                "", // country
                                                                "", // street2
                                                                part.Gender,
                                                                "", // emergency name
                                                                ""  // emergency phone
                                                            );
                                                            newPart.Trim();
                                                            newPart.FormatData(theEvent.UseMaleFemale);
                                                            newParts.Add(newPart);
                                                        }
                                                        else if (part.Bib.Length > 0)
                                                        {
                                                            if (partEsDict.TryGetValue(part.Id, out Objects.Participant? updatedPart) && updatedPart.IsSimilar(part))
                                                            {
                                                                Log.D("Network.Registration.RegistrationWorker", $"Updated Part - Bib: {part.Bib}");
                                                                updatedPart.Update(
                                                                    part.FirstName,
                                                                    part.LastName,
                                                                    part.Gender,
                                                                    part.Birthdate,
                                                                    distance,
                                                                    part.Bib,
                                                                    part.SmsEnabled,
                                                                    part.Mobile
                                                                );
                                                                updParts.Add(updatedPart);
                                                            }
                                                            else if (partDictionary.TryGetValue((part.FirstName, part.LastName, part.Birthdate, part.Distance), out Objects.Participant? oldTwo))
                                                            {
                                                                Log.D("Network.Registration.RegistrationWorker", $"Updated Part2 - Bib: {part.Bib}");
                                                                oldTwo.Update(
                                                                    part.FirstName,
                                                                    part.LastName,
                                                                    part.Gender,
                                                                    part.Birthdate,
                                                                    distance,
                                                                    part.Bib,
                                                                    part.SmsEnabled,
                                                                    part.Mobile
                                                                );
                                                                updParts.Add(oldTwo);
                                                            }
                                                        }
                                                    }
                                                    database.AddParticipants(newParts);
                                                    database.UpdateParticipants(updParts);
                                                    mWindow.UpdateParticipantsFromRegistration();
                                                }
                                                catch (Exception e)
                                                {
                                                    Log.E("Network.Registration.RegistrationWorker", $"Error deserializing json for add participant. {e.Message}");
                                                    SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                    {
                                                        Error = RegistrationError.PARTICIPANT_NOT_FOUND
                                                    }));
                                                }
                                                break;
                                            case Request.DISCONNECT:
                                                Log.D("Network.Registration.RegistrationWorker", "Received disconnect message.");
                                                clients.Remove(sock);
                                                bufferDictionary.Remove(sock);
                                                sock.Close();
                                                break;
                                            default:
                                                Log.D("Network.Registration.RegistrationWorker", "Unknown message received.");
                                                SendMessage(sock, JsonSerializer.Serialize(new ErrorResponse
                                                {
                                                    Error = RegistrationError.UNKNOWN_MESSAGE
                                                }));
                                                break;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.E("Network.Registration.RegistrationWorker", $"Error deserializing json. {e.Message}");
                                    }
                                    m = Msg().Match(buffer.ToString());
                                }
                                bufferDictionary[sock] = buffer;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.D("Network.Registration.RegistrationWorker", $"Error communicating with socket. {e.Message}");
                            clients.Remove(sock);
                            bufferDictionary.Remove(sock);
                            sock.Close();
                        }
                    }
                }
                if (!threadLock.TryEnter(3000)) continue;
                try
                {
                    if (!keepalive)
                    {
                        running = false;
                        break;
                    }
                }
                finally
                {
                    threadLock.Exit();
                }
            }
            foreach (Socket sock in clients)
            {
                try
                {
                    if (sock != server)
                    {
                        SendMessage(sock, JsonSerializer.Serialize(new DisconnectResponse()));
                    }
                    sock.Close();
                }
                catch
                {
                    Log.D("Network.Registration.RegistrationWorker", "Error closing socket.");
                }
            }
            Log.D("Network.Registration.RegistrationWorker", "Thread exiting.");
        }

        private void SendParticipants(Event theEvent)
        {
            Log.D("Network.Registration.RegistrationWorker", $"Attempting to send participants message. There are {clients.Count} clients connected.");
            foreach (Socket sock in clients.Where(sock => sock != server && sock.Connected))
            {
                SendMessage(sock, JsonSerializer.Serialize(new ParticipantsResponse
                {
                    Participants = GetParticipants(theEvent),
                    Distances = GetDistances(),
                }));
            }
        }

        private List<Participant> GetParticipants(Event theEvent)
        {
            List<Participant> output = [];
            List<Objects.Participant> participants = database.GetParticipants(theEvent.Identifier);
            output.AddRange(participants.Select(participant => new Participant()
            {
                Id = participant.EventSpecific.Identifier.ToString(),
                Bib = participant.Bib,
                FirstName = participant.FirstName,
                LastName = participant.LastName,
                Gender = participant.Gender,
                Birthdate = participant.Birthdate,
                Distance = participant.Distance,
                Mobile = participant.Mobile,
                SmsEnabled = participant.EventSpecific.SmsEnabled,
                Apparel = participant.EventSpecific.Apparel
            }));
            return output;
        }

        private List<string> GetDistances()
        {
            return [.. distanceDictionary.Keys];
        }

        private static void SendMessage(Socket sock, string msg)
        {
            Log.D("Network.Registration.RegistrationWorker", $"Sending message '{msg}'");
            sock.Send(Encoding.Default.GetBytes($"{msg}\n"));
        }
    }
}

