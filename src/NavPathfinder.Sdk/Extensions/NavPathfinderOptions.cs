namespace NavPathfinder.Sdk.Extensions;

/// <summary>
/// Configuration options for NavPathfinder SDK registration with <see cref="ServiceCollectionExtensions.AddNavPathfinder"/>.
/// </summary>
public sealed class NavPathfinderOptions
{
    /// <summary>
    /// Optional license key. Omit (or leave <c>null</c>) for unlicensed sequential mode (free).
    /// Supply a valid key to enable the full licensed engine.
    /// </summary>
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Root directory used to persist tuner state between sessions.
    /// Defaults to <c>{TempPath}/navpathfinder</c> when <c>null</c>.
    /// </summary>
    public string? TunerStatePath { get; set; }
}
