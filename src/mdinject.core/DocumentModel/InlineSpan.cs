namespace Mdinject.Core.DocumentModel;

/// <summary>
/// A run of text within a block, carrying only the semantic inline flags mdinject models
/// (bold/italic/code) — no presentation properties, per AGENTS.md's document model constraints.
/// </summary>
public sealed record InlineSpan(string Text, bool Bold, bool Italic, bool Code);
