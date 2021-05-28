using Crawler.Domain.Entities;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawler.Text.Extraction
{
    public interface ITextExtractor
    {
        bool HandlesProtocolFile(string text);
        Task<IEnumerable<Protocol>> ParseRawProtocolAsync(string text);
    }
}
