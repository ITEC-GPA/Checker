using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.PilesFixture;
using static GeotechnicsTests.WallsFixture;

namespace GeotechnicsTests;

/// <summary>
/// Retaining walls, phase W1: the legacy outputs frozen by the ANTHEA harness (WallCapture, commit cbed972; walls-*.jsonl) reproduced by
/// GPC.Checkers.Geotechnics.Walls with the typed inputs of Model after the conversion of the units (m → mm, kPa → MPa, kNm/m → N·mm/mm).
/// </summary>
[TestClass]
public class WallsMigrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LegacyLawsAreReproduced()
    {
        var counts = new Dictionary<string, int>(); int rejected = 0;
        foreach (var line in Lines("walls-functions.jsonl"))
        {
            string kind = Str(line["kind"]); var args = line["args"]!; var r = line["result"]; string id = kind + " " + args.ToJsonString();
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
            void Rejected(Action run) { Assert.IsTrue(IsError(r), id); Throws(run, id); rejected++; }
            switch (kind)
            {
                case "Ka": Close(Req(r, "Ka"), EarthPressure.RankineActive(Req(args["phi"], "phi") * Deg), id, 1e-13); break;
                case "SeismicKa":
                {
                    double phi = Req(args["phi"], "phi") * Deg, kh = Req(args["kh"], "kh"), kv = Req(args["kv"], "kv");
                    if (IsError(r)) Rejected(() => EarthPressure.MononobeOkabe(phi, kh, kv)); else Close(Req(r, "K"), EarthPressure.MononobeOkabe(phi, kh, kv), id, 1e-12);
                    break;
                }
                case "ActiveHorizontal":
                {
                    double phi = Req(args["phi"], "phi") * Deg, delta = Req(args["delta"], "delta") * Deg, kh = Req(args["kh"], "kh"), kv = Req(args["kv"], "kv");
                    if (IsError(r)) Rejected(() => EarthPressure.ActiveHorizontal(phi, delta, kh, kv)); else Close(Req(r, "K"), EarthPressure.ActiveHorizontal(phi, delta, kh, kv), id, 1e-12);
                    break;
                }
                case "ContactLaw":
                {
                    var c = WallContact.Law(Req(args["width"], "B") * M, Req(args["n"], "N"), Req(args["x"], "x") * M);
                    Assert.AreEqual(Bool(r!["Valid"]), c.Valid, id);
                    Close(Req(r["Start"], "s") * M, c.Start, id, 1e-12, 1e-9); Close(Req(r["End"], "e") * M, c.End, id, 1e-12, 1e-9);
                    Close(Req(r["Toe"], "t") * KPa, c.Toe, id, 1e-12, 1e-15); Close(Req(r["Heel"], "h") * KPa, c.Heel, id, 1e-12, 1e-15); Close(Req(r["Peak"], "p") * KPa, c.Peak, id, 1e-12, 1e-15);
                    break;
                }
                case "Pressure":
                {
                    var c = WallContact.Law(Req(args["width"], "B") * M, Req(args["n"], "N"), Req(args["x"], "x") * M);
                    Close(Req(r, "p") * KPa, c.Pressure(Req(args["at"], "at") * M), id, 1e-12, 1e-15);
                    break;
                }
                case "Integrate":
                {
                    var pieces = args["pieces"]!.AsArray().Select(p => new WallPressureSegment(Req(p!["Z0"], "z0") * M, Req(p["Z1"], "z1") * M, Req(p["P0"], "p0") * KPa, Req(p["P1"], "p1") * KPa)).ToArray();
                    var v = WallPressureSegment.Integrate(pieces, Req(args["left"], "l") * M, Req(args["right"], "r") * M, Req(args["root"], "root") * M);
                    Close(Req(r!["Force"], "F"), v.Force, id, 1e-12, 1e-12); Close(Req(r["Moment"], "M") * KNm, v.Moment, id, 1e-12, 1e-9);
                    break;
                }
                case "IntegrateCurvature":
                {
                    var points = args["points"]!.AsArray().Select(p => new WallCurvaturePoint(Req(p!["Y"], "y") * M, (Num(p["Curvature"]) ?? double.NaN) / M)).ToArray();
                    double height = Req(args["height"], "H") * M;
                    if (IsError(r)) { Rejected(() => WallDisplacementPoint.Integrate(points, height)); break; }
                    var shape = WallDisplacementPoint.Integrate(points, height); var legacy = r!.AsArray();
                    Assert.AreEqual(legacy.Count, shape.Count, id);
                    for (int i = 0; i < shape.Count; i++)
                    {
                        Close(Req(legacy[i]!["Y"], "y") * M, shape[i].Height, id, 1e-12, 1e-9); Close(Req(legacy[i]!["Curvature"], "k") / M, shape[i].Curvature, id, 1e-12, 1e-18);
                        Close(Req(legacy[i]!["Rotation"], "r"), shape[i].Rotation, id, 1e-10, 1e-15); Close(Req(legacy[i]!["DisplacementMm"], "u"), shape[i].Displacement, id, 1e-10, 1e-12);
                    }
                    break;
                }
                case "DeriveSeismic":
                {
                    var s = args["seismic"]!.AsObject(); string source = Str(s["source"]);
                    if (source == "kh e kv assegnati") { Assert.IsNull(r, id); break; }
                    var method = Str(s["method"]) == "Wood semplificato" ? WallSeismicMethod.Wood : WallSeismicMethod.MononobeOkabe;
                    WallSeismic Derive() => source == "Da parametri del sito (SLV)" ? WallSeismic.FromSite(method, Site(s)) : throw new ArgumentException("Sisma: modalità di definizione non riconosciuta.");
                    if (IsError(r)) { Rejected(() => Derive()); break; }
                    var w = Derive(); var site = w.Site!;
                    Close(Req(r!["AgG"], "ag"), site.AgG, id, 1e-15); Close(Req(r["Ss"], "Ss"), site.Ss, id, 1e-14); Close(Req(r["St"], "St"), site.St, id, 1e-14);
                    Close(Req(r["AmaxG"], "amax"), site.AmaxG, id, 1e-14, 1e-16); Close(Req(r["Beta"], "β"), w.Beta, id, 1e-15);
                    Close(Req(r["Kh"], "kh"), w.Kh, id, 1e-14, 1e-16); Close(Req(r["Kv"], "kv"), w.Kv, id, 1e-14, 1e-16);
                    Close(Req(r["BetaOverturning"], "βr"), w.BetaOverturning, id, 1e-15); Close(Req(r["KhOverturning"], "khr"), w.KhOverturning, id, 1e-14, 1e-16);
                    Close(Req(r["KvOverturning"], "kvr"), w.KvOverturning, id, 1e-14, 1e-16);
                    Assert.AreEqual(Str(r["SoilFormula"]), site.SoilFormula, id); Assert.AreEqual(Str(r["TopographyFormula"]), site.TopographyFormula, id);
                    break;
                }
                default: Assert.Fail("Unknown kind " + kind); break;
            }
        }
        Assert.AreEqual(15, counts["Ka"]); Assert.AreEqual(150, counts["SeismicKa"]); Assert.AreEqual(162, counts["ActiveHorizontal"]);
        Assert.AreEqual(60, counts["ContactLaw"]); Assert.AreEqual(240, counts["Pressure"]); Assert.AreEqual(6, counts["Integrate"]); Assert.AreEqual(6, counts["IntegrateCurvature"]);
        Assert.AreEqual(2 * 6 * 4 * 3 + 8 + 5 + 2, counts["DeriveSeismic"]);
        Assert.AreEqual(98, rejected);
    }

    [TestMethod]
    public void LegacyCombinationsAreReproduced()
    {
        int generated = 0, rows = 0, rejected = 0;
        foreach (var line in Lines("walls-combinations.jsonl"))
        {
            string name = Str(line["name"]); var legacy = line["ordinary"];
            if (IsError(legacy))
            {
                if (line["input"] is not JsonObject doc) { rejected++; continue; } // Upgrade itself rejected the document.
                Throws(() => { var i = Input(doc); RetainingWallAnalysis.Calculate(i, WallCombinations.Generate(i)); }, name); rejected++; continue;
            }
            WallInput input;
            // ANTHEA generates the combinations before checking the geometry and the soils: these documents are rejected by its calculation.
            try { input = Input(line["input"]!.AsObject()); }
            catch (ArgumentException) { Assert.IsTrue(name is "errore fusto rovescio" or "errore terreno di posa gamma sat" or "errore altezza libera", name); rejected++; continue; }
            var port = WallCombinations.Generate(input); var list = legacy!.AsArray();
            Assert.AreEqual(list.Count, port.Count, name);
            for (int i = 0; i < port.Count; i++)
            {
                var c = Combination(list[i]!.AsObject()); var p = port[i]; string id = name + " " + c.Name;
                Assert.AreEqual(c.Name, p.Name, id); Assert.AreEqual(c.State, p.State, id); Assert.AreEqual(c.Approach, p.Approach, id); Assert.AreEqual(c.Purpose, p.Purpose, id);
                foreach (var (a, b, what) in new[] { (c.Wall, p.Wall, "wall"), (c.Soil, p.Soil, "soil"), (c.ValleySoil, p.ValleySoil, "valley"), (c.Water, p.Water, "water"), (c.FrictionFactor, p.FrictionFactor, "mphi"),
                    (c.SlidingFactor, p.SlidingFactor, "rslide"), (c.OverturningFactor, p.OverturningFactor, "rover"), (c.BearingFactor, p.BearingFactor, "rbearing"), (c.Kh, p.Kh, "kh"), (c.Kv, p.Kv, "kv") })
                    Close(a, b, id + " " + what, 1e-14, 1e-16);
                Assert.AreEqual(c.Coefficients.Count, p.Coefficients.Count, id);
                foreach (var (key, value) in c.Coefficients) Close(value, p.Coefficients[key], id + " " + key, 1e-14, 1e-16);
                rows++;
            }
            generated++;
        }
        Assert.AreEqual(110, generated + rejected);
        Assert.IsTrue(rows > 1500, "rows " + rows);
    }

    /// <summary>Documents rejected by ANTHEA for reasons outside the geotechnical core of W1 (structure, user interface): they are calculated here.</summary>
    static readonly HashSet<string> OutsideW1 = new()
    {
        "errore esposizione mancante", "errore tipologia futura", "errore estensioni", "errore versione 3", "errore matrice obsoleta",
        "errore gravità lunghezza efficace corta", "errore armatura 40 barre", "errore altezza 20"
    };

    [TestMethod]
    public void LegacyWallsAreReproduced()
    {
        int calculated = 0, rejected = 0, outside = 0, cases = 0, sections = 0, checks = 0, legacyNaN = 0, roundingCuts = 0;
        static bool Near(double expected, double actual, double relative, double absolute) => Math.Abs(expected - actual) <= relative * Math.Abs(expected) + absolute;
        foreach (var line in GzipLines("walls-documents.jsonl.gz"))
        {
            string name = Str(line["name"]); var doc = line["input"]!.AsObject(); var r = line["result"]!.AsObject();
            if (IsError(r))
            {
                if (OutsideW1.Contains(name)) { outside++; continue; }
                Throws(() =>
                {
                    var i = Input(doc); var rows = (Num(doc["version"]) ?? 1) >= 2 && Str(doc["combination_mode"]) == "Personalizzate"
                        ? doc["combinations"]!.AsArray().Select(c => c!.AsObject()).Where(c => Bool(c["enabled"])).Select(Combination).ToArray() : WallCombinations.Generate(i);
                    RetainingWallAnalysis.Calculate(i, rows);
                }, name);
                rejected++; continue;
            }
           var input = Input(doc); var combos = Combinations(doc, r);
            var result = RetainingWallAnalysis.Calculate(input, combos);
            bool v1 = (Num(doc["version"]) ?? 1) < 2;
            Close(Req(r["larghezza"], "B") * M, result.Width, name + " width", 1e-13);
            Close(Req(r["area"], "A") * M * M, result.Area, name + " area", 1e-12);
            var legacyCases = r["combinazioni"]!.AsArray();
            Assert.AreEqual(legacyCases.Count, result.Cases.Count, name);
            for (int k = 0; k < result.Cases.Count; k++)
            {
                var lc = legacyCases[k]!.AsObject(); var pc = result.Cases[k]; string id = name + " · " + Str(lc["Name"]);
                Assert.AreEqual(Str(lc["Name"]), pc.Combination.Name, id);
                Close(Req(lc["Horizontal"], "H"), pc.Horizontal, id + " H", 1e-9, 1e-9); Close(Req(lc["Vertical"], "V"), pc.Vertical, id + " V", 1e-9, 1e-9);
                Close(Req(lc["Uplift"], "U"), pc.Uplift, id + " U", 1e-9, 1e-9);
                Close(Req(lc["Stabilizing"], "Ms") * KNm, pc.Stabilizing, id + " Ms", 1e-9, 1e-6); Close(Req(lc["Overturning"], "Mr") * KNm, pc.Overturning, id + " Mr", 1e-9, 1e-6);
                Close(Req(lc["X"], "x") * M, pc.X, id + " x", 1e-9, 1e-6); Close(Req(lc["Eccentricity"], "e") * M, pc.Eccentricity, id + " e", 1e-9, 1e-6);
                Close(Req(lc["EffectiveWidth"], "B'") * M, pc.EffectiveWidth, id + " B'", 1e-9, 1e-6);
                var contact = lc["Contact"]!; Assert.AreEqual(Bool(contact["Valid"]), pc.Contact.Valid, id + " contact");
                Close(Req(contact["Start"], "s") * M, pc.Contact.Start, id + " start", 1e-9, 1e-6); Close(Req(contact["End"], "e") * M, pc.Contact.End, id + " end", 1e-9, 1e-6);
                Close(Req(contact["Toe"], "t") * KPa, pc.Contact.Toe, id + " toe", 1e-9, 1e-12); Close(Req(contact["Heel"], "h") * KPa, pc.Contact.Heel, id + " heel", 1e-9, 1e-12);
                Close(Req(lc["SlidingResistance"], "S") , pc.SlidingResistance, id + " sliding", 1e-9, 1e-9);
                Close(Req(lc["OverturningResistance"], "O") * KNm, pc.OverturningResistance, id + " overturning", 1e-9, 1e-6);
                double? bearing = Num(lc["BearingResistance"]);
                Assert.AreEqual(bearing.HasValue, pc.BearingResistance.HasValue, id + " bearing");
                if (bearing.HasValue) Close(bearing.Value, pc.BearingResistance!.Value, id + " bearing", 1e-9, 1e-9);
                Assert.AreEqual(Str(lc["SeismicBearingError"]).Length > 0, pc.SeismicBearingError.Length > 0, id + " seismic bearing error " + Str(lc["SeismicBearingError"]) + " / " + pc.SeismicBearingError);
                if (lc["SeismicBearing"] is JsonObject sb)
                {
                    Assert.IsNotNull(pc.SeismicBearing, id);
                    Close(Req(sb["Capacity"], "cap"), pc.SeismicBearing!.Capacity, id + " Annex F", 1e-9, 1e-9); Close(Req(sb["NMax"], "Nmax"), pc.SeismicBearing.NMax, id + " Nmax", 1e-9, 1e-9);
                    Close(Req(sb["SoilInertia"], "F"), pc.SeismicBearing.SoilInertia, id + " F", 1e-12, 1e-15);
                }
                Segments(lc["Pressures"]!.AsArray(), pc.Pressures, id + " pressures"); Segments(lc["StemPressures"]!.AsArray(), pc.StemPressures, id + " stem pressures");
                Segments(lc["ValleyPressures"]!.AsArray(), pc.ValleyPressures, id + " valley pressures");
                Details(lc["PressureDetails"]!.AsArray(), pc.PressureDetails, id + " details"); Details(lc["StemPressureDetails"]!.AsArray(), pc.StemPressureDetails, id + " stem details");
                var audit = lc["SoilAudit"]!;
                Close(Req(audit["Dv_m"], "Dv") * M, pc.Soil.ValleyHeight, id + " Dv", 1e-12, 1e-9); Close(Req(audit["peso_valle_kN_m"], "Wv"), pc.Soil.ValleyWeight, id + " Wv", 1e-9, 1e-9);
                Close(Req(audit["momento_peso_valle_kNm_m"], "Mv") * KNm, pc.Soil.ValleyMoment, id + " Mv", 1e-9, 1e-6); Close(Req(audit["q_ricoprimento_kPa"], "q'") * KPa, pc.Soil.Overburden, id + " q'", 1e-9, 1e-12);
                Close(Req(audit["delta_muro_d_gradi"], "δ") * Deg, pc.Soil.WallFriction, id + " δ", 1e-12, 1e-15); Close(Req(audit["delta_base_d_gradi"], "δb") * Deg, pc.Soil.BaseFriction, id + " δb", 1e-12, 1e-15);
                Close(Req(audit["passiva_disponibile_kN_m"], "Pp") , pc.Soil.PassiveAvailable, id + " Pp", 1e-9, 1e-9); Close(Req(audit["passiva_limite_equilibrio"], "ηeq"), pc.Soil.PassiveScale, id + " ηeq", 1e-9, 1e-12);
                Close(Req(audit["Nq"], "Nq"), pc.Soil.Nq, id + " Nq", 1e-12); Close(Req(audit["Ngamma"], "Nγ"), pc.Soil.Ngamma, id + " Nγ", 1e-12);
                Close(Req(audit["iq"], "iq"), pc.Soil.Iq, id + " iq", 1e-9, 1e-12); Assert.AreEqual(Bool(audit["base_ruvida"]), pc.Soil.RoughBase, id + " rough");
                if (!v1) Assert.AreEqual(lc["Actions"]!.AsArray().Count, pc.Actions.Count, id + " actions");
                // Internal forces: every legacy cut (positions in m) has the cut of the port; version 1 has only the full length of toe and heel.
                var legacySections = lc["Sections"]!.AsArray();
                foreach (var ls in legacySections)
                {
                    var member = Member(Str(ls!["Name"])); double position = Req(ls["Position"], "z") * M;
                    var ps = pc.Sections.FirstOrDefault(p => p.Member == member && Math.Abs(p.Position - position) <= 1e-6 + 1e-12 * position);
                    Assert.IsNotNull(ps, id + " cut " + member + " " + position);
                    bool nan = Str(ls["M"]) == "NaN";
                    // Tolerances of the forces and of the moment; looser for the cut 1e-4 mm above a load (the pressures act on 1e-4 mm more).
                    double tf = 1e-9, tm = 1e-5;
                    bool Same(WallSectionForce p, double f, double m) => Near(Req(ls["N"], "N"), p.N, 1e-9, f) && Near(Req(ls["V"], "V"), p.V, 1e-9, f)
                        && (nan || Near(Req(ls["M"], "M") * KNm, p.M, 1e-9, m));
                    if (!Same(ps!, tf, tm) && member == WallMember.Stem)
                    {
                        // At the elevation of a concentrated action ANTHEA includes it or not by the rounding of ht − z in m (3.45 − 3.0 =
                        // 0.4500000000000002): its value is the one just above the action, the cut of the port at −1e-4 mm.
                        var above = pc.Sections.FirstOrDefault(p => p.Member == member && Math.Abs(p.Position - (position - 1e-4)) <= 1e-6 + 1e-12 * position);
                        if (above != null && Same(above, 1e-4, .05) && input.Actions.Any(x => x.Enabled && x.Type != WallActionType.UniformSurcharge && Math.Abs(input.Geometry.TotalHeight - x.Z - position) <= 1e-6))
                        { ps = above; roundingCuts++; tf = 1e-4; tm = .05; }
                    }
                    Close(Req(ls["Thickness"], "t") * M, ps.Thickness, id + " thickness", 1e-9, 1e-5); Close(Req(ls["N"], "N"), ps.N, id + " N " + member + " " + position, 1e-9, tf);
                    if (nan) { legacyNaN++; Assert.IsTrue(double.IsFinite(ps.M), id + " finite M"); }
                    else Close(Req(ls["M"], "M") * KNm, ps.M, id + " M " + member + " " + position, 1e-9, tm);
                    Close(Req(ls["V"], "V"), ps.V, id + " V " + member + " " + position, 1e-9, tf);
                    sections++;
                }
                if (!v1)
                    foreach (var ps in pc.Sections)
                        Assert.IsTrue(legacySections.Any(ls => Member(Str(ls!["Name"])) == ps.Member && Math.Abs(Req(ls["Position"], "z") * M - ps.Position) <= 1e-6 + 1e-12 * ps.Position), id + " extra cut " + ps.Member + " " + ps.Position);
                cases++;
            }
            var legacyChecks = r["verifiche_geotecniche"]!.AsArray().Where(c => CheckKind(Str(c!["Name"])) != null && result.Cases.Any(x => x.Combination.Name == Str(c!["Combination"]))).ToArray();
            Assert.AreEqual(legacyChecks.Length, result.Checks.Count, name + " checks");
            for (int k = 0; k < legacyChecks.Length; k++)
            {
                var lk = legacyChecks[k]!; var pk = result.Checks[k]; string id = name + " · " + Str(lk["Combination"]) + " " + Str(lk["Name"]);
                Assert.AreEqual(CheckKind(Str(lk["Name"])), pk.Kind, id); Assert.AreEqual(Str(lk["Combination"]), pk.Combination, id);
                double scale = pk.Kind == WallCheckKind.Overturning ? KNm : pk.Kind == WallCheckKind.Contact ? M : 1;
                Close(Req(lk["Demand"], "D") * scale, pk.Demand, id + " demand", 1e-9, 1e-6);
                double? resistance = Num(lk["Resistance"]); Assert.AreEqual(resistance.HasValue, pk.Resistance.HasValue, id + " resistance");
                if (resistance.HasValue) Close(resistance.Value * scale, pk.Resistance!.Value, id + " resistance", 1e-9, 1e-6);
                double? ratio = Num(lk["Ratio"]); Assert.AreEqual(ratio.HasValue, pk.Ratio.HasValue, id + " ratio");
                if (ratio.HasValue) Close(ratio.Value, pk.Ratio!.Value, id + " ratio", 1e-9, 1e-12);
                Assert.AreEqual(CheckStatus(Str(lk["Status"])), pk.Status, id + " status " + Str(lk["Status"]));
                checks++;
            }
            calculated++;
        }
        Assert.AreEqual(110, calculated + rejected + outside);
        Assert.AreEqual(88, calculated); Assert.AreEqual(14, rejected); Assert.AreEqual(8, outside);
        Assert.AreEqual(35, legacyNaN, "legacy defect: NaN moments of the stem with the valley water");
        TestContext.WriteLine($"{calculated} walls, {cases} combinations, {sections} cuts, {checks} checks; {roundingCuts} cuts at the loads decided by the rounding of ANTHEA, {legacyNaN} NaN of ANTHEA");
        Assert.IsTrue(cases > 2000 && sections > 100000 && checks > 5000, $"{cases} cases, {sections} cuts, {checks} checks");
    }

    static void Segments(JsonArray legacy, IReadOnlyList<WallPressureSegment> port, string id)
    {
        var a = legacy.Where(p => Req(p!["Z1"], "z1") - Req(p["Z0"], "z0") > 1e-9).ToArray(); var b = port.Where(p => p.Z1 - p.Z0 > 1e-6).ToArray();
        Assert.AreEqual(a.Length, b.Length, id + " count");
        for (int i = 0; i < a.Length; i++)
        {
            Close(Req(a[i]!["Z0"], "z0") * M, b[i].Z0, id + " z0", 1e-12, 1e-6); Close(Req(a[i]!["Z1"], "z1") * M, b[i].Z1, id + " z1", 1e-12, 1e-6);
            Close(Req(a[i]!["P0"], "p0") * KPa, b[i].P0, id + " p0", 1e-9, 1e-12); Close(Req(a[i]!["P1"], "p1") * KPa, b[i].P1, id + " p1", 1e-9, 1e-12);
        }
    }

    static void Details(JsonArray legacy, IReadOnlyList<WallPressureDetail> port, string id)
    {
        var a = legacy.Where(p => Req(p!["Z1"], "z1") - Req(p["Z0"], "z0") > 1e-9).ToArray(); var b = port.Where(p => p.Z1 - p.Z0 > 1e-6).ToArray();
        Assert.AreEqual(a.Length, b.Length, id + " count");
        for (int i = 0; i < a.Length; i++)
        {
            var l = a[i]!; var p = b[i];
            Close(Req(l["Phi"], "φ") * Deg, p.FrictionAngle, id + " φ", 1e-12); Close(Req(l["PhiDesign"], "φd") * Deg, p.DesignFrictionAngle, id + " φd", 1e-12);
            Close(Req(l["K"], "K"), p.K, id + " K", 1e-12); Close(Req(l["Ke"], "Ke"), p.Ke, id + " Ke", 1e-12);
            foreach (var (key, value) in new[] { ("Sigma0", p.Sigma0), ("Sigma1", p.Sigma1), ("Surcharge", p.Surcharge), ("Water0", p.Water0), ("Water1", p.Water1), ("Dynamic", p.Dynamic), ("Total0", p.Total0), ("Total1", p.Total1) })
                Close(Req(l[key], key) * KPa, value, id + " " + key, 1e-9, 1e-12);
        }
    }
}
