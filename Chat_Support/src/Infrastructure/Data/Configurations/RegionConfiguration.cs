using Chat_Support.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.Property(e => e.Id).HasComment("کلید");
        builder.Property(e => e.Name).HasComment("نام ناحیه/حوزه");
        builder.Property(e => e.ParentId).HasComment("کد ناحیه بالاتر - کلید به جدول Regions");
        builder.Property(e => e.Title).HasComment("عنوان");
    }
}
