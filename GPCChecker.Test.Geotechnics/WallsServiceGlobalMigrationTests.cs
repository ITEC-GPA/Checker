using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Checkers.Geotechnics.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.PilesFixture;
using static GeotechnicsTests.WallsFixture;

namespace GeotechnicsTests;

/// <summary>
/// Retaining walls, phase W3: serviceability (settlements, rotation, displacements from the legacy curvatures, Newmark) and global stability of the
/// legacy documents (fixtures walls-*.jsonl, ANTHEA harness cbed972) reproduced by WallServiceability and WallGlobalStability.
/// </summary>
[TestClass]
public class WallsServiceGlobalMigrationTests
{
    public TestContext TestContext { get; set; } = null!;

    static (string Name, JsonObject Doc, JsonObject Result, WallInput Input, WallResult Wall)[] Documents(Func<JsonObject, bool> select)
        => GzipLines("walls-documents.jsonl.gz").Where(l => !IsError(l["result"]) && select(l["input"]!.AsObject())).Select(l =>
        {
            var doc = l["input"]!.AsObject(); var r = l["result"]!.AsObject(); var input = Input(doc);
            return (Str(l["name"]), doc, r, input, RetainingWallAnalysis.Calculate(input, Combinations(doc, r)));
        }).ToArray();

    [TestMethod]
    public void LegacyServiceabilityIsReproduced()
    {
        int docs = 0, cases = 0, records = 0, checks = 0;
        foreach (var (name, doc, r, input, wall) in Documents(d => Service(d) != null))
        {
            var options = Service(doc)!; var legacyCases = r["combinazioni"]!.AsArray();
            var service = WallServiceability.Calculate(input, wall, options, c => Curvatures(doc, legacyCases.First(x => Str(x!["Name"]) == c.Combination.Name)!.AsObject()));
            var legacy = r["esercizio"]!;
            var lc = legacy["Cases"]!.AsArray(); Assert.AreEqual(lc.Count, service.Cases.Count, "service cases");
            for (int i = 0; i < lc.Count; i++)
            {
                var l = lc[i]!; var p = service.Cases[i]; string id = Str(l["Combination"]);
                Assert.AreEqual(id, p.Combination);
                foreach (var (key, value) in new[] { ("ToeSettlement", p.ToeSettlement), ("CentreSettlement", p.CentreSettlement), ("HeelSettlement", p.HeelSettlement), ("StemDisplacement", p.StemDisplacement),
                    ("HeadDisplacement", p.HeadDisplacement) })
                {
                    double? expected = Num(l[key]); Assert.AreEqual(expected.HasValue, value.HasValue, id + " " + key);
                    if (expected.HasValue) Close(expected.Value, value!.Value, id + " " + key, 1e-9, 1e-9);
                }
                double? rotation = Num(l["FoundationRotation"]); Assert.AreEqual(rotation.HasValue, p.Rotation.HasValue);
                if (rotation.HasValue) Close(rotation.Value, p.Rotation!.Value, id + " rotation", 1e-9, 1e-14);
                Assert.AreEqual(Str(l["Status"]), p.Status, id + " status");
                cases++;
            }
            var eq = legacy["Earthquakes"]!.AsArray(); Assert.AreEqual(eq.Count, service.Sliding.Count);
            for (int i = 0; i < eq.Count; i++)
            {
                var history = eq[i]!["History"];
                Assert.AreEqual(history is JsonObject, service.Sliding[i].History != null, Str(eq[i]!["Name"]));
                if (history is JsonObject h) Close(Req(h["DisplacementMm"], "u"), service.Sliding[i].History!.Displacement, "Newmark", 1e-12);
                records++;
            }
            var legacyChecks = r["verifiche_geotecniche"]!.AsArray().Where(c => ServiceKind(Str(c!["Name"])) != null).ToArray();
            Assert.AreEqual(legacyChecks.Length, service.Checks.Count, name + " checks");
            for (int k = 0; k < legacyChecks.Length; k++)
            {
                var lk = legacyChecks[k]!; var pk = service.Checks[k]; string id = name + " " + Str(lk["Combination"]) + " " + Str(lk["Name"]);
                Assert.AreEqual(ServiceKind(Str(lk["Name"])), pk.Kind, id); Assert.AreEqual(Str(lk["Combination"]), pk.Combination, id);
                Close(Req(lk["Demand"], "D"), pk.Demand, id + " demand", 1e-9, 1e-12);
                double? resistance = Num(lk["Resistance"]); Assert.AreEqual(resistance.HasValue, pk.Resistance.HasValue, id + " resistance");
                if (resistance.HasValue) Close(resistance.Value, pk.Resistance!.Value, id, 1e-12);
                double? ratio = Num(lk["Ratio"]); Assert.AreEqual(ratio.HasValue, pk.Ratio.HasValue, id + " ratio");
                if (ratio.HasValue) Close(ratio.Value, pk.Ratio!.Value, id + " ratio", 1e-9, 1e-12);
                string status = Str(lk["Status"]);
                if (status is "Soddisfatta" or "Non soddisfatta") Assert.AreEqual(CheckStatus(status), pk.Status, id); else Assert.AreEqual(status, pk.Message, id);
                checks++;
            }
            docs++;
        }
        Assert.AreEqual(6, docs);
        TestContext.WriteLine($"{docs} documents, {cases} service cases, {records} accelerograms, {checks} checks");
        Assert.IsTrue(cases >= 18 && records >= 12 && checks > 60, $"{cases} {records} {checks}");
    }

    [TestMethod]
    public void LegacyGlobalCombinationsAreReproduced()
    {
        int lines = 0, rows = 0, rejected = 0;
        foreach (var line in Lines("walls-combinations.jsonl"))
        {
            string name = Str(line["name"]); var legacy = line["global"];
            if (line["input"] is not JsonObject doc) continue;
            WallInput input;
            try { input = Input(doc); } catch (ArgumentException) { continue; }
            // ANTHEA checks the actions while generating; the port checks them in the calculation of the wall.
            if (IsError(legacy)) { Throws(() => { WallGlobalStability.Combinations(input, GlobalSeismic(doc), false); RetainingWallAnalysis.Calculate(input, WallCombinations.Generate(input)); }, name); rejected++; continue; }
            var port = WallGlobalStability.Combinations(input, GlobalSeismic(doc), false); var list = legacy!.AsArray();
            Assert.AreEqual(list.Count, port.Count, name);
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i]!; var p = port[i]; string id = name + " " + Str(l["name"]);
                Assert.AreEqual(Str(l["name"]), p.Name, id);
                foreach (var (a, b, what) in new[] { (Req(l["mphi"], "mφ"), p.TanFrictionAngle, "mφ"), (Req(l["mc"], "mc"), p.EffectiveCohesion, "mc"), (Req(l["mcu"], "mcu"), p.UndrainedShearStrength, "mcu"),
                    (Req(l["r"], "r"), p.ResistanceFactor, "γR"), (Req(l["kh"], "kh"), p.Kh, "kh"), (Req(l["kv"], "kv"), p.Kv, "kv"), (Req(l["soil"], "soil"), p.SoilWeight, "soil"), (Req(l["wall"], "wall"), p.BodyWeight, "wall") })
                    Close(a, b, id + " " + what, 1e-14, 1e-16);
                // By position: ANTHEA upgrades a version 1 document again inside the generator, with new identifiers of the actions.
                var coefficients = l["coefficients"]!.AsObject().Select(c => Req(c.Value, c.Key)).ToArray(); var values = input.Actions.Select(a => p.Loads[a.Id]).ToArray();
                Assert.AreEqual(coefficients.Length, values.Length, id);
                for (int k = 0; k < values.Length; k++) Close(coefficients[k], values[k], id + " action " + k, 1e-14, 1e-16);
                rows++;
            }
            lines++;
        }
        TestContext.WriteLine($"{lines} documents, {rows} global rows, {rejected} rejected");
        Assert.IsTrue(lines > 90 && rows > 300, $"{lines} {rows}");
    }

    [TestMethod]
    public void LegacyGlobalStabilityIsReproduced()
    {
        int docs = 0, cases = 0, sameCircle = 0, otherCircle = 0, outside = 0, lostSlice = 0;
        foreach (var line in GzipLines("walls-documents.jsonl.gz").Where(l => !IsError(l["result"]) && Bool(l["input"]!["global_stability"]?["enabled"])))
        {
            var doc = line["input"]!.AsObject(); var r = line["result"]!.AsObject(); string name = Str(line["name"]);
            if (!Bool(doc["global_stability"]!["profile_confirmed"])) { Assert.IsFalse(r["stabilita_globale"] is JsonObject, name); outside++; continue; } // confirmation of the interface
            var input = Input(doc); var profile = GlobalProfile(doc);
            var rows = WallGlobalStability.Combinations(input, GlobalSeismic(doc), profile.Undrained);
            var result = WallGlobalStability.Calculate(input, profile, rows);
            var legacy = r["stabilita_globale"]!["Cases"]!.AsArray(); Assert.AreEqual(legacy.Count, result.Cases.Count, name);
            for (int i = 0; i < legacy.Count; i++)
            {
                var l = legacy[i]!; var p = result.Cases[i]; string id = name + " " + Str(l["Factors"]!["Name"]);
                Assert.AreEqual(Str(l["Factors"]!["Name"]), p.Factors.Name, id);
                var lc = l["Critical"]; Assert.AreEqual(lc is JsonObject, p.Critical != null, id + " critical");
                if (lc is not JsonObject critical) continue;
                var circle = critical["Circle"]!;
                var legacyCircle = new SlipCircle(Req(circle["X"], "X") * M, Req(circle["Y"], "Y") * M, Req(circle["Radius"], "R") * M, Req(circle["Left"], "L") * M, Req(circle["Right"], "R") * M);
                double legacyF = Req(critical["Factor"], "F");
                bool same = Math.Abs(legacyCircle.X - p.Critical!.Circle.X) < 1e-6 && Math.Abs(legacyCircle.Y - p.Critical.Circle.Y) < 1e-6 && Math.Abs(legacyCircle.Radius - p.Critical.Circle.Radius) < 1e-6;
                if (same) { Close(legacyF, p.Critical.Factor, id + " F", 1e-9); sameCircle++; }
                else
                {
                    // The port explores the boundary circles that the legacy depth filter dropped (phase G): its minimum is not higher, and the legacy
                    // circle evaluated by the port has the legacy factor.
                    var legacySlices = critical["Slices"]!.AsArray(); int expected = legacySlices.Count;
                    int count = new[] { result.Search.Slices, 2 * result.Search.Slices }.First(n => SlopeGeometry.Divisions(result.Section, legacyCircle, n).Length - 1 >= expected);
                    var all = SlopeStability.Slices(result.Section, legacyCircle, p.Factors, count);
                    if (all.Length == expected + 1)
                    {
                        // Legacy defect: the last division point exceeded the end of the arc by one ulp and was dropped, with the last slice. The
                        // port without that slice has the legacy factor; the complete arc has a higher factor, and the port minimum is not above it.
                        Close(Req(legacySlices[^1]!["Right"], "right") * M, all[^2].Right, id + " end of the legacy slices", 1e-12, 1e-6);
                        var truncated = BishopSolver.Solve(legacyCircle, all.Take(expected).ToArray(), p.Factors.ResistanceFactor)!;
                        Close(legacyF, truncated.Factor, id + " F of the legacy circle without its last slice", 1e-9);
                        var complete = BishopSolver.Solve(legacyCircle, all, p.Factors.ResistanceFactor);
                        Assert.IsTrue(complete == null || p.Critical.Factor <= complete.Factor * (1 + 1e-9), id + " port minimum above the complete legacy circle");
                        lostSlice++;
                    }
                    else
                    {
                        // The port explores the boundary circles that the legacy depth filter dropped (phase G): its minimum is not higher, and the
                        // legacy circle evaluated by the port has the legacy factor.
                        Assert.AreEqual(expected, all.Length, id + " slices");
                        var solved = BishopSolver.Solve(legacyCircle, all, p.Factors.ResistanceFactor)!;
                        Close(legacyF, solved.Factor, id + " F of the legacy circle", 1e-9);
                        Assert.IsTrue(p.Critical.Factor <= legacyF * (1 + 1e-9), id + " port minimum above the legacy one");
                        otherCircle++;
                    }
                }
                cases++;
            }
            var checks = WallGlobalStability.Checks(result);
            var legacyChecks = r["verifiche_geotecniche"]!.AsArray().Where(c => Str(c!["Name"]) == "Stabilità globale · Bishop").ToArray();
            Assert.AreEqual(legacyChecks.Length, checks.Count, name + " checks");
            for (int k = 0; k < checks.Count; k++) Close(Req(legacyChecks[k]!["Demand"], "γR"), checks[k].Demand, name + " γR", 1e-15);
            docs++;
        }
        TestContext.WriteLine($"{docs} documents, {cases} combinations: {sameCircle} with the legacy circle, {otherCircle} with a lower or equal one, {lostSlice} legacy circles without their last slice; {outside} unconfirmed profiles");
        Assert.AreEqual(3, docs); Assert.AreEqual(1, outside); Assert.IsTrue(cases >= 8, "cases " + cases);
    }

    [TestMethod]
    public void ProposedProfileIsTheLegacyPreparation()
    {
        int n = 0;
        foreach (var line in GzipLines("walls-documents.jsonl.gz").Where(l => Bool(l["input"]!["global_stability"]?["enabled"]) && Str(l["input"]!["global_stability"]!["soil_mode"]) == "Due colonne"))
        {
            var doc = line["input"]!.AsObject(); string name = Str(line["name"]);
            var proposed = WallGlobalStability.Propose(Input(doc), 5, 30, 2); var legacy = GlobalProfile(doc);
            void Same(IReadOnlyList<SlopePoint> a, IReadOnlyList<SlopePoint> b, string what)
            {
                Assert.AreEqual(a.Count, b.Count, name + " " + what);
                for (int i = 0; i < a.Count; i++) { Close(a[i].X, b[i].X, name + " " + what + " x", 1e-12, 1e-9); Close(a[i].Y, b[i].Y, name + " " + what + " y", 1e-12, 1e-9); }
            }
            Same(legacy.Valley, proposed.Valley, "valley"); Same(legacy.Uphill, proposed.Uphill, "uphill");
            Assert.AreEqual(legacy.Layers.Count, proposed.Layers.Count); Assert.AreEqual(legacy.ValleyLayers.Count, proposed.ValleyLayers.Count);
            for (int i = 0; i < legacy.Layers.Count; i++) { Close(legacy.Layers[i].Bottom, proposed.Layers[i].Bottom, name + " bottom", 1e-12, 1e-9); Close(legacy.Layers[i].Soil.FrictionAngle, proposed.Layers[i].Soil.FrictionAngle, name + " φ", 1e-14); }
            Close(legacy.SoilSplitX, proposed.SoilSplitX, name + " split", 1e-12);
            var s = legacy.Search; var t = proposed.Search;
            foreach (var (a, b, what) in new[] { (s.ExitMin, t.ExitMin, "exit min"), (s.ExitMax, t.ExitMax, "exit max"), (s.EntryMin, t.EntryMin, "entry min"), (s.EntryMax, t.EntryMax, "entry max"),
                (s.DepthMin, t.DepthMin, "depth min"), (s.DepthMax, t.DepthMax, "depth max") })
                Close(a, b, name + " " + what, 1e-12, 1e-9);
            n++;
        }
        Assert.AreEqual(4, n);
    }
}
