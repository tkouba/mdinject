namespace Mdinject.Core.DocumentModel;

/// <summary>
/// The GitHub-style alert types recognized in a <c>&gt; [!KIND]</c> blockquote marker.
/// </summary>
public enum AlertKind
{
    Note,
    Tip,
    Important,
    Warning,
    Caution,
}
