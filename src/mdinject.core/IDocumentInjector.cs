using Mdinject.Core.Configuration;
using Mdinject.Core.DocumentModel;

namespace Mdinject.Core;

/// <summary>
/// Injects a parsed document into a copy of a Word template at a named placeholder, and saves the
/// result. The template itself is never modified.
/// </summary>
public interface IDocumentInjector
{
    /// <returns>
    /// Warnings about automatic style fallbacks the caller should surface to the user (e.g. a link
    /// rendered without a named style because the template defines no canonical "Hyperlink" style).
    /// Empty when nothing needed a fallback.
    /// </returns>
    /// <param name="basePath">
    /// Directory that a relative <see cref="DocumentModel.DocumentBlock.Image.Source"/> resolves
    /// against (typically the markdown input file's directory). Defaults to the current directory.
    /// </param>
    /// <exception cref="PlaceholderNotFoundException">No paragraph in the template contains exactly "{{placeholder}}".</exception>
    /// <exception cref="Styles.StyleResolutionException">A construct in <paramref name="document"/> has no resolvable style.</exception>
    /// <exception cref="ImageProcessingException">An image can't be embedded (missing file, unsupported format, ...).</exception>
    Task<IReadOnlyList<string>> InjectAsync(
        string templatePath,
        string outputPath,
        string placeholder,
        Document document,
        StyleMappingConfiguration configuration,
        string basePath = ".",
        CancellationToken cancellationToken = default);
}
