using Mdinject.Core.Styles;

namespace Mdinject.Core.Configuration;

/// <summary>
/// A style-mapping starter file for a specific template: every known construct, mapped to a
/// best-effort guess where the template makes one resolvable, or <see langword="null"/> where the
/// author needs to decide. Distinct from <see cref="StyleMappingConfiguration"/>, which only ever
/// holds entries an author actually configured (or explicitly left blank).
/// </summary>
public sealed record GeneratedStyleMapping(
    IReadOnlyDictionary<BlockStyleKey, string?> Blocks,
    IReadOnlyDictionary<InlineStyleKey, string?> Inlines);
