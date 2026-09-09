namespace Mdinject.Core.Configuration;

/// <summary>
/// Block-level markdown constructs that can be mapped to a template paragraph style, mirroring the internal Document Model.
/// Bullet/numbered lists (<see cref="DocumentModel.DocumentBlock.List"/>) are deliberately absent:
/// bullets come from a paragraph's direct <c>w:numPr</c>/<c>w:numId</c> reference into
/// numbering.xml mdinject builds itself, not from a named style - the same category as bold/italic
/// direct formatting, not style resolution.
/// </summary>
public enum BlockStyleKey
{
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Heading7,
    Heading8,
    Heading9,
    Paragraph,
    Table,
    CodeBlock,
    Blockquote,
    HorizontalRule,
    Note,
    Tip,
    Important,
    Warning,
    Caution,
}
