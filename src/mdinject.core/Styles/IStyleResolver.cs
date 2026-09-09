using Mdinject.Core.Configuration;

namespace Mdinject.Core.Styles;

/// <summary>
/// Resolves markdown constructs to template styles, per the "Alias / StyleName / normalized
/// StyleId" resolution order and the built-in defaults used when a construct is unconfigured.
/// </summary>
public interface IStyleResolver
{
    /// <summary>
    /// Resolves the paragraph style id to apply for a block-level construct. Always a named style:
    /// falls back to a built-in default reference (e.g. "Normal", "heading 1") when unconfigured.
    /// </summary>
    /// <exception cref="StyleResolutionException">The configured or default reference matches no style in the template.</exception>
    string ResolveBlockStyle(BlockStyleKey key, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles);

    /// <summary>
    /// Resolves the formatting to apply for an inline construct: a named character style when
    /// configured, otherwise the construct's default (direct formatting for bold/italic, none for code).
    /// </summary>
    /// <exception cref="StyleResolutionException">A configured reference matches no style in the template.</exception>
    InlineStyleResolution ResolveInlineStyle(InlineStyleKey key, StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles);

    /// <summary>
    /// Resolves the blockquote block: a named style (configured, or a guessed canonical "Quote"
    /// match), or a direct-indent fallback when neither is available - see
    /// <see cref="BlockquoteStyleResolution"/>. Unlike <see cref="ResolveBlockStyle"/>, this never
    /// errors for being unconfigured.
    /// </summary>
    /// <exception cref="StyleResolutionException">A configured reference matches no style in the template.</exception>
    BlockquoteStyleResolution ResolveBlockquoteStyle(StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles);

    /// <summary>
    /// Resolves the horizontal rule block: a named style when configured, or a direct paragraph
    /// border when not - see <see cref="HorizontalRuleStyleResolution"/>. Never errors for being
    /// unconfigured; no canonical style name is guessed.
    /// </summary>
    /// <exception cref="StyleResolutionException">A configured reference matches no style in the template.</exception>
    HorizontalRuleStyleResolution ResolveHorizontalRuleStyle(StyleMappingConfiguration configuration, IReadOnlyList<StyleInfo> templateStyles);
}
