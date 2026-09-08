namespace Mdinject.Core.DocumentModel;

/// <summary>
/// Parses Markdown text into the internal document model.
/// </summary>
public interface IMarkdownDocumentParser
{
    /// <exception cref="MarkdownConversionException">The Markdown uses a construct not yet supported.</exception>
    Document Parse(string markdown);
}
