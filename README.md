# mdinject

mdinject is a .NET tool that injects Markdown content into existing Word DOCX 
templates using configurable style mappings.

A local tool for local access only is also supported.

## Philosophy

- Markdown defines document structure.
- DOCX template defines appearance.
- mdinject connects the two.

The tool does not generate a new document layout. It opens an existing DOCX template, 
locates a placeholder, converts Markdown into Word structures, applies configured styles, 
and saves a new DOCX document.

## Goals

- Keep the visual design inside Word templates.
- Keep the content in Markdown.
- Support CI/CD document generation.
- Preserve headers, footers, cover pages, TOC placeholders, numbering, and branding from the template.

## Example

Template:

{{CONTENT}}

Markdown:

```md
# Installation

Welcome.

## Requirements

- .NET
- SQL Server

> [!WARNING]
> Danger

```

Command:

```bash
mdinject \
  --configuration configuration.yaml \
  --template template.docx \
  --placeholder CONTENT \
  --input installation.md \
  --output manual.docx \
  --force
```

By default, mdinject refuses to overwrite an existing `--output` file. Pass `--force` to overwrite it.

Override or add individual style-mapping entries from the command line with `--blocks`/`--inlines`,
using the same key names as the YAML file (see [Style Mapping](#style-mapping)). Both are
repeatable and take precedence over whatever `--configuration` loaded:

```bash
mdinject --template template.docx --placeholder CONTENT --input installation.md --output manual.docx \
  --blocks heading1="Heading 1" --blocks paragraph=Normal \
  --inlines code="Inline Code"
```

## Supported Markdown (V1)

- Headings (1-6)
- Paragraphs
- Bullet lists
- Numbered lists
- Blockquotes
- Tables
- Code blocks
- Horizontal rules
- Images
- Links
- Markdown extension: Alerts

## Style Mapping

Default style mapping from standard Word template.

Example:

```yaml
blocks:
  heading1: "Heading 1"
  heading2: "Heading 2"
  heading3: "Heading 3"
  paragraph: "Normal"
  bulletList: "List Bullet"
  numberedList: "List Number"
  codeBlock: "Code"
  table: "Table Grid"
  warning: "YellowParagraph"
  blockquote: "Quote"
  horizontalRule: "Horizontal Rule"
inlines:
  bold: 
  italic:
  code: "Inline Code"
  link: "Hyperlink"
```

Corporate templates may use arbitrary style names:

```yaml
blocks:
  heading1: "Titre 1"
  heading2: "Titre 2"
  paragraph: "Texte Standard"
```

### Default resolution

A construct left out of the configuration doesn't always behave the same way:

- **Bold, italic** — always fall back to Word's own direct character formatting (the same as
  pressing Ctrl+B/Ctrl+I), whether or not a mapping table is given at all.
- **Paragraph, table** — resolve to whichever style the template itself flags as the default for
  that kind (the same style Word applies when nothing is chosen explicitly), so these work out of
  the box even without a configuration file.
- **Headings** — no unambiguous default exists, so mdinject guesses the canonical Word name
  (`"heading 1"`, `"heading 2"`, ...). If the template doesn't define that level, this only becomes
  an error once a document actually uses that heading — not upfront.
- **Code (block and inline)** — has no native Word equivalent to fall back to, so it must be
  configured explicitly if a document uses code; otherwise, it errors when encountered. Explicitly
  configuring an empty value (`code:` with nothing after it) is different from leaving the key out:
  it deliberately opts out of formatting rather than being an error.
- **Links** — a link always becomes a real, clickable Word hyperlink regardless of configuration;
  the `link` style only controls its *appearance* (e.g. the classic blue underline). Unlike code,
  a link never errors for being unconfigured: mdinject guesses the canonical Word name
  (`"Hyperlink"`) and uses it if the template defines it, otherwise the link is left unstyled but
  still fully functional. Explicitly configuring an empty value (`link:`) always opts out of
  styling, even if the template does define a `Hyperlink` style. Only absolute URLs
  (`https://...`, `mailto:...`, ...) are supported — relative links have no meaningful target once
  injected into a Word document and are rejected.
- **Blockquotes** — like links, a blockquote never errors for being unconfigured: mdinject guesses
  the canonical Word name (`"Quote"`) and uses it if the template defines it, otherwise each
  quoted paragraph is indented directly instead (the same category of exception as bold/italic
  direct formatting) and a warning is printed. Unlike links, there's no way to configure an explicit
  blank opt-out for a block style — an absent `blockquote` key and an empty one behave the same.
- **Horizontal rules** — also never error for being unconfigured, but unlike blockquotes/links
  there's no canonical Word style name to guess, so an unconfigured rule draws a direct paragraph
  bottom border (the genuine default, not a degraded fallback — no warning either). Configuring
  `horizontalRule` replaces the border entirely with the named style, handing appearance back to
  the template.
- **Bullet/numbered lists** — not part of style mapping at all, and never error for being
  unconfigured. mdinject creates its own bullet or decimal numbering definition directly in
  `numbering.xml` for every markdown list and references it from each item via a direct
  `w:numPr`/`w:numId`, the same category as bold/italic direct formatting rather than a named
  style — this works whether or not the template defines any numbering of its own. Each separate
  markdown list gets its own numbering definition, so every list restarts at "1." rather than
  continuing a previous one. List items themselves use the resolved `paragraph` style. Nested lists
  and list items spanning more than one paragraph aren't supported yet.

Run `mdinject create configuration` (see [Auxiliary commands](#auxiliary-commands)) to generate a
starter file with everything mdinject can resolve for a specific template already filled in.

## Placeholders

Default syntax:

```text
{{CONTENT}}
```

Examples:

```text
{{INTRO}}
{{API}}
{{APPENDIX}}
```

## Multi-Step Composition

```bash
mdinject --template template.docx --placeholder INTRO --input intro.md --output step1.docx
mdinject --template step1.docx --placeholder API --input api.md --output step2.docx
mdinject --template step2.docx --placeholder APPENDIX --input appendix.md --output final.docx
```

## Non-Goals

- DOCX merging
- Style conflict resolution
- WYSIWYG editing
- PDF generation
- Word replacement

## Typical Use Cases

- API documentation
- Installation guides
- Operations manuals
- Customer deliverables
- Generated release documentation

## Installation

### Global tool
```shell
dotnet tool install -g mdinject
```

### Local tool
```shell
dotnet new tool-manifest
dotnet tool install mdinject
```

## Auxiliary commands

### list styles

List template document styles

```text
StyleId | StyleName | Aliases | Type      | BasedOn
Normln  | Normal    |         | paragraph |
```

Command line
```bash
mdinject list styles template.docx
```

### create configuration

Generates a starter style-mapping YAML file for a specific template: constructs with a genuine
default (paragraph, table, bullet lists) are filled in with the template's own default style,
headings are filled in when the template defines the canonical Word name, and anything mdinject
can't resolve (like code) is left blank for you to fill in — see
[Default resolution](#default-resolution).

Command line
```bash
mdinject create configuration template.docx --output style-mapping.yaml
```

Fill in (or override) specific entries directly with `--blocks`/`--inlines`, e.g. to supply the
constructs mdinject couldn't guess on its own:

```bash
mdinject create configuration template.docx --output style-mapping.yaml \
  --blocks codeBlock=Code --inlines code="Inline Code"
```

### list placeholders

List of placeholders in document

Command line
```bash
mdinject list placeholders template.docx
```
