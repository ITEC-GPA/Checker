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
/// Transverse capacity (PaloOrizzontale) and axial capacity of piles and micropiles (engine of Calcolo) of ANTHEA reproduced on the typed inputs:
/// SoilProfile and Soil of Model, factors of StandardNTC2018Geotechnics (ξ, γR = 1.3, γb, γs, γst, γG as the legacy defaults).
/// </summary>
[TestClass]
public class PileCapacityMigrationTests
{
    private static readonly StandardNTC2018Geotechnics Ntc = new();

    // ---------------------------------------------------------------- Transverse capacity

    private static (LateralPile Pile, LateralPileSurvey[] Surveys, LateralPileFactors Factors, LateralGroupEfficiency Efficiency) Lateral(JsonObject data)
    {
        var g = data["generali"]!; var v = data["verifica"]!;
        bool water = Bool(g["presenza_falda"]); double? zw = water ? Req(g["profondita_falda"], "zw") : null;
        var surveys = data["stratigrafie"]!.AsArray().Select(s => new LateralPileSurvey(Profile(s!.AsArray(), zw, PileSoil), s.AsArray().Select(l => Behaviour(l!.AsObject())))).ToArray();
        double d = Req(g["diametro"], "D") * M; double my;
        if (Str(data["tipo_sezione"]) == "CHS")
        {
            var s = data["sezione"]!; var tube = new SectionCHS(Req(s["diametro_chs_mm"], "D"), Req(s["spessore_chs_mm"], "t"));
            var steel = new SteelMaterial("S", 210000, Req(s["fy_chs_mpa"], "fy"), 510);
            my = MicropileTube.LateralResistance(tube, steel, new StandardNTC2018Steel { GammaM0 = Req(s["gamma_m0"], "γM0") }, Req(g["azione_assiale"], "N") * 1000, d).ResistingMoment;
        }
        else my = Req(g["momento_resistente"], "My") * 1e6;
        string method = Str(g["metodo_calcolo"]);
        var pile = new LateralPile(d, Req(g["lunghezza"], "L") * M, Req(g["eccentricita"], "e") * M, Str(g["vincolo"]) == "Impedita", my, Str(g["provenienza_momento"]) == "" && Str(data["tipo_sezione"]) == "CHS" ? "CHS" : Str(g["provenienza_momento"]),
            method == "Broms" ? LateralPileMethod.Broms : method.StartsWith("Stratificato") ? LateralPileMethod.Stratified : (LateralPileMethod)99,
            Req(g["azione_orizzontale"], "H") * 1000, Req(g["passo"], "step") * M, Req(g["tolleranza"], "tol"));
        var factors = LateralPileFactors.FromStandard(Ntc, Profiles(Str(v["verticali_indagate"])));
        var efficiency = Str(v["efficienza_metodo"]) == "Reese & Van Impe (foglio)"
            ? LateralGroupEfficiency.ReeseVanImpeEfficiency(d, Req(v["interasse_anteriore"], "a") * M, Req(v["interasse_posteriore"], "p") * M, Req(v["interasse_sinistro"], "s") * M, Req(v["interasse_destro"], "d") * M)
            : LateralGroupEfficiency.Manual(Num(v["efficienza_eta"]) ?? 1);
        return (pile, surveys, factors, efficiency);
    }

    private static readonly Dictionary<string, LateralMechanism> Mechanisms = new() { ["Corto"] = LateralMechanism.Short, ["Intermedio"] = LateralMechanism.Intermediate, ["Lungo"] = LateralMechanism.Long };
    private static readonly Dictionary<string, DiagramSide> Sides = new() { ["prima"] = DiagramSide.Before, ["dopo"] = DiagramSide.After, ["nodo"] = DiagramSide.Node };

    [TestMethod]
    public void LegacyLateralCapacitiesAreReproduced()
    {
        int solved = 0, rejected = 0, notRepresentable = 0;
        foreach (var line in Lines("piles-lateral.jsonl"))
        {
            string name = Str(line["name"]); var data = line["input"]!.AsObject(); var r = line["result"]!.AsObject();
            // Inputs that the typed API cannot express (unknown names, inactive layers, an independent moment) or that Model rejects itself.
            if (name is "tipologia ignota" or "metodo ignoto" or "strato disattivato" or "vincolo ignoto" or "momento applicato") { Assert.IsTrue(IsError(r), name); notRepresentable++; continue; }
            if (name == "gamma sat sotto acqua") { Assert.ThrowsException<ArgumentOutOfRangeException>(() => Lateral(data), "Model soil: γsat ≥ γ"); notRepresentable++; continue; }
            if (name == "verticali ignote") { Assert.IsTrue(IsError(r)); Assert.AreEqual(Ntc.PileCorrelationFactors(5), Ntc.PileCorrelationFactors(6), "Model: 6 profiles use the column of 5 (safe side)"); notRepresentable++; continue; }
            if (IsError(r)) { Assert.ThrowsException<ArgumentException>(() => { var x = Lateral(data); LateralPileCapacity.Calculate(x.Pile, x.Surveys, x.Factors, x.Efficiency); }, name); rejected++; continue; }
            var input = Lateral(data); var p = LateralPileCapacity.Calculate(input.Pile, input.Surveys, input.Factors, input.Efficiency);
            Close(Req(r["capacita_kn"], "H") * 1000, p.Capacity, name + " H", 1e-9);
            Assert.AreEqual((int)Req(r["sondaggio_governante"], "gov"), p.GoverningSurvey, name); Assert.AreEqual(Mechanisms[Str(r["meccanismo"])], p.Mechanism, name);
            Close(Req(r["capacita_media_kn"], "mean") * 1000, p.MeanCapacity, name + " mean", 1e-9);
            Assert.AreEqual(Req(r["xi3"], "ξ3"), p.Factors.Xi3); Assert.AreEqual(Req(r["xi4"], "ξ4"), p.Factors.Xi4); Assert.AreEqual(Req(r["gamma_r"], "γR"), p.Factors.ResistanceFactor);
            Close(Req(r["efficienza"]!["eta"], "η"), p.Efficiency.Eta, name + " η", 1e-13);
            if (p.Efficiency.ReeseVanImpe)
                foreach (var (key, value) in new[] { ("anteriore", p.Efficiency.Front), ("posteriore", p.Efficiency.Back), ("sinistro", p.Efficiency.Left), ("destro", p.Efficiency.Right),
                    ("anteriore_sinistro", p.Efficiency.FrontLeft), ("anteriore_destro", p.Efficiency.FrontRight), ("posteriore_sinistro", p.Efficiency.BackLeft), ("posteriore_destro", p.Efficiency.BackRight) })
                    Close(Req(r["efficienza"]![key], key), value, name + " " + key, 1e-13);
            Close(Req(r["ramo_media_kn"], "mean branch") * 1000, p.MeanBranch, name, 1e-9); Close(Req(r["ramo_minimo_kn"], "min branch") * 1000, p.MinimumBranch, name, 1e-9);
            Assert.AreEqual(Str(r["criterio_governante"]) == "Media / ξ3", p.MeanGoverns, name);
            Close(Req(r["resistenza_caratteristica_manuale_kn"], "Rk") * 1000, p.CharacteristicResistance, name + " Rk", 1e-9);
            Close(Req(r["resistenza_progetto_manuale_kn"], "Rd") * 1000, p.DesignResistance, name + " Rd", 1e-9);
            Close(Req(r["rapporto_meccanico"], "ratio"), p.MechanicalRatio, name, 1e-9); Close(Req(r["utilizzo_manuale"], "use"), p.Utilization, name, 1e-9);
            Assert.AreEqual(Str(r["esito_manuale"]) == "Verifica soddisfatta", p.Satisfied, name);
            Assert.AreEqual(Str(r["modello_adottato"]), p.ModelName, name); Assert.AreEqual(Bool(r["sperimentale"]), p.Experimental, name);
            Assert.AreEqual(Str(r["versione_motore"]), p.EngineVersion, name);
            var surveys = r["sondaggi"]!.AsArray(); Assert.AreEqual(surveys.Count, p.Surveys.Count, name);
            for (int k = 0; k < surveys.Count; k++) SameSurvey(surveys[k]!.AsObject(), p.Surveys[k], name + " #" + k, input.Pile.Diameter);
            solved++;
        }
        Assert.AreEqual(126, solved); Assert.AreEqual(12, rejected); Assert.AreEqual(7, notRepresentable);
    }

    private static void SameSurvey(JsonObject e, LateralSurveyResult s, string id, double d)
    {
        Close(Req(e["capacita_kn"], "H") * 1000, s.Capacity, id + " H", 1e-9); Assert.AreEqual(Mechanisms[Str(e["meccanismo"])], s.Mechanism, id);
        Assert.AreEqual(Bool(e["coesivo"]), s.Cohesive, id); Assert.AreEqual(Bool(e["misto"]), s.Mixed, id);
        Assert.AreEqual(Str(e["chiusura_equilibrio"]) == "Distribuita", s.DistributedClosure, id); Assert.AreEqual(Str(e["modello_adottato"]), s.ModelName, id);
        var candidates = e["candidati"]!.AsArray(); Assert.AreEqual(candidates.Count, s.Candidates.Count, id);
        for (int i = 0; i < candidates.Count; i++)
        {
            Assert.AreEqual(Mechanisms[Str(candidates[i]!["meccanismo"])], s.Candidates[i].Mechanism, id); Assert.AreEqual(Bool(candidates[i]!["governante"]), s.Candidates[i].Governing, id);
            var c = Num(candidates[i]!["capacita_kn"]);
            if (c.HasValue) Close(c.Value * 1000, s.Candidates[i].Capacity!.Value, id + " candidate", 1e-9); else Assert.IsNull(s.Candidates[i].Capacity, id);
        }
        Close(Req(e["momento_testa_knm"], "m0") * 1e6, s.HeadMoment, id + " M0", 1e-9, 1e-3); Close(Req(e["momento_massimo_knm"], "Mmax") * 1e6, s.MaximumMoment, id + " Mmax", 1e-9);
        Close(Req(e["quota_momento_massimo_m"], "zM") * M, s.MaximumMomentDepth, id + " zMmax", 1e-8, 1e-6); Close(Req(e["quota_taglio_nullo_m"], "zV") * M, s.ZeroShearDepth, id + " zV", 1e-8, 1e-6);
        Close(Req(e["fine_reazioni_m"], "end") * M, s.ReactionEnd, id + " end", 1e-8, 1e-6); Close(Req(e["risultante_concentrata_kn"], "F") * 1000, s.ConcentratedResultant, id + " F", 1e-7, 1e-3);
        var change = Num(e["inversione_reazioni_m"]);
        if (change.HasValue) Close(change.Value * M, s.ReversalDepth!.Value, id + " change", 1e-8, 1e-6); else Assert.IsNull(s.ReversalDepth, id);
        var hinges = e["cerniere_m"]!.AsArray(); Assert.AreEqual(hinges.Count, s.Hinges.Count, id);
        for (int i = 0; i < hinges.Count; i++) Close(Req(hinges[i], "hinge") * M, s.Hinges[i], id + " hinge", 1e-8, 1e-6);
        double q = s.LimitDiagram.Sum(x => x.Resultant);
        Assert.IsTrue(Math.Abs(s.ResidualForce) <= 1e-5 * Math.Max(1, q) && Math.Abs(Req(e["residuo_forza_kn"], "rF") * 1000) <= 1e-5 * Math.Max(1, q), id + " residual force");
        Assert.AreEqual(Bool(e["tensioni_disponibili"]), s.StressesAvailable, id);
        var limit = e["diagramma_limite"]!.AsArray(); Assert.AreEqual(limit.Count, s.LimitDiagram.Count, id + " limit diagram");
        for (int i = 0; i < limit.Count; i++)
        {
            var x = limit[i]!; var y = s.LimitDiagram[i]; string lid = id + " segment " + i;
            Assert.AreEqual((int)Req(x["strato"], "layer"), y.Layer, lid); Assert.AreEqual(Str(x["tipologia"]) == "Coesivo", y.Behaviour == SoilBehaviour.Cohesive, lid);
            Close(Req(x["da_m"], "top") * M, y.Top, lid, 1e-12, 1e-9); Close(Req(x["a_m"], "bottom") * M, y.Bottom, lid, 1e-12, 1e-9);
            Close(Req(x["p_iniziale_kn_m"], "p0"), y.InitialReaction, lid + " p0", 1e-10, 1e-10); Close(Req(x["pendenza_kn_m2"], "k") * 1e-3, y.Slope, lid + " k", 1e-10, 1e-13);
            Close(Req(x["risultante_kn"], "Q") * 1000, y.Resultant, lid + " Q", 1e-10, 1e-7); Close(Req(x["momento_primo_knm"], "S") * 1e6, y.FirstMoment, lid + " S", 1e-10, 1e-4);
            var sigma = Num(x["sigma_eff_iniziale_kpa"]);
            if (sigma.HasValue) Close(sigma.Value * KPa, y.EffectiveStressAtTop!.Value, lid + " σ'", 1e-10, 1e-13); else Assert.IsNull(y.EffectiveStressAtTop, lid);
        }
        SameRows(e["diagrammi"]!.AsArray(), s.Diagram.Select(x => (x.Depth, x.Side, new[] { x.Reaction, x.Shear, x.Moment, x.Pressure })).ToArray(),
            row => new[] { Req(row["p_kn_m"], "p"), Req(row["v_kn"], "V") * 1000, Req(row["m_knm"], "M") * 1e6, Req(row["q_kpa"], "q") * KPa },
            new[] { 1e-7, 1e-3, 1.0, 1e-10 }, id + " diagram");
        SameRows(e["diagramma_terreno"]!.AsArray(), s.Ground.Select(x => (x.Depth, x.Side, new[] { x.TotalStress ?? double.NaN, x.PorePressure, x.EffectiveStress ?? double.NaN, x.LimitPressure, x.LimitReaction, x.IntegratedReaction, x.Layer })).ToArray(),
            row => new[] { Num(row["sigma_v_kpa"]) * KPa ?? double.NaN, Req(row["u_kpa"], "u") * KPa, Num(row["sigma_eff_kpa"]) * KPa ?? double.NaN, Req(row["q_lim_kpa"], "q") * KPa,
                Req(row["p_lim_kn_m"], "p"), Req(row["q_integrale_kn"], "Q") * 1000, Req(row["strato"], "layer") },
            new[] { 1e-12, 1e-12, 1e-12, 1e-10, 1e-7, 1e-3, 0 }, id + " ground");
    }

    /// <summary>
    /// Rows by depth and side: every row of the port has a legacy row at the same depth (1e-6 mm) and side; every legacy row has a port row,
    /// except those within 1e-9 m of another legacy depth (a step sample that coincides with an interface only in mm).
    /// </summary>
    private static void SameRows(JsonArray legacy, (double Depth, DiagramSide Side, double[] Values)[] port, Func<JsonNode, double[]> values, double[] absolute, string id)
    {
        var rows = legacy.Select(r => (Depth: Req(r!["z"], "z") * M, Side: Sides[Str(r!["lato"])], Values: values(r!))).ToArray();
        foreach (var x in port)
        {
            var match = rows.Where(r => Math.Abs(r.Depth - x.Depth) <= 1e-6 * Math.Max(1, x.Depth / 1e4) && r.Side == x.Side).ToArray();
            Assert.IsTrue(match.Length >= 1, $"{id}: row at {x.Depth} mm {x.Side} not in the legacy output");
            for (int i = 0; i < x.Values.Length; i++)
                if (double.IsNaN(match[0].Values[i])) Assert.IsTrue(double.IsNaN(x.Values[i]), $"{id} z={x.Depth} value {i}");
                else Close(match[0].Values[i], x.Values[i], $"{id} z={x.Depth} {x.Side} value {i}", 1e-8, absolute[i]);
        }
        foreach (var r in rows)
            if (!port.Any(x => Math.Abs(r.Depth - x.Depth) <= 1e-6 * Math.Max(1, x.Depth / 1e4) && r.Side == x.Side))
                Assert.IsTrue(rows.Any(o => o.Depth != r.Depth && Math.Abs(o.Depth - r.Depth) <= 1e-6), $"{id}: legacy row at {r.Depth} mm {r.Side} missing in the port");
    }

    // ---------------------------------------------------------------- Axial capacity

    private static readonly Dictionary<string, PileInstallation> Installations = new()
    {
        ["Trivellato"] = PileInstallation.Bored, ["Elica continua"] = PileInstallation.ContinuousFlightAuger, ["Profilato d'acciaio"] = PileInstallation.DrivenSteelSection,
        ["Tubo d'acciaio chiuso"] = PileInstallation.DrivenClosedSteelTube, ["Calcestruzzo prefabbricato"] = PileInstallation.DrivenPrecastConcrete,
        ["Calcestruzzo gettato in opera"] = PileInstallation.DrivenCastInPlace
    };

    private static PileGroupEfficiency Efficiency(JsonNode? eff, double diameter)
    {
        string method = Str(eff?["metodo"]);
        int Count(string key) { double v = Num(eff?[key]) ?? 1; if (v % 1 != 0) throw new ArgumentException("not an integer"); return (int)v; }
        return method switch
        {
            "Converse-Labarre" => PileGroupEfficiency.ConverseLabarre(Count("numero_pali_x"), Count("numero_pali_y"), (Num(eff!["interasse_x"]) ?? double.NaN) * M, (Num(eff["interasse_y"]) ?? double.NaN) * M, diameter),
            "Feld" => PileGroupEfficiency.Feld(Count("numero_pali_x"), Count("numero_pali_y")),
            "Definita dall'utente" => PileGroupEfficiency.UserDefined(Num(eff!["eta_compressione"]) ?? 1, Num(eff["eta_trazione"]) ?? 1),
            "Nessuna riduzione" or "" => PileGroupEfficiency.None(),
            _ => throw new ArgumentException("unknown efficiency")
        };
    }

    [TestMethod]
    public void LegacyAxialCapacitiesAreReproduced()
    {
        int piles = 0, micropiles = 0, rejected = 0, notRepresentable = 0;
        foreach (var line in Lines("piles-vertical.jsonl"))
        {
            string name = Str(line["name"]); bool micro = Bool(line["micro"]); var data = line["input"]!.AsObject(); var r = line["result"]!.AsObject(); var g = data["generali"]!;
            if (name is "tipologia ignota" or "addensamento mancante" or "metodo Nq ignoto" or "micropalo metodo precedente" or "micropalo senza alpha" or "verticali ignote" or "efficienza ignota")
            { Assert.IsTrue(IsError(r), name); notRepresentable++; continue; }
            if (name == "phi 90") { Assert.IsTrue(IsError(r)); Assert.ThrowsException<ArgumentOutOfRangeException>(() => PileSoil(data["stratigrafie"]![0]![0]!.AsObject(), 0), "Model soil: φ < 90°"); notRepresentable++; continue; }
            if (name == "diametro nullo") { Assert.IsTrue(IsError(r)); rejected++; Assert.ThrowsException<ArgumentException>(() => RunPile(data)); continue; }
            if (IsError(r))
            {
                Assert.ThrowsException<ArgumentException>(() => { if (micro) RunMicropile(data); else RunPile(data); }, name); rejected++; continue;
            }
            if (micro) { SameAxial(r, RunMicropile(data), name, true, s => (s.Shaft, s.Base)); micropiles++; }
            else { SameAxial(r, RunPile(data), name, false, s => (s.DrainedShaft, s.DrainedBase)); piles++; }
        }
        Assert.AreEqual(25, piles); Assert.AreEqual(12, micropiles); Assert.AreEqual(10, rejected); Assert.AreEqual(8, notRepresentable);
    }

    private static PileResistanceFactors Factors(JsonNode g)
    {
        var f = PileResistanceFactors.FromStandard(Ntc, Profiles(Str(g["verticali_indagate"])));
        Assert.AreEqual(Req(g["sicurezza_base"], "γb"), f.Base); Assert.AreEqual(Req(g["sicurezza_laterale_compressione"], "γs"), f.ShaftCompression);
        Assert.AreEqual(Req(g["sicurezza_laterale_trazione"], "γst"), f.ShaftTension); Assert.AreEqual(Req(g["peso_palo_sfavorevole"], "γG"), f.WeightUnfavourable);
        Assert.AreEqual(Req(g["peso_palo_favorevole"], "γG fav"), f.WeightFavourable);
        return f;
    }

    private static AxialCapacityResult<AxialSurveyResistance> RunPile(JsonObject data)
    {
        var g = data["generali"]!; double d = Req(g["diametro"], "D") * M;
        if (!(d > 0)) throw new ArgumentException("diameter");
        bool water = Bool(g["presenza_falda"]); double? zw = water ? Req(g["profondita_falda"], "zw") : null;
        string type = Str(g["tipo_palo"]); var installation = Installations[type == "Battuto" ? Str(g["sottotipo_palo_battuto"]) : type];
        var surveys = data["stratigrafie"]!.AsArray().Select(s => new AxialPileSurvey(Profile(s!.AsArray(), zw, PileSoil), s.AsArray().Select(l => new AxialPileLayer(Behaviour(l!.AsObject()),
            Str(l["addensamento"]) == "Sciolto" ? SoilDensity.Loose : SoilDensity.Dense, Num(l["nc"]) ?? 9, Bool(l["laterale_attiva"], true))))).ToArray();
        var pile = new AxialPile(installation, d, Req(g["lunghezza"], "L") * M, Req(g["peso_specifico_palo"], "γ") * KN3, Bool(g["considera_sottospinta"]),
            Num(g["azione_compressione"]) * 1000, Num(g["azione_trazione"]) * 1000);
        return AxialPileCapacity.Calculate(pile, surveys, Factors(g), Efficiency(data["efficienza"], d));
    }

    private static AxialCapacityResult<MicropileSurveyResistance> RunMicropile(JsonObject data)
    {
        var g = data["generali"]!; double d = Req(g["diametro"], "D") * M; var (cd, ct) = Designation(Str(g["profilo_chs"]));
        var surveys = data["stratigrafie"]!.AsArray().Select(s => new MicropileSurvey(Profile(s!.AsArray(), null,
            (l, i) => new Soil(Str(l["terreno"]), 18 * KN3, 20 * KN3, 0, 0, "BD layer")), s.AsArray().Select(l => (BdSoils[Str(l!["terreno"])], Req(l["alpha"], "α"), Bool(l["laterale_attiva"], true))))).ToArray();
        var pile = new Micropile(d, Req(g["lunghezza"], "L") * M, (Num(g["inclinazione"]) ?? 0) * Deg, Enum.Parse<MicropileInjection>(Str(g["tipo_iniezione"])), Req(g["pressione_iniezione"], "p"),
            (Num(g["inizio_aderenza"]) ?? 0) * M, Bool(g["considera_punta"]) ? Req(g["percentuale_punta"], "%") : null, new SectionCHS(cd, ct), TubeSteel, (Num(g["peso_specifico_palo"]) ?? 25) * KN3,
            Num(g["azione_compressione"]) * 1000, Num(g["azione_trazione"]) * 1000);
        return AxialPileCapacity.Calculate(pile, surveys, Factors(g), Efficiency(data["efficienza"], d));
    }

    private static void SameAxial<T>(JsonObject r, AxialCapacityResult<T> p, string id, bool micro, Func<T, (double Shaft, double Base)> drained)
    {
        Close(Req(r["profondita_massima"], "max") * M, p.MaximumDepth, id, 1e-12, 1e-9); Close(Req(r["lunghezza_palo"], "L") * M, p.PileLength, id, 1e-15);
        Assert.AreEqual(Bool(r["copertura_completa"]), p.FullCoverage, id); Assert.AreEqual((int)Req(r["numero_stratigrafie"], "n"), p.SurveyCount, id);
        var eff = r["efficienza"]!; Close(Req(eff["eta_compressione"], "ηc"), p.Efficiency.Compression, id, 1e-14); Close(Req(eff["eta_trazione"], "ηt"), p.Efficiency.Tension, id, 1e-14);
        Assert.AreEqual((int)Req(eff["numero_pali"], "n"), p.Efficiency.Piles, id);
        int count = (int)Req(r["numero_dettagli"], "details"); Assert.AreEqual(count, p.Depths.Count, id + " depths");
        // Curves at every depth.
        foreach (var (key, curve) in r["curve"]!.AsObject())
        {
            var c = key switch
            {
                "compressione" => (AxialCondition.Drained, true), "trazione" => (AxialCondition.Drained, false), "drenante_compressione" => (AxialCondition.Drained, true),
                "drenante_trazione" => (AxialCondition.Drained, false), "non_drenante_compressione" => (AxialCondition.Undrained, true), _ => (AxialCondition.Undrained, false)
            };
            var mine = p.Curves[c];
            foreach (var (series, values) in new[] { ("media", mine.Mean), ("minima", mine.Minimum), ("progetto", mine.Design) })
            {
                var points = curve![series]!.AsArray(); Assert.AreEqual(points.Count, values.Count, id + " " + key);
                for (int i = 0; i < points.Count; i++)
                {
                    Close(Req(points[i]![0], "z") * M, values[i].Depth, $"{id} {key} {series} z#{i}", 1e-12, 1e-6);
                    Close(Req(points[i]![1], "R") * 1000, values[i].Value, $"{id} {key} {series} #{i}", 1e-9, 1e-6);
                }
            }
        }
        foreach (var (key, mine) in new[] { ("compressione", p.CompressionActions), ("trazione", p.TensionActions) })
        {
            var points = r["azioni"]![key]!.AsArray(); Assert.AreEqual(points.Count, mine.Count, id + " actions " + key);
            for (int i = 0; i < points.Count; i++) Close(Req(points[i]![1], "E") * 1000, mine[i].Value, $"{id} action {key} #{i}", 1e-10, 1e-6);
        }
        // Details of one depth in ten: weight, verticals, components.
        foreach (var kept in r["dettagli"]!.AsArray())
        {
            int index = (int)Req(kept!["indice"], "i"); var e = kept["dettaglio"]!; var mine = p.Depths[index]; string did = $"{id} depth #{index}";
            Close(Req(e["z"], "z") * M, mine.Depth, did, 1e-12, 1e-6); Close(Req(e["peso"], "W") * 1000, mine.Weight, did + " weight", 1e-10, 1e-6);
            var surveys = e["sondaggi"]!.AsArray(); Assert.AreEqual(surveys.Count, mine.Surveys.Count, did);
            for (int k = 0; k < surveys.Count; k++)
            {
                var s = surveys[k]!; string sid = did + " vertical " + k;
                if (micro)
                {
                    var m = (MicropileSurveyResistance)(object)mine.Surveys[k]!;
                    Close(Req(s["compressione"]!["laterale"], "Ls") * 1000, m.Shaft, sid + " shaft", 1e-10, 1e-6); Close(Req(s["compressione"]!["base"], "B") * 1000, m.Base, sid + " base", 1e-10, 1e-6);
                    Assert.AreEqual(s["tratti"]!.AsArray().Count, m.Segments.Count, sid);
                    continue;
                }
                var a = (AxialSurveyResistance)(object)mine.Surveys[k]!;
                Close(Req(s["sigma_punta"], "σ'") * KPa, a.TipEffectiveStress, sid + " σ'", 1e-11, 1e-14); Close(Req(s["sigma_totale_punta"], "σ") * KPa, a.TipTotalStress, sid + " σ", 1e-11, 1e-14);
                Close(Req(s["drenante"]!["base"], "Bd") * 1000, a.DrainedBase, sid + " drained base", 1e-9, 1e-6); Close(Req(s["drenante"]!["laterale"], "Ld") * 1000, a.DrainedShaft, sid + " drained shaft", 1e-9, 1e-6);
                Close(Req(s["non_drenante"]!["base"], "Bu") * 1000, a.UndrainedBase, sid + " undrained base", 1e-9, 1e-6); Close(Req(s["non_drenante"]!["laterale"], "Lu") * 1000, a.UndrainedShaft, sid + " undrained shaft", 1e-9, 1e-6);
                var nq = Num(s["nq"]); if (nq.HasValue) Close(nq.Value, a.Nq!.Nq, sid + " Nq", 1e-11); else Assert.IsNull(a.Nq, sid);
                var nc = Num(s["nc"]); if (nc.HasValue) Assert.AreEqual(nc.Value, a.Nc); else Assert.IsNull(a.Nc, sid);
                var tratti = s["tratti"]!.AsArray(); Assert.AreEqual(tratti.Count, a.Segments.Count, sid + " segments");
                for (int t = 0; t < tratti.Count; t++)
                {
                    var x = tratti[t]!; var y = a.Segments[t]; string tid = sid + " segment " + t;
                    Close(Req(x["cielo"], "top") * M, y.Top, tid, 1e-12, 1e-6); Close(Req(x["fondo"], "bottom") * M, y.Bottom, tid, 1e-12, 1e-6);
                    Close(Req(x["sigma_media"], "σm") * KPa, y.MeanStress, tid + " σm", 1e-11, 1e-14); Close(Req(x["sigma_fondo"], "σb") * KPa, y.BottomStress, tid + " σb", 1e-11, 1e-14);
                    Close(Req(x["k"], "K"), y.K!.Value, tid + " K", 1e-15); Close(Req(x["mu"], "μ"), y.Mu!.Value, tid + " μ", 1e-12);
                    Close(Req(x["tau_d"], "τd") * KPa, y.DrainedShear, tid + " τd", 1e-10, 1e-14); Close(Req(x["tau_u"], "τu") * KPa, y.UndrainedShear, tid + " τu", 1e-10, 1e-14);
                    var alpha = Num(x["alfa"]); if (alpha.HasValue) Close(alpha.Value, y.Alpha!.Value, tid + " α", 1e-12); else Assert.IsNull(y.Alpha, tid);
                    Close(Req(x["laterale_d"], "Ld") * 1000, y.DrainedLateral, tid, 1e-9, 1e-6); Close(Req(x["laterale_u"], "Lu") * 1000, y.UndrainedLateral, tid, 1e-9, 1e-6);
                }
            }
            foreach (var (key, comp) in e["componenti"]!.AsObject())
            {
                var c = key switch
                {
                    "compressione" or "drenante" => (AxialCondition.Drained, true), "trazione" or "drenante_trazione" => (AxialCondition.Drained, false),
                    "non_drenante" => (AxialCondition.Undrained, true), _ => (AxialCondition.Undrained, false)
                };
                var mineC = mine.Components[c];
                foreach (var (branch, b) in new[] { ("Media", mineC.Mean), ("Minimo", mineC.Minimum) })
                {
                    var x = comp![branch]!;
                    Close(Req(x["calc"]![0], "calc L") * 1000, b.Shaft, $"{did} {key} {branch} shaft", 1e-9, 1e-6); Close(Req(x["calc"]![1], "calc B") * 1000, b.Base, $"{did} {key} {branch} base", 1e-9, 1e-6);
                    Close(Req(x["d"]![0], "d L") * 1000, b.DesignShaft, $"{did} {key} {branch} design shaft", 1e-9, 1e-6); Close(Req(x["d"]![1], "d B") * 1000, b.DesignBase, $"{did} {key} {branch} design base", 1e-9, 1e-6);
                }
            }
        }
        Assert.AreEqual(r["avvisi"]!.AsArray().Count, p.Warnings.Count, id + " warnings: " + string.Join(" | ", p.Warnings));
    }
}
