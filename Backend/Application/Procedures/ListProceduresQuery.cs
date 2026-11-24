namespace Application.Procedures;

/// <summary>The catalog; inactive procedures only when <paramref name="IncludeInactive"/> is true.</summary>
public sealed record ListProceduresQuery(bool IncludeInactive = false);
