using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octopus.Server.Contracts;
using Octopus.Server.Domain.Entities;
using Octopus.Server.Persistence.EfCore;

using WorkspaceRole = Octopus.Server.Domain.Enums.WorkspaceRole;
using ProjectRole = Octopus.Server.Domain.Enums.ProjectRole;
using DomainFileKind = Octopus.Server.Domain.Enums.FileKind;
using DomainFileCategory = Octopus.Server.Domain.Enums.FileCategory;

namespace Octopus.Server.App.Tests.Endpoints;

public class ModelVersionEndpointsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDbName;

    public ModelVersionEndpointsTests()
    {
        _testDbName = $"test_{Guid.NewGuid()}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remove ALL DbContext-related services
                services.RemoveAll(typeof(DbContextOptions<OctopusDbContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(OctopusDbContext));

                // Add in-memory database for testing
                services.AddDbContext<OctopusDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDbName);
                });
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<WorkspaceDto> CreateWorkspaceAsync(string name = "Test Workspace")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/workspaces",
            new CreateWorkspaceRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>())!;
    }

    private async Task<ProjectDto> CreateProjectAsync(Guid workspaceId, string name = "Test Project")
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/projects",
            new CreateProjectRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private async Task<ModelDto> CreateModelAsync(Guid projectId, string name = "Test Model")
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/models",
            new CreateModelRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModelDto>())!;
    }

    private async Task<FileEntity> CreateFileInProjectAsync(Guid projectId, string name = "test.ifc")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();

        var file = new FileEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            ContentType = "application/x-step",
            SizeBytes = 1024,
            Kind = DomainFileKind.Source,
            Category = DomainFileCategory.Ifc,
            StorageProvider = "InMemory",
            StorageKey = $"test/{Guid.NewGuid()}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Files.Add(file);
        await dbContext.SaveChangesAsync();

        return file;
    }

    #region Create ModelVersion Tests

    [Fact]
    public async Task CreateModelVersion_ReturnsCreated_WithValidRequest()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var ifcFile = await CreateFileInProjectAsync(project.Id);

        var request = new CreateModelVersionRequest
        {
            IfcFileId = ifcFile.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var version = await response.Content.ReadFromJsonAsync<ModelVersionDto>();
        Assert.NotNull(version);
        Assert.Equal(model.Id, version.ModelId);
        Assert.Equal(ifcFile.Id, version.IfcFileId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(ProcessingStatus.Pending, version.Status);
        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.True(version.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task CreateModelVersion_IncrementsVersionNumber()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var ifcFile1 = await CreateFileInProjectAsync(project.Id, "v1.ifc");
        var ifcFile2 = await CreateFileInProjectAsync(project.Id, "v2.ifc");
        var ifcFile3 = await CreateFileInProjectAsync(project.Id, "v3.ifc");

        // Create first version
        await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile1.Id });

        // Create second version
        await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile2.Id });

        // Act - Create third version
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile3.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<ModelVersionDto>();
        Assert.Equal(3, version!.VersionNumber);
    }

    [Fact]
    public async Task CreateModelVersion_ReturnsBadRequest_WhenIfcFileNotFound()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var nonExistentFileId = Guid.NewGuid();

        var request = new CreateModelVersionRequest
        {
            IfcFileId = nonExistentFileId
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateModelVersion_ReturnsBadRequest_WhenIfcFileInDifferentProject()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project1 = await CreateProjectAsync(workspace.Id, "Project 1");
        var project2 = await CreateProjectAsync(workspace.Id, "Project 2");
        var model = await CreateModelAsync(project1.Id);
        var ifcFileInOtherProject = await CreateFileInProjectAsync(project2.Id);

        var request = new CreateModelVersionRequest
        {
            IfcFileId = ifcFileInOtherProject.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateModelVersion_ReturnsBadRequest_WhenIfcFileIsDeleted()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);

        // Create and then mark as deleted
        var ifcFile = await CreateFileInProjectAsync(project.Id);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();
        var file = await dbContext.Files.FindAsync(ifcFile.Id);
        file!.IsDeleted = true;
        file.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        var request = new CreateModelVersionRequest
        {
            IfcFileId = ifcFile.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateModelVersion_ReturnsNotFound_WhenModelNotFound()
    {
        // Arrange
        var randomModelId = Guid.NewGuid();

        var request = new CreateModelVersionRequest
        {
            IfcFileId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{randomModelId}/versions", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateModelVersion_ReturnsForbidden_WhenUserIsViewer()
    {
        // Arrange - Create model where user only has Viewer access
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Viewer Workspace",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Workspaces.Add(workspace);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Test Project",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Projects.Add(project);

        var model = new Model
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Test Model",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Models.Add(model);

        var ifcFile = new FileEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "test.ifc",
            ContentType = "application/x-step",
            SizeBytes = 1024,
            Kind = DomainFileKind.Source,
            Category = DomainFileCategory.Ifc,
            StorageProvider = "InMemory",
            StorageKey = "test/key",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Files.Add(ifcFile);

        // Ensure dev user is provisioned
        await _client.GetAsync("/api/v1/me");
        var devUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Subject == "dev-user");

        if (devUser != null)
        {
            // Add user as Member to workspace (which gives only Viewer access to projects)
            dbContext.WorkspaceMemberships.Add(new WorkspaceMembership
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = devUser.Id,
                Role = WorkspaceRole.Member,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateModelVersion_SetsStatusToPending()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var ifcFile = await CreateFileInProjectAsync(project.Id);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<ModelVersionDto>();
        Assert.Equal(ProcessingStatus.Pending, version!.Status);
    }

    #endregion

    #region List ModelVersions Tests

    [Fact]
    public async Task ListModelVersions_ReturnsVersions_WhenUserHasAccess()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var ifcFile1 = await CreateFileInProjectAsync(project.Id, "v1.ifc");
        var ifcFile2 = await CreateFileInProjectAsync(project.Id, "v2.ifc");

        await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile1.Id });
        await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile2.Id });

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/models/{model.Id}/versions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedList<ModelVersionDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task ListModelVersions_ReturnsPagedResults()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);

        for (int i = 0; i < 5; i++)
        {
            var ifcFile = await CreateFileInProjectAsync(project.Id, $"v{i}.ifc");
            await _client.PostAsJsonAsync(
                $"/api/v1/models/{model.Id}/versions",
                new CreateModelVersionRequest { IfcFileId = ifcFile.Id });
        }

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/models/{model.Id}/versions?page=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedList<ModelVersionDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task ListModelVersions_ReturnsEmptyList_WhenNoVersions()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/models/{model.Id}/versions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedList<ModelVersionDto>>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListModelVersions_ReturnsNotFound_WhenModelNotFound()
    {
        // Arrange
        var randomModelId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/models/{randomModelId}/versions");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListModelVersions_OrderedByVersionNumberDescending()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);

        for (int i = 0; i < 3; i++)
        {
            var ifcFile = await CreateFileInProjectAsync(project.Id, $"v{i}.ifc");
            await _client.PostAsJsonAsync(
                $"/api/v1/models/{model.Id}/versions",
                new CreateModelVersionRequest { IfcFileId = ifcFile.Id });
        }

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/models/{model.Id}/versions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedList<ModelVersionDto>>();
        Assert.Equal(3, result!.Items[0].VersionNumber);
        Assert.Equal(2, result.Items[1].VersionNumber);
        Assert.Equal(1, result.Items[2].VersionNumber);
    }

    #endregion

    #region Get ModelVersion Tests

    [Fact]
    public async Task GetModelVersion_ReturnsVersion_WhenUserHasAccess()
    {
        // Arrange
        var workspace = await CreateWorkspaceAsync();
        var project = await CreateProjectAsync(workspace.Id);
        var model = await CreateModelAsync(project.Id);
        var ifcFile = await CreateFileInProjectAsync(project.Id);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/models/{model.Id}/versions",
            new CreateModelVersionRequest { IfcFileId = ifcFile.Id });
        var created = await createResponse.Content.ReadFromJsonAsync<ModelVersionDto>();

        // Act
        var response = await _client.GetAsync($"/api/v1/modelversions/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<ModelVersionDto>();
        Assert.NotNull(version);
        Assert.Equal(created.Id, version.Id);
        Assert.Equal(model.Id, version.ModelId);
        Assert.Equal(ifcFile.Id, version.IfcFileId);
        Assert.Equal(ProcessingStatus.Pending, version.Status);
    }

    [Fact]
    public async Task GetModelVersion_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/modelversions/{randomId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetModelVersion_ReturnsNotFound_WhenUserHasNoAccessToProject()
    {
        // Arrange - Create version in project user doesn't have access to
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Inaccessible Workspace",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Workspaces.Add(workspace);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Hidden Project",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Projects.Add(project);

        var model = new Model
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Hidden Model",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Models.Add(model);

        var ifcFile = new FileEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "test.ifc",
            ContentType = "application/x-step",
            SizeBytes = 1024,
            Kind = DomainFileKind.Source,
            Category = DomainFileCategory.Ifc,
            StorageProvider = "InMemory",
            StorageKey = "test/key",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Files.Add(ifcFile);

        var version = new ModelVersion
        {
            Id = Guid.NewGuid(),
            ModelId = model.Id,
            VersionNumber = 1,
            IfcFileId = ifcFile.Id,
            Status = Domain.Enums.ProcessingStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ModelVersions.Add(version);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/modelversions/{version.Id}");

        // Assert - Returns 404 to avoid revealing version existence
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
