using Crawler.Domain.Entities;

namespace Crawler.Text.Extraction
{
    public interface ITextExtractor
    {
        bool HandlesProtocol(string text);
        Protocol ParseRawProtocol(string text);
    }
}
