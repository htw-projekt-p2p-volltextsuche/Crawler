using Crawler.Configuration;
using Crawler.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Protocols.Indexing
{
    public class IndexApi
    {
        private readonly IOptions<ApplicationOptions> _options;
        private readonly ILogger<IndexApi> _logger;

        public IndexApi(IOptions<ApplicationOptions> options, ILogger<IndexApi> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task IndexAsync(IEnumerable<Protocol> protocols)
        {
            var client = new HttpClient();
            var url = $"{_options.Value.IndexingApiEndpoint}/index/speeches";

            _logger.LogInformation($"Sending POST with {protocols.Count()} protocols to {url}..");
            var response = await client.PostAsJsonAsync(url, protocols, CancellationToken.None);

            _logger.LogInformation($"Returned status code = {response.StatusCode}.");
            response.EnsureSuccessStatusCode();
        }
    }
}
