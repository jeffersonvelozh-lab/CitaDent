using Application.Common;

namespace Application.Users;

/// <summary>One user by id, of any role, active or not.</summary>
public sealed class GetUserHandler(IUserRepository users) : IQueryHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await users.GetByIdAsync(query.Id, cancellationToken)
            ?? throw UserErrors.NotFound(query.Id);

        return UserDto.From(user);
    }
}
