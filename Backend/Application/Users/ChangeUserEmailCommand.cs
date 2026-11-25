namespace Application.Users;

public sealed record ChangeUserEmailCommand(Guid Id, string Email);
