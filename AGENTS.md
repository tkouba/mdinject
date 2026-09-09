# mdinject 

## Code Style

Prefer:

- sealed
- readonly
- async/await
- records where appropriate
- dependency injection
- nullable reference types
- English comments

Avoid:

- static mutable state
- Thread usage
- Task.Run in hot paths
- reflection without strong justification

### Type names vs aliases
- **Declarations, casts, type annotations** → C# alias: `int`, `string`, `bool`, `double`
- **Static methods, static properties, constants** → BCL class name: `Int32.TryParse`, `String.IsNullOrEmpty`, `String.Empty`, `Double.NaN`

### Null and length checks
- Prefer explicit checks: `value != null && value.Length > 0` over pattern matching `value is { Length: > 0 }`

### Console applications
- Do not use top-level statements, use explicit `Program` class with `Main` method.
- Prefer async `Main`

### Code refactoring
- **Keep comments** → Never remove comments from code, regardless of language. Do not translate comments unless the user asks for translation.

## Testing Expectations

Before merging:

- Unit tests pass
- Disposal tested (where appropriate and convenient — skip if it would require a mocking library or invasive testability seams for a minor case)
- Cancellation tested (where appropriate and convenient — skip if it would require a mocking library or invasive testability seams for a minor case)

## Version Management

Git Tag-based versioning

## Backward Compatibility

Breaking changes require explicit justification.


## High-Level Architecture

```text
+----------------+
| Markdown Input |
+----------------+
        |
        v
+----------------+
| Markdig Parser |
+----------------+
        |
        v
+----------------+
| Document Model |
+----------------+
        |
        v
+----------------+
| Style Mapper   |
+----------------+
        |
        v
+----------------+
| DOCX Injector  |
+----------------+
        |
        v
+----------------+
| Output DOCX    |
+----------------+
```

## Technical Stack

* net 10 (or higher)
* Microsoft.Extensions.Configuration
* Microsoft.Extensions.Logging
* Microsoft.Extensions.DependencyInjection
* YamlDotNet
* NReco.Logging.File

### Markdown Parser

* Markdig

### DOCX Engine

* Open XML SDK

### CLI

* Microsoft.Extensions.Configuration.FileExtensions
* Microsoft.Extensions.Configuration.UserSecrets
* Spectre.Console.Cli
* Spectre.Console.Cli.Extensions.DependencyInjection

## Project Structure

```text
README.md
mdinject.slnx
src/
 ├─ mdinject.cli/
 ├─ mdinject.core/
 |   ├─ Configuration
 |   ├─ IDocumentInjector interface
 |   ├─ StyleMapping
 |   └─ Markdig dependency
 └─ mdinject.docx/
     ├─ DocxInjector main injector class
     └─ Markdig dependency (AST model)
tests/
 ├─ mdinject.core.tests/
 └─ mdinject.docx.tests/
docs/
```

### mdinject.cli

(assembly name mdinject)

Responsibilities:

- argument parsing
- configuration loading
- pipeline orchestration
- logging
- exit codes

### mdinject.core

Responsibilities:

- Markdown parsing
- internal document model
- style mapping
- content rendering abstraction

No OpenXML dependencies.

### mdinject.docx

Responsibilities:

- OpenXML integration
- placeholder discovery
- insertion of Word elements
- image handling
- document save operations

## Placeholder Model

Supported format:

```text
{{CONTENT}}
{{API}}
{{INTRO}}
```

V1 assumes a placeholder occupies a full paragraph.

Example:

```text
Executive Summary

{{CONTENT}}

Appendix
```

Injection process:

1. Locate placeholder text.
2. Capture insertion position.
3. Remove placeholder paragraph.
4. Insert generated Word elements.
5. Save document.

## Internal Document Model

Example:

```text
Document
 ├─ Heading(level=1)
 ├─ Paragraph
 ├─ List(ordered)
 ├─ Blockquote
 ├─ Table
 ├─ CodeBlock
 └─ HorizontalRule
```

The model intentionally contains no presentation properties.

Forbidden examples:

```text
Font
Color
Size
Margins
Spacing
```

Those belong to Word styles.

## Style Mapping

Configuration example:

```yaml
styles:
  heading1: Heading 1
  heading2: Heading 2
  paragraph: Normal
  bulletList: List Bullet
```

Rendering rule:

```text
Heading(1)
    ->
Style = Heading 1
```

No direct formatting should be generated.

## Markdown Support (V1)

### Headings

```md
# Heading 1
## Heading 2
```

### Paragraphs

```md
Normal paragraph.
```

### Lists

Bullet and numbered, flat (no nesting), single-paragraph items only.

```md
- Item A
- Item B
```

```md
1. Step one
2. Step two
```

Bullets/numbers are direct `w:numPr`/`w:numId` formatting into a numbering definition mdinject
creates itself in `numbering.xml` - not a named style - so this works regardless of what the
template defines. Each separate list gets its own numbering definition so it always restarts
at "1."; list items use the resolved `paragraph` style.

### Tables

GitHub-style pipe tables (a header row is required; no column alignment, no nested block content
in a cell). Rendered as a real Word `w:tbl` using the resolved `table`/`paragraph` styles, with the
header row marked `w:tblHeader` so a template's own conditional header-row formatting (if any)
applies - never direct formatting. A table is always followed by an empty paragraph, since OOXML
doesn't allow one to be the last body content or immediately followed by another table.

### Code Blocks

Fenced code blocks.

### Horizontal Rules

`---`/`***`/`___` on its own line. Unlike blockquote/link, there's no canonical Word style name to
guess, so an unconfigured rule draws a direct paragraph bottom border - the intentional default
(no warning), not a degraded fallback. Configuring `horizontalRule` replaces the border entirely
with the named style instead, per `StyleResolver.ResolveHorizontalRuleStyle`.

### Images

Relative file references.

### Links

`[text](url)`. Only absolute URLs (`https://...`, `mailto:...`, ...) are supported; relative URLs
are rejected. Rendered as a native Word hyperlink (clickable regardless of configuration); the
`link` style mapping only controls its appearance - unconfigured, it guesses the canonical
`"Hyperlink"` character style name (falling back to unstyled if the template doesn't define it)
rather than erroring like `code` does.

### Blockquotes

`> quoted text`, one or more paragraphs (a blank `>` line starts a new paragraph within the same
quote); nested constructs (lists, further blockquotes, ...) aren't supported yet. Like links, an
unconfigured blockquote never errors: mdinject guesses the canonical `"Quote"` paragraph style name,
falling back to direct paragraph indentation (`w:ind w:left="720"`) with a warning if the template
doesn't define it.

## Image Strategy

Images are copied into the DOCX package.

Example:

```md
![Architecture](images/architecture.png)
```

Rules:

- preserve aspect ratio
- use default sizing initially
- no advanced layout in V1

## Error Handling

Possible errors:

- placeholder not found
- invalid template
- invalid markdown
- missing image
- missing style mapping

CLI should return non-zero exit codes.

## Configuration

Example:

```yaml
blocks:
  heading1: Heading 1
  heading2: Heading 2
  heading3: Heading 3
  paragraph: Normal
inlines:
  bold: 
  italic:
  code: "Inline Code"
```

Blocks are mapped to paragraph docx style types. Inlines are mapped to character docx style types.

### Command-line overrides

Both `mdinject` (inject) and `mdinject create configuration` accept repeatable `--blocks`/
`--inlines <KEY=VALUE>` options (Spectre.Console.Cli dictionary options, split on `=`), using the
same flat key names as the YAML file. `StyleMappingConfigurationOverrides.Apply` layers these on top
of a loaded/generated mapping - an override always wins - reusing the same key-name resolution and
blank-value rules as `StyleMappingConfigurationLoader` (`RawStyleMappingNormalizer`, shared by both).

### DOCX style resolver

How to find DOCX style from markdown style using configuration:

1. Alias
2. StyleName
3. StyleId - normalized (without spaces, non-ascii-7 characters and case-insensitive)


### Default styles resolver

Resolving styles without explicit configuration - use default paragraph style ids (Normal, heading 1)
with inlines bold/italic as bold or italic font (this is an exception from the rule "no font styles")
and code style as nothing (could be changed in future).

## Design Constraints

mdinject should never:

- merge unrelated DOCX files
- resolve style conflicts
- redesign a template
- replace Word layout features

The template remains the single source of truth for presentation.
