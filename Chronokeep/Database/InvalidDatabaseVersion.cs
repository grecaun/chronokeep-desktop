namespace Chronokeep.Database
{
    internal class InvalidDatabaseVersion : System.Exception
    {
        public int FoundVersion { get; set; } = -1;
        public int MaxVersion { get; set; } = -1;
        public InvalidDatabaseVersion() { }
        public InvalidDatabaseVersion(int foundVersion, int maxVersion)
        {
            FoundVersion = foundVersion;
            MaxVersion = maxVersion;
        }
    }
}
