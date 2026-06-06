using BeenThere.Core.Exceptions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BeenThere.Infrastructure.Drive;

/// <summary>
/// Builds an authenticated Google Drive client for a user by loading OAuth tokens
/// from Identity, refreshing them, and persisting any new tokens back.
/// </summary>
internal sealed partial class GoogleDriveClientFactory(
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    ILogger<GoogleDriveClientFactory> logger) : IGoogleDriveClientFactory
{
    private const string GoogleLoginProvider = "Google";
    private const string RefreshTokenName = "refresh_token";
    private const string AccessTokenName = "access_token";

    public async Task<DriveService> CreateAsync(IdentityUser user)
    {
        var refreshToken = await userManager.GetAuthenticationTokenAsync(
            user, GoogleLoginProvider, RefreshTokenName);
        var accessToken = await userManager.GetAuthenticationTokenAsync(
            user, GoogleLoginProvider, AccessTokenName);

        if (string.IsNullOrEmpty(refreshToken) && string.IsNullOrEmpty(accessToken))
        {
            throw new DriveReauthenticationRequiredException(
                "Google Drive access has expired. Please sign out and sign in again.");
        }

        var credential = BuildCredential(user.Id, accessToken, refreshToken);
        await RefreshCredentialAsync(user.Id, credential);
        await PersistRefreshedTokensAsync(user, credential);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BeenThere"
        });
    }

    private UserCredential BuildCredential(string userId, string? accessToken, string? refreshToken)
    {
        var clientId = configuration["GOOGLE_CLIENT_ID"]
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not configured");
        var clientSecret = configuration["GOOGLE_CLIENT_SECRET"]
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not configured");

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret }
        });

        return new UserCredential(flow, userId, new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        });
    }

    private async Task RefreshCredentialAsync(string userId, UserCredential credential)
    {
        try
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
        }
        catch (TokenResponseException ex)
        {
            LogTokenRefreshFailed(logger, userId, ex);
            throw new DriveReauthenticationRequiredException(
                "Google Drive access has expired. Please sign out and sign in again.", ex);
        }
    }

    private async Task PersistRefreshedTokensAsync(IdentityUser user, UserCredential credential)
    {
        if (!string.IsNullOrEmpty(credential.Token.AccessToken))
        {
            await userManager.SetAuthenticationTokenAsync(
                user, GoogleLoginProvider, AccessTokenName, credential.Token.AccessToken);
        }

        if (!string.IsNullOrEmpty(credential.Token.RefreshToken))
        {
            await userManager.SetAuthenticationTokenAsync(
                user, GoogleLoginProvider, RefreshTokenName, credential.Token.RefreshToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh failed for user {UserId}")]
    private static partial void LogTokenRefreshFailed(ILogger logger, string userId, Exception ex);
}
