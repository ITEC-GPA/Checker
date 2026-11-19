using GPC.Design.Sections;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

/// <summary>
/// The DelaunayMesh meshes of the usual concrete sections and of the existing templates stay bit-identical.
/// Usual sections: the outlines that ANTHEA builds itself (SezioneCA: rectangle, circle, T, rectangle and circle with a hole), with the same
/// arithmetic. Templates: every <see cref="ConcreteTemplateKind"/> with the inputs of <see cref="ConcreteSectionTemplateChecks"/> and of the
/// separate stadium layout. For each case the mesh is the one the section solver integrates, <see cref="ReinforcedConcreteSection.Mesh"/> of
/// the section built from the shape as ANTHEA's CheckerSection does; for the templates the whole output of
/// <see cref="ConcreteSectionTemplates.Create"/> (outline, holes, bars, links, fibres, sizes, note) is fingerprinted too.
/// Fixtures/concrete-section-meshes.tsv was captured before the free section (Checker b357bc65, GPC.Design 0.47.0.1, GPCModel 5.54.0.0,
/// DelaunayMesh 2.0.0.14): a regression capture of the code before the change, not an independent expectation. It is never regenerated to
/// make the test pass. To capture again (only with the user's approval): GPC_SECTION_MESH_CAPTURE=&lt;file&gt;, once per runtime.
/// </summary>
[TestClass]
public sealed class ConcreteSectionMeshIdentityChecks
{
    private static string Runtime => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        .StartsWith(".NET Framework", StringComparison.Ordinal) ? "net472" : "net8.0";

    [TestMethod]
    public void UsualAndTemplateMeshesAreUnchanged()
    {
        var actual = Fingerprints().ToList();
        string? capture = Environment.GetEnvironmentVariable("GPC_SECTION_MESH_CAPTURE");
        if (!string.IsNullOrEmpty(capture))
        {
            File.WriteAllLines(capture, actual.Select(c => $"{Runtime}\t{c.Name}\t{c.Faces}\t{c.Mesh}\t{c.Template}"));
            Assert.Inconclusive("Capture written to " + capture);
        }
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "concrete-section-meshes.tsv");
        var expected = File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
            .Select(l => l.Split('\t')).Where(f => f[0] == Runtime).ToDictionary(f => f[1]);
        Assert.AreEqual(expected.Count, actual.Count, "number of cases for " + Runtime);
        var differences = new List<string>();
        foreach (var c in actual)
        {
            if (!expected.TryGetValue(c.Name, out var e)) { differences.Add(c.Name + ": not captured"); continue; }
            if (e[2] != c.Faces.ToString(System.Globalization.CultureInfo.InvariantCulture) || e[3] != c.Mesh) differences.Add(c.Name + ": mesh");
            if (e[4] != c.Template) differences.Add(c.Name + ": template output");
        }
        Assert.AreEqual(0, differences.Count, string.Join("; ", differences));
        Assert.IsTrue(actual.All(c => c.Faces > 0));
    }

    internal sealed record Fingerprint(string Name, int Faces, string Mesh, string Template);

    internal static IEnumerable<Fingerprint> Fingerprints()
    {
        foreach (var (name, shape) in UsualSections())
        {
            var mesh = new ReinforcedConcreteSection(shape, Concrete()).Mesh;
            yield return new(name, mesh.FacesCount, MeshDigest(mesh), "-");
        }
        foreach (var (name, input) in TemplateInputs())
        {
            var geometry = ConcreteSectionTemplates.Create(input);
            var mesh = new ReinforcedConcreteSection(geometry.Shape, Concrete()).Mesh;
            yield return new(name, mesh.FacesCount, MeshDigest(mesh), TemplateDigest(geometry));
        }
    }

    internal static ConcreteMaterial Concrete() => new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

    private static Polygon2d Polygon(IEnumerable<double[]> points) => new(points.Select(p => new Point2d(p[0], p[1])));

    /// <summary>The outlines of SezioneCA.Geometry and SezioneCA.BuildHole of ANTHEA, with the same expressions.</summary>
    internal static IEnumerable<(string Name, Shape2d Shape)> UsualSections()
    {
        double b = 600, h = 800;
        double[][] rectangle = [[-b / 2, -h / 2], [b / 2, -h / 2], [b / 2, h / 2], [-b / 2, h / 2]];
        yield return ("Rettangolare 600x800", new Shape2d(Polygon(rectangle), []));
        foreach (int sides in new[] { 32, 96 })
        {
            double radius = 500;
            var circle = Enumerable.Range(0, sides).Select(i => new[] { radius * Math.Cos(2 * Math.PI * i / sides), radius * Math.Sin(2 * Math.PI * i / sides) }).ToArray();
            yield return ($"Circolare D1000 {sides} lati", new Shape2d(Polygon(circle), []));
            double ri = 250;
            var hole = Enumerable.Range(0, sides).Select(i => new[] { ri * Math.Cos(-2 * Math.PI * i / sides), ri * Math.Sin(-2 * Math.PI * i / sides) }).ToArray();
            yield return ($"Circolare D1000 foro D500 {sides} lati", new Shape2d(Polygon(circle), [Polygon(hole)]));
        }
        double bi = 300, hi = 400;
        double[][] rectangleHole = [[-bi / 2, -hi / 2], [-bi / 2, hi / 2], [bi / 2, hi / 2], [bi / 2, -hi / 2]];
        yield return ("Rettangolare 600x800 foro 300x400", new Shape2d(Polygon(rectangle), [Polygon(rectangleHole)]));
        double bf = 1200, bw = 400, hf = 250, af = bf * hf, hw = h - hf, aw = bw * hw, yf = h / 2 - hf / 2, yw = -h / 2 + hw / 2;
        double centroidY = (af * yf + aw * yw) / (af + aw);
        double[][] raw = [[-bf / 2, h / 2], [bf / 2, h / 2], [bf / 2, h / 2 - hf], [bw / 2, h / 2 - hf], [bw / 2, -h / 2], [-bw / 2, -h / 2], [-bw / 2, h / 2 - hf], [-bf / 2, h / 2 - hf]];
        yield return ("A T 1200/400x800/250", new Shape2d(Polygon(raw.Select(p => new[] { p[0], p[1] - centroidY })), []));
    }

    internal static ConcreteTemplateInput TemplateInput(ConcreteTemplateKind kind) => new(kind,
        1000, 1200, 300, 300, 800, 250, 400, 500, 35, 20, 180, 24, 16, 10, 150);

    internal static IEnumerable<(string Name, ConcreteTemplateInput Input)> TemplateInputs()
    {
        foreach (var kind in new[] { ConcreteTemplateKind.I, ConcreteTemplateKind.C, ConcreteTemplateKind.L, ConcreteTemplateKind.HollowRectangle, ConcreteTemplateKind.Trapezoid })
            yield return (kind.ToString(), TemplateInput(kind));
        foreach (var kind in new[] { ConcreteTemplateKind.Ellipse, ConcreteTemplateKind.Stadium, ConcreteTemplateKind.HollowCircle })
            yield return (kind.ToString(), TemplateInput(kind) with { Height = kind == ConcreteTemplateKind.HollowCircle ? 1000 : 1600, InnerHeight = 400 });
        foreach (int sides in new[] { 3, 4, 6, 8, 12 })
            yield return ("RegularPolygon " + sides, TemplateInput(ConcreteTemplateKind.RegularPolygon) with { PolygonSides = sides });
        var stadium = new ConcreteTemplateInput(ConcreteTemplateKind.Stadium, 5000, 2500, 0, 0, 0, 0, 0, 0, 50, 20, 180, 24, 16, 12, 150) { Stadium = new(17, 32, 8, 25) };
        yield return ("Stadium separate", stadium);
        yield return ("Stadium separate vertical", stadium with { Width = 2500, Height = 5000 });
    }

    /// <summary>Vertices, faces (vertex ids) and face points of the mesh, as the solver reads them.</summary>
    internal static string MeshDigest(Mesh mesh)
    {
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory))
        {
            var vertices = mesh.GetVertices();
            writer.Write(vertices.Length);
            foreach (var v in vertices) { writer.Write(v.Point.X); writer.Write(v.Point.Y); writer.Write(v.Point.Z); }
            var faces = mesh.GetFaces();
            writer.Write(faces.Length);
            foreach (var face in faces)
            {
                writer.Write(face.A); writer.Write(face.B); writer.Write(face.C); writer.Write(face.D);
                foreach (var v in mesh.GetFaceVertices(face)) { writer.Write(v.Point.X); writer.Write(v.Point.Y); }
            }
        }
        return RuntimeCompatibility.Sha256Hex(memory.ToArray());
    }

    internal static string TemplateDigest(ConcreteTemplateGeometry g)
    {
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory))
        {
            void Points(IEnumerable<Point2d> points)
            {
                var list = points.ToList();
                writer.Write(list.Count);
                foreach (var p in list) { writer.Write(p.X); writer.Write(p.Y); }
            }
            Points(g.Shape.Fill2d.Points);
            var holes = g.Shape.Holes2d ?? [];
            writer.Write(holes.Length);
            foreach (var hole in holes) Points(hole.Points);
            writer.Write(g.Bars.Count);
            foreach (var bar in g.Bars) { writer.Write(bar.X); writer.Write(bar.Y); writer.Write(bar.Diameter); writer.Write(bar.Area); }
            writer.Write(g.Links.Count);
            foreach (var link in g.Links) { writer.Write(link.Name); Points(link.Points); }
            writer.Write(g.Fibers.Count);
            foreach (var fiber in g.Fibers) { writer.Write(fiber.X); writer.Write(fiber.Y); writer.Write(fiber.Area); }
            writer.Write(g.Width); writer.Write(g.Height); writer.Write(g.Area); writer.Write(g.ShearNote);
            writer.Write(g.ColumnBarPath is not null);
            if (g.ColumnBarPath is not null) Points(g.ColumnBarPath);
        }
        return RuntimeCompatibility.Sha256Hex(memory.ToArray());
    }
}
