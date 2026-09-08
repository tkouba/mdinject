namespace Mdinject.Core.DocumentModel;

/// <summary>
/// A block-level construct in the internal document model, per AGENTS.md:
/// Document → Heading / Paragraph / BulletList / Table / CodeBlock. Table is not modeled yet
/// (deferred to a later pass).
/// </summary>
public abstract record DocumentBlock
{
    private DocumentBlock()
    {
    }

    public sealed record Heading(int Level, IReadOnlyList<InlineSpan> Content) : DocumentBlock;

    public sealed record Paragraph(IReadOnlyList<InlineSpan> Content) : DocumentBlock;

    public sealed record BulletList(IReadOnlyList<IReadOnlyList<InlineSpan>> Items) : DocumentBlock;

    public sealed record CodeBlock(string Text) : DocumentBlock;
}
