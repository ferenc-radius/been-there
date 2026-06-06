namespace BeenThere.Core.Exceptions;

/// <summary>
/// Thrown when Google Drive access must be re-consented by the user.
/// </summary>
public sealed class DriveReauthenticationRequiredException : Exception
{
    public DriveReauthenticationRequiredException(string message)
        : base(message)
    {
    }

    public DriveReauthenticationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
