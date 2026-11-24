using Application.Common;
using Application.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Patients;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// Patients. Any signed-in role searches and reads them (the dentist sees their patient); reception and
/// admin register, edit, activate and deactivate them. Patients are never deleted, only deactivated (RN-11).
/// Errors (VALIDATION, RN-10, RN-12, DUPLICATE, NOT-FOUND) are answered by the central error handling.
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    /// <summary>
    /// Patients whose name or document contains every word of <paramref name="term"/>, a page at a time.
    /// A page past the end returns no items and the total.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PatientDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<PatientDto>> Search(
        [FromServices] SearchPatientsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? term = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        handler.HandleAsync(new SearchPatientsQuery(term, includeInactive, page, pageSize), cancellationToken);

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
        PatientRequest request,
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

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<PatientDto> Update(
        Guid id,
        PatientRequest request,
        [FromServices] UpdatePatientHandler handler,
        CancellationToken cancellationToken) =>
        // [Required] already rejected a missing DocumentType or BirthDate with a 400.
        handler.HandleAsync(
            new UpdatePatientCommand(
                id,
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

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Activate(
        Guid id,
        [FromServices] SetPatientActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: true, handler, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] SetPatientActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: false, handler, cancellationToken);

    private async Task<IActionResult> SetActive(
        Guid id,
        bool isActive,
        SetPatientActiveHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetPatientActiveCommand(id, isActive), cancellationToken);
        return NoContent();
    }
}
