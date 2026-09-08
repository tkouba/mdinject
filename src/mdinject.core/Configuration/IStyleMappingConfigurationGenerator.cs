using Mdinject.Core.Styles;

namespace Mdinject.Core.Configuration;

/// <summary>
/// Generates a starter style-mapping configuration for a specific template, pre-filling whatever
/// can be confidently resolved and leaving the rest blank for the author to decide.
/// </summary>
public interface IStyleMappingConfigurationGenerator
{
    GeneratedStyleMapping Generate(IReadOnlyList<StyleInfo> templateStyles);
}
