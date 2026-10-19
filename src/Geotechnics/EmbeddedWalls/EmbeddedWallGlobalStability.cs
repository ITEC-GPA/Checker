using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using ModelActionCategory = GPC.Model.Standards.GeotechnicalCharacteristicActionCategory;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

/// <summary>Explicit global-stability geometry for a continuous rectangular wall. Lengths mm.
/// Search and optional water line use slope coordinates: valley on the left, y=0 at the wall top.
/// Fields can be transformed with SeepageSlopeField. Supplied soil parameters and loads must be characteristic.</summary>
public sealed class EmbeddedWallGlobalProfile
{
    public double Thickness { get; }
    public double LeftExtent { get; }
    public double RightExtent { get; }
    public int ValleyDirection { get; }
    public bool CharacteristicInputsConfirmed { get; }
    public string Source { get; }
    public SlopeSearch Search { get; }
    public IReadOnlyList<SlopePoint> Water { get; }
    public EmbeddedWallGlobalProfile(double thickness, double leftExtent, double rightExtent, int valleyDirection,
        bool characteristicInputsConfirmed, string source, SlopeSearch search, IEnumerable<SlopePoint>? water = null)
    {
        Thickness = thickness; LeftExtent = leftExtent; RightExtent = rightExtent; ValleyDirection = valleyDirection;
        CharacteristicInputsConfirmed = characteristicInputsConfirmed; Source = source ?? ""; Search = search ?? throw new ArgumentNullException(nameof(search));
        Water = Array.AsReadOnly((water ?? []).ToArray());
    }
}

/// <summary>Prepared input in the legacy slope coordinate system. No soil reactions or anchor forces are added as external loads.</summary>
public sealed class EmbeddedWallGlobalPrepared
{
    public string CaseId { get; }
    public string StageId { get; }
    public double ReferenceElevation { get; }
    public int ValleyDirection { get; }
    public SlopeSection Section { get; }
    public SlopeSearch Search { get; }
    public string Source { get; }
    internal EmbeddedWallGlobalPrepared(EmbeddedWallModel m, string stage, EmbeddedWallGlobalProfile p, SlopeSection section)
    { CaseId = m.CaseId; StageId = stage; ReferenceElevation = m.Top; ValleyDirection = p.ValleyDirection; Section = section; Search = p.Search; Source = p.Source; }
}

/// <summary>Geometry and action adapter to the existing global stability engine. All factors remain explicit in SlopeFactors.
/// Continuous vertical wall, horizontal ground, two soil columns. External support systems require their global geometry and are not silently ignored.</summary>
public static class EmbeddedWallGlobalStability
{
    public static EmbeddedWallGlobalCase CreateCase(EmbeddedWallGlobalPrepared prepared, GeotechnicalCombination combination,
        IReadOnlyList<GeotechnicalActionSelection> selections, bool undrained)
    {
        if (prepared == null || combination == null || selections == null) throw new ArgumentNullException("Global case inputs");
        if (combination.Check != GeotechnicalCheck.GlobalStability || combination.Situation != GeotechnicalSituation.PersistentTransient)
            throw new NotSupportedException("Questo adattatore richiede la combinazione statica di stabilità globale, distinta dalla resistenza locale della paratia.");
        var ids = new HashSet<string>(prepared.Section.Loads.Select(l => l.Id), StringComparer.Ordinal);
        if (selections.Count != ids.Count || selections.Any(s => s == null || !ids.Contains(s.Id)) || selections.Select(s => s.Id).Distinct().Count() != ids.Count)
            throw new ArgumentException("Ogni azione globale richiede una sola classificazione, favorevolezza e fonte dei coefficienti di combinazione.");
        var a = combination.Actions;
        var coefficients = selections.Select(s =>
        {
            var category = s.Category switch
            {
                ModelActionCategory.PermanentStructural => GeotechnicalActionCategory.Permanent,
                ModelActionCategory.PermanentNonStructural => GeotechnicalActionCategory.NonStructuralPermanent,
                _ => GeotechnicalActionCategory.Variable
            };
            double partial = GeotechnicalActions.Factor(a, category, s.Unfavourable);
            return new EmbeddedWallGlobalActionCoefficient(s, partial);
        }).ToArray();
        var factors = SlopeFactors.FromCombination(combination, 0, 0, undrained, coefficients.ToDictionary(c => c.Selection.Id, c => c.CombinedFactor));
        return new(factors, combination.Reference, Array.AsReadOnly(coefficients));
    }
    public static EmbeddedWallGlobalPrepared Prepare(EmbeddedWallModel input, string stageId, EmbeddedWallGlobalProfile profile, ISlopeHydraulicField? field = null)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        EmbeddedWallAnalysis.Validate(input); var m = EmbeddedWallAnalysis.Snapshot(input);
        var stage = m.Stages.SingleOrDefault(s => s.Id == stageId) ?? throw new ArgumentException("Fase globale inesistente.");
        if (!profile.CharacteristicInputsConfirmed || string.IsNullOrWhiteSpace(profile.Source))
            throw new NotSupportedException("Stabilità globale: confermare dati caratteristici, geometria continua e fonte del modello. Non riapplicare M2 a terreni già ridotti.");
        if (profile.ValleyDirection is not (-1 or 1) || !Finite(profile.Thickness) || !Finite(profile.LeftExtent) || !Finite(profile.RightExtent)
            || profile.Thickness <= 0 || profile.LeftExtent <= profile.Thickness / 2 || profile.RightExtent <= profile.Thickness / 2)
            throw new ArgumentException("Stabilità globale: spessore positivo, estensioni oltre le facce e direzione di scorrimento ±1 richiesti.");
        if (stage.ActiveSupports.Count > 0)
            throw new NotSupportedException("La fase contiene sostegni: servono geometria dei bulbi o strutture di contrasto per determinare le azioni esterne alla superficie globale. Nessuna reazione locale viene sommata automaticamente.");
        if (stage.DistributedLoads.Count > 0)
            throw new NotSupportedException("Carichi distribuiti su tratti verticali: il verificatore globale richiede il trasferimento delle sole porzioni interne alla superficie. Il risultante locale non viene concentrato automaticamente in un punto.");
        if (m.FixTopDisplacement || m.FixTopRotation || m.FixBottomDisplacement || m.FixBottomRotation)
            throw new NotSupportedException("Vincoli strutturali assegnati: definire il sistema esterno di vincolo nel modello globale prima della verifica.");
        if (stage.Left.Elevation > m.Top || stage.Right.Elevation > m.Top || stage.Left.Elevation < m.Bottom || stage.Right.Elevation < m.Bottom)
            throw new NotSupportedException("Geometria globale corrente: terreni alle facce compresi fra testa e piede della parete continua.");
        if (m.LeftSoils.Count == 0 || m.RightSoils.Count == 0 || m.Segments.Any(s => s.WeightPerLength <= 0))
            throw new ArgumentException("Stabilità globale: due colonne profonde e peso totale positivo di ciascun tratto della paratia richiesti.");
        if (stage.Left.WaterElevation > stage.Left.Elevation || stage.Right.WaterElevation > stage.Right.Elevation)
            throw new NotSupportedException("Acqua sopra il terreno: introdurre anche le azioni sulle frontiere esterne nel modello globale; il solo campo interstiziale non basta.");
        bool rightValley = profile.ValleyDirection == 1;
        var valley = rightValley ? stage.Right : stage.Left; var uphill = rightValley ? stage.Left : stage.Right;
        var valleySoils = rightValley ? m.RightSoils : m.LeftSoils; var uphillSoils = rightValley ? m.LeftSoils : m.RightSoils;
        double half = profile.Thickness / 2, left = -(rightValley ? profile.RightExtent : profile.LeftExtent), right = rightValley ? profile.LeftExtent : profile.RightExtent;
        double vy = valley.Elevation - m.Top, uy = uphill.Elevation - m.Top;
        if (vy > uy) throw new ArgumentException("La direzione selezionata deve portare verso il terreno più basso.");
        SlopePoint[] surface = new[] { new SlopePoint(left, vy), new SlopePoint(-half, vy), new SlopePoint(-half, 0), new SlopePoint(half, 0),
            new SlopePoint(half, uy), new SlopePoint(right, uy) }.Distinct().ToArray();
        SlopeLayer[] Layers(IReadOnlyList<EmbeddedWallSoil> soils) => soils.Select(s => new SlopeLayer(s.Soil, s.Bottom - m.Top)).ToArray();
        var bodies = m.Segments.Select(s => new SlopeBody(s.SectionId, new[] { new SlopePoint(-half, s.Bottom - m.Top), new SlopePoint(half, s.Bottom - m.Top),
            new SlopePoint(half, s.Top - m.Top), new SlopePoint(-half, s.Top - m.Top) }, s.WeightPerLength / (m.TributaryWidth * profile.Thickness))).ToArray();
        var loads = new List<SlopeLoad>();
        if (valley.Surcharge != 0) loads.Add(new(rightValley ? "RightSurcharge" : "LeftSurcharge", left, -half, vy, valley.Surcharge, 0, 0, true));
        if (uphill.Surcharge != 0) loads.Add(new(rightValley ? "LeftSurcharge" : "RightSurcharge", half, right, uy, uphill.Surcharge, 0, 0, true));
        if (stage.DistributedLoad != 0) loads.Add(new("StructureDistributed", 0, 0, (m.Bottom - m.Top) / 2, 0,
            profile.ValleyDirection * stage.DistributedLoad * (m.Top - m.Bottom) / m.TributaryWidth, 0, false));
        for (int i = 0; i < stage.PointLoads.Count; i++)
        {
            var p = stage.PointLoads[i]; if (p.DownwardForce == 0 && p.HorizontalForce == 0 && p.Moment == 0) continue;
            // du/d(depth) is positive counterclockwise in physical x,z. Slope driving moment is clockwise;
            // reflecting x when the valley is on the physical right reverses the couple.
            loads.Add(new("PointLoad-" + i, 0, 0, p.Elevation - m.Top, p.DownwardForce / m.TributaryWidth,
                profile.ValleyDirection * p.HorizontalForce / m.TributaryWidth, profile.ValleyDirection * p.Moment / m.TributaryWidth, false));
        }
        IReadOnlyList<SlopePoint> water = profile.Water;
        if (field == null && water.Count == 0 && (stage.Left.WaterElevation.HasValue || stage.Right.WaterElevation.HasValue))
        {
            if (stage.Left.WaterElevation != stage.Right.WaterElevation)
                throw new NotSupportedException("Falde diverse: assegnare il profilo piezometrico globale o un campo FEM convergente; non viene interpolato implicitamente sotto il piede.");
            double level = stage.Left.WaterElevation!.Value - m.Top;
            water = new[] { new SlopePoint(left, level), new SlopePoint(right, level) };
        }
        var section = field == null ? new SlopeSection(surface, Layers(uphillSoils), water, bodies, loads, -half, half, Layers(valleySoils))
            : new SlopeSection(surface, Layers(uphillSoils), water, bodies, loads, -half, half, Layers(valleySoils), 0, SoilUnits.WaterUnitWeight, field);
        return new(m, stageId, profile, section);
    }
    public static SlopeResult Calculate(EmbeddedWallGlobalPrepared prepared, IReadOnlyList<SlopeFactors> cases, CancellationToken cancellation = default)
    {
        if (prepared == null) throw new ArgumentNullException(nameof(prepared));
        if (cases == null) throw new ArgumentNullException(nameof(cases));
        cancellation.ThrowIfCancellationRequested();
        var ids = new HashSet<string>(prepared.Section.Loads.Select(l => l.Id), StringComparer.Ordinal);
        foreach (var c in cases)
            if (c == null || ids.Any(id => !c.Loads.ContainsKey(id)) || c.Loads.Keys.Any(id => !ids.Contains(id)))
                throw new ArgumentException("Coefficienti globali espliciti richiesti per ogni azione presente, senza identificativi sconosciuti.");
        return SlopeStability.Calculate(prepared.Section, prepared.Search, cases, cancellation);
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

public sealed class EmbeddedWallGlobalActionCoefficient
{
    public GeotechnicalActionSelection Selection { get; }
    public double PartialFactor { get; }
    public double CombinedFactor => PartialFactor * Selection.CombinationFactor;
    internal EmbeddedWallGlobalActionCoefficient(GeotechnicalActionSelection selection, double partial) { Selection = selection; PartialFactor = partial; }
}
public sealed class EmbeddedWallGlobalCase
{
    public SlopeFactors Factors { get; }
    public string Reference { get; }
    public IReadOnlyList<EmbeddedWallGlobalActionCoefficient> Actions { get; }
    internal EmbeddedWallGlobalCase(SlopeFactors factors, string reference, IReadOnlyList<EmbeddedWallGlobalActionCoefficient> actions)
    { Factors = factors; Reference = reference; Actions = actions; }
}
