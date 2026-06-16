using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using System.IO;
using System.Text.RegularExpressions;

namespace Chronokeep.IO
{
    public partial class CsvImporter : IDataImporter
    {
        [GeneratedRegex("\"[^\"]*\",|[^,]*,|[^,]*$")]
        private static partial Regex DataRegex();

        public ImportData? Data { get; private set; }
        private readonly string filePath;
        protected readonly StreamReader File;

        public CsvImporter(string filePath)
        {
            Log.D("IO.CSVImporter", "Opening file.");
            File = new StreamReader(filePath);
            this.filePath = filePath;
        }

        public void FetchHeaders()
        {
            Log.D("IO.CSVImporter", "Getting headers from file.");
            ProcessFirstLine(File.ReadLine()!);
        }

        protected void ProcessFirstLine(string line)
        {
            MatchCollection matches = DataRegex().Matches(line);
            string[] headers = new string[matches.Count];
            int counter = 0;
            foreach (Match m in matches)
            {
                headers[counter++] = m.Value.Replace('"', ' ').TrimEnd(',').Trim();
            }
            Data = new ImportData(headers, filePath, ImportData.FileType.CSV);
        }

        public void FetchData()
        {
            Log.D("IO.CSVImporter", "Getting data from file.");
            while (File.ReadLine()! is { } line)
            {
                MatchCollection matches = DataRegex().Matches(line);
                string[] dataLine = new string[matches.Count];
                int counter = 0;
                foreach (Match m in matches)
                {
                    string match = m.Value.Replace('"', ' ').Trim().TrimEnd(',');
                    dataLine[counter++] = match;
                }
                Data!.AddData(dataLine);
            }
            Finish();
        }

        public void Finish()
        {
            try
            {
                Log.D("IO.CSVImporter", "Closing file.");
                File.Close();
            }
            catch
            {
                Log.D("IO.CSVImporter", "Already closed.");
            }
        }
    }
}