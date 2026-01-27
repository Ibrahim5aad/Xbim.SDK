using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octopus.Blazor.Models;
using Octopus.Blazor.Services.Abstractions.Server;

namespace Octopus.Blazor.Tests.Configuration;

public class ConfigurationBindingTests
{
    [Fact]
    public void AddOctopusBlazorStandalone_WithConfiguration_BindsThemeOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Standalone:Theme:InitialTheme"] = "Light",
                ["Octopus:Standalone:Theme:LightAccentColor"] = "#ff0000",
                ["Octopus:Standalone:Theme:DarkAccentColor"] = "#00ff00"
            })
            .Build();

        // Act
        services.AddOctopusBlazorStandalone(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(ViewerTheme.Light, options.InitialTheme);
        Assert.Equal("#ff0000", options.LightAccentColor);
        Assert.Equal("#00ff00", options.DarkAccentColor);
    }

    [Fact]
    public void AddOctopusBlazorStandalone_WithConfiguration_BindsFileLoaderPanelOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Standalone:FileLoaderPanel:AllowIfcFiles"] = "false",
                ["Octopus:Standalone:FileLoaderPanel:AllowCustomHeaders"] = "false",
                ["Octopus:Standalone:FileLoaderPanel:AutoCloseOnLoad"] = "false"
            })
            .Build();

        // Act
        services.AddOctopusBlazorStandalone(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.False(options.FileLoaderPanel.AllowIfcFiles);
        Assert.False(options.FileLoaderPanel.AllowCustomHeaders);
        Assert.False(options.FileLoaderPanel.AutoCloseOnLoad);
    }

    [Fact]
    public void AddOctopusBlazorStandalone_WithConfiguration_BindsDemoModels()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Standalone:FileLoaderPanel:DemoModels:0:Name"] = "Test Model",
                ["Octopus:Standalone:FileLoaderPanel:DemoModels:0:Path"] = "models/test.wexbim",
                ["Octopus:Standalone:FileLoaderPanel:DemoModels:0:Description"] = "A test model",
                ["Octopus:Standalone:FileLoaderPanel:DemoModels:1:Name"] = "Second Model",
                ["Octopus:Standalone:FileLoaderPanel:DemoModels:1:Path"] = "models/second.wexbim"
            })
            .Build();

        // Act
        services.AddOctopusBlazorStandalone(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(2, options.FileLoaderPanel.DemoModels.Count);
        Assert.Equal("Test Model", options.FileLoaderPanel.DemoModels[0].Name);
        Assert.Equal("models/test.wexbim", options.FileLoaderPanel.DemoModels[0].Path);
        Assert.Equal("A test model", options.FileLoaderPanel.DemoModels[0].Description);
        Assert.Equal("Second Model", options.FileLoaderPanel.DemoModels[1].Name);
    }

    [Fact]
    public void AddOctopusBlazorStandalone_WithConfiguration_BindsSources()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Standalone:Sources:StaticAssets:0:RelativePath"] = "models/sample.wexbim",
                ["Octopus:Standalone:Sources:StaticAssets:0:Name"] = "Sample Model",
                ["Octopus:Standalone:Sources:Urls:0:Url"] = "https://example.com/model.wexbim",
                ["Octopus:Standalone:Sources:Urls:0:Name"] = "Remote Model"
            })
            .Build();

        // Act
        services.AddOctopusBlazorStandalone(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.StandaloneSources);
        Assert.Single(options.StandaloneSources.StaticAssets);
        Assert.Equal("models/sample.wexbim", options.StandaloneSources.StaticAssets[0].RelativePath);
        Assert.Single(options.StandaloneSources.Urls);
        Assert.Equal("https://example.com/model.wexbim", options.StandaloneSources.Urls[0].Url);
    }

    [Fact]
    public void AddOctopusBlazorServer_WithConfiguration_BindsAllOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Standalone:Theme:InitialTheme"] = "Light",
                ["Octopus:Standalone:FileLoaderPanel:AllowIfcFiles"] = "true"
            })
            .Build();

        // Act
        services.AddOctopusBlazorServer(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(ViewerTheme.Light, options.InitialTheme);
        Assert.True(options.FileLoaderPanel.AllowIfcFiles);
    }

    [Fact]
    public void AddOctopusBlazorPlatformConnected_WithConfiguration_BindsServerOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Server:BaseUrl"] = "https://api.example.com",
                ["Octopus:Server:RequireAuthentication"] = "true",
                ["Octopus:Server:TimeoutSeconds"] = "60"
            })
            .Build();

        // Act
        services.AddOctopusBlazorPlatformConnected(configuration);
        var provider = services.BuildServiceProvider();
        var serverOptions = provider.GetService<OctopusServerOptions>();

        // Assert
        Assert.NotNull(serverOptions);
        Assert.Equal("https://api.example.com", serverOptions.BaseUrl);
        Assert.True(serverOptions.RequireAuthentication);
        Assert.Equal(60, serverOptions.TimeoutSeconds);
    }

    [Fact]
    public void AddOctopusBlazorPlatformConnected_WithConfiguration_RegistersServerServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Server:BaseUrl"] = "https://api.example.com"
            })
            .Build();

        // Act
        services.AddOctopusBlazorPlatformConnected(configuration);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IWorkspacesService));
        Assert.Contains(services, d => d.ServiceType == typeof(IProjectsService));
        Assert.Contains(services, d => d.ServiceType == typeof(IFilesService));
        Assert.Contains(services, d => d.ServiceType == typeof(IModelsService));
    }

    [Fact]
    public void AddOctopusBlazorPlatformConnected_WithMissingBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Missing BaseUrl
                ["Octopus:Server:RequireAuthentication"] = "true"
            })
            .Build();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOctopusBlazorPlatformConnected(configuration));
        Assert.Contains("BaseUrl", ex.Message);
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void AddOctopusBlazorPlatformConnected_WithInvalidBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Octopus:Server:BaseUrl"] = "not-a-valid-url"
            })
            .Build();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOctopusBlazorPlatformConnected(configuration));
        Assert.Contains("valid HTTP or HTTPS URL", ex.Message);
    }

    [Fact]
    public void OctopusServerOptions_Validate_ThrowsOnMissingBaseUrl()
    {
        // Arrange
        var options = new OctopusServerOptions { BaseUrl = null };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public void OctopusServerOptions_Validate_ThrowsOnInvalidUrl()
    {
        // Arrange
        var options = new OctopusServerOptions { BaseUrl = "ftp://invalid.com" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("HTTP or HTTPS", ex.Message);
    }

    [Fact]
    public void OctopusServerOptions_Validate_SucceedsWithValidUrl()
    {
        // Arrange
        var options = new OctopusServerOptions { BaseUrl = "https://api.example.com" };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void FileLoaderPanelOptions_ToDemoModelList_ConvertsCorrectly()
    {
        // Arrange
        var options = new FileLoaderPanelOptions();
        options.AddDemoModel("Model 1", "path/to/model1.wexbim", "First model");
        options.AddDemoModel("Model 2", "https://example.com/model2.wexbim");

        // Act
        var demoModels = options.ToDemoModelList();

        // Assert
        Assert.Equal(2, demoModels.Count);
        Assert.Equal("Model 1", demoModels[0].Name);
        Assert.Equal("path/to/model1.wexbim", demoModels[0].Path);
        Assert.Equal("First model", demoModels[0].Description);
        Assert.Equal("Model 2", demoModels[1].Name);
        Assert.Null(demoModels[1].Description);
    }

    [Fact]
    public void ThemeOptions_GetViewerTheme_ReturnsCorrectTheme()
    {
        // Arrange & Act & Assert
        Assert.Equal(ViewerTheme.Light, new ThemeOptions { InitialTheme = "Light" }.GetViewerTheme());
        Assert.Equal(ViewerTheme.Light, new ThemeOptions { InitialTheme = "light" }.GetViewerTheme());
        Assert.Equal(ViewerTheme.Light, new ThemeOptions { InitialTheme = "LIGHT" }.GetViewerTheme());
        Assert.Equal(ViewerTheme.Dark, new ThemeOptions { InitialTheme = "Dark" }.GetViewerTheme());
        Assert.Equal(ViewerTheme.Dark, new ThemeOptions { InitialTheme = "invalid" }.GetViewerTheme());
        Assert.Equal(ViewerTheme.Dark, new ThemeOptions { InitialTheme = null! }.GetViewerTheme());
    }

    [Fact]
    public void SourcesConfig_ToStandaloneSourceOptions_ConvertsCorrectly()
    {
        // Arrange
        var config = new SourcesConfig
        {
            StaticAssets = new List<StaticAssetSourceConfig>
            {
                new() { RelativePath = "models/a.wexbim", Name = "Model A" }
            },
            Urls = new List<UrlSourceConfig>
            {
                new() { Url = "https://example.com/b.wexbim", Name = "Model B" }
            },
            LocalFiles = new List<LocalFileSourceConfig>
            {
                new() { FilePath = "/path/to/c.wexbim", Name = "Model C" }
            }
        };

        // Act
        var standaloneOptions = config.ToStandaloneSourceOptions();

        // Assert
        Assert.Single(standaloneOptions.StaticAssets);
        Assert.Equal("models/a.wexbim", standaloneOptions.StaticAssets[0].RelativePath);
        Assert.Single(standaloneOptions.Urls);
        Assert.Equal("https://example.com/b.wexbim", standaloneOptions.Urls[0].Url);
        Assert.Single(standaloneOptions.LocalFiles);
        Assert.Equal("/path/to/c.wexbim", standaloneOptions.LocalFiles[0].FilePath);
    }
}
