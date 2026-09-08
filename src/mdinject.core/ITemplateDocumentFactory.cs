namespace Mdinject.Core;

/// <summary>
/// Opens a template document from a file path. Implemented per document format (e.g. docx).
/// </summary>
public interface ITemplateDocumentFactory
{
    ITemplateDocument Open(string templatePath);
}
