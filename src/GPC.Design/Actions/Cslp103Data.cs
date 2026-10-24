using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace GPC.Design.Actions;

public sealed record SpectrumTown(string Region, string Province, string Name, double Longitude, double Latitude);
internal sealed record HazardNode(int Id, double Longitude, double Latitude, double X, double Y, double[] Values);

/// <summary>Immutable numerical tables of the public CSLP Spettri-NTC 1.0.3 workbook.</summary>
public static class Cslp103Data
{
    private static readonly Lazy<HazardNode[]> nodes = new(ReadNodes);
    private static readonly Lazy<IReadOnlyList<SpectrumTown>> towns = new(ReadTowns);
    internal static HazardNode[] Nodes => nodes.Value;
    public static IReadOnlyList<SpectrumTown> Towns => towns.Value;
    private static Stream Open(string name) => new GZipStream(typeof(Cslp103Data).Assembly.GetManifestResourceStream("GPC.Design.Actions." + name)
        ?? throw new InvalidOperationException("Tabella CSLP assente: " + name), CompressionMode.Decompress);
    private static HazardNode[] ReadNodes()
    {
        using var reader = new StreamReader(Open("cslp103-grid.csv.gz"));
        var result = new List<HazardNode>();
        while (reader.ReadLine() is { } line)
        {
            var v = line.Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var values = new double[v.Length - 5];
            Array.Copy(v, 5, values, 0, values.Length);
            result.Add(new((int)v[0], v[1], v[2], v[3], v[4], values));
        }
        if (result.Count != 10751) throw new InvalidOperationException("Reticolo CSLP incompleto.");
        return result.ToArray();
    }
    private static IReadOnlyList<SpectrumTown> ReadTowns()
    {
        using var stream = Open("cslp103-towns.json.gz");
        using var document = JsonDocument.Parse(stream);
        return Array.AsReadOnly(document.RootElement.EnumerateArray().Select(r =>
            new SpectrumTown(r[0].GetString()!, r[1].GetString()!, r[2].GetString()!, r[3].GetDouble(), r[4].GetDouble())).ToArray());
    }
}
