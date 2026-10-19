using System.Collections.ObjectModel;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.Seepage;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Seepage;

public sealed class HydraulicSafetyResult
{
    public string CaseId { get; internal set; } = "";
    public string StageId { get; internal set; } = "";
    public string ElementId { get; internal set; } = "";
    public string Method { get; internal set; } = "";
    public string Reference { get; internal set; } = "";
    public string Source { get; internal set; } = "";
    public string Unit { get; internal set; } = "";
    public double? Demand { get; internal set; }
    public double? Capacity { get; internal set; }
    public double? Utilization { get; internal set; }
    public EngineeringOutcome Outcome { get; internal set; } = EngineeringOutcome.NotEvaluated;
    public string Diagnostic { get; internal set; } = "";
    public IReadOnlyDictionary<string, double> Coefficients { get; internal set; } = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>());
    public IReadOnlyList<HydraulicActionResult> Actions { get; internal set; } = Array.Empty<HydraulicActionResult>();
}
public sealed class HydraulicActionResult
{
    public string Id { get; }
    public HydraulicActionCategory Category { get; }
    public double CharacteristicUpwardForce { get; }
    public double Factor { get; }
    public double DesignUpwardForce => CharacteristicUpwardForce * Factor;
    public string Source { get; }
    internal HydraulicActionResult(HydraulicForceAction a, double factor)
    { Id = a.Id; Category = a.Category; CharacteristicUpwardForce = a.UpwardForce; Factor = factor; Source = a.Source; }
}

/// <summary>Local hydraulic UPL/HYD checks, separate from global slope stability and undrained basal failure.
/// Inputs are characteristic actions/gradients; only an explicitly identified additional Rd is already a design resistance.</summary>
public static class HydraulicSafety
{
    public static HydraulicSafetyResult Uplift(HydraulicUpliftInput input, StandardGeotechnical standard)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var result = Start(input.CaseId, input.StageId, input.ElementId, "UPL", "N", standard, input.Situation, out var profile);
        if (profile == null) return result;
        Nonnegative(input.AdditionalDesignResistance, "Resistenza aggiuntiva Rd");
        if (input.CharacteristicActions == null) throw new ArgumentException("Azioni UPL mancanti.");
        if (input.CharacteristicActions.Count == 0) return Unavailable(result, "Assegnare le risultanti caratteristiche delle azioni, in N.");
        if (input.AdditionalDesignResistance > 0 && string.IsNullOrWhiteSpace(input.ResistanceSource))
            return Unavailable(result, "Rd assegnata: documentare metodo, M2 e coefficienti pertinenti; nessuna riduzione implicita.");
        var factors = profile.UpliftActions; var actions = new List<HydraulicActionResult>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in input.CharacteristicActions)
        {
            if (a == null || string.IsNullOrWhiteSpace(a.Id) || !ids.Add(a.Id) || !Enum.IsDefined(typeof(HydraulicActionCategory), a.Category))
                throw new ArgumentException("Azioni UPL: identificativi univoci e categorie valide richiesti.");
            Finite(a.UpwardForce, "Azione caratteristica");
            if (string.IsNullOrWhiteSpace(a.Source)) return Unavailable(result, "Fonte mancante per l'azione " + a.Id);
            bool up = a.UpwardForce > 0;
            double factor = a.Category switch
            {
                HydraulicActionCategory.PermanentStructural => up ? factors.PermanentUnfavourable : factors.PermanentFavourable,
                HydraulicActionCategory.PermanentNonStructural => up ? factors.NonStructuralUnfavourable : factors.NonStructuralFavourable,
                _ => up ? factors.VariableUnfavourable : factors.VariableFavourable
            };
            actions.Add(new(a, factor));
        }
        result.Actions = actions.AsReadOnly();
        result.Source = "Azioni caratteristiche; Rd già di progetto: " + (input.AdditionalDesignResistance > 0 ? input.ResistanceSource : "non utilizzata");
        result.Coefficients = ReadOnly(new() { ["gammaG1_unfavourable"] = factors.PermanentUnfavourable, ["gammaG1_favourable"] = factors.PermanentFavourable,
            ["gammaG2_unfavourable"] = factors.NonStructuralUnfavourable, ["gammaG2_favourable"] = factors.NonStructuralFavourable,
            ["gammaQ_unfavourable"] = factors.VariableUnfavourable, ["gammaQ_favourable"] = factors.VariableFavourable,
            ["gammaM_phi_for_Rd"] = profile.UpliftResistanceMaterials.TanFrictionAngle, ["gammaM_c_for_Rd"] = profile.UpliftResistanceMaterials.EffectiveCohesion,
            ["gammaM_cu_for_Rd"] = profile.UpliftResistanceMaterials.UndrainedShearStrength });
        return Evaluate(result, actions.Sum(a => Math.Max(0, a.DesignUpwardForce)), input.AdditionalDesignResistance + actions.Sum(a => Math.Max(0, -a.DesignUpwardForce)),
            "Vinst,d confrontata con Gstb,d + Rd. Pesi totali e risultanti idrauliche coerenti; Rd non fattorizzata una seconda volta. Verifica locale UPL.");
    }
    public static HydraulicSafetyResult FreeOutflow(HydraulicGradientInput input, StandardGeotechnical standard)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!Enum.IsDefined(typeof(HydraulicGradientMeasure), input.Measure)) throw new ArgumentException("Tipo di gradiente non valido.");
        var result = Start(input.CaseId, input.StageId, input.ElementId, "HYD-" + input.Measure, "1", standard, input.Situation, out var profile);
        if (profile == null) return result;
        Finite(input.UpwardGradient, "Gradiente"); Finite(input.SaturatedUnitWeight, "Peso saturo");
        if (!(input.SaturatedUnitWeight > SoilUnits.WaterUnitWeight)) throw new ArgumentException("Peso saturo maggiore del peso dell'acqua richiesto.");
        if (!input.PredominantlyVerticalFlow || !input.FreeOutflow || string.IsNullOrWhiteSpace(input.ApplicabilitySource))
            return Unavailable(result, "Criterio non applicabile senza efflusso libero, flusso prevalentemente verticale e motivazione documentata. Una mesh 2D non dimostra queste condizioni.");
        double factor = input.Measure == HydraulicGradientMeasure.Mean ? profile.MeanGradientFactor : profile.ExitGradientFactor;
        double critical = (input.SaturatedUnitWeight - SoilUnits.WaterUnitWeight) / SoilUnits.WaterUnitWeight;
        result.Source = input.ApplicabilitySource;
        result.Coefficients = ReadOnly(new() { ["gammaR"] = factor, ["criticalGradient"] = critical, ["waterUnitWeight"] = SoilUnits.WaterUnitWeight });
        return Evaluate(result, Math.Max(0, input.UpwardGradient), critical / factor,
            "Gradiente ascendente caratteristico confrontato con ic/gammaR. Terreno saturo; non verifica erosione interna né instabilità non drenata del fondo.");
    }
    public static HydraulicSafetyResult LoadedOutflow(HydraulicLoadedExitInput input, StandardGeotechnical standard)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var result = Start(input.CaseId, input.StageId, input.ElementId, "HYD-loaded", "MPa", standard, input.Situation, out var profile);
        if (profile == null) return result;
        Nonnegative(input.PorePressure, "Pressione interstiziale"); Nonnegative(input.HydrostaticPressure, "Pressione idrostatica");
        Nonnegative(input.EffectiveStressWithoutSeepage, "Tensione efficace senza filtrazione");
        if (!input.PredominantlyVerticalFlow || !input.LoadedOutflow || string.IsNullOrWhiteSpace(input.ApplicabilitySource))
            return Unavailable(result, "Criterio non applicabile senza frontiera caricata, flusso prevalentemente verticale e motivazione documentata.");
        result.Source = input.ApplicabilitySource;
        result.Coefficients = ReadOnly(new() { ["gammaR"] = profile.LoadedExitFactor });
        return Evaluate(result, Math.Max(0, input.PorePressure - input.HydrostaticPressure), input.EffectiveStressWithoutSeepage / profile.LoadedExitFactor,
            "Sovrappressione rispetto all'idrostatica confrontata con la tensione verticale efficace senza filtrazione divisa per gammaR. Verifica locale HYD.");
    }
    private static HydraulicSafetyResult Start(string caseId, string stage, string element, string method, string unit,
        StandardGeotechnical standard, GeotechnicalSituation situation, out GeotechnicalHydraulicProfile? profile)
    {
        if (standard == null) throw new ArgumentNullException(nameof(standard));
        if (string.IsNullOrWhiteSpace(caseId) || string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(element)) throw new ArgumentException("Caso, fase e punto/elemento di verifica obbligatori.");
        if (!Enum.IsDefined(typeof(GeotechnicalSituation), situation)) throw new ArgumentException("Situazione di progetto non valida.");
        profile = standard.HydraulicProfile(situation);
        return new HydraulicSafetyResult { CaseId = caseId, StageId = stage, ElementId = element, Method = method, Unit = unit,
            Reference = profile?.Reference ?? "", Diagnostic = profile == null ? "Profilo normativo idraulico non implementato per " + standard.Name + " / " + situation : "" };
    }
    private static HydraulicSafetyResult Unavailable(HydraulicSafetyResult r, string message) { r.Diagnostic = message; return r; }
    private static HydraulicSafetyResult Evaluate(HydraulicSafetyResult r, double demand, double capacity, string diagnostic)
    {
        if (!UnsaturatedConductivity.Finite(demand) || !UnsaturatedConductivity.Finite(capacity)) return Unavailable(r, "Overflow nel bilancio idraulico: nessun esito utilizzabile.");
        r.Demand = demand; r.Capacity = capacity; r.Utilization = capacity > 0 ? demand / capacity : demand == 0 ? 0 : null;
        r.Outcome = demand <= capacity ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
        r.Diagnostic = capacity == 0 && demand > 0 ? diagnostic + " Capacità nulla; utilizzo non finito, verifica non soddisfatta." : diagnostic;
        if (r.Utilization.HasValue && !UnsaturatedConductivity.Finite(r.Utilization.Value)) r.Utilization = null;
        return r;
    }
    private static IReadOnlyDictionary<string, double> ReadOnly(Dictionary<string, double> values) => new ReadOnlyDictionary<string, double>(values);
    private static void Finite(double v, string name) { if (!UnsaturatedConductivity.Finite(v)) throw new ArgumentException(name + ": valore non finito."); }
    private static void Nonnegative(double v, string name) { Finite(v, name); if (v < 0) throw new ArgumentException(name + ": valore non negativo richiesto."); }
}
