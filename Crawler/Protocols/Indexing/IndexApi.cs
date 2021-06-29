using Crawler.Configuration;
using Crawler.Domain.Entities;

using Microsoft.Extensions.Options;

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Protocols.Indexing
{
    public class IndexApi
    {
        private readonly IOptions<ApplicationOptions> _options;

        public IndexApi(IOptions<ApplicationOptions> options)
        {
            _options = options;
        }

        public async Task IndexAsync(IEnumerable<Protocol> protocols)
        {
            var client = new HttpClient();
            var url = $"{_options.Value.IndexingApiEndpoint}/index/speeches";

            var response = await client.PostAsJsonAsync(url, protocols, CancellationToken.None);

            response.EnsureSuccessStatusCode();
        }
    }
}
