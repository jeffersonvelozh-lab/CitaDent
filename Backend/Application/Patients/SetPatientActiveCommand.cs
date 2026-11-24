namespace Application.Patients;

/// <summary>Activates (<paramref name="IsActive"/> true) or deactivates a patient.</summary>
public sealed record SetPatientActiveCommand(Guid Id, bool IsActive);
