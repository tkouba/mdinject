using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mdinject.Core.Configuration;

/// <summary>
/// Writes a <see cref="GeneratedStyleMapping"/> as YAML in the same shape
/// <see cref="StyleMappingConfigurationLoader"/> reads, using the canonical key names from
/// <see cref="StyleKeyNames"/>.
/// </summary>
public sealed class StyleMappingConfigurationWriter : IStyleMappingConfigurationWriter
{
    private readonly ISerializer serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task WriteAsync(string path, GeneratedStyleMapping mapping, CancellationToken cancellationToken = default)
    {
        var raw = new RawStyleMappingConfiguration
        {
            Blocks = ToRaw(mapping.Blocks, StyleKeyNames.BlockNamesByKey),
            Inlines = ToRaw(mapping.Inlines, StyleKeyNames.InlineNamesByKey),
        };

        var yaml = serializer.Serialize(raw);

        await File.WriteAllTextAsync(path, yaml, cancellationToken);
    }

    private static Dictionary<string, string?> ToRaw<TKey>(IReadOnlyDictionary<TKey, string?> mapping, IReadOnlyDictionary<TKey, string> namesByKey)
        where TKey : notnull
    {
        var raw = new Dictionary<string, string?>();

        foreach (var (key, value) in mapping)
            raw[namesByKey[key]] = value;

        return raw;
    }
}
