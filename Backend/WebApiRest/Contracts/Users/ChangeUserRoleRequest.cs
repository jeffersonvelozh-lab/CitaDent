using System.ComponentModel.DataAnnotations;
using Domain.Users;

namespace WebApiRest.Contracts.Users;

/// <summary>Body of PUT /api/users/{id}/role: <c>{ "role": "Admin" }</c> or <c>{ "role": "Reception" }</c>.</summary>
public sealed record ChangeUserRoleRequest([Required] UserRole? Role);
