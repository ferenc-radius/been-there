namespace BeenThere.Core.Interfaces;

public interface IPreferencesService
{
    Task<UserPreferencesDto> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateStickFigureAsync(string userId, string figureKey, CancellationToken cancellationToken = default);
}

public sealed record UserPreferencesDto(string StickFigure, string TileProvider);
