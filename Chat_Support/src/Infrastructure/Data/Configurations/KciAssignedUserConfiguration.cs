using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class KciAssignedUserConfiguration : IEntityTypeConfiguration<KciAssignedUser>
{
    public void Configure(EntityTypeBuilder<KciAssignedUser> builder)
    {
        builder.Property(e => e.Id).HasComment("کلید");
        builder.Property(e => e.GroupId).HasComment("گروه کاربری - کلید به جدول kci_groups");
        builder.Property(e => e.UserId).HasComment("کد کاربر - کلید به جدول kci_users");

    }
}
