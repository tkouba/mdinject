namespace Mdinject.Core.DocumentModel;

/// <summary>
/// Thrown when a Markdown document uses a construct the current document model doesn't support yet
/// (e.g. ordered lists, tables), so the gap is surfaced clearly instead of silently dropping content.
/// </summary>
public sealed class MarkdownConversionException : Exception
{
    public MarkdownConversionException(string message)
        : base(message)
    {
    }
}
