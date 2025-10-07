using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Support.Commands;

public record DeleteSupportAgentCommand(int AgentId) : IRequest<bool>;

public class DeleteSupportAgentCommandHandler : IRequestHandler<DeleteSupportAgentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IAgentAssignmentService _agentAssignment;

    public DeleteSupportAgentCommandHandler(
        IApplicationDbContext context,
        IAgentAssignmentService agentAssignment)
    {
        _context = context;
        _agentAssignment = agentAssignment;
    }

    public async Task<bool> Handle(DeleteSupportAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

        if (agent == null)
            return false;

        // First, reassign all active tickets
        await _agentAssignment.UpdateAgentStatusAsync(agent.UserId, AgentStatus.Offline, cancellationToken);

        // Then delete the agent
        _context.SupportAgents.Remove(agent);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
