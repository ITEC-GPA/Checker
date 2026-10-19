using GPC.Model.Geotechnics.Seepage;

namespace GPC.Checkers.Geotechnics.Seepage;

/// <summary>Owned snapshot and precomputed linear triangle geometry. No mesh generation or Gmsh dependency.</summary>
internal sealed class SeepageMesh
{
    internal SeepageNode[] Nodes = Array.Empty<SeepageNode>();
    internal Element[] Elements = Array.Empty<Element>();
    internal SeepageHead[] Heads = Array.Empty<SeepageHead>();
    internal int[] Faces = Array.Empty<int>();
    internal double[] Initial = Array.Empty<double>(), Loads = Array.Empty<double>();
    internal string CaseId = "";
    internal double SpecifiedInflow, SpecifiedOutflow;
    internal sealed class Element
    {
        internal int[] Nodes = Array.Empty<int>();
        internal double[] X = Array.Empty<double>(), Z = Array.Empty<double>(), Matrix = Array.Empty<double>();
        internal SeepageMaterial Material = null!;
    }
    internal static SeepageMesh Create(SeepageModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        var m = new SeepageMesh { CaseId = model.CaseId, Nodes = model.Nodes?.ToArray() ?? throw new ArgumentException("Nodi mancanti."),
            Heads = model.Heads?.ToArray() ?? throw new ArgumentException("Carichi imposti mancanti."),
            Faces = model.SeepageFaceNodes?.ToArray() ?? throw new ArgumentException("Nodi drenanti mancanti."),
            Initial = model.InitialHeads?.ToArray() ?? throw new ArgumentException("Stato iniziale mancante.") };
        var triangles = model.Triangles?.ToArray() ?? throw new ArgumentException("Triangoli mancanti.");
        var materials = model.Materials?.ToArray() ?? throw new ArgumentException("Materiali mancanti.");
        var fluxes = model.Fluxes?.ToArray() ?? throw new ArgumentException("Flussi mancanti.");
        int n = m.Nodes.Length;
        if (n < 3 || n > 50000 || triangles.Length == 0 || triangles.Length > 100000) throw new ArgumentException("Dimensioni della mesh fuori campo (3–50000 nodi, massimo 100000 triangoli).");
        foreach (var p in m.Nodes) if (!Finite(p.X) || !Finite(p.Z)) throw new ArgumentException("Coordinate non finite.");
        foreach (var p in materials)
        {
            if (p == null || !Finite(p.Kxx) || !Finite(p.Kzz) || !Finite(p.Kxz) || p.Kxx <= 0 || p.Kzz <= 0
                || Math.Abs(p.Kxz / Math.Sqrt(p.Kxx) / Math.Sqrt(p.Kzz)) >= 1 || string.IsNullOrWhiteSpace(p.Source))
                throw new ArgumentException("Assegnare un tensore di permeabilità positivo definito e la fonte.");
            if (p.Unsaturated != null) UnsaturatedConductivity.At(p.Unsaturated, 0);
        }
        var edges = new Dictionary<(int, int), int>(); var unique = new HashSet<(int, int, int)>();
        int[] parent = Enumerable.Range(0, n).ToArray(); var used = new bool[n];
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Index(int i) { if (i < 0 || i >= n) throw new ArgumentException("Indice di nodo fuori campo."); }
        m.Elements = triangles.Select(t =>
        {
            Index(t.A); Index(t.B); Index(t.C); int[] ids = { t.A, t.B, t.C }; var sorted = ids.OrderBy(i => i).ToArray();
            if (!unique.Add((sorted[0], sorted[1], sorted[2]))) throw new ArgumentException("Triangolo duplicato.");
            if (t.Material < 0 || t.Material >= materials.Length) throw new ArgumentException("Materiale del triangolo assente.");
            var a = m.Nodes[t.A]; var b = m.Nodes[t.B]; var c = m.Nodes[t.C];
            double cross = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
            double size = Math.Max(Math.Abs(b.X - a.X) + Math.Abs(b.Z - a.Z), Math.Abs(c.X - a.X) + Math.Abs(c.Z - a.Z));
            if (!Finite(cross) || Math.Abs(cross) <= 1e-12 * size * size || size == 0) throw new ArgumentException("Triangolo degenere o troppo distorto.");
            var e = new Element { Nodes = ids, X = new[] { b.Z - c.Z, c.Z - a.Z, a.Z - b.Z }.Select(v => v / cross).ToArray(),
                Z = new[] { c.X - b.X, a.X - c.X, b.X - a.X }.Select(v => v / cross).ToArray(), Material = materials[t.Material], Matrix = new double[9] };
            for (int i = 0; i < 3; i++)
            {
                used[ids[i]] = true; parent[Find(ids[i])] = Find(ids[0]);
                var edge = Edge(ids[i], ids[(i + 1) % 3]); edges[edge] = edges.TryGetValue(edge, out int count) ? count + 1 : 1;
                for (int j = 0; j < 3; j++) e.Matrix[3 * i + j] = Math.Abs(cross) / 2 *
                    (e.X[i] * (e.Material.Kxx * e.X[j] + e.Material.Kxz * e.Z[j]) + e.Z[i] * (e.Material.Kxz * e.X[j] + e.Material.Kzz * e.Z[j]));
            }
            return e;
        }).ToArray();
        if (used.Any(u => !u) || edges.Values.Any(c => c > 2)) throw new ArgumentException("Mesh con nodi isolati o bordo non conforme.");
        var headIds = new HashSet<int>();
        foreach (var h in m.Heads) { Index(h.Node); if (!headIds.Add(h.Node) || !Finite(h.Head)) throw new ArgumentException("Carico imposto duplicato o non finito."); }
        var anchored = new HashSet<int>(m.Heads.Select(h => Find(h.Node)));
        if (Enumerable.Range(0, n).Any(i => !anchored.Contains(Find(i)))) throw new ArgumentException("Ogni dominio connesso richiede almeno un carico piezometrico imposto.");
        var boundary = new HashSet<int>(edges.Where(e => e.Value == 1).SelectMany(e => new[] { e.Key.Item1, e.Key.Item2 }));
        var faces = new HashSet<int>();
        foreach (int i in m.Faces) if (!boundary.Contains(i) || headIds.Contains(i) || !faces.Add(i)) throw new ArgumentException("Nodo drenante non esterno, duplicato o con carico imposto.");
        m.Loads = new double[n]; var loaded = new HashSet<(int, int)>();
        foreach (var f in fluxes)
        {
            Index(f.A); Index(f.B); var edge = Edge(f.A, f.B);
            if (!edges.TryGetValue(edge, out int count) || count != 1 || !loaded.Add(edge) || !Finite(f.Inflow)
                || faces.Contains(f.A) || faces.Contains(f.B)) throw new ArgumentException("Flusso su bordo interno, duplicato o drenante.");
            var a = m.Nodes[f.A]; var b = m.Nodes[f.B]; double q = f.Inflow * Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
            m.Loads[f.A] += q / 2; m.Loads[f.B] += q / 2;
            if (q >= 0) m.SpecifiedInflow += q; else m.SpecifiedOutflow -= q;
        }
        if (m.Initial.Length != 0 && (m.Initial.Length != n || m.Initial.Any(v => !Finite(v)))) throw new ArgumentException("Carichi iniziali incompleti o non finiti.");
        return m;
    }
    private static (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);
    private static bool Finite(double v) => UnsaturatedConductivity.Finite(v);
}
