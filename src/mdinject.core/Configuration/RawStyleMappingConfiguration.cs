namespace Mdinject.Core.Configuration;

/// <summary>
/// Direct YAML deserialization target for the style-mapping configuration file. Deliberately
/// dumb: no validation, no enum keys — <see cref="StyleMappingConfigurationLoader"/> normalizes
/// this into a <see cref="StyleMappingConfiguration"/>.
/// </summary>
internal sealed class RawStyleMappingConfiguration
{
    public Dictionary<string, string?> Blocks { get; set; } = new();

    public Dictionary<string, string?> Inlines { get; set; } = new();
}
