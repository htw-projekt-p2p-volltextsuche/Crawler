using Crawler.Persistence.Local;

using Microsoft.EntityFrameworkCore;

using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            var pointer = _appDbContext.Pointers.CreateProxy();

            pointer.Identifier = identifier;

            await _appDbContext.Pointers.AddAsync(pointer);
            await _appDbContext.SaveChangesAsync();
        }

        public async Task UnmarkAsIndexedAsync(string identifier)
        {
            var pointer = await _appDbContext.Pointers.FirstOrDefaultAsync(x => x.Identifier == identifier);

            if (pointer == null) return;

            _appDbContext.Pointers.Remove(pointer);

            await _appDbContext.SaveChangesAsync();
        }

        public string Hash(string text)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2"));
                }

                return sb.ToString();
            }
        }
    }
}
