using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mdinject.Core.Configuration;

/// <summary>
/// Reads a style-mapping YAML file and normalizes it into a <see cref="StyleMappingConfiguration"/>,
/// resolving each key from the flat set of names shown in AGENTS.md (e.g. "heading1", "bold") to the
/// matching <see cref="BlockStyleKey"/> or <see cref="InlineStyleKey"/>.
/// </summary>
public sealed class StyleMappingConfigurationLoader : IStyleMappingConfigurationLoader
{
    private readonly IDeserializer deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task<StyleMappingConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var yaml = await File.ReadAllTextAsync(path, cancellationToken);

        RawStyleMappingConfiguration raw;
        try
        {
            raw = deserializer.Deserialize<RawStyleMappingConfiguration>(yaml) ?? new RawStyleMappingConfiguration();
        }
        catch (YamlException ex)
        {
            throw new StyleMappingConfigurationException($"Invalid YAML in style mapping configuration '{path}': {ex.Message}", ex);
        }

        var blocks = RawStyleMappingNormalizer.NormalizeBlocks(raw.Blocks);
        var inlines = RawStyleMappingNormalizer.NormalizeInlines(raw.Inlines);

        return new StyleMappingConfiguration(blocks, inlines);
    }
}
