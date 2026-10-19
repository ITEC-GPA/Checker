using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Geometry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Design.Concrete;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Piles
{
    /// <summary>Exclusive circular RC session. Reuses PileReinforcement for nominal ULS, existing stress/crack
    /// engines for SLS, existing detailing for geometric rules. Input actions N/Nmm, compression negative.
    /// Ordinary static NTC2018 only. Anchorage, member stability and dissipative detailing are separate.</summary>
    public sealed class CircularRcSectionSession
    {
        readonly object sync = new object();
        readonly CircularRcSection definition;
        readonly ReinforcedConcreteSection section;
        readonly ConcreteMaterialEN1992 concrete;
        readonly SteelMaterial steel;
        readonly StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
        readonly CoordinateSystem axes;
        readonly PileReinforcement pile;
        readonly CrackSectionGeometry geometry;
        readonly StressLimitContext limits;
        readonly LegacySectionCalculation service;
        readonly Lazy<LegacySectionCalculation> uncracked;
        readonly string exposure;
        readonly bool shearConfirmed;
        static readonly double ForceUnit = GPC.Model.Units.ConvertForceToDefault(1, GPC.Model.Units.Knm);
        static readonly double MomentUnit = GPC.Model.Units.ConverMomentToDefault(1, GPC.Model.Units.Knm);
        public double Fcd => Math.Abs(concrete.CalculateFcd(standard));
        public double Fyd => Math.Abs(steel.CalculateFyd(standard));

        public CircularRcSectionSession(CircularRcSection definition, bool shearModelConfirmed, double leverFactor,
            double creep, string exposure, double aggregate, double? minimumDurabilityCover, double coverDeviation,
            PileDetailingMode detailingMode, bool compressionBarsRestrained, bool endZonesConfirmed)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.MaterialSource) || !Finite(creep) || creep < 0 || string.IsNullOrWhiteSpace(exposure)
                || !Finite(leverFactor) || leverFactor <= 0 || leverFactor > .9 || !Enum.IsDefined(typeof(PileDetailingMode), detailingMode)
                || !Finite(aggregate) || aggregate < 0 || !Finite(coverDeviation) || coverDeviation < 0
                || minimumDurabilityCover.HasValue && (!Finite(minimumDurabilityCover.Value) || minimumDurabilityCover < 0))
                throw new ArgumentException("Explicit material source, serviceability, shear and detailing data required.");
            this.exposure = exposure; shearConfirmed = shearModelConfirmed;
            section = definition.CreateSection(); concrete = (ConcreteMaterialEN1992)section.ConcreteMaterial;
            steel = section.Rebars.First().RebarMaterial;
            axes = new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            geometry = CrackSectionGeometry.From(section); limits = StressLimitContext.Resolve(standard, section);
            var ultimate = Checker(false, false, 0);
            pile = new PileReinforcement(new PileRcParameters {
                Solver = ultimate.SectionSolver, Axes = axes, Section = section, Standard = standard,
                Diameter = definition.Diameter, Fck = definition.Fck, Fctm = concrete.Fctm, Fctk05 = concrete.Fctk05,
                Fcd = Fcd, Fyd = Fyd, Es = definition.SteelModulus, Fyk = definition.Fyk,
                Cover = definition.Cover, LinkDiameter = definition.LinkDiameter, LinkSpacing = definition.LinkSpacing,
                LeverFactor = leverFactor, ShearModelConfirmed = shearConfirmed, DesignActionsConfirmed = true,
                // Foundation-pile seismic minima are not silently applied to a retaining-wall member.
                PileMinimumRequirements = false, DetailingMode = detailingMode, Aggregate = aggregate,
                MinimumDurabilityCover = minimumDurabilityCover, CoverDeviation = coverDeviation,
                CompressionBarsRestrained = compressionBarsRestrained, EndZonesConfirmed = endZonesConfirmed });
            service = new LegacySectionCalculation(Checker(true, false, creep));
            uncracked = new Lazy<LegacySectionCalculation>(() => new LegacySectionCalculation(Checker(true, true, creep)));
        }

        SectionCheckerModelCode2010 Checker(bool linear, bool tension, double creep)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
                creep, 0, tension, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tension);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative); return checker;
        }

        public IReadOnlyList<WallSectionCheck> CheckUltimate(WallSectionActions action, CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (sync)
            {
                cancellation.ThrowIfCancellationRequested();
                var result = pile.Check(new PileAction { Depth = 0, N = -action.N / ForceUnit, V = action.V / ForceUnit, M = action.M / MomentUnit });
                cancellation.ThrowIfCancellationRequested();
                double? capacity = action.M >= 0 ? result.MRdPositive : -result.MRdNegative;
                return Array.AsReadOnly(new[] {
                    new WallSectionCheck("CircularRC.NM", "Nmm", Math.Abs(action.M), capacity * MomentUnit,
                        result.Execution, result.Message + " NTC 2018 §4.1.2.3.4, N concomitante; ancoraggio delle barre e stabilità della membratura separati."),
                    new WallSectionCheck("CircularRC.Shear", "N", Math.Abs(action.V), result.ShearRatio.HasValue ? result.VRd * ForceUnit : null,
                        result.Execution, shearConfirmed ? "NTC 2018 §4.1.2.3.5; modello circolare equivalente e z/d assegnato, staffe singole."
                            : "Modello di taglio circolare non confermato; nessuna capacità attribuita.") });
            }
        }

        public IReadOnlyList<DetailingCheck> CheckConstruction(double maximumCompression, CancellationToken cancellation = default(CancellationToken))
        {
            if (!Finite(maximumCompression)) throw new ArgumentException("Finite compression in N required.");
            lock (sync) { cancellation.ThrowIfCancellationRequested(); return pile.ConstructionChecks(maximumCompression / ForceUnit); }
        }

        public WallServiceabilityResult CheckServiceability(WallSectionActions action, ServiceabilityCombination combination,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!Enum.IsDefined(typeof(ServiceabilityCombination), combination)) throw new ArgumentOutOfRangeException(nameof(combination));
            lock (sync)
            {
                cancellation.ThrowIfCancellationRequested();
                var checks = new List<WallSectionCheck>(); SectionResponse response = null; double? curvature = null;
                try
                {
                    var input = new SectionAnalysisInput(new ResultBeamForces(action.N, action.V, 0, 0, action.M, 0, axes));
                    response = service.Solve(input, cancellation);
                    if (response.Diagnostics.Status != CalculationStatus.Completed) throw new InvalidOperationException(response.Diagnostics.Message);
                    curvature = Math.Sign(action.M) * Math.Abs(response.Strain.ChiY);
                    var stress = StressLimitCheck.Evaluate(response, limits, combination);
                    if (stress.ConcreteLimit.HasValue)
                        checks.Add(new WallSectionCheck("CircularRC.ConcreteStress", "MPa", Math.Max(0, -stress.ConcreteMinStress), stress.ConcreteLimit));
                    if (combination == ServiceabilityCombination.Characteristic)
                        checks.Add(new WallSectionCheck("CircularRC.SteelStress", "MPa", stress.SteelMaxStress, StressLimitCheck.SteelLimit(standard, steel)));
                    else
                    {
                        double? elasticMaximum = null;
                        double UncrackedStress()
                        {
                            if (elasticMaximum.HasValue) return elasticMaximum.Value;
                            var elastic = uncracked.Value.Solve(input, cancellation);
                            if (elastic.Diagnostics.Status != CalculationStatus.Completed) throw new InvalidOperationException(elastic.Diagnostics.Message);
                            elasticMaximum = elastic.Concrete.Max(p => p.Stress); return elasticMaximum.Value;
                        }
                        var options = SectionCrackOptions.Default.WithValidateAtUse(true).WithTrace(true)
                            .WithAnalysisContext(concrete.E, response.PsiRebar).WithUnreinforcedTensileZoneFails(true);
                        var crack = SectionCrackCheck.Evaluate(new SectionCrackInput(standard, combination, exposure, false, null,
                            geometry, response.Strain.ToStrainPlane(), response.Bars.Select(p => p.Stress), response.Linear, false, false,
                            steel.E, concrete.Ecm, concrete.Fctm, false, true, definition.Cover + definition.LinkDiameter,
                            definition.Cover + definition.LinkDiameter, null, UncrackedStress, false, options));
                        checks.Add(crack.Passed == false && !crack.Width.HasValue && crack.Limit.HasValue
                            ? WallSectionCheck.NotSatisfiedWithoutDemand("CircularRC.Cracking", "mm", crack.Limit.Value, crack.Status)
                            : new WallSectionCheck("CircularRC.Cracking", "mm", crack.Width ?? 0, crack.Width.HasValue ? crack.Limit : null,
                                ExecutionStatus.Completed, crack.Status));
                        cancellation.ThrowIfCancellationRequested();
                        return new WallServiceabilityResult(checks, curvature, response, crack, elasticMaximum);
                    }
                    cancellation.ThrowIfCancellationRequested(); return new WallServiceabilityResult(checks, curvature, response);
                }
                catch (Exception error) when (!(error is OperationCanceledException))
                {
                    checks.Add(new WallSectionCheck("CircularRC.Serviceability", "Nmm", Math.Abs(action.M), null, ExecutionStatus.Error, error.Message));
                    return new WallServiceabilityResult(checks, curvature, response);
                }
            }
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
