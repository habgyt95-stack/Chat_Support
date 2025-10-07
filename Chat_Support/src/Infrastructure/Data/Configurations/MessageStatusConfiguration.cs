using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class MessageStatusConfiguration :IEntityTypeConfiguration<MessageStatus>
{
    public void Configure(EntityTypeBuilder<MessageStatus> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.Message)
            .WithMany(m => m.Statuses)
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();
    }
}
