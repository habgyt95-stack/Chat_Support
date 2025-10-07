using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Support.Commands;

public record CreateSupportAgentCommand(
    int UserId,
    int MaxConcurrentChats = 5
) : IRequest<int>;

public class CreateSupportAgentCommandHandler : IRequestHandler<CreateSupportAgentCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateSupportAgentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateSupportAgentCommand request, CancellationToken cancellationToken)
    {
        // Check if agent already exists
        var existing = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, cancellationToken);

        if (existing != null)
            throw new InvalidOperationException("Agent already exists");

        // Check if user exists
        var user = await _context.KciUsers.FindAsync(request.UserId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");

        var agent = new SupportAgent
        {
            UserId = request.UserId,
            IsActive = true,
            AgentStatus = AgentStatus.Offline,
            CurrentActiveChats = 0,
            MaxConcurrentChats = request.MaxConcurrentChats,
            LastActivityAt = DateTime.Now
        };

        _context.SupportAgents.Add(agent);
        await _context.SaveChangesAsync(cancellationToken);

        return agent.Id;
    }
}
