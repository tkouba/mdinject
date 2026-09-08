namespace Mdinject.Core.Configuration;

/// <summary>
/// Block-level markdown constructs that can be mapped to a template paragraph style, mirroring the internal Document Model.
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
    BulletList,
    Table,
    CodeBlock,
}
