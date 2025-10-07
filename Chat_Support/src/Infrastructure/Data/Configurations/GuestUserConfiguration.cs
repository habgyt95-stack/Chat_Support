using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class GuestUserConfiguration : IEntityTypeConfiguration<GuestUser>
{
    public void Configure(EntityTypeBuilder<GuestUser> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.Email).HasMaxLength(256);
        entity.Property(e => e.Phone).HasMaxLength(20);
        entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
        entity.Property(e => e.UserAgent).HasMaxLength(500);

        entity.HasIndex(e => e.SessionId).IsUnique();
        entity.HasIndex(e => e.Email);
    }
}
