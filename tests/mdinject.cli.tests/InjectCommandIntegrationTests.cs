using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Mdinject.Cli.Tests;

/// <summary>
/// Exercises the CLI entry point end to end (real DI wiring, real parser, real injector) to cover
/// the wiring in Program.cs and InjectCommand that the core/docx unit tests don't reach: argument
/// validation, exit codes, and the --force overwrite guard.
/// </summary>
public sealed class InjectCommandIntegrationTests : IDisposable
{
    private readonly string templatePath;
    private readonly string inputPath;
    private readonly string outputPath;

    public InjectCommandIntegrationTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");
        outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");

        CreateTestTemplate(templatePath);
    }

    [Fact]
    public async Task Main_ValidInput_WritesDocumentAndReturnsZero()
    {
        await File.WriteAllTextAsync(inputPath, "# Title\n\nBody text.\n");

        var exitCode = await Program.Main(
            ["--template", templatePath, "--placeholder", "CONTENT", "--input", inputPath, "--output", outputPath]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));

        using var result = WordprocessingDocument.Open(outputPath, false);
        var texts = result.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .Select(p => String.Concat(p.Descendants<Text>().Select(t => t.Text)))
            .ToList();

        Assert.Equal(["Before", "Title", "Body text.", "After"], texts);
    }

    [Fact]
    public async Task Main_OutputAlreadyExistsWithoutForce_DoesNotOverwriteAndReturnsNonZero()
    {
        await File.WriteAllTextAsync(inputPath, "Body text.\n");
        await File.WriteAllBytesAsync(outputPath, [1, 2, 3]);

        var exitCode = await Program.Main(
            ["--template", templatePath, "--placeholder", "CONTENT", "--input", inputPath, "--output", outputPath]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(outputPath));
    }

    [Fact]
    public async Task Main_OutputAlreadyExistsWithForce_Overwrites()
    {
        await File.WriteAllTextAsync(inputPath, "Body text.\n");
        await File.WriteAllBytesAsync(outputPath, [1, 2, 3]);

        var exitCode = await Program.Main(
            ["--template", templatePath, "--placeholder", "CONTENT", "--input", inputPath, "--output", outputPath, "--force"]);

        Assert.Equal(0, exitCode);
        Assert.NotEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(outputPath));
    }

    [Fact]
    public async Task Main_TemplateFileMissing_ReturnsNonZeroWithoutWritingOutput()
    {
        await File.WriteAllTextAsync(inputPath, "Body text.\n");

        var exitCode = await Program.Main(
            ["--template", Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx"), "--placeholder", "CONTENT", "--input", inputPath, "--output", outputPath]);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Main_PlaceholderNotFoundInTemplate_ReturnsOne()
    {
        await File.WriteAllTextAsync(inputPath, "Body text.\n");

        var exitCode = await Program.Main(
            ["--template", templatePath, "--placeholder", "MISSING", "--input", inputPath, "--output", outputPath]);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Main_UnsupportedMarkdownConstruct_ReturnsOne()
    {
        await File.WriteAllTextAsync(inputPath, "1. First\n2. Second\n");

        var exitCode = await Program.Main(
            ["--template", templatePath, "--placeholder", "CONTENT", "--input", inputPath, "--output", outputPath]);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    public void Dispose()
    {
        foreach (var path in new[] { templatePath, inputPath, outputPath })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void CreateTestTemplate(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
            new Paragraph(new Run(new Text("Before"))),
            new Paragraph(new Run(new Text("{{CONTENT}}"))),
            new Paragraph(new Run(new Text("After")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        styles.Append(normal);

        var heading1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
        heading1.Append(new StyleName { Val = "heading 1" });
        heading1.Append(new BasedOn { Val = "Normal" });
        styles.Append(heading1);

        var defaultFont = new Style { Type = StyleValues.Character, StyleId = "DefaultParagraphFont", Default = true };
        defaultFont.Append(new StyleName { Val = "Default Paragraph Font" });
        styles.Append(defaultFont);

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }
}
