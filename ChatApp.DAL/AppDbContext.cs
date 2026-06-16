using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.DAL;

/// <summary>
/// EF Core DbContext — currently contains only the Users table.
/// We will add more tables (Rooms, Messages) in later phases via migrations.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);

            e.Property(u => u.Username)
             .IsRequired()
             .HasMaxLength(50);

            e.Property(u => u.Email)
             .IsRequired()
             .HasMaxLength(150);

            e.Property(u => u.PasswordHash)
             .IsRequired();

            // Unique constraints — prevent duplicate registrations
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
        });
    }
}
