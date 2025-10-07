using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class SupportAgentConfiguration : IEntityTypeConfiguration<SupportAgent>
{
    public void Configure(EntityTypeBuilder<SupportAgent> builder)
    {
        // این جدول باید در دیتابیس ایجاد شود
        builder.ToTable("Support_Agents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .UseIdentityColumn();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.MaxConcurrentChats)
            .HasDefaultValue(5);

        builder.Property(e => e.LastActivityAt)
            .HasColumnType("datetime");

        builder.HasOne(e => e.User)
            .WithOne(u => u.SupportAgent)
            .HasForeignKey<SupportAgent>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AssignedTickets)
            .WithOne(t => t.AssignedAgent)
            .HasForeignKey(t => t.AssignedAgentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.UserId)
            .IsUnique();

        builder.HasIndex(e => e.IsActive);
    }
}
