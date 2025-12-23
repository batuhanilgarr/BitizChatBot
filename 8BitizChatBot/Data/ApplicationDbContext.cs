using Microsoft.EntityFrameworkCore;
using BitizChatBot.Models;

namespace BitizChatBot.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdminSettingsEntity> AdminSettings { get; set; }
    public DbSet<DomainApiKeyEntity> DomainApiKeys { get; set; }
    public DbSet<DomainAppearanceEntity> DomainAppearances { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessageEntity> ChatMessages { get; set; }
    public DbSet<ConversationContextEntity> ConversationContexts { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<AuditLogEntity> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AdminSettings - tek satır olacak
        modelBuilder.Entity<AdminSettingsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.ToTable("AdminSettings");
        });

        // DomainApiKeys
        modelBuilder.Entity<DomainApiKeyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Domain).IsUnique();
            entity.ToTable("DomainApiKeys");
        });

        // DomainAppearances
        modelBuilder.Entity<DomainAppearanceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Domain).IsUnique();
            entity.ToTable("DomainAppearances");
        });

        // ChatSessions
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Domain);
            entity.ToTable("ChatSessions");
        });

        // ChatMessages
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("ChatMessages");
        });

        // ConversationContexts
        modelBuilder.Entity<ConversationContextEntity>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.HasIndex(e => e.LastActivity);
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("ConversationContexts");
        });

        // Users
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.ToTable("Users");
        });

        // AuditLogs
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.ToTable("AuditLogs");
        });
    }
}

