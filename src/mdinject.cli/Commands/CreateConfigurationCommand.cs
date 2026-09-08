using System.ComponentModel;
using Mdinject.Core;
using Mdinject.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mdinject.Cli.Commands;

/// <summary>
/// Generates a starter style-mapping YAML file from a template's styles, so users have a real,
/// editable configuration to start from instead of writing one by hand.
/// </summary>
[Description("Generates a starter style-mapping configuration file from a template's styles.")]
public sealed class CreateConfigurationCommand(
    ITemplateDocumentFactory templateDocumentFactory,
    IStyleMappingConfigurationGenerator generator,
    IStyleMappingConfigurationWriter writer) : AsyncCommand<CreateConfigurationCommand.Settings>
{
    private readonly ITemplateDocumentFactory _templateDocumentFactory = templateDocumentFactory;
    private readonly IStyleMappingConfigurationGenerator _generator = generator;
    private readonly IStyleMappingConfigurationWriter _writer = writer;

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the template document (.docx).")]
        [CommandArgument(0, "<TEMPLATE>")]
        public string TemplatePath { get; init; } = String.Empty;

        [Description("Path to write the generated style-mapping YAML file to.")]
        [CommandOption("-o|--output <PATH>")]
        [DefaultValue("style-mapping.yaml")]
        public string OutputPath { get; init; } = "style-mapping.yaml";

        [Description("Overwrite the output file if it already exists.")]
        [CommandOption("-f|--force")]
        public bool Force { get; init; }

        public override ValidationResult Validate()
        {
            if (!File.Exists(TemplatePath))
                return ValidationResult.Error($"Template file not found: {TemplatePath}");

            if (File.Exists(OutputPath) && !Force)
                return ValidationResult.Error($"Output file already exists: {OutputPath}. Use --force to overwrite it.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        using var templateDocument = _templateDocumentFactory.Open(settings.TemplatePath);
        var styles = await templateDocument.GetStylesAsync(cancellationToken);

        var mapping = _generator.Generate(styles);

        await _writer.WriteAsync(settings.OutputPath, mapping, cancellationToken);

        AnsiConsole.MarkupLine($"[green]Style mapping configuration written to[/] {settings.OutputPath}");

        return 0;
    }
}
