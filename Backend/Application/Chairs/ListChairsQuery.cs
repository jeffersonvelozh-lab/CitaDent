namespace Application.Chairs;

/// <summary>The chairs; inactive ones only when <paramref name="IncludeInactive"/> is true.</summary>
public sealed record ListChairsQuery(bool IncludeInactive = false);
