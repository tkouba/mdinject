namespace Mdinject.Core.Styles;

/// <summary>
/// Outcome of resolving the horizontal rule block: a named template paragraph style when
/// configured (the template controls its appearance entirely, e.g. its own border), or a direct
/// paragraph border when not - the genuine, intentional default (no canonical style name exists to
/// guess for this construct), not a degraded fallback, so unlike
/// <see cref="BlockquoteStyleResolution.DirectIndent"/> it carries no warning. See
/// <see cref="StyleResolver.ResolveHorizontalRuleStyle"/>.
/// </summary>
public abstract record HorizontalRuleStyleResolution
{
    private HorizontalRuleStyleResolution()
    {
    }

    public sealed record NamedStyle(string StyleId) : HorizontalRuleStyleResolution;

    public sealed record DirectFormatting : HorizontalRuleStyleResolution;
}
