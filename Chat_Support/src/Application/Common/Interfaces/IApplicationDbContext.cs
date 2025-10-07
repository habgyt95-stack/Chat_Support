using Chat_Support.Domain.Entities;


namespace Chat_Support.Application.Common.Interfaces;
public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }
    DbSet<TodoItem> TodoItems { get; }

    DbSet<KciUser> KciUsers { get; }
    DbSet<KciGroup> KciGroups { get; }
    DbSet<KciAssignedUser> KciAssignedUsers { get; }
    DbSet<Region> Regions { get; }
    DbSet<CmsUserRegion> CmsUserRegions { get; }
    DbSet<SupportAgent> SupportAgents { get; }
    DbSet<UserFacility> UserFacilities { get; }
    DbSet<GroupFacility> GroupFacilities { get; }
    DbSet<TicketReply> TicketReplies { get; }
    DbSet<ChatRoom> ChatRooms { get; }
    DbSet<ChatRoomMember> ChatRoomMembers { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<MessageStatus> MessageStatuses { get; }
    DbSet<MessageReaction> MessageReactions { get; }
    DbSet<UserConnection> UserConnections { get; }
    DbSet<GuestUser> GuestUsers { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<ChatFileMetadata> ChatFileMetadatas { get; }
    DbSet<ChatLoginOtp> ChatLoginOtps { get; }
    DbSet<ChatUserRefreshToken> ChatUserRefreshTokens { get; }
    DbSet<SupportGuestChatRoomMember> SupportGuestChatRoomMembers { get; }
    DbSet<AbrikChatUsersToken> AbrikChatUsersTokens { get; }
    DbSet<UserFcmTokenInfoMobileAbrikChat> UserFcmTokenInfoMobileAbrikChats { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
