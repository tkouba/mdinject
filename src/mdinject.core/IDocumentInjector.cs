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
    /// <exception cref="PlaceholderNotFoundException">No paragraph in the template contains exactly "{{placeholder}}".</exception>
    /// <exception cref="Styles.StyleResolutionException">A construct in <paramref name="document"/> has no resolvable style.</exception>
    Task<IReadOnlyList<string>> InjectAsync(
        string templatePath,
        string outputPath,
        string placeholder,
        Document document,
        StyleMappingConfiguration configuration,
        CancellationToken cancellationToken = default);
}
