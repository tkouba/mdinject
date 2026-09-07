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
  --output manual.docx 
```

## Supported Markdown (V1)

- Headings (1-6)
- Paragraphs
- Bullet lists
- Numbered lists
- Tables
- Code blocks
- Images
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
inlines:
  bold: 
  italic:
  code: "Inline Code"
```

Corporate templates may use arbitrary style names:

```yaml
blocks:
  heading1: "Titre 1"
  heading2: "Titre 2"
  paragraph: "Texte Standard"
```

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

### list placeholders

List of placeholders in document

Command line
```bash
mdinject list placeholders template.docx
```
