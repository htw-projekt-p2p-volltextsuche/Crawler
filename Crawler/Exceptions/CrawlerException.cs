using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crawler.Exceptions
{
    public class CrawlerException : Exception
    {
        public CrawlerException(string message) : base(message)
        {
        }

        public CrawlerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
