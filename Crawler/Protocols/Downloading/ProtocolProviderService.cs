using AngleSharp;

using Crawler.Exceptions;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Protocols.Downloading
{
    public class ProtocolProviderService
    {
        public async IAsyncEnumerable<string> GetProtocolUrisAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = new HttpClient();

            {
                // Get all protocols in new format
                int offset = 0;
                const int step = 10;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = await client.GetAsync($"https://www.bundestag.de/ajax/filterlist/de/services/opendata/543410-543410?limit={step}&noFilterSet=true&offset={offset}", cancellationToken);
                    var content = await request.Content.ReadAsStringAsync(cancellationToken);

                    // End of the list
                    if (content.Contains("Leider keine Ergebnisse gefunden!")) break;

                    // Extract uris of protocol files from the response
                    var document = await BrowsingContext.New(AngleSharp.Configuration.Default).OpenAsync(request => request.Content(content));

                    // Find all links to documents in the HTML document
                    var linkElements = document.QuerySelectorAll("a[class=bt-link-dokument]");

                    foreach (var linkElement in linkElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var uri = linkElement.GetAttribute("href");

                        yield return $"https://www.bundestag.de{uri}";
                    }

                    // Increase offset to next page
                    offset += step;

                    // Artifical delay to not hit the server too hard
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }



            {
                // Get all protocols in old format
                int offset = 0;
                const int step = 10;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = await client.GetAsync($"https://www.bundestag.de/ajax/filterlist/de/services/opendata/488214-488214?limit={step}&noFilterSet=true&offset={offset}", cancellationToken);
                    var content = await request.Content.ReadAsStringAsync(cancellationToken);

                    // End of the list
                    if (content.Contains("Leider keine Ergebnisse gefunden!")) break;

                    // Extract uris of protocol files from the response
                    var document = await BrowsingContext.New(AngleSharp.Configuration.Default).OpenAsync(request => request.Content(content));

                    // Find all links to documents in the HTML document
                    var linkElements = document.QuerySelectorAll("a[class=bt-link-dokument]");

                    foreach (var linkElement in linkElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var uri = linkElement.GetAttribute("href");

                        yield return $"https://www.bundestag.de{uri}";
                    }

                    // Increase offset to next page
                    offset += step;

                    // Artifical delay to not hit the server too hard
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        public async IAsyncEnumerable<(string FileName, string Content)> GetRawProtocolsAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var httpClient = new HttpClient();


            var isZip = uri.ToLower().EndsWith(".zip");
            var isXml = uri.ToLower().EndsWith(".xml");

            if (!isZip && !isXml)
            {
                throw new CrawlerException($"Unable to determine file extension of {uri} (it must be .xml or .zip).");
            }

            var response = await httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode) throw new CrawlerException($"Server returned error status code when attempting to access {uri}.");

            if (isXml)
            {
                yield return (uri.Split("/").Last(), await response.Content.ReadAsStringAsync(cancellationToken));
            }
            else if (isZip)
            {
                Directory.CreateDirectory("./tmp");

                try
                {
                    var zipData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    var zipFilePath = $"./tmp/protocols.zip";
                    var contentPath = $"./tmp/files";

                    await File.WriteAllBytesAsync(zipFilePath, zipData);
                    ZipFile.ExtractToDirectory(zipFilePath, contentPath);

                    var files = Directory.GetFiles(contentPath, "*.xml");

                    foreach (var file in files)
                    {
                        yield return (Path.GetFileName(file), await File.ReadAllTextAsync(file));
                    }
                }
                finally
                {
                    Directory.Delete("./tmp", true);
                }
            }
        }
    }
}
