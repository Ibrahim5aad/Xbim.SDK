using Microsoft.Extensions.DependencyInjection;
using Octopus.Blazor.Services.Abstractions.Server;
using Octopus.Blazor.Services.Server;
using Octopus.Client;

namespace Octopus.Blazor.Tests.Server;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOctopusBlazorServerConnected_ShouldRegisterServerServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000");

        // Assert - All server-backed services should be registered
        Assert.Contains(services, d => d.ServiceType == typeof(IWorkspacesService));
        Assert.Contains(services, d => d.ServiceType == typeof(IProjectsService));
        Assert.Contains(services, d => d.ServiceType == typeof(IFilesService));
        Assert.Contains(services, d => d.ServiceType == typeof(IModelsService));
        Assert.Contains(services, d => d.ServiceType == typeof(IUsageService));
        Assert.Contains(services, d => d.ServiceType == typeof(IProcessingService));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_ShouldRegisterApiClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000");

        // Assert - API client should be registered
        Assert.Contains(services, d => d.ServiceType == typeof(IOctopusApiClient));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_WithEmptyBaseUrl_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddOctopusBlazorServerConnected(""));
        Assert.Throws<ArgumentException>(() => services.AddOctopusBlazorServerConnected((string)null!));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_WithTokenProvider_ShouldRegisterProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var tokenProvider = new StaticTokenProvider("test-token");

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000", tokenProvider);

        // Assert - Token provider should be registered
        Assert.Contains(services, d => d.ServiceType == typeof(IAuthTokenProvider));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_WithTokenFactory_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        Func<Task<string?>> tokenFactory = () => Task.FromResult<string?>("test-token");

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000", tokenFactory);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IWorkspacesService));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_ServicesShouldBeSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOctopusBlazorServerConnected("https://localhost:5000");

        // Assert - Services should be registered as singletons
        var workspacesDescriptor = services.First(d => d.ServiceType == typeof(IWorkspacesService));
        Assert.Equal(ServiceLifetime.Singleton, workspacesDescriptor.Lifetime);

        var projectsDescriptor = services.First(d => d.ServiceType == typeof(IProjectsService));
        Assert.Equal(ServiceLifetime.Singleton, projectsDescriptor.Lifetime);
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_ShouldAlsoRegisterStandaloneServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000");

        // Assert - Standalone services should also be available
        Assert.Contains(services, d => d.ServiceType == typeof(Octopus.Blazor.Services.ThemeService));
        Assert.Contains(services, d => d.ServiceType == typeof(Octopus.Blazor.Services.Abstractions.IPropertyService));
        Assert.Contains(services, d => d.ServiceType == typeof(Octopus.Blazor.Services.Abstractions.IWexBimSourceProvider));
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_WithBlazorOptions_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOctopusBlazorServerConnected("https://localhost:5000", opt =>
        {
            opt.InitialTheme = Octopus.Blazor.Models.ViewerTheme.Dark;
        });
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Octopus.Blazor.OctopusBlazorOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(Octopus.Blazor.Models.ViewerTheme.Dark, options.InitialTheme);
    }

    [Fact]
    public void AddOctopusBlazorServerConnected_WithClientOptions_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert - Should not throw
        services.AddOctopusBlazorServerConnected(
            "https://localhost:5000",
            blazorOpt => { },
            clientOpt => { clientOpt.TokenFactory = _ => Task.FromResult<string?>("token"); });
    }

    [Fact]
    public void ServerServices_ShouldImplementCorrectInterfaces()
    {
        // Assert
        Assert.True(typeof(IWorkspacesService).IsAssignableFrom(typeof(WorkspacesService)));
        Assert.True(typeof(IProjectsService).IsAssignableFrom(typeof(ProjectsService)));
        Assert.True(typeof(IFilesService).IsAssignableFrom(typeof(FilesService)));
        Assert.True(typeof(IModelsService).IsAssignableFrom(typeof(ModelsService)));
        Assert.True(typeof(IUsageService).IsAssignableFrom(typeof(UsageService)));
        Assert.True(typeof(IProcessingService).IsAssignableFrom(typeof(ProcessingService)));
    }
}
