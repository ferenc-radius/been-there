using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Drive;
using BeenThere.Infrastructure.Persistence;
using BeenThere.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BeenThere.Infrastructure.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeenThere.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Nominatim geocoding service
        services.Configure<NominatimOptions>(configuration.GetSection("Nominatim"));
        services.AddTransient<RateLimitHandler>();
        services.AddMemoryCache();
        services.AddHttpClient<Core.Interfaces.IGeocodingService, NominatimGeocodingService>(client =>
        {
            var baseUrl = configuration.GetValue<string>("Nominatim:BaseUrl") ?? "https://nominatim.openstreetmap.org/";
            client.BaseAddress = new Uri(baseUrl);
            // User-Agent is set by service from options when possible
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("Nominatim:TimeoutSeconds") ?? 10);
        })
        .AddHttpMessageHandler<RateLimitHandler>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IGoogleDriveClientFactory, GoogleDriveClientFactory>();
        services.AddScoped<IDriveService, DriveService>();
        services.AddScoped<IRouteFileParser, SharpGpxParser>();
        services.AddScoped<IRouteFileParser, XDocumentKmlParser>();
        services.AddScoped<IRouteAssembler, RouteAssembler>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IPreferencesService, PreferencesService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<IRouteSocialService, RouteSocialService>();
        services.AddSingleton<IDuplicateDetectionChannel, NullDuplicateDetectionChannel>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsql => npgsql.UseNetTopologySuite())
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
