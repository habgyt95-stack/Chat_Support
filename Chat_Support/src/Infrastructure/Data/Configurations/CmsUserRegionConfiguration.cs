using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class CmsUserRegionConfiguration : IEntityTypeConfiguration<CmsUserRegion>
{
    public void Configure(EntityTypeBuilder<CmsUserRegion> builder)
    {
        builder.Property(e => e.Id).HasComment("کلید");
        builder.Property(e => e.RegionId).HasComment("شناسه ناحیه ی اختصای به کاربر");
        builder.Property(e => e.UserId).HasComment("شناسه کاربر");
    }
}
