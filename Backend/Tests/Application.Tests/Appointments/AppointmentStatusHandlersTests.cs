using Application.Appointments;
using Application.Common;
using Application.Tests.Fakes;
using Domain.Appointments;
using Domain.Common;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;
using Domain.Users;

namespace Application.Tests.Appointments;

/// <summary>Cancel, complete and mark as no-show (RN-07, RN-08, RN-09), with in-memory doubles. All data is fictitious.</summary>
public class AppointmentStatusHandlersTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);

    // The appointment: Monday, December 7, 2026, 09:00 to 09:30 at the clinic.
    private static DateTimeOffset MondayAt(int hour, int minute = 0) => new(2026, 12, 7, hour, minute, 0, Ecuador);

    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Guid _dentistUserId = Guid.CreateVersion7();
    private readonly Dentist _dentist;
    private readonly Appointment _appointment;

    // By default the appointment is over: 10:00 at the clinic.
    private DateTimeOffset _now = MondayAt(10);

    public AppointmentStatusHandlersTests()
    {
        _dentist = _dentists.Seed(Dentist.Create(_dentistUserId, "Carla", "Mora", "LIC-001"));
        _dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        _appointment = _appointments.Seed(Appointment.Schedule(
            Patient.Create(DocumentType.Cedula, "1710034065", "Ana", "Pérez", new DateOnly(1990, 5, 20), new DateOnly(2026, 11, 18)),
            _dentist,
            Chair.Create("Sillón 1"),
            Procedure.Create("Limpieza", null, 30, 25m),
            MondayAt(9),
            Guid.CreateVersion7(),
            new SchedulingContext(TestClinic.Ecuador, [], [])));
    }

    private ClinicClock Clock => TestClinic.ClockAt(_now.ToUniversalTime());

    private Task<AppointmentDto> CancelAsync(string reason = "El paciente viajó", Guid? id = null) =>
        new CancelAppointmentHandler(_appointments, Clock, _unitOfWork)
            .HandleAsync(new CancelAppointmentCommand(id ?? _appointment.Id, reason));

    private Task<AppointmentDto> MarkNoShowAsync(Guid? id = null) =>
        new MarkAppointmentNoShowHandler(_appointments, Clock, _unitOfWork)
            .HandleAsync(new MarkAppointmentNoShowCommand(id ?? _appointment.Id));

    private Task<AppointmentDto> CompleteAsync(Guid? actorUserId = null, UserRole role = UserRole.Dentist, Guid? id = null) =>
        new CompleteAppointmentHandler(_appointments, _dentists, Clock, _unitOfWork)
            .HandleAsync(new CompleteAppointmentCommand(id ?? _appointment.Id, actorUserId ?? _dentistUserId, role));

    private async Task AssertFailsAsync(Func<Task> action, string code)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(action);
        Assert.Equal(code, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Moves the appointment to another final state, as if that had been saved before.
    private void PutIn(AppointmentStatus status)
    {
        switch (status)
        {
            case AppointmentStatus.Completed: _appointment.Complete(); break;
            case AppointmentStatus.Cancelled: _appointment.Cancel("Motivo anterior"); break;
            case AppointmentStatus.NoShow: _appointment.MarkNoShow(); break;
        }
    }

    // Cancel

    [Fact]
    public async Task Cancel_BeforeItStarts_StoresTheReasonAndSavesOnce()
    {
        _now = MondayAt(8, 59);

        var result = await CancelAsync("  El paciente viajó ");

        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal("El paciente viajó", result.CancelReason);
        Assert.Equal(AppointmentStatus.Cancelled, _appointment.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(9, 0)] // Right when it starts.
    [InlineData(10, 0)] // After it ended.
    public async Task Cancel_OnceItHasStarted_ThrowsValidation(int hour, int minute)
    {
        _now = MondayAt(hour, minute);

        await AssertFailsAsync(() => CancelAsync(), DomainException.ValidationCode);
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancel_WithoutAReason_ThrowsRN08(string reason)
    {
        _now = MondayAt(8);

        await AssertFailsAsync(() => CancelAsync(reason), "RN-08");
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
    }

    // Mark as no-show

    [Theory]
    [InlineData(9, 0)] // Right when it starts.
    [InlineData(10, 0)]
    public async Task MarkNoShow_OnceItHasStarted_ChangesTheStatusAndSavesOnce(int hour, int minute)
    {
        _now = MondayAt(hour, minute);

        var result = await MarkNoShowAsync();

        Assert.Equal(AppointmentStatus.NoShow, result.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MarkNoShow_BeforeItStarts_ThrowsValidation()
    {
        _now = MondayAt(8, 59);

        await AssertFailsAsync(() => MarkNoShowAsync(), DomainException.ValidationCode);
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
    }

    // Complete and RN-09

    [Fact]
    public async Task Complete_ByTheAssignedDentist_ChangesTheStatusAndSavesOnce()
    {
        var result = await CompleteAsync();

        Assert.Equal(AppointmentStatus.Completed, result.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Complete_ByAnAdmin_IsAllowed_RN09()
    {
        var result = await CompleteAsync(Guid.CreateVersion7(), UserRole.Admin);

        Assert.Equal(AppointmentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task Complete_ByAnotherDentist_ThrowsRN09()
    {
        var other = _dentists.Seed(Dentist.Create(Guid.CreateVersion7(), "Luis", "Vera", "LIC-002"));

        await AssertFailsAsync(() => CompleteAsync(other.UserId), "RN-09");
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
    }

    [Fact]
    public async Task Complete_ByReception_ThrowsRN09()
    {
        await AssertFailsAsync(() => CompleteAsync(_dentistUserId, UserRole.Reception), "RN-09");
    }

    [Fact]
    public async Task Complete_ByAnotherDentist_ThrowsRN09_EvenBeforeItStarts()
    {
        _now = MondayAt(8);

        await AssertFailsAsync(() => CompleteAsync(Guid.CreateVersion7()), "RN-09");
    }

    [Fact]
    public async Task Complete_BeforeItStarts_ThrowsValidation()
    {
        _now = MondayAt(8, 59);

        await AssertFailsAsync(() => CompleteAsync(), DomainException.ValidationCode);
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
    }

    // RN-07: only a scheduled appointment changes state, whatever the time

    public static TheoryData<AppointmentStatus, string> FinalStatesAndActions()
    {
        var data = new TheoryData<AppointmentStatus, string>();

        foreach (var status in new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow })
        {
            foreach (var action in new[] { "cancel", "no-show", "complete" })
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FinalStatesAndActions))]
    public async Task AnAppointmentNotScheduled_CannotChangeState_RN07(AppointmentStatus status, string action)
    {
        PutIn(status);

        // Before the start too: RN-07 wins over the time rules, and says more.
        foreach (var now in new[] { MondayAt(8), MondayAt(10) })
        {
            _now = now;

            Func<Task> attempt = action switch
            {
                "cancel" => () => CancelAsync(),
                "no-show" => () => MarkNoShowAsync(),
                _ => () => CompleteAsync()
            };

            await AssertFailsAsync(attempt, "RN-07");
            Assert.Equal(status, _appointment.Status);
        }
    }

    // Unknown appointment

    [Fact]
    public async Task EveryAction_WithAnUnknownId_ThrowsNotFound()
    {
        var unknown = Guid.CreateVersion7();

        await AssertFailsAsync(() => CancelAsync(id: unknown), ApplicationErrorCodes.NotFound);
        await AssertFailsAsync(() => MarkNoShowAsync(unknown), ApplicationErrorCodes.NotFound);
        await AssertFailsAsync(() => CompleteAsync(role: UserRole.Admin, id: unknown), ApplicationErrorCodes.NotFound);
    }
}
