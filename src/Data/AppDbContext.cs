using Microsoft.EntityFrameworkCore;
using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CustomerConsent> CustomerConsents { get; set; }
    public DbSet<MessageLog> MessageLogs { get; set; }
    public DbSet<ConversationWindow> ConversationWindows { get; set; }
    public DbSet<ScheduledMessage> ScheduledMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CustomerConsent
        modelBuilder.Entity<CustomerConsent>(entity =>
        {
            entity.ToTable("customer_consents");
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
            entity.HasIndex(e => e.ShopifyCustomerId);
        });

        // MessageLog
        modelBuilder.Entity<MessageLog>(entity =>
        {
            entity.ToTable("message_logs");
            entity.HasIndex(e => e.PhoneNumber);
            entity.HasIndex(e => e.ShopifyOrderId);
            entity.HasIndex(e => e.SentAt);
            entity.HasIndex(e => e.WhatsAppMessageId);
        });

        // ConversationWindow
        modelBuilder.Entity<ConversationWindow>(entity =>
        {
            entity.ToTable("conversation_windows");
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
        });

        // ScheduledMessage
        modelBuilder.Entity<ScheduledMessage>(entity =>
        {
            entity.ToTable("scheduled_messages");
            entity.HasIndex(e => new { e.Status, e.ScheduledFor });
            entity.HasIndex(e => new { e.PhoneNumber, e.Status });
            entity.HasIndex(e => e.ShopifyCheckoutId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
