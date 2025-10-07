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

    public GetAllAgentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AgentDto>> Handle(GetAllAgentsQuery request, CancellationToken cancellationToken)
    {
        var agents = await _context.SupportAgents
            .Include(a => a.User)
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
