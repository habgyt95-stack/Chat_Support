using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class GroupFacilityConfiguration : IEntityTypeConfiguration<GroupFacility>
{
    public void Configure(EntityTypeBuilder<GroupFacility> builder)
    {
        builder.Property(e => e.AccessType)
            .IsFixedLength()
            .HasComment("نوع دسترسی y/n");
        builder.Property(e => e.DlinkId).HasComment("کد لینک مربوطه - کلید به جدول CMSDirectLinks");
        builder.Property(e => e.FacilityId).HasComment("کد امکان");
        builder.Property(e => e.GroupId).HasComment("کد گروه کاربری - کلید به kci_groups");
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasComment("کلید");
        builder.Property(e => e.LinkId).HasComment("کد لینک - کلید به جدول CMS_Links");
        builder.Property(e => e.TableName).HasComment("نام جدول");
    }
}
