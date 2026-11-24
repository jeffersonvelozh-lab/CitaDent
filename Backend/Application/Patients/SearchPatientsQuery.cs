using Application.Common;

namespace Application.Patients;

/// <summary>
/// Patients whose name or document contains every word of <paramref name="Term"/> (empty: all of them).
/// Inactive patients only when <paramref name="IncludeInactive"/> is true.
/// </summary>
public sealed record SearchPatientsQuery(
    string? Term = null,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);
