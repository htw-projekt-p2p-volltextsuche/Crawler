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
                // Assume the URI as the identifier for the purpose of determining whether this document has already been indexed
                var identifier = uri;

                // Skip already indexed documents
                if (await trackingService.IsIndexedAsync(identifier))
                {
                    _logger.LogInformation($"{uri} was already indexed, skipped.");

                    continue;
                }

                try
                {
                    _logger.LogInformation($"Working on {uri}.");

                    // Download the protocol or protocols (in case it is a zip)
                    var protocolTexts = providerService.GetRawProtocolsAsync(uri, _cancellationTokenSource.Token);

                    await foreach (var protocolText in protocolTexts)
                    {
                        try
                        {
                            IEnumerable<Protocol> protocols = null;

                            // Find a suitable text extractor for this protocol format
                            foreach (var extractor in _textExtractors)
                            {
                                if (extractor.HandlesProtocolFile(protocolText))
                                {
                                    protocols = await extractor.ParseRawProtocolAsync(protocolText);
                                }
                            }

                            // If we haven't found a text extractor, maybe a new format was created for the protocols that we do not support yet?
                            if (protocols == null)
                            {
                                _logger.LogWarning($"Found no text extractor that handles this protocol file!");
                                continue;
                            }

                            // Generate GUIDs for each protocol
                            foreach (var protocol in protocols)
                            {
                                protocol.Id = Guid.NewGuid().ToString();
                            }

                            _logger.LogInformation($"{protocols.Count()} protocols have been extracted.");

                            try
                            {
                                await trackingService.MarkAsIndexedAsync(identifier);

                                _logger.LogInformation($"Saving protocols to MongoDB storage..");

                                foreach (var protocol in protocols)
                                {
                                    // Save the text in full for easy access by the frontend to display to the user
                                    await mongo.AddProtocolAsync(protocol);

                                    _logger.LogInformation($"Added protocol {protocol.Id} (speaker: {protocol.Speaker}, affiliation: {protocol.Affiliation}).");
                                }

                                // Send the procotols to the indexing api
                                await indexApi.IndexAsync(protocols);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurred while attempting to save protocols to the storages. View the enclosed exception for more details. Changes have been rolled back.");

                                // Cleanup mongo db
                                foreach (var protocol in protocols)
                                {
                                    await mongo.RemoveProtocolAsync(protocol);
                                }

                                await trackingService.UnmarkAsIndexedAsync(identifier);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to extract data.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Indexing of {uri} failed. Documents of this resource have not been indexed.");
                }
            }
        }
    }
}
