using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;


namespace Chat_Support.Application.Common.Interfaces;

public interface IAgentAssignmentService
{
    Task<SupportAgent?> GetBestAvailableAgentAsync(CancellationToken cancellationToken = default);
    Task<SupportAgent?> GetBestAvailableAgentAsync(int? regionId = null, CancellationToken cancellationToken = default);
    Task<int> GetAgentWorkloadAsync(int agentId, CancellationToken cancellationToken = default);
    Task UpdateAgentStatusAsync(int agentId, AgentStatus status, CancellationToken cancellationToken = default);
    Task ReassignBotTicketsToAvailableAgentsAsync(CancellationToken cancellationToken = default);
}
