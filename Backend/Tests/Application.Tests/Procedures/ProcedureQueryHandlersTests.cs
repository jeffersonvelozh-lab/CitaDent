using Application.Common;
using Application.Procedures;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Procedures;

namespace Application.Tests.Procedures;

/// <summary>Get and list procedures, with an in-memory repository.</summary>
public class ProcedureQueryHandlersTests
{
    private readonly FakeProcedureRepository _procedures = new();

    private GetProcedureHandler Get => new(_procedures);
    private ListProceduresHandler List => new(_procedures);

    [Fact]
    public async Task Get_ReturnsTheProcedureEvenWhenInactive()
    {
        var cleaning = _procedures.Seed(Procedure.Create("Limpieza", "Básica", 30, 25m));
        cleaning.Deactivate();

        var result = await Get.HandleAsync(new GetProcedureQuery(cleaning.Id));

        Assert.Equal(new ProcedureDto(cleaning.Id, "Limpieza", "Básica", 30, 25m, false), result);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Get.HandleAsync(new GetProcedureQuery(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task List_ByDefault_ReturnsOnlyActiveOnesOrderedByName()
    {
        _procedures.Seed(Procedure.Create("Resina", null, 45, 40m));
        _procedures.Seed(Procedure.Create("Extracción", null, 40, 50m));
        _procedures.Seed(Procedure.Create("Blanqueamiento", null, 60, 120m)).Deactivate();

        var result = await List.HandleAsync(new ListProceduresQuery());

        Assert.Equal(["Extracción", "Resina"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task List_IncludingInactive_ReturnsAll()
    {
        _procedures.Seed(Procedure.Create("Resina", null, 45, 40m));
        _procedures.Seed(Procedure.Create("Blanqueamiento", null, 60, 120m)).Deactivate();

        var result = await List.HandleAsync(new ListProceduresQuery(IncludeInactive: true));

        Assert.Equal(["Blanqueamiento", "Resina"], result.Select(p => p.Name));
        Assert.False(result[0].IsActive);
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsAnEmptyList()
    {
        var result = await List.HandleAsync(new ListProceduresQuery());

        Assert.Empty(result);
    }
}
