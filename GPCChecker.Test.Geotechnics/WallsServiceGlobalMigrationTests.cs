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
        int lines = 0, rows = 0, rejected = 0, added = 0;
        foreach (var line in Lines("walls-combinations.jsonl"))
        {
            string name = Str(line["name"]); var legacy = line["global"];
            if (line["input"] is not JsonObject doc) continue;
            WallInput input;
            try { input = Input(doc); } catch (ArgumentException) { continue; }
            // ANTHEA checks the actions while generating; the port checks them in the calculation of the wall.
            if (IsError(legacy)) { Throws(() => { WallGlobalStability.Combinations(input, GlobalSeismic(doc), false); RetainingWallAnalysis.Calculate(input, WallCombinations.Generate(input)); }, name); rejected++; continue; }
            var port = WallGlobalStability.Combinations(input, GlobalSeismic(doc), false); var list = legacy!.AsArray();
            // The legacy rows with the decisions D4 b (WallsFixture.DecidedGlobal) and D6 a (WallsFixture.DecidedGlobalD6) of 28/10/2026. The loads by
            // position: ANTHEA upgrades a version 1 document again inside the generator, with new identifiers of the actions.
            var decided = list.Select(l =>
            {
                var coefficients = l!["coefficients"]!.AsObject().Select(c => Req(c.Value, c.Key)).ToArray();
                Assert.AreEqual(coefficients.Length, input.Actions.Count, name + " " + Str(l["name"]));
                return new GPC.Checkers.Geotechnics.Slopes.SlopeFactors(Str(l["name"]), Req(l["soil"], "soil"), Req(l["wall"], "wall"), Req(l["mphi"], "mφ"), Req(l["mc"], "mc"), Req(l["mcu"], "mcu"),
                    Req(l["r"], "r"), Req(l["kh"], "kh"), Req(l["kv"], "kv"), false, input.Actions.Select((a, k) => (a.Id, DecidedGlobal(Str(l["name"]), input, a.Id, coefficients[k]))).ToDictionary(p => p.Item1, p => p.Item2));
            }).ToList();
            var expected = DecidedGlobalD6(decided, input);
            Assert.AreEqual(expected.Count, port.Count, name);
            added += expected.Count - list.Count;
            for (int i = 0; i < expected.Count; i++)
            {
                var l = expected[i].Row; var p = port[i]; string id = name + " " + l.Name;
                Assert.AreEqual(l.Name, p.Name, id);
                foreach (var (a, b, what) in new[] { (l.TanFrictionAngle, p.TanFrictionAngle, "mφ"), (l.EffectiveCohesion, p.EffectiveCohesion, "mc"), (l.UndrainedShearStrength, p.UndrainedShearStrength, "mcu"),
                    (l.ResistanceFactor, p.ResistanceFactor, "γR"), (l.Kh, p.Kh, "kh"), (l.Kv, p.Kv, "kv"), (l.SoilWeight, p.SoilWeight, "soil"), (l.BodyWeight, p.BodyWeight, "wall") })
                    Close(a, b, id + " " + what, 1e-14, 1e-16);
                foreach (var a in input.Actions) Close(l.Loads[a.Id], p.Loads[a.Id], id + " action " + a.Id, 1e-14, 1e-16);
                rows++;
            }
            lines++;
        }
        TestContext.WriteLine($"{lines} documents, {rows} global rows ({added} added by the decision D6 a), {rejected} rejected");
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
            var legacy = r["stabilita_globale"]!["Cases"]!.AsArray();
            // The legacy rows with the decisions D4 b and D6 a of 28/10/2026 (WallsFixture.DecidedGlobal, DecidedGlobalD6): every legacy case is
            // compared with the port case of its row with every variable group present; the rows with absent groups are new (loads by position).
            var expanded = DecidedGlobalD6(legacy.Select(l =>
            {
                var f = l!["Factors"]!; var loads = f["Loads"]!.AsObject().Select(c => Req(c.Value, c.Key)).ToArray(); string row = Str(f["Name"]);
                Assert.AreEqual(input.Actions.Count, loads.Length, name + " " + row);
                return new SlopeFactors(row, Req(f["Soil"], "soil"), Req(f["Body"], "body"), Req(f["MPhi"], "mφ"), Req(f["MC"], "mc"), Req(f["MCu"], "mcu"), Req(f["R"], "r"),
                    Req(f["Kh"], "kh"), Req(f["Kv"], "kv"), Bool(f["Undrained"]), input.Actions.Select((a, k) => (a.Id, DecidedGlobal(row, input, a.Id, loads[k]))).ToDictionary(p => p.Item1, p => p.Item2));
            }).ToList(), input);
            Assert.AreEqual(expanded.Count, result.Cases.Count, name);
            for (int i = 0; i < legacy.Count; i++)
            {
                int j = expanded.FindIndex(e => e.Origin == i && e.Pattern == 0);
                var l = legacy[i]!; var p = result.Cases[j]; string id = name + " " + Str(l["Factors"]!["Name"]);
                Assert.AreEqual(expanded[j].Row.Name, p.Factors.Name, id);
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
            // One check for every case: the legacy ones are those of the rows with every variable group present (D6 a).
            Assert.AreEqual(legacy.Count, legacyChecks.Length, name + " legacy checks"); Assert.AreEqual(expanded.Count, checks.Count, name + " checks");
            for (int k = 0; k < legacyChecks.Length; k++)
                Close(Req(legacyChecks[k]!["Demand"], "γR"), checks[expanded.FindIndex(e => e.Origin == k && e.Pattern == 0)].Demand, name + " γR", 1e-15);
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
