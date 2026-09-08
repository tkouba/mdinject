namespace Mdinject.Core.Configuration;

/// <summary>
/// Loads and validates a style-mapping configuration file.
/// </summary>
public interface IStyleMappingConfigurationLoader
{
    Task<StyleMappingConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default);
}
