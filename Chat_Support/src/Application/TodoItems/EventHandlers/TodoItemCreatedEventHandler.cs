using Chat_Support.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Application.TodoItems.EventHandlers;
public class TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>
{
    private readonly ILogger<TodoItemCreatedEventHandler> _logger;

    public TodoItemCreatedEventHandler(ILogger<TodoItemCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TodoItemCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chat_Support Domain Event: {DomainEvent}", notification.GetType().Name);

        return Task.CompletedTask;
    }
}
