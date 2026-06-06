using System.Security.Claims;
using BeenThere.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BeenThere.Infrastructure.Services;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
