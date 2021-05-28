using Crawler.Configuration;
using Crawler.Exceptions;
using Crawler.Persistence.Local;
using Crawler.Persistence.Mongo;
using Crawler.Protocols.Downloading;
using Crawler.Protocols.Extraction;
using Crawler.Protocols.Tracking;
using Crawler.Text.Extraction;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

namespace Crawler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    logger.LogInformation("Migrating database if necessary.");
                    await db.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    throw new CrawlerException("Could not migrate local database.", ex);
                }

                try
                {
                    await host.RunAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Fatal exception occurred.");
                }
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Entry point of application
                    services.AddHostedService<ApplicationHost>();

                    // Add configuration to DI container
                    services.Configure<ApplicationOptions>(context.Configuration);

                    // Mongo database service
                    services.AddSingleton<MongoContext>();

                    // Add the local database that is used to keep track of which protocols have already been processed
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options
                            .UseSqlite(services.BuildServiceProvider().GetService<IOptions<ApplicationOptions>>().Value.LocalDbConnectionString)
                            .UseLazyLoadingProxies();
                    });

                    // Service to keep track of which speeches have already been indexed
                    services.AddScoped<ProtocolTrackingService>();

                    // Service to find and download protocols
                    services.AddScoped<ProtocolProviderService>();

                    // Text extractor services
                    services.AddScoped<ITextExtractor, NineteenTextExtractor>();
                })
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    // Primarily load from configuration file
                    configuration
                        .AddJsonFile("appsettings.json")
                        // Allow overwrites by file in a docker container (this file should not be tracked by git to avoid
                        // production secrets being tracked in git.
                        .AddJsonFile("appsettings.local.json", optional: true);

                    // Allow overwrites via env variables
                    configuration.AddEnvironmentVariables();

                    // Allow overwrites via command line arguments with highest priority
                    configuration.AddCommandLine(args);
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
                });
    }
}
