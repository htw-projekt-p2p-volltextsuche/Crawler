using Crawler.Domain.Entities;

using System.Threading.Tasks;

namespace Crawler.Text.Extraction
{
    public interface ITextExtractor
    {
        bool HandlesProtocol(string text);
        Task<Protocol> ParseRawProtocolAsync(string text);
    }
}
