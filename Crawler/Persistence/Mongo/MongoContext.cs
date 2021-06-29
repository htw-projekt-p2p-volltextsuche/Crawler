using Crawler.Configuration;
using Crawler.Domain.Entities;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

using System.Threading.Tasks;

namespace Crawler.Persistence.Mongo
{
    public class MongoContext
    {
        private readonly IOptions<ApplicationOptions> _options;
        private readonly MongoClient _mongoClient;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Protocol> _protocols;

        public MongoContext(IOptions<ApplicationOptions> options)
        {
            _options = options;

            _mongoClient = new MongoClient(_options.Value.MongoConnectionString);
            _database = _mongoClient.GetDatabase(_options.Value.MongoDatabase);
            _protocols = _database.GetCollection<Protocol>("protocols");
        }

        public async Task AddProtocolAsync(Protocol protocol)
        {
            await _protocols.InsertOneAsync(protocol);
        }

        public async Task RemoveProtocolAsync(Protocol protocol)
        {
            await _protocols.DeleteOneAsync(x => x.Id == protocol.Id);
        }
    }
}
