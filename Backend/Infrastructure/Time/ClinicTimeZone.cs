using Microsoft.Extensions.Configuration;

namespace Infrastructure.Time;

/// <summary>Reads the clinic's time zone from configuration ("Clinic:TimeZone", an IANA id).</summary>
public static class ClinicTimeZone
{
    public const string ConfigurationKey = "Clinic:TimeZone";
    public const string DefaultId = "America/Guayaquil";

    /// <summary>
    /// Returns the configured zone, or America/Guayaquil when none is set. An unknown id stops the start,
    /// because every "today" and every working hour would be computed in the wrong zone.
    /// </summary>
    public static TimeZoneInfo Resolve(IConfiguration configuration)
    {
        var id = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(id))
        {
            id = DefaultId;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"{ConfigurationKey} \"{id}\" is not a known time zone.", ex);
        }
    }
}
