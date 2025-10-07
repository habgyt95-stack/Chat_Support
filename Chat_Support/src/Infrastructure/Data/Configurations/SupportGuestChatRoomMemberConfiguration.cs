using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Chat_Support.Domain.Entities;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class SupportGuestChatRoomMemberConfiguration : IEntityTypeConfiguration<SupportGuestChatRoomMember>
{
    public void Configure(EntityTypeBuilder<SupportGuestChatRoomMember> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.GuestUser)
            .WithMany()
            .HasForeignKey(e => e.GuestUserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ChatRoom)
            .WithMany()
            .HasForeignKey(e => e.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.GuestUserId, e.ChatRoomId }).IsUnique();
    }
}
