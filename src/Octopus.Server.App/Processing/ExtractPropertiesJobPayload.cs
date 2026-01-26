namespace Octopus.Server.App.Processing;

/// <summary>
/// Payload for IFC properties extraction job.
/// </summary>
public record ExtractPropertiesJobPayload
{
    /// <summary>
    /// The model version to extract properties from.
    /// </summary>
    public required Guid ModelVersionId { get; init; }
}
