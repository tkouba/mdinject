namespace Mdinject.Core.DocumentModel;

/// <summary>
/// A block-level construct in the internal document model, per AGENTS.md:
/// Document → Heading / Paragraph / List / Blockquote / Table / CodeBlock / HorizontalRule / Image.
/// </summary>
public abstract record DocumentBlock
{
    private DocumentBlock()
    {
    }

    public sealed record Heading(int Level, IReadOnlyList<InlineSpan> Content) : DocumentBlock;

    public sealed record Paragraph(IReadOnlyList<InlineSpan> Content) : DocumentBlock;

    /// <summary>
    /// A flat (non-nested) bullet (<paramref name="Ordered"/> false) or numbered (true) list. Word
    /// renders the marker itself via numbering, not a style, so this carries no presentation
    /// properties beyond the ordered/unordered distinction, which is structural markdown syntax
    /// (<c>-</c>/<c>*</c> vs <c>1.</c>), not appearance.
    /// </summary>
    public sealed record List(bool Ordered, IReadOnlyList<IReadOnlyList<InlineSpan>> Items) : DocumentBlock;

    /// <summary>
    /// A blockquote, optionally tagged as a GitHub-style alert (<c>&gt; [!NOTE]</c>, etc.) via
    /// <paramref name="Alert"/>. An alert falls back to being rendered as a plain blockquote
    /// whenever its own type has no configured style - see
    /// <see cref="Styles.StyleResolver.ResolveAlertStyle"/>.
    /// </summary>
    public sealed record Blockquote(IReadOnlyList<IReadOnlyList<InlineSpan>> Paragraphs, AlertKind? Alert = null) : DocumentBlock;

    /// <summary>
    /// A GitHub-style pipe table: one header row plus zero or more body rows, each cell holding a
    /// single run of inline content (a cell is not a full nested block). Column alignment and
    /// nested block content within a cell aren't modeled - no presentation properties, per the
    /// document model's constraints.
    /// </summary>
    public sealed record Table(
        IReadOnlyList<IReadOnlyList<InlineSpan>> HeaderCells,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<InlineSpan>>> Rows) : DocumentBlock;

    public sealed record CodeBlock(string Text) : DocumentBlock;

    /// <summary>
    /// A thematic break (<c>---</c>/<c>***</c>/<c>___</c> on its own line). No content or
    /// presentation properties - see <see cref="Styles.HorizontalRuleStyleResolution"/> for how it
    /// resolves to appearance.
    /// </summary>
    public sealed record HorizontalRule : DocumentBlock;

    /// <summary>
    /// A standalone image (<c>![alt](source)</c> as the only content of its paragraph). Only a
    /// local file path - relative to the markdown file, or absolute - is supported for
    /// <paramref name="Source"/>; remote URLs are rejected at parse time. No resizing, cropping, or
    /// format conversion is performed - the image is embedded exactly as it is on disk, sized to
    /// its natural pixel dimensions.
    /// </summary>
    public sealed record Image(string Source, string AltText) : DocumentBlock;
}
