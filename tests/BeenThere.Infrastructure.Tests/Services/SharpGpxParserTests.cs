using BeenThere.Infrastructure.Services;
using System.Reflection;

#pragma warning disable CA1707

namespace BeenThere.Infrastructure.Tests.Services;

public sealed class SharpGpxParserTests
{
    private readonly SharpGpxParser _parser = new();

    [Fact]
    public void CanHandle_Gpx_ReturnsTrue() => Assert.True(_parser.CanHandle("gpx"));

    [Fact]
    public void CanHandle_Kml_ReturnsFalse() => Assert.False(_parser.CanHandle("kml"));

    [Fact]
    public void Parse_SimpleGpx_ReturnsParsedRoute()
    {
        using var stream = LoadFixture("simple.gpx");

        var result = _parser.Parse(stream, "simple.gpx");

        Assert.Equal("Test Hike", result.Name);
        Assert.Equal(3, result.Points.Count);
        Assert.NotNull(result.Date);
        Assert.True(result.DistanceM > 0);
    }

    [Fact]
    public void Parse_SimpleGpx_ReturnsCorrectCoordinates()
    {
        using var stream = LoadFixture("simple.gpx");

        var result = _parser.Parse(stream, "simple.gpx");

        var first = result.Points[0];
        Assert.Equal(51.5074, first.Latitude, precision: 4);
        Assert.Equal(-0.1278, first.Longitude, precision: 4);
        Assert.Equal(10.0, first.ElevationM);
    }

    [Fact]
    public void Parse_SimpleGpx_ExtractsTelemetry()
    {
        using var stream = LoadFixture("simple.gpx");

        var result = _parser.Parse(stream, "simple.gpx");

        Assert.Equal((short)120, result.Points[0].HrBpm);
        Assert.Equal((short)75, result.Points[0].CadenceRpm);
        Assert.Null(result.Points[0].PowerW);
    }

    [Fact]
    public void Parse_SimpleGpx_ComputesElevGain()
    {
        using var stream = LoadFixture("simple.gpx");

        var result = _parser.Parse(stream, "simple.gpx");

        // Elevation: 10 → 15 → 12. Gain = 5 (drop from 15→12 is ignored).
        Assert.Equal(5.0, result.ElevGainM, precision: 1);
    }

    [Fact]
    public void Parse_NoNameGpx_FallsBackToFilename()
    {
        using var stream = LoadFixture("no-name.gpx");

        var result = _parser.Parse(stream, "my-morning-walk.gpx");

        Assert.Equal("my-morning-walk", result.Name);
    }

    [Fact]
    public void Parse_NoNameGpx_PointsHaveNoTelemetry()
    {
        using var stream = LoadFixture("no-name.gpx");

        var result = _parser.Parse(stream, "no-name.gpx");

        Assert.All(result.Points, p =>
        {
            Assert.Null(p.HrBpm);
            Assert.Null(p.CadenceRpm);
            Assert.Null(p.PowerW);
        });
    }

    [Fact]
    public void Parse_IndexesPointsFromZero()
    {
        using var stream = LoadFixture("simple.gpx");

        var result = _parser.Parse(stream, "simple.gpx");

        for (var i = 0; i < result.Points.Count; i++)
            Assert.Equal(i, result.Points[i].Idx);
    }

    private static Stream LoadFixture(string filename)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        return asm.GetManifestResourceStream(resourceName)!;
    }
}
