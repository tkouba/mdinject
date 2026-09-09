using Mdinject.Core.Styles;

namespace Mdinject.Core;

/// <summary>
/// Read-only view over a template document, used to inspect what it offers before injection.
/// </summary>
public interface ITemplateDocument : IDisposable
{
    /// <summary>
    /// Returns all styles defined in the template document.
    /// </summary>
    Task<IReadOnlyList<StyleInfo>> GetStylesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the names of all placeholders (e.g. "CONTENT" for {{CONTENT}}) found in the template
    /// document, in document order with duplicates removed. Per the V1 placeholder model, a
    /// placeholder must occupy a full paragraph on its own.
    /// </summary>
    Task<IReadOnlyList<string>> GetPlaceholdersAsync(CancellationToken cancellationToken = default);
}
