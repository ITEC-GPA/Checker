using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Concrete;

namespace GPC.Design.EmbeddedWalls;

/// <summary>One row of identical RC piles. Spacing/width in mm, other dimensions from the physical section.
/// A declared shear equivalent is required; a ring of single hoops is not replaced by a spiral.</summary>
public sealed record EmbeddedWallRcPile(string SectionId, CircularRcSection Section, double Spacing,
    bool SingleRowEqualSharingConfirmed, string RepresentationSource, bool ShearModelConfirmed, double LeverFactor,
    double Creep, string Exposure, double Aggregate, double? MinimumDurabilityCover, double CoverDeviation,
    PileDetailingMode DetailingMode, bool CompressionBarsRestrained, bool EndZonesConfirmed)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SectionId) || Section is null || !PortableMath.IsFinite(Spacing) || Spacing < Section.Diameter)
            throw new ArgumentException("Unique section, physical circular RC properties and spacing no smaller than diameter required.");
    }
    public double FlexuralRigidity(double tributaryWidth)
    {
        Validate();
        if (!PortableMath.IsFinite(tributaryWidth) || tributaryWidth <= 0 || !SingleRowEqualSharingConfirmed || string.IsNullOrWhiteSpace(RepresentationSource))
            throw new ArgumentException("Positive strip width and documented single-row pressure transfer/equal load sharing required.");
        double value = Section.GrossFlexuralRigidity * tributaryWidth / Spacing;
        if (!PortableMath.IsFinite(value) || value <= 0) throw new ArithmeticException("Nonfinite equivalent RC wall stiffness.");
        return value;
    }
    public CircularRcSectionSession CreateSession()
    {
        Validate();
        return new(Section, ShearModelConfirmed, LeverFactor, Creep, Exposure, Aggregate, MinimumDurabilityCover,
            CoverDeviation, DetailingMode, CompressionBarsRestrained, EndZonesConfirmed);
    }
}

public sealed record EmbeddedWallRcPileResult(string CaseId, string StageId, string SectionId, double Elevation, string Side,
    double? StripToPileFactor, WallSectionActions? Actions, IReadOnlyList<WallSectionCheck> Checks,
    IReadOnlyList<DetailingCheck> ConstructionChecks, EngineeringOutcome Outcome, string Diagnostic);

public static class EmbeddedWallRcPiles
{
    /// <summary>Concomitant N-M-V per physical pile, N positive tension. Each history owns its section sessions.
    /// Nominal ULS/SLS checks remain distinct from construction, anchorage and member/global stability.</summary>
    public static IReadOnlyList<EmbeddedWallRcPileResult> Check(WallAnalysisResult analysis, double tributaryWidth,
        IReadOnlyList<EmbeddedWallRcPile> definitions, EmbeddedWallSectionCheckKind kind, CancellationToken cancellation = default)
    {
        Guard.NotNull(analysis); Guard.NotNull(definitions); cancellation.ThrowIfCancellationRequested();
        if (!analysis.IsComplete) throw new InvalidOperationException("Incomplete history cannot be certified by RC section checks.");
        if (!PortableMath.IsFinite(tributaryWidth) || tributaryWidth <= 0 || !PortableEnum.IsDefined(kind)) throw new ArgumentException("Explicit width/check kind required.");
        // Pile actions are strip actions × spacing/width: a width other than the analysed one would scale them wrongly.
        if (Math.Abs(tributaryWidth - analysis.TributaryWidth) > 1e-7) throw new ArgumentException("The checked width must be the analysed tributary width.");
        var sections = new Dictionary<string, EmbeddedWallRcPile>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition is null) throw new ArgumentException("Null RC pile definition."); definition.Validate();
            if (!sections.TryAdd(definition.SectionId, definition)) throw new ArgumentException("Duplicate RC pile section.");
        }
        var sessions = new Dictionary<string, CircularRcSectionSession>(StringComparer.Ordinal);
        var rows = new List<EmbeddedWallRcPileResult>();
        foreach (var stage in analysis.Stages)
        {
            var details = new Dictionary<string, IReadOnlyList<DetailingCheck>>(StringComparer.Ordinal);
            foreach (var point in stage.Points)
            {
                cancellation.ThrowIfCancellationRequested(); double? factor = null; WallSectionActions? actions = null;
                IReadOnlyList<WallSectionCheck> checks = []; IReadOnlyList<DetailingCheck> construction = [];
                string diagnostic = "Sezione circolare in c.a. non definita.";
                if (sections.TryGetValue(point.SectionId, out var definition))
                {
                    diagnostic = "Documentare fila unica e ripartizione uniforme; più file, pali secanti ed effetto arco richiedono modelli distinti.";
                    if (definition.SingleRowEqualSharingConfirmed && !string.IsNullOrWhiteSpace(definition.RepresentationSource))
                    {
                        factor = definition.Spacing / tributaryWidth;
                        actions = new WallSectionActions(-point.NormalForce * factor.Value, point.Shear * factor.Value, -point.Moment * factor.Value);
                        if (!sessions.TryGetValue(point.SectionId, out var session)) sessions.Add(point.SectionId, session = definition.CreateSession());
                        checks = kind == EmbeddedWallSectionCheckKind.Ultimate ? session.CheckUltimate(actions, cancellation)
                            : session.CheckServiceability(actions, kind switch {
                                EmbeddedWallSectionCheckKind.Characteristic => ServiceabilityCombination.Characteristic,
                                EmbeddedWallSectionCheckKind.Frequent => ServiceabilityCombination.Frequent,
                                EmbeddedWallSectionCheckKind.QuasiPermanent => ServiceabilityCombination.QuasiPermanent,
                                _ => throw new ArgumentOutOfRangeException(nameof(kind)) }, cancellation).Checks;
                        if (!details.TryGetValue(point.SectionId, out var savedConstruction))
                        {
                            // Only the scalar maximum compression is used by detailing; it does not create an artificial N-M-V vector.
                            double compression = stage.Points.Where(p => p.SectionId == point.SectionId).Max(p => p.NormalForce) * factor.Value;
                            savedConstruction = kind == EmbeddedWallSectionCheckKind.Ultimate ? session.CheckConstruction(compression, cancellation) : [];
                            details.Add(point.SectionId, savedConstruction);
                        }
                        construction = savedConstruction;
                        diagnostic = "NTC 2018; azioni concomitanti per singolo palo, N positivo a trazione. Resistenza nominale e SLE in Checker Concrete. "
                            + "Dettagli costruttivi riportati separatamente; ancoraggi/giunti, instabilità e sisma non certificati. " + definition.RepresentationSource;
                    }
                }
                var outcome = checks.Any(c => c.Outcome == EngineeringOutcome.NotSatisfied) ? EngineeringOutcome.NotSatisfied
                    : checks.Count > 0 && checks.All(c => c.Outcome == EngineeringOutcome.Satisfied) ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotEvaluated;
                rows.Add(new(analysis.CaseId, stage.Id, point.SectionId, point.Elevation, point.Side, factor, actions,
                    checks, construction ?? [], outcome, diagnostic));
            }
        }
        return rows.AsReadOnly();
    }
}
