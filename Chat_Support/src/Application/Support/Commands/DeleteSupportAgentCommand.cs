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

        // First, mark agent as offline to trigger ticket reassignment
        await _agentAssignment.UpdateAgentStatusAsync(agent.UserId, AgentStatus.Offline, cancellationToken);

        // Find all tickets still assigned to this agent and reassign or unassign them
        var assignedTickets = await _context.SupportTickets
            .Where(t => t.AssignedAgentUserId == agent.Id)  // ✅ FK به SupportAgent.Id اشاره داره
            .ToListAsync(cancellationToken);

        foreach (var ticket in assignedTickets)
        {
            // Try to assign to another available agent
            var newAgent = await _agentAssignment.GetBestAvailableAgentAsync(ticket.RegionId, cancellationToken);
            
            if (newAgent != null)
            {
                ticket.AssignedAgentUserId = newAgent.Id;  // ✅ باید Id باشه نه UserId!
            }
            else
            {
                // No agent available, unassign the ticket
                ticket.AssignedAgentUserId = null;
            }
        }

        // Save ticket reassignments
        await _context.SaveChangesAsync(cancellationToken);

        // Now safe to delete the agent
        _context.SupportAgents.Remove(agent);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
