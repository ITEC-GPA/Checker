using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.PilesFixture;

namespace GeotechnicsTests;

/// <summary>
/// Piles and micropiles moved from ANTHEA (Nq, BustamanteDoix, Chs, GeometriaMicropalo, MicropaloOrizzontale, PaloOrizzontale with .Stratified and
/// .Diagnostics, the engine of Calcolo; sources of commit fe4652c, captured by the harness at 2663c96). Every legacy output is reproduced with the
/// typed inputs built on Model types (SoilProfile, Soil, SectionCHS, SteelMaterial, the Model standards) after the conversion of the units.
/// </summary>
[TestClass]
public class PilesMigrationTests
{
    [TestMethod]
    public void LegacyNqFactorsAreReproduced()
    {
        int details = 0, curves = 0, errors = 0;
        foreach (var line in Lines("piles-nq.jsonl"))
        {
            var r = line["result"];
            if (Str(line["kind"]) == "curve")
            {
                Close(Req(line["value"], "value"), BearingCapacityFactors.Curve(Req(line["phi"], "phi"), (int)Req(line["index"], "i"), Bool(line["large"])), "curve", 1e-14, 1e-14); curves++; continue;
            }
            double phi = line["phiText"] != null ? double.Parse(Str(line["phiText"]), System.Globalization.CultureInfo.InvariantCulture) : Req(line["phi"], "phi");
            double ratio = line["ratioText"] != null ? double.Parse(Str(line["ratioText"]), System.Globalization.CultureInfo.InvariantCulture) : Req(line["ratio"], "ratio");
            bool large = Bool(line["large"]); string id = $"Nq φ={phi} z/D={ratio} large={large}";
            if (IsError(r)) { Assert.ThrowsException<ArgumentException>(() => BearingCapacityFactors.Nq(phi * Deg, ratio, large), id); errors++; continue; }
            var n = BearingCapacityFactors.Nq(phi * Deg, ratio, large);
            Assert.AreEqual(Str(r!["fattore"]), n.Factor, id); Close(Req(r["phi"], "phi"), n.FrictionAngleDegrees, id + " φ", 1e-12, 1e-12);
            Close(Req(r["rapporto_adottato"], "r"), n.AdoptedSlenderness, id, 1e-15); Assert.AreEqual(Req(r["r1"], "r1"), n.Ratio1, id); Assert.AreEqual(Req(r["r2"], "r2"), n.Ratio2, id);
            Close(Req(r["phi1"], "p1"), n.FrictionAngle1, id + " φ1", 1e-12, 1e-12); Close(Req(r["phi2"], "p2"), n.FrictionAngle2, id + " φ2", 1e-12, 1e-12);
            Close(Req(r["n1"], "n1"), n.Value1, id + " n1", 1e-11); Close(Req(r["n2"], "n2"), n.Value2, id + " n2", 1e-11);
            Close(Req(r["t"], "t"), n.Weight, id + " t", 1e-12, 1e-14); Close(Req(r["nq"], "nq"), n.Nq, id + " Nq", 1e-11);
            Assert.AreEqual(Bool(r["limite_phi"]), n.FrictionAngleClipped, id + " clipped φ"); Assert.AreEqual(Bool(r["limite_rapporto"]), n.SlendernessClipped, id + " clipped z/D");
            details++;
        }
        Assert.AreEqual(2 * 25 * 16, details); Assert.AreEqual(5, errors); Assert.AreEqual(4 * 101 + 2 * 81, curves);
    }

    [TestMethod]
    public void LegacyBustamanteDoixIsReproduced()
    {
        int ranges = 0, parameters = 0, segments = 0, cosines = 0, unrepresentable = 0;
        foreach (var line in Lines("piles-bustamante-doix.jsonl"))
        {
            var r = line["result"]; string kind = Str(line["kind"]);
            if (kind == "cosine")
            {
                double? theta = Num(line["theta"]);
                if (IsError(r)) Assert.ThrowsException<ArgumentException>(() => MicropileTube.AxisCosine((theta ?? double.NaN) * Deg), "θ " + theta);
                else Close(Req(r, "cos"), MicropileTube.AxisCosine(theta!.Value * Deg), "cos " + theta, 1e-15);
                cosines++; continue;
            }
            if (kind == "segments")
            {
                var layers = line["layers"]!.AsArray().Select(l => new MicropileLayer(Req(l!["top"], "top") * M, Req(l["bottom"], "bottom") * M, BdSoils[Str(l["values"]!["terreno"])],
                    Req(l["values"]!["alpha"], "α"), Bool(l["values"]!["laterale_attiva"], true))).ToArray();
                var injection = Enum.Parse<MicropileInjection>(Str(line["injection"])); string sid = "BD segments " + line.ToJsonString().Substring(0, 120);
                MicropileShaftSegment[] Run() => BustamanteDoix.Segments(layers, Req(line["tip"], "tip") * M, Req(line["start"], "start") * M, Req(line["diameter"], "D") * M, injection, Req(line["pressure"], "p"));
                if (IsError(r)) { Assert.ThrowsException<ArgumentException>(Run, sid); segments++; continue; }
                var s = Run(); var expected = r!.AsArray(); Assert.AreEqual(expected.Count, s.Length, sid);
                for (int i = 0; i < s.Length; i++)
                {
                    var e = expected[i]!; string id = sid + " #" + i;
                    Assert.AreEqual((int)Req(e["strato"], "strato"), s[i].Layer, id); Close(Req(e["cielo"], "top") * M, s[i].Top, id, 1e-12, 1e-9); Close(Req(e["fondo"], "bottom") * M, s[i].Bottom, id, 1e-12, 1e-9);
                    Close(Req(e["s"], "s") * KPa, s[i].UnitResistance, id + " s", 1e-12, 1e-15); Close(Req(e["laterale"], "lateral") * 1000, s[i].Lateral, id + " lateral", 1e-12, 1e-9);
                    Assert.AreEqual(e["laterale_attiva"] == null || Bool(e["laterale_attiva"], true), s[i].ShaftActive, id);
                    if (s[i].ShaftActive)
                    {
                        Assert.AreEqual(Str(e["curva"]), s[i].Shaft!.Curve, id); Close(Req(e["ds"], "ds") * M, s[i].DrillDiameter!.Value, id + " ds", 1e-12);
                        var range = e["alpha_consigliato"]!.AsArray(); Assert.AreEqual((Req(range[0], "a"), Req(range[1], "b")), s[i].Shaft!.RecommendedAlpha, id);
                    }
                }
                segments++; continue;
            }
            string soilName = Str(line["soil"]), injectionName = Str(line["injection"]);
            if (!BdSoils.ContainsKey(soilName) || injectionName == "IGX") { Assert.IsTrue(IsError(r), "an unknown soil or injection is rejected by ANTHEA"); unrepresentable++; continue; }
            var soil = BdSoils[soilName]; var inj = Enum.Parse<MicropileInjection>(injectionName);
            if (kind == "range")
            {
                var a = BustamanteDoix.AlphaRange(soil, inj); Assert.AreEqual(Req(r![0], "min"), a.Min); Assert.AreEqual(Req(r[1], "max"), a.Max); ranges++; continue;
            }
            double pressure = Num(line["pressure"]) ?? double.NaN, alpha = Req(line["alpha"], "α"); string pid = $"BD {soilName} {injectionName} p={pressure} α={alpha}";
            if (IsError(r)) { Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.UnitShaftResistance(soil, inj, pressure, alpha), pid); parameters++; continue; }
            var shaft = BustamanteDoix.UnitShaftResistance(soil, inj, pressure, alpha);
            Assert.AreEqual(Str(r!["curva"]), shaft.Curve, pid); Close(Req(r["s"], "s") * KPa, shaft.UnitResistance, pid, 1e-12, 1e-15); Assert.AreEqual(alpha, shaft.Alpha);
            parameters++;
        }
        Assert.AreEqual(26, ranges); Assert.AreEqual(26 * 19 * 2, parameters); Assert.AreEqual(4 * 2 * 3 * 5, segments); Assert.AreEqual(8, cosines);
        Assert.AreEqual(14 * 3 * 39 - 26 * 39, unrepresentable, "Torba with every injection and IGX with every soil: ranges and parameters");
    }

    [TestMethod]
    public void LegacyTubeWeightsAndSectionsAreReproduced()
    {
        int weights = 0, sections = 0, skipped = 0;
        foreach (var line in Lines("piles-chs.jsonl"))
        {
            var r = line["result"];
            if (Str(line["kind"]) == "weight")
            {
                string profile = Str(line["profile"]); double? drill = Num(line["diameter"]); double gamma = Req(line["gamma"], "γ");
                if (line["D"] == null) { if (profile.Contains("999")) skipped++; else Assert.ThrowsException<ArgumentException>(() => MicropileTube.Weight(new SectionCHS(139.7, 8), TubeSteel, (drill ?? double.NaN) * M, gamma * KN3)); weights++; continue; }
                var tube = new SectionCHS(Req(line["D"], "D"), Req(line["t"], "t")); string id = profile + " D=" + drill + " γ=" + gamma;
                if (IsError(r)) { Assert.ThrowsException<ArgumentException>(() => MicropileTube.Weight(tube, TubeSteel, drill!.Value * M, gamma * KN3), id); weights++; continue; }
                var w = MicropileTube.Weight(tube, TubeSteel, drill!.Value * M, gamma * KN3);
                Close(Req(r!["area_acciaio"], "As") * M * M, w.SteelArea, id + " As", 1e-12); Close(Req(r["area_cls"], "Ac") * M * M, w.GroutArea, id + " Ac", 1e-12);
                Close(Req(r["q_acciaio"], "qs"), w.Steel, id + " qs", 1e-12); Close(Req(r["q_cls"], "qc"), w.Grout, id + " qc", 1e-12); Close(Req(r["q_totale"], "q"), w.Total, id + " q", 1e-12);
                weights++; continue;
            }
            string mode = Str(line["mode"]); string profileName = Str(line["profile"]);
            if (mode == "Altro" || profileName.Contains("999")) { Assert.IsTrue(IsError(r)); skipped++; continue; }
            var (d, t) = mode == "Catalogo" ? Designation(profileName) : (Req(line["D"], "D"), Req(line["t"], "t"));
            double fy = Req(line["fy"], "fy"), gammaM0 = Req(line["gammaM0"], "γM0"), drillDiameter = Req(line["drill"], "drill") * M; double? axial = Num(line["axial"]);
            var standard = new StandardNTC2018Steel { GammaM0 = gammaM0 }; var steel = new SteelMaterial("fy" + fy, 210000, fy, fy * 1.2);
            string sid = $"{mode} {profileName} D={d} t={t} fy={fy} N={axial}";
            if (2 * t >= d) { Assert.IsTrue(IsError(r)); Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(new SectionCHS(d, t), steel, standard, 0, drillDiameter)); sections++; continue; }
            var tube2 = new SectionCHS(d, t); var p = line["properties"];
            if (!IsError(p))
            {
                Close(Req(p!["area_mm2"], "A"), tube2.Area, sid + " A", 1e-12); Close(Req(p["wpl_mm3"], "Wpl"), tube2.Wpl1, sid + " Wpl", 1e-12); Close(Req(p["inerzia_mm4"], "I"), tube2.J11, sid + " I", 1e-12);
                Assert.AreEqual((int)Req(p["classe"], "class"), MicropileTube.SectionClass(tube2, fy), sid + " class");
            }
            MicropileTubeResistance Run() => MicropileTube.LateralResistance(tube2, steel, standard, (axial ?? double.NaN) * 1000, drillDiameter);
            if (IsError(r)) { Assert.ThrowsException<ArgumentException>(Run, sid); sections++; continue; }
            var res = Run();
            Close(Req(r!["fyd_mpa"], "fyd"), res.DesignYieldStrength, sid + " fyd", 1e-15); Close(Req(r["npl_kn"], "Npl") * 1000, res.PlasticAxial, sid + " Npl", 1e-12);
            Close(Req(r["mpl_knm"], "Mpl") * 1e6, res.PlasticMoment, sid + " Mpl", 1e-12); Close(Req(r["momento_knm"], "My") * 1e6, res.ResistingMoment, sid + " My", 1e-12);
            Assert.AreEqual(1, res.SectionClass); sections++;
        }
        Assert.AreEqual(64 * 6 + 4, weights); Assert.AreEqual(7 * 4 * 4 + 8 + 1, sections); Assert.AreEqual(1 + 2, skipped);
    }
}
