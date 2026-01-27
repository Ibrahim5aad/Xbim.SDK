namespace Octopus.Server.Domain.Enums;

/// <summary>
/// Types of audit events for OAuth applications.
/// </summary>
public enum OAuthAppAuditEventType
{
    /// <summary>
    /// App was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// App properties were updated.
    /// </summary>
    Updated = 1,

    /// <summary>
    /// App was enabled.
    /// </summary>
    Enabled = 2,

    /// <summary>
    /// App was disabled.
    /// </summary>
    Disabled = 3,

    /// <summary>
    /// App was deleted.
    /// </summary>
    Deleted = 4,

    /// <summary>
    /// Client secret was rotated.
    /// </summary>
    SecretRotated = 5
}
