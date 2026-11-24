using Application.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Patients;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// Patients. Reception and admin register them; any signed-in role reads one (the dentist sees their patient).
/// Errors (VALIDATION, RN-10, RN-12, DUPLICATE, NOT-FOUND) are answered by the central error handling.
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PatientDto> Get(
        Guid id,
        [FromServices] GetPatientHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetPatientQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreatePatientRequest request,
        [FromServices] CreatePatientHandler handler,
        CancellationToken cancellationToken)
    {
        // [Required] already rejected a missing DocumentType or BirthDate with a 400.
        var created = await handler.HandleAsync(
            new CreatePatientCommand(
                request.DocumentType!.Value,
                request.DocumentNumber ?? string.Empty,
                request.FirstName ?? string.Empty,
                request.LastName ?? string.Empty,
                request.BirthDate!.Value,
                request.Phone,
                request.Email,
                request.Address,
                request.GuardianName,
                request.GuardianPhone),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
}
