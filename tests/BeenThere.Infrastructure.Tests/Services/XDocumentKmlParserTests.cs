using BeenThere.Infrastructure.Services;
using System.Reflection;

#pragma warning disable CA1707

namespace BeenThere.Infrastructure.Tests.Services;

public sealed class XDocumentKmlParserTests
{
    private readonly XDocumentKmlParser _parser = new();

    [Fact]
    public void CanHandle_Kml_ReturnsTrue() => Assert.True(_parser.CanHandle("kml"));

    [Fact]
    public void CanHandle_Gpx_ReturnsFalse() => Assert.False(_parser.CanHandle("gpx"));

    [Fact]
    public void Parse_SimpleKml_ReturnsParsedRoute()
    {
        using var stream = LoadFixture("simple.kml");

        var result = _parser.Parse(stream, "simple.kml");

        Assert.Equal("Test Walk", result.Name);
        Assert.Equal(3, result.Points.Count);
        Assert.True(result.DistanceM > 0);
    }

    [Fact]
    public void Parse_SimpleKml_ReturnsCorrectCoordinates()
    {
        using var stream = LoadFixture("simple.kml");

        var result = _parser.Parse(stream, "simple.kml");

        var first = result.Points[0];
        Assert.Equal(51.5074, first.Latitude, precision: 4);
        Assert.Equal(-0.1278, first.Longitude, precision: 4);
        Assert.Equal(10.0, first.ElevationM);
    }

    [Fact]
    public void Parse_SimpleKml_HasNoTelemetry()
    {
        using var stream = LoadFixture("simple.kml");

        var result = _parser.Parse(stream, "simple.kml");

        Assert.All(result.Points, p =>
        {
            Assert.Null(p.HrBpm);
            Assert.Null(p.CadenceRpm);
            Assert.Null(p.PowerW);
        });
    }

    [Fact]
    public void Parse_SimpleKml_ComputesElevGain()
    {
        using var stream = LoadFixture("simple.kml");

        var result = _parser.Parse(stream, "simple.kml");

        // 10 → 15 → 12: gain = 5
        Assert.Equal(5.0, result.ElevGainM, precision: 1);
    }

    [Fact]
    public void Parse_NoLineStringKml_ReturnsEmptyPoints()
    {
        const string kml = """
            <?xml version="1.0"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document><name>Empty</name></Document>
            </kml>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(kml));

        var result = _parser.Parse(stream, "empty.kml");

        Assert.Equal("Empty", result.Name);
        Assert.Empty(result.Points);
    }

    [Fact]
    public void Parse_IndexesPointsFromZero()
    {
        using var stream = LoadFixture("simple.kml");

        var result = _parser.Parse(stream, "simple.kml");

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
