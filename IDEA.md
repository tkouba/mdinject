# mdinject - Project Idea

mdinject is a document composition tool.

The project follows the principle: "Do one thing well."

## Vision

Create the simplest possible tool that injects Markdown content into 
a Word template while fully respecting corporate styles.

Markdown owns content. Templates own presentation. mdinject owns composition.

## Core Principle

Core rule:

```text
Markdown = Structure
DOCX Template = Appearance
mdinject = Composition
```

Wrong:

```text
Markdown -> Formatting Decisions -> DOCX
```

Correct:

```text
Markdown -> Document Structure
DOCX Template -> Presentation
```

## Problem

Existing tools focus on one of two areas:

1. Full document converters.
2. Variable-replacement template engines.

Neither focuses on inserting large Markdown documents into a corporate Word template.

## Proposed Solution

A .NET Global Tool:

```bash
mdinject
```

Single responsibility:

```text
Placeholder
    +
Markdown
    =
Styled Word Content
```

## V1 Command

```bash
mdinject \
  --template template.docx \
  --placeholder CONTENT \
  --input content.md \
  --output result.docx
```

## Internal Pipeline

```text
Open Template
    ↓
Find Placeholder
    ↓
Parse Markdown
    ↓
Semantic Document Model
    ↓
Map Styles
    ↓
Insert Content
    ↓
Save Document
```

## V1 Features

Supported:

- Heading 1-6
- Paragraphs
- Bullet lists
- Numbered lists
- Tables
- Code blocks
- Images

Not Supported:

- DOCX merging
- Footnotes
- Track changes
- Complex HTML
- Embedded Office objects

## Style Philosophy

Never generate direct formatting, except when specified in the source markdown file.

Avoid:

```text
Font = Calibri
Size = 16
Bold = True
```

Use:

```text
Style = Heading1
```

The template is the single source of truth for appearance.

## Future Ideas

### Variable Injection

Variables from configuration and/or from command line.

```text
{{VERSION}}
{{BUILD_DATE}}
{{COMMIT}}
```

Variable is word expansion not paragraph.

Configuration future extensions:

```yaml
variables:
  VERSION: 1.0.0
```

### Shared Document Model

Potential future renderers:

```text
Markdown AST -> DOCX
Markdown AST -> HTML

```

## Success Criteria

A user can:

1. Create a DOCX template in Word.
2. Write documentation in Markdown.
3. Run a build pipeline.
4. Produce a ready-to-distribute DOCX.

without Pandoc customization, Lua filters, or manual Word formatting.

## Non-Goals

mdinject intentionally does not generate PDF files.

PDF generation is considered a separate concern and can be performed
by dedicated tools using either DOCX as input.

