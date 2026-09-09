using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;

namespace Mdinject.Core.Styles;

/// <summary>
/// Default <see cref="IStyleResolver"/> implementation. See <see cref="BlockStyleKindRegistry"/> for
/// which block constructs have a genuine template default versus a canonical-name guess versus none.
/// </summary>
public sealed class StyleResolver : IStyleResolver
{
    public string ResolveBlockStyle(BlockStyleKey key, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var kind = BlockStyleKindRegistry.Kinds[key];

        if (configuration.Blocks.TryGetValue(key, out var configuredReference))
        {
            var configuredStyle = StyleLookup.FindByReference(configuredReference, kind, templateStyles);
            if (configuredStyle == null)
                throw new StyleResolutionException($"Could not resolve configured style '{configuredReference}' for block '{key}' in the template.");

            return configuredStyle.Id;
        }

        if (BlockStyleKindRegistry.AutoDefaultKeys.Contains(key))
        {
            var defaultStyle = StyleLookup.FindDefault(kind, templateStyles);
            if (defaultStyle == null)
                throw new StyleResolutionException($"Template has no default {kind} style, required for block '{key}'.");

            return defaultStyle.Id;
        }

        if (BlockStyleKindRegistry.DefaultNames.TryGetValue(key, out var nameGuess))
        {
            var guessedStyle = StyleLookup.FindByReference(nameGuess, kind, templateStyles);
            if (guessedStyle != null)
                return guessedStyle.Id;
        }

        throw new StyleResolutionException($"Block '{key}' is not configured and has no usable default in the template.");
    }

    public InlineStyleResolution ResolveInlineStyle(InlineStyleKey key, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var isConfigured = configuration.Inlines.TryGetValue(key, out var reference);

        if (isConfigured && reference != null)
        {
            var style = StyleLookup.FindByReference(reference, StyleKind.Character, templateStyles);
            if (style == null)
                throw new StyleResolutionException($"Could not resolve configured style '{reference}' for inline '{key}' in the template.");

            return new InlineStyleResolution.NamedStyle(style.Id);
        }

        // Bold/italic have a genuine Word-native default (direct formatting) regardless of whether
        // the key is present-but-blank or absent entirely.
        if (key == InlineStyleKey.Bold || key == InlineStyleKey.Italic)
            return new InlineStyleResolution.DirectFormatting();

        // Code and Link both fall back to no named style when explicitly configured blank
        // ("code:"/"link:") - a deliberate opt-out.
        if (isConfigured)
            return new InlineStyleResolution.NoFormatting();

        // Unlike Code, a link is functional (clickable) without any named style at all, so a fully
        // absent key isn't an error: guess Word's own canonical character style name ("Hyperlink")
        // and fall back to no styling - never no formatting at all - if the template doesn't define it.
        if (key == InlineStyleKey.Link)
        {
            var guessedStyle = StyleLookup.FindByReference("Hyperlink", StyleKind.Character, templateStyles);
            if (guessedStyle != null)
                return new InlineStyleResolution.NamedStyle(guessedStyle.Id);

            return new InlineStyleResolution.NoFormatting(
                "Template has no 'Hyperlink' style; links will be inserted without a named style. " +
                "Configure 'link' explicitly to silence this warning.");
        }

        throw new StyleResolutionException($"Inline '{key}' is not configured and has no usable default.");
    }

    public BlockquoteStyleResolution ResolveBlockquoteStyle(StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        if (configuration.Blocks.TryGetValue(BlockStyleKey.Blockquote, out var configuredReference))
        {
            var configuredStyle = StyleLookup.FindByReference(configuredReference, StyleKind.Paragraph, templateStyles);
            if (configuredStyle == null)
                throw new StyleResolutionException($"Could not resolve configured style '{configuredReference}' for block 'Blockquote' in the template.");

            return new BlockquoteStyleResolution.NamedStyle(configuredStyle.Id);
        }

        // Unlike CodeBlock, a blockquote is renderable without any named style at all (as an
        // indented paragraph), so a fully absent key isn't an error: guess Word's own canonical
        // paragraph style name ("Quote") and fall back to direct indentation - never a throw - if
        // the template doesn't define it.
        var guessedStyle = StyleLookup.FindByReference(BlockStyleKindRegistry.DefaultNames[BlockStyleKey.Blockquote], StyleKind.Paragraph, templateStyles);
        if (guessedStyle != null)
            return new BlockquoteStyleResolution.NamedStyle(guessedStyle.Id);

        return new BlockquoteStyleResolution.DirectIndent(
            "Template has no 'Quote' style; blockquotes will be indented directly instead. " +
            "Configure 'blockquote' explicitly to silence this warning.");
    }

    public HorizontalRuleStyleResolution ResolveHorizontalRuleStyle(StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        if (configuration.Blocks.TryGetValue(BlockStyleKey.HorizontalRule, out var configuredReference))
        {
            var configuredStyle = StyleLookup.FindByReference(configuredReference, StyleKind.Paragraph, templateStyles);
            if (configuredStyle == null)
                throw new StyleResolutionException($"Could not resolve configured style '{configuredReference}' for block 'HorizontalRule' in the template.");

            return new HorizontalRuleStyleResolution.NamedStyle(configuredStyle.Id);
        }

        // Unlike Blockquote/Link, there's no canonical Word style name to guess for a horizontal
        // rule - direct paragraph border formatting is the genuine intentional default, not a
        // degraded fallback, so this never warns.
        return new HorizontalRuleStyleResolution.DirectFormatting();
    }

    public BlockquoteStyleResolution ResolveAlertStyle(AlertKind kind, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles)
    {
        var key = AlertBlockStyleKey(kind);

        if (configuration.Blocks.TryGetValue(key, out var configuredReference))
        {
            var configuredStyle = StyleLookup.FindByReference(configuredReference, StyleKind.Paragraph, templateStyles);
            if (configuredStyle == null)
                throw new StyleResolutionException($"Could not resolve configured style '{configuredReference}' for block '{key}' in the template.");

            return new BlockquoteStyleResolution.NamedStyle(configuredStyle.Id);
        }

        // No style configured for this specific alert kind: fall back to whatever a plain
        // blockquote resolves to - but always warn, even when that fallback is a real named style
        // (the "Quote" guess), since the alert's own visual distinction (icon/color per kind) is
        // lost either way. A plain (non-alert) blockquote resolving that same style never warns.
        var keyName = StyleKeyNames.BlockNamesByKey[key];
        var marker = $"[!{kind.ToString().ToUpperInvariant()}]";

        return ResolveBlockquoteStyle(configuration, templateStyles) switch
        {
            BlockquoteStyleResolution.NamedStyle namedStyle => new BlockquoteStyleResolution.NamedStyle(
                namedStyle.StyleId,
                $"No style configured for the '{marker}' alert; it will use the template's 'Quote' style instead. " +
                $"Configure '{keyName}' explicitly to silence this warning."),

            BlockquoteStyleResolution.DirectIndent => new BlockquoteStyleResolution.DirectIndent(
                $"No style configured for the '{marker}' alert, and the template has no 'Quote' style either; " +
                $"it will be indented directly. Configure '{keyName}' (or 'blockquote') explicitly to silence this warning."),

            var other => throw new InvalidOperationException($"Unexpected blockquote style resolution '{other.GetType().Name}'."),
        };
    }

    private static BlockStyleKey AlertBlockStyleKey(AlertKind kind)
    {
        return kind switch
        {
            AlertKind.Note => BlockStyleKey.Note,
            AlertKind.Tip => BlockStyleKey.Tip,
            AlertKind.Important => BlockStyleKey.Important,
            AlertKind.Warning => BlockStyleKey.Warning,
            AlertKind.Caution => BlockStyleKey.Caution,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown alert kind."),
        };
    }
}
