using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Mdinject.Cli.Tests;

/// <summary>
/// Exercises "mdinject list placeholders" end to end (real DI wiring) via Program.Main.
/// </summary>
public sealed class ListPlaceholdersCommandIntegrationTests : IDisposable
{
    private readonly string templatePath;

    public ListPlaceholdersCommandIntegrationTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");

        using var document = WordprocessingDocument.Create(templatePath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("{{CONTENT}}"))),
            new Paragraph(new Run(new Text("{{LICENSE}}")))));
        mainPart.Document.Save();
    }

    [Fact]
    public async Task Main_ValidTemplate_ReturnsZero()
    {
        var exitCode = await Program.Main(["list", "placeholders", templatePath]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_TemplateFileMissing_ReturnsNonZero()
    {
        var exitCode = await Program.Main(["list", "placeholders", Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx")]);

        Assert.NotEqual(0, exitCode);
    }

    public void Dispose()
    {
        if (File.Exists(templatePath))
            File.Delete(templatePath);
    }
}
