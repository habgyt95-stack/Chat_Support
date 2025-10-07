using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class UserConnectionConfiguration:IEntityTypeConfiguration<UserConnection>
{
    public void Configure(EntityTypeBuilder<UserConnection> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ConnectionId).IsRequired().HasMaxLength(100);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ConnectionId).IsUnique();
        entity.HasIndex(e => e.UserId);
    }
}
