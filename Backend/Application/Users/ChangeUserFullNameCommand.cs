namespace Application.Users;

public sealed record ChangeUserFullNameCommand(Guid Id, string FullName);
