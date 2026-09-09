namespace Mdinject.Core.DocumentModel;

/// <summary>
/// A run of text within a block, carrying only the semantic inline flags mdinject models
/// (bold/italic/code) and an optional hyperlink target — no presentation properties, per
/// AGENTS.md's document model constraints. <see cref="LinkUrl"/> is the link's absolute URL
/// (e.g. "https://..." or "mailto:..."), or null when the span isn't part of a link.
/// </summary>
public sealed record InlineSpan(string Text, bool Bold, bool Italic, bool Code, string? LinkUrl = null);
