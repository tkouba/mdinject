namespace Mdinject.Core.Styles;

/// <summary>
/// Thrown when a style reference (configured or default) cannot be found in the template document.
/// </summary>
public sealed class StyleResolutionException : Exception
{
    public StyleResolutionException(string message)
        : base(message)
    {
    }
}
