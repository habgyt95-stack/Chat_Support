using Chat_Support.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class KciGroupConfiguration : IEntityTypeConfiguration<KciGroup>
{
    public void Configure(EntityTypeBuilder<KciGroup> builder)
    {
        builder.Property(e => e.Id).HasComment("کلید");
        builder.Property(e => e.Description).HasComment("شرح");
        builder.Property(e => e.Name).HasComment("نام گروه کاربری");
    }
}
