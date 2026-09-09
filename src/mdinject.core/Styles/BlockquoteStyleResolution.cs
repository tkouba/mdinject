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

    /// <summary>
    /// <paramref name="Warning"/> is set when this is <see cref="StyleResolver.ResolveAlertStyle"/>
    /// falling back to a plain blockquote's named style because the alert's own kind has no
    /// configured style - the alert's visual distinction is lost even though a real style was
    /// found, so this still warns, unlike a plain (non-alert) blockquote resolving the same style.
    /// </summary>
    public sealed record NamedStyle(string StyleId, string? Warning = null) : BlockquoteStyleResolution;

    public sealed record DirectIndent(string Warning) : BlockquoteStyleResolution;
}
