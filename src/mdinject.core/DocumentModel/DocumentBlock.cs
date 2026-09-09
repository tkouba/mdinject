namespace Mdinject.Core.DocumentModel;

/// <summary>
/// A block-level construct in the internal document model, per AGENTS.md:
/// Document → Heading / Paragraph / List / Blockquote / Table / CodeBlock. Table is not modeled yet
/// (deferred to a later pass).
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

    public sealed record Blockquote(IReadOnlyList<IReadOnlyList<InlineSpan>> Paragraphs) : DocumentBlock;

    public sealed record CodeBlock(string Text) : DocumentBlock;
}
