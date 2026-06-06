using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Drive;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeenThere.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IGoogleDriveClientFactory, GoogleDriveClientFactory>();
        services.AddScoped<IDriveService, DriveService>();
        services.AddScoped<IRouteFileParser, SharpGpxParser>();
        services.AddScoped<IRouteFileParser, XDocumentKmlParser>();
        services.AddScoped<IRouteAssembler, RouteAssembler>();
        services.AddScoped<IImportService, ImportService>();
        services.AddSingleton<IDuplicateDetectionChannel, NullDuplicateDetectionChannel>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsql => npgsql.UseNetTopologySuite())
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
