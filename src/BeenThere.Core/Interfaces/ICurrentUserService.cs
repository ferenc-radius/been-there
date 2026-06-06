namespace BeenThere.Core.Interfaces;

/// <summary>
/// Provides the current authenticated user's ID to infrastructure layers.
/// Abstracted so Core has no ASP.NET Core dependency.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The current user's ID, or <c>null</c> when the request is unauthenticated.
    /// </summary>
    string? UserId { get; }
}
