using MongoDB.Bson.Serialization.Attributes;

using System.Text.Json.Serialization;

namespace Crawler.Domain.Entities
{
    public class Protocol
    {
        [BsonId]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; }

        [JsonPropertyName("affiliation")]
        public string Affiliation { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
