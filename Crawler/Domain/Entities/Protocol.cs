using MongoDB.Bson.Serialization.Attributes;

using System.Text.Json.Serialization;

namespace Crawler.Domain.Entities
{
    public class Protocol
    {
        [BsonId]
        [JsonPropertyName("doc_id")]
        public string Id { get; set; }

        [BsonElement("title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [BsonElement("speaker")]
        [JsonPropertyName("speaker")]
        public string Speaker { get; set; }

        [BsonElement("affiliation")]
        [JsonPropertyName("affiliation")]
        public string Affiliation { get; set; }

        [BsonElement("date")]
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [BsonElement("text")]
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
