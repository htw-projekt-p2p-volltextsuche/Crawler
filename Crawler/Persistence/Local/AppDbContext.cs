using Crawler.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Crawler.Persistence.Local
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Pointer> Pointers { get; set; }
    }
}
