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


        });
        return await app.RunAsync(args);
    }
}
