using Mdinject.Cli.Commands;
using Mdinject.Core;
using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;
using Mdinject.Core.Styles;
using Mdinject.Docx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;

namespace Mdinject.Cli;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
#if DEBUG
            .AddUserSecrets<Program>()
#endif
            ;

        var services = new ServiceCollection();
        services.AddSingleton<ITemplateDocumentFactory, DocxTemplateDocumentFactory>();
        services.AddSingleton<IStyleMappingConfigurationGenerator, StyleMappingConfigurationGenerator>();
        services.AddSingleton<IStyleMappingConfigurationWriter, StyleMappingConfigurationWriter>();
        services.AddSingleton<IStyleMappingConfigurationLoader, StyleMappingConfigurationLoader>();
        services.AddSingleton<IStyleResolver, StyleResolver>();
        services.AddSingleton<IMarkdownDocumentParser, MarkdownDocumentParser>();
        services.AddSingleton<IDocumentInjector, DocxInjector>();

        using var registrar = new DependencyInjectionRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetName().Name ?? "mdinject";
            config.SetApplicationName(name);
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "0.0.0";

            StringBuilder versionInfo = new StringBuilder($"Version: {version}");

            config.SetApplicationVersion(versionInfo.ToString());

            config.AddBranch("list", branch =>
            {
                branch.SetDescription("List information about a template document.");
                branch.AddCommand<ListStylesCommand>("styles");
                branch.AddCommand<ListPlaceholdersCommand>("placeholders");
            });

            config.AddBranch("create", branch =>
            {
                branch.SetDescription("Create files from a template document.");
                branch.AddCommand<CreateConfigurationCommand>("configuration");
            });
        });
        app.SetDefaultCommand<InjectCommand>();
        return await app.RunAsync(args);
    }
}
