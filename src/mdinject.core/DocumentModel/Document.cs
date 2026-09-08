namespace Mdinject.Core.DocumentModel;

/// <summary>
/// The root of the internal document model produced by parsing Markdown. Intentionally free of
/// presentation properties — appearance is entirely the template's responsibility.
/// </summary>
public sealed record Document(IReadOnlyList<DocumentBlock> Blocks);
