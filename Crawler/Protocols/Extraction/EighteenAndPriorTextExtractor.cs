using Crawler.Domain.Entities;
using Crawler.Exceptions;
using Crawler.Text.Extraction;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Crawler.Protocols.Extraction
{
    public class EighteenAndPriorTextExtractor : ITextExtractor
    {
        private readonly ILogger<EighteenAndPriorTextExtractor> _logger;

        public EighteenAndPriorTextExtractor(ILogger<EighteenAndPriorTextExtractor> logger)
        {
            _logger = logger;
        }

        public bool HandlesProtocolFile(string text)
        {
            return text.Contains("<DOKUMENT>") && text.Contains("<WAHLPERIODE>") && text.Contains("<DOKUMENTART>");
        }

        public async Task<IEnumerable<Protocol>> ParseRawProtocolAsync(string text)
        {
            var tmpDirectory = "./18-tmp";
            Directory.CreateDirectory(tmpDirectory);

            var inputFile = Path.Combine(tmpDirectory, "file.xml");
            var outputFile = Path.Combine("file.json");

            try
            {
                await File.WriteAllTextAsync(inputFile, text);

                ProcessStartInfo psi = new()
                {
                    FileName = "java",
                    Arguments = $"-jar textextraction-18.jar \"{inputFile}\""
                };

                _logger.LogInformation(Environment.NewLine + "OUTPUT OF EXTRACTOR --------------------- " + Environment.NewLine);
                Process p = Process.Start(psi);

                await p.WaitForExitAsync();
                _logger.LogInformation(Environment.NewLine + "----------------------------------------- " + Environment.NewLine);

                if (p.ExitCode != 0)
                {
                    throw new CrawlerException($"Cannot index protocol file. Text extractor returned error code {p.ExitCode}.");
                }

                var outputText = await File.ReadAllTextAsync(outputFile);
                var protocols = JsonSerializer.Deserialize<List<Protocol>>(outputText);

                return protocols;
            }
            catch (Exception ex)
            {
                throw new CrawlerException("Error during execution of indexing of protocol file.", ex);
            }
            finally
            {
                if (File.Exists(outputFile)) File.Delete(outputFile);

                Directory.Delete(tmpDirectory, recursive: true);
            }
        }

    }
}
