using Application.Common;
using Infrastructure.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiRest.Tests;

/// <summary>The clinic's time zone comes from configuration and a wrong one stops the start.</summary>
public class ClinicTimeZoneTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private static IConfiguration Settings(string? timeZone) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ClinicTimeZone.ConfigurationKey] = timeZone })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("America/Guayaquil")]
    public void Resolve_MissingOrGuayaquil_IsUtcMinusFiveWithoutDaylightSaving(string? timeZone)
    {
        var zone = ClinicTimeZone.Resolve(Settings(timeZone));

        Assert.Equal(TimeSpan.FromHours(-5), zone.BaseUtcOffset);
        Assert.False(zone.SupportsDaylightSavingTime);
    }

    [Fact]
    public void Resolve_UnknownZone_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ClinicTimeZone.Resolve(Settings("Mars/Olympus")));

        Assert.Contains("Clinic:TimeZone", ex.Message);
    }

    [Fact]
    public void Api_UsesTheConfiguredZoneForItsClinicClock()
    {
        var clock = factory.Services.GetRequiredService<ClinicClock>();

        Assert.Equal(TimeSpan.FromHours(-5), clock.Now.Offset);
    }

    [Fact]
    public void Api_WithAnUnknownZone_DoesNotStart()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting(ClinicTimeZone.ConfigurationKey, "Mars/Olympus"));

        Assert.Throws<InvalidOperationException>(() => misconfigured.CreateClient());
    }
}
