namespace Mdinject.Core.Styles;

/// <summary>
/// Describes a single style defined in a template document.
/// </summary>
/// <param name="Id">Style id, used to reference the style from style-mapping configuration.</param>
/// <param name="Name">Human-readable style name, as shown in Word's style picker.</param>
/// <param name="Kind">Style category (paragraph, character, table, numbering).</param>
/// <param name="IsDefault">Whether this style is the document's default for its kind.</param>
/// <param name="BasedOnId">Style id this style inherits from, if any.</param>
/// <param name="Aliases">Alternate names defined for the style (Word's "aliases" field), used to resolve style-mapping configuration.</param>
/// <param name="IsCustom">Whether the style is user/template-defined rather than one of Word's built-in styles.</param>
public sealed record StyleInfo(
    string Id,
    string Name,
    StyleKind Kind,
    bool IsDefault,
    string? BasedOnId,
    IReadOnlyList<string> Aliases,
    bool IsCustom);
