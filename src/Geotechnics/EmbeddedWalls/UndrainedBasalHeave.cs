using System.Collections.ObjectModel;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public sealed class UndrainedBasalHeaveResult
{
    public string CaseId { get; internal set; } = "";
    public string StageId { get; internal set; } = "";
    public string Combination { get; internal set; } = "";
    public string Method { get; internal set; } = "";
    public string Reference { get; internal set; } = "";
    public string Source { get; internal set; } = "";
    public string Unit => "MPa";
    public double? MechanismWidth { get; internal set; }
    public double? GrossPressure { get; internal set; }
    public double? ExternalShearRelief { get; internal set; }
    public double? NetPressure { get; internal set; }
    public double? Demand { get; internal set; }
    public double? Capacity { get; internal set; }
    public double? Utilization { get; internal set; }
    public EngineeringOutcome Outcome { get; internal set; } = EngineeringOutcome.NotEvaluated;
    public string Diagnostic { get; internal set; } = "";
    public IReadOnlyDictionary<string, double> Coefficients { get; internal set; }
        = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>());
}

/// <summary>Homogeneous total-stress basal mechanism, FHWA-IF-99-015 §5.8.2 eq.34-37.
/// Nc is explicitly assigned with a source. Uniform surcharges follow vertical block equilibrium.
/// No resistance from the wall, anchors, struts or a bottom plug is credited. No UPL/HYD check is performed.</summary>
public static class UndrainedBasalHeave
{
    public static UndrainedBasalHeaveResult Characteristic(UndrainedBasalHeaveInput input)
    {
        var p = Prepare(input);
        return Compute(p, null);
    }

    public static IReadOnlyList<UndrainedBasalHeaveResult> Check(UndrainedBasalHeaveInput input, StandardGeotechnical standard)
    {
        if (standard == null) throw new ArgumentNullException(nameof(standard));
        var p = Prepare(input);
        var combinations = standard.Combinations(GeotechnicalCheck.UndrainedBasalHeave, p.Situation);
        if (combinations.Count == 0)
            return Array.AsReadOnly(new[] { Start(p, "", "Profilo normativo non disponibile per instabilità non drenata del fondo e situazione richiesta.") });
        return Array.AsReadOnly(combinations.Select(c => Compute(p, c)).ToArray());
    }

    private static UndrainedBasalHeaveResult Compute(Prepared p, GeotechnicalCombination? c)
    {
        var r = Start(p, c?.Name ?? "Caratteristico", p.Unavailable);
        if (p.Unavailable.Length > 0) return r;
        if (c != null && (c.Check != GeotechnicalCheck.UndrainedBasalHeave || c.Situation != p.Situation || c.ResistanceFactor != 1))
        {
            r.Diagnostic = "Combinazione non applicabile: questo confronto di pressione netta richiede il profilo specifico del fondo con gammaR=1.";
            return r;
        }
        double g1 = c?.Actions.PermanentUnfavourable ?? 1;
        double g2 = c?.Actions.NonStructuralUnfavourable ?? 1;
        double q = c?.Actions.VariableUnfavourable ?? 1;
        double m = c?.Materials.UndrainedShearStrength ?? 1;
        double mg = c?.Materials.UnitWeight ?? 1;
        double strength = p.Cu / m;
        double gross = g1 * (p.Gamma / mg) * p.H + g2 * p.G2 + q * p.Q * p.Psi;
        double relief = p.Shear ? strength * (p.H / p.Width) : 0;
        double net = gross - relief, capacity = p.Nc * strength;
        if (!Finite(gross) || !Finite(relief) || !Finite(net) || !Finite(capacity) || capacity <= 0)
        { r.Diagnostic = "Scala numerica non rappresentabile: nessuna verifica soddisfatta restituita."; return r; }
        double utilization = Math.Max(0, net) / capacity;
        if (!Finite(utilization)) { r.Diagnostic = "Utilizzo non rappresentabile: verificare unità e scala degli input."; return r; }
        r.MechanismWidth = p.Width; r.GrossPressure = gross; r.ExternalShearRelief = relief;
        r.NetPressure = net; r.Demand = Math.Max(0, net); r.Capacity = capacity; r.Utilization = utilization;
        r.Reference += c == null ? "" : "; " + c.Reference;
        r.Coefficients = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>
        { ["Nc"] = p.Nc, ["gammaG1"] = g1, ["gammaG2"] = g2, ["gammaQ"] = q, ["psiQ"] = p.Psi,
          ["gammaM_cu"] = m, ["gammaM_unitWeight"] = mg, ["gammaR"] = 1 });
        r.Outcome = c == null ? EngineeringOutcome.NotEvaluated
            : utilization <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
        r.Diagnostic = (c == null ? "Bilancio caratteristico, non verifica normativa. " : "Verifica locale GEO del meccanismo dichiarato. ")
            + (net <= 0 ? "Pressione netta non positiva: il taglio esterno equilibra il carico in questo meccanismo. " : "")
            + "Tensioni totali; Nc assegnato. Non verifica UPL, HYD, stabilità globale o tampone di fondo.";
        return r;
    }

    private static UndrainedBasalHeaveResult Start(Prepared p, string combination, string diagnostic) => new()
    {
        CaseId = p.CaseId, StageId = p.StageId, Combination = combination,
        Method = p.Shear ? "Blocco omogeneo non drenato, taglio esterno incluso" : "Blocco omogeneo non drenato, taglio esterno trascurato",
        Reference = "FHWA-IF-99-015 (1999), §5.8.2, eq.34-37; sovraccarichi uniformi per equilibrio verticale",
        Source = p.Source, Diagnostic = diagnostic
    };

    private sealed class Prepared
    {
        internal string CaseId = "", StageId = "", Source = "", Unavailable = "";
        internal double H, Width, Gamma, Cu, Nc, G2, Q, Psi;
        internal bool Shear;
        internal GeotechnicalSituation Situation;
    }

    private static Prepared Prepare(UndrainedBasalHeaveInput i)
    {
        if (i == null) throw new ArgumentNullException(nameof(i));
        if (string.IsNullOrWhiteSpace(i.CaseId) || string.IsNullOrWhiteSpace(i.StageId))
            throw new ArgumentException("Caso e fase del fondo scavo sono obbligatori.");
        Positive(i.ExcavationDepth, "Profondità scavo"); Positive(i.ExcavationWidth, "Larghezza scavo");
        Positive(i.TotalUnitWeight, "Peso totale"); Positive(i.UndrainedStrength, "Resistenza non drenata");
        Positive(i.BearingCapacityFactor, "Nc");
        if (i.StiffLayerDepth.HasValue) Positive(i.StiffLayerDepth.Value, "Profondità strato rigido");
        Nonnegative(i.PermanentSurcharge, "Sovraccarico permanente"); Nonnegative(i.VariableSurcharge, "Sovraccarico variabile");
        if (!Finite(i.VariableCombinationFactor) || i.VariableCombinationFactor < 0 || i.VariableCombinationFactor > 1)
            throw new ArgumentOutOfRangeException(nameof(i.VariableCombinationFactor));
        if (!Enum.IsDefined(typeof(GeotechnicalSituation), i.Situation)) throw new ArgumentException("Situazione di progetto non valida.");
        var p = new Prepared { CaseId = i.CaseId, StageId = i.StageId, H = i.ExcavationDepth,
            Width = Math.Min(i.ExcavationWidth / Math.Sqrt(2), i.StiffLayerDepth ?? double.MaxValue),
            Gamma = i.TotalUnitWeight, Cu = i.UndrainedStrength, Nc = i.BearingCapacityFactor,
            G2 = i.PermanentSurcharge, Q = i.VariableSurcharge, Psi = i.VariableCombinationFactor,
            Shear = i.IncludeExternalBlockShear, Situation = i.Situation,
            Source = "Modello: " + i.ApplicabilitySource + "; Nc: " + i.BearingCapacitySource + "; azioni: " + i.ActionSource };
        if (!i.HomogeneousUndrainedMechanismConfirmed || !i.LongExcavationConfirmed || !i.CharacteristicValuesConfirmed
            || string.IsNullOrWhiteSpace(i.ApplicabilitySource) || string.IsNullOrWhiteSpace(i.BearingCapacitySource)
            || string.IsNullOrWhiteSpace(i.ActionSource))
            p.Unavailable = "Confermare scavo lungo, terreno omogeneo non drenato nel volume del meccanismo, valori caratteristici e fonti di modello, Nc e azioni. Nessuna media automatica fra strati.";
        if (i.Situation != GeotechnicalSituation.PersistentTransient)
            p.Unavailable = "Meccanismo disponibile soltanto per situazione statica persistente/transitoria; sisma non implementato.";
        return p;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static void Positive(double value, string label)
    { if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(label, "Valore finito positivo richiesto."); }
    private static void Nonnegative(double value, string label)
    { if (!Finite(value) || value < 0) throw new ArgumentOutOfRangeException(label, "Valore finito non negativo richiesto."); }
}
