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

    public UpdateSupportAgentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateSupportAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

        if (agent == null)
            return false;

        if (request.IsActive.HasValue)
            agent.IsActive = request.IsActive.Value;

        if (request.MaxConcurrentChats.HasValue)
            agent.MaxConcurrentChats = request.MaxConcurrentChats.Value;

        agent.LastActivityAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
