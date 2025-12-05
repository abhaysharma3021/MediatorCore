namespace MediatorCore.Samples.Features.Commands;

public class UpdateUserCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Email { get; set; }
}