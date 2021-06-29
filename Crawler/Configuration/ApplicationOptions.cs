namespace Crawler.Configuration
{
    public class ApplicationOptions
    {
        public string Interval { get; set; }

        // MongoDb
        public string MongoConnectionString { get; set; }
        public string MongoDatabase { get; set; }

        // Local database
        public string LocalDbConnectionString { get; set; }


        // Indexing
        public string IndexingApiEndpoint { get; set; }
    }
}
