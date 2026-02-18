namespace Xbim.WexServer.Storage.LocalDisk;

/// <summary>
/// Configuration options for Local Disk Storage provider.
/// </summary>
public class LocalDiskStorageOptions
{
    /// <summary>
    /// The base directory path where files will be stored.
    /// Default: "wex-storage" in the application's content root.
    /// </summary>
    public string BasePath { get; set; } = "wex-storage";

    /// <summary>
    /// Whether to create the base directory if it doesn't exist.
    /// Default: true
    /// </summary>
    public bool CreateDirectoryIfNotExists { get; set; } = true;
}
