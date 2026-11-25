using Application.Common;

namespace Application.Appointments;

/// <summary>One appointment by id, in any state.</summary>
public sealed class GetAppointmentHandler(IAppointmentRepository appointments)
    : IQueryHandler<GetAppointmentQuery, AppointmentDto>
{
    public async Task<AppointmentDto> HandleAsync(GetAppointmentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var appointment = await appointments.GetByIdAsync(query.Id, cancellationToken)
            ?? throw AppointmentErrors.NotFound(query.Id);

        return AppointmentDto.From(appointment);
    }
}
