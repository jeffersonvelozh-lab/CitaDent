using Application.Users;
using Domain.Common;
using Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Users;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// User accounts, only for admins. Dentist accounts are created and activated through /api/dentists, together
/// with the dentist record; here they can be read and get a new email or password. Users are never deleted and
/// the password hash is never returned. Errors (VALIDATION, NOT-FOUND, DUPLICATE, LAST-ADMIN) are answered by
/// the central error handling.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class UsersController : ControllerBase
{
    /// <summary>
    /// The users ordered by name; inactive ones only with <paramref name="includeInactive"/>. <paramref name="role"/>
    /// is a role name (Admin, Reception, Dentist, in any letter case); a number or an unknown name is a 400.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<UserDto>> List(
        [FromServices] ListUsersHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? role = null) =>
        handler.HandleAsync(new ListUsersQuery(includeInactive, ParseRole(role)), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserDto> Get(
        Guid id,
        [FromServices] GetUserHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetUserQuery(id), cancellationToken);

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        [FromServices] CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        // Model validation already answered 400 when the role is missing.
        var created = await handler.HandleAsync(
            new CreateUserCommand(
                request.Email ?? string.Empty,
                request.FullName ?? string.Empty,
                request.Password ?? string.Empty,
                request.Role!.Value),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes the email the user signs in with; the old one stops working at once.</summary>
    [HttpPut("{id:guid}/email")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> ChangeEmail(
        Guid id,
        ChangeUserEmailRequest request,
        [FromServices] ChangeUserEmailHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new ChangeUserEmailCommand(id, request.Email ?? string.Empty), cancellationToken);

    /// <summary>Sets a new password without the old one; the old one stops working at once.</summary>
    [HttpPut("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetUserPasswordRequest request,
        [FromServices] ResetUserPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ResetUserPasswordCommand(id, request.Password ?? string.Empty), cancellationToken);
        return NoContent();
    }

    /// <summary>Moves the user between Admin and Reception; the last active admin keeps the role (409).</summary>
    [HttpPut("{id:guid}/role")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request,
        [FromServices] ChangeUserRoleHandler handler,
        CancellationToken cancellationToken) =>
        // Model validation already answered 400 when the role is missing.
        handler.HandleAsync(new ChangeUserRoleCommand(id, request.Role!.Value), cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Activate(
        Guid id,
        [FromServices] SetUserActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: true, handler, cancellationToken);

    /// <summary>The user can no longer sign in; the last active admin cannot be deactivated (409).</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] SetUserActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: false, handler, cancellationToken);

    private async Task<IActionResult> SetActive(
        Guid id,
        bool isActive,
        SetUserActiveHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetUserActiveCommand(id, isActive), cancellationToken);
        return NoContent();
    }

    // Query binding would accept "1" or "99" for an enum, so the role is read as text and only a defined
    // name is accepted, the same as enums in JSON bodies.
    private static UserRole? ParseRole(string? role)
    {
        if (role is null)
        {
            return null;
        }

        foreach (var value in Enum.GetValues<UserRole>())
        {
            if (string.Equals(value.ToString(), role.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new DomainException(DomainException.ValidationCode, "Unknown user role.");
    }
}
