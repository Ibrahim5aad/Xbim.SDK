using Microsoft.EntityFrameworkCore;
using Octopus.Server.Abstractions.Auth;
using Octopus.Server.Domain.Enums;
using Octopus.Server.Persistence.EfCore;

namespace Octopus.Server.App.Auth;

/// <summary>
/// Implementation of the authorization service that checks user permissions
/// against workspace and project memberships in the database.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;
    private readonly OctopusDbContext _dbContext;

    public AuthorizationService(IUserContext userContext, OctopusDbContext dbContext)
    {
        _userContext = userContext;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessWorkspaceAsync(
        Guid workspaceId,
        WorkspaceRole minimumRole = WorkspaceRole.Guest,
        CancellationToken cancellationToken = default)
    {
        var role = await GetWorkspaceRoleAsync(workspaceId, cancellationToken);
        return role.HasValue && role.Value >= minimumRole;
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessProjectAsync(
        Guid projectId,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default)
    {
        var role = await GetProjectRoleAsync(projectId, cancellationToken);
        return role.HasValue && role.Value >= minimumRole;
    }

    /// <inheritdoc />
    public async Task<WorkspaceRole?> GetWorkspaceRoleAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || !_userContext.UserId.HasValue)
        {
            return null;
        }

        var membership = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.WorkspaceId == workspaceId && m.UserId == _userContext.UserId.Value,
                cancellationToken);

        return membership?.Role;
    }

    /// <inheritdoc />
    public async Task<ProjectRole?> GetProjectRoleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || !_userContext.UserId.HasValue)
        {
            return null;
        }

        var userId = _userContext.UserId.Value;

        // First check direct project membership
        var projectMembership = await _dbContext.ProjectMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.ProjectId == projectId && m.UserId == userId,
                cancellationToken);

        if (projectMembership != null)
        {
            return projectMembership.Role;
        }

        // Check workspace-level access (Admin/Owner get ProjectAdmin access to all projects in workspace)
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            return null;
        }

        var workspaceMembership = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.WorkspaceId == project.WorkspaceId && m.UserId == userId,
                cancellationToken);

        if (workspaceMembership == null)
        {
            return null;
        }

        // Workspace Admin and Owner get ProjectAdmin access to all projects
        if (workspaceMembership.Role >= WorkspaceRole.Admin)
        {
            return ProjectRole.ProjectAdmin;
        }

        // Workspace Member gets Viewer access to all projects
        if (workspaceMembership.Role >= WorkspaceRole.Member)
        {
            return ProjectRole.Viewer;
        }

        // Workspace Guest has no implicit project access
        return null;
    }

    /// <inheritdoc />
    public async Task RequireWorkspaceAccessAsync(
        Guid workspaceId,
        WorkspaceRole minimumRole = WorkspaceRole.Guest,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        if (!await CanAccessWorkspaceAsync(workspaceId, minimumRole, cancellationToken))
        {
            throw new ForbiddenAccessException(
                $"Access denied. Minimum workspace role required: {minimumRole}");
        }
    }

    /// <inheritdoc />
    public async Task RequireProjectAccessAsync(
        Guid projectId,
        ProjectRole minimumRole = ProjectRole.Viewer,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        if (!await CanAccessProjectAsync(projectId, minimumRole, cancellationToken))
        {
            throw new ForbiddenAccessException(
                $"Access denied. Minimum project role required: {minimumRole}");
        }
    }
}
