using Mdinject.Core;

namespace Mdinject.Docx;

public sealed class DocxTemplateDocumentFactory : ITemplateDocumentFactory
{
    public ITemplateDocument Open(string templatePath)
    {
        return new DocxTemplateDocument(templatePath);
    }
}
