using Mdinject.Core.Configuration;

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

        // Code has no native default. A present-but-blank value ("code:") is a deliberate opt-out;
        // a fully absent key has no default and errors only once code is actually used.
        if (isConfigured)
            return new InlineStyleResolution.NoFormatting();

        throw new StyleResolutionException($"Inline '{key}' is not configured and has no usable default.");
    }
}
