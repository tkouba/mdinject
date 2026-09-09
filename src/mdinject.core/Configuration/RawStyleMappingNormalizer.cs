namespace Mdinject.Core.Configuration;

/// <summary>
/// Resolves a flat "key name" → value map (from YAML or CLI --blocks/--inlines options) into typed
/// <see cref="StyleMappingConfiguration"/> dictionaries, per the key names in <see cref="StyleKeyNames"/>
/// and the blank-value rules documented on <see cref="StyleMappingConfiguration"/>. Shared by
/// <see cref="StyleMappingConfigurationLoader"/> and <see cref="StyleMappingConfigurationOverrides"/>
/// so both interpret a blank value identically.
/// </summary>
internal static class RawStyleMappingNormalizer
{
    public static IReadOnlyDictionary<BlockStyleKey, string> NormalizeBlocks(IReadOnlyDictionary<string, string?> rawBlocks)
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

    public static IReadOnlyDictionary<InlineStyleKey, string?> NormalizeInlines(IReadOnlyDictionary<string, string?> rawInlines)
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
