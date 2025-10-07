using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class ChatFileMetadataConfiguration : IEntityTypeConfiguration<ChatFileMetadata>
{
    public void Configure(EntityTypeBuilder<ChatFileMetadata> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(e => e.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.FileSize)
            .IsRequired();

        entity.Property(e => e.UploadedDate)
            .IsRequired();

        entity.HasOne(e => e.ChatRoom)
            .WithMany()
            .HasForeignKey(e => e.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.UploadedBy)
            .WithMany()
            .HasForeignKey(e => e.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.ChatRoomId);
        entity.HasIndex(e => e.UploadedById);
        entity.HasIndex(e => e.UploadedDate);
    }
}
