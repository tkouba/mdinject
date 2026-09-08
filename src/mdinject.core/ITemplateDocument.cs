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
}
