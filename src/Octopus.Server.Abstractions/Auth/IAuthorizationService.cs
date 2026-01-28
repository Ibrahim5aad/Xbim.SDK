using Octopus.Server.Domain.Enums;

namespace Octopus.Server.Abstractions.Auth;

/// <summary>
/// Authorization service for checking user permissions (RBAC and scope-based).
/// </summary>
public interface IAuthorizationService
{
    #region Scope-based Authorization

    /// <summary>
    /// Gets all scopes present in the current request's token.
    /// </summary>
    /// <returns>The scopes from the token, or an empty set if no scopes.</returns>
    IReadOnlySet<string> GetScopes();

    /// <summary>
    /// Checks if the current request has the specified scope.
    /// </summary>
    /// <param name="scope">The required scope.</param>
    /// <returns>True if the scope is present.</returns>
    bool HasScope(string scope);

    /// <summary>
    /// Checks if the current request has any of the specified scopes.
    /// </summary>
    /// <param name="scopes">The scopes to check.</param>
    /// <returns>True if any of the scopes are present.</returns>
    bool HasAnyScope(params string[] scopes);

    /// <summary>
    /// Checks if the current request has all of the specified scopes.
    /// </summary>
    /// <param name="scopes">The scopes to check.</param>
    /// <returns>True if all scopes are present.</returns>
    bool HasAllScopes(params string[] scopes);

    /// <summary>
    /// Ensures the current request has at least one of the specified scopes.
    /// Throws an exception if none of the scopes are present.
    /// </summary>
    /// <param name="scopes">The required scopes (at least one must be present).</param>
    /// <exception cref="Exception">Thrown if none of the scopes are present.</exception>
    void RequireScope(params string[] scopes);

    /// <summary>
    /// Ensures the current request has all of the specified scopes.
    /// Throws an exception if any scope is missing.
    /// </summary>
    /// <param name="scopes">The required scopes (all must be present).</param>
    /// <exception cref="Exception">Thrown if any scope is missing.</exception>
    void RequireAllScopes(params string[] scopes);

    #endregion

    #region Role-based Authorization (RBAC)
    /// <summary>
    /// Checks if the current user has access to a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace ID.</param>
    /// <param name="minimumRole">The minimum role required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authorized.</returns>
    Task<bool> CanAccessWorkspaceAsync(Guid workspaceId, WorkspaceRole minimumRole = WorkspaceRole.Guest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user has access to a project.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="minimumRole">The minimum role required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authorized.</returns>
    Task<bool> CanAccessProjectAsync(Guid projectId, ProjectRole minimumRole = ProjectRole.Viewer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user's role in a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's role, or null if no membership.</returns>
    Task<WorkspaceRole?> GetWorkspaceRoleAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user's role in a project.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's role, or null if no membership.</returns>
    Task<ProjectRole?> GetProjectRoleAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the current user has access to a workspace, throwing if not authorized.
    /// </summary>
    /// <param name="workspaceId">The workspace ID.</param>
    /// <param name="minimumRole">The minimum role required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if not authorized.</exception>
    Task RequireWorkspaceAccessAsync(Guid workspaceId, WorkspaceRole minimumRole = WorkspaceRole.Guest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the current user has access to a project, throwing if not authorized.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="minimumRole">The minimum role required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if not authorized.</exception>
    Task RequireProjectAccessAsync(Guid projectId, ProjectRole minimumRole = ProjectRole.Viewer, CancellationToken cancellationToken = default);

    #endregion
}
