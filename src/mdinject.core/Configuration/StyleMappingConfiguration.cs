namespace Mdinject.Core.Configuration;

/// <summary>
/// Normalized style-mapping configuration: for each markdown construct, the style reference
/// (alias, style name, or style id) an author configured in YAML. A construct absent from either
/// dictionary was left unconfigured and should fall back to the default style resolver.
/// <see cref="Inlines"/> values may be present but <see langword="null"/>: for inline "code", that's
/// a deliberate opt-out (no formatting) — distinct from the key being absent entirely, which has no
/// default at all.
/// </summary>
public sealed record StyleMappingConfiguration(
    IReadOnlyDictionary<BlockStyleKey, string> Blocks,
    IReadOnlyDictionary<InlineStyleKey, string?> Inlines)
{
    public static StyleMappingConfiguration Empty { get; } = new(
        new Dictionary<BlockStyleKey, string>(),
        new Dictionary<InlineStyleKey, string?>());
}
