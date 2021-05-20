using MongoDB.Bson.Serialization.Attributes;

namespace Crawler.Domain.Entities
{
    public class Protocol
    {
        [BsonId]
        public string Id { get; set; }

        public string Title { get; set; }

        public string Speaker { get; set; }

        public string Affiliation { get; set; }

        public string Date { get; set; }

        public string Text { get; set; }
    }
}
