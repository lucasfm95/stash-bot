using Microsoft.EntityFrameworkCore;

namespace StashBot.Infrastructure.Persistence;

public sealed class StashBotDbContext(DbContextOptions<StashBotDbContext> options) : DbContext(options)
{
    public DbSet<StoredMessage> Messages => Set<StoredMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredMessage>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(m => m.TelegramMessageId)
                .HasColumnName("telegram_message_id")
                .IsRequired();

            entity.Property(m => m.ChatId)
                .HasColumnName("chat_id")
                .IsRequired();

            entity.Property(m => m.FromUsername)
                .HasColumnName("from_username");

            entity.Property(m => m.FromFirstName)
                .HasColumnName("from_first_name");

            entity.Property(m => m.Text)
                .HasColumnName("text")
                .IsRequired();

            entity.Property(m => m.ReceivedAt)
                .HasColumnName("received_at")
                .HasDefaultValueSql("now()");
        });
    }
}
