using Domain.Appointments;
using Domain.Common;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;
using Xunit;

namespace Domain.Tests.Appointments;

public class AppointmentTests
{
    // Ecuador has no daylight saving time, so a fixed UTC-5 zone behaves the same on any machine.
    private static readonly TimeZoneInfo Ecuador =
        TimeZoneInfo.CreateCustomTimeZone("ecuador-test", TimeSpan.FromHours(-5), "Ecuador", "Ecuador");

    private static readonly TimeSpan EcuadorOffset = TimeSpan.FromHours(-5);
    private static readonly Guid User = Guid.NewGuid();

    private readonly Patient _patient = Patient.Create(
        DocumentType.Cedula, "1710034065", "Ana", "Pérez", new DateOnly(1990, 5, 20), new DateOnly(2026, 11, 18));

    private readonly Dentist _dentist = NewDentist();
    private readonly Chair _chair = Chair.Create("Chair 1");
    private readonly Procedure _cleaning = Procedure.Create("Cleaning", null, 30, 25m);

    // December 7, 2026 is a Monday; the dentist works 08:00-12:00 and 14:00-18:00.
    private static DateTimeOffset MondayAt(int hour, int minute) =>
        new(2026, 12, 7, hour, minute, 0, EcuadorOffset);

    private static Dentist NewDentist()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123");

        dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);

        return dentist;
    }

    private static SchedulingContext ContextWith(
        IReadOnlyCollection<Appointment>? existing = null,
        IReadOnlyCollection<ScheduleBlock>? blocks = null) =>
        new(Ecuador, blocks ?? Array.Empty<ScheduleBlock>(), existing ?? Array.Empty<Appointment>());

    private Appointment Book(Dentist dentist, Chair chair, DateTimeOffset start, params Appointment[] existing) =>
        Appointment.Schedule(_patient, dentist, chair, _cleaning, start, User, ContextWith(existing));

    private Appointment BookWithBlocks(DateTimeOffset start, params ScheduleBlock[] blocks) =>
        Appointment.Schedule(_patient, _dentist, _chair, _cleaning, start, User, ContextWith(blocks: blocks));

    private ScheduleBlock BlockOnMonday(Guid? dentistId, int fromHour, int toHour) =>
        ScheduleBlock.Create(dentistId, new TimeRange(MondayAt(fromHour, 0), MondayAt(toHour, 0)), "Block");

    // ---- Scheduling: RN-01 to RN-05 ----

    [Fact]
    public void Schedule_CreatesAScheduledAppointment_WhoseEndIsStartPlusDuration()
    {
        var start = MondayAt(9, 0);

        var appointment = Book(_dentist, _chair, start);

        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(_patient.Id, appointment.PatientId);
        Assert.Equal(_dentist.Id, appointment.DentistId);
        Assert.Equal(_chair.Id, appointment.ChairId);
        Assert.Equal(_cleaning.Id, appointment.ProcedureId);
        Assert.Equal(User, appointment.CreatedByUserId);
        Assert.Equal(start, appointment.Slot.Start);
        Assert.Equal(start.AddMinutes(30), appointment.Slot.End);
    }

    [Fact]
    public void Schedule_UsesTheDurationOfTheProcedure()
    {
        var rootCanal = Procedure.Create("Root canal", null, 90, 120m);
        var start = MondayAt(9, 0);

        var appointment = Appointment.Schedule(_patient, _dentist, _chair, rootCanal, start, User, ContextWith());

        Assert.Equal(start.AddMinutes(90), appointment.Slot.End);
    }

    [Fact]
    public void Schedule_Throws_RN02_WhenTheDentistHasAnOverlappingAppointment()
    {
        var first = Book(_dentist, _chair, MondayAt(9, 0));

        var ex = Assert.Throws<DomainException>(() =>
            Book(_dentist, Chair.Create("Chair 2"), MondayAt(9, 15), first));

        Assert.Equal("RN-02", ex.Code);
    }

    [Fact]
    public void Schedule_Throws_RN03_WhenTheChairIsOccupied()
    {
        var first = Book(_dentist, _chair, MondayAt(9, 0));

        var ex = Assert.Throws<DomainException>(() =>
            Book(NewDentist(), _chair, MondayAt(9, 15), first));

        Assert.Equal("RN-03", ex.Code);
    }

    [Fact]
    public void Schedule_Allows_BackToBackAppointments_ForTheSameDentistAndChair()
    {
        var first = Book(_dentist, _chair, MondayAt(9, 0));

        var second = Book(_dentist, _chair, MondayAt(9, 30), first);

        Assert.Equal(AppointmentStatus.Scheduled, second.Status);
    }

    [Fact]
    public void Schedule_Allows_TheSameTime_ForADifferentDentistAndChair()
    {
        var first = Book(_dentist, _chair, MondayAt(9, 0));

        var second = Book(NewDentist(), Chair.Create("Chair 2"), MondayAt(9, 0), first);

        Assert.Equal(AppointmentStatus.Scheduled, second.Status);
    }

    [Fact]
    public void Schedule_IgnoresCancelledAppointments()
    {
        var first = Book(_dentist, _chair, MondayAt(9, 0));
        first.Cancel("Patient called to cancel");

        var second = Book(_dentist, _chair, MondayAt(9, 0), first);

        Assert.Equal(AppointmentStatus.Scheduled, second.Status);
    }

    [Theory]
    [InlineData(7, 0)]   // before opening
    [InlineData(12, 0)]  // lunch break
    [InlineData(11, 45)] // starts in the morning block, ends after it
    public void Schedule_Throws_RN05_WhenTheSlotIsOutsideWorkingHours(int hour, int minute)
    {
        var ex = Assert.Throws<DomainException>(() => Book(_dentist, _chair, MondayAt(hour, minute)));

        Assert.Equal("RN-05", ex.Code);
    }

    [Fact]
    public void Schedule_Throws_RN05_OnADayWithoutWorkingHours()
    {
        var sunday = new DateTimeOffset(2026, 12, 6, 9, 0, 0, EcuadorOffset);

        var ex = Assert.Throws<DomainException>(() => Book(_dentist, _chair, sunday));

        Assert.Equal("RN-05", ex.Code);
    }

    [Fact]
    public void Schedule_Throws_RN05_WhenTheDentistHasABlockedPeriod()
    {
        var vacation = BlockOnMonday(_dentist.Id, 9, 10);

        var ex = Assert.Throws<DomainException>(() => BookWithBlocks(MondayAt(9, 30), vacation));

        Assert.Equal("RN-05", ex.Code);
    }

    [Fact]
    public void Schedule_Throws_RN05_WhenTheWholeClinicIsBlocked()
    {
        var holiday = BlockOnMonday(null, 8, 12);

        var ex = Assert.Throws<DomainException>(() => BookWithBlocks(MondayAt(9, 0), holiday));

        Assert.Equal("RN-05", ex.Code);
    }

    [Fact]
    public void Schedule_Allows_ABlockThatBelongsToAnotherDentist()
    {
        var otherDentistVacation = BlockOnMonday(Guid.NewGuid(), 9, 10);

        var appointment = BookWithBlocks(MondayAt(9, 30), otherDentistVacation);

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public void Schedule_Allows_ASlotThatStartsWhenABlockEnds()
    {
        var vacation = BlockOnMonday(_dentist.Id, 9, 10);

        var appointment = BookWithBlocks(MondayAt(10, 0), vacation);

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Theory]
    [InlineData("patient")]
    [InlineData("dentist")]
    [InlineData("chair")]
    [InlineData("procedure")]
    public void Schedule_Throws_WhenAnInvolvedEntityIsInactive(string which)
    {
        switch (which)
        {
            case "patient":
                _patient.Deactivate();
                break;
            case "dentist":
                _dentist.Deactivate();
                break;
            case "chair":
                _chair.Deactivate();
                break;
            case "procedure":
                _cleaning.Deactivate();
                break;
        }

        var ex = Assert.Throws<DomainException>(() => Book(_dentist, _chair, MondayAt(9, 0)));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Schedule_Throws_WhenTheCreatorIsMissing()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Appointment.Schedule(_patient, _dentist, _chair, _cleaning, MondayAt(9, 0), Guid.Empty, ContextWith()));

        Assert.Equal("VALIDATION", ex.Code);
    }

    // ---- State changes: RN-07 and RN-08 ----

    [Fact]
    public void Complete_ChangesTheStatusToCompleted()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));

        appointment.Complete();

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public void MarkNoShow_ChangesTheStatusToNoShow()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));

        appointment.MarkNoShow();

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }

    [Fact]
    public void Cancel_StoresTheReason_AndChangesTheStatus()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));

        appointment.Cancel("  Patient is sick  ");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal("Patient is sick", appointment.CancelReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_Throws_RN08_WhenTheReasonIsBlank(string? reason)
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));

        var ex = Assert.Throws<DomainException>(() => appointment.Cancel(reason!));

        Assert.Equal("RN-08", ex.Code);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public void Cancel_Throws_AndKeepsTheStatus_WhenTheReasonIsTooLong()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));

        var ex = Assert.Throws<DomainException>(() =>
            appointment.Cancel(new string('x', Appointment.CancelReasonMaxLength + 1)));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(appointment.CancelReason);
    }

    [Fact]
    public void ACompletedAppointment_CannotChangeState()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));
        appointment.Complete();

        AssertCannotChangeState(appointment);
    }

    [Fact]
    public void ACancelledAppointment_CannotChangeState()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));
        appointment.Cancel("Patient called to cancel");

        AssertCannotChangeState(appointment);
    }

    [Fact]
    public void ANoShowAppointment_CannotChangeState()
    {
        var appointment = Book(_dentist, _chair, MondayAt(9, 0));
        appointment.MarkNoShow();

        AssertCannotChangeState(appointment);
    }

    private static void AssertCannotChangeState(Appointment appointment)
    {
        var statusBefore = appointment.Status;

        Assert.Equal("RN-07", Assert.Throws<DomainException>(() => appointment.Complete()).Code);
        Assert.Equal("RN-07", Assert.Throws<DomainException>(() => appointment.MarkNoShow()).Code);
        Assert.Equal("RN-07", Assert.Throws<DomainException>(() => appointment.Cancel("Any reason")).Code);
        Assert.Equal(statusBefore, appointment.Status);
    }
}