using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.CompositeBridge;

/// <summary>Structural-steel stress in MPa on global y in mm; tension positive. Strain = stress / Ea.</summary>
public sealed record LinearAxialStressField(double ReferenceY, double StressAtReference, double Slope)
{
    public double Stress(double y) => StressAtReference + Slope * (y - ReferenceY);
    public double Strain(double y, double steelElasticModulus) => Stress(y) / BridgeNumbers.Require(steelElasticModulus, nameof(steelElasticModulus), strict: true);
}

/// <summary>
/// Planar N–Mx adapter to Checker's existing linear solver. Accepts Model composite sections with any number
/// of steel pieces, including multiple webs. The section must have principal axes parallel to global x/y
/// and one structural-steel elastic modulus. No torsion, biaxial bending or closed-cell shear-flow model.
/// </summary>
public static class CompositeLinearStressSolver
{
    /// <param name="section">Model section; caller must not mutate it while solving.</param>
    /// <param name="forceN">Axial force in N, tension positive.</param>
    /// <param name="momentNmm">Moment in Nmm at the application point, positive compresses upper fibres.</param>
    /// <param name="applicationX">Global x coordinate in mm.</param>
    /// <param name="applicationY">Global y coordinate in mm.</param>
    /// <param name="effectivePhi">Product psiL * phi, as expected by the native solver.</param>
    /// <param name="standard">Concrete standard for the native Checker context.</param>
    public static LinearAxialStressField Solve(ReinforcedConcreteSection section, double forceN, double momentNmm,
        double applicationX, double applicationY, double effectivePhi, BridgeStandard standard)
    {
        if (section is null) throw new ArgumentNullException(nameof(section));
        BridgeNumbers.Require(forceN, nameof(forceN), double.NegativeInfinity);
        BridgeNumbers.Require(momentNmm, nameof(momentNmm), double.NegativeInfinity);
        BridgeNumbers.Require(applicationX, nameof(applicationX), double.NegativeInfinity);
        BridgeNumbers.Require(applicationY, nameof(applicationY), double.NegativeInfinity);
        BridgeNumbers.Require(effectivePhi, nameof(effectivePhi));
        if (standard != BridgeStandard.Ntc2018 && standard != BridgeStandard.Eurocode4) throw new ArgumentOutOfRangeException(nameof(standard));
        if (forceN == 0 && momentNmm == 0) return new(applicationY, 0, 0);
        var axes = new CoordinateSystem(new Point2d(applicationX, applicationY), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
            SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, effectivePhi, 0, true, 16);
        SectionCheckerModelCode2010 checker;
        lock (CompositeSolverSynchronization.Construction)
            checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options,
                standard == BridgeStandard.Ntc2018 ? new StandardNTC2018Concrete() : new StandardEN1992p11(), true, -1, new StandardEN1993p11());
        var response = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(forceN, 0, 0, 0, momentNmm, 0, axes), effectivePhi, 0);
        var vertices = response.GetStructuralSteelVerticesTension(effectivePhi).OrderBy(v => v.point.Y).ToArray();
        if (vertices.Length < 2 || vertices.Last().point.Y <= vertices.First().point.Y)
            throw new InvalidOperationException("Almeno due quote di acciaio strutturale richieste per il campo N–Mx.");
        var low = vertices.First(); var high = vertices.Last();
        double slope = (high.tension - low.tension) / (high.point.Y - low.point.Y);
        if (!BridgeNumbers.IsFinite(low.tension) || !BridgeNumbers.IsFinite(slope)) throw new InvalidOperationException("Checker non ha restituito un campo tensionale finito.");
        // Keep the actual native reference fibre, avoiding unnecessary stress transports.
        return new(low.point.Y, low.tension, slope);
    }
}
