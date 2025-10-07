using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class TicketReplyConfiguration : IEntityTypeConfiguration<TicketReply>
{
    public void Configure(EntityTypeBuilder<TicketReply> builder)
    {
        builder.ToTable("TicketReplies"); // یا نام جدول موجود در دیتابیس

        builder.HasKey(e => e.Id)
            .HasName("PK__TicketRe__3214EC077F0EA2C0");

        builder.Property(e => e.TicketId)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasColumnType("int"); // تغییر از string به int

        builder.Property(e => e.Message)
            .IsRequired();

        builder.Property(e => e.Created)
            .HasDefaultValueSql("(getdate())")
            .HasColumnType("datetimeoffset");

        builder.HasOne(d => d.User)
            .WithMany(p => p.TicketReplies)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__TicketRep__UserI__0A9D95DB");

        builder.HasOne(d => d.Ticket)
            .WithMany()
            .HasForeignKey(d => d.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
