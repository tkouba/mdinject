namespace Mdinject.Core.Configuration;

/// <summary>
/// Serializes a generated style-mapping starter file to YAML on disk.
/// </summary>
public interface IStyleMappingConfigurationWriter
{
    Task WriteAsync(string path, GeneratedStyleMapping mapping, CancellationToken cancellationToken = default);
}
