using Application.Appointments;
using Application.Common;
using Application.Tests.Fakes;
using Domain.Appointments;
using Domain.Common;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;

namespace Application.Tests.Appointments;

/// <summary>Booking an appointment, with in-memory doubles. All data is fictitious.</summary>
public class ScheduleAppointmentHandlerTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);

    // Monday, December 7, 2026 at the clinic. The dentist works 08:00-12:00.
    private static DateTimeOffset MondayAt(int hour, int minute = 0) => new(2026, 12, 7, hour, minute, 0, Ecuador);

    private readonly FakePatientRepository _patients = new();
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeChairRepository _chairs = new();
    private readonly FakeProcedureRepository _procedures = new();
    private readonly FakeScheduleBlockRepository _blocks = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Patient _patient;
    private readonly Dentist _dentist;
    private readonly Chair _chair;
    private readonly Procedure _procedure;
    private readonly Guid _userId = Guid.CreateVersion7();

    // Tuesday, December 1, 2026, 07:00 at the clinic.
    private DateTimeOffset _now = new(2026, 12, 1, 7, 0, 0, Ecuador);

    public ScheduleAppointmentHandlerTests()
    {
        _patient = _patients.Seed(Patient.Create(
            DocumentType.Cedula, "1710034065", "Ana", "Pérez", new DateOnly(1990, 5, 20), new DateOnly(2026, 11, 18)));
        _dentist = _dentists.Seed(NewDentist());
        _chair = _chairs.Seed(Chair.Create("Sillón 1"));
        _procedure = _procedures.Seed(Procedure.Create("Limpieza", null, 30, 25m));
    }

    private static Dentist NewDentist()
    {
        var dentist = Dentist.Create(Guid.CreateVersion7(), "Carla", "Mora", $"LIC-{Guid.NewGuid():N}"[..20]);
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);
        return dentist;
    }

    private ScheduleAppointmentHandler Handler
    {
        get
        {
            var clock = TestClinic.ClockAt(_now.ToUniversalTime());
            return new(
                _patients,
                _dentists,
                _chairs,
                _procedures,
                _appointments,
                new SchedulingContextLoader(_blocks, _appointments, clock),
                clock,
                _unitOfWork);
        }
    }

    private ScheduleAppointmentCommand Command(
        DateTimeOffset? startsAt = null,
        Guid? patientId = null,
        Guid? dentistId = null,
        Guid? chairId = null,
        Guid? procedureId = null) =>
        new(
            patientId ?? _patient.Id,
            dentistId ?? _dentist.Id,
            chairId ?? _chair.Id,
            procedureId ?? _procedure.Id,
            startsAt ?? MondayAt(9),
            _userId);

    private Task<AppointmentDto> ScheduleAsync(ScheduleAppointmentCommand? command = null) =>
        Handler.HandleAsync(command ?? Command());

    private async Task<DomainException> ScheduleFailsAsync(ScheduleAppointmentCommand? command = null) =>
        await Assert.ThrowsAsync<DomainException>(() => ScheduleAsync(command));

    // An appointment saved earlier, built with an empty context.
    private Appointment ExistingAppointment(Dentist dentist, Chair chair, DateTimeOffset start) =>
        Appointment.Schedule(
            _patient, dentist, chair, _procedure, start, _userId, new SchedulingContext(TestClinic.Ecuador, [], []));

    private void AssertNothingSaved()
    {
        Assert.Empty(_appointments.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Success

    [Fact]
    public async Task Schedule_AddsAScheduledAppointmentThatLastsTheProcedure_AndSavesOnce()
    {
        var result = await ScheduleAsync();

        var added = Assert.Single(_appointments.Added);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal(_patient.Id, result.PatientId);
        Assert.Equal(_dentist.Id, result.DentistId);
        Assert.Equal(_chair.Id, result.ChairId);
        Assert.Equal(_procedure.Id, result.ProcedureId);
        Assert.Equal(new DateTimeOffset(2026, 12, 7, 14, 0, 0, TimeSpan.Zero), result.StartsAt);
        Assert.Equal(TimeSpan.Zero, result.StartsAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 7, 14, 30, 0, TimeSpan.Zero), result.EndsAt); // RN-01
        Assert.Equal(AppointmentStatus.Scheduled, result.Status);
        Assert.Null(result.CancelReason);
        Assert.Equal(_userId, result.CreatedByUserId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Schedule_LoadsTheContextForTheSlotDentistAndChair()
    {
        await ScheduleAsync();

        Assert.Equal((new TimeRange(MondayAt(9), MondayAt(9, 30)), _dentist.Id, _chair.Id), _appointments.LastQuery);
    }

    [Fact]
    public async Task Schedule_RightAfterAnotherAppointment_IsAllowed_RN04()
    {
        _appointments.Overlapping.Add(ExistingAppointment(_dentist, _chair, MondayAt(8, 30)));

        await ScheduleAsync();

        Assert.Single(_appointments.Added);
    }

    // Missing references

    [Theory]
    [InlineData("patient")]
    [InlineData("dentist")]
    [InlineData("chair")]
    [InlineData("procedure")]
    public async Task Schedule_WithAReferenceThatDoesNotExist_ThrowsValidationAndDoesNotSave(string what)
    {
        var unknown = Guid.CreateVersion7();
        var command = what switch
        {
            "patient" => Command(patientId: unknown),
            "dentist" => Command(dentistId: unknown),
            "chair" => Command(chairId: unknown),
            _ => Command(procedureId: unknown)
        };

        var ex = await ScheduleFailsAsync(command);

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Contains(what, ex.Message, StringComparison.Ordinal);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Schedule_WithAnInactiveDentist_ThrowsValidation()
    {
        _dentist.Deactivate();

        var ex = await ScheduleFailsAsync();

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    // RN-06

    [Fact]
    public async Task Schedule_InThePast_ThrowsRN06BeforeLoadingTheContext()
    {
        _now = MondayAt(9, 1);

        var ex = await ScheduleFailsAsync();

        Assert.Equal("RN-06", ex.Code);
        Assert.Null(_appointments.LastQuery);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Schedule_StartingRightNow_IsAllowed_RN06()
    {
        _now = MondayAt(9);

        await ScheduleAsync();

        Assert.Single(_appointments.Added);
    }

    [Fact]
    public async Task Schedule_InThePast_ComparesInstantsNotLocalTimes_RN06()
    {
        // 09:00 at the clinic is 14:00 UTC: a start written in UTC an hour earlier is still in the past.
        _now = MondayAt(9);

        var ex = await ScheduleFailsAsync(Command(startsAt: new DateTimeOffset(2026, 12, 7, 13, 0, 0, TimeSpan.Zero)));

        Assert.Equal("RN-06", ex.Code);
    }

    // Domain rules with the loaded context

    [Fact]
    public async Task Schedule_OutsideTheWorkingHours_ThrowsRN05()
    {
        var ex = await ScheduleFailsAsync(Command(startsAt: MondayAt(11, 45)));

        Assert.Equal("RN-05", ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Schedule_InABlockedPeriod_ThrowsRN05()
    {
        _blocks.Seed(ScheduleBlock.Create(null, new TimeRange(MondayAt(0), MondayAt(23)), "Feriado"));

        var ex = await ScheduleFailsAsync();

        Assert.Equal("RN-05", ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Schedule_WhenTheDentistIsBusy_ThrowsRN02()
    {
        _appointments.Overlapping.Add(ExistingAppointment(_dentist, Chair.Create("Sillón 2"), MondayAt(9, 15)));

        var ex = await ScheduleFailsAsync();

        Assert.Equal("RN-02", ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Schedule_WhenTheChairIsBusy_ThrowsRN03()
    {
        _appointments.Overlapping.Add(ExistingAppointment(NewDentist(), _chair, MondayAt(8, 45)));

        var ex = await ScheduleFailsAsync();

        Assert.Equal("RN-03", ex.Code);
        AssertNothingSaved();
    }

    // The database finds a conflict that the context did not have yet

    [Theory]
    [InlineData(ScheduleConflict.Dentist, "RN-02")]
    [InlineData(ScheduleConflict.Chair, "RN-03")]
    public async Task Schedule_WhenTheDatabaseFindsAConflict_ThrowsTheMatchingRule(ScheduleConflict conflict, string code)
    {
        _unitOfWork.FailWith = new ScheduleConflictException(conflict);

        var ex = await ScheduleFailsAsync();

        Assert.Equal(code, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Schedule_WhenTheSaveFailsForAnotherReason_LetsTheErrorThrough()
    {
        _unitOfWork.FailWith = new InvalidOperationException("Connection lost.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ScheduleAsync());
    }
}
