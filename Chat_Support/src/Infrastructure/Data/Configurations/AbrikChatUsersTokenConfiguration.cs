using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class AbrikChatUsersTokenConfiguration : IEntityTypeConfiguration<AbrikChatUsersToken>
{
    public void Configure(EntityTypeBuilder<AbrikChatUsersToken> builder)
    {
        builder.ToTable("AbrikChatUsersTokens");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DeviceId).HasMaxLength(100);
        builder.Property(e => e.RefreshToken).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IssuedAt).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.IsRevoked).IsRequired();

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.DeviceId });
    }
}
