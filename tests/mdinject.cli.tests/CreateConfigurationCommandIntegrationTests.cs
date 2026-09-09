using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Mdinject.Cli.Tests;

/// <summary>
/// Exercises "mdinject create configuration" end to end (real DI wiring) via Program.Main, covering
/// the --blocks/--inlines override wiring that the core-level generator/writer tests don't reach.
/// </summary>
public sealed class CreateConfigurationCommandIntegrationTests : IDisposable
{
    private readonly string templatePath;
    private readonly string outputPath;

    public CreateConfigurationCommandIntegrationTests()
    {
        templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");

        using var document = WordprocessingDocument.Create(templatePath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(new Paragraph(new Run(new Text("Hello")))));

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();
        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        styles.Append(normal);
        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
        mainPart.Document.Save();
    }

    [Fact]
    public async Task Main_BlocksAndInlinesOptions_AppearInGeneratedFile()
    {
        var exitCode = await Program.Main(
        [
            "create", "configuration", templatePath, "--output", outputPath,
            "--blocks", "codeBlock=Code", "--inlines", "code=Inline Code",
        ]);

        Assert.Equal(0, exitCode);

        var yaml = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("codeBlock: Code", yaml);
        Assert.Contains("code: Inline Code", yaml);
    }

    [Fact]
    public async Task Main_UnknownBlocksKey_ReturnsOneWithoutWritingOutput()
    {
        var exitCode = await Program.Main(
            ["create", "configuration", templatePath, "--output", outputPath, "--blocks", "nosuchkey=Foo"]);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    public void Dispose()
    {
        foreach (var path in new[] { templatePath, outputPath })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
