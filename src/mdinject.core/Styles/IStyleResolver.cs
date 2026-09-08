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
}
