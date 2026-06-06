#pragma warning disable CA1707 // Test names use underscore-separated segments per project convention

using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Drive;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BeenThere.Integration.Tests;

/// <summary>
/// Tests for Milestone 2: Google OAuth + Drive storage guard conditions.
/// Happy-path Drive API tests require real Google credentials and are handled
/// in a separate live-integration suite.
/// </summary>
public class Milestone2IntegrationTests
{
    private static Mock<UserManager<IdentityUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(s => s.UserId).Returns((string?)null);
        return new ApplicationDbContext(options, currentUser.Object);
    }

    private static IConfiguration CreateConfiguration()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["GOOGLE_CLIENT_ID"]).Returns("test-client-id");
        config.Setup(c => c["GOOGLE_CLIENT_SECRET"]).Returns("test-client-secret");
        return config.Object;
    }

    private static void SetupBothTokensNull(Mock<UserManager<IdentityUser>> userManagerMock, IdentityUser user)
    {
        userManagerMock
            .Setup(um => um.GetAuthenticationTokenAsync(user, "Google", "refresh_token"))
            .ReturnsAsync((string?)null);
        userManagerMock
            .Setup(um => um.GetAuthenticationTokenAsync(user, "Google", "access_token"))
            .ReturnsAsync((string?)null);
    }

    private static DriveService CreateDriveService(
        Mock<UserManager<IdentityUser>> userManagerMock,
        ApplicationDbContext? db = null,
        IConfiguration? configuration = null)
    {
        var config = configuration ?? CreateConfiguration();
        var factory = new GoogleDriveClientFactory(
            userManagerMock.Object,
            config,
            new Mock<ILogger<GoogleDriveClientFactory>>().Object);
        var logger = new Mock<ILogger<DriveService>>();
        var dbContext = db ?? CreateInMemoryDbContext(Guid.NewGuid().ToString());
        return new DriveService(userManagerMock.Object, dbContext, factory, logger.Object);
    }

    // ── CreateUserFolderAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateUserFolderAsync_UnknownUser_ThrowsDriveFolderCreationException()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var driveService = CreateDriveService(userManagerMock);
        var userId = "nonexistent-" + Guid.NewGuid();

        userManagerMock
            .Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((IdentityUser?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BeenThere.Core.Exceptions.DriveFolderCreationException>(
            () => driveService.CreateUserFolderAsync(userId, CancellationToken.None));

        Assert.Contains("Failed to create or lookup user folder in Drive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUserFolderAsync_NoTokens_ThrowsDriveReauthenticationRequiredException()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var driveService = CreateDriveService(userManagerMock);
        var userId = "user-" + Guid.NewGuid();
        var user = new IdentityUser { Id = userId, UserName = "testuser" };

        userManagerMock.Setup(um => um.FindByIdAsync(userId)).ReturnsAsync(user);
        SetupBothTokensNull(userManagerMock, user);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BeenThere.Core.Exceptions.DriveReauthenticationRequiredException>(
            () => driveService.CreateUserFolderAsync(userId, CancellationToken.None));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── UploadFileAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFileAsync_NoTokens_ThrowsDriveReauthenticationRequiredException()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var driveService = CreateDriveService(userManagerMock);
        var userId = "user-" + Guid.NewGuid();
        var user = new IdentityUser { Id = userId, UserName = "testuser" };

        userManagerMock.Setup(um => um.FindByIdAsync(userId)).ReturnsAsync(user);
        SetupBothTokensNull(userManagerMock, user);

        // Act & Assert
        await Assert.ThrowsAsync<BeenThere.Core.Exceptions.DriveReauthenticationRequiredException>(
            () => driveService.UploadFileAsync(
                userId, Guid.NewGuid(), "My Route",
                new MemoryStream([1, 2, 3]), "gpx",
                CancellationToken.None));
    }

    // ── DownloadFileAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadFileAsync_RouteNotInDatabase_ThrowsDriveDownloadException()
    {
        // Arrange
        var userManagerMock = CreateUserManagerMock();
        var driveService = CreateDriveService(userManagerMock);
        var userId = "user-" + Guid.NewGuid();

        // No route seeded — RequireDriveFileIdAsync returns null

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BeenThere.Core.Exceptions.DriveDownloadException>(
            () => driveService.DownloadFileAsync(userId, Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("No Drive file found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadFileAsync_NoTokens_ThrowsDriveReauthenticationRequiredException()
    {
        // Arrange
        var userId = "user-" + Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var dbName = "download-test-" + Guid.NewGuid();
        var db = CreateInMemoryDbContext(dbName);

        // Seed a route with a driveFileId so the DB lookup succeeds
        var route = new Route
        {
            Id = routeId,
            UserId = userId,
            Name = "Test Route",
            Date = DateTimeOffset.UtcNow
        };
        route.DriveFileId = "fake-drive-id";
        db.Routes.Add(route);
        await db.SaveChangesAsync();

        var userManagerMock = CreateUserManagerMock();
        var user = new IdentityUser { Id = userId, UserName = "testuser" };
        userManagerMock.Setup(um => um.FindByIdAsync(userId)).ReturnsAsync(user);
        SetupBothTokensNull(userManagerMock, user);

        var driveService = CreateDriveService(userManagerMock, db);

        // Act & Assert
        await Assert.ThrowsAsync<BeenThere.Core.Exceptions.DriveReauthenticationRequiredException>(
            () => driveService.DownloadFileAsync(userId, routeId, CancellationToken.None));
    }
}

