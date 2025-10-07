using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class MessageReactionConfiguration:IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Emoji).IsRequired().HasMaxLength(10);

        entity.HasOne(e => e.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.MessageId, e.UserId, e.Emoji }).IsUnique();
    }
}
