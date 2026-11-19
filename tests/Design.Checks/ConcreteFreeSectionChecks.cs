using System.Text.Json;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Design.Sections;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

/// <summary>
/// Free concrete sections (<see cref="ConcreteSectionTemplates.CreateFree"/>): a polygon from a drawing gives the same geometry as the
/// usual rectangle and as the templates of the same shape, the properties of an irregular polygon are those of the independent oracle
/// (Oracles/free_section_polygon.py, exact fractions, Fixtures/free-section-independent.json), the bars are returned as given, and every
/// invalid datum is refused with its reason.
/// </summary>
[TestClass]
public sealed class ConcreteFreeSectionChecks
{
    private static Point2d P(double x, double y) => new(x, y);
    private static IReadOnlyList<Point2d> Poly(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => P(xy[2 * i], xy[2 * i + 1])).ToArray();
    private static ConcreteFreeBar Bar(double x, double y, double d) => new(x, y, d);

    private static Section Properties(Shape2d shape)
    {
        var section = new Section(shape);
        section.SetMechanicalProperties();
        return section;
    }

    private static void Close(double expected, double actual, double relative, string what)
        => Assert.AreEqual(expected, actual, relative * Math.Max(1, Math.Abs(expected)), what);

    private static double Signed(Polygon2d polygon) => polygon.GetSignedArea();

    // ------------------------------------------------------------------ rectangle against the usual rectangle

    /// <summary>The usual rectangle of ANTHEA (SezioneCA: outline centred, counterclockwise from the lower left corner) with the bars of
    /// UltimateSections.Symmetric of the Concrete suite: 300 × 500, 4Ø16 at (±104; ±204).</summary>
    private static Shape2d UsualRectangle() => new(new Polygon2d(new[] { P(-150, -250), P(150, -250), P(150, 250), P(-150, 250) }), []);
    private static readonly (double X, double Y)[] RectangleBars = [(-104, -204), (104, -204), (-104, 204), (104, 204)];

    /// <summary>The rectangle drawn at (1000; 2000), clockwise from the lower left corner and closed by repeating the first vertex.</summary>
    private static ConcreteFreeSectionInput DrawnRectangle(bool otherStart) => new(
        otherStart ? Poly(1300, 2500, 1000, 2500, 1000, 2000, 1300, 2000) : Poly(1000, 2000, 1000, 2500, 1300, 2500, 1300, 2000, 1000, 2000), [],
        RectangleBars.Select(b => Bar(1150 + b.X, 2250 + b.Y, 16)).ToArray());

    [TestMethod]
    public void RectangleAsPolygonMatchesTheUsualRectangle()
    {
        foreach (bool otherStart in new[] { false, true })
        {
            string id = otherStart ? "counterclockwise from the upper right corner" : "clockwise, closed";
            var g = ConcreteSectionTemplates.CreateFree(DrawnRectangle(otherStart));
            Close(300d * 500, g.Area, 1e-12, "area " + id);
            Assert.AreEqual(300, g.Width, 1e-9); Assert.AreEqual(500, g.Height, 1e-9);
            Assert.AreEqual(1150, g.InputCentroid!.X, 1e-9, "centroid x " + id);
            Assert.AreEqual(2250, g.InputCentroid.Y, 1e-9, "centroid y " + id);
            Assert.AreEqual(4, g.Shape.Fill2d.Points.Length);
            Assert.IsTrue(Signed(g.Shape.Fill2d) > 0, "outline counterclockwise " + id);
            Assert.AreEqual(0, g.Shape.Holes2d?.Length ?? 0);
            Assert.AreEqual(0, g.Links.Count);
            Assert.IsNull(g.ColumnBarPath);

            var free = Properties(g.Shape); var usual = Properties(UsualRectangle());
            Close(300d * 500 * 500 * 500 / 12, free.Jxx, 1e-12, "Jxx " + id);
            Close(500d * 300 * 300 * 300 / 12, free.Jyy, 1e-12, "Jyy " + id);
            Assert.AreEqual(0, free.Jxy, 1e-3, "Jxy " + id);
            Close(usual.Jxx, free.Jxx, 1e-12, "Jxx against the usual rectangle"); Close(usual.Jyy, free.Jyy, 1e-12, "Jyy against the usual rectangle");
            Assert.AreEqual(0, free.Centroid.X, 1e-9); Assert.AreEqual(0, free.Centroid.Y, 1e-9);

            // Fibres of the templates: the 48 × 48 cells of the bounding box, the whole rectangle here.
            Assert.AreEqual(48 * 48, g.Fibers.Count);
            Close(g.Area, g.Fibers.Sum(f => f.Area), 1e-12, "sum of the fibre areas " + id);

            Assert.AreEqual(4, g.Bars.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(RectangleBars[i].X, g.Bars[i].X, 1e-9); Assert.AreEqual(RectangleBars[i].Y, g.Bars[i].Y, 1e-9);
                Assert.AreEqual(16, g.Bars[i].Diameter); Assert.AreEqual(Math.PI * 16 * 16 / 4, g.Bars[i].Area);
            }
        }
        // Clockwise from the lower left corner becomes the outline of the usual rectangle itself, coordinate by coordinate: same mesh.
        var same = ConcreteSectionTemplates.CreateFree(DrawnRectangle(false));
        CollectionAssert.AreEqual(UsualRectangle().Fill2d.Points.Select(p => (p.X, p.Y)).ToArray(), same.Shape.Fill2d.Points.Select(p => (p.X, p.Y)).ToArray());
        Assert.AreEqual(ConcreteSectionMeshIdentityChecks.MeshDigest(new ReinforcedConcreteSection(UsualRectangle(), ConcreteSectionMeshIdentityChecks.Concrete()).Mesh),
            ConcreteSectionMeshIdentityChecks.MeshDigest(new ReinforcedConcreteSection(same.Shape, ConcreteSectionMeshIdentityChecks.Concrete()).Mesh));
    }

    private static ReinforcedConcreteSection Reinforced(Shape2d shape, IEnumerable<(double X, double Y, double D)> bars)
    {
        var section = new ReinforcedConcreteSection(shape, new ConcreteMaterialEN1992("C25", 25, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle));
        var steel = new SteelMaterial("B450C", 200000, 450, 450, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        foreach (var b in bars) section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(b.D, steel), new Point2d(b.X, b.Y)) });
        return section;
    }

    /// <summary>MxRd and MyRd (N mm) of the points of the domain at constant N, configured as ANTHEA's resistance (DomainPointContractTests of the
    /// Concrete suite): NTC 2018 (αcc 0.85, γc 1.5, γs 1.15), local axes (centroid, -X, -Y), plastic domain, non linear, 32 angles, iterative.</summary>
    private static double[][] MomentsAtConstantN(ReinforcedConcreteSection section, double axialKn)
    {
        var local = new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
        var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(local, SectionSolver.FailureAnalysisTypes.ConstantN,
            SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
        var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
        checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
        var force = new ResultBeamForces(axialKn * 1000, 0, 0, 0, 0, 0, new CoordinateSystem(local), 1).ToCoordinateSystemWithEccentricity(local);
        var domain = checker.GetFailureDomainResult().CalculateDomainConstantAxialForce(force);
        return domain.Domain.DomainPoints.Select(p => new[] { p.NRd, p.MxRd, p.MyRd }).ToArray();
    }

    [TestMethod]
    public void RectangleAsPolygonHasTheResistingMomentsOfTheUsualRectangle()
    {
        var usual = Reinforced(UsualRectangle(), RectangleBars.Select(b => (b.X, b.Y, 16d)));
        foreach (bool otherStart in new[] { false, true })
        {
            var g = ConcreteSectionTemplates.CreateFree(DrawnRectangle(otherStart));
            var free = Reinforced(g.Shape, g.Bars.Select(b => (b.X, b.Y, b.Diameter)));
            foreach (double n in new[] { -1500d, -500, 0, 200 })
            {
                var expected = MomentsAtConstantN(usual, n); var actual = MomentsAtConstantN(free, n);
                Assert.AreEqual(expected.Length, actual.Length);
                double scale = expected.Max(p => Math.Max(Math.Abs(p[1]), Math.Abs(p[2])));
                for (int i = 0; i < expected.Length; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        string what = $"N = {n} kN, point {i}, component {k}, {(otherStart ? "other start" : "same outline")}";
                        // Same outline coordinate by coordinate: the same numbers. Other start vertex: the same rectangle, another mesh;
                        // tolerance of the domain points of ANTHEA on N, max(1000 N; 1e-6 |N|) (DomainPointContractTests), 1e-6 of the largest moment.
                        if (!otherStart) Assert.AreEqual(expected[i][k], actual[i][k], 0, what);
                        else Assert.AreEqual(expected[i][k], actual[i][k], k == 0 ? Math.Max(1000, 1e-6 * Math.Abs(n * 1000)) : 1e-6 * scale, what);
                    }
            }
        }
    }

    // ------------------------------------------------------------------ against the templates

    private static void SameGeometry(ConcreteTemplateGeometry template, ConcreteTemplateGeometry free, string id)
    {
        Close(template.Area, free.Area, 1e-12, "area " + id);
        Assert.AreEqual(template.Width, free.Width, 1e-9); Assert.AreEqual(template.Height, free.Height, 1e-9);
        // The same vertices (as a set: start and direction may differ) and the same holes.
        static (double, double)[] Rounded(IEnumerable<Point2d> points) => points.Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))).OrderBy(p => p).ToArray();
        CollectionAssert.AreEqual(Rounded(template.Shape.Fill2d.Points), Rounded(free.Shape.Fill2d.Points), "outline " + id);
        var th = template.Shape.Holes2d ?? []; var fh = free.Shape.Holes2d ?? [];
        Assert.AreEqual(th.Length, fh.Length);
        for (int k = 0; k < th.Length; k++) CollectionAssert.AreEqual(Rounded(th[k].Points), Rounded(fh[k].Points), "hole " + id);
        var t = Properties(template.Shape); var f = Properties(free.Shape);
        Close(t.Jxx, f.Jxx, 1e-12, "Jxx " + id); Close(t.Jyy, f.Jyy, 1e-12, "Jyy " + id);
        Assert.AreEqual(t.Jxy, f.Jxy, 1e-12 * Math.Max(t.Jxx, t.Jyy), "Jxy " + id);
        Assert.AreEqual(0, f.Centroid.X, 1e-9); Assert.AreEqual(0, f.Centroid.Y, 1e-9);
        // The same sampling of the fibres.
        Assert.AreEqual(template.Fibers.Count, free.Fibers.Count, "fibres " + id);
        for (int i = 0; i < template.Fibers.Count; i++)
        {
            Assert.AreEqual(template.Fibers[i].X, free.Fibers[i].X, 1e-9); Assert.AreEqual(template.Fibers[i].Y, free.Fibers[i].Y, 1e-9);
            Close(template.Fibers[i].Area, free.Fibers[i].Area, 1e-12, "fibre area " + id);
        }
        // The DelaunayMesh mesh that the solver integrates covers the net area.
        var mesh = new ReinforcedConcreteSection(free.Shape, ConcreteSectionMeshIdentityChecks.Concrete()).Mesh;
        double meshArea = mesh.GetFaces().Sum(face => Math.Abs(new Polygon2d(mesh.GetFaceVertices(face).Select(v => new Point2d(v.Point.X, v.Point.Y))).GetSignedArea()));
        Close(free.Area, meshArea, 1e-9, "mesh area " + id);
    }

    /// <summary>The template bars moved into the drawing, as the application would read them from the same drawing.</summary>
    private static ConcreteFreeBar[] DrawnBars(ConcreteTemplateGeometry template, double x, double y) => template.Bars.Select(b => Bar(b.X + x, b.Y + y, b.Diameter)).ToArray();

    private static void SameBars(ConcreteTemplateGeometry template, ConcreteTemplateGeometry free)
    {
        Assert.AreEqual(template.Bars.Count, free.Bars.Count);
        for (int i = 0; i < template.Bars.Count; i++)
        {
            Assert.AreEqual(template.Bars[i].X, free.Bars[i].X, 1e-9); Assert.AreEqual(template.Bars[i].Y, free.Bars[i].Y, 1e-9);
            Assert.AreEqual(template.Bars[i].Diameter, free.Bars[i].Diameter); Close(template.Bars[i].Area, free.Bars[i].Area, 1e-15, "bar area");
        }
    }

    [TestMethod]
    public void LShapeAsPolygonMatchesTheTemplate()
    {
        // Template «A L» of ConcreteSectionTemplateChecks: legs 1000 × 300 (flange at the bottom) and 1200 × 300 (web on the left).
        var template = ConcreteSectionTemplates.Create(ConcreteSectionMeshIdentityChecks.TemplateInput(ConcreteTemplateKind.L));
        // Centroid of the L in the frame of the drawing (corner at the origin), by hand: flange 1000 × 300 at (500; 150), web 300 × 900 at (150; 750).
        double area = 1000d * 300 + 300d * 900, xg = (1000d * 300 * 500 + 300d * 900 * 150) / area, yg = (1000d * 300 * 150 + 300d * 900 * 750) / area;
        var free = ConcreteSectionTemplates.CreateFree(new(Poly(0, 0, 1000, 0, 1000, 300, 300, 300, 300, 1200, 0, 1200), null, DrawnBars(template, xg, yg)));
        Close(570000, free.Area, 1e-12, "area of the L");
        Assert.AreEqual(xg, free.InputCentroid!.X, 1e-9); Assert.AreEqual(yg, free.InputCentroid.Y, 1e-9);
        SameGeometry(template, free, "L");
        SameBars(template, free);

        // The polygon of the template itself, as its GPCModel class gives it (SectionL, clockwise from its corner; the template keeps that
        // direction): the free section turns it counterclockwise from the same first vertex, with the same geometry.
        var native = new SectionL(1000, 300, 1200, 300, "").Shape.Fill2d;
        var c = Properties(new Shape2d(native, [])).Centroid;
        var same = ConcreteSectionTemplates.CreateFree(new(native.Points, [], DrawnBars(template, c.X, c.Y)));
        Assert.IsTrue(Signed(same.Shape.Fill2d) > 0);
        Assert.AreEqual(native.Points[0].X - same.InputCentroid!.X, same.Shape.Fill2d.Points[0].X, 1e-9);
        Assert.AreEqual(native.Points[0].Y - same.InputCentroid.Y, same.Shape.Fill2d.Points[0].Y, 1e-9);
        SameGeometry(template, same, "L from the polygon of SectionL");
        SameBars(template, same);
    }

    [TestMethod]
    public void RectangleWithRectangularHoleMatchesTheHollowRectangle()
    {
        // Template HollowRectangle of ConcreteSectionTemplateChecks: 1000 × 1200 with a centred hole 400 × 500.
        var template = ConcreteSectionTemplates.Create(ConcreteSectionMeshIdentityChecks.TemplateInput(ConcreteTemplateKind.HollowRectangle));
        // Drawn with the lower left corner at (5000; -3000), outline counterclockwise, hole clockwise.
        double x0 = 5000, y0 = -3000;
        var free = ConcreteSectionTemplates.CreateFree(new(
            Poly(x0, y0, x0 + 1000, y0, x0 + 1000, y0 + 1200, x0, y0 + 1200),
            [Poly(x0 + 300, y0 + 350, x0 + 300, y0 + 850, x0 + 700, y0 + 850, x0 + 700, y0 + 350)],
            DrawnBars(template, x0 + 500, y0 + 600)));
        Close(1000d * 1200 - 400d * 500, free.Area, 1e-12, "net area");
        Assert.AreEqual(x0 + 500, free.InputCentroid!.X, 1e-9); Assert.AreEqual(y0 + 600, free.InputCentroid.Y, 1e-9);
        Assert.IsTrue(Signed(free.Shape.Holes2d![0]) > 0, "hole in the direction of the outline, as Shape2d keeps it");
        var p = Properties(free.Shape);
        Close((1000d * 1200 * 1200 * 1200 - 400d * 500 * 500 * 500) / 12, p.Jxx, 1e-12, "Jxx by hand");
        Close((1200d * 1000 * 1000 * 1000 - 500d * 400 * 400 * 400) / 12, p.Jyy, 1e-12, "Jyy by hand");
        SameGeometry(template, free, "hollow rectangle");
        SameBars(template, free);
    }

    // ------------------------------------------------------------------ irregular polygon against the independent oracle

    [TestMethod]
    public void IrregularPolygonHasTheIndependentProperties()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "free-section-independent.json")));
        // Bars in the drawing, clear of the sides and of the hole (checked by the library).
        ConcreteFreeBar[] bars = [Bar(75, 40, 20), Bar(650, 40, 16), Bar(400, 550, 25), Bar(250, 500, 12)];
        foreach (var c in json.RootElement.GetProperty("cases").EnumerateObject())
        {
            var outline = c.Value.GetProperty("outline").EnumerateArray().Select(v => P(v[0].GetDouble(), v[1].GetDouble())).ToArray();
            var holes = c.Value.GetProperty("holes").EnumerateArray().Select(h => (IReadOnlyList<Point2d>)h.EnumerateArray().Select(v => P(v[0].GetDouble(), v[1].GetDouble())).ToArray()).ToArray();
            var values = c.Value.GetProperty("values");
            double V(string key) => values.GetProperty(key).GetDouble();
            // The oracle outline is counterclockwise: once as it is, once clockwise from the same first vertex.
            foreach (bool reversed in new[] { false, true })
            {
                string id = c.Name + (reversed ? " clockwise" : "");
                var given = reversed ? new[] { outline[0] }.Concat(outline.Skip(1).Reverse()).ToArray() : outline;
                var g = ConcreteSectionTemplates.CreateFree(new(given, holes, bars));
                Close(V("area"), g.Area, 1e-12, "area " + id);
                Assert.AreEqual(V("xG"), g.InputCentroid!.X, 1e-9, "xG " + id);
                Assert.AreEqual(V("yG"), g.InputCentroid.Y, 1e-9, "yG " + id);
                var p = Properties(g.Shape);
                Close(V("area"), p.Area, 1e-12, "area of the GPC section " + id);
                Close(V("Jxx"), p.Jxx, 1e-12, "Jxx " + id);
                Close(V("Jyy"), p.Jyy, 1e-12, "Jyy " + id);
                Close(V("Jxy"), p.Jxy, 1e-12, "Jxy " + id);
                Assert.AreEqual(0, p.Centroid.X, 1e-9); Assert.AreEqual(0, p.Centroid.Y, 1e-9);
                // The vertices are those given, moved to the centroid, counterclockwise from the first one.
                Assert.IsTrue(Signed(g.Shape.Fill2d) > 0);
                Assert.AreEqual(outline[0].X - V("xG"), g.Shape.Fill2d.Points[0].X, 1e-9); Assert.AreEqual(outline[0].Y - V("yG"), g.Shape.Fill2d.Points[0].Y, 1e-9);
                for (int i = 0; i < outline.Length; i++)
                {
                    Assert.AreEqual(outline[i].X - V("xG"), g.Shape.Fill2d.Points[i].X, 1e-9); Assert.AreEqual(outline[i].Y - V("yG"), g.Shape.Fill2d.Points[i].Y, 1e-9);
                }
                // Bars: the positions given, moved by the independent centroid; area π Ø² / 4.
                Assert.AreEqual(bars.Length, g.Bars.Count);
                for (int i = 0; i < bars.Length; i++)
                {
                    Assert.AreEqual(bars[i].X - V("xG"), g.Bars[i].X, 1e-9); Assert.AreEqual(bars[i].Y - V("yG"), g.Bars[i].Y, 1e-9);
                    Assert.AreEqual(bars[i].Diameter, g.Bars[i].Diameter);
                    Assert.AreEqual(Math.PI * bars[i].Diameter * bars[i].Diameter / 4, g.Bars[i].Area);
                }
                // Fibres: the sampling of the templates, every one in the concrete; the DelaunayMesh mesh covers the independent area.
                double dx = g.Width / 48, dy = g.Height / 48;
                Assert.IsTrue(g.Fibers.Count > 0);
                foreach (var f in g.Fibers)
                {
                    var point = P(f.X, f.Y);
                    Assert.IsTrue(g.Shape.Fill2d.IsPointInside(point, 0) && !(g.Shape.Holes2d ?? []).Any(h => h.IsPointInside(point, 0)));
                    Close(dx * dy, f.Area, 1e-12, "fibre area");
                }
                var mesh = new ReinforcedConcreteSection(g.Shape, ConcreteSectionMeshIdentityChecks.Concrete()).Mesh;
                double meshArea = mesh.GetFaces().Sum(face => Math.Abs(new Polygon2d(mesh.GetFaceVertices(face).Select(v => new Point2d(v.Point.X, v.Point.Y))).GetSignedArea()));
                Close(V("area"), meshArea, 1e-9, "mesh area " + id);
            }
        }
    }

    // ------------------------------------------------------------------ bars

    private static readonly IReadOnlyList<Point2d> Square = Poly(0, 0, 400, 0, 400, 400, 0, 400);
    private static readonly IReadOnlyList<Point2d> SquareHole = Poly(150, 150, 250, 150, 250, 250, 150, 250);

    private static string Refusal(IReadOnlyList<Point2d>? outline, IReadOnlyList<IReadOnlyList<Point2d>>? holes, IReadOnlyList<ConcreteFreeBar>? bars)
    {
        var error = Assert.ThrowsException<ArgumentException>(() => ConcreteSectionTemplates.CreateFree(new(outline!, holes, bars!)));
        return error.Message;
    }

    private static void Refused(string expected, IReadOnlyList<Point2d>? outline, IReadOnlyList<IReadOnlyList<Point2d>>? holes = null, IReadOnlyList<ConcreteFreeBar>? bars = null)
    {
        string message = Refusal(outline, holes, bars ?? [Bar(50, 50, 20)]);
        StringAssert.Contains(message, expected);
    }

    [TestMethod]
    public void BarsAreReturnedAsGivenOrRefused()
    {
        // Accepted: a bar tangent to the outline (cover 0, the limit of the application) and one tangent to the hole.
        var g = ConcreteSectionTemplates.CreateFree(new(Square, [SquareHole], [Bar(10, 200, 20), Bar(200, 140, 20), Bar(390.5, 390.5, 16)]));
        Assert.AreEqual(3, g.Bars.Count);
        Assert.AreEqual(10 - 200, g.Bars[0].X, 1e-9); Assert.AreEqual(0, g.Bars[0].Y, 1e-9);
        Assert.AreEqual(Math.PI * 100, g.Bars[0].Area); Assert.AreEqual(Math.PI * 64, g.Bars[2].Area);

        Refused("Inserire almeno una barra", Square, null, []);
        Refused("La barra 2 (x = 450, y = 200 mm, Ø 20) è fuori dal contorno della sezione", Square, null, [Bar(50, 50, 20), Bar(450, 200, 20)]);
        Refused("La barra 1 (x = 200, y = 200 mm, Ø 20) cade nel foro 1", Square, [SquareHole], [Bar(200, 200, 20)]);
        Refused("La barra 1 (x = 5, y = 200 mm, Ø 20) interseca il contorno della sezione", Square, null, [Bar(5, 200, 20)]);
        Refused("La barra 1 (x = 200, y = 145 mm, Ø 20) interseca il foro 1", Square, [SquareHole], [Bar(200, 145, 20)]);
        Refused("Barra 1: il diametro deve essere positivo", Square, null, [Bar(50, 50, 0)]);
        Refused("Barra 1: il diametro deve essere positivo", Square, null, [Bar(50, 50, -16)]);
        Refused("Barra 1: il diametro deve essere positivo", Square, null, [Bar(50, 50, double.NaN)]);
        Refused("Barra 2: coordinate non finite", Square, null, [Bar(50, 50, 20), Bar(double.PositiveInfinity, 50, 20)]);
        Refused("Le barre 1 e 2 si sovrappongono", Square, null, [Bar(50, 50, 20), Bar(70, 50, 20)]);
        Refused("al massimo 2000", Square, null, Enumerable.Range(0, 2001).Select(i => Bar(1 + i % 45 * 8.8, 1 + i / 45 * 8.8, 1)).ToArray());
    }

    // ------------------------------------------------------------------ polygons

    [TestMethod]
    public void InvalidPolygonsAndHolesAreRefused()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ConcreteSectionTemplates.CreateFree(null!));
        Refused("Contorno: vertici mancanti", null);
        Refused("Contorno: servono almeno 3 vertici distinti (ne sono dati 2)", Poly(0, 0, 400, 0));
        Refused("Contorno: servono almeno 3 vertici distinti (ne sono dati 2)", Poly(0, 0, 400, 0, 0, 0));
        Refused("Contorno: coordinate non finite al vertice 2", Poly(0, 0, double.NaN, 0, 400, 400));
        Refused("Contorno: il lato fra i vertici 2 e 3 ha lunghezza nulla", Poly(0, 0, 400, 0, 400, 0, 400, 400, 0, 400));
        Refused("Contorno: area nulla (vertici allineati)", Poly(0, 0, 200, 0, 400, 0));
        Refused("Contorno autointersecante: i lati 1 e 3 si incrociano o si toccano", Poly(0, 0, 400, 400, 400, 0, 0, 400));
        // The vertex 3 touches the vertex 6 (an hourglass closed on itself).
        Refused("Contorno autointersecante", Poly(0, 0, 400, 0, 200, 200, 400, 400, 0, 400, 200, 200));
        // The vertex 5 runs back over the side 4.
        Refused("Contorno: i lati 4 e 5 si sovrappongono", Poly(0, 0, 400, 0, 400, 400, 0, 400, 0, 500));
        Refused("al massimo 5000", Enumerable.Range(0, 5001).Select(i => P(1000 * Math.Cos(2 * Math.PI * i / 5001), 1000 * Math.Sin(2 * Math.PI * i / 5001))).ToArray());

        Refused("Foro 1: servono almeno 3 vertici distinti", Square, [Poly(100, 100, 200, 100)]);
        Refused("Foro 1 autointersecante", Square, [Poly(100, 100, 300, 300, 300, 100, 100, 300)], [Bar(50, 50, 20)]);
        Refused("Il foro 1 è esterno al contorno della sezione", Square, [Poly(500, 500, 600, 500, 600, 600, 500, 600)]);
        Refused("Il foro 1 è esterno al contorno della sezione", Poly(100, 100, 300, 100, 300, 300, 100, 300), [Poly(0, 0, 400, 0, 400, 400, 0, 400)], [Bar(200, 200, 20)]);
        Refused("Il foro 1 tocca o attraversa il contorno della sezione", Square, [Poly(300, 100, 500, 100, 500, 300, 300, 300)]);
        Refused("Il foro 2 tocca o attraversa il contorno della sezione", Square, [SquareHole, Poly(300, 0, 350, 100, 300, 100)]);
        Refused("I fori 1 e 2 si toccano o si sovrappongono", Square, [SquareHole, Poly(200, 200, 300, 200, 300, 300, 200, 300)]);
        Refused("I fori 1 e 2 si toccano o si sovrappongono", Square, [SquareHole, Poly(250, 250, 300, 250, 300, 300)]);
        Refused("I fori 1 e 2 si sovrappongono: uno contiene l'altro", Square, [Poly(100, 100, 300, 100, 300, 300, 100, 300), SquareHole]);
    }

    [TestMethod]
    public void DirectionAndClosingVertexDoNotChangeTheGeometry()
    {
        ConcreteFreeBar[] bars = [Bar(50, 50, 20), Bar(350, 350, 20)];
        var reference = ConcreteSectionTemplates.CreateFree(new(Square, [SquareHole], bars));
        foreach (var (outline, hole) in new[] {
            (Poly(0, 0, 400, 0, 400, 400, 0, 400, 0, 0), SquareHole),
            (Poly(0, 0, 0, 400, 400, 400, 400, 0), Poly(150, 150, 150, 250, 250, 250, 250, 150, 150, 150)) })
        {
            var g = ConcreteSectionTemplates.CreateFree(new(outline, [hole], bars));
            CollectionAssert.AreEqual(reference.Shape.Fill2d.Points.Select(p => (p.X, p.Y)).ToArray(), g.Shape.Fill2d.Points.Select(p => (p.X, p.Y)).ToArray());
            CollectionAssert.AreEqual(reference.Shape.Holes2d![0].Points.Select(p => (p.X, p.Y)).ToArray(), g.Shape.Holes2d![0].Points.Select(p => (p.X, p.Y)).ToArray());
            CollectionAssert.AreEqual(reference.Fibers.ToArray(), g.Fibers.ToArray());
            CollectionAssert.AreEqual(reference.Bars.ToArray(), g.Bars.ToArray());
            Assert.AreEqual(reference.Area, g.Area);
        }
        // The input is not changed and its points are not shared with the result.
        Assert.AreEqual(0, Square[0].X); Assert.AreEqual(0, Square[0].Y);
        Assert.IsFalse(reference.Shape.Fill2d.Points.Any(p => Square.Any(q => ReferenceEquals(p, q))));
    }
}
