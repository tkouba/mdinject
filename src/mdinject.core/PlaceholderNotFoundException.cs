namespace Mdinject.Core;

/// <summary>
/// Thrown when the named placeholder paragraph (e.g. a paragraph containing exactly "{{CONTENT}}")
/// cannot be found in the template.
/// </summary>
public sealed class PlaceholderNotFoundException : Exception
{
    public PlaceholderNotFoundException(string message)
        : base(message)
    {
    }
}
