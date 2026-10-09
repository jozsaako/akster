using Microsoft.EntityFrameworkCore;
using PetSitting.Domain.Common;
using PetSitting.Domain.Users;
using PetSitting.Domain.Pets;
using PetSitting.Domain.Availability;

namespace PetSitting.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Message> Messages => Set<Message>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<PetPicture> PetPictures => Set<PetPicture>();
    public DbSet<SitterProfile> SitterProfiles => Set<SitterProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One-to-One: SitterProfile with User (by id, no navigation across subsystems)
        modelBuilder.Entity<SitterProfile>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<SitterProfile>(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-Many: User with RefreshTokens
        modelBuilder.Entity<User>()
            .HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-Many: User with Pets
        modelBuilder.Entity<User>()
            .HasMany(u => u.Pets)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-Many: Pet with PetPictures
        modelBuilder.Entity<Pet>()
            .HasMany(p => p.Pictures)
            .WithOne(pp => pp.Pet)
            .HasForeignKey(pp => pp.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
