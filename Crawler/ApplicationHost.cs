using Crawler.Configuration;
using Crawler.Domain.Entities;
using Crawler.Exceptions;
using Crawler.Persistence.Mongo;
using Crawler.Protocols.Downloading;
using Crawler.Protocols.Indexing;
using Crawler.Protocols.Tracking;
using Crawler.Text.Extraction;

using Cronos;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler
{
    public class ApplicationHost : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ApplicationOptions> _options;
        private readonly ILogger<ApplicationHost> _logger;
        private readonly IEnumerable<ITextExtractor> _textExtractors;
        private readonly CronExpression _interval;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ApplicationHost(
            IServiceProvider serviceProvider,
            IOptions<ApplicationOptions> options,
            ILogger<ApplicationHost> logger,
            IEnumerable<ITextExtractor> textExtractors)
        {
            _serviceProvider = serviceProvider;
            _options = options;
            _logger = logger;
            _textExtractors = textExtractors;

            // Attempt to parse the cron expression
            try
            {
                _interval = CronExpression.Parse(_options.Value.Interval, CronFormat.Standard);
            }
            catch (Exception ex)
            {
                throw new CrawlerException("Unable to parse the interval cron expression.", ex);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () => await RunAsync());

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();

            return Task.CompletedTask;
        }

        public async Task RunAsync()
        {
            if (_options.Value.InitialDelay >= 0)
            {
                _logger.LogInformation($"Waiting for {_options.Value.InitialDelay} second(s) before entering indexing schedule.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.Value.InitialDelay), cancellationToken: _cancellationTokenSource.Token);
                }
                catch
                {
                    // Error or the cancellation token was cancelled, either way, exit the function
                    return;
                }
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var next = _interval.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                var timespan = next - DateTimeOffset.Now;

                // If we have no time to run, just exist this
                if (!timespan.HasValue)
                {
                    _logger.LogInformation("No time to run found.");

                    break;
                }

                _logger.LogInformation($"Next scheduled run at {next.Value}.");

                var delayTask = Task.Delay(timespan.Value, cancellationToken: _cancellationTokenSource.Token);

                try
                {
                    await delayTask;
                }
                catch
                {
                    // If we got here, our cancellation token got cancelled and we should stop running
                    break;
                }

                _logger.LogInformation("Running indexing.");

                try
                {
                    await WorkAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Indexing failed.");
                }

                _logger.LogInformation("Indexing ended.");
            }

            _logger.LogInformation("Exiting.");
        }

        public async Task WorkAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            var provider = scope.ServiceProvider;
            var providerService = provider.GetRequiredService<ProtocolProviderService>();
            var trackingService = provider.GetRequiredService<ProtocolTrackingService>();
            var indexApi = provider.GetRequiredService<IndexApi>();
            var mongo = provider.GetRequiredService<MongoContext>();

            // Get all protocol URIs and iterate
            await foreach (var uri in providerService.GetProtocolUrisAsync())
            {
                // All protocols that were extracted from this specific text file.
                List<Protocol> allProtocolsOfContainer = new List<Protocol>();

                // Assume the URI as the identifier for the purpose of determining whether this document has already been indexed
                var identifier = uri;

                // Check if this is the data of timeperiod we do not support
                const string pattern = @"pp(\d+)-data\.zip";
                var match = Regex.Match(uri, pattern);

                if (match.Success)
                {
                    try
                    {
                        var period = int.Parse(match.Groups[1].Value);

                        if (period < 18)
                        {
                            _logger.LogInformation($"Skipping {uri} because it is of an unsupported older time period.");
                            continue;
                        }
                    }
                    catch
                    {
                    }
                }

                try
                {
                    // Skip already indexed documents
                    if (await trackingService.IsIndexedAsync(identifier))
                    {
                        _logger.LogInformation($"{identifier} was already indexed, skipped.");

                        continue;
                    }

                    _logger.LogInformation($"Working on {uri}..");

                    // We might get back multiple protocol files here, because the download might be a zip!
                    _logger.LogInformation($"Downloading from uri and extracting..");
                    var protocolFileContents = providerService.GetRawProtocolsAsync(uri, _cancellationTokenSource.Token);

                    await foreach ((var protocolFileName, var protocolFileContent) in protocolFileContents)
                    {
                        _logger.LogInformation($"Working on file {protocolFileName}..");

                        // All protocols that were extracted from this specific text file.
                        IEnumerable<Protocol> protocols = new List<Protocol>();

                        // Find a suitable text extractor for this protocol format
                        foreach (var extractor in _textExtractors)
                        {
                            if (extractor.HandlesProtocolFile(protocolFileContent))
                            {
                                protocols = await extractor.ParseRawProtocolAsync(protocolFileContent);
                            }
                        }

                        // If we haven't found a text extractor, maybe a new format was created for the protocols that we do not support yet?
                        if (protocols == null)
                        {
                            throw new CrawlerException($"Found no text extractor that handles this protocol file!");
                        }

                        _logger.LogInformation($"Extracted {protocols.Count()} protocols from file {protocolFileName}.");
                        allProtocolsOfContainer.AddRange(protocols);
                    }

                    _logger.LogInformation($"{allProtocolsOfContainer.Count()} protocols have been extracted in total from {uri}.");
                    _logger.LogInformation($"Saving protocols to MongoDB storage..");

                    foreach (var protocol in allProtocolsOfContainer)
                    {
                        protocol.Id = Guid.NewGuid().ToString();

                        await mongo.AddProtocolAsync(protocol);
                    }

                    // Send the procotols to the indexing api
                    _logger.LogInformation($"Saving protocols to indexing cluster..");
                    await indexApi.IndexAsync(allProtocolsOfContainer);


                    // Mark this uri as done so we skip it entirely next time
                    _logger.LogInformation($"Marking {uri} has completely indexed..");
                    await trackingService.MarkAsIndexedAsync(identifier);

                    _logger.LogInformation($"Completed work on {uri}.");
                }
                catch (Exception ex)
                {
                    // Cleanup mongo db from all protocols we might have already saved
                    foreach (var protocol in allProtocolsOfContainer)
                    {
                        await mongo.RemoveProtocolAsync(protocol);
                    }

                    // Make sure we will attempt to index again next time
                    await trackingService.UnmarkAsIndexedAsync(identifier);

                    _logger.LogError(ex, $"Error occurred while attempting to work on the protocols file(s) of {uri}. View the enclosed exception for more details. MongoDB and local tracking database have been rolled back.");
                }
            }
        }
    }
}
