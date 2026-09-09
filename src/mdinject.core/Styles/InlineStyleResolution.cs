namespace Mdinject.Core.Styles;

/// <summary>
/// Outcome of resolving an inline markdown construct: a named template character style, direct
/// character formatting applied without any named style (the bold/italic default), or no
/// formatting at all (the code default).
/// </summary>
public abstract record InlineStyleResolution
{
    private InlineStyleResolution()
    {
    }

    public sealed record NamedStyle(string StyleId) : InlineStyleResolution;

    public sealed record DirectFormatting : InlineStyleResolution;

    /// <summary>
    /// <paramref name="Warning"/> is set when this is an automatic fallback the user didn't ask for
    /// (e.g. an unconfigured link with no canonical style in the template) - callers should surface
    /// it to the user. It's null for a deliberate opt-out (an explicit blank configuration value).
    /// </summary>
    public sealed record NoFormatting(string? Warning = null) : InlineStyleResolution;
}
