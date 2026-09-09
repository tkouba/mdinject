using System.ComponentModel;
using Mdinject.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mdinject.Cli.Commands;

/// <summary>
/// Lists the placeholders found in a template document, so users can see what's available to inject into.
/// </summary>
[Description("Lists the placeholders found in a template document.")]
public sealed class ListPlaceholdersCommand(ITemplateDocumentFactory templateDocumentFactory) : AsyncCommand<ListPlaceholdersCommand.Settings>
{
    private readonly ITemplateDocumentFactory _templateDocumentFactory = templateDocumentFactory;

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the template document (.docx).")]
        [CommandArgument(0, "<TEMPLATE>")]
        public string TemplatePath { get; init; } = String.Empty;

        public override ValidationResult Validate()
        {
            if (!File.Exists(TemplatePath))
                return ValidationResult.Error($"Template file not found: {TemplatePath}");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        using var templateDocument = _templateDocumentFactory.Open(settings.TemplatePath);
        var placeholders = await templateDocument.GetPlaceholdersAsync(cancellationToken);

        var table = new Table();
        table.AddColumn("Placeholder");

        foreach (var placeholder in placeholders)
            table.AddRow(placeholder);

        AnsiConsole.Write(table);

        return 0;
    }
}
