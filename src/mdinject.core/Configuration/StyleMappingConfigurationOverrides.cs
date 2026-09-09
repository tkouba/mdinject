namespace Mdinject.Core.Configuration;

/// <summary>
/// Applies CLI <c>--blocks</c>/<c>--inlines</c> overrides (the same flat key names as YAML, e.g.
/// "heading1", "code") on top of an existing <see cref="StyleMappingConfiguration"/> or
/// <see cref="GeneratedStyleMapping"/>. An override wins over whatever was already there.
/// </summary>
public static class StyleMappingConfigurationOverrides
{
    public static StyleMappingConfiguration Apply(
        StyleMappingConfiguration configuration,
        IDictionary<string, string>? blockOverrides,
        IDictionary<string, string>? inlineOverrides)
    {
        var blocks = new Dictionary<BlockStyleKey, string>(configuration.Blocks);
        var inlines = new Dictionary<InlineStyleKey, string?>(configuration.Inlines);

        if (blockOverrides != null)
        {
            foreach (var (key, value) in RawStyleMappingNormalizer.NormalizeBlocks(ToNullableValues(blockOverrides)))
                blocks[key] = value;
        }

        if (inlineOverrides != null)
        {
            foreach (var (key, value) in RawStyleMappingNormalizer.NormalizeInlines(ToNullableValues(inlineOverrides)))
                inlines[key] = value;
        }

        return new StyleMappingConfiguration(blocks, inlines);
    }

    public static GeneratedStyleMapping Apply(
        GeneratedStyleMapping mapping,
        IDictionary<string, string>? blockOverrides,
        IDictionary<string, string>? inlineOverrides)
    {
        var blocks = new Dictionary<BlockStyleKey, string?>(mapping.Blocks);
        var inlines = new Dictionary<InlineStyleKey, string?>(mapping.Inlines);

        if (blockOverrides != null)
        {
            foreach (var (name, value) in blockOverrides)
                blocks[ResolveBlockKey(name)] = String.IsNullOrEmpty(value) ? null : value;
        }

        if (inlineOverrides != null)
        {
            foreach (var (name, value) in inlineOverrides)
                inlines[ResolveInlineKey(name)] = String.IsNullOrEmpty(value) ? null : value;
        }

        return new GeneratedStyleMapping(blocks, inlines);
    }

    private static BlockStyleKey ResolveBlockKey(string name)
    {
        if (!StyleKeyNames.BlockKeysByName.TryGetValue(name, out var key))
            throw new StyleMappingConfigurationException($"Unknown block style key '{name}'.");

        return key;
    }

    private static InlineStyleKey ResolveInlineKey(string name)
    {
        if (!StyleKeyNames.InlineKeysByName.TryGetValue(name, out var key))
            throw new StyleMappingConfigurationException($"Unknown inline style key '{name}'.");

        return key;
    }

    private static Dictionary<string, string?> ToNullableValues(IDictionary<string, string> source)
    {
        return source.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
    }
}
