using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mdinject.Core;
using Mdinject.Core.Styles;

namespace Mdinject.Docx;

/// <summary>
/// Reads style information from a .docx template using the OpenXML SDK.
/// </summary>
public sealed class DocxTemplateDocument : ITemplateDocument
{
    private readonly WordprocessingDocument document;

    public DocxTemplateDocument(string templatePath)
    {
        document = WordprocessingDocument.Open(templatePath, false);
    }

    public Task<IReadOnlyList<StyleInfo>> GetStylesAsync(CancellationToken cancellationToken = default)
    {
        var stylesPart = document.MainDocumentPart?.StyleDefinitionsPart;
        if (stylesPart == null || stylesPart.Styles == null)
            return Task.FromResult<IReadOnlyList<StyleInfo>>(Array.Empty<StyleInfo>());

        var result = new List<StyleInfo>();

        foreach (var style in stylesPart.Styles.Elements<Style>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (style.StyleId == null || style.StyleId.Value == null)
                continue;

            var name = style.StyleName?.Val?.Value ?? style.StyleId.Value;
            var kind = MapKind(style.Type);
            var isDefault = style.Default != null && style.Default.Value;
            var basedOnId = style.BasedOn?.Val?.Value;
            var aliases = ParseAliases(style.Aliases?.Val?.Value);
            var isCustom = style.CustomStyle != null && style.CustomStyle.Value;

            result.Add(new StyleInfo(style.StyleId.Value, name, kind, isDefault, basedOnId, aliases, isCustom));
        }

        return Task.FromResult<IReadOnlyList<StyleInfo>>(result);
    }

    private static StyleKind MapKind(EnumValue<StyleValues>? type)
    {
        if (type == null)
            return StyleKind.Paragraph;

        if (type.Value == StyleValues.Character)
            return StyleKind.Character;

        if (type.Value == StyleValues.Table)
            return StyleKind.Table;

        if (type.Value == StyleValues.Numbering)
            return StyleKind.Numbering;

        return StyleKind.Paragraph;
    }

    private static IReadOnlyList<string> ParseAliases(string? rawAliases)
    {
        if (String.IsNullOrEmpty(rawAliases))
            return Array.Empty<string>();

        return rawAliases
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public void Dispose()
    {
        document.Dispose();
    }
}
