using ChatApp.Domain.Entities;
using ChatApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>           Users           => Set<User>();
    public DbSet<Room>           Rooms           => Set<Room>();
    public DbSet<RoomMember>     RoomMembers     => Set<RoomMember>();
    public DbSet<Message>        Messages        => Set<Message>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ──────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).IsRequired().HasMaxLength(50);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
            e.Property(u => u.AvatarColor).HasMaxLength(7).HasDefaultValue("#6366f1");
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // ── Room ──────────────────────────────────────────────────────────
        mb.Entity<Room>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(100);
            e.Property(r => r.Type).HasConversion<string>();
            e.HasOne(r => r.CreatedBy)
             .WithMany()
             .HasForeignKey(r => r.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── RoomMember (composite PK join table) ──────────────────────────
        mb.Entity<RoomMember>(e =>
        {
            e.HasKey(rm => new { rm.UserId, rm.RoomId });
            e.Property(rm => rm.Role).HasConversion<string>();
            e.HasOne(rm => rm.User)
             .WithMany(u => u.RoomMemberships)
             .HasForeignKey(rm => rm.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rm => rm.Room)
             .WithMany(r => r.Members)
             .HasForeignKey(rm => rm.RoomId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Message ───────────────────────────────────────────────────────
        mb.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Content).IsRequired().HasMaxLength(4000);
            e.Property(m => m.Type).HasConversion<string>();
            e.HasOne(m => m.User)
             .WithMany(u => u.Messages)
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Room)
             .WithMany(r => r.Messages)
             .HasForeignKey(m => m.RoomId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.RoomId, m.CreatedAt });
            e.HasQueryFilter(m => !m.IsDeleted);
        });

        // ── UserConnection ────────────────────────────────────────────────
        mb.Entity<UserConnection>(e =>
        {
            e.HasKey(uc => uc.Id);
            e.Property(uc => uc.ConnectionId).IsRequired().HasMaxLength(256);
            e.HasIndex(uc => uc.ConnectionId);
            e.HasOne(uc => uc.User)
             .WithMany(u => u.Connections)
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(uc => !uc.IsDeleted);
        });
    }
}
