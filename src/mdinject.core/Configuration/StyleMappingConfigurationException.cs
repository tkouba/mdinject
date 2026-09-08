namespace Mdinject.Core.Configuration;

/// <summary>
/// Thrown when a style-mapping configuration file is malformed: invalid YAML or an unknown block/inline key.
/// </summary>
public sealed class StyleMappingConfigurationException : Exception
{
    public StyleMappingConfigurationException(string message)
        : base(message)
    {
    }

    public StyleMappingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
