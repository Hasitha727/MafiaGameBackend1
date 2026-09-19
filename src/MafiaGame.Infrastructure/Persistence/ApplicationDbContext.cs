namespace MafiaGame.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using MafiaGame.Domain.Entities;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<GameRoom> GameRooms => Set<GameRoom>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();
    public DbSet<GameEventLog> GameEventLogs => Set<GameEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(100).IsRequired();
            entity.HasIndex(u => u.Username).IsUnique();
        });

        // PlayerGameStats Configuration (1-to-1 with User)
        modelBuilder.Entity<PlayerGameStats>(entity =>
        {
            entity.HasKey(s => s.UserId);
            entity.HasOne(s => s.User)
                  .WithOne(u => u.Stats)
                  .HasForeignKey<PlayerGameStats>(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // GameRoom Configuration
        modelBuilder.Entity<GameRoom>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.RoomCode).HasMaxLength(6).IsRequired();
            entity.HasIndex(r => r.RoomCode).IsUnique();
        });

        // RoomPlayer Configuration
        modelBuilder.Entity<RoomPlayer>(entity =>
        {
            entity.HasKey(rp => rp.Id);

            entity.HasOne(rp => rp.Room)
                  .WithMany(r => r.Players)
                  .HasForeignKey(rp => rp.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.User)
                  .WithMany()
                  .HasForeignKey(rp => rp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // GameEventLog Configuration
        modelBuilder.Entity<GameEventLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Room)
                  .WithMany(r => r.EventLogs)
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
