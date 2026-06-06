namespace BeenThere.Core.Exceptions;

/// <summary>
/// Thrown when user folder creation or lookup in Google Drive fails.
/// </summary>
public class DriveFolderCreationException : Exception
{
    public DriveFolderCreationException(string message)
        : base(message)
    {
    }

    public DriveFolderCreationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
