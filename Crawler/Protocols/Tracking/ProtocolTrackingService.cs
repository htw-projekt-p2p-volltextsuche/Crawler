using Crawler.Persistence.Local;

using Microsoft.EntityFrameworkCore;

using System.Linq;
using System.Threading.Tasks;

namespace Crawler.Protocols.Tracking
{
    public class ProtocolTrackingService
    {
        private readonly AppDbContext _appDbContext;

        public ProtocolTrackingService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<bool> IsIndexedAsync(string identifier)
        {
            return await _appDbContext.Pointers.AnyAsync(x => x.Identifier == identifier);
        }

        public async Task MarkAsIndexedAsync(string identifier)
        {
            _appDbContext.Pointers.RemoveRange(await _appDbContext.Pointers.Where(x => x.Identifier == identifier).ToListAsync());
            await _appDbContext.SaveChangesAsync();
        }

        //private string Hash(string text)
        //{
        //    using (SHA1Managed sha1 = new SHA1Managed())
        //    {
        //        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
        //        var sb = new StringBuilder(hash.Length * 2);

        //        foreach (byte b in hash)
        //        {
        //            sb.Append(b.ToString("X2"));
        //        }

        //        return sb.ToString();
        //    }
        //}
    }
}
