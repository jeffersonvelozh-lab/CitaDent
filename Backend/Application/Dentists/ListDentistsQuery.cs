namespace Application.Dentists;

/// <summary>The dentists; inactive ones only when <paramref name="IncludeInactive"/> is true.</summary>
public sealed record ListDentistsQuery(bool IncludeInactive = false);
