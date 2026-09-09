using System.ComponentModel;
using Mdinject.Core;
using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;
using Mdinject.Core.Styles;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mdinject.Cli.Commands;

/// <summary>
/// Injects Markdown content into a Word template at a named placeholder. The default command,
/// matching the README's `mdinject --template ... --placeholder ... --input ... --output ...` usage.
/// </summary>
[Description("Injects Markdown content into a Word template at a named placeholder.")]
public sealed class InjectCommand(
    IMarkdownDocumentParser parser,
    IDocumentInjector injector,
    IStyleMappingConfigurationLoader configurationLoader) : AsyncCommand<InjectCommand.Settings>
{
    private readonly IMarkdownDocumentParser _parser = parser;
    private readonly IDocumentInjector _injector = injector;
    private readonly IStyleMappingConfigurationLoader _configurationLoader = configurationLoader;

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the style-mapping YAML configuration file. Built-in defaults are used if omitted.")]
        [CommandOption("--configuration <PATH>")]
        public string? ConfigurationPath { get; init; }

        [Description("Path to the Word template document (.docx).")]
        [CommandOption("--template <PATH>")]
        public string TemplatePath { get; init; } = String.Empty;

        [Description("Placeholder name to replace, e.g. CONTENT for {{CONTENT}}.")]
        [CommandOption("--placeholder <NAME>")]
        public string Placeholder { get; init; } = String.Empty;

        [Description("Path to the input Markdown file.")]
        [CommandOption("--input <PATH>")]
        public string InputPath { get; init; } = String.Empty;

        [Description("Path to write the resulting Word document to.")]
        [CommandOption("--output <PATH>")]
        public string OutputPath { get; init; } = String.Empty;

        [Description("Overwrite the output file if it already exists.")]
        [CommandOption("--force")]
        public bool Force { get; init; }

        public override ValidationResult Validate()
        {
            if (!File.Exists(TemplatePath))
                return ValidationResult.Error($"Template file not found: {TemplatePath}");

            if (!File.Exists(InputPath))
                return ValidationResult.Error($"Input Markdown file not found: {InputPath}");

            if (String.IsNullOrWhiteSpace(Placeholder))
                return ValidationResult.Error("A --placeholder name is required.");

            if (ConfigurationPath != null && !File.Exists(ConfigurationPath))
                return ValidationResult.Error($"Configuration file not found: {ConfigurationPath}");

            if (!Force && File.Exists(OutputPath))
                return ValidationResult.Error($"Output file already exists: {OutputPath}. Use --force to overwrite.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = settings.ConfigurationPath != null
                ? await _configurationLoader.LoadAsync(settings.ConfigurationPath, cancellationToken)
                : StyleMappingConfiguration.Empty;

            var markdown = await File.ReadAllTextAsync(settings.InputPath, cancellationToken);
            var document = _parser.Parse(markdown);

            var warnings = await _injector.InjectAsync(settings.TemplatePath, settings.OutputPath, settings.Placeholder, document, configuration, cancellationToken);

            foreach (var warning in warnings)
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warning)}");

            AnsiConsole.MarkupLine($"[green]Document written to[/] {Markup.Escape(settings.OutputPath)}");
            return 0;
        }
        catch (Exception ex) when (ex is StyleMappingConfigurationException or MarkdownConversionException or StyleResolutionException or PlaceholderNotFoundException or NotSupportedException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
