using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Drive;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

#pragma warning disable CA1707

namespace BeenThere.Infrastructure.Tests.Services;

public class DriveServiceTests
{
    private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
    private readonly Mock<IGoogleDriveClientFactory> _mockClientFactory;
    private readonly DriveService _driveService;

    public DriveServiceTests()
    {
        var store = Mock.Of<IUserStore<IdentityUser>>();
        _mockUserManager = new Mock<UserManager<IdentityUser>>(
            store, null!, null!, null!, null!, null!, null!, null!, null!);
        _mockClientFactory = new Mock<IGoogleDriveClientFactory>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"DriveServiceTests_{Guid.NewGuid()}")
            .Options;
        var currentUser = new Mock<ICurrentUserService>();
        var db = new ApplicationDbContext(options, currentUser.Object);
        var logger = Mock.Of<ILogger<DriveService>>();

        _driveService = new DriveService(
            _mockUserManager.Object, db, _mockClientFactory.Object, logger);
    }

    [Fact]
    public async Task CreateUserFolderAsync_ShouldThrowDriveFolderCreationException_WhenUserNotFound()
    {
        // Arrange
        var userId = "test-user-id";
        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((IdentityUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DriveFolderCreationException>(
            () => _driveService.CreateUserFolderAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task UploadFileAsync_ShouldThrowDriveUploadException_WhenUserNotFound()
    {
        // Arrange
        var userId = "test-user-id";
        var routeId = Guid.NewGuid();
        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((IdentityUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DriveUploadException>(
            () => _driveService.UploadFileAsync(
                userId, routeId, "Test Route", new MemoryStream(), "gpx", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadFileAsync_ShouldThrowDriveDownloadException_WhenNoRouteInDatabase()
    {
        // Arrange — no route seeded; RequireDriveFileIdAsync throws immediately
        var userId = "test-user-id";
        var routeId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<DriveDownloadException>(
            () => _driveService.DownloadFileAsync(userId, routeId, CancellationToken.None));
    }
}
