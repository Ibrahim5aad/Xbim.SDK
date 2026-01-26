namespace Octopus.Blazor.Services.Abstractions;

/// <summary>
/// Represents a source that can provide WexBIM model data for viewing.
/// <para>
/// Implementations may load WexBIM from:
/// <list type="bullet">
///   <item>Local files or static web assets (standalone)</item>
///   <item>Remote URLs (standalone or server-connected)</item>
///   <item>Octopus.Server model versions (server-connected)</item>
///   <item>In-memory byte arrays (any mode)</item>
/// </list>
/// </para>
/// </summary>
public interface IWexBimSource
{
    /// <summary>
    /// Unique identifier for this source instance.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of source (e.g., "Url", "LocalFile", "Server", "InMemory").
    /// </summary>
    WexBimSourceType SourceType { get; }

    /// <summary>
    /// Whether this source is currently available and can provide data.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the WexBIM data as a byte array.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The WexBIM data, or null if unavailable.</returns>
    Task<byte[]?> GetDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a URL that can be used to load the WexBIM directly (if available).
    /// <para>
    /// Not all sources support direct URL loading. Check <see cref="SupportsDirectUrl"/>
    /// before calling this method.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL, or null if direct URL loading is not supported.</returns>
    Task<string?> GetUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this source supports loading via direct URL.
    /// </summary>
    bool SupportsDirectUrl { get; }
}

/// <summary>
/// The type of WexBIM source.
/// </summary>
public enum WexBimSourceType
{
    /// <summary>
    /// WexBIM loaded from a static URL (e.g., wwwroot, CDN).
    /// </summary>
    Url,

    /// <summary>
    /// WexBIM loaded from a local file path (server-side only).
    /// </summary>
    LocalFile,

    /// <summary>
    /// WexBIM loaded from Octopus.Server via the API.
    /// </summary>
    Server,

    /// <summary>
    /// WexBIM provided as in-memory byte array.
    /// </summary>
    InMemory,

    /// <summary>
    /// WexBIM generated from IFC processing.
    /// </summary>
    ProcessedIfc
}
