using Mdinject.Core.Configuration;

namespace Mdinject.Core.Styles;

/// <summary>
/// Metadata describing how each <see cref="BlockStyleKey"/> resolves when unconfigured:
/// - <see cref="Kinds"/>: which <see cref="StyleKind"/> it targets.
/// - <see cref="AutoDefaultKeys"/>: constructs with a genuine Word-native default — resolved via
///   the template's own <see cref="StyleInfo.IsDefault"/> flag for that kind, never an error.
/// - <see cref="DefaultNames"/>: constructs with no native default but a canonical Word name worth
///   guessing (headings only) — resolved via that name if the template happens to define it.
/// Anything in neither set (currently only <see cref="BlockStyleKey.CodeBlock"/>) has no default at
/// all: it must be explicitly configured, and unconfigured use throws only once the construct is
/// actually encountered.
/// </summary>
internal static class BlockStyleKindRegistry
{
    public static readonly IReadOnlyDictionary<BlockStyleKey, StyleKind> Kinds = new Dictionary<BlockStyleKey, StyleKind>
    {
        [BlockStyleKey.Heading1] = StyleKind.Paragraph,
        [BlockStyleKey.Heading2] = StyleKind.Paragraph,
        [BlockStyleKey.Heading3] = StyleKind.Paragraph,
        [BlockStyleKey.Heading4] = StyleKind.Paragraph,
        [BlockStyleKey.Heading5] = StyleKind.Paragraph,
        [BlockStyleKey.Heading6] = StyleKind.Paragraph,
        [BlockStyleKey.Heading7] = StyleKind.Paragraph,
        [BlockStyleKey.Heading8] = StyleKind.Paragraph,
        [BlockStyleKey.Heading9] = StyleKind.Paragraph,
        [BlockStyleKey.Paragraph] = StyleKind.Paragraph,
        [BlockStyleKey.CodeBlock] = StyleKind.Paragraph,
        [BlockStyleKey.Table] = StyleKind.Table,
    };

    public static readonly IReadOnlySet<BlockStyleKey> AutoDefaultKeys = new HashSet<BlockStyleKey>
    {
        BlockStyleKey.Paragraph,
        BlockStyleKey.Table,
    };

    // Word's built-in style names are stored in English internally regardless of UI locale,
    // so these match localized templates too via the style-name resolution tier.
    public static readonly IReadOnlyDictionary<BlockStyleKey, string> DefaultNames = new Dictionary<BlockStyleKey, string>
    {
        [BlockStyleKey.Heading1] = "heading 1",
        [BlockStyleKey.Heading2] = "heading 2",
        [BlockStyleKey.Heading3] = "heading 3",
        [BlockStyleKey.Heading4] = "heading 4",
        [BlockStyleKey.Heading5] = "heading 5",
        [BlockStyleKey.Heading6] = "heading 6",
        [BlockStyleKey.Heading7] = "heading 7",
        [BlockStyleKey.Heading8] = "heading 8",
        [BlockStyleKey.Heading9] = "heading 9",
    };
}
