using System.Reflection;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Data;
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<KciUser> KciUsers => Set<KciUser>();
    public DbSet<KciGroup> KciGroups => Set<KciGroup>();
    public DbSet<KciAssignedUser> KciAssignedUsers => Set<KciAssignedUser>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<CmsUserRegion> CmsUserRegions => Set<CmsUserRegion>();
    public DbSet<SupportAgent> SupportAgents => Set<SupportAgent>();
    public DbSet<UserFacility> UserFacilities => Set<UserFacility>();
    public DbSet<GroupFacility> GroupFacilities => Set<GroupFacility>();
    public DbSet<TicketReply> TicketReplies => Set<TicketReply>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MessageStatus> MessageStatuses => Set<MessageStatus>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();
    public DbSet<GuestUser> GuestUsers => Set<GuestUser>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<ChatFileMetadata> ChatFileMetadatas => Set<ChatFileMetadata>();
    public DbSet<ChatLoginOtp> ChatLoginOtps => Set<ChatLoginOtp>();
    public DbSet<ChatUserRefreshToken> ChatUserRefreshTokens => Set<ChatUserRefreshToken>();
    public DbSet<SupportGuestChatRoomMember> SupportGuestChatRoomMembers => Set<SupportGuestChatRoomMember>();
    public DbSet<AbrikChatUsersToken> AbrikChatUsersTokens => Set<AbrikChatUsersToken>();
    public DbSet<UserFcmTokenInfoMobileAbrikChat> UserFcmTokenInfoMobileAbrikChats => Set<UserFcmTokenInfoMobileAbrikChat>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        // Ignore System.Text.RegularExpressions.Capture and Group to prevent EF Core mapping errors
        builder.Ignore<System.Text.RegularExpressions.Capture>();
        builder.Ignore<System.Text.RegularExpressions.Group>();
    }
}
