using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class UserFcmTokenInfoMobileAbrikChatConfiguration : IEntityTypeConfiguration<UserFcmTokenInfoMobileAbrikChat>
{
    public void Configure(EntityTypeBuilder<UserFcmTokenInfoMobileAbrikChat> builder)
    {
        builder.ToTable("UserFcmTokenInfoMobileAbrikChat");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.FcmToken).HasMaxLength(1000);
        builder.Property(e => e.DeviceId).HasMaxLength(200);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.DeviceId });
    }
}
