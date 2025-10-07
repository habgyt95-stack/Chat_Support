using Chat_Support.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class KciUserConfiguration : IEntityTypeConfiguration<KciUser>
{
    public void Configure(EntityTypeBuilder<KciUser> builder)
    {
        builder.ToTable("KCI_Users", tb =>
        {
            tb.HasTrigger("General_Delete_Tracking");
            tb.HasTrigger("General_Insert_Tracking");
            tb.HasTrigger("Password_Update_Tracking");
            tb.HasTrigger("trg_UpdateLastPasswordChangeDate");
        });

        builder.Property(e => e.AccessFlag).HasDefaultValue(true);
        builder.Property(e => e.ActiveDirectoryUserName).HasDefaultValue("");
        builder.Property(e => e.Sex).IsFixedLength();
    }
}
