using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class ChatMessageConfiguration: IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Content).IsRequired();
        entity.Property(e => e.AttachmentUrl).HasMaxLength(1000);
        entity.Property(e => e.AttachmentType).HasMaxLength(100);
        
        // Make sender relationship optional
        entity.HasOne(e => e.Sender)
            .WithMany()
            .HasForeignKey(e => e.SenderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ChatRoom)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(e => e.ReplyToMessageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        entity.HasIndex(e => e.ChatRoomId);
        entity.HasIndex(e => e.SenderId);
        entity.HasIndex(e => e.Created);
    }
}
