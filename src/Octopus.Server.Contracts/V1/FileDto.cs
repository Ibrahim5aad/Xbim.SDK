namespace Octopus.Server.Contracts.V1;

/// <summary>
/// File kind classification.
/// </summary>
public enum FileKind
{
    Source = 0,
    Artifact = 1
}

/// <summary>
/// File category for filtering.
/// </summary>
public enum FileCategory
{
    Other = 0,
    Ifc = 1,
    WexBim = 2,
    Properties = 3,
    Thumbnail = 4,
    Log = 5
}

/// <summary>
/// Represents a file in the registry.
/// </summary>
public record FileDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string? Checksum { get; init; }
    public FileKind Kind { get; init; }
    public FileCategory Category { get; init; }
    public string StorageProvider { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
}

/// <summary>
/// Link type for file relationships.
/// </summary>
public enum FileLinkType
{
    DerivedFrom = 0,
    ThumbnailOf = 1,
    PropertiesOf = 2,
    LogOf = 3
}

/// <summary>
/// Represents a relationship between files.
/// </summary>
public record FileLinkDto
{
    public Guid Id { get; init; }
    public Guid SourceFileId { get; init; }
    public Guid TargetFileId { get; init; }
    public FileLinkType LinkType { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Upload session status.
/// </summary>
public enum UploadSessionStatus
{
    Reserved = 0,
    Uploading = 1,
    Committed = 2,
    Failed = 3,
    Expired = 4
}

/// <summary>
/// Represents an upload session for chunked/resumable uploads.
/// </summary>
public record UploadSessionDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public long? ExpectedSizeBytes { get; init; }
    public UploadSessionStatus Status { get; init; }
    public string? UploadUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public record ReserveUploadRequest
{
    public string FileName { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public long? ExpectedSizeBytes { get; init; }
}

public record CommitUploadRequest
{
    public string? Checksum { get; init; }
}
