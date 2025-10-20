using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Support.Queries;

public record AgentDto(
    int Id,
    int UserId,
    string UserName,
    string FullName,
    bool IsActive,
    string? AgentStatus,
    int CurrentActiveChats,
    int MaxConcurrentChats,
    DateTime? LastActivityAt
);

public record GetAllAgentsQuery() : IRequest<List<AgentDto>>;

public class GetAllAgentsQueryHandler : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetAllAgentsQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<AgentDto>> Handle(GetAllAgentsQuery request, CancellationToken cancellationToken)
    {
        var userRegionId = _user.RegionId;

        var agents = await _context.SupportAgents
            .Include(a => a.User)
            .Where(a => a.User!.RegionId == userRegionId) // فیلتر بر اساس ناحیه کاربر جاری
            .Select(a => new AgentDto(
                a.Id,
                a.UserId,
                a.User!.UserName!,
                $"{a.User.FirstName} {a.User.LastName}",
                a.IsActive,
                a.AgentStatus != null ? a.AgentStatus.ToString() : "Unknown",
                a.CurrentActiveChats,
                a.MaxConcurrentChats,
                a.LastActivityAt
            ))
            .ToListAsync(cancellationToken);

        return agents;
    }
}
