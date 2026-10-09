using System.IO.Compression;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.PilesFixture;

namespace GeotechnicsTests;

/// <summary>
/// Reading of the wall fixtures and conversion of the legacy wall document (m, kN/m, kPa, kN/m³, degrees) to the typed input with Model soils and
/// profiles (mm, N/mm, MPa, N/mm³, rad): the adapter that ANTHEA will use. Kept separate from the assertions.
/// </summary>
internal static class WallsFixture
{
    public const double KNm = 1000; // 1 kNm/m = 1000 N·mm/mm
    /// <summary>The adapter of the legacy documents keeps γRd on the soil inertia of Annex F, as ANTHEA (see ShallowFoundationSeismic).</summary>
    public const bool LegacyInertia = true;

    public static IEnumerable<JsonObject> GzipLines(string file)
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(Folder, file)), CompressionMode.Decompress);
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        string? line;
        while ((line = reader.ReadLine()) != null) yield return JsonNode.Parse(line)!.AsObject();
    }

    static double D(JsonNode? n, string key) => Num(n?[key]) ?? throw new ArgumentException("Dato mancante: " + key);

    public static double TotalHeight(JsonObject d) => D(d["geometry"], "height") + D(d["geometry"], "slab");
    public static double ValleyHeight(JsonObject d) => Str(d["valley"]?["height_mode"]) is "" or "Interamente libero" ? 0 : TotalHeight(d) - D(d["valley"], "free_height");

    /// <summary>Profile of the layers from the ground at the elevation (m above the base) downwards.</summary>
    public static SoilProfile Column(JsonArray layers, double ground, string name)
    {
        var list = new List<SoilLayer>(); double top = ground;
        foreach (var l in layers)
        {
            double bottom = top - D(l, "thickness");
            var soil = new Soil(Str(l!["name"]) is { Length: > 0 } n ? n : "Strato", D(l, "gamma") * KN3, D(l, "gamma_sat") * KN3, D(l, "phi") * Deg, 0, "ANTHEA fixture");
            list.Add(new SoilLayer(soil, top * M, bottom * M)); top = bottom;
        }
        return new SoilProfile(name, list, "ANTHEA fixture");
    }

    static WallInterface Interface(JsonObject d, bool wall)
    {
        var i = d["interfaces"];
        string mode = Str(i?[wall ? "wall_mode" : "base_mode"]);
        var friction = mode switch
        {
            "Assegnato" or "" => WallFriction.Assigned, "Gettato in opera" => WallFriction.CastInPlace, "Prefabbricato liscio" => WallFriction.PrecastSmooth, "Liscio" => WallFriction.Smooth,
            _ => throw new ArgumentException("Modalità di attrito non valida.")
        };
        // Version 1: no interfaces (ANTHEA completes them with δ = 0 on the back and δ of the foundation at the base).
        double angle = friction == WallFriction.Assigned ? (wall ? (i == null ? 0 : D(i, "wall_delta")) : D(d["foundation"], "delta")) * Deg : 0;
        double cv = friction is WallFriction.CastInPlace or WallFriction.PrecastSmooth ? (Num(i?[wall ? "wall_phi_cv" : "base_phi_cv"]) ?? double.NaN) * Deg : 0;
        return new WallInterface(friction, angle, cv);
    }

    public static WallActionType ActionType(string type) => type switch
    {
        "Sovraccarico uniforme" => WallActionType.UniformSurcharge, "Forza orizzontale" => WallActionType.HorizontalForce, "Forza verticale" => WallActionType.VerticalForce,
        "Momento" => WallActionType.Moment, "Pressione laterale" => WallActionType.LateralPressure, "Urto" => WallActionType.Impact,
        _ => throw new ArgumentException("Tipo o natura dell’azione non supportati.")
    };

    static double ActionUnit(WallActionType type) => type is WallActionType.UniformSurcharge or WallActionType.LateralPressure ? KPa : type == WallActionType.Moment ? KNm : 1;

    public static WallLimitState State(string state) => state switch
    {
        "SLU" => WallLimitState.Ultimate, "SLE" => WallLimitState.Characteristic, "SLE_FREQ" => WallLimitState.Frequent, "SLE_QP" => WallLimitState.QuasiPermanent,
        "SISMA" => WallLimitState.Seismic, "ECCEZIONALE" => WallLimitState.Exceptional, _ => throw new ArgumentException("Stato limite non valido.")
    };

    /// <summary>
    /// Asserts that the action throws an <see cref="ArgumentException"/> or a derived one (the Model soils throw <see cref="ArgumentOutOfRangeException"/>).
    /// </summary>
    public static void Throws(Action run, string id)
    {
        try { run(); }
        catch (ArgumentException) { return; }
        Assert.Fail("Accepted: " + id);
    }

    public static NtcSiteAmplification Site(JsonNode s)
    {
        double ag = Num(s["ag_g"]) ?? throw new ArgumentException("Sisma: ag/g allo SLV (es. 0,20, non 20), inserire un valore fra 0 e 1.");
        double? ss = null; (NtcSoilCategory, double)? soil = null;
        switch (Str(s["ss_mode"]))
        {
            case "Assegnato": ss = Num(s["ss"]) ?? throw new ArgumentException("Sisma: Ss assegnato, inserire un valore fra 0.1 e 5."); break;
            case "Calcolato":
                if (!Enum.TryParse<NtcSoilCategory>(Str(s["soil_class"]), out var category) || Str(s["soil_class"]).Length != 1) throw new ArgumentException("Sisma: scegliere la categoria di sottosuolo A–E dalla relazione geotecnica.");
                soil = (category, category == NtcSoilCategory.A ? 0 : Num(s["f0"]) ?? throw new ArgumentException("Sisma: F₀ allo SLV, inserire un valore fra 2.2 e 10.")); break;
            default: throw new ArgumentException("Sisma: scegliere come definire Ss.");
        }
        double? st = null; (NtcTopography, double, double, double)? topography = null;
        switch (Str(s["st_mode"]))
        {
            case "Assegnato": st = Num(s["st"]) ?? throw new ArgumentException("Sisma: St assegnato, inserire un valore fra 1 e 5."); break;
            case "Calcolato":
                var shape = Str(s["topography"]) switch
                {
                    "Pianeggiante" => NtcTopography.Flat, "Pendio" => NtcTopography.Slope, "Rilievo a cresta stretta" => NtcTopography.Ridge,
                    _ => throw new ArgumentException("Sisma: scegliere pianeggiante, pendio o rilievo a cresta stretta.")
                };
                topography = (shape, (Num(s["slope"]) ?? double.NaN) * Deg, (Num(s["relief_height"]) ?? double.NaN) * M, (Num(s["site_height"]) ?? double.NaN) * M); break;
            default: throw new ArgumentException("Sisma: scegliere come definire St.");
        }
        return NtcSiteAmplification.Create(ag, soil, ss, topography, st);
    }

    /// <summary>The typed input of a legacy wall document (version 2, or 1 with its loads as correlated actions).</summary>
    public static WallInput Input(JsonObject d)
    {
        var g = d["geometry"]!; double ht = TotalHeight(d);
        var geometry = new WallGeometry(D(g, "height") * M, D(g, "stem_base") * M, D(g, "stem_top") * M, D(g, "slab") * M, D(g, "toe") * M, D(g, "heel") * M);
        var f = d["foundation"]!;
        var foundation = new Soil("Terreno di posa", D(f, "gamma") * KN3, D(f, "gamma_sat") * KN3, D(f, "phi") * Deg, 0, "ANTHEA fixture");
        var backfill = Column(d["layers"]!.AsArray(), ht, "Monte");
        var v = d["valley"];
        double dv = ValleyHeight(d);
        var valleyLayers = v == null || Bool(v["linked"]) ? d["layers"]!.AsArray() : v["layers"]!.AsArray();
        var valley = new WallValley(dv * M, Column(valleyLayers, dv, "Valle"), v != null && Bool(v["passive"]), v == null ? 0 : Num(v["mobilization"]) ?? double.NaN);
        var water = Bool(d["water"]?["enabled"]) ? new WallWater(D(d["water"], "depth") * M, D(d["water"], "front_head") * M) : null;
        var actions = new List<WallAction>();
        if ((Num(d["version"]) ?? 1) < 2)
        {
            var l = d["loads"]!; double a = D(g, "toe"), s = D(g, "stem_base"), top = D(g, "stem_top");
            actions.Add(new WallAction("q", "Sovraccarico", WallActionType.UniformSurcharge, WallActionCategory.Q, D(l, "surcharge") * KPa, ht * M, D(g, "slab") * M, 0, 1, 1, 1, "legacy"));
            actions.Add(new WallAction("hq", "Forza orizzontale", WallActionType.HorizontalForce, WallActionCategory.Q, D(l, "horizontal"), ht * M, D(g, "slab") * M, 0, 1, 1, 1, "legacy"));
            actions.Add(new WallAction("nq", "Forza verticale", WallActionType.VerticalForce, WallActionCategory.Q, D(l, "vertical"), ht * M, D(g, "slab") * M, (a + s - top / 2) * M, 1, 1, 1, "legacy"));
        }
        else
            foreach (var x in d["actions"]!.AsArray())
            {
                var type = ActionType(Str(x!["type"]));
                var category = Enum.TryParse<WallActionCategory>(Str(x["category"]), out var c) ? c : throw new ArgumentException("Tipo o natura dell’azione non supportati.");
                actions.Add(new WallAction(Str(x["id"]), Str(x["name"]), type, category, (Num(x["value"]) ?? double.NaN) * ActionUnit(type), (Num(x["z"]) ?? double.NaN) * M,
                    (Num(x["z0"]) ?? double.NaN) * M, (Num(x["x"]) ?? double.NaN) * M, Num(x["psi0"]) ?? double.NaN, Num(x["psi1"]) ?? double.NaN, Num(x["psi2"]) ?? double.NaN,
                    Str(x["group"]), Bool(x["enabled"])));
            }
        WallSeismic? seismic = null;
        var sd = d["seismic"]!;
        if (Bool(sd["enabled"]))
        {
            var method = Str(sd["method"]) switch { "" or "Mononobe–Okabe" => WallSeismicMethod.MononobeOkabe, "Wood semplificato" => WallSeismicMethod.Wood, _ => throw new ArgumentException("Metodo sismico non riconosciuto.") };
            var b = d["bearing_seismic"];
            // ANTHEA completes the missing keys: source from the site, model factor 1.15.
            var bearing = b == null || Str(b["source"]) is "" or "Da sito" ? WallSeismicBearing.FromSite(Num(b?["model_factor"]) ?? 1.15, LegacyInertia)
                : Str(b["source"]) == "Assegnata" ? WallSeismicBearing.Assigned(Num(b["ground_kh"]) ?? double.NaN, Num(b["ground_kv"]) ?? double.NaN, Num(b["model_factor"]) ?? 1.15, LegacyInertia)
                : throw new ArgumentException("Selezionare l’origine dell’accelerazione per la portanza sismica.");
            string source = Str(sd["source"]);
            seismic = source is "" or "kh e kv assegnati" ? WallSeismic.Assigned(method, D(sd, "kh"), D(sd, "kv"), bearing)
                : source == "Da parametri del sito (SLV)" ? WallSeismic.FromSite(method, Site(sd), bearing) : throw new ArgumentException("Sisma: modalità di definizione non riconosciuta.");
        }
        var r = d["reinforcement"];
        double? split = r != null && Bool(r["two_zones"]) ? D(r, "lower_height") * M : null;
        return new WallInput(Str(d["family"]) == "gravity" ? WallFamily.Gravity : WallFamily.Cantilever, geometry, D(d["materials"], "gamma") * KN3, backfill, foundation, valley,
            Interface(d, true), Interface(d, false), water, actions, seismic, split);
    }

    /// <summary>A combination row of a version 2 document.</summary>
    public static WallCombination Combination(JsonObject c)
    {
        var purpose = Str(c["purpose"]) switch { "" => WallSeismicPurpose.None, "Generale" => WallSeismicPurpose.General, "Ribaltamento" => WallSeismicPurpose.Overturning, _ => throw new ArgumentException("Destinazione della combinazione sismica non valida.") };
        double soil = Num(c["soil"]) ?? double.NaN;
        return new WallCombination(Str(c["name"]), State(Str(c["state"])), Num(c["wall"]) ?? double.NaN, soil, Num(c["valley_soil"]) ?? soil, Num(c["water"]) ?? double.NaN,
            Num(c["mphi"]) ?? double.NaN, Num(c["rslide"]) ?? double.NaN, Num(c["rover"]) ?? double.NaN, Num(c["rbearing"]) ?? double.NaN, Num(c["kh"]) ?? double.NaN, Num(c["kv"]) ?? double.NaN,
            c["coefficients"]!.AsObject().ToDictionary(p => p.Key, p => Num(p.Value) ?? double.NaN), purpose, Str(c["approach"]));
    }

    /// <summary>
    /// The combinations of a legacy result: the rows used by a version 2 document, or the fixed cases of a version 1 document (characteristic,
    /// frequent ψ1 and quasi permanent ψ2 on the three loads; 8 or 16 ULS with γR 1.1, 1.15, 1.4; seismic with ψ2 and the seismic bearing γR 1.2).
    /// </summary>
    public static WallCombination[] Combinations(JsonObject input, JsonObject result)
    {
        if ((Num(input["version"]) ?? 1) >= 2) return result["combinazioni_usate"]!.AsArray().Select(c => c!.AsObject()).Where(c => Bool(c["enabled"])).Select(Combination).ToArray();
        return result["combinazioni"]!.AsArray().Select(c =>
        {
            var state = State(Str(c!["State"])); bool design = state is WallLimitState.Ultimate or WallLimitState.Seismic;
            double fq = Req(c["LiveFactor"], "fq"), fs = Req(c["SoilFactor"], "fs");
            return new WallCombination(Str(c["Name"]), state, Req(c["WallFactor"], "fc"), fs, fs, Req(c["WaterFactor"], "fw"), 1, design ? 1.1 : 1, design ? 1.15 : 1,
                state == WallLimitState.Seismic ? 1.2 : design ? 1.4 : 1, Req(c["Kh"], "kh"), Req(c["Kv"], "kv"), new Dictionary<string, double> { ["q"] = fq, ["hq"] = fq, ["nq"] = fq });
        }).ToArray();
    }

    /// <summary>Options of the serviceability of a document (kPa → MPa, kN/m² → MPa); null when nothing is requested.</summary>
    public static WallServiceOptions? Service(JsonObject d)
    {
        var s = d["serviceability"]; if (s == null) return null;
        bool settlement = Bool(s["settlement"]), displacement = Bool(s["displacement"]);
        var histories = s["histories"]?.AsArray().Where(h => Bool(h!["enabled"])).Select(h => new WallAccelerogram(Str(h!["name"]), Str(h["state"]), Bool(h["compatible"]),
            Num(h["yield_g"]) ?? double.NaN, Num(h["scale"]) ?? double.NaN, Num(h["limit_mm"]) ?? double.NaN,
            h["samples"]!.AsArray().Select(p => new GPC.Checkers.Geotechnics.Seismic.AccelerogramSample(Num(p!["t"]) ?? double.NaN, Num(p["a_g"]) ?? double.NaN)))).ToArray() ?? new WallAccelerogram[0];
        if (!settlement && !displacement && histories.Length == 0) return null;
        var settlementOptions = settlement ? new WallSettlementOptions(s["layers"]!.AsArray().Select(l => new GPC.Checkers.Geotechnics.Foundations.SettlementLayer(Str(l!["name"]), D(l, "thickness") * M, D(l, "modulus") * KPa)),
            (Num(s["removed_pressure"]) ?? double.NaN) * KPa, Num(s["settlement_limit"]) ?? 25, Num(s["rotation_limit"]) ?? .002, Bool(s["rigid_base"])) : null;
        (double, double)? displacementOptions = displacement ? (Num(s["head_limit"]) ?? double.NaN, (Num(s["horizontal_stiffness"]) ?? double.NaN) * KPa) : null;
        return new WallServiceOptions(settlementOptions, displacementOptions, histories);
    }

    /// <summary>The curvatures of the stem of a legacy combination (Y above the slab in m, 1/m), null for a gravity wall with assigned strengths.</summary>
    public static IReadOnlyList<WallCurvaturePoint>? Curvatures(JsonObject d, JsonObject legacyCase)
        => Str(d["family"]) == "gravity" && Str(d["gravity_design"]?["type"]) == "Resistenze assegnate" ? null
            : legacyCase["Curvatures"]!.AsArray().Select(p => new WallCurvaturePoint(Req(p!["Y"], "Y") * M, Req(p["Curvature"], "κ") / M)).ToArray();

    static GPC.Checkers.Geotechnics.Slopes.SlopeLayer[] GlobalLayers(JsonArray layers, bool undrained) => layers.Select(l =>
    {
        double? cu = Num(l!["cu"]); if (cu is <= 0) cu = null;
        var soil = new Soil(Str(l["name"]) is { Length: > 0 } n ? n : "Strato", D(l, "gamma") * KN3, D(l, "gamma_sat") * KN3, D(l, "phi") * Deg, (Num(l["c"]) ?? 0) * KPa, "ANTHEA fixture",
            undrainedShearStrength: cu * KPa);
        return new GPC.Checkers.Geotechnics.Slopes.SlopeLayer(soil, D(l, "bottom") * M);
    }).ToArray();

    static GPC.Checkers.Geotechnics.Slopes.SlopePoint[] Points(JsonNode? points) => points?.AsArray().Select(p => new GPC.Checkers.Geotechnics.Slopes.SlopePoint(D(p, "x") * M, D(p, "y") * M)).ToArray()
        ?? new GPC.Checkers.Geotechnics.Slopes.SlopePoint[0];

    /// <summary>The global profile of a document (points and bottoms in m, kPa).</summary>
    public static WallGlobalProfile GlobalProfile(JsonObject d)
    {
        var g = d["global_stability"]!; bool undrained = Str(g["condition"]) == "Non drenata"; bool two = Str(g["soil_mode"]) == "Due colonne";
        int I(string key) => (int)D(g, key);
        var search = new GPC.Checkers.Geotechnics.Slopes.SlopeSearch(D(g, "exit_min") * M, D(g, "exit_max") * M, D(g, "entry_min") * M, D(g, "entry_max") * M, D(g, "depth_min") * M, D(g, "depth_max") * M,
            I("grid"), I("slices"), I("refinements"));
        return new WallGlobalProfile(Points(g["valley"]), Points(g["uphill"]), GlobalLayers(g["layers"]!.AsArray(), undrained), search, two ? GlobalLayers(g["valley_layers"]!.AsArray(), undrained) : null,
            two ? D(g, "soil_split_x") * M : 0, Bool(g["water_enabled"]) ? Points(g["water"]) : null, undrained);
    }

    /// <summary>The seismic coefficients of the global stability of a document, null without the seismic action.</summary>
    public static WallGlobalSeismic? GlobalSeismic(JsonObject d)
    {
        var g = d["global_stability"]!; if (!Bool(g["seismic"])) return null;
        return Str(g["seismic_source"]) == "Da sito · βs=0,38" ? WallGlobalSeismic.FromSite(Site(d["seismic"]!)) : WallGlobalSeismic.Assigned(D(g, "kh"), D(g, "kv"));
    }

    public static WallCheckKind? ServiceKind(string name) => name switch
    {
        "Cedimento edometrico finale" or "Cedimenti" => WallCheckKind.Settlement, "Rotazione da cedimenti differenziali" => WallCheckKind.Rotation,
        "Spostamento elastico fusto · base fissa" => WallCheckKind.StemDisplacement, "Spostamento totale in testa · stima disaccoppiata" or "Spostamento totale in testa" => WallCheckKind.HeadDisplacement,
        "Spostamento Newmark" => WallCheckKind.Newmark, _ => name.StartsWith("Scorrimento permanente Newmark") ? WallCheckKind.Newmark : null
    };

    public static WallMember Member(string name) => name switch { "Fusto" => WallMember.Stem, "Valle" => WallMember.Toe, "Monte" => WallMember.Heel, _ => throw new ArgumentException(name) };

    public static WallCheckKind? CheckKind(string name) => name switch
    {
        "Scorrimento" => WallCheckKind.Sliding, "Ribaltamento" => WallCheckKind.Overturning, "Capacità portante" => WallCheckKind.Bearing, "Contatto fondazione" => WallCheckKind.Contact, _ => null
    };

    public static WallCheckStatus CheckStatus(string status) => status switch
    {
        "Soddisfatta" => WallCheckStatus.Satisfied, "Non soddisfatta" => WallCheckStatus.NotSatisfied, "Non soddisfatta: resistenza nulla" => WallCheckStatus.ZeroResistance,
        "Contatto in compressione" => WallCheckStatus.ContactInCompression, "Perdita di equilibrio" => WallCheckStatus.LossOfEquilibrium, _ => WallCheckStatus.Unavailable
    };
}
