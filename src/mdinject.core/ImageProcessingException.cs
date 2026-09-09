namespace Mdinject.Core;

/// <summary>
/// Thrown when an image referenced by the markdown can't be embedded: the file doesn't exist, its
/// format isn't supported, or its dimensions couldn't be determined.
/// </summary>
public sealed class ImageProcessingException : Exception
{
    public ImageProcessingException(string message)
        : base(message)
    {
    }
}
