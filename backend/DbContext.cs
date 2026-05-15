using backend.Models;
using backend.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace backend
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Message> Messages => Set<Message>();
        public DbSet<User> Users => Set<User>();
        public DbSet<backend.Identity.Models.RefreshToken> RefreshTokens => Set<backend.Identity.Models.RefreshToken>();
    }
}
