namespace Crawler.Configuration
{
    public class ApplicationOptions
    {
        public string Interval { get; set; } = "* * * * *";

        public long InitialDelay { get; set; } = 0;

        public int MaximumBatchSize { get; set; } = 100;

        // MongoDb
        public string MongoConnectionString { get; set; }
        public string MongoDatabase { get; set; }
        public string MongoCollection { get; set; }

        // Local database
        public string LocalDbConnectionString { get; set; }


        // Indexing
        public string IndexingApiEndpoint { get; set; }
        public long IndexingApiTimeout { get; set; } = 300;
    }
}
