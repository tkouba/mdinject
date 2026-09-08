namespace Mdinject.Core.Configuration;

/// <summary>
/// The canonical YAML key names for each <see cref="BlockStyleKey"/>/<see cref="InlineStyleKey"/>,
/// per AGENTS.md's configuration example. Shared by <see cref="StyleMappingConfigurationLoader"/>
/// (name → key) and <see cref="StyleMappingConfigurationWriter"/> (key → name).
/// </summary>
internal static class StyleKeyNames
{
    public static readonly IReadOnlyDictionary<string, BlockStyleKey> BlockKeysByName =
        new Dictionary<string, BlockStyleKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["heading1"] = BlockStyleKey.Heading1,
            ["heading2"] = BlockStyleKey.Heading2,
            ["heading3"] = BlockStyleKey.Heading3,
            ["heading4"] = BlockStyleKey.Heading4,
            ["heading5"] = BlockStyleKey.Heading5,
            ["heading6"] = BlockStyleKey.Heading6,
            ["heading7"] = BlockStyleKey.Heading7,
            ["heading8"] = BlockStyleKey.Heading8,
            ["heading9"] = BlockStyleKey.Heading9,
            ["paragraph"] = BlockStyleKey.Paragraph,
            ["bulletList"] = BlockStyleKey.BulletList,
            ["table"] = BlockStyleKey.Table,
            ["codeBlock"] = BlockStyleKey.CodeBlock,
        };

    public static readonly IReadOnlyDictionary<string, InlineStyleKey> InlineKeysByName =
        new Dictionary<string, InlineStyleKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["bold"] = InlineStyleKey.Bold,
            ["italic"] = InlineStyleKey.Italic,
            ["code"] = InlineStyleKey.Code,
        };

    public static readonly IReadOnlyDictionary<BlockStyleKey, string> BlockNamesByKey =
        BlockKeysByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static readonly IReadOnlyDictionary<InlineStyleKey, string> InlineNamesByKey =
        InlineKeysByName.ToDictionary(pair => pair.Value, pair => pair.Key);
}
