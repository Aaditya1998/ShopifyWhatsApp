using Microsoft.EntityFrameworkCore;
using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CustomerConsent> CustomerConsents { get; set; }
    public DbSet<MessageLog> MessageLogs { get; set; }
    public DbSet<ConversationWindow> ConversationWindows { get; set; }

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

        base.OnModelCreating(modelBuilder);
    }
}
