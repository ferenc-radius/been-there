namespace BeenThere.Core.Exceptions;

/// <summary>
/// Thrown when a file download from Google Drive fails or the file is not found.
/// </summary>
public class DriveDownloadException : Exception
{
    public DriveDownloadException(string message)
        : base(message)
    {
    }

    public DriveDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
