using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Slenderness
{
    /// <summary>
    /// Model column of a reinforced concrete column (0.27.0.0, method page docs/metodi/ca.colonna-modello.md): EN 1992-1-1 5.8.6(6), equilibrium at the
    /// critical section with the curvature distribution of 5.8.8.2(3)-(4), e2 = l0²/c χ, M2 = |NEd| e2; the curvature comes from the response of the
    /// section at constant N with the design laws of the checker (parabola-rectangle and steel of the bars, net section, concrete in tension neglected
    /// as NTC 2018 §4.1.2.3.9.2, no tension stiffening), at imposed curvature: for each χ in the direction of the moment the strain at the reference
    /// point is found on N by bracketing (Illinois) among the planes within the ultimate strains of the plastic failure domain (εcu at the compressed
    /// edge, εc2 at the pivot C, εud and the compressive ultimate strain at the edges of the bars). Creep (5.8.6(4)): the strains of the concrete law
    /// are multiplied by (1 + φef), with its ultimate strains; the bars are not. Results: M1Rd,max = max over χ of [M(χ) − |NEd| l0²/c χ] with the
    /// mode, the equilibrium for an assigned M0Ed, slenderness, imperfection, the nominal curvature of 5.8.8 (informative) and a trace in Italian.
    /// </summary>
    public static class ModelColumnAnalysis
    {
        /// <summary>Key of <see cref="Exception.Data"/> holding the <see cref="ModelColumnRejection"/> of a rejected request.</summary>
        public const string RejectionKey = "GPC.ModelColumnRejection";

        /// <summary>
        /// The model column on a native checker. Axes: the coordinate system of the forces of the checker (as
        /// <see cref="Response.MomentCurvatureAnalysis.Calculate(Response.MomentCurvatureRequest, SectionCheckerModelCode2010, ReinforcedConcreteSection, CoordinateSystem, double, CancellationToken)"/>),
        /// with the axis of the member of the solver; the direction θ of the moment and the moments are in these axes. The section is the section of
        /// the checker. Rejections: <see cref="ModelColumnException"/>.
        /// </summary>
        public static ModelColumnResult Calculate(ModelColumnRequest request, SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section,
            CoordinateSystem axes, CancellationToken token = default(CancellationToken))
            => Calculate(request, checker, section, axes, token, false);

        /// <summary>With <paramref name="creepPath"/> the integration of the creep is used also at φef = 0 (tests of the bit-identical path).</summary>
        internal static ModelColumnResult Calculate(ModelColumnRequest request, SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section,
            CoordinateSystem axes, CancellationToken token, bool creepPath)
            => Calculate(request, checker, section, axes, token, creepPath, null);

        /// <summary>
        /// With a cache of the curves at imposed curvature (0.28.0.0): requests with the same N, direction, φef, l0²/c and steps share one curve,
        /// computed once (<see cref="ModelColumnEnvelope"/>, <see cref="Domain"/>); the result is the one of the request alone, bit for bit.
        /// </summary>
        internal static ModelColumnResult Calculate(ModelColumnRequest request, SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section,
            CoordinateSystem axes, CancellationToken token, bool creepPath, ModelColumnCurveCache cache)
        {
            if (request == null || checker == null || section == null || axes == null) throw new ArgumentNullException();
            Validate(request);
            token.ThrowIfCancellationRequested();
            var (profile, solver, concrete) = Resolve(request.AxialForce, checker, section);
            if (request.AxialForce > 0)
                throw Reject(ModelColumnRejection.TensionAxialForce, "Model column: tensile axial force; the model column is a compression member.", request.AxialForce);
            return new ModelColumnEngine(request, checker, solver, section, concrete, axes, profile, token, creepPath, cache).Run();
        }

        /// <summary>The checks of the standard, the checker and the section, common to every request on them; the profile, the solver and the concrete.</summary>
        internal static (ModelColumnProfile Profile, SectionSolverModelCode2010 Solver, ConcreteMaterialEuropeanCommon Concrete) Resolve(double axialForce,
            SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section)
        {
            if (!ModelColumnProfiles.TryResolve(checker.StandardModelCode2010, out var profile))
                throw Reject(ModelColumnRejection.UnsupportedStandard, ModelColumnProfiles.NotSupportedReason(checker.StandardModelCode2010), axialForce);
            if (!(checker.SectionSolver is SectionSolverModelCode2010 solver) || !ReferenceEquals(solver.ConcreteSection, section))
                throw Reject(ModelColumnRejection.InvalidRequest, "Model column: the section must be the section of the checker.", axialForce);
            if (checker.SectionCheckerOptions.FailureDomainType != SectionSolver.FailureDomainTypes.Plastic)
                throw Reject(ModelColumnRejection.UnsupportedChecker, "Model column: the checker must use the plastic failure domain.", axialForce);
            if (solver.ConsiderTensileConcrete)
                throw Reject(ModelColumnRejection.UnsupportedChecker, "Model column: the concrete in tension is neglected (NTC 2018 §4.1.2.3.9.2); use a checker without it.", axialForce);
            if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon concrete) || concrete.ConcreteType != ConcreteMaterial.ConcreteTypes.Concrete)
                throw Reject(ModelColumnRejection.UnsupportedSection, "Model column: ordinary concrete of the European standards only.", axialForce);
            if (section.RebarsCount == 0 || section.SteelSections.Count > 0)
                throw Reject(ModelColumnRejection.UnsupportedSection, "Model column: reinforced concrete section with bars and without steel profiles only.", axialForce);
            return (profile, solver, concrete);
        }

        /// <summary>
        /// Reduced domain N–M1Rd,max of the column of the request (0.28.0.0): the capacity of the first order M1Rd,max and the moment M(χu) of the
        /// section at constant N, in the direction θ of the request, for N = j/n NRd,c (j = 0…n, n = <paramref name="intervals"/>, NRd,c the
        /// resistance in centred compression with the law of the creep) and at the N of the request. Same l0, l, c, imperfection, minimum
        /// eccentricity and steps as the request; φef is the one of the request (with (5.19) the φef of its N and M0Ed) at every N. The request is
        /// calculated first and its rejections are thrown (<see cref="ModelColumnException"/>); a point rejected for the transverse moment is
        /// kept without values. Progress: <see cref="ModelColumnStage.Domain"/>, one step per point.
        /// </summary>
        public static ModelColumnDomain Domain(ModelColumnRequest request, SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section,
            CoordinateSystem axes, int intervals = 40, IProgress<ModelColumnProgress> progress = null, CancellationToken token = default(CancellationToken))
        {
            if (request == null || checker == null || section == null || axes == null) throw new ArgumentNullException();
            ValidateIntervals(intervals, request.AxialForce);
            var cache = new ModelColumnCurveCache();
            var own = Calculate(request, checker, section, axes, token, false, cache);
            return Domain(request, own, new[] { request.AxialForce }, checker, section, axes, intervals, cache, progress, token);
        }

        internal static void ValidateIntervals(int intervals, double axialForce)
        {
            if (intervals < MinimumDomainIntervals || intervals > MaximumDomainIntervals)
                throw Reject(ModelColumnRejection.InvalidRequest, "Model column: " + MinimumDomainIntervals + "-" + MaximumDomainIntervals
                    + " intervals of the reduced domain.", axialForce);
        }

        /// <summary>Limits of the number of intervals of the reduced domain.</summary>
        public const int MinimumDomainIntervals = 4, MaximumDomainIntervals = 200;

        /// <summary>
        /// The reduced domain of a calculated column <paramref name="reference"/> (its direction, φef, imperfection and minimum), at the grid of N
        /// and at the extra axial forces (compression), on the cache of the curves.
        /// </summary>
        internal static ModelColumnDomain Domain(ModelColumnRequest template, ModelColumnResult reference, IEnumerable<double> extra,
            SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section, CoordinateSystem axes, int intervals, ModelColumnCurveCache cache,
            IProgress<ModelColumnProgress> progress, CancellationToken token)
        {
            double phi = reference.Creep.EffectiveRatio, resistance = reference.Section.CompressionResistance;
            // φef of the reference at every N: assigned (0 is the same curve as the creep neglected, scale 1 + φef = 1)
            var creep = phi == 0 ? ModelColumnCreep.Neglected(template.Creep.FinalCoefficient)
                : ModelColumnCreep.Effective(phi, template.Creep.FinalCoefficient);
            var forces = Enumerable.Range(0, intervals + 1).Select(j => j == 0 ? 0.0 : j == intervals ? resistance : resistance * j / intervals)
                .Concat(extra.Where(n => n <= 0 && n >= resistance)).Distinct().OrderByDescending(n => n).ToList();
            var (profile, solver, concrete) = Resolve(template.AxialForce, checker, section);
            var points = new List<ModelColumnDomainPoint>();
            for (int i = 0; i < forces.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var request = new ModelColumnRequest(forces[i], reference.DirectionDegrees, template.EffectiveLength, template.MemberLength, null, template.CurvatureFactor,
                    template.Imperfection, template.MinimumEccentricity, creep, template.EndMomentRatio, template.Steps, template.AxialTolerance,
                    template.TransverseMomentLimit, template.RejectTransverseMoment);
                var point = new ModelColumnDomainPoint { AxialForce = forces[i] };
                try
                {
                    // the engine directly: φef is the one of the reference also beyond the limit of an assigned φef (10) of the validation
                    var r = new ModelColumnEngine(request, checker, solver, section, concrete, axes, profile, token, false, cache).Run();
                    point.MaximumFirstOrderMoment = r.Capacity.MaximumFirstOrderMoment; point.UltimateMoment = r.Capacity.UltimateMoment;
                    point.CurvatureAtMaximum = r.Capacity.CurvatureAtMaximum; point.DeflectionAtMaximum = r.Capacity.DeflectionAtMaximum; point.Mode = r.Capacity.Mode;
                }
                catch (ModelColumnException rejection) { point.Rejection = rejection.Reason; }
                points.Add(point);
                progress?.Report(new ModelColumnProgress(ModelColumnStage.Domain, i + 1, forces.Count));
            }
            return new ModelColumnDomain
            {
                DirectionDegrees = reference.DirectionDegrees, EffectiveLength = reference.EffectiveLength, CurvatureFactor = reference.CurvatureFactor,
                EffectiveCreepRatio = phi, CompressionResistance = resistance, ImperfectionEccentricity = reference.Imperfection.Eccentricity,
                MinimumEccentricity = reference.FirstOrder.MinimumEccentricity, Points = points.AsReadOnly()
            };
        }

        internal static ModelColumnException Reject(ModelColumnRejection reason, string message, double axialForce, double value = double.NaN)
            => new ModelColumnException(message, reason, axialForce, value);

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void Validate(ModelColumnRequest p)
        {
            bool valid = Finite(p.AxialForce) && Finite(p.DirectionDegrees) && Finite(p.EffectiveLength) && p.EffectiveLength > 0 && Finite(p.MemberLength)
                && p.MemberLength > 0 && p.CurvatureFactor >= 8 && p.CurvatureFactor <= 10 && p.Steps >= 10 && p.Steps <= 500 && Finite(p.AxialTolerance)
                && p.AxialTolerance > 0 && p.TransverseMomentLimit > 0 && p.TransverseMomentLimit <= 1
                && Enum.IsDefined(typeof(ModelColumnMinimumEccentricity), p.MinimumEccentricity)
                && (!p.FirstOrderMoment.HasValue || (Finite(p.FirstOrderMoment.Value) && p.FirstOrderMoment.Value >= 0))
                && (!p.EndMomentRatio.HasValue || (p.EndMomentRatio.Value >= -1 && p.EndMomentRatio.Value <= 1));
            if (!valid)
                throw Reject(ModelColumnRejection.InvalidRequest, "Model column: finite N and direction, l0 > 0, l > 0, c in [8; 10], 10-500 steps, positive axial tolerance, "
                    + "transverse limit in (0; 1], M0Ed ≥ 0 in the direction of the moment, rm in [-1; 1].", p.AxialForce);
            var i = p.Imperfection;
            bool imperfection = i.Kind == ModelColumnImperfectionKind.Assigned ? i.Eccentricity.HasValue && Finite(i.Eccentricity.Value) && i.Eccentricity.Value >= 0
                : i.Kind != ModelColumnImperfectionKind.En1992Inclination || (i.Members >= 1 && (!i.BaseInclination.HasValue
                    || (Finite(i.BaseInclination.Value) && i.BaseInclination.Value > 0 && i.BaseInclination.Value <= 0.1)));
            if (!imperfection)
                throw Reject(ModelColumnRejection.InvalidRequest, "Model column: assigned eccentricity ≥ 0, θ0 in (0; 0.1] and m ≥ 1.", p.AxialForce);
            var c = p.Creep;
            bool final = !c.FinalCoefficient.HasValue || (Finite(c.FinalCoefficient.Value) && c.FinalCoefficient.Value >= 0 && c.FinalCoefficient.Value <= 10);
            bool creep = final && (c.Kind != ModelColumnCreepKind.Effective || (c.EffectiveRatio.HasValue && Finite(c.EffectiveRatio.Value) && c.EffectiveRatio.Value >= 0
                    && c.EffectiveRatio.Value <= 10))
                && (c.Kind != ModelColumnCreepKind.FinalCoefficient || (c.FinalCoefficient.HasValue && c.QuasiPermanentMoment.HasValue && Finite(c.QuasiPermanentMoment.Value)
                    && c.QuasiPermanentAxialForce.HasValue && Finite(c.QuasiPermanentAxialForce.Value) && c.QuasiPermanentAxialForce.Value <= 0 && p.FirstOrderMoment.HasValue));
            if (!creep)
                throw Reject(ModelColumnRejection.InvalidRequest, "Model column: φef and φ(∞,t0) in [0; 10]; (5.19) needs the first-order moment, M0Eqp and a compressive NEqp.",
                    p.AxialForce);
        }
    }

    /// <summary>
    /// Cache of the curves at imposed curvature of one envelope or reduced domain (0.28.0.0): one template, so a curve depends only on N, the
    /// direction, 1 + φef, k = |N| l0²/c and the steps. Not thread-safe; one per calculation.
    /// </summary>
    internal sealed class ModelColumnCurveCache
    {
        private readonly Dictionary<(double N, double Direction, double Scale, double K, int Steps), ModelColumnEngine.Curve> curves
            = new Dictionary<(double, double, double, double, int), ModelColumnEngine.Curve>();

        /// <summary>The distinct curves calculated.</summary>
        internal int Count => curves.Count;

        internal bool TryGet((double, double, double, double, int) key, out ModelColumnEngine.Curve curve) => curves.TryGetValue(key, out curve);

        internal void Add((double, double, double, double, int) key, ModelColumnEngine.Curve curve) => curves[key] = curve;
    }

    /// <summary>The calculation of one model column (state of one call).</summary>
    internal sealed class ModelColumnEngine
    {
        private struct Forces
        {
            public double N, M, T;
        }

        internal sealed class Sample
        {
            public double Chi, Strain, N, M, T, Concrete, Steel;
            public bool Yielded;
        }

        /// <summary>
        /// The curve at imposed curvature at NEd: the samples (first yield and maximum inserted), χu, the first yield, the transverse ratio and the
        /// maximum of M − k χ (null when the transverse moment rejects the request). Read only once built.
        /// </summary>
        internal sealed class Curve
        {
            public List<Sample> Samples;
            public double UltimateCurvature, TransverseRatio;
            public double? YieldCurvature;
            public Sample YieldSample, Maximum;
        }

        private readonly ModelColumnRequest p;
        private readonly SectionCheckerModelCode2010 checker;
        private readonly SectionSolverModelCode2010 solver;
        private readonly ReinforcedConcreteSection section;
        private readonly ConcreteMaterialEuropeanCommon concrete;
        private readonly StandardModelCode2010 standard;
        private readonly CoordinateSystem axes, forceAxes;
        private readonly ModelColumnProfile profile;
        private readonly CancellationToken token;
        private readonly bool creepPath;
        private readonly ModelColumnCurveCache cache;
        private readonly Point2d origin;
        private readonly ReinforcedConcreteRebar[] rebars;
        private readonly Dictionary<int, bool> inside;
        private readonly List<ModelColumnTraceStep> trace = new List<ModelColumnTraceStep>();
        private readonly List<ModelColumnWarning> warnings = new List<ModelColumnWarning>();
        private double cd, sd, teta, r11, r12, r21, r22;
        private double scale, ecu, ec2, wMax, wMin, wC, nEd, k, nTolerance;
        private double[] barW, barR, barTension, barCompression, barYieldTension, barYieldCompression;
        private double[] vertexW;

        internal ModelColumnEngine(ModelColumnRequest request, SectionCheckerModelCode2010 checker, SectionSolverModelCode2010 solver, ReinforcedConcreteSection section,
            ConcreteMaterialEuropeanCommon concrete, CoordinateSystem axes, ModelColumnProfile profile, CancellationToken token, bool creepPath,
            ModelColumnCurveCache cache = null)
        {
            p = request; this.checker = checker; this.solver = solver; this.section = section; this.concrete = concrete; this.axes = axes; this.profile = profile;
            this.token = token; this.creepPath = creepPath; this.cache = cache; standard = checker.StandardModelCode2010;
            forceAxes = checker.SectionCheckerOptions.ForceReferenceCoordinateSystem;
            origin = solver.IntegrationReferencePoint;
            rebars = section.GetRebars();
            inside = section.GetRebarIsInsideAssociation();
        }

        private void Step(string code, string description, string formula, double? value, string unit, string reference)
            => trace.Add(new ModelColumnTraceStep(code, description, formula, value, unit, reference));

        internal ModelColumnResult Run()
        {
            Directions();
            nEd = p.AxialForce;
            double n = Math.Abs(nEd), l0 = p.EffectiveLength, c = p.CurvatureFactor;
            k = n * l0 * l0 / c;
            bool ntc = ModelColumnProfiles.IsNtc(profile);
            string method = "EN 1992-1-1 5.8.6(6), 5.8.8.2(3)-(4); NTC 2018 §4.1.2.3.9.3";
            Step(ModelColumnTraceCodes.Method, "Metodo della colonna modello: equilibrio nella sezione critica con la curvatura dal legame momento-curvatura della sezione a N costante",
                "e2 = χ·l0²/c; M2 = NEd·e2", null, "", method + "; " + ModelColumnProfiles.Reference(profile));
            Step(ModelColumnTraceCodes.AxialForce, "Sforzo normale di progetto (compressione)", "NEd", n, "N", "");
            Step(ModelColumnTraceCodes.EffectiveLength, "Lunghezza libera di inflessione", "l0", l0, "mm", ntc ? "NTC 2018 [4.1.42]" : "EN 1992-1-1 5.8.3.2");
            Step(ModelColumnTraceCodes.MemberLength, "Lunghezza dell'elemento", "l", p.MemberLength, "mm", "");

            // ---- section and slenderness
            var props = SectionProperties();
            double fcd = Math.Abs(concrete.CalculateFcd(standard)), fck = Math.Abs(concrete.Fck);
            double asFyd = rebars.Sum(b => b.Area * b.RebarMaterial.CalculateDesignYieldingStressTension(standard));
            double asTotal = rebars.Sum(b => b.Area);
            double ws = rebars.Sum(b => b.Area * W(b.Position)) / asTotal;
            double iS = Math.Sqrt(rebars.Sum(b => b.Area * (W(b.Position) - ws) * (W(b.Position) - ws)) / asTotal);
            int tensile = Enumerable.Range(0, rebars.Length).OrderBy(i => barW[i]).First();
            var sectionResult = new ModelColumnSection
            {
                ConcreteArea = props.Area, Depth = wMax - wMin, Inertia = props.Inertia, RadiusOfGyration = Math.Sqrt(props.Inertia / props.Area),
                ReinforcementArea = asTotal, ReinforcementRadiusOfGyration = iS, CharacteristicStrength = fck, DesignStrength = fcd, ReinforcementResistance = asFyd,
                AxialResistance = props.Area * fcd + asFyd, YieldStrain = rebars[tensile].RebarMaterial.CalculateDesignYieldingStrainTension(standard)
            };
            double h = sectionResult.Depth, radius = sectionResult.RadiusOfGyration, lambda = l0 / radius;
            Step(ModelColumnTraceCodes.ConcreteArea, "Area della sezione di calcestruzzo", "Ac", props.Area, "mm²", "");
            Step(ModelColumnTraceCodes.Inertia, "Momento d'inerzia della sezione di calcestruzzo non fessurata nella direzione di flessione", "Ic", props.Inertia, "mm⁴", "");
            Step(ModelColumnTraceCodes.RadiusOfGyration, "Raggio d'inerzia", "i = √(Ic/Ac)", radius, "mm", ntc ? "NTC 2018 [4.1.42]" : "EN 1992-1-1 5.8.3.2(1)");
            Step(ModelColumnTraceCodes.Slenderness, "Snellezza", "λ = l0/i", lambda, "−", ntc ? "NTC 2018 [4.1.42]" : "EN 1992-1-1 (5.14)");
            int slendernessSteps = trace.Count;

            // ---- imperfection and first-order moment
            var imperfection = Imperfection(n, l0);
            var firstOrder = FirstOrder(n, l0, h, imperfection);
            // ---- creep
            var creep = Creep(n, lambda, h, imperfection, firstOrder);
            scale = 1 + creep.EffectiveRatio;
            ecu = concrete.StrainUCompression * scale;
            ec2 = concrete.StrainYPureCompression * scale;
            wC = wMax - (1 - concrete.StrainYPureCompression / concrete.StrainUCompression) * (wMax - wMin);
            nTolerance = 1e-11 * (props.Area * fcd + asFyd);

            // the limit slenderness depends on φef (factor A): its steps go after the slenderness in the trace
            int afterCreep = trace.Count;
            var slenderness = Slenderness(n, props.Area, fcd, asFyd, lambda, creep.EffectiveRatio);
            var limitSteps = trace.GetRange(afterCreep, trace.Count - afterCreep);
            trace.RemoveRange(afterCreep, limitSteps.Count);
            trace.InsertRange(slendernessSteps, limitSteps);

            Step(ModelColumnTraceCodes.CurvatureFactor, "Fattore della distribuzione di curvatura", "c (10 ≈ π²; 8 con momento del primo ordine costante)", c, "−",
                "EN 1992-1-1 5.8.8.2(4)");
            Step(ModelColumnTraceCodes.SecondOrderStiffness, "Coefficiente del momento del secondo ordine", "k = NEd·l0²/c (M2 = k·χ)", k, "N·mm²", "EN 1992-1-1 (5.33)");

            // ---- the curve at imposed curvature
            double compressionResistance = Evaluate(Lower(0), 0).N;
            sectionResult.CompressionResistance = compressionResistance;
            if (nEd < compressionResistance)
                throw ModelColumnAnalysis.Reject(ModelColumnRejection.AxialForceBeyondResistance, "Model column: |NEd| = " + n.ToString("G6", CultureInfo.InvariantCulture)
                    + " N beyond the resistance in centred compression " + Math.Abs(compressionResistance).ToString("G6", CultureInfo.InvariantCulture) + " N.",
                    nEd, compressionResistance);
            var key = (nEd, p.DirectionDegrees, scale, k, p.Steps);
            Curve curve = null;
            if (cache == null || !cache.TryGet(key, out curve))
            {
                curve = BuildCurve();
                cache?.Add(key, curve);
            }
            var samples = curve.Samples;
            double chiU = curve.UltimateCurvature, transverseRatio = curve.TransverseRatio;
            double? yieldCurvature = curve.YieldCurvature; Sample yieldSample = curve.YieldSample;
            if (transverseRatio > p.TransverseMomentLimit)
            {
                if (p.RejectTransverseMoment)
                    throw ModelColumnAnalysis.Reject(ModelColumnRejection.TransverseMomentBeyondLimit, "Model column: transverse moment " + (100 * transverseRatio).ToString("0.##", CultureInfo.InvariantCulture)
                        + " % of the moment in the direction of bending, beyond " + (100 * p.TransverseMomentLimit).ToString("0.##", CultureInfo.InvariantCulture)
                        + " %: the section is not symmetric about the plane of bending.", nEd, transverseRatio);
                warnings.Add(ModelColumnWarning.TransverseMoment);
            }
            Sample maximum = curve.Maximum;
            double chiStar = maximum.Chi, m1Rd = G(maximum);
            var mode = chiU - chiStar <= 1e-9 * chiU ? ModelColumnMode.SectionFailure : ModelColumnMode.Instability;
            if (n == 0) warnings.Add(ModelColumnWarning.NoAxialForce);

            var points = samples.Select(s => Point(s, s.Chi == chiU, ReferenceEquals(s, maximum))).ToList();
            var capacity = new ModelColumnCapacity
            {
                MaximumFirstOrderMoment = m1Rd, CurvatureAtMaximum = chiStar, MomentAtMaximum = maximum.M, DeflectionAtMaximum = l0 * l0 / c * chiStar,
                SecondOrderMomentAtMaximum = k * chiStar, Mode = mode, UltimateCurvature = chiU, UltimateMoment = samples.Last().M, YieldCurvature = yieldCurvature,
                YieldMoment = yieldSample?.M,
                MaximumAppliedMoment = m1Rd >= (firstOrder.MinimumMoment ?? 0) && m1Rd - imperfection.Moment >= 0 ? m1Rd - imperfection.Moment : (double?)null
            };
            Step(ModelColumnTraceCodes.ConcreteLaw, "Legami di progetto della sezione: parabola-rettangolo e acciaio delle barre, sezione netta, calcestruzzo teso trascurato",
                creep.EffectiveRatio > 0 ? "εc2' = εc2·(1 + φef); εcu' = εcu·(1 + φef)" : "εc2; εcu", null, "", "NTC 2018 §4.1.2.3.9.2; EN 1992-1-1 5.8.6(3)-(4)");
            Step(ModelColumnTraceCodes.UltimateCurvature, "Curvatura ultima della sezione a NEd", "χu", chiU, "1/mm", "EN 1992-1-1 6.1(5)");
            Step(ModelColumnTraceCodes.UltimateMoment, "Momento della sezione alla curvatura ultima", "M(χu)", capacity.UltimateMoment, "Nmm", "");
            if (yieldCurvature.HasValue)
                Step(ModelColumnTraceCodes.YieldCurvature, "Curvatura del primo snervamento delle barre", "χy", yieldCurvature.Value, "1/mm", "");
            Step(ModelColumnTraceCodes.CurvatureAtMaximum, "Curvatura del massimo", "χ*", chiStar, "1/mm", "");
            Step(ModelColumnTraceCodes.DeflectionAtMaximum, "Spostamento del secondo ordine al massimo", "e2* = χ*·l0²/c", capacity.DeflectionAtMaximum, "mm",
                "EN 1992-1-1 5.8.8.2(3)");
            Step(ModelColumnTraceCodes.SecondOrderMomentAtMaximum, "Momento del secondo ordine al massimo", "M2* = NEd·e2*", capacity.SecondOrderMomentAtMaximum, "Nmm",
                "EN 1992-1-1 (5.33)");
            Step(ModelColumnTraceCodes.MaximumFirstOrderMoment, "Momento massimo del primo ordine sopportabile", "M1Rd,max = max[M(χ) − NEd·l0²/c·χ]", m1Rd, "Nmm", method);
            Step(ModelColumnTraceCodes.Mode, "Modo di crisi", mode == ModelColumnMode.Instability ? "instabilità (massimo prima di χu)" : "rottura della sezione (massimo a χu)",
                null, "", "");

            // ---- equilibrium
            ModelColumnEquilibrium equilibrium = null;
            if (firstOrder.TotalMoment.HasValue)
                equilibrium = Equilibrium(samples, maximum, firstOrder.TotalMoment.Value, m1Rd, l0, c);

            // ---- nominal curvature (informative)
            var nominal = Nominal(n, props.Area, fcd, fck, asFyd, lambda, creep.EffectiveRatio, h, iS, sectionResult.YieldStrain, l0, c, firstOrder.TotalMoment);

            var result = new ModelColumnResult
            {
                Profile = profile, AxialForce = nEd, DirectionDegrees = p.DirectionDegrees, EffectiveLength = l0, MemberLength = p.MemberLength, CurvatureFactor = c,
                SecondOrderStiffness = k, Section = sectionResult, Slenderness = slenderness, Imperfection = imperfection, FirstOrder = firstOrder, Creep = creep,
                Points = points.AsReadOnly(), Capacity = capacity, Equilibrium = equilibrium, NominalCurvature = nominal, TransverseMomentRatio = transverseRatio,
                Verified = equilibrium?.Verified,
            };
            result.Status = "Model column (EN 1992-1-1 5.8.6(6), 5.8.8.2; " + profile + "): M1Rd,max = " + m1Rd.ToString("G7", CultureInfo.InvariantCulture)
                + " Nmm at chi* = " + chiStar.ToString("G6", CultureInfo.InvariantCulture) + " 1/mm (" + mode + "), chi_u = " + chiU.ToString("G6", CultureInfo.InvariantCulture)
                + " 1/mm, phi_ef = " + creep.EffectiveRatio.ToString("G4", CultureInfo.InvariantCulture) + ", e_i = " + imperfection.Eccentricity.ToString("G6", CultureInfo.InvariantCulture)
                + " mm" + (equilibrium == null ? "; capacity only." : equilibrium.Found
                    ? "; M0Ed,tot = " + equilibrium.FirstOrderMoment.ToString("G7", CultureInfo.InvariantCulture) + " Nmm, MEd = "
                        + equilibrium.DesignMoment.Value.ToString("G7", CultureInfo.InvariantCulture) + " Nmm, ratio " + equilibrium.Ratio.ToString("G4", CultureInfo.InvariantCulture) + "."
                    : "; M0Ed,tot = " + equilibrium.FirstOrderMoment.ToString("G7", CultureInfo.InvariantCulture) + " Nmm beyond M1Rd,max: no equilibrium (instability).");
            result.Trace = trace.AsReadOnly();
            result.Warnings = warnings.AsReadOnly();
            return result;
        }

        /// <summary>
        /// The curve at NEd: χu, uniform samples on [0; χu] with the first yield inserted, the transverse ratio and, unless it rejects the request,
        /// the maximum of M − k χ (best sample, golden section between its neighbours, inserted in the samples).
        /// </summary>
        private Curve BuildCurve()
        {
            double chiU = UltimateCurvature();
            var samples = new List<Sample>();
            for (int i = 0; i <= p.Steps; i++)
            {
                token.ThrowIfCancellationRequested();
                samples.Add(Required(i == p.Steps ? chiU : chiU * i / p.Steps));
            }
            double? yieldCurvature = null; Sample yieldSample = null;
            int firstYield = samples.FindIndex(s => s.Yielded);
            if (firstYield == 0) { yieldCurvature = 0; yieldSample = samples[0]; }
            else if (firstYield > 0)
            {
                double low = samples[firstYield - 1].Chi; Sample high = samples[firstYield];
                for (int i = 0; i < 50 && high.Chi - low > 1e-15 * chiU; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var candidate = Required(0.5 * (low + high.Chi));
                    if (candidate.Yielded) high = candidate; else low = candidate.Chi;
                }
                if (high.Chi < samples[firstYield].Chi) samples.Insert(firstYield, high);
                yieldCurvature = high.Chi; yieldSample = high;
            }
            double maxMoment = samples.Max(s => Math.Abs(s.M)), maxTransverse = samples.Max(s => Math.Abs(s.T));
            // moments of the round-off only (|NEd| = NRd,c of a symmetric section, χu = 0: 1e-11 (Ac fcd + As fyd) h is far above them): no ratio
            var curve = new Curve
            {
                Samples = samples, UltimateCurvature = chiU, TransverseRatio = maxMoment > nTolerance * (wMax - wMin) ? maxTransverse / maxMoment : 0, YieldCurvature = yieldCurvature,
                YieldSample = yieldSample
            };
            if (curve.TransverseRatio > p.TransverseMomentLimit && p.RejectTransverseMoment) return curve;

            // ---- maximum of M - k chi
            int best = 0;
            for (int i = 1; i < samples.Count; i++) if (G(samples[i]) > G(samples[best])) best = i;
            Sample maximum = samples[best];
            if (samples.Count > 1)
            {
                double a = samples[Math.Max(0, best - 1)].Chi, b = samples[Math.Min(samples.Count - 1, best + 1)].Chi;
                var refined = Golden(a, b);
                if (G(refined) > G(maximum)) maximum = refined;
            }
            double chiStar = maximum.Chi;
            // the maximum refined between two samples is a point of the curve too (tangent of the graphical reading)
            if (!samples.Contains(maximum))
            {
                var same = samples.FirstOrDefault(s => s.Chi == chiStar);
                if (same != null) maximum = same;
                else samples.Insert(samples.FindIndex(s => s.Chi > chiStar), maximum);
            }
            curve.Maximum = maximum;
            return curve;
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>
        /// Direction θ of the moment in the axes of the forces, the same direction in the axes of the solver (angle teta of its strain planes: for
        /// teta the planes ε = ε0 − χ w, w = (y − y0) cos teta − (x − x0) sin teta, give a moment along (cos teta; sin teta) in a section with the
        /// principal axes of the direction), the rotation of the moments from the solver axes to the axes of the forces and the distances w.
        /// </summary>
        private void Directions()
        {
            double angle = p.DirectionDegrees * Math.PI / 180;
            cd = Math.Cos(angle); sd = Math.Sin(angle);
            var solverAxes = solver.SolverAxes;
            var unitN = new ResultBeamForces(1, 0, 0, 0, 0, 0, solverAxes).ToCoordinateSystem(axes);
            if (Math.Abs(unitN.N - 1) > 1e-9)
                throw ModelColumnAnalysis.Reject(ModelColumnRejection.InvalidRequest, "Model column: the axes must keep the axis of the member of the solver.", p.AxialForce);
            var m1 = new ResultBeamForces(0, 0, 0, 0, 1, 0, solverAxes).ToCoordinateSystem(axes);
            var m2 = new ResultBeamForces(0, 0, 0, 0, 0, 1, solverAxes).ToCoordinateSystem(axes);
            r11 = m1.M1; r21 = m1.M2; r12 = m2.M1; r22 = m2.M2;
            // the direction in the solver axes: the transposed rotation (orthonormal axes)
            teta = Math.Atan2(r12 * cd + r22 * sd, r11 * cd + r21 * sd);
            var outline = section.ConcreteShape.Fill2d.Points;
            vertexW = outline.Select(v => W(v)).ToArray();
            wMax = vertexW.Max(); wMin = vertexW.Min();
            barW = rebars.Select(b => W(b.Position)).ToArray();
            barR = rebars.Select(b => 0.5 * b.RebarSection.Diameter).ToArray();
            barTension = rebars.Select(b => b.RebarMaterial.CalculateDesignUltimateStrainTension(standard) - b.EpsilonP).ToArray();
            barCompression = rebars.Select(b => b.RebarMaterial.CalculateDesignUltimateStrainCompression(standard) - b.EpsilonP).ToArray();
            barYieldTension = rebars.Select(b => b.RebarMaterial.CalculateDesignYieldingStrainTension(standard)).ToArray();
            barYieldCompression = rebars.Select(b => b.RebarMaterial.CalculateDesignYieldingStrainCompression(standard)).ToArray();
        }

        private double W(Point2d point) => (point.Y - origin.Y) * Math.Cos(teta) - (point.X - origin.X) * Math.Sin(teta);

        /// <summary>Area and second moment about the centroid along the direction of the curvature of the gross concrete (outline minus holes).</summary>
        private (double Area, double Inertia) SectionProperties()
        {
            double a = 0, sx = 0, sy = 0, ixx = 0, iyy = 0, ixy = 0;
            void Add(Point2d[] points, double sign)
            {
                double pa = 0, psx = 0, psy = 0, pixx = 0, piyy = 0, pixy = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    var u = points[i]; var v = points[(i + 1) % points.Length];
                    double x0 = u.X - origin.X, y0 = u.Y - origin.Y, x1 = v.X - origin.X, y1 = v.Y - origin.Y, cross = x0 * y1 - x1 * y0;
                    pa += cross / 2; psx += (y0 + y1) * cross / 6; psy += (x0 + x1) * cross / 6;
                    pixx += (y0 * y0 + y0 * y1 + y1 * y1) * cross / 12; piyy += (x0 * x0 + x0 * x1 + x1 * x1) * cross / 12;
                    pixy += (x0 * y1 + 2 * x0 * y0 + 2 * x1 * y1 + x1 * y0) * cross / 24;
                }
                double o = sign * Math.Sign(pa);
                a += o * pa; sx += o * psx; sy += o * psy; ixx += o * pixx; iyy += o * piyy; ixy += o * pixy;
            }
            Add(section.ConcreteShape.Fill2d.Points, 1);
            foreach (var hole in section.ConcreteShape.Holes2d ?? new Polygon2d[0]) Add(hole.Points, -1);
            double cx = sy / a, cy = sx / a;
            double jxx = ixx - a * cy * cy, jyy = iyy - a * cx * cx, jxy = ixy - a * cx * cy;
            // w = u · (p − p0), u = (−sin teta; cos teta)
            double ux = -Math.Sin(teta), uy = Math.Cos(teta);
            return (a, ux * ux * jyy + 2 * ux * uy * jxy + uy * uy * jxx);
        }

        // ---------------------------------------------------------------- section at imposed curvature

        /// <summary>Lower bound of ε0 at the curvature: εcu' at the compressed edge, εc2' at the pivot C, ultimate compressive strain at the edges of the bars.</summary>
        private double Lower(double chi)
        {
            double lower = Math.Max(ecu + chi * wMax, ec2 + chi * wC);
            for (int i = 0; i < barW.Length; i++) lower = Math.Max(lower, barCompression[i] + chi * (barW[i] + barR[i]));
            return lower;
        }

        /// <summary>Upper bound of ε0 at the curvature: εud at the far edges of the bars.</summary>
        private double Upper(double chi)
        {
            double upper = double.PositiveInfinity;
            for (int i = 0; i < barW.Length; i++) upper = Math.Min(upper, barTension[i] + chi * (barW[i] - barR[i]));
            return upper;
        }

        /// <summary>
        /// Resultant of a plane in the solver axes about the reference point of the integration. Without creep: the integration of the failure
        /// domain (<see cref="SectionSolver.CalculateForceResultantForDomain(StrainPlane, Dictionary{int, bool})"/>). With creep the concrete is
        /// integrated on the plane divided by (1 + φef), the law with stretched strains, and the bars on the plane, each minus the concrete it displaces
        /// at its stretched strain, in the order and with the expressions of the integration of the domain (the same values at φef = 0).
        /// </summary>
        internal ForceTuple Integrate(double e0, double chi)
        {
            var plane = new StrainPlane(origin, teta, chi, e0);
            if (!creepPath && scale == 1) return solver.CalculateForceResultantForDomain(plane, inside);
            var concreteForces = solver.IntegrateSectionStress(new StrainPlane(origin, teta, chi / scale, e0 / scale));
            double n = 0, mx = 0, my = 0;
            for (int i = 0; i < rebars.Length; i++)
            {
                double strain = plane.GetStrain(rebars[i].Position);
                double sigmaS = solver.CalculateStressRebar(rebars[i], strain);
                double sigmaC = 0;
                if (inside[i])
                    sigmaC = solver.CalculateSigmaC(strain / scale);
                n += (sigmaS - sigmaC) * rebars[i].Area;
                mx += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - origin.Y);
                my += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - origin.X);
            }
            return concreteForces + new ForceTuple(n, -mx, my);
        }

        /// <summary>N, moment in the direction θ and transverse moment in the axes of the forces.</summary>
        private Forces Evaluate(double e0, double chi)
        {
            var f = solver.GetExternalForces(Integrate(e0, chi), forceAxes);
            double m1 = r11 * f.Mx + r12 * f.My, m2 = r21 * f.Mx + r22 * f.My;
            return new Forces { N = f.N, M = m1 * cd + m2 * sd, T = -m1 * sd + m2 * cd };
        }

        /// <summary>The plane at the curvature with N = NEd, by regula falsi (Illinois) on ε0 between the bounds; null when no admissible plane carries NEd.</summary>
        private Sample Solve(double chi)
        {
            double a = Lower(chi), b = Upper(chi);
            if (!(a <= b)) return null;
            var fa = Evaluate(a, chi); double ga = fa.N - nEd;
            if (ga > 0) return null;
            var fb = Evaluate(b, chi); double gb = fb.N - nEd;
            if (gb < 0) return null;
            double x; Forces fx;
            if (ga == 0) { x = a; fx = fa; }
            else if (gb == 0) { x = b; fx = fb; }
            else
            {
                x = a; fx = fa; double gx = ga, bestG = double.PositiveInfinity, bestX = a; Forces bestF = fa;
                int side = 0;
                for (int iteration = 0; iteration < 200; iteration++)
                {
                    x = (a * gb - b * ga) / (gb - ga);
                    if (!(x > a && x < b)) x = 0.5 * (a + b);
                    fx = Evaluate(x, chi); gx = fx.N - nEd;
                    if (Math.Abs(gx) < bestG) { bestG = Math.Abs(gx); bestX = x; bestF = fx; }
                    if (Math.Abs(gx) <= nTolerance) break;
                    if (gx < 0) { a = x; ga = gx; if (side == -1) gb *= 0.5; side = -1; }
                    else { b = x; gb = gx; if (side == 1) ga *= 0.5; side = 1; }
                    if (b - a <= 1e-15 * Math.Max(1e-3, Math.Max(Math.Abs(a), Math.Abs(b)))) break;
                }
                x = bestX; fx = bestF;
            }
            var sample = new Sample { Chi = chi, Strain = x, N = fx.N, M = fx.M, T = fx.T, Concrete = Math.Max(0, chi * wMax - x), Steel = double.NegativeInfinity };
            for (int i = 0; i < rebars.Length; i++)
            {
                double strain = x - chi * barW[i];
                sample.Steel = Math.Max(sample.Steel, strain);
                double total = strain + rebars[i].EpsilonP;
                if (total >= barYieldTension[i] || total <= barYieldCompression[i]) sample.Yielded = true;
            }
            return sample;
        }

        private Sample Required(double chi)
            => Solve(chi) ?? throw new InvalidOperationException("Model column: no equilibrium at chi = " + chi.ToString("R", CultureInfo.InvariantCulture)
                + " 1/mm below the ultimate curvature.");

        /// <summary>The largest curvature with an admissible plane at NEd: doubling from (|εcu'| + εud)/h/16, then bisection.</summary>
        private double UltimateCurvature()
        {
            double a = 0, b = (-ecu + barTension.Max()) / (wMax - wMin) / 16;
            int doublings = 0;
            while (Solve(b) != null)
            {
                token.ThrowIfCancellationRequested();
                a = b; b *= 2;
                if (++doublings > 60) throw new InvalidOperationException("Model column: ultimate curvature not bounded.");
            }
            for (int i = 0; i < 64 && b - a > 1e-15 * b; i++)
            {
                token.ThrowIfCancellationRequested();
                double m = 0.5 * (a + b);
                if (Solve(m) != null) a = m; else b = m;
            }
            return a;
        }

        private double G(Sample s) => s.M - k * s.Chi;

        /// <summary>Golden-section search of the maximum of M − k χ on [a; b]; the best evaluated sample.</summary>
        private Sample Golden(double a, double b)
        {
            double ratio = (Math.Sqrt(5) - 1) / 2;
            double x1 = b - ratio * (b - a), x2 = a + ratio * (b - a);
            Sample s1 = Required(x1), s2 = Required(x2), bestSample = G(s1) >= G(s2) ? s1 : s2;
            for (int i = 0; i < 80 && b - a > 1e-15 * Math.Max(b, 1e-30); i++)
            {
                token.ThrowIfCancellationRequested();
                if (G(s1) >= G(s2)) { b = x2; x2 = x1; s2 = s1; x1 = b - ratio * (b - a); s1 = Required(x1); }
                else { a = x1; x1 = x2; s1 = s2; x2 = a + ratio * (b - a); s2 = Required(x2); }
                if (G(s1) > G(bestSample)) bestSample = s1;
                if (G(s2) > G(bestSample)) bestSample = s2;
            }
            return bestSample;
        }

        private ModelColumnPoint Point(Sample s, bool ultimate, bool maximum) => new ModelColumnPoint
        {
            Curvature = s.Chi, Moment = s.M, TransverseMoment = s.T, SecondOrderMoment = k * s.Chi, FirstOrderCapacity = G(s), ReferenceStrain = s.Strain,
            ConcreteCompressionStrain = s.Concrete, SteelStrain = s.Steel, Yielded = s.Yielded, Ultimate = ultimate, Maximum = maximum
        };

        // ---------------------------------------------------------------- equilibrium

        private ModelColumnEquilibrium Equilibrium(List<Sample> samples, Sample maximum, double m0, double m1Rd, double l0, double c)
        {
            var result = new ModelColumnEquilibrium { FirstOrderMoment = m0, Ratio = m1Rd > 0 ? m0 / m1Rd : double.PositiveInfinity };
            if (!(m1Rd > 0) || m0 > m1Rd)
            {
                Step(ModelColumnTraceCodes.Ratio, "Rapporto fra il momento del primo ordine e il massimo sopportabile", "M0Ed,tot / M1Rd,max", result.Ratio, "−", "");
                Step(ModelColumnTraceCodes.Outcome, "Esito", "M0Ed,tot > M1Rd,max: nessuna configurazione di equilibrio (instabilità), non verificato", null, "", "");
                return result;
            }
            var branch = samples.Where(s => s.Chi < maximum.Chi).Concat(new[] { maximum }).ToList();
            int j = branch.FindIndex(s => G(s) >= m0);
            double chi;
            if (j <= 0)
            {
                chi = branch[Math.Max(j, 0)].Chi;
                if (j == 0 && G(branch[0]) > m0) warnings.Add(ModelColumnWarning.EquilibriumAtZeroCurvature);
            }
            else
            {
                double low = branch[j - 1].Chi, high = branch[j].Chi;
                for (int i = 0; i < 100 && high - low > 1e-15 * high; i++)
                {
                    token.ThrowIfCancellationRequested();
                    double mid = 0.5 * (low + high);
                    if (G(Required(mid)) >= m0) high = mid; else low = mid;
                }
                chi = high;
            }
            result.Found = true; result.Verified = true; result.Curvature = chi; result.Deflection = l0 * l0 / c * chi; result.SecondOrderMoment = k * chi;
            result.DesignMoment = m0 + k * chi;
            result.SectionMoment = Required(chi).M;
            Step(ModelColumnTraceCodes.EquilibriumCurvature, "Curvatura di equilibrio (prima intersezione stabile)", "M(χeq) = M0Ed,tot + NEd·l0²/c·χeq", chi, "1/mm",
                "EN 1992-1-1 5.8.6(6)");
            Step(ModelColumnTraceCodes.EquilibriumDeflection, "Spostamento del secondo ordine", "e2 = χeq·l0²/c", result.Deflection.Value, "mm", "EN 1992-1-1 5.8.8.2(3)");
            Step(ModelColumnTraceCodes.EquilibriumSecondOrderMoment, "Momento del secondo ordine", "M2 = NEd·e2", result.SecondOrderMoment.Value, "Nmm", "EN 1992-1-1 (5.33)");
            Step(ModelColumnTraceCodes.DesignMoment, "Momento di progetto nella sezione critica", "MEd = M0Ed,tot + M2", result.DesignMoment.Value, "Nmm", "EN 1992-1-1 (5.31)");
            Step(ModelColumnTraceCodes.Ratio, "Rapporto fra il momento del primo ordine e il massimo sopportabile", "M0Ed,tot / M1Rd,max", result.Ratio, "−", "");
            Step(ModelColumnTraceCodes.Outcome, "Esito", "M0Ed,tot ≤ M1Rd,max: verificato", null, "", "");
            return result;
        }

        // ---------------------------------------------------------------- slenderness, imperfection, first order, creep

        private ModelColumnSlenderness Slenderness(double n, double area, double fcd, double asFyd, double lambda, double phi)
        {
            bool ntc = ModelColumnProfiles.IsNtc(profile);
            double nu = n / (area * fcd), omega = asFyd / (area * fcd);
            double a = 1 / (1 + 0.2 * phi), b = Math.Sqrt(1 + 2 * omega), c = p.EndMomentRatio.HasValue ? 1.7 - p.EndMomentRatio.Value : 0.7;
            double ntcLimit = nu > 0 ? 25 / Math.Sqrt(nu) : double.PositiveInfinity, enLimit = nu > 0 ? 20 * a * b * c / Math.Sqrt(nu) : double.PositiveInfinity;
            var result = new ModelColumnSlenderness
            {
                Slenderness = lambda, RelativeAxialForce = nu, MechanicalRatio = omega, LimitNtc2018 = ntcLimit, FactorA = a, FactorB = b, FactorC = c, LimitEn1992 = enLimit,
                Limit = ntc ? ntcLimit : enLimit, Reference = ntc ? "NTC 2018 §4.1.2.3.9.2 [4.1.41]" : "EN 1992-1-1 5.8.3.1 (5.13N)"
            };
            result.SecondOrderEffectsNegligible = lambda <= result.Limit;
            Step(ModelColumnTraceCodes.RelativeAxialForce, "Sforzo normale adimensionale", ntc ? "ν = NEd/(Ac·fcd)" : "n = NEd/(Ac·fcd)", nu, "−", result.Reference);
            if (ntc)
                Step(ModelColumnTraceCodes.LimitSlenderness, "Snellezza limite (informativa: il calcolo si esegue comunque)", "λlim = 25/√ν", ntcLimit, "−", result.Reference);
            else
            {
                Step(ModelColumnTraceCodes.MechanicalRatio, "Rapporto meccanico d'armatura", "ω = As·fyd/(Ac·fcd)", omega, "−", result.Reference);
                Step(ModelColumnTraceCodes.FactorA, "Coefficiente della viscosità", "A = 1/(1 + 0,2·φef)", a, "−", result.Reference);
                Step(ModelColumnTraceCodes.FactorB, "Coefficiente dell'armatura", "B = √(1 + 2·ω)", b, "−", result.Reference);
                Step(ModelColumnTraceCodes.FactorC, "Coefficiente dei momenti d'estremità", p.EndMomentRatio.HasValue ? "C = 1,7 − rm" : "C = 0,7 (rm non noto)", c, "−", result.Reference);
                Step(ModelColumnTraceCodes.LimitSlenderness, "Snellezza limite (informativa: il calcolo si esegue comunque)", "λlim = 20·A·B·C/√n", enLimit, "−", result.Reference);
            }
            return result;
        }

        private ModelColumnImperfectionResult Imperfection(double n, double l0)
        {
            var i = p.Imperfection;
            var result = new ModelColumnImperfectionResult { Kind = i.Kind };
            switch (i.Kind)
            {
                case ModelColumnImperfectionKind.Assigned:
                    result.Eccentricity = i.Eccentricity.Value; result.Reference = "assegnata";
                    Step(ModelColumnTraceCodes.ImperfectionEccentricity, "Eccentricità dell'imperfezione assegnata", "ei", result.Eccentricity, "mm", "");
                    break;
                case ModelColumnImperfectionKind.En1992Inclination:
                    {
                        double theta0 = i.BaseInclination ?? ModelColumnProfiles.BaseInclination(profile);
                        double alphaH = Math.Min(1, Math.Max(2.0 / 3, 2 / Math.Sqrt(p.MemberLength / 1000))), alphaM = Math.Sqrt(0.5 * (1 + 1.0 / i.Members));
                        result.BaseInclination = theta0; result.HeightFactor = alphaH; result.MemberFactor = alphaM; result.Inclination = theta0 * alphaH * alphaM;
                        result.Eccentricity = result.Inclination.Value * l0 / 2;
                        result.Reference = "EN 1992-1-1 5.2(5) (5.1), 5.2(7)a (5.2)" + (ModelColumnProfiles.IsNtc(profile) ? ", richiamata da NTC 2018 §4.1.2.3.9.3" : "");
                        Step(ModelColumnTraceCodes.BaseInclination, "Inclinazione di base", "θ0", theta0, "−", "EN 1992-1-1 5.2(5)");
                        Step(ModelColumnTraceCodes.HeightFactor, "Coefficiente di riduzione per la lunghezza", "αh = 2/√l (l in m), 2/3 ≤ αh ≤ 1", alphaH, "−", "EN 1992-1-1 5.2(5)");
                        Step(ModelColumnTraceCodes.MemberFactor, "Coefficiente per il numero di elementi", "αm = √(0,5·(1 + 1/m)), m = " + i.Members.ToString(CultureInfo.InvariantCulture),
                            alphaM, "−", "EN 1992-1-1 5.2(5)-(6)");
                        Step(ModelColumnTraceCodes.Inclination, "Inclinazione", "θi = θ0·αh·αm", result.Inclination.Value, "−", "EN 1992-1-1 (5.1)");
                        Step(ModelColumnTraceCodes.ImperfectionEccentricity, "Eccentricità dell'imperfezione", "ei = θi·l0/2", result.Eccentricity, "mm", result.Reference);
                    }
                    break;
                case ModelColumnImperfectionKind.En1992Simplified:
                    result.Eccentricity = l0 / 400; result.Reference = "EN 1992-1-1 5.2(7)";
                    Step(ModelColumnTraceCodes.ImperfectionEccentricity, "Eccentricità dell'imperfezione (pilastri isolati di sistemi controventati)", "ei = l0/400",
                        result.Eccentricity, "mm", result.Reference);
                    break;
                case ModelColumnImperfectionKind.Ntc2018Straightness:
                    result.Eccentricity = p.MemberLength / 300; result.Reference = "NTC 2018 §4.1.2.3.9.3";
                    Step(ModelColumnTraceCodes.ImperfectionEccentricity, "Difetto di rettilineità (pilastri di telai a nodi fissi)", "ei = l/300", result.Eccentricity, "mm",
                        result.Reference);
                    break;
                default:
                    result.Eccentricity = 0; result.Reference = "nessuna";
                    Step(ModelColumnTraceCodes.ImperfectionEccentricity, "Eccentricità dell'imperfezione (nessuna)", "ei", 0, "mm", "");
                    break;
            }
            result.Moment = n * result.Eccentricity;
            Step(ModelColumnTraceCodes.ImperfectionMoment, "Momento dell'imperfezione", "NEd·ei", result.Moment, "Nmm", "");
            return result;
        }

        private ModelColumnFirstOrder FirstOrder(double n, double l0, double h, ModelColumnImperfectionResult imperfection)
        {
            var rule = p.MinimumEccentricity;
            if (rule == ModelColumnMinimumEccentricity.Standard)
                rule = ModelColumnProfiles.IsNtc(profile) ? ModelColumnMinimumEccentricity.Ntc2018 : ModelColumnMinimumEccentricity.En1992;
            var result = new ModelColumnFirstOrder { AppliedMoment = p.FirstOrderMoment, ImperfectionMoment = imperfection.Moment, MinimumEccentricityRule = rule };
            if (rule == ModelColumnMinimumEccentricity.Ntc2018)
            {
                result.MinimumEccentricity = Math.Max(l0 / 200, 20); result.MinimumReference = "NTC 2018 §4.1.2.3.4.2";
                Step(ModelColumnTraceCodes.MinimumEccentricity, "Eccentricità minima (minimo del momento totale, non sommata all'imperfezione)", "e0 = max(l0/200; 20 mm)",
                    result.MinimumEccentricity.Value, "mm", result.MinimumReference);
            }
            else if (rule == ModelColumnMinimumEccentricity.En1992)
            {
                result.MinimumEccentricity = Math.Max(h / 30, 20); result.MinimumReference = "EN 1992-1-1 6.1(4)";
                Step(ModelColumnTraceCodes.MinimumEccentricity, "Eccentricità minima (minimo del momento totale, non sommata all'imperfezione)", "e0 = max(h/30; 20 mm)",
                    result.MinimumEccentricity.Value, "mm", result.MinimumReference);
            }
            if (result.MinimumEccentricity.HasValue)
            {
                result.MinimumMoment = n * result.MinimumEccentricity.Value;
                Step(ModelColumnTraceCodes.MinimumMoment, "Momento minimo", "NEd·e0", result.MinimumMoment.Value, "Nmm", result.MinimumReference);
            }
            if (p.FirstOrderMoment.HasValue)
            {
                double withImperfection = p.FirstOrderMoment.Value + imperfection.Moment;
                result.TotalMoment = Math.Max(withImperfection, result.MinimumMoment ?? 0);
                result.MinimumGoverns = result.MinimumMoment.HasValue && result.MinimumMoment.Value > withImperfection;
                Step(ModelColumnTraceCodes.AppliedMoment, "Momento del primo ordine nella sezione critica, senza imperfezione", "M0Ed", p.FirstOrderMoment.Value, "Nmm", "");
                Step(ModelColumnTraceCodes.TotalFirstOrderMoment, "Momento totale del primo ordine", result.MinimumMoment.HasValue ? "M0Ed,tot = max(M0Ed + NEd·ei; NEd·e0)"
                    : "M0Ed,tot = M0Ed + NEd·ei", result.TotalMoment.Value, "Nmm", "EN 1992-1-1 5.8.8.2(1)");
            }
            return result;
        }

        private ModelColumnCreepResult Creep(double n, double lambda, double h, ModelColumnImperfectionResult imperfection, ModelColumnFirstOrder firstOrder)
        {
            var creep = p.Creep;
            var result = new ModelColumnCreepResult { Kind = creep.Kind, FinalCoefficient = creep.FinalCoefficient };
            string reference = "EN 1992-1-1 5.8.4";
            if (creep.Kind == ModelColumnCreepKind.Effective) result.EffectiveRatio = creep.EffectiveRatio.Value;
            else if (creep.Kind == ModelColumnCreepKind.FinalCoefficient)
            {
                double design = p.FirstOrderMoment.Value + imperfection.Moment;
                double quasiPermanent = creep.QuasiPermanentMoment.Value + Math.Abs(creep.QuasiPermanentAxialForce.Value) * imperfection.Eccentricity;
                if (!(design > 0) || quasiPermanent < 0)
                    throw ModelColumnAnalysis.Reject(ModelColumnRejection.InvalidRequest, "Model column: (5.19) needs M0Ed + NEd ei > 0 and M0Eqp + NEqp ei ≥ 0.", p.AxialForce);
                result.MomentRatio = quasiPermanent / design;
                result.EffectiveRatio = creep.FinalCoefficient.Value * result.MomentRatio.Value;
                Step(ModelColumnTraceCodes.FinalCreepCoefficient, "Coefficiente di viscosità finale", "φ(∞,t0)", creep.FinalCoefficient.Value, "−", "NTC 2018 §11.2.10.7; EN 1992-1-1 3.1.4");
                Step(ModelColumnTraceCodes.CreepMomentRatio, "Rapporto dei momenti del primo ordine quasi permanente e di progetto, con l'imperfezione",
                    "M0Eqp/M0Ed = (M0Eqp + NEqp·ei)/(M0Ed + NEd·ei)", result.MomentRatio.Value, "−", "EN 1992-1-1 5.8.4(2)");
            }
            Step(ModelColumnTraceCodes.EffectiveCreepRatio, "Coefficiente di viscosità efficace", creep.Kind == ModelColumnCreepKind.FinalCoefficient ? "φef = φ(∞,t0)·M0Eqp/M0Ed"
                : creep.Kind == ModelColumnCreepKind.Effective ? "φef assegnato" : "φef = 0 (viscosità trascurata)", result.EffectiveRatio, "−",
                creep.Kind == ModelColumnCreepKind.FinalCoefficient ? "EN 1992-1-1 (5.19)" : reference);
            if (creep.FinalCoefficient.HasValue) result.FinalCoefficientCondition = creep.FinalCoefficient.Value <= 2;
            result.SlendernessCondition = lambda <= 75;
            if (n == 0) result.EccentricityCondition = true;
            else if (p.FirstOrderMoment.HasValue) result.EccentricityCondition = (p.FirstOrderMoment.Value + imperfection.Moment) / n >= h;
            result.NeglectAdmissible = result.FinalCoefficientCondition == true && result.SlendernessCondition && result.EccentricityCondition == true;
            if (result.EffectiveRatio == 0)
            {
                Step(ModelColumnTraceCodes.CreepConditions, "Condizioni per trascurare la viscosità: φ(∞,t0) ≤ 2, λ ≤ 75, M0Ed/NEd ≥ h",
                    result.NeglectAdmissible ? "soddisfatte" : "non dimostrate", null, "", "EN 1992-1-1 5.8.4(4)");
                if (!result.NeglectAdmissible && n > 0) warnings.Add(ModelColumnWarning.CreepNeglectedWithoutConditions);
            }
            return result;
        }

        // ---------------------------------------------------------------- nominal curvature

        private ModelColumnNominalCurvature Nominal(double n, double area, double fcd, double fck, double asFyd, double lambda, double phi, double h, double iS,
            double yieldStrain, double l0, double c, double? m0)
        {
            const string reference = "EN 1992-1-1 5.8.8";
            var r = new ModelColumnNominalCurvature { RelativeAxialForce = n / (area * fcd), MechanicalRatio = asFyd / (area * fcd), Nbal = 0.4, YieldStrain = yieldStrain };
            r.Nu = 1 + r.MechanicalRatio;
            r.Kr = Math.Max(0, Math.Min(1, (r.Nu - r.RelativeAxialForce) / (r.Nu - r.Nbal)));
            r.Beta = 0.35 + fck / 200 - lambda / 150;
            r.Kphi = Math.Max(1, 1 + r.Beta * phi);
            r.EffectiveDepth = h / 2 + iS;
            r.BasicCurvature = yieldStrain / (0.45 * r.EffectiveDepth);
            r.Curvature = r.Kr * r.Kphi * r.BasicCurvature;
            r.Deflection = r.Curvature * l0 * l0 / c;
            r.SecondOrderMoment = n * r.Deflection;
            r.ResistingMoment = Resistance();
            if (r.ResistingMoment.HasValue)
            {
                r.MaximumFirstOrderMoment = r.ResistingMoment.Value - r.SecondOrderMoment;
                if (m0.HasValue)
                {
                    r.DesignMoment = m0.Value + r.SecondOrderMoment;
                    r.Ratio = r.ResistingMoment.Value > 0 ? r.DesignMoment.Value / r.ResistingMoment.Value : double.PositiveInfinity;
                }
            }
            else warnings.Add(ModelColumnWarning.ResistanceNotAvailable);
            Step(ModelColumnTraceCodes.NominalKr, "Curvatura nominale (confronto informativo): coefficiente dello sforzo normale",
                "Kr = (nu − n)/(nu − nbal) ≤ 1; nu = 1 + ω; nbal = 0,4", r.Kr, "−", reference + " (5.36)");
            Step(ModelColumnTraceCodes.NominalBeta, "Curvatura nominale: coefficiente della viscosità", "β = 0,35 + fck/200 − λ/150", r.Beta, "−", reference + " (5.37)");
            Step(ModelColumnTraceCodes.NominalKphi, "Curvatura nominale: coefficiente della viscosità", "Kφ = 1 + β·φef ≥ 1", r.Kphi, "−", reference + " (5.37)");
            Step(ModelColumnTraceCodes.NominalDepth, "Curvatura nominale: altezza utile", "d = h/2 + is", r.EffectiveDepth, "mm", reference + " (5.35)");
            Step(ModelColumnTraceCodes.NominalBasicCurvature, "Curvatura nominale di base", "1/r0 = εyd/(0,45·d)", r.BasicCurvature, "1/mm", reference + ".3(1)");
            Step(ModelColumnTraceCodes.NominalCurvature, "Curvatura nominale", "1/r = Kr·Kφ·1/r0", r.Curvature, "1/mm", reference + " (5.34)");
            Step(ModelColumnTraceCodes.NominalDeflection, "Curvatura nominale: spostamento del secondo ordine", "e2 = (1/r)·l0²/c", r.Deflection, "mm", reference + ".2(3)");
            Step(ModelColumnTraceCodes.NominalSecondOrderMoment, "Curvatura nominale: momento del secondo ordine", "M2 = NEd·e2", r.SecondOrderMoment, "Nmm", reference + " (5.33)");
            if (r.ResistingMoment.HasValue)
            {
                Step(ModelColumnTraceCodes.NominalResistance, "Momento resistente della sezione a NEd (dominio di rottura)", "MRd(NEd)", r.ResistingMoment.Value, "Nmm",
                    "NTC 2018 [4.1.18a]; EN 1992-1-1 6.1");
                Step(ModelColumnTraceCodes.NominalMaximumFirstOrderMoment, "Curvatura nominale: momento massimo del primo ordine", "MRd − M2",
                    r.MaximumFirstOrderMoment.Value, "Nmm", reference);
                if (r.DesignMoment.HasValue)
                {
                    Step(ModelColumnTraceCodes.NominalDesignMoment, "Curvatura nominale: momento di progetto", "MEd = M0Ed,tot + M2", r.DesignMoment.Value, "Nmm", reference + " (5.31)");
                    Step(ModelColumnTraceCodes.NominalRatio, "Curvatura nominale: rapporto di utilizzo", "MEd/MRd", r.Ratio.Value, "−", reference);
                }
            }
            return r;
        }

        /// <summary>MRd(N) of the failure domain of the checker in the direction θ (validated point at constant N); null when not available.</summary>
        private double? Resistance()
        {
            try
            {
                var force = new ResultBeamForces(nEd, 0, 0, 0, cd * 1e6, sd * 1e6, axes);
                var point = solver.CalculateConvergedDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantN, token);
                if (point?.StrainPlane == null || !ModelColumnAnalysis.Finite(point.NRd + point.MxRd + point.MyRd) || Math.Abs(point.NRd - nEd) > p.AxialTolerance)
                    return null;
                var forces = new ResultBeamForces(point.NRd, 0, 0, 0, point.MxRd, point.MyRd, forceAxes).ToCoordinateSystem(axes);
                double m = forces.M1 * cd + forces.M2 * sd;
                return m > 0 ? m : (double?)null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return null;
            }
        }
    }
}
