using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class ChatRoomMemberConfiguration : IEntityTypeConfiguration<ChatRoomMember>
{
    public void Configure(EntityTypeBuilder<ChatRoomMember> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ChatRoom)
            .WithMany(c => c.Members)
            .HasForeignKey(e => e.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.UserId, e.ChatRoomId }).IsUnique();
    }
}
