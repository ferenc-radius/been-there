using BeenThere.Core.Exceptions;
using BeenThere.Infrastructure.Drive;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

#pragma warning disable CA1707

namespace BeenThere.Infrastructure.Tests.Drive;

public class GoogleDriveClientFactoryTests
{
    private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
    private readonly GoogleDriveClientFactory _factory;

    public GoogleDriveClientFactoryTests()
    {
        var store = Mock.Of<IUserStore<IdentityUser>>();
        _mockUserManager = new Mock<UserManager<IdentityUser>>(
            store, null!, null!, null!, null!, null!, null!, null!, null!);

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["GOOGLE_CLIENT_ID"]).Returns("test-client-id");
        mockConfig.Setup(c => c["GOOGLE_CLIENT_SECRET"]).Returns("test-client-secret");

        _factory = new GoogleDriveClientFactory(
            _mockUserManager.Object,
            mockConfig.Object,
            Mock.Of<ILogger<GoogleDriveClientFactory>>());
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowDriveReauthenticationRequiredException_WhenBothTokensAbsent()
    {
        // Arrange
        var user = new IdentityUser { Id = "user-1" };
        _mockUserManager
            .Setup(um => um.GetAuthenticationTokenAsync(user, "Google", "refresh_token"))
            .ReturnsAsync((string?)null);
        _mockUserManager
            .Setup(um => um.GetAuthenticationTokenAsync(user, "Google", "access_token"))
            .ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DriveReauthenticationRequiredException>(
            () => _factory.CreateAsync(user));
    }
}
