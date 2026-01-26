using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octopus.Server.Abstractions.Processing;
using Octopus.Server.Abstractions.Storage;
using Octopus.Server.App.Storage;
using Octopus.Server.Domain.Entities;
using Octopus.Server.Domain.Enums;
using Octopus.Server.Persistence.EfCore;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace Octopus.Server.App.Processing;

/// <summary>
/// Job handler for extracting properties from IFC files.
/// Reads IFC from storage, extracts all element properties, stores as JSON artifact,
/// and updates ModelVersion with the properties file reference.
/// </summary>
public class ExtractPropertiesJobHandler : IJobHandler<ExtractPropertiesJobPayload>
{
    public const string JobTypeName = "ExtractProperties";

    private readonly OctopusDbContext _dbContext;
    private readonly IStorageProvider _storageProvider;
    private readonly IProgressNotifier _progressNotifier;
    private readonly ILogger<ExtractPropertiesJobHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string JobType => JobTypeName;

    public ExtractPropertiesJobHandler(
        OctopusDbContext dbContext,
        IStorageProvider storageProvider,
        IProgressNotifier progressNotifier,
        ILogger<ExtractPropertiesJobHandler> logger)
    {
        _dbContext = dbContext;
        _storageProvider = storageProvider;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(string jobId, ExtractPropertiesJobPayload payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting properties extraction job {JobId} for ModelVersion {ModelVersionId}",
            jobId, payload.ModelVersionId);

        // Load the model version with related entities
        var modelVersion = await _dbContext.ModelVersions
            .Include(mv => mv.Model)
            .ThenInclude(m => m!.Project)
            .Include(mv => mv.IfcFile)
            .FirstOrDefaultAsync(mv => mv.Id == payload.ModelVersionId, cancellationToken);

        if (modelVersion is null)
        {
            _logger.LogError("ModelVersion {ModelVersionId} not found", payload.ModelVersionId);
            await NotifyFailureAsync(jobId, payload.ModelVersionId, "ModelVersion not found", cancellationToken);
            return;
        }

        // Idempotency check: if already has properties file, skip
        if (modelVersion.PropertiesFileId.HasValue)
        {
            _logger.LogInformation("ModelVersion {ModelVersionId} already has properties file (idempotency), skipping",
                payload.ModelVersionId);
            await NotifySuccessAsync(jobId, payload.ModelVersionId, cancellationToken);
            return;
        }

        // Validate prerequisites
        var project = modelVersion.Model?.Project;
        if (project is null)
        {
            _logger.LogError("Project not found for ModelVersion {ModelVersionId}", payload.ModelVersionId);
            await SetFailedStatusAsync(modelVersion, "Project not found", cancellationToken);
            await NotifyFailureAsync(jobId, payload.ModelVersionId, "Project not found", cancellationToken);
            return;
        }

        var ifcFile = modelVersion.IfcFile;
        if (ifcFile is null || string.IsNullOrEmpty(ifcFile.StorageKey))
        {
            _logger.LogError("IFC file not found or has no storage key for ModelVersion {ModelVersionId}",
                payload.ModelVersionId);
            await SetFailedStatusAsync(modelVersion, "IFC file not found", cancellationToken);
            await NotifyFailureAsync(jobId, payload.ModelVersionId, "IFC file not found", cancellationToken);
            return;
        }

        try
        {
            await NotifyProgressAsync(jobId, payload.ModelVersionId, "Starting", 0, "Starting properties extraction...", cancellationToken);

            // Download IFC file from storage
            await NotifyProgressAsync(jobId, payload.ModelVersionId, "Downloading", 10, "Downloading IFC file...", cancellationToken);

            using var ifcStream = await _storageProvider.OpenReadAsync(ifcFile.StorageKey, cancellationToken);
            if (ifcStream is null)
            {
                throw new InvalidOperationException($"Failed to open IFC file from storage: {ifcFile.StorageKey}");
            }

            // Write to temp file (xBIM requires file path for large models)
            var tempPath = Path.Combine(Path.GetTempPath(), $"xbim_{Guid.NewGuid()}.ifc");
            try
            {
                await using (var tempFile = File.Create(tempPath))
                {
                    await ifcStream.CopyToAsync(tempFile, cancellationToken);
                }

                await NotifyProgressAsync(jobId, payload.ModelVersionId, "Opening", 20, "Opening IFC model...", cancellationToken);

                // Configure xBIM services
                IfcStore.ModelProviderFactory.UseHeuristicModelProvider();

                // Open IFC file
                using var model = IfcStore.Open(tempPath);
                if (model is null)
                {
                    throw new InvalidOperationException("Failed to open IFC model");
                }

                await NotifyProgressAsync(jobId, payload.ModelVersionId, "Extracting", 40, "Extracting properties...", cancellationToken);

                // Extract properties from all products
                var propertiesData = await ExtractPropertiesAsync(model, jobId, payload.ModelVersionId, cancellationToken);

                if (propertiesData is null)
                {
                    throw new InvalidOperationException("Failed to extract properties from IFC model");
                }

                await NotifyProgressAsync(jobId, payload.ModelVersionId, "Storing", 80, "Storing properties artifact...", cancellationToken);

                // Store properties artifact
                var propertiesStorageKey = StorageKeyHelper.GenerateArtifactKey(
                    project.WorkspaceId,
                    project.Id,
                    "properties",
                    ".json");

                using var propertiesStream = new MemoryStream(propertiesData);
                await _storageProvider.PutAsync(propertiesStorageKey, propertiesStream, "application/json", cancellationToken);

                await NotifyProgressAsync(jobId, payload.ModelVersionId, "Finalizing", 90, "Creating artifact record...", cancellationToken);

                // Create properties file record
                var propertiesFile = new FileEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = $"{Path.GetFileNameWithoutExtension(ifcFile.Name)}.properties.json",
                    ContentType = "application/json",
                    SizeBytes = propertiesData.Length,
                    Kind = FileKind.Artifact,
                    Category = FileCategory.Properties,
                    StorageProvider = _storageProvider.ProviderId,
                    StorageKey = propertiesStorageKey,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.Files.Add(propertiesFile);

                // Create lineage link (Properties derived from IFC)
                var fileLink = new FileLink
                {
                    Id = Guid.NewGuid(),
                    SourceFileId = ifcFile.Id,
                    TargetFileId = propertiesFile.Id,
                    LinkType = FileLinkType.PropertiesOf,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.FileLinks.Add(fileLink);

                // Update ModelVersion with artifact reference
                modelVersion.PropertiesFileId = propertiesFile.Id;

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully extracted properties for ModelVersion {ModelVersionId}. Properties file size: {Size} bytes",
                    payload.ModelVersionId, propertiesData.Length);

                await NotifySuccessAsync(jobId, payload.ModelVersionId, cancellationToken);
            }
            finally
            {
                // Cleanup temp file
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting properties for ModelVersion {ModelVersionId}",
                payload.ModelVersionId);

            await SetFailedStatusAsync(modelVersion, ex.Message, cancellationToken);
            await NotifyFailureAsync(jobId, payload.ModelVersionId, ex.Message, cancellationToken);
        }
    }

    private async Task<byte[]?> ExtractPropertiesAsync(
        IModel model,
        string jobId,
        Guid modelVersionId,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var elements = new List<ExtractedElement>();
                var products = model.Instances.OfType<IIfcProduct>().ToList();
                var totalProducts = products.Count;
                var processedCount = 0;

                foreach (var product in products)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return null;

                    var element = ExtractElementProperties(product);
                    if (element != null)
                    {
                        elements.Add(element);
                    }

                    processedCount++;

                    // Report progress every 100 items or at certain percentages
                    if (processedCount % 100 == 0 || processedCount == totalProducts)
                    {
                        var percentComplete = 40 + (int)((processedCount / (double)totalProducts) * 30); // 40-70%
                        await NotifyProgressAsync(jobId, modelVersionId, "Extracting",
                            percentComplete, $"Extracted {processedCount}/{totalProducts} elements...", CancellationToken.None);
                    }
                }

                await NotifyProgressAsync(jobId, modelVersionId, "Serializing", 75, "Serializing properties...", CancellationToken.None);

                // Create the output structure
                var output = new ExtractedPropertiesDocument
                {
                    SchemaVersion = "1.0",
                    ExtractedAt = DateTimeOffset.UtcNow,
                    TotalElements = elements.Count,
                    Elements = elements
                };

                // Serialize to JSON
                var json = JsonSerializer.SerializeToUtf8Bytes(output, JsonOptions);
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting properties from IFC model");
                return null;
            }
        }, cancellationToken);
    }

    private ExtractedElement? ExtractElementProperties(IIfcProduct product)
    {
        try
        {
            var element = new ExtractedElement
            {
                EntityLabel = product.EntityLabel,
                GlobalId = product.GlobalId.ToString(),
                Name = GetLabelValue(product.Name),
                TypeName = product.ExpressType.Name,
                Description = GetTextValue(product.Description),
                ObjectType = GetLabelValue(product.ObjectType)
            };

            // Extract property sets
            var relDefines = product.IsDefinedBy.OfType<IIfcRelDefinesByProperties>().ToList();
            foreach (var rel in relDefines)
            {
                if (rel.RelatingPropertyDefinition is IIfcPropertySet pset)
                {
                    var propertySet = ExtractPropertySet(pset);
                    if (propertySet != null && propertySet.Properties.Count > 0)
                    {
                        element.PropertySets.Add(propertySet);
                    }
                }
                else if (rel.RelatingPropertyDefinition is IIfcElementQuantity qset)
                {
                    var quantitySet = ExtractQuantitySet(qset);
                    if (quantitySet != null && quantitySet.Quantities.Count > 0)
                    {
                        element.QuantitySets.Add(quantitySet);
                    }
                }
            }

            // Extract type properties
            var relType = product.IsTypedBy?.FirstOrDefault();
            if (relType?.RelatingType is IIfcTypeObject typeObject)
            {
                element.TypeObjectName = GetLabelValue(typeObject.Name);
                element.TypeObjectType = typeObject.ExpressType.Name;

                if (typeObject.HasPropertySets != null)
                {
                    foreach (var pset in typeObject.HasPropertySets.OfType<IIfcPropertySet>())
                    {
                        var propertySet = ExtractPropertySet(pset, isTypeProperty: true);
                        if (propertySet != null && propertySet.Properties.Count > 0)
                        {
                            element.TypePropertySets.Add(propertySet);
                        }
                    }
                }
            }

            return element;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting properties for entity {EntityLabel}", product.EntityLabel);
            return null;
        }
    }

    private ExtractedPropertySet? ExtractPropertySet(IIfcPropertySet pset, bool isTypeProperty = false)
    {
        try
        {
            var propertySet = new ExtractedPropertySet
            {
                Name = GetLabelValue(pset.Name) ?? "Unnamed",
                GlobalId = pset.GlobalId.ToString(),
                IsTypeProperty = isTypeProperty
            };

            foreach (var prop in pset.HasProperties)
            {
                var extractedProp = ExtractProperty(prop);
                if (extractedProp != null)
                {
                    propertySet.Properties.Add(extractedProp);
                }
            }

            return propertySet;
        }
        catch
        {
            return null;
        }
    }

    private ExtractedQuantitySet? ExtractQuantitySet(IIfcElementQuantity qset)
    {
        try
        {
            var quantitySet = new ExtractedQuantitySet
            {
                Name = GetLabelValue(qset.Name) ?? "Unnamed",
                GlobalId = qset.GlobalId.ToString()
            };

            foreach (var quantity in qset.Quantities)
            {
                var extractedQty = ExtractQuantity(quantity);
                if (extractedQty != null)
                {
                    quantitySet.Quantities.Add(extractedQty);
                }
            }

            return quantitySet;
        }
        catch
        {
            return null;
        }
    }

    private ExtractedProperty? ExtractProperty(IIfcProperty property)
    {
        try
        {
            var name = GetIdentifierValue(property.Name) ?? "Unknown";

            return property switch
            {
                IIfcPropertySingleValue singleValue => new ExtractedProperty
                {
                    Name = name,
                    Value = singleValue.NominalValue?.ToString(),
                    ValueType = GetValueType(singleValue.NominalValue),
                    Unit = singleValue.Unit?.ToString()
                },
                IIfcPropertyEnumeratedValue enumValue => new ExtractedProperty
                {
                    Name = name,
                    Value = enumValue.EnumerationValues != null
                        ? string.Join(", ", enumValue.EnumerationValues.Select(v => v.ToString()))
                        : null,
                    ValueType = "enumeration"
                },
                IIfcPropertyBoundedValue boundedValue => new ExtractedProperty
                {
                    Name = name,
                    Value = $"{boundedValue.LowerBoundValue?.ToString() ?? "?"} - {boundedValue.UpperBoundValue?.ToString() ?? "?"}",
                    ValueType = "range",
                    Unit = boundedValue.Unit?.ToString()
                },
                IIfcPropertyListValue listValue => new ExtractedProperty
                {
                    Name = name,
                    Value = listValue.ListValues != null
                        ? string.Join(", ", listValue.ListValues.Select(v => v.ToString()))
                        : null,
                    ValueType = "list"
                },
                IIfcPropertyTableValue => new ExtractedProperty
                {
                    Name = name,
                    Value = "[Table]",
                    ValueType = "table"
                },
                IIfcComplexProperty complexProp => new ExtractedProperty
                {
                    Name = name,
                    Value = $"[{complexProp.HasProperties.Count()} properties]",
                    ValueType = "complex"
                },
                _ => new ExtractedProperty
                {
                    Name = name,
                    Value = property.ToString(),
                    ValueType = "unknown"
                }
            };
        }
        catch
        {
            return null;
        }
    }

    private ExtractedQuantity? ExtractQuantity(IIfcPhysicalQuantity quantity)
    {
        try
        {
            var name = GetLabelValue(quantity.Name) ?? "Unknown";

            return quantity switch
            {
                IIfcQuantityLength length => new ExtractedQuantity
                {
                    Name = name,
                    Value = length.LengthValue,
                    ValueType = "length",
                    Unit = length.Unit?.ToString() ?? "m"
                },
                IIfcQuantityArea area => new ExtractedQuantity
                {
                    Name = name,
                    Value = area.AreaValue,
                    ValueType = "area",
                    Unit = area.Unit?.ToString() ?? "m2"
                },
                IIfcQuantityVolume volume => new ExtractedQuantity
                {
                    Name = name,
                    Value = volume.VolumeValue,
                    ValueType = "volume",
                    Unit = volume.Unit?.ToString() ?? "m3"
                },
                IIfcQuantityCount count => new ExtractedQuantity
                {
                    Name = name,
                    Value = count.CountValue,
                    ValueType = "count",
                    Unit = null
                },
                IIfcQuantityWeight weight => new ExtractedQuantity
                {
                    Name = name,
                    Value = weight.WeightValue,
                    ValueType = "weight",
                    Unit = weight.Unit?.ToString() ?? "kg"
                },
                IIfcQuantityTime time => new ExtractedQuantity
                {
                    Name = name,
                    Value = time.TimeValue,
                    ValueType = "time",
                    Unit = time.Unit?.ToString() ?? "s"
                },
                _ => new ExtractedQuantity
                {
                    Name = name,
                    Value = null,
                    ValueType = "unknown",
                    Unit = null
                }
            };
        }
        catch
        {
            return null;
        }
    }

    private string GetValueType(IIfcValue? value)
    {
        if (value == null) return "null";

        var underlyingValue = value.Value;
        return underlyingValue switch
        {
            bool => "boolean",
            int or long => "integer",
            double or float => "double",
            string => "string",
            _ => "string"
        };
    }

    private string? GetLabelValue(object? label)
    {
        if (label == null) return null;
        var str = label.ToString();
        return string.IsNullOrEmpty(str) ? null : str;
    }

    private string? GetTextValue(object? text)
    {
        if (text == null) return null;
        var str = text.ToString();
        return string.IsNullOrEmpty(str) ? null : str;
    }

    private string? GetIdentifierValue(object? identifier)
    {
        if (identifier == null) return null;
        var str = identifier.ToString();
        return string.IsNullOrEmpty(str) ? null : str;
    }

    private async Task SetFailedStatusAsync(ModelVersion modelVersion, string errorMessage, CancellationToken cancellationToken)
    {
        // Only set failed if ModelVersion doesn't already have properties
        // Don't change the main processing status as that's for WexBIM conversion
        modelVersion.ErrorMessage = $"Properties extraction failed: {errorMessage}";

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save error state for ModelVersion {ModelVersionId}", modelVersion.Id);
        }
    }

    private async Task NotifyProgressAsync(string jobId, Guid modelVersionId, string stage, int percentComplete, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _progressNotifier.NotifyProgressAsync(new ProcessingProgress
            {
                JobId = jobId,
                ModelVersionId = modelVersionId,
                Stage = stage,
                PercentComplete = percentComplete,
                Message = message,
                IsComplete = false,
                IsSuccess = false
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send progress notification");
        }
    }

    private async Task NotifySuccessAsync(string jobId, Guid modelVersionId, CancellationToken cancellationToken)
    {
        try
        {
            await _progressNotifier.NotifyProgressAsync(new ProcessingProgress
            {
                JobId = jobId,
                ModelVersionId = modelVersionId,
                Stage = "Complete",
                PercentComplete = 100,
                Message = "Properties extraction completed successfully",
                IsComplete = true,
                IsSuccess = true
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send success notification");
        }
    }

    private async Task NotifyFailureAsync(string jobId, Guid modelVersionId, string errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            await _progressNotifier.NotifyProgressAsync(new ProcessingProgress
            {
                JobId = jobId,
                ModelVersionId = modelVersionId,
                Stage = "Failed",
                PercentComplete = 0,
                Message = "Properties extraction failed",
                IsComplete = true,
                IsSuccess = false,
                ErrorMessage = errorMessage
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send failure notification");
        }
    }
}

#region Extracted Properties Data Models

/// <summary>
/// Root document containing all extracted properties.
/// </summary>
public class ExtractedPropertiesDocument
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset ExtractedAt { get; set; }
    public int TotalElements { get; set; }
    public List<ExtractedElement> Elements { get; set; } = new();
}

/// <summary>
/// Properties for a single IFC element.
/// </summary>
public class ExtractedElement
{
    public int EntityLabel { get; set; }
    public string? GlobalId { get; set; }
    public string? Name { get; set; }
    public string? TypeName { get; set; }
    public string? Description { get; set; }
    public string? ObjectType { get; set; }
    public string? TypeObjectName { get; set; }
    public string? TypeObjectType { get; set; }
    public List<ExtractedPropertySet> PropertySets { get; set; } = new();
    public List<ExtractedQuantitySet> QuantitySets { get; set; } = new();
    public List<ExtractedPropertySet> TypePropertySets { get; set; } = new();
}

/// <summary>
/// A property set with its properties.
/// </summary>
public class ExtractedPropertySet
{
    public string Name { get; set; } = string.Empty;
    public string? GlobalId { get; set; }
    public bool IsTypeProperty { get; set; }
    public List<ExtractedProperty> Properties { get; set; } = new();
}

/// <summary>
/// A quantity set with its quantities.
/// </summary>
public class ExtractedQuantitySet
{
    public string Name { get; set; } = string.Empty;
    public string? GlobalId { get; set; }
    public List<ExtractedQuantity> Quantities { get; set; } = new();
}

/// <summary>
/// A single property value.
/// </summary>
public class ExtractedProperty
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string";
    public string? Unit { get; set; }
}

/// <summary>
/// A single quantity value.
/// </summary>
public class ExtractedQuantity
{
    public string Name { get; set; } = string.Empty;
    public double? Value { get; set; }
    public string ValueType { get; set; } = "unknown";
    public string? Unit { get; set; }
}

#endregion
