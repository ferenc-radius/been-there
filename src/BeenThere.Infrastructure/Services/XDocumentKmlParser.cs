using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using System.Xml.Linq;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Parses KML files using System.Xml.Linq (ADR-0005).
/// Implements <see cref="IRouteFileParser"/> — handles the "kml" extension.
/// All telemetry (HR, cadence, power) is null — KML has no standard telemetry schema.
/// </summary>
public sealed class XDocumentKmlParser : IRouteFileParser
{
    public bool CanHandle(string fileExtension) =>
        fileExtension.Equals("kml", StringComparison.OrdinalIgnoreCase);
    public ParsedRoute Parse(Stream stream, string originalFilename)
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidDataException("KML file has no root element.");
        var ns = root.Name.Namespace;

        var name = root.Descendants(ns + "name").FirstOrDefault()?.Value?.Trim()
            ?? Path.GetFileNameWithoutExtension(originalFilename);

        var coordinatesEl = root.Descendants(ns + "LineString")
            .Select(ls => ls.Element(ns + "coordinates"))
            .FirstOrDefault(c => c != null);

        if (coordinatesEl == null)
        {
            return new ParsedRoute
            {
                Name = name,
                DistanceM = 0,
                ElevGainM = 0,
                Points = [],
            };
        }

        var points = ParseCoordinates(coordinatesEl.Value);
        var (distanceM, elevGainM) = ComputeStats(points);

        return new ParsedRoute
        {
            Name = name,
            DistanceM = distanceM,
            ElevGainM = elevGainM,
            Points = points,
        };
    }

    private static List<ParsedRoutePoint> ParseCoordinates(string raw)
    {
        var points = new List<ParsedRoutePoint>();
        var idx = 0;

        foreach (var token in raw.Split([' ', '\t', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                continue;
            }

            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat))
            {
                continue;
            }

            double? elevM = null;
            if (parts.Length >= 3 && double.TryParse(parts[2],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var alt))
            {
                elevM = alt;
            }

            points.Add(new ParsedRoutePoint
            {
                Idx = idx++,
                Longitude = lon,
                Latitude = lat,
                ElevationM = elevM,
            });
        }

        return points;
    }

    private static (double DistanceM, double ElevGainM) ComputeStats(List<ParsedRoutePoint> points)
    {
        double distanceM = 0;
        double elevGainM = 0;

        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1];
            var curr = points[i];

            distanceM += HaversineMetres(prev.Latitude, prev.Longitude,
                                         curr.Latitude, curr.Longitude);

            if (prev.ElevationM.HasValue && curr.ElevationM.HasValue)
            {
                var gain = curr.ElevationM.Value - prev.ElevationM.Value;
                if (gain > 0)
                {
                    elevGainM += gain;
                }
            }
        }

        return (distanceM, elevGainM);
    }

    private static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
