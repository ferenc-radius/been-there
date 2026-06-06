using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Identity;

namespace BeenThere.Infrastructure.Drive;

public interface IGoogleDriveClientFactory
{
    Task<DriveService> CreateAsync(IdentityUser user);
}
