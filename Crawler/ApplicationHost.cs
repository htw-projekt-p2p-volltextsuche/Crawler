using Crawler.Configuration;
using Crawler.Domain.Entities;
using Crawler.Exceptions;
using Crawler.Extensions;
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

            if (_options.Value.MaximumBatchSize <= 0)
            {
                throw new CrawlerException("MaximumBatchSize must be atleast set to 1.");
            }

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

                    // This variable is used to determine whether we should mark "identifier" as indexed at the end of this run.
                    // We might NOT want to mark it as indexed if there was an error with atleast one protocol (e.g. its invalid, or couldnt be saved), so that we will
                    // retry on the next scheduled run of the crawler.
                    bool markResourceAsIndexed = true;

                    // We might get back multiple protocol files here, because the download might be a zip!
                    _logger.LogInformation($"Downloading from uri and extracting..");
                    var protocolFileContents = providerService.GetRawProtocolsAsync(uri, _cancellationTokenSource.Token);

                    await foreach ((var protocolFileName, var protocolFileContent) in protocolFileContents)
                    {
                        _logger.LogInformation($"Working on file {protocolFileName}..");

                        // All protocols that were extracted from this specific text file.
                        List<Protocol> protocols = new List<Protocol>();

                        // Find a suitable text extractor for this protocol format
                        foreach (var extractor in _textExtractors)
                        {
                            if (extractor.HandlesProtocolFile(protocolFileContent))
                            {
                                protocols = (await extractor.ParseRawProtocolAsync(protocolFileContent))?.ToList();
                            }
                        }

                        // If we haven't found a text extractor, maybe a new format was created for the protocols that we do not support yet?
                        if (protocols == null)
                        {
                            throw new CrawlerException($"Found no text extractor that handles this protocol file!");
                        }

                        _logger.LogInformation($"Extracted {protocols.Count} protocols from file {protocolFileName}.");
                        allProtocolsOfContainer.AddRange(protocols);
                    }

                    _logger.LogInformation($"{allProtocolsOfContainer.Count()} protocols have been extracted in total from {uri}.");

                    // Check each protocol if is has already been indexed and exclude it
                    // ALSO exclude empty documents with invalid data!!
                    var tmpProtocols = new List<Protocol>();

                    _logger.LogInformation("Checking for already indexed or invalid protocols..");

                    // For displaying informational texts
                    int amountInvalidProtocols = 0;
                    int amountAlreadyIndexedProtocols = 0;

                    foreach (var protocol in allProtocolsOfContainer)
                    {
                        // Create a hash of the protocol text content and check if we have already indexed this
                        var hash = trackingService.Hash(protocol.Text);
                        var isInvalid = string.IsNullOrWhiteSpace(protocol.Speaker) || string.IsNullOrWhiteSpace(protocol.Title) || string.IsNullOrWhiteSpace(protocol.Text);

                        if (isInvalid)
                        {
                            //_logger.LogWarning($"Parsed protocol was invalid (empty text). Skipping. This resource will be reattempted again.");
                            amountInvalidProtocols++;
                            markResourceAsIndexed = false;

                            continue;
                        }

                        if (await trackingService.IsIndexedAsync(hash))
                        {
                            //_logger.LogInformation("Parsed protocol was already indexed. Skipping.");
                            amountAlreadyIndexedProtocols++;

                            continue;
                        }

                        tmpProtocols.Add(protocol);
                    }

                    if (amountInvalidProtocols > 0) _logger.LogWarning($"{amountInvalidProtocols} invalid protocols will have to be reattempted.");
                    if (amountAlreadyIndexedProtocols > 0) _logger.LogInformation($"{amountAlreadyIndexedProtocols} already indexed protocols of this resource will be skipped.");

                    allProtocolsOfContainer = tmpProtocols;

                    _logger.LogInformation("Splitting protocols into chunks..");

                    // Put all protocols into a queue so we can work on it in chunks
                    var chunks = allProtocolsOfContainer.Split(_options.Value.MaximumBatchSize).ToList();
                    int chunksAmount = chunks.Count;
                    int currentChunkNumber = 1;

                    if (chunks.Count > 0)
                    {
                        _logger.LogInformation($"Split into {chunks.Count} chunks.");

                        foreach (var chunk in chunks)
                        {
                            var chunkProtocols = chunk.ToList();

                            try
                            {
                                _logger.LogInformation($"Working on chunk {currentChunkNumber} out of {chunksAmount}..");
                                _logger.LogInformation($"[Chunk {currentChunkNumber}/{chunksAmount}] Took {chunkProtocols.Count} protocols for this chunk.");
                                _logger.LogInformation($"[Chunk {currentChunkNumber}/{chunksAmount}] Saving protocols of chunk to MongoDB storage and marking each as indexed..");

                                foreach (var protocol in chunkProtocols)
                                {
                                    protocol.Id = Guid.NewGuid().ToString();

                                    await mongo.AddProtocolAsync(protocol);

                                    // Mark this uri as done so we skip it entirely next time
                                    var protocolIdentifier = trackingService.Hash(protocol.Text);

                                    await trackingService.MarkAsIndexedAsync(protocolIdentifier);
                                }

                                // Send the procotols to the indexing api
                                _logger.LogInformation($"[Chunk {currentChunkNumber}/{chunksAmount}] Saving protocols of chunk to indexing cluster..");
                                await indexApi.IndexAsync(chunkProtocols);

                                _logger.LogInformation($"[Chunk {currentChunkNumber}/{chunksAmount}] Chunk completed successfully.");
                            }
                            catch (Exception ex)
                            {
                                markResourceAsIndexed = false;

                                _logger.LogError(ex, $"Error occurred while attempting to work on {chunkProtocols.Count} protocols of the current chunk. View the enclosed exception for more details. Changes have been rolled back. All protocols of this chunk will be reattempted next run.");

                                // Cleanup mongo db from all protocols of the failed chunk we might have already saved
                                foreach (var protocol in chunkProtocols)
                                {
                                    await mongo.RemoveProtocolAsync(protocol);

                                    // Make sure we will attempt to index each protocol of the chunk again next time
                                    var protocolIdentifier = trackingService.Hash(protocol.Text);
                                    await trackingService.UnmarkAsIndexedAsync(protocolIdentifier);
                                }
                            }

                            currentChunkNumber++;
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No chunks created because there are no protocols to work on.");
                    }

                    if (markResourceAsIndexed)
                    {
                        await trackingService.MarkAsIndexedAsync(identifier);
                        _logger.LogInformation($"All protocols of {uri} completed without issues. The resource has been marked as indexed.");
                    }
                    else
                    {
                        _logger.LogWarning($"Atleast one protocol of the resource {uri} failed to index. Not saving this resource as indexed. All failed protocols will be reattempted next run.");
                    }


                }
                catch (Exception ex)
                {
                    // If we got here, make sure we mark this entire resource as not indexed. We will thus attempt to reindex it entirely next time. Any chunk
                    // that might have successfully indexed will be skipped, so no duplicates will be created regardless!
                    await trackingService.UnmarkAsIndexedAsync(identifier);

                    _logger.LogError(ex, $"Error occurred while attempting to work on the protocols file(s) of {uri}. View the enclosed exception for more details. MongoDB and local tracking database have been rolled back where necessary.");
                }
            }
        }
    }
}
