using backend.Availability.Models;
using backend.Models;
using backend.Identity.Models;
using backend.Pets.Models;
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
        public DbSet<Pet> Pets => Set<Pet>();
        public DbSet<PetPicture> PetPictures => Set<PetPicture>();
        public DbSet<SitterAvailability> SitterAvailabilities => Set<SitterAvailability>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SitterAvailability>()
                .HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<SitterAvailability>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
