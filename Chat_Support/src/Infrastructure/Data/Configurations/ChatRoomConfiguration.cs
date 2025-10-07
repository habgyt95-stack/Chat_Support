using Chat_Support.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.Avatar).HasMaxLength(500);
     
        entity.HasOne<KciUser>()
            .WithMany(u => u.CreatedChatRooms)
            .HasForeignKey(e => e.CreatedById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Region)
            .WithMany()
            .HasForeignKey(e => e.RegionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.CreatedById);
    }
}
