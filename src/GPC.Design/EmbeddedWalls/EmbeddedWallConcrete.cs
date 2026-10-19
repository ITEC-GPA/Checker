using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;

namespace GPC.Design.EmbeddedWalls;

/// <summary>A rectangular strip without shear links. All dimensions mm, material strengths MPa.
/// Front is the right-hand face of the wall (positive displacement direction); back is the left-hand face.</summary>
public sealed record EmbeddedWallRcStrip(string Id, double Width, double Thickness, double ClearCover,
    int BackBars, double BackDiameter, int FrontBars, double FrontDiameter, double Fck, double Fyk,
    double SteelModulus, double UltimateSteelStrain, double Creep, string Exposure)
{
    private WallStrip Strip() => new(WallMember.Stem, Width, Thickness, ClearCover, BackBars, BackDiameter, FrontBars, FrontDiameter);
    private ConcreteMaterialEN1992 Concrete() => new("Paratia " + Id, Fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
    public double GrossFlexuralRigidity
    {
        get { Validate(); return Concrete().E * Width * Math.Pow(Thickness, 3) / 12; }
    }
    public void Validate()
    {
        _ = Strip();
        if (string.IsNullOrWhiteSpace(Id) || !double.IsFinite(Fck) || Fck is < 12 or > 90 || !double.IsFinite(Fyk) || Fyk <= 0
            || !double.IsFinite(SteelModulus) || SteelModulus <= Fyk || !double.IsFinite(UltimateSteelStrain) || UltimateSteelStrain <= Fyk / SteelModulus
            || !double.IsFinite(Creep) || Creep < 0 || string.IsNullOrWhiteSpace(Exposure))
            throw new ArgumentException("Complete ordinary-concrete strip, reinforcement and serviceability properties required.");
    }
    /// <summary>Creates an independent section session. No shared mutable material, mesh or verifier between analyses.</summary>
    public WallSectionSession CreateSession()
    {
        Validate();
        var shape = new Shape2d(new Polygon2d(new[] { new Point2d(0, 0), new Point2d(Width, 0), new Point2d(Width, Thickness), new Point2d(0, Thickness) }));
        var section = new ReinforcedConcreteSection(shape, Concrete());
        var steel = new SteelMaterial("Armatura " + Id, SteelModulus, Fyk, Fyk, UltimateSteelStrain,
            SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        Add(FrontBars, FrontDiameter, ClearCover + FrontDiameter / 2);
        Add(BackBars, BackDiameter, Thickness - ClearCover - BackDiameter / 2);
        return new WallSectionSession(Strip(), section, new StandardNTC2018Concrete(), Creep, Exposure);
        void Add(int count, double diameter, double y)
        {
            var bar = new RebarSectionCircular(diameter, steel);
            section.AddRebars(Enumerable.Range(0, count).Select(i => new ReinforcedConcreteRebar(bar,
                new Point3d(ClearCover + diameter / 2 + (Width - 2 * ClearCover - diameter) * i / (count - 1), y, 0))));
        }
    }
}

public enum EmbeddedWallSectionCheckKind { Ultimate, Characteristic, Frequent, QuasiPermanent }

/// <summary>One cut, one stage and one nonlinear history. Actions are never assembled from separate extrema.</summary>
public sealed record EmbeddedWallSectionResult(string CaseId, string StageId, string SectionId, double Elevation,
    string Side, WallSectionActions Actions, IReadOnlyList<WallSectionCheck> Checks, string Diagnostic);

public static class EmbeddedWallConcrete
{
    /// <summary>Map the beam cut convention (compression positive; positive curvature tensions the left/back face)
    /// to Concrete (compression negative; positive moment tensions its bottom/front face). No unit or width scaling.</summary>
    public static WallSectionActions Actions(WallPointResult point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new WallSectionActions(-point.NormalForce, point.Shear, -point.Moment);
    }

    /// <summary>Check only a completed analysis of the explicitly classified combination. The caller must first run
    /// a separate history for each factored combination; this method never factors or superposes nonlinear results.
    /// Scope: exact NTC2018 profile, ordinary rectangular RC, no links. Detailing and global safety are separate.</summary>
    public static IReadOnlyList<EmbeddedWallSectionResult> Check(WallAnalysisResult analysis, double tributaryWidth,
        IReadOnlyList<EmbeddedWallRcStrip> sections, EmbeddedWallSectionCheckKind kind, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(analysis); ArgumentNullException.ThrowIfNull(sections);
        if (!analysis.IsComplete) throw new InvalidOperationException("A partial or nonconvergent history cannot be certified by section checks.");
        if (!Enum.IsDefined(kind) || !double.IsFinite(tributaryWidth) || tributaryWidth <= 0) throw new ArgumentException("Explicit check type and positive represented width required.");
        var definitions = sections.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var sessions = new Dictionary<string, WallSectionSession>(StringComparer.Ordinal);
        var results = new List<EmbeddedWallSectionResult>();
        foreach (var stage in analysis.Stages)
        foreach (var point in stage.Points)
        {
            cancellation.ThrowIfCancellationRequested();
            var actions = Actions(point);
            if (!definitions.TryGetValue(point.SectionId, out var definition))
            {
                results.Add(new(analysis.CaseId, stage.Id, point.SectionId, point.Elevation, point.Side, actions, Array.Empty<WallSectionCheck>(), "Sezione in c.a. non definita: verifica non eseguita."));
                continue;
            }
            if (Math.Abs(definition.Width - tributaryWidth) > 1e-7) throw new ArgumentException("RC strip width must match the analysed strip; implicit action scaling is forbidden.");
            if (!sessions.TryGetValue(point.SectionId, out var session)) sessions.Add(point.SectionId, session = definition.CreateSession());
            IReadOnlyList<WallSectionCheck> checks;
            if (kind == EmbeddedWallSectionCheckKind.Ultimate) checks = Array.AsReadOnly(new[] { session.CheckMoment(actions, cancellation), session.CheckShear(actions, cancellation).Check });
            else checks = session.CheckServiceability(actions, kind switch
            {
                EmbeddedWallSectionCheckKind.Characteristic => ServiceabilityCombination.Characteristic,
                EmbeddedWallSectionCheckKind.Frequent => ServiceabilityCombination.Frequent,
                EmbeddedWallSectionCheckKind.QuasiPermanent => ServiceabilityCombination.QuasiPermanent,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            }, cancellation).Checks;
            results.Add(new(analysis.CaseId, stage.Id, point.SectionId, point.Elevation, point.Side, actions, checks,
                "NTC 2018; sezione rettangolare senza armatura trasversale. Dettagli costruttivi e verifiche geotecniche separati."));
        }
        return results.AsReadOnly();
    }
}
