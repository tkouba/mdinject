using Mdinject.Core.Styles;

namespace Mdinject.Core.Configuration;

/// <summary>
/// Default <see cref="IStyleMappingConfigurationGenerator"/> implementation. Reuses the exact same
/// default-resolution rules as <see cref="StyleResolver"/> (see <see cref="BlockStyleKindRegistry"/>),
/// but never throws: an unresolvable construct is simply left blank in the generated file.
/// </summary>
public sealed class StyleMappingConfigurationGenerator : IStyleMappingConfigurationGenerator
{
    public GeneratedStyleMapping Generate(IReadOnlyList<StyleInfo> templateStyles)
    {
        var blocks = new Dictionary<BlockStyleKey, string?>();

        foreach (var key in Enum.GetValues<BlockStyleKey>())
        {
            var style = ResolveBlockGuess(key, templateStyles);
            blocks[key] = style != null ? SelectDisplayReference(style) : null;
        }

        // Bold/italic already default to Word's own direct formatting - nothing useful to suggest.
        // Code has no default at all. Link guesses the canonical "Hyperlink" character style, same
        // as headings guess their canonical paragraph style names.
        var linkGuess = StyleLookup.FindByReference("Hyperlink", StyleKind.Character, templateStyles);

        var inlines = new Dictionary<InlineStyleKey, string?>
        {
            [InlineStyleKey.Bold] = null,
            [InlineStyleKey.Italic] = null,
            [InlineStyleKey.Code] = null,
            [InlineStyleKey.Link] = linkGuess != null ? SelectDisplayReference(linkGuess) : null,
        };

        return new GeneratedStyleMapping(blocks, inlines);
    }

    private static StyleInfo? ResolveBlockGuess(BlockStyleKey key, IReadOnlyList<StyleInfo> templateStyles)
    {
        var kind = BlockStyleKindRegistry.Kinds[key];

        if (BlockStyleKindRegistry.AutoDefaultKeys.Contains(key))
            return StyleLookup.FindDefault(kind, templateStyles);

        if (BlockStyleKindRegistry.DefaultNames.TryGetValue(key, out var nameGuess))
            return StyleLookup.FindByReference(nameGuess, kind, templateStyles);

        // CodeBlock: nothing to guess.
        return null;
    }

    /// <summary>
    /// Picks the most human-readable reference for a resolved style, in the same priority order
    /// <see cref="StyleLookup.FindByReference"/> matches against: alias, then name, then id. The
    /// generated file is meant to be hand-edited, so a corporate template's arbitrary style id
    /// (e.g. "Nadpis1") should never show up when a real alias or name is available.
    /// </summary>
    private static string SelectDisplayReference(StyleInfo style)
    {
        if (style.Aliases.Count > 0)
            return style.Aliases[0];

        // Name already falls back to the style id when the template defines no name at all.
        return style.Name;
    }
}
