using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Slopes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>
/// General geotechnics moved from ANTHEA (Anthea.Calculations.Geotechnics: BishopSolver, SlopeGeometry, SlopeStability, FoundationSettlement,
/// NewmarkSliding, ShallowFoundationSeismic; sources of commit fe4652c, captured by the harness at dadea50). Every legacy output is reproduced
/// after the conversion to the units of Model.
/// </summary>
[TestClass]
public class GeneralGeotechnicsMigrationTests
{
    [TestMethod]
    public void LegacyBishopSolutionsAreReproduced()
    {
        int ok = 0, none = 0, errors = 0;
        foreach (var row in Rows("geotechnics-bishop.csv"))
        {
            var c = row.Split(';'); string id = "Bishop " + c[0];
            double gammaR = D(c[1]); double x = 0;
            var slices = c[2].Length == 0 ? new SlopeSlice[0] : c[2].Split('|').Select((s, i) =>
            {
                var p = s.Split(':').Select(D).ToArray(); double b = p[4] * M;
                var slice = new SlopeSlice(i + 1, x, x + b, 0, M, p[0] * Deg, "S", p[5], 0, x + b / 2, .5 * M, 0, 0, p[3] * KPa, p[1] * Deg, p[2] * KPa, p[5], p[6]);
                x += b; return slice;
            }).ToArray();
            var circle = new SlipCircle(0, 10 * M, 10 * M, -3 * M, 6 * M);
            if (c[3].StartsWith("error"))
            {
                Assert.AreEqual("error:ArgumentException", c[3], id); Assert.ThrowsException<ArgumentException>(() => BishopSolver.Solve(circle, slices, gammaR), id); errors++; continue;
            }
            var r = BishopSolver.Solve(circle, slices, gammaR);
            if (c[3] == "null") { Assert.IsNull(r, id); none++; continue; }
            Assert.IsNotNull(r, id);
            Close(D(c[4]), r.Factor, id + " F", 1e-9); Close(D(c[5]), r.Ratio, id + " ratio", 1e-9);
            Assert.AreEqual(int.Parse(c[6]), r.Iterations, id + " iterations");
            Close(D(c[7]), r.Residual, id + " residual", 0, 1e-9 * Math.Max(1, r.Factor * r.Driving));
            Close(D(c[8]), r.Driving, id + " driving", 1e-9, 1e-9); Close(D(c[9]), r.Resistance, id + " resistance", 1e-9, 1e-9);
            var expected = c[10].Split('|');
            Assert.AreEqual(expected.Length, r.Slices.Count, id);
            for (int i = 0; i < expected.Length; i++)
            {
                var e = expected[i].Split(':').Select(D).ToArray(); var s = r.Slices[i];
                Close(e[0], s.NormalEffective, id + " N' " + i, 1e-8, 1e-8); Close(e[1], s.Resistance, id + " R " + i, 1e-8, 1e-8);
                Close(e[2], s.Mobilized, id + " Rmob " + i, 1e-8, 1e-8); Close(e[3], s.MAlpha, id + " mα " + i, 1e-9, 1e-12);
            }
            ok++;
        }
        Assert.AreEqual(321, ok); Assert.AreEqual(90, none); Assert.AreEqual(3, errors);
    }

    private static Dictionary<string, SlopeSection> Sections(string[] rows)
        => rows.Where(r => r.StartsWith("SECTION;")).Select(r => r.Split(';')).ToDictionary(c => c[1], Section);

    [TestMethod]
    public void LegacySlopeSearchesAreReproduced()
    {
        var rows = Rows("geotechnics-slope.csv"); var sections = Sections(rows);
        var searches = rows.Where(r => r.StartsWith("SEARCH;")).Select(r => r.Split(';')).ToDictionary(c => c[1], Search);
        var cases = rows.Where(r => r.StartsWith("CASE;")).Select(r => r.Split(';')).ToArray();
        Assert.AreEqual(4, sections.Count); Assert.AreEqual(6, searches.Count); Assert.AreEqual(15, cases.Length);
        foreach (var group in cases.GroupBy(c => c[1] + "/" + c[2]))
        {
            var first = group.First();
            var result = SlopeStability.Calculate(sections[first[1]], searches[first[2]], group.Select(c => Factors(c[3])).ToArray());
            Assert.AreEqual(SlopeStability.Notes.Count, result.Notes.Count);
            int k = 0;
            foreach (var c in group)
            {
                var r = result.Cases[k++]; string id = c[1] + " " + c[2] + " " + r.Factors.Name;
                // Section S2: the legacy filter on the depth drops by rounding part of the circles built exactly at the bounds (with grid 3 and no
                // refinement it tries 15 of the 24 circles that exist); the port keeps them (1e-9 mm), so the search explores another set of circles.
                if (c[1] == "S2") { SameEvaluationOfTheLegacyCircle(c, sections["S2"], searches[c[2]], r, id); continue; }
                Assert.AreEqual(int.Parse(c[12]), r.Tried, id + " tried"); Assert.AreEqual(int.Parse(c[13]), r.GeometricallyValid, id + " valid");
                Assert.AreEqual(int.Parse(c[14]), r.Solved, id + " solved"); Assert.AreEqual(int.Parse(c[15]), r.NumericalFailures, id + " failures");
                Assert.AreEqual(bool.Parse(c[16]), r.Boundary, id + " boundary");
                string status = c[17];
                var expectedStatus = status.Contains("Nessuna superficie") ? SlopeSearchStatus.NoSurface : status.Contains("Ricerca incompleta") ? SlopeSearchStatus.Incomplete
                    : status.Contains("Minimo sul bordo") ? SlopeSearchStatus.BoundaryMinimum : status.Contains("Discretizzazione non convergente") ? SlopeSearchStatus.NotConverged
                    : SlopeSearchStatus.Converged;
                Assert.AreEqual(expectedStatus, r.Status, id + " status " + status);
                if (c[4] == "null") { Assert.IsNull(r.Critical, id); Assert.IsNull(r.Ratio); Assert.IsFalse(r.IsConclusive); continue; }
                var b = r.Critical!;
                Assert.AreEqual(status.StartsWith("Non soddisfatta"), b.Ratio > 1, id + " verdict");
                bool refinedOk = r.RefinedFactor.HasValue && Math.Abs(r.RefinedFactor.Value / b.Factor - 1) <= .02;
                Assert.AreEqual(r.Status != SlopeSearchStatus.NotConverged, refinedOk, id + " refined factor " + r.RefinedFactor);
                SameCircle(Circle(c[5]), b.Circle, id);
                Close(D(c[6]), b.Factor, id + " F", 1e-9); Close(D(c[7]), b.Ratio, id + " ratio", 1e-9);
                Assert.AreEqual(int.Parse(c[8]), b.Iterations, id + " iterations");
                Close(D(c[10]), b.Driving, id + " driving", 1e-9, 1e-9); Close(D(c[11]), b.Resistance, id + " resistance", 1e-9, 1e-9);
                var slices = c[18].Split('|'); Assert.AreEqual(slices.Length, b.Slices.Count, id + " slices");
                for (int i = 0; i < slices.Length; i++) SameSlice(slices[i], b.Slices[i], id, true);
            }
        }
    }

    /// <summary>
    /// Where the explored sets differ, the port evaluates the critical circle of the legacy exactly as the legacy did (slices, F with the search
    /// slices or twice as many after the convergence check) and its own minimum is not higher; with no surface both find none.
    /// </summary>
    private static void SameEvaluationOfTheLegacyCircle(string[] c, SlopeSection section, SlopeSearch search, SlopeCaseResult r, string id)
    {
        if (c[4] == "null")
        {
            Assert.IsNull(r.Critical, id); Assert.AreEqual(SlopeSearchStatus.NoSurface, r.Status, id);
            Assert.AreEqual(15, int.Parse(c[12]), id + " legacy"); Assert.AreEqual(24, r.Tried, id + ": 8 exit-entry pairs reach the 3 depths");
            Assert.AreEqual(0, r.GeometricallyValid, id + ": every circle cuts the foundation");
            return;
        }
        var legacy = Circle(c[5])!; var expected = c[18].Split('|');
        int count = new[] { search.Slices, 2 * search.Slices }.First(n => SlopeGeometry.Divisions(section, legacy, n).Length - 1 == expected.Length);
        var slices = SlopeStability.Slices(section, legacy, r.Factors, count);
        var solved = BishopSolver.Solve(legacy, slices, r.Factors.ResistanceFactor)!;
        Close(D(c[6]), solved.Factor, id + " F of the legacy circle", 1e-9); Assert.AreEqual(int.Parse(c[8]), solved.Iterations, id + " iterations");
        for (int i = 0; i < expected.Length; i++) SameSlice(expected[i], solved.Slices[i], id + " legacy circle", true);
        Assert.IsTrue(r.Critical!.Factor <= D(c[6]) * (1 + 1e-9), id + $": port minimum {r.Critical.Factor} above the legacy one {c[6]}");
        Assert.IsTrue(r.Tried > 0 && r.Solved > 0 && r.NumericalFailures == 0, id);
    }

    [TestMethod]
    public void LegacySlicesOfAssignedCirclesAreReproduced()
    {
        var rows = Rows("geotechnics-slope.csv"); var sections = Sections(rows); int n = 0;
        foreach (var c in rows.Where(r => r.StartsWith("SLICES;")).Select(r => r.Split(';')))
        {
            var section = sections[c[1]]; var factors = Factors(c[2]); var circle = Circle(c[4])!; string id = "SLICES " + c[1] + " " + factors.Name;
            var slices = SlopeStability.Slices(section, circle, factors, int.Parse(c[3]));
            Assert.AreEqual(bool.Parse(c[5]), SlopeGeometry.Admissible(section, circle), id + " admissible");
            var expected = c[7].Split('|'); Assert.AreEqual(expected.Length, slices.Length, id + " count");
            for (int i = 0; i < expected.Length; i++) SameSlice(expected[i], slices[i], id, false);
            var solved = BishopSolver.Solve(circle, slices, factors.ResistanceFactor);
            if (c[6].Length == 0) Assert.IsNull(solved, id); else Close(D(c[6]), solved!.Factor, id + " F", 1e-9);
            n++;
        }
        Assert.AreEqual(6, n);
    }

    [TestMethod]
    public void LegacyGeometryFunctionsAreReproduced()
    {
        var rows = Rows("geotechnics-slope.csv"); var sections = Sections(rows); var counts = new Dictionary<string, int>();
        foreach (var c in rows.Where(r => r.StartsWith("GEO;")).Select(r => r.Split(';')))
        {
            string kind = c[1], id = string.Join(" ", c.Take(4)); counts[kind] = counts.TryGetValue(kind, out var k) ? k + 1 : 1;
            switch (kind)
            {
                case "THROUGH":
                case "TANGENT":
                    var p = Points(c[2]);
                    SameCircle(Circle(c[4]), kind == "THROUGH" ? SlopeGeometry.Through(p[0], p[1], D(c[3]) * M) : SlopeGeometry.TangentAtEntry(p[0], p[1]), id);
                    break;
                case "CROSSINGS":
                    var expected = c[4].Length == 0 ? new double[0] : c[4].Split('/').Select(D).ToArray(); var actual = SlopeGeometry.Crossings(Circle(c[2])!, D(c[3]) * M).ToArray();
                    Assert.AreEqual(expected.Length, actual.Length, id);
                    for (int i = 0; i < expected.Length; i++) Close(expected[i] * M, actual[i], id, 1e-12, 1e-7);
                    break;
                case "PROPERTIES":
                    var props = SlopeGeometry.Properties(Points(c[2])); var v = c[4].Split(':').Select(D).ToArray();
                    Close(v[0] * M * M, props.Area, id + " area", 1e-12); Close(v[1] * M, props.Centroid.X, id + " x", 1e-12, 1e-9); Close(v[2] * M, props.Centroid.Y, id + " y", 1e-12, 1e-9);
                    break;
                case "INTERVAL":
                    if (c[4].StartsWith("error")) { Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.VerticalInterval(Points(c[2]), D(c[3]) * M), id); break; }
                    var interval = SlopeGeometry.VerticalInterval(Points(c[2]), D(c[3]) * M);
                    if (c[4] == "null") { Assert.IsNull(interval, id); break; }
                    var span = c[4].Split(':').Select(D).ToArray();
                    Close(span[0] * M, interval!.Value.Bottom, id, 1e-12, 1e-9); Close(span[1] * M, interval.Value.Top, id, 1e-12, 1e-9);
                    break;
                case "HEIGHT":
                    if (c[4].StartsWith("error")) { Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.Height(Points(c[2]), D(c[3]) * M), id); break; }
                    Close(D(c[4]) * M, SlopeGeometry.Height(Points(c[2]), D(c[3]) * M), id, 1e-12, 1e-9);
                    break;
                case "ADMISSIBLE":
                    var circle = Circle(c[3]);
                    if (circle != null) Assert.AreEqual(bool.Parse(c[4]), SlopeGeometry.Admissible(sections[c[2]], circle), id);
                    break;
                case "DIVISIONS":
                    var cuts = c[4].Split('/').Select(D).ToArray(); var divisions = SlopeGeometry.Divisions(sections[c[2]], Circle(c[3])!, 12);
                    Assert.AreEqual(cuts.Length, divisions.Length, id);
                    for (int i = 0; i < cuts.Length; i++) Close(cuts[i] * M, divisions[i], id + " " + i, 1e-12, 1e-7);
                    break;
                default: Assert.Fail("Unknown row " + kind); break;
            }
        }
        Assert.AreEqual(8, counts["THROUGH"]); Assert.AreEqual(6, counts["TANGENT"]); Assert.AreEqual(7, counts["CROSSINGS"]); Assert.AreEqual(3, counts["PROPERTIES"]);
        Assert.AreEqual(27, counts["INTERVAL"]); Assert.AreEqual(13, counts["HEIGHT"]); Assert.AreEqual(5, counts["ADMISSIBLE"]); Assert.AreEqual(3, counts["DIVISIONS"]);
    }

    [TestMethod]
    public void LegacySettlementsAreReproduced()
    {
        int stresses = 0, calculations = 0, rejected = 0;
        foreach (var c in Rows("geotechnics-settlement.csv").Select(r => r.Split(';')))
        {
            string id = string.Join(" ", c.Take(10));
            if (c[0] == "STRESS")
            {
                double x = D(c[1]) * M, z = D(c[2]) * M, left = D(c[3]) * M, right = D(c[4]) * M, pl = D(c[5]) * KPa, pr = D(c[6]) * KPa;
                if (c[7].StartsWith("error")) Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Stress(x, z, left, right, pl, pr), id);
                else Close(D(c[7]) * KPa, FoundationSettlement.Stress(x, z, left, right, pl, pr), id, 1e-9, 1e-14);
                stresses++;
            }
            else if (c[0] == "CALC")
            {
                var layers = c[1].Split('|').Select(l => l.Split(':')).Select(l => new SettlementLayer(l[0], D(l[1]) * M, D(l[2]) * KPa)).ToArray();
                SettlementResult Run() => FoundationSettlement.Calculate(layers, D(c[2]) * M, D(c[3]) * M, D(c[4]) * M, D(c[5]) * KPa, D(c[6]) * KPa, D(c[7]) * KPa, D(c[8]) * M, int.Parse(c[9]));
                calculations++;
                if (c[10].StartsWith("error")) { Assert.ThrowsException<ArgumentException>(Run, id); rejected++; continue; }
                var r = Run();
                Close(D(c[11]), r.Settlement, id + " settlement", 1e-9, 1e-12); Close(D(c[12]) * KPa, r.BottomStress, id + " bottom stress", 1e-9, 1e-14);
                var slices = c[13].Split('|'); Assert.AreEqual(slices.Length, r.Slices.Count, id);
                for (int i = 0; i < slices.Length; i++)
                {
                    var e = slices[i].Split(':'); var s = r.Slices[i];
                    Assert.AreEqual(e[0], s.Soil); Close(D(e[1]) * M, s.Top, id + " top", 1e-12, 1e-9); Close(D(e[2]) * M, s.Bottom, id + " bottom", 1e-12, 1e-9);
                    Close(D(e[3]) * KPa, s.Stress, id + " stress", 1e-9, 1e-14); Close(D(e[4]) * KPa, s.ConstrainedModulus, id + " modulus", 1e-15);
                    Close(D(e[5]), s.Settlement, id + " slice settlement", 1e-9, 1e-14);
                }
            }
        }
        Assert.AreEqual(121, stresses); Assert.AreEqual(36, calculations); Assert.AreEqual(6, rejected);
    }

    [TestMethod]
    public void LegacyNewmarkDisplacementsAreReproduced()
    {
        var rows = Rows("geotechnics-newmark.csv").Select(r => r.Split(';')).ToArray();
        var records = rows.Where(c => c[0] == "RECORD").ToDictionary(c => c[1], c => c[2].Split('|').Select(s => s.Split(':')).Select(s => new AccelerogramSample(D(s[0]), D(s[1]))).ToArray());
        int runs = 0;
        foreach (var c in rows.Where(c => c[0] == "RUN"))
        {
            string id = string.Join(" ", c.Take(4));
            var r = NewmarkSliding.Calculate(records[c[1]], D(c[2]), D(c[3]));
            Assert.AreEqual("ok", c[4], id);
            Close(D(c[5]), r.Displacement, id + " displacement", 1e-9, 1e-9); Close(D(c[6]) * M, r.PeakVelocity, id + " peak velocity", 1e-9, 1e-9);
            Close(D(c[7]), r.Pga, id + " pga", 1e-15);
            var points = c[8].Split('|'); Assert.AreEqual(points.Length, r.Points.Count, id);
            for (int i = 0; i < points.Length; i++)
            {
                var e = points[i].Split(':').Select(D).ToArray(); var p = r.Points[i];
                Close(e[0], p.Time, id + " t", 1e-12, 1e-12); Close(e[1], p.Acceleration, id + " a", 1e-15);
                Close(e[2] * M, p.Velocity, id + " v " + i, 1e-9, 1e-9); Close(e[3], p.Displacement, id + " d " + i, 1e-9, 1e-9);
            }
            runs++;
        }
        Assert.AreEqual(50, runs); Assert.AreEqual(5, records.Count);
    }

    [TestMethod]
    public void LegacySeismicBearingCapacitiesAreReproduced()
    {
        int ok = 0, errors = 0, exhausted = 0, satisfied = 0;
        foreach (var c in Rows("geotechnics-seismic-bearing.csv").Select(r => r.Split(';')))
        {
            string id = string.Join(" ", c.Take(10));
            SeismicBearingResult Run() => ShallowFoundationSeismic.Calculate(D(c[0]) * M, D(c[1]) * KN3, D(c[2]) * Deg, D(c[3]), D(c[4]), D(c[5]) * M, D(c[6]), D(c[7]), D(c[8]), D(c[9]), modelFactorOnInertia: true);
            if (c[10].StartsWith("error")) { Assert.ThrowsException<ArgumentException>(Run, id); errors++; continue; }
            var r = Run();
            Close(D(c[11]), r.Capacity, id + " capacity", 1e-9, 1e-9);
            if (c[12].Length == 0) Assert.IsNull(r.Ratio, id); else Close(D(c[12]), r.Ratio!.Value, id + " ratio", 1e-9);
            Close(D(c[13]), r.NMax, id + " Nmax", 1e-9); Close(D(c[14]), r.SoilInertia, id + " F", 1e-12, 1e-15);
            Close(D(c[15]), r.NBar, id + " N", 1e-12, 1e-15); Close(D(c[16]), r.VBar, id + " V", 1e-12, 1e-15); Close(D(c[17]), r.MBar, id + " M", 1e-12, 1e-15);
            Close(D(c[18]), r.VerticalLimit, id + " limit", 1e-12, 1e-15);
            if (c[19].Length == 0) Assert.IsNull(r.Interaction, id); else Close(D(c[19]), r.Interaction!.Value, id + " interaction", 1e-9, 1e-12);
            var status = c[20] == "Soddisfatta" ? SeismicBearingStatus.Satisfied : c[20].Contains("dominio sismico esaurito") ? SeismicBearingStatus.DomainExhausted : SeismicBearingStatus.NotSatisfied;
            Assert.AreEqual(status, r.Status, id);
            ok++; if (status == SeismicBearingStatus.DomainExhausted) exhausted++; if (status == SeismicBearingStatus.Satisfied) satisfied++;
        }
        Assert.AreEqual(3900, ok + errors); Assert.IsTrue(exhausted > 0 && satisfied > 0 && ok - exhausted - satisfied > 0, $"{ok} {exhausted} {satisfied}");
    }
}
