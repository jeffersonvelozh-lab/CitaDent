using Application.Common;

namespace Application.Users;

/// <summary>The users of every role, ordered by full name and email.</summary>
public sealed class ListUsersHandler(IUserRepository users) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = await users.ListAsync(query.IncludeInactive, query.Role, cancellationToken);

        return list.Select(UserDto.From).ToList();
    }
}
