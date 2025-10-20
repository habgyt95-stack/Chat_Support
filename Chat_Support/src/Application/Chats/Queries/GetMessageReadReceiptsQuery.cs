using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Chats.Queries;

public record MessageReadReceiptDto(
    int UserId,
    string UserName,
    string FullName,
    string? AvatarUrl,
    DateTime ReadAt
);

public record GetMessageReadReceiptsQuery(int MessageId) : IRequest<List<MessageReadReceiptDto>>;

public class GetMessageReadReceiptsQueryHandler : IRequestHandler<GetMessageReadReceiptsQuery, List<MessageReadReceiptDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMessageReadReceiptsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MessageReadReceiptDto>> Handle(GetMessageReadReceiptsQuery request, CancellationToken cancellationToken)
    {
        // Order by entity property before projection to avoid EF translation issues when ordering by DTO members
        var receipts = await _context.MessageStatuses
            .AsNoTracking()
            .Where(ms => ms.MessageId == request.MessageId
                         && ms.Status == Domain.Enums.ReadStatus.Read
                         && ms.UserId != null)
            .OrderBy(ms => ms.StatusAt)
            .Select(ms => new MessageReadReceiptDto(
                ms.UserId!.Value,
                ms.User.UserName!,
                ($"{ms.User.FirstName} {ms.User.LastName}").Trim(),
                ms.User.ImageName,
                ms.StatusAt
            ))
            .ToListAsync(cancellationToken);

        return receipts;
    }
}
