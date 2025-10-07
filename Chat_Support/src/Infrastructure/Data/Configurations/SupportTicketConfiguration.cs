using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat_Support.Infrastructure.Data.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasOne(ticket => ticket.RequesterUser)
            .WithMany(user => user.SupportTicketsAsRequester)
            .HasForeignKey(ticket => ticket.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(ticket => ticket.AssignedAgent)
            .WithMany(user => user.AssignedTickets)
            .HasForeignKey(ticket => ticket.AssignedAgentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.RequesterGuest)
            .WithMany(g => g.SupportTickets)
            .HasForeignKey(e => e.RequesterGuestId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ChatRoom)
            .WithOne()
            .HasForeignKey<SupportTicket>(e => e.ChatRoomId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.Property(e => e.Status)
            .HasConversion<int>();

        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.AssignedAgentUserId);
    }
}
