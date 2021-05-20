using Crawler.Configuration;
using Crawler.Domain.Entities;
using Crawler.Exceptions;
using Crawler.Protocols.Downloading;
using Crawler.Protocols.Tracking;

using Cronos;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler
{
    public class ApplicationHost : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ApplicationOptions> _options;
        private readonly ILogger<ApplicationHost> _logger;
        private readonly CronExpression _interval;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ApplicationHost(IServiceProvider serviceProvider, IOptions<ApplicationOptions> options, ILogger<ApplicationHost> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options;
            _logger = logger;

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

            await foreach (var uri in providerService.GetProtocolUrisAsync())
            {
                var identifier = uri;

                if (await trackingService.IsIndexedAsync(identifier))
                {
                    _logger.LogInformation($"{uri} was already indexed, skipped.");

                    continue;
                }

                try
                {
                    _logger.LogInformation($"Downloading {uri}..");

                    var protocolTexts = providerService.GetRawProtocolsAsync(uri, _cancellationTokenSource.Token);

                    await foreach (var protocolText in protocolTexts)
                    {
                        _logger.LogInformation($"DEBUG: {protocolText.Substring(0, 20)}..");
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
