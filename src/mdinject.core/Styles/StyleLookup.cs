namespace Mdinject.Core.Styles;

/// <summary>
/// Matches a style reference (configured or a canonical-name guess) against a template's styles,
/// and finds a kind's template-level default. Shared by <see cref="StyleResolver"/> (which throws
/// when nothing matches) and <see cref="StyleMappingConfigurationGenerator"/> (which doesn't).
/// </summary>
internal static class StyleLookup
{
    /// <summary>
    /// Resolution order per AGENTS.md: alias, then style name, then normalized style id — all case-insensitive.
    /// </summary>
    public static StyleInfo? FindByReference(string reference, StyleKind kind, IReadOnlyList<StyleInfo> templateStyles)
    {
        var candidates = templateStyles.Where(s => s.Kind == kind).ToList();

        var byAlias = candidates.FirstOrDefault(s => s.Aliases.Any(alias => String.Equals(alias, reference, StringComparison.OrdinalIgnoreCase)));
        if (byAlias != null)
            return byAlias;

        var byName = candidates.FirstOrDefault(s => String.Equals(s.Name, reference, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return byName;

        var normalizedReference = NormalizeId(reference);
        return candidates.FirstOrDefault(s => NormalizeId(s.Id) == normalizedReference);
    }

    /// <summary>
    /// Finds the style the template itself flags as the default for a given kind (e.g. the one
    /// paragraph style with <c>w:default="1"</c>) — the same style Word falls back to when a
    /// paragraph/table/etc. carries no explicit style reference at all.
    /// </summary>
    public static StyleInfo? FindDefault(StyleKind kind, IReadOnlyList<StyleInfo> templateStyles)
    {
        return templateStyles.FirstOrDefault(s => s.Kind == kind && s.IsDefault);
    }

    private static string NormalizeId(string value)
    {
        var normalized = new System.Text.StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (c > 127 || Char.IsWhiteSpace(c))
                continue;

            normalized.Append(Char.ToUpperInvariant(c));
        }

        return normalized.ToString();
    }
}
