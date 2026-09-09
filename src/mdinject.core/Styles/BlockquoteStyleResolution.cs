namespace Mdinject.Core.Styles;

/// <summary>
/// Outcome of resolving the blockquote block: a named template paragraph style (configured, or a
/// guessed canonical "Quote" match), or a direct-indent fallback (with a warning to surface to the
/// user) when neither is available. Unlike other blocks, an unresolvable blockquote never errors -
/// see <see cref="StyleResolver.ResolveBlockquoteStyle"/>.
/// </summary>
public abstract record BlockquoteStyleResolution
{
    private BlockquoteStyleResolution()
    {
    }

    public sealed record NamedStyle(string StyleId) : BlockquoteStyleResolution;

    public sealed record DirectIndent(string Warning) : BlockquoteStyleResolution;
}
