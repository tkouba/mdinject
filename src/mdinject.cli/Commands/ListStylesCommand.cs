using System.ComponentModel;
using Mdinject.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mdinject.Cli.Commands;

/// <summary>
/// Lists the styles defined in a template document, so users can see what's available for style mapping.
/// </summary>
[Description("Lists the styles defined in a template document.")]
public sealed class ListStylesCommand(ITemplateDocumentFactory templateDocumentFactory) : AsyncCommand<ListStylesCommand.Settings>
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
        var styles = await templateDocument.GetStylesAsync(cancellationToken);

        var table = new Table();
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Kind");
        table.AddColumn("Default");
        table.AddColumn("Based on");
        table.AddColumn("Aliases");
        table.AddColumn("Custom");

        foreach (var style in styles.OrderBy(s => s.Kind).ThenBy(s => s.Name))
        {
            table.AddRow(
                style.Id,
                style.Name,
                style.Kind.ToString(),
                style.IsDefault ? "yes" : String.Empty,
                style.BasedOnId ?? String.Empty,
                String.Join(", ", style.Aliases),
                style.IsCustom ? "yes" : String.Empty);
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
