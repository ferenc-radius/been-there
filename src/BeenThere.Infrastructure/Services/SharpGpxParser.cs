using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using SharpGPX;
using SharpGPX.GPX1_1;
using System.Xml.Linq;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Parses GPX 1.0/1.1 files using BlueToque.SharpGPX (ADR-0005).
/// Implements <see cref="IRouteFileParser"/> — handles the "gpx" extension.
/// Extracts Garmin TrackPointExtension telemetry (HR, cadence, power) when present.
/// </summary>
public sealed class SharpGpxParser : IRouteFileParser
{
    private static readonly XNamespace GarminTpe =
        "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    public bool CanHandle(string fileExtension) =>
        fileExtension.Equals("gpx", StringComparison.OrdinalIgnoreCase);

    public ParsedRoute Parse(Stream stream, string originalFilename)
    {
        var gpx = GpxClass.FromStream(stream);

        var name = gpx.Metadata?.name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            var firstTrackName = gpx.Tracks?.FirstOrDefault()?.name?.Trim();
            name = string.IsNullOrWhiteSpace(firstTrackName)
                ? Path.GetFileNameWithoutExtension(originalFilename)
                : firstTrackName;
        }

        DateTimeOffset? date = null;
        var points = new List<ParsedRoutePoint>();
        var idx = 0;

        foreach (var track in gpx.Tracks ?? [])
        {
            foreach (var seg in track.trkseg ?? [])
            {
                foreach (var pt in seg.trkpt ?? [])
                {
                    var recordedAt = ParseTime(pt);
                    if (date == null && recordedAt != null)
                    {
                        date = recordedAt;
                    }

                    points.Add(new ParsedRoutePoint
                    {
                        Idx = idx++,
                        Longitude = (double)pt.lon,
                        Latitude = (double)pt.lat,
                        ElevationM = pt.eleSpecified ? (double?)pt.ele : null,
                        RecordedAt = recordedAt,
                        HrBpm = ExtractHr(pt),
                        CadenceRpm = ExtractCadence(pt),
                        PowerW = ExtractPower(pt),
                    });
                }
            }
        }

        var (distanceM, elevGainM) = ComputeStats(points);

        return new ParsedRoute
        {
            Name = name,
            Date = date,
            DistanceM = distanceM,
            ElevGainM = elevGainM,
            Points = points,
        };
    }

    private static DateTimeOffset? ParseTime(wptType pt)
    {
        if (!pt.timeSpecified)
        {
            return null;
        }

        return new DateTimeOffset(pt.time, TimeSpan.Zero);
    }

    private static short? ExtractHr(wptType pt)
    {
        var ext = GetTpeExtension(pt);
        if (ext == null)
        {
            return null;
        }

        var hr = ext.Element(GarminTpe + "hr");
        return hr != null && short.TryParse(hr.Value, out var v) ? v : null;
    }

    private static short? ExtractCadence(wptType pt)
    {
        var ext = GetTpeExtension(pt);
        if (ext == null)
        {
            return null;
        }

        var cad = ext.Element(GarminTpe + "cad");
        return cad != null && short.TryParse(cad.Value, out var v) ? v : null;
    }

    private static short? ExtractPower(wptType pt)
    {
        if (pt.extensions?.Any == null)
        {
            return null;
        }

        foreach (var node in pt.extensions.Any)
        {
            if (node.LocalName == "power" && short.TryParse(node.InnerText, out var v))
            {
                return v;
            }
        }
        return null;
    }

    private static XElement? GetTpeExtension(wptType pt)
    {
        if (pt.extensions?.Any == null)
        {
            return null;
        }

        foreach (var node in pt.extensions.Any)
        {
            if (node.LocalName == "TrackPointExtension")
            {
                try { return XElement.Parse(node.OuterXml); }
                catch { return null; }
            }
        }
        return null;
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
