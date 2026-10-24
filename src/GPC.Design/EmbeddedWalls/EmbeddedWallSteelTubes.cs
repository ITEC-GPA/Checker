using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;

namespace GPC.Design.EmbeddedWalls;

/// <summary>One row of identical tubes with equal load sharing, spacing in mm. No implicit multirow/arching model.</summary>
public sealed record EmbeddedWallSteelTube(string SectionId, CircularHollowSteelSection Section, double Spacing,
    bool SingleRowEqualSharingConfirmed, string RepresentationSource)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SectionId) || Section is null || !PortableMath.IsFinite(Spacing) || Spacing < Section.Diameter)
            throw new ArgumentException("Unique section identifier, tube properties and spacing no smaller than the tube diameter required.");
    }
    /// <summary>Elastic EI for the represented strip, Nmm². Pressure acts on the declared strip width.</summary>
    public double FlexuralRigidity(double tributaryWidth)
    {
        Validate();
        if (!PortableMath.IsFinite(tributaryWidth) || tributaryWidth <= 0) throw new ArgumentException("Positive represented strip width required.");
        if (!SingleRowEqualSharingConfirmed || string.IsNullOrWhiteSpace(RepresentationSource))
            throw new ArgumentException("Document the single row and equal sharing assumption before assigning its equivalent rigidity.");
        double rigidity = Section.ElasticModulus * Section.Inertia * tributaryWidth / Spacing;
        if (!PortableMath.IsFinite(rigidity) || rigidity <= 0) throw new ArithmeticException("Nonfinite equivalent wall rigidity.");
        return rigidity;
    }
}

/// <summary>Local section result at one cut, one stage and one case. Actions are per physical tube, N positive tension.</summary>
public sealed record EmbeddedWallSteelTubeResult(string CaseId, string StageId, string SectionId, double Elevation, string Side,
    double? NormalForce, double? Shear, double? Moment, double? StripToMemberFactor, CircularHollowSectionResult? Verification,
    EngineeringOutcome Outcome, string Diagnostic);

public static class EmbeddedWallSteelTubes
{
    /// <summary>All N-M-V components use the SAME cut and spacing/strip-width multiplier exactly once.
    /// No action factors or envelope recombination. Section checks do not replace member/ground stability.</summary>
    public static IReadOnlyList<EmbeddedWallSteelTubeResult> Check(WallAnalysisResult analysis, double tributaryWidth,
        IReadOnlyList<EmbeddedWallSteelTube> definitions, StandardEN1993p11? standard, EmbeddedWallSteelCheckKind kind,
        CancellationToken cancellation = default)
    {
        Guard.NotNull(analysis); Guard.NotNull(definitions); cancellation.ThrowIfCancellationRequested();
        if (!analysis.IsComplete) throw new InvalidOperationException("Incomplete histories cannot be certified by section verification.");
        if (!PortableMath.IsFinite(tributaryWidth) || tributaryWidth <= 0 || !PortableEnum.IsDefined(kind)) throw new ArgumentException("Explicit width and check kind required.");
        // Tube actions are strip actions × spacing/width: a width other than the analysed one would scale them wrongly.
        if (Math.Abs(tributaryWidth - analysis.TributaryWidth) > 1e-7) throw new ArgumentException("The checked width must be the analysed tributary width.");
        var sections = new Dictionary<string, EmbeddedWallSteelTube>(StringComparer.Ordinal);
        foreach (var item in definitions)
        {
            if (item is null) throw new ArgumentException("Null section definition."); item.Validate();
            if (!sections.TryAdd(item.SectionId, item)) throw new ArgumentException("Duplicate tube section definition.");
        }
        var rows = new List<EmbeddedWallSteelTubeResult>();
        foreach (var stage in analysis.Stages)
        foreach (var point in stage.Points)
        {
            cancellation.ThrowIfCancellationRequested(); CircularHollowSectionResult? verification = null;
            double? factor = null, normal = null, shear = null, moment = null;
            string diagnostic;
            if (!sections.TryGetValue(point.SectionId, out var definition)) diagnostic = "Sezione tubolare non definita.";
            else if (!definition.SingleRowEqualSharingConfirmed || string.IsNullOrWhiteSpace(definition.RepresentationSource))
                diagnostic = "Documentare fila unica di tubi identici e ripartizione uniforme; nessuna equivalenza automatica per più file o effetto arco.";
            else
            {
                factor = definition.Spacing / tributaryWidth;
                normal = -point.NormalForce * factor.Value; shear = point.Shear * factor.Value; moment = -point.Moment * factor.Value;
                if (!PortableMath.IsFinite(normal.Value) || !PortableMath.IsFinite(shear.Value) || !PortableMath.IsFinite(moment.Value))
                    throw new ArithmeticException("Nonfinite member actions after strip conversion.");
                if (kind == EmbeddedWallSteelCheckKind.StaticUltimate)
                    verification = Ntc2018CircularHollowSections.Check(definition.Section, normal.Value, shear.Value, moment.Value, standard, cancellation);
                diagnostic = kind == EmbeddedWallSteelCheckKind.StaticUltimate
                    ? "Azioni concomitanti per singolo tubo; N positivo a trazione. Resistenza locale in Checker Steel, stabilità della membratura e del terreno separate. " + definition.RepresentationSource
                    : "Azioni conservate; il percorso corrente verifica soltanto SLU statici con provenienza dichiarata.";
            }
            rows.Add(new(analysis.CaseId, stage.Id, point.SectionId, point.Elevation, point.Side, normal, shear, moment, factor,
                verification, verification?.Outcome ?? EngineeringOutcome.NotEvaluated, diagnostic));
        }
        return rows.AsReadOnly();
    }
}
