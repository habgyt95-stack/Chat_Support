using Chat_Support.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class UserFacilityConfiguration : IEntityTypeConfiguration<UserFacility>
{
    public void Configure(EntityTypeBuilder<UserFacility> builder)
    {
        builder.Property(e => e.AccessType)
            .IsFixedLength()
            .HasComment("نوع دسترسی y/n");
        builder.Property(e => e.DlinkId).HasComment("کد لینک مربوطه - کلید به جدول CMSDirectLinks");
        builder.Property(e => e.FacilityId).HasComment("ماژول مربوطه - کلید به جدول facilities");
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasComment("کلید");
        builder.Property(e => e.LinkId).HasComment("کد لینک مربوطه - کلید به جدول CMS_Links");
        builder.Property(e => e.TableName).HasComment("نام جدول مربوطه - کلید به databases");
        builder.Property(e => e.UserId).HasComment("کد کاربر مربوطه - کلید به kci_users");

    }
}
