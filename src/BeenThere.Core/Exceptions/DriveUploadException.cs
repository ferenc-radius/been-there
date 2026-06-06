namespace BeenThere.Core.Exceptions;

/// <summary>
/// Thrown when a file upload to Google Drive fails.
/// </summary>
public class DriveUploadException : Exception
{
    public DriveUploadException(string message)
        : base(message)
    {
    }

    public DriveUploadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
