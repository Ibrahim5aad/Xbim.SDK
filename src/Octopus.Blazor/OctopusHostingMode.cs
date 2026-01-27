namespace Octopus.Blazor;

/// <summary>
/// Indicates the current Octopus.Blazor hosting mode.
/// </summary>
public enum OctopusHostingMode
{
    /// <summary>
    /// Standalone viewer mode. Suitable for loading WexBIM from local files, static assets, or URLs
    /// without Octopus.Server connectivity.
    /// <para>
    /// In this mode, the <c>FileLoaderPanel</c> component is available for loading models.
    /// </para>
    /// </summary>
    Standalone,

    /// <summary>
    /// Platform-connected mode. Connected to Octopus.Server for full workspace, project,
    /// file, and model management functionality.
    /// <para>
    /// In this mode, models are loaded through the workspace/project/model navigation
    /// rather than through direct file selection.
    /// </para>
    /// </summary>
    PlatformConnected
}

/// <summary>
/// Provides information about the current Octopus.Blazor hosting mode.
/// <para>
/// Inject this service to determine which mode the application is running in
/// and to conditionally enable or disable functionality.
/// </para>
/// </summary>
public interface IOctopusHostingModeProvider
{
    /// <summary>
    /// Gets the current hosting mode.
    /// </summary>
    OctopusHostingMode HostingMode { get; }

    /// <summary>
    /// Gets a value indicating whether the application is in standalone mode.
    /// </summary>
    bool IsStandalone { get; }

    /// <summary>
    /// Gets a value indicating whether the application is in platform-connected mode.
    /// </summary>
    bool IsPlatformConnected { get; }
}

/// <summary>
/// Hosting mode provider for standalone viewer applications.
/// </summary>
internal sealed class StandaloneHostingModeProvider : IOctopusHostingModeProvider
{
    /// <inheritdoc />
    public OctopusHostingMode HostingMode => OctopusHostingMode.Standalone;

    /// <inheritdoc />
    public bool IsStandalone => true;

    /// <inheritdoc />
    public bool IsPlatformConnected => false;
}

/// <summary>
/// Hosting mode provider for platform-connected applications.
/// </summary>
internal sealed class PlatformConnectedHostingModeProvider : IOctopusHostingModeProvider
{
    /// <inheritdoc />
    public OctopusHostingMode HostingMode => OctopusHostingMode.PlatformConnected;

    /// <inheritdoc />
    public bool IsStandalone => false;

    /// <inheritdoc />
    public bool IsPlatformConnected => true;
}
