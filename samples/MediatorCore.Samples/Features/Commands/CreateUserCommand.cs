using MediatorCore.Samples.DTOs;

namespace MediatorCore.Samples.Features.Commands;

public class CreateUserCommand : IRequest<UserDto>
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
}


public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IMediator _mediator;

    public CreateUserCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Create user logic
        var userDto = new UserDto { /* ... */ };

        // Publish domain event
        await _mediator.Publish(new UserCreatedEvent(userDto.Id, userDto.Username), cancellationToken);

        return userDto;
    }
}

public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Send welcome email, update search index, etc.
        await Task.CompletedTask;
    }
}

public record UserCreatedEvent(Guid UserId, string Username) : INotification;