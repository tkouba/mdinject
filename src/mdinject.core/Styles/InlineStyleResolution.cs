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

    public sealed record NoFormatting : InlineStyleResolution;
}
