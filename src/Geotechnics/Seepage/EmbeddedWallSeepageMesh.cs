using GPC.Model.Geotechnics.Seepage;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>Structured conforming triangles around a finite impermeable cutoff. Layers, water levels,
/// excavation surfaces and wall toe are mesh lines; rectangular cells are split with alternating diagonals.
/// Domain extent and bottom are engineering inputs, not inferred boundaries.</summary>
public static class EmbeddedWallSeepageMesh
{
    public static SeepageModel Create(EmbeddedWallSeepageDomain d)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        var values = new[] { d.LeftExtent, d.RightExtent, d.Bottom, d.WallTop, d.WallBottom, d.WallThickness, d.LeftGround, d.RightGround, d.LeftHead, d.RightHead, d.MaximumElementLength };
        if (values.Any(v => !UnsaturatedConductivity.Finite(v)) || d.WallThickness <= 0 || d.MaximumElementLength <= 0
            || d.LeftExtent <= d.WallThickness / 2 || d.RightExtent <= d.WallThickness / 2 || d.Bottom >= d.WallBottom
            || d.WallBottom >= Math.Min(d.LeftGround, d.RightGround) || d.WallTop < Math.Max(d.LeftGround, d.RightGround))
            throw new ArgumentException("Dominio idraulico non valido: la parete deve attraversare entrambi i terreni ed essere interna ai bordi laterali e inferiore.");
        var left = d.LeftLayers?.ToArray() ?? throw new ArgumentException("Strati idraulici sinistri mancanti.");
        var right = d.RightLayers?.ToArray() ?? throw new ArgumentException("Strati idraulici destri mancanti.");
        ValidateLayers(left, d.LeftGround, d.LeftHead, d.Bottom); ValidateLayers(right, d.RightGround, d.RightHead, d.Bottom);
        var materials = left.Concat(right).Select(l => l.Material).Distinct().ToArray();
        double half = d.WallThickness / 2;
        double[] xs = Subdivide(new[] { -d.LeftExtent, -half, 0, half, d.RightExtent }, d.MaximumElementLength);
        double maxGround = Math.Max(d.LeftGround, d.RightGround);
        double[] zs = Subdivide(new[] { d.Bottom, d.WallBottom, d.LeftGround, d.RightGround, d.LeftHead, d.RightHead }
            .Concat(left.Concat(right).SelectMany(l => new[] { l.Top, l.Bottom })).Where(z => z >= d.Bottom && z <= maxGround), d.MaximumElementLength);
        if ((long)xs.Length * zs.Length > 50000) throw new ArgumentException("Mesh idraulica oltre 50000 nodi: rivedere passo ed estensione del dominio.");
        var nodes = new List<SeepageNode>(); var indices = new Dictionary<(int, int), int>(); var triangles = new List<SeepageTriangle>();
        int Node(int i, int j)
        {
            if (!indices.TryGetValue((i, j), out int index)) { index = nodes.Count; indices.Add((i, j), index); nodes.Add(new(xs[i], zs[j])); }
            return index;
        }
        for (int j = 0; j < zs.Length - 1; j++) for (int i = 0; i < xs.Length - 1; i++)
        {
            double x = (xs[i] + xs[i + 1]) / 2, z = (zs[j] + zs[j + 1]) / 2;
            if (Math.Abs(x) < half && z > d.WallBottom || z > (x < 0 ? d.LeftGround : d.RightGround)) continue;
            var layers = x < 0 ? left : right;
            var material = layers.Single(l => z < l.Top && z > l.Bottom).Material;
            int m = Array.IndexOf(materials, material), a = Node(i, j), b = Node(i + 1, j), c = Node(i, j + 1), e = Node(i + 1, j + 1);
            if ((i + j) % 2 == 0) { triangles.Add(new(a, b, e, m)); triangles.Add(new(a, e, c, m)); }
            else { triangles.Add(new(a, b, c, m)); triangles.Add(new(b, e, c, m)); }
        }
        var heads = new Dictionary<int, double>(); var faces = new List<int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = nodes[i]; bool leftSide = p.X < 0;
            double ground = leftSide ? d.LeftGround : d.RightGround, water = leftSide ? d.LeftHead : d.RightHead;
            if (p.X == -d.LeftExtent || p.X == d.RightExtent) heads.Add(i, water);
            else if (Math.Abs(p.X) >= half && p.Z == ground)
            { if (water >= ground) heads.Add(i, water); else faces.Add(i); }
        }
        return new SeepageModel { CaseId = d.CaseId, Nodes = nodes.AsReadOnly(), Triangles = triangles.AsReadOnly(), Materials = Array.AsReadOnly(materials),
            Heads = heads.Select(h => new SeepageHead(h.Key, h.Value)).ToArray(), SeepageFaceNodes = faces.AsReadOnly(),
            InitialHeads = nodes.Select(p => d.LeftHead + (p.X + d.LeftExtent) / (d.LeftExtent + d.RightExtent) * (d.RightHead - d.LeftHead)).ToArray() };
    }
    private static void ValidateLayers(SeepageLayer[] layers, double ground, double water, double bottom)
    {
        if (layers.Length == 0 || layers.Any(l => l == null || l.Material == null) || layers[0].Top < ground || layers[layers.Length - 1].Bottom > bottom)
            throw new ArgumentException("Gli strati idraulici devono coprire il terreno fino al fondo del dominio.");
        double previous = layers[0].Top;
        foreach (var l in layers)
        {
            if (!UnsaturatedConductivity.Finite(l.Top) || !UnsaturatedConductivity.Finite(l.Bottom) || l.Top != previous || l.Top <= l.Bottom)
                throw new ArgumentException("Strati idraulici ordinati e contigui richiesti.");
            if (Math.Min(l.Top, ground) > water && l.Material.Unsaturated == null)
                throw new ArgumentException("Terreno sopra la falda: assegnare la legge non satura, senza estendere implicitamente il modello confinato.");
            previous = l.Bottom;
        }
    }
    private static double[] Subdivide(IEnumerable<double> source, double step)
    {
        var sorted = source.Distinct().OrderBy(x => x).ToArray(); var values = new List<double> { sorted[0] };
        for (int i = 1; i < sorted.Length; i++)
        {
            double count = Math.Ceiling((sorted[i] - sorted[i - 1]) / step);
            if (count > 50000) throw new ArgumentException("Passo idraulico troppo piccolo per il dominio.");
            for (int j = 1; j < count; j++) values.Add(sorted[i - 1] + (sorted[i] - sorted[i - 1]) * j / count);
            values.Add(sorted[i]);
        }
        return values.ToArray();
    }
}
