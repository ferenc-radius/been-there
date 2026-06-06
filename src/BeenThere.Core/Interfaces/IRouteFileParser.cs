using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

/// <summary>
/// Parses a supported route file format into a normalised <see cref="ParsedRoute"/> (ADR-0005).
/// Implementations use the Strategy pattern: each implementation declares which extension(s) it
/// handles via <see cref="CanHandle"/>, allowing new formats to be added without modifying
/// any orchestration code (Open/Closed Principle).
/// </summary>
public interface IRouteFileParser
{
    /// <summary>Returns <c>true</c> when this parser can process files with the given lowercase extension.</summary>
    /// <param name="fileExtension">Lowercase extension without the leading dot, e.g. "gpx" or "kml".</param>
    bool CanHandle(string fileExtension);

    /// <summary>
    /// Parses the stream into a normalised route. Computes distance and elevation gain.
    /// </summary>
    /// <param name="stream">Readable file stream. Not disposed by the parser.</param>
    /// <param name="originalFilename">Used to derive a fallback route name when the file contains none.</param>
    ParsedRoute Parse(Stream stream, string originalFilename);
}
