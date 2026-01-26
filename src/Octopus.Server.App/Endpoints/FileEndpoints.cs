using Microsoft.EntityFrameworkCore;
using Octopus.Server.Abstractions.Auth;
using Octopus.Server.Contracts;
using Octopus.Server.Persistence.EfCore;

using ProjectRole = Octopus.Server.Domain.Enums.ProjectRole;
using FileKind = Octopus.Server.Contracts.FileKind;
using FileCategory = Octopus.Server.Contracts.FileCategory;

namespace Octopus.Server.App.Endpoints;

/// <summary>
/// File API endpoints for managing files.
/// </summary>
public static class FileEndpoints
{
    /// <summary>
    /// Maps file-related endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        // Project-scoped file endpoints
        var projectFilesGroup = app.MapGroup("/api/v1/projects/{projectId:guid}/files")
            .WithTags("Files")
            .RequireAuthorization();

        projectFilesGroup.MapGet("", ListFiles)
            .WithName("ListFiles")
            .WithOpenApi();

        return app;
    }

    /// <summary>
    /// Lists files in a project with optional filtering by kind and category.
    /// Requires at least Viewer role in the project.
    /// </summary>
    private static async Task<IResult> ListFiles(
        Guid projectId,
        IUserContext userContext,
        IAuthorizationService authZ,
        OctopusDbContext dbContext,
        FileKind? kind = null,
        FileCategory? category = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        // Require at least Viewer role to list files
        await authZ.RequireProjectAccessAsync(projectId, ProjectRole.Viewer, cancellationToken);

        // Verify project exists
        var projectExists = await dbContext.Projects
            .AnyAsync(p => p.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            return Results.NotFound(new { error = "Not Found", message = "Project not found." });
        }

        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Build query - exclude deleted files by default
        var query = dbContext.Files
            .Where(f => f.ProjectId == projectId && !f.IsDeleted);

        // Apply optional filters
        if (kind.HasValue)
        {
            var domainKind = (Domain.Enums.FileKind)(int)kind.Value;
            query = query.Where(f => f.Kind == domainKind);
        }

        if (category.HasValue)
        {
            var domainCategory = (Domain.Enums.FileCategory)(int)category.Value;
            query = query.Where(f => f.Category == domainCategory);
        }

        // Order by creation date descending (newest first)
        query = query.OrderByDescending(f => f.CreatedAt);

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Get paginated items
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .Select(f => new FileDto
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                Name = f.Name,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes,
                Checksum = f.Checksum,
                Kind = (FileKind)(int)f.Kind,
                Category = (FileCategory)(int)f.Category,
                StorageProvider = f.StorageProvider,
                StorageKey = f.StorageKey,
                IsDeleted = f.IsDeleted,
                CreatedAt = f.CreatedAt,
                DeletedAt = f.DeletedAt
            })
            .ToListAsync(cancellationToken);

        var result = new PagedList<FileDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Results.Ok(result);
    }
}
