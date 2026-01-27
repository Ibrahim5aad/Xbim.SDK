using Octopus.Blazor.Models;

namespace Octopus.Blazor;

/// <summary>
/// Configuration options for Octopus.Blazor services.
/// </summary>
public class OctopusBlazorOptions
{
    /// <summary>
    /// Gets or sets the initial viewer theme. Defaults to <see cref="ViewerTheme.Dark"/>.
    /// </summary>
    public ViewerTheme InitialTheme { get; set; } = ViewerTheme.Dark;

    /// <summary>
    /// Gets or sets the accent color for light theme. Defaults to "#0969da".
    /// </summary>
    public string LightAccentColor { get; set; } = "#0969da";

    /// <summary>
    /// Gets or sets the accent color for dark theme. Defaults to "#1e7e34".
    /// </summary>
    public string DarkAccentColor { get; set; } = "#1e7e34";

    /// <summary>
    /// Gets or sets the background color for light theme. Defaults to "#ffffff".
    /// </summary>
    public string LightBackgroundColor { get; set; } = "#ffffff";

    /// <summary>
    /// Gets or sets the background color for dark theme. Defaults to "#404040".
    /// </summary>
    public string DarkBackgroundColor { get; set; } = "#404040";

    /// <summary>
    /// Gets or sets the standalone WexBIM source configuration.
    /// <para>
    /// Configure this to automatically load WexBIM models from static assets, URLs, or local files.
    /// </para>
    /// </summary>
    public StandaloneSourceOptions? StandaloneSources { get; set; }

    /// <summary>
    /// Gets or sets the FileLoaderPanel configuration options.
    /// <para>
    /// Configure this to customize the behavior of the FileLoaderPanel component,
    /// including demo models, default paths, and feature toggles.
    /// </para>
    /// </summary>
    public FileLoaderPanelOptions FileLoaderPanel { get; set; } = new();
}

/// <summary>
/// Configuration options for the FileLoaderPanel component.
/// </summary>
public class FileLoaderPanelOptions
{
    /// <summary>
    /// Gets or sets the list of demo models to display in the FileLoaderPanel.
    /// </summary>
    public List<DemoModelConfig> DemoModels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to allow loading IFC files. Defaults to true.
    /// <para>
    /// When enabled, the FileLoaderPanel will accept .ifc and .ifczip files for processing.
    /// Requires IFC processing capability (use <c>AddOctopusBlazorServer</c> for Blazor Server).
    /// </para>
    /// </summary>
    public bool AllowIfcFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow custom HTTP headers for URL loading. Defaults to true.
    /// </summary>
    public bool AllowCustomHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically close the panel after loading a model. Defaults to true.
    /// </summary>
    public bool AutoCloseOnLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets the default URL to pre-populate in the URL input field.
    /// </summary>
    public string? DefaultUrl { get; set; }

    /// <summary>
    /// Adds a demo model configuration.
    /// </summary>
    /// <param name="name">The display name of the demo model.</param>
    /// <param name="path">The path or URL to the model file.</param>
    /// <param name="description">Optional description of the demo model.</param>
    /// <returns>This instance for chaining.</returns>
    public FileLoaderPanelOptions AddDemoModel(string name, string path, string? description = null)
    {
        DemoModels.Add(new DemoModelConfig { Name = name, Path = path, Description = description });
        return this;
    }

    /// <summary>
    /// Converts the demo model configurations to the component-friendly <see cref="Models.DemoModel"/> list.
    /// </summary>
    /// <returns>A list of <see cref="Models.DemoModel"/> instances.</returns>
    public List<Models.DemoModel> ToDemoModelList()
    {
        return DemoModels.Select(c => new Models.DemoModel
        {
            Name = c.Name,
            Path = c.Path,
            Description = c.Description
        }).ToList();
    }
}

/// <summary>
/// Configuration for a demo model in the FileLoaderPanel.
/// </summary>
public class DemoModelConfig
{
    /// <summary>
    /// Gets or sets the display name of the demo model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path or URL to the model file.
    /// <para>
    /// Supports:
    /// <list type="bullet">
    ///   <item>Relative paths within wwwroot (e.g., "models/house.wexbim")</item>
    ///   <item>Paths with ~/ prefix for wwwroot (e.g., "~/models/house.wexbim")</item>
    ///   <item>Absolute URLs (e.g., "https://cdn.example.com/model.wexbim")</item>
    /// </list>
    /// </para>
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for the demo model.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Configuration options for Octopus.Server connectivity in PlatformConnected mode.
/// <para>
/// Bind this from the "Octopus:Server" configuration section.
/// </para>
/// </summary>
public class OctopusServerOptions
{
    /// <summary>
    /// The configuration section path for server options.
    /// </summary>
    public const string SectionName = "Octopus:Server";

    /// <summary>
    /// Gets or sets the base URL of the Octopus.Server API.
    /// <para>
    /// Required for PlatformConnected mode. Example: "https://api.octopus.example.com"
    /// </para>
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets whether to require authentication. Defaults to true.
    /// <para>
    /// When true, requests to the server will include authentication tokens.
    /// </para>
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout in seconds for API requests. Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Validates the server options and throws if misconfigured.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException(
                $"Octopus.Server configuration is invalid: 'BaseUrl' is required. " +
                $"Configure the '{SectionName}:BaseUrl' setting in appsettings.json or call " +
                $"AddOctopusBlazorPlatformConnected(baseUrl) with a valid URL.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Octopus.Server configuration is invalid: 'BaseUrl' must be a valid HTTP or HTTPS URL. " +
                $"Current value: '{BaseUrl}'. " +
                $"Example: \"https://api.octopus.example.com\"");
        }
    }
}

/// <summary>
/// Configuration options for standalone mode.
/// <para>
/// Bind this from the "Octopus:Standalone" configuration section.
/// </para>
/// </summary>
public class OctopusStandaloneOptions
{
    /// <summary>
    /// The configuration section path for standalone options.
    /// </summary>
    public const string SectionName = "Octopus:Standalone";

    /// <summary>
    /// Gets or sets the theme configuration.
    /// </summary>
    public ThemeOptions Theme { get; set; } = new();

    /// <summary>
    /// Gets or sets the FileLoaderPanel configuration.
    /// </summary>
    public FileLoaderPanelOptions FileLoaderPanel { get; set; } = new();

    /// <summary>
    /// Gets or sets the sources configuration.
    /// </summary>
    public SourcesConfig Sources { get; set; } = new();
}

/// <summary>
/// Theme configuration options for binding from configuration.
/// </summary>
public class ThemeOptions
{
    /// <summary>
    /// Gets or sets the initial theme. Valid values: "Dark", "Light". Defaults to "Dark".
    /// </summary>
    public string InitialTheme { get; set; } = "Dark";

    /// <summary>
    /// Gets or sets the accent color for light theme.
    /// </summary>
    public string LightAccentColor { get; set; } = "#0969da";

    /// <summary>
    /// Gets or sets the accent color for dark theme.
    /// </summary>
    public string DarkAccentColor { get; set; } = "#1e7e34";

    /// <summary>
    /// Gets or sets the background color for light theme.
    /// </summary>
    public string LightBackgroundColor { get; set; } = "#ffffff";

    /// <summary>
    /// Gets or sets the background color for dark theme.
    /// </summary>
    public string DarkBackgroundColor { get; set; } = "#404040";

    /// <summary>
    /// Converts the theme string to a <see cref="ViewerTheme"/> enum value.
    /// </summary>
    public ViewerTheme GetViewerTheme()
    {
        return InitialTheme?.Equals("Light", StringComparison.OrdinalIgnoreCase) == true
            ? ViewerTheme.Light
            : ViewerTheme.Dark;
    }
}

/// <summary>
/// Sources configuration for binding from configuration.
/// </summary>
public class SourcesConfig
{
    /// <summary>
    /// Gets or sets the list of static asset sources (paths within wwwroot).
    /// </summary>
    public List<StaticAssetSourceConfig> StaticAssets { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of URL sources.
    /// </summary>
    public List<UrlSourceConfig> Urls { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of local file sources (Blazor Server only).
    /// </summary>
    public List<LocalFileSourceConfig> LocalFiles { get; set; } = new();

    /// <summary>
    /// Converts to <see cref="StandaloneSourceOptions"/> for programmatic use.
    /// </summary>
    public StandaloneSourceOptions ToStandaloneSourceOptions()
    {
        var options = new StandaloneSourceOptions();
        foreach (var asset in StaticAssets)
        {
            options.AddStaticAsset(asset.RelativePath, asset.Name);
        }
        foreach (var url in Urls)
        {
            options.AddUrl(url.Url, url.Name);
        }
        foreach (var file in LocalFiles)
        {
            options.AddLocalFile(file.FilePath, file.Name);
        }
        return options;
    }
}

/// <summary>
/// Configuration options for standalone WexBIM sources.
/// </summary>
public class StandaloneSourceOptions
{
    /// <summary>
    /// Gets the list of static asset sources (relative paths within wwwroot).
    /// </summary>
    public List<StaticAssetSourceConfig> StaticAssets { get; } = new();

    /// <summary>
    /// Gets the list of URL sources (HTTP/HTTPS URLs).
    /// </summary>
    public List<UrlSourceConfig> Urls { get; } = new();

    /// <summary>
    /// Gets the list of local file sources (absolute file paths, Blazor Server only).
    /// </summary>
    public List<LocalFileSourceConfig> LocalFiles { get; } = new();

    /// <summary>
    /// Adds a static asset source from wwwroot.
    /// </summary>
    /// <param name="relativePath">Relative path within wwwroot (e.g., "models/sample.wexbim").</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>This instance for chaining.</returns>
    public StandaloneSourceOptions AddStaticAsset(string relativePath, string? name = null)
    {
        StaticAssets.Add(new StaticAssetSourceConfig { RelativePath = relativePath, Name = name });
        return this;
    }

    /// <summary>
    /// Adds a URL source.
    /// </summary>
    /// <param name="url">The URL of the WexBIM file.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>This instance for chaining.</returns>
    public StandaloneSourceOptions AddUrl(string url, string? name = null)
    {
        Urls.Add(new UrlSourceConfig { Url = url, Name = name });
        return this;
    }

    /// <summary>
    /// Adds a local file source (Blazor Server only).
    /// </summary>
    /// <param name="filePath">The absolute path to the WexBIM file.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>This instance for chaining.</returns>
    public StandaloneSourceOptions AddLocalFile(string filePath, string? name = null)
    {
        LocalFiles.Add(new LocalFileSourceConfig { FilePath = filePath, Name = name });
        return this;
    }
}

/// <summary>
/// Configuration for a static asset WexBIM source.
/// </summary>
public class StaticAssetSourceConfig
{
    /// <summary>
    /// Gets or sets the relative path within wwwroot.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional display name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Configuration for a URL WexBIM source.
/// </summary>
public class UrlSourceConfig
{
    /// <summary>
    /// Gets or sets the URL of the WexBIM file.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the optional HTTP headers to include in requests.
    /// </summary>
    public Dictionary<string, string> Headers { get; } = new();
}

/// <summary>
/// Configuration for a local file WexBIM source.
/// </summary>
public class LocalFileSourceConfig
{
    /// <summary>
    /// Gets or sets the absolute path to the WexBIM file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional display name.
    /// </summary>
    public string? Name { get; set; }
}
