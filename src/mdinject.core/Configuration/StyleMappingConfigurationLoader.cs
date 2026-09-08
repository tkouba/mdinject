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

        var blocks = NormalizeBlocks(raw.Blocks);
        var inlines = NormalizeInlines(raw.Inlines);

        return new StyleMappingConfiguration(blocks, inlines);
    }

    private static IReadOnlyDictionary<BlockStyleKey, string> NormalizeBlocks(Dictionary<string, string?> rawBlocks)
    {
        var blocks = new Dictionary<BlockStyleKey, string>();

        foreach (var (name, value) in rawBlocks)
        {
            if (!StyleKeyNames.BlockKeysByName.TryGetValue(name, out var key))
                throw new StyleMappingConfigurationException($"Unknown block style key '{name}'.");

            // A block has no "no formatting" concept - a blank value is treated the same as the key being absent.
            if (!String.IsNullOrEmpty(value))
                blocks[key] = value;
        }

        return blocks;
    }

    private static IReadOnlyDictionary<InlineStyleKey, string?> NormalizeInlines(Dictionary<string, string?> rawInlines)
    {
        var inlines = new Dictionary<InlineStyleKey, string?>();

        foreach (var (name, value) in rawInlines)
        {
            if (!StyleKeyNames.InlineKeysByName.TryGetValue(name, out var key))
                throw new StyleMappingConfigurationException($"Unknown inline style key '{name}'.");

            // A present-but-blank value is kept (null): for "code" it's a deliberate no-formatting
            // opt-out, distinct from the key being absent. StyleResolver interprets the two differently.
            inlines[key] = String.IsNullOrEmpty(value) ? null : value;
        }

        return inlines;
    }
}
