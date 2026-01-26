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
}
