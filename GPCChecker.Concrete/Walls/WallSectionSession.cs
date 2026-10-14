using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Walls
{
    /// <summary>Totals on one strip: N and V in N, M in Nmm. Compression negative; positive M tensions the bottom face.</summary>
    public sealed class WallSectionActions
    {
        public double N { get; }
        public double V { get; }
        public double M { get; }
        public WallSectionActions(double n, double v, double m)
        {
            if (!WallStrip.Finite(n) || !WallStrip.Finite(v) || !WallStrip.Finite(m)) throw new ArgumentException("Finite wall actions required.");
            N = n; V = v; M = m;
        }
    }

    public sealed class WallServiceabilityResult
    {
        public IReadOnlyList<WallSectionCheck> Checks { get; }
        /// <summary>Total signed curvature, 1/mm, including creep. Null if the strain plane was not calculated.</summary>
        public double? Curvature { get; }
        /// <summary>Solver gradient before creep amplification, 1/mm; diagnostic only, not the curvature to integrate for displacements.</summary>
        public double? ElasticEquivalentCurvature { get; }
        public SectionResponse Response { get; }
        /// <summary>Original cracking-engine result; the separate uncracked proof, when present, is reported in Checks.</summary>
        public SectionCrackResult CrackResponse { get; }
        public double? UncrackedMaximumStress { get; }
        public bool ProvenUncracked { get; }
        internal WallServiceabilityResult(IEnumerable<WallSectionCheck> checks, double? curvature, SectionResponse response,
            SectionCrackResult crack = null, double? uncrackedMaximumStress = null, bool provenUncracked = false)
        {
            Checks = Array.AsReadOnly(checks.ToArray()); ElasticEquivalentCurvature = curvature; Response = response;
            Curvature = curvature * (response != null && response.Linear ? Homogenization.CreepFactor(response.PsiRebar) : 1);
            CrackResponse = crack; UncrackedMaximumStress = uncrackedMaximumStress; ProvenUncracked = provenUncracked;
        }
    }

    /// <summary>
    /// Native retaining-wall section session. Reuses section resistance, stress-limit and crack engines.
    /// The caller transfers exclusive ownership of the prepared section/materials/standard and must not mutate them afterwards.
    /// Mesh construction is not repeated; calls in this session are serialized, independent sessions may run in parallel.
    /// Current scope: NTC ordinary concrete, rectangular strip without tendons, structural steel or shear links.
    /// </summary>
    public sealed partial class WallSectionSession
    {
        readonly object sync = new object();
        readonly WallStrip strip;
        readonly ReinforcedConcreteSection section;
        readonly StandardNTC2018Concrete standard;
        readonly ConcreteMaterialEuropeanCommon concrete;
        readonly SteelMaterial steel;
        readonly CoordinateSystem axes;
        readonly CrackSectionGeometry geometry;
        readonly StressLimitContext stressLimits;
        readonly double creep;
        readonly string exposure;
        readonly SectionCheckerModelCode2010 ultimate;
        readonly LegacySectionCalculation resistance, service;
        readonly Lazy<LegacySectionCalculation> uncracked;
        readonly Dictionary<Tuple<double, int>, SectionResistanceResponse> momentCache = new Dictionary<Tuple<double, int>, SectionResistanceResponse>();
        static readonly double UnitMomentNmm = GPC.Model.Units.ConverMomentToDefault(1, GPC.Model.Units.Knm);

        public WallSectionSession(WallStrip strip, ReinforcedConcreteSection preparedSection, StandardNTC2018Concrete standard,
            double creep, string exposure)
        {
            this.strip = strip ?? throw new ArgumentNullException(nameof(strip));
            section = preparedSection ?? throw new ArgumentNullException(nameof(preparedSection));
            this.standard = standard ?? throw new ArgumentNullException(nameof(standard));
            if (standard.GetType() != typeof(StandardNTC2018Concrete)) throw new NotSupportedException("Exact NTC 2018 profile required.");
            if (!WallStrip.Finite(creep) || creep < 0) throw new ArgumentOutOfRangeException(nameof(creep));
            this.creep = creep; this.exposure = exposure;
            concrete = section.ConcreteMaterial as ConcreteMaterialEuropeanCommon ?? throw new NotSupportedException("Ordinary European concrete required.");
            if (section.Rebars.Count() != strip.TopCount + strip.BottomCount || section.Rebars.Any(b => b.EpsilonP != 0 || b.RebarMaterial.SteelType != SteelMaterial.SteelTypes.Rebar))
                throw new ArgumentException("Prepared section must contain the ordinary bars of the strip and no prestressing.");
            steel = section.Rebars.First().RebarMaterial;
            if (section.Rebars.Any(b => b.RebarMaterial.E != steel.E || b.RebarMaterial.Fyk != steel.Fyk))
                throw new NotSupportedException("The strip requires one longitudinal reinforcement material.");
            geometry = CrackSectionGeometry.From(section);
            ValidateGeometry();
            axes = new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            stressLimits = StressLimitContext.Resolve(standard, section);
            ultimate = CreateChecker(false, false);
            resistance = new LegacySectionCalculation(ultimate);
            service = new LegacySectionCalculation(CreateChecker(true, false));
            uncracked = new Lazy<LegacySectionCalculation>(() => new LegacySectionCalculation(CreateChecker(true, true)));
        }

        void ValidateGeometry()
        {
            const double toleranceMm = 1e-7;
            if (geometry.Holes.Count != 0 || Math.Abs(geometry.Width - strip.Width) > toleranceMm || Math.Abs(geometry.Height - strip.Thickness) > toleranceMm)
                throw new ArgumentException("Prepared geometry differs from the wall strip.");
            double minX = geometry.Outline.Min(p => p.X), maxX = geometry.Outline.Max(p => p.X);
            double minY = geometry.Outline.Min(p => p.Y), maxY = geometry.Outline.Max(p => p.Y);
            if (geometry.Outline.Any(p => Math.Min(Math.Abs(p.X - minX), Math.Abs(p.X - maxX)) > toleranceMm ||
                Math.Min(Math.Abs(p.Y - minY), Math.Abs(p.Y - maxY)) > toleranceMm))
                throw new ArgumentException("Rectangular prepared outline required.");
            foreach (var face in new[] { WallFace.Top, WallFace.Bottom })
            {
                bool top = face == WallFace.Top;
                double diameter = top ? strip.TopDiameter : strip.BottomDiameter;
                double y = top ? maxY - strip.Cover - diameter / 2 : minY + strip.Cover + diameter / 2;
                var bars = geometry.Bars.Where(b => Math.Abs(b.Y - y) <= toleranceMm).OrderBy(b => b.X).ToArray();
                int count = top ? strip.TopCount : strip.BottomCount;
                if (bars.Length != count || bars.Any(b => Math.Abs(b.Diameter - diameter) > toleranceMm))
                    throw new ArgumentException("Prepared face reinforcement differs from the wall strip.");
                for (int i = 0; i < count; i++)
                {
                    double x = minX + strip.Cover + diameter / 2 + (strip.Width - 2 * strip.Cover - diameter) * i / (count - 1);
                    if (Math.Abs(bars[i].X - x) > toleranceMm) throw new ArgumentException("Equally spaced bars with the declared clear cover required.");
                }
            }
        }

        SectionCheckerModelCode2010 CreateChecker(bool linear, bool tension)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
                linear ? creep : 0, 0, tension, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tension);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        SectionAnalysisInput Input(WallSectionActions action) => new SectionAnalysisInput(new ResultBeamForces(action.N, action.V, 0, 0, action.M, 0, axes));

        public WallReinforcementChecks CheckLongitudinalAreas() => WallStripDetailing.Evaluate(strip, concrete.Fctm, steel.Fyk);

        /// <summary>Exact-N, single-axis moment capacity. A zero applied M still probes the positive direction, as in the existing wall check.</summary>
        public WallSectionCheck CheckMoment(WallSectionActions action, CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (sync)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    int sign = action.M >= 0 ? 1 : -1; var key = Tuple.Create(action.N, sign);
                    if (!momentCache.TryGetValue(key, out var response))
                    {
                        response = resistance.SolveResistance(Input(new WallSectionActions(action.N, 0, sign * UnitMomentNmm)), cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        momentCache.Add(key, response);
                    }
                    bool valid = response.Diagnostics.Status == CalculationStatus.Completed && !response.IsZeroDemand && response.N.HasValue && response.M1.HasValue && response.M2.HasValue;
                    if (valid) valid = Math.Abs(response.N.Value - action.N) <= DomainPointAxialTolerance.Calculate(ultimate.SectionSolver, action.N)
                        && response.M1.Value * sign >= 0 && Math.Abs(response.M2.Value) <= Math.Max(UnitMomentNmm, Math.Abs(response.M1.Value) * .001);
                    return new WallSectionCheck("Wall.SectionMoment", "Nmm", Math.Abs(action.M), valid ? Math.Abs(response.M1.Value) : (double?)null,
                        valid ? ExecutionStatus.Completed : ExecutionStatus.Error, response.Diagnostics.Message);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                { return new WallSectionCheck("Wall.SectionMoment", "Nmm", Math.Abs(action.M), null, ExecutionStatus.Error, e.Message); }
            }
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
                    var input = Input(action); response = service.Solve(input, cancellation);
                    if (response.Diagnostics.Status != CalculationStatus.Completed) throw new InvalidOperationException(response.Diagnostics.Message);
                    curvature = Math.Sign(action.M) * Math.Abs(response.Strain.ChiY);
                    var stress = StressLimitCheck.Evaluate(response, stressLimits, combination);
                    if (stress.ConcreteLimit.HasValue) checks.Add(new WallSectionCheck("Wall.ConcreteStress", "MPa", Math.Max(0, -stress.ConcreteMinStress), stress.ConcreteLimit));
                    if (combination == ServiceabilityCombination.Characteristic)
                        checks.Add(new WallSectionCheck("Wall.SteelStress", "MPa", stress.SteelMaxStress, StressLimitCheck.SteelLimit(standard, steel)));
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
                        var options = SectionCrackOptions.Default.WithValidateAtUse(true).WithTrace(true).WithAnalysisContext(concrete.E, response.PsiRebar);
                        var crack = SectionCrackCheck.Evaluate(new SectionCrackInput(standard, combination, exposure, false, null, geometry,
                            response.Strain.ToStrainPlane(), response.Bars.Select(p => p.Stress), response.Linear, false, false, steel.E, concrete.Ecm, concrete.Fctm,
                            false, true, strip.Cover, strip.Cover, strip.MaximumBarSpacing, UncrackedStress, false, options));
                        bool proveUncracked = !crack.Width.HasValue && (crack.Outcome == CrackOutcome.NoTensileReinforcement || crack.Reason == CrackReason.NoEffectiveSteelOrArea)
                            && UncrackedStress() <= concrete.Fctk05;
                        double? width = proveUncracked ? 0 : crack.Width;
                        checks.Add(new WallSectionCheck("Wall.Cracking", "mm", width ?? 0, width.HasValue ? crack.Limit : null,
                            ExecutionStatus.Completed, proveUncracked ? "Uncracked elastic tensile stress does not exceed fctk,0.05." : crack.Status));
                        cancellation.ThrowIfCancellationRequested();
                        return new WallServiceabilityResult(checks, curvature, response, crack, elasticMaximum, proveUncracked);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    return new WallServiceabilityResult(checks, curvature, response);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    checks.Add(new WallSectionCheck("Wall.Serviceability", "Nmm", Math.Abs(action.M), null, ExecutionStatus.Error, e.Message));
                    return new WallServiceabilityResult(checks, curvature, response);
                }
            }
        }
    }
}
