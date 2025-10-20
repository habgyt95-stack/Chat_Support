using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Support.Commands;

public record UpdateSupportAgentCommand(
    int AgentId,
    bool? IsActive = null,
    int? MaxConcurrentChats = null
) : IRequest<bool>;

public class UpdateSupportAgentCommandHandler : IRequestHandler<UpdateSupportAgentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IAgentAssignmentService _agentAssignmentService;

    public UpdateSupportAgentCommandHandler(IApplicationDbContext context, IAgentAssignmentService agentAssignmentService)
    {
        _context = context;
        _agentAssignmentService = agentAssignmentService;
    }

    public async Task<bool> Handle(UpdateSupportAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

        if (agent == null)
            return false;

        var wasActive = agent.IsActive;

        if (request.IsActive.HasValue)
            agent.IsActive = request.IsActive.Value;

        if (request.MaxConcurrentChats.HasValue)
            agent.MaxConcurrentChats = request.MaxConcurrentChats.Value;

        agent.LastActivityAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);

        // If admin deactivated the agent, set status to Offline and reassign tickets within region
        if (wasActive && request.IsActive.HasValue && request.IsActive.Value == false)
        {
            await _agentAssignmentService.UpdateAgentStatusAsync(agent.UserId, Domain.Enums.AgentStatus.Offline, cancellationToken);
        }

        return true;
    }
}
