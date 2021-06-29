using Crawler.Domain.Entities;
using Crawler.Exceptions;
using Crawler.Text.Extraction;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Crawler.Protocols.Extraction
{
    public class NineteenTextExtractor : ITextExtractor
    {
        public bool HandlesProtocolFile(string text)
        {
            return true;
        }

        public async Task<IEnumerable<Protocol>> ParseRawProtocolAsync(string text)
        {
            var tmpDirectory = "./tmp";
            Directory.CreateDirectory(tmpDirectory);

            var inputFile = Path.Combine(tmpDirectory, "file.xml");
            var outputFile = Path.Combine(tmpDirectory, "file.json");

            try
            {
                await File.WriteAllTextAsync(inputFile, text);

                ProcessStartInfo psi = new()
                {
                    FileName = "python3",
                    Arguments = $"text-extraction19.py {inputFile}"
                };

                Process p = Process.Start(psi);

                await p.WaitForExitAsync();

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
                Directory.Delete(tmpDirectory, recursive: true);
            }
        }
    }
}
