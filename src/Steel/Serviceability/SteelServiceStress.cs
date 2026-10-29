using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;

namespace GPC.Checkers.Steel.Serviceability
{
    /// <summary>Declared basis of the serviceability stress limits sigma &lt;= fy/gammaM,ser, tau &lt;= fy/(sqrt3 gammaM,ser)
    /// and sqrt(sigma^2+3tau^2) &lt;= fy/gammaM,ser (EN 1993-2:2006 7.3(1), eq. 7.1-7.3). gammaM,ser is a nationally
    /// determined parameter (recommended 1.00) and has no default here. EN 1993-1-1 does not require the check for
    /// buildings and no NTC 2018/Circolare 2019 text for it is verified in this repository: the applicability (bridge,
    /// project requirement) is a caller declaration, recorded and never inferred. Sources are provenance, not certification.</summary>
    public sealed class SteelServiceStressBasis
    {
        public double GammaMSer { get; }
        public string GammaMSerSource { get; }
        public string ApplicabilitySource { get; }

        public SteelServiceStressBasis(double gammaMSer, string gammaMSerSource, string applicabilitySource)
        {
            SteelInputValidation.Finite(gammaMSer, nameof(gammaMSer));
            if (gammaMSer < 1) throw new ArgumentOutOfRangeException(nameof(gammaMSer), "gammaM,ser below 1 would admit service stresses above fy.");
            if (string.IsNullOrWhiteSpace(gammaMSerSource)) throw new ArgumentException("The source of gammaM,ser is required.", nameof(gammaMSerSource));
            if (string.IsNullOrWhiteSpace(applicabilitySource)) throw new ArgumentException("The applicability source of the stress limitation is required.", nameof(applicabilitySource));
            GammaMSer = gammaMSer; GammaMSerSource = gammaMSerSource; ApplicabilitySource = applicabilitySource;
        }
    }

    public enum SteelServiceStressCriterion { Normal, Shear, Equivalent }

    /// <summary>Governing value of one criterion and its point, in section geometry axes X/Y through the gross
    /// centroid (mm). Normal: signed sigma, tension positive. Shear: |tau| along the wall. Equivalent:
    /// sqrt(sigma^2+3tau^2) with Sigma and Tau of the same point, except IsBound (class 4), where the maxima of
    /// different points are combined (X/Y are then those of the sigma maximum).</summary>
    public sealed class SteelServiceStressValue
    {
        public SteelServiceStressCriterion Criterion { get; }
        public double Stress { get; }
        public double Limit { get; }
        public double Utilization { get; }
        public bool Passed => Utilization <= 1;
        public double Sigma { get; }
        public double Tau { get; }
        public string Location { get; }
        public double X { get; }
        public double Y { get; }
        public bool IsBound { get; }
        public string Reference { get; }

        internal SteelServiceStressValue(SteelServiceStressCriterion criterion, double stress, double limit, double sigma, double tau,
            string location, double x, double y, bool bound, string reference)
        {
            foreach (double v in new[] { stress, limit, sigma, tau, x, y }) SteelInputValidation.Finite(v, "service stress");
            Criterion = criterion; Stress = stress; Limit = limit; Utilization = Math.Abs(stress) / limit;
            Sigma = sigma; Tau = tau; Location = location; X = x; Y = y; IsBound = bound; Reference = reference;
            SteelInputValidation.Finite(Utilization, "service stress utilization");
        }
    }

    /// <summary>One cut, one serviceability state. Values and Utilization exist only when evaluated; Utilization
    /// is the largest stress/limit ratio of the three criteria (one utilization definition). An unavailable result
    /// keeps its status, code and the trace collected so far.</summary>
    public sealed class SteelServiceStressResult
    {
        public SteelVerificationInput Input { get; }
        public SteelServiceStressBasis Basis { get; }
        public SteelExecutionStatus Status { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }
        public string Reference { get; }
        public string Scope { get; }
        public int? SectionClass { get; }
        public string ClassificationSource { get; }
        /// <summary>"GrossElastic" (classes 1-3 or no compressed fibre) or "EffectiveClass4Bound".</summary>
        public string StressModel { get; }
        public string Family { get; }
        public string ShearModel { get; }
        public IReadOnlyList<SteelServiceStressValue> Values { get; }
        public double? Utilization { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public bool IsEvaluated => Status == SteelExecutionStatus.Passed || Status == SteelExecutionStatus.Failed;
        public SteelServiceStressValue Value(SteelServiceStressCriterion criterion) => Values.FirstOrDefault(v => v.Criterion == criterion);

        internal SteelServiceStressResult(SteelVerificationInput input, SteelServiceStressBasis basis, SteelExecutionStatus status,
            string code, string message, string reference, string scope, int? sectionClass, string classificationSource,
            string stressModel, string family, string shearModel, IEnumerable<SteelServiceStressValue> values, IEnumerable<SectionResistanceValue> trace)
        {
            Input = input; Basis = basis; Status = status; DiagnosticCode = code; Message = message; Reference = reference; Scope = scope;
            SectionClass = sectionClass; ClassificationSource = classificationSource; StressModel = stressModel; Family = family; ShearModel = shearModel;
            Values = Array.AsReadOnly((values ?? Enumerable.Empty<SteelServiceStressValue>()).ToArray());
            Trace = Array.AsReadOnly((trace ?? Enumerable.Empty<SectionResistanceValue>()).ToArray());
            Utilization = Values.Count == 0 ? (double?)null : Values.Max(v => v.Utilization);
        }
    }

    /// <summary>Serviceability stress limitation of a steel section at one cut: elastic nominal stresses of the
    /// concomitant actions, which the caller declares to be those of the characteristic combination (EN 1993-2:2006
    /// 7.3(1)). sigma = N/A + Mx y/Ixx - My x/Iyy on the gross Model section (classes 1-3, or no compressed fibre);
    /// class 4: the effective section of SteelEffectiveSectionSolver (widths at fy, conservative at service stresses)
    /// on its bounding rectangle. tau: qualified thin-walled or Jourawski distributions (ServiceStressModel). The
    /// maxima of sigma, tau and sqrt(sigma^2+3tau^2) are exact along each evaluated line (polynomial maxima).
    /// Torsion, shear lag, secondary effects, transverse stresses, local loads, web breathing and fatigue are excluded.</summary>
    public static class SteelServiceStressCheck
    {
        public const string MethodId = "Steel.ServiceStress";
        private const string Scope = "Elastic nominal stresses of one cut, characteristic combination declared by the caller: gross section for classes 1-3, "
            + "class4 bound on the effective section (widths at fy) of the bounding rectangle; tau by qualified thin-walled/Jourawski distributions for exact doubly "
            + "symmetric H, RHS, CHS and narrow solid rectangles. Excluded: torsion, shear lag and secondary effects (EN 1993-2 7.3(1): to be in the actions or "
            + "declared negligible by the applicability source), transverse stresses, patch loads, web breathing, fatigue, holes and connections.";

        public static SteelServiceStressResult Evaluate(SteelVerificationInput input, SteelServiceStressBasis basis,
            SteelSectionClassification classification = null, CancellationToken cancellation = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (classification != null && !ReferenceEquals(classification.Input, input))
                throw new ArgumentException("Classification belongs to a different input snapshot/code/action state.", nameof(classification));
            cancellation.ThrowIfCancellationRequested();
            var run = new Run(input, basis);
            try { return run.Execute(classification, cancellation); }
            catch (ArithmeticException ex) { return run.Stop(SteelExecutionStatus.CalculationError, "ServiceStress.NumericalRange", ex.Message); }
        }

        private sealed class Run
        {
            private readonly SteelVerificationInput input;
            private readonly SteelServiceStressBasis basis;
            private readonly List<SectionResistanceValue> trace = new List<SectionResistanceValue>();
            private readonly string reference;
            private int? sectionClass; private string classificationSource, stressModel, family, shearModel;

            internal Run(SteelVerificationInput input, SteelServiceStressBasis basis)
            {
                this.input = input; this.basis = basis;
                reference = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                    ? "Declared limits in the form of EN 1993-2:2006 7.3(1) eq. (7.1)-(7.3) (no NTC 2018/Circolare 2019 clause verified in the repository); tau = V S/(I t)"
                    : "EN 1993-2:2006 7.3(1) eq. (7.1)-(7.3), characteristic combination; tau EN 1993-1-1:2005 6.2.6(4) eq. (6.20)";
            }

            private void Add(string name, double value, string unit) => trace.Add(new SectionResistanceValue(name, value, unit));

            internal SteelServiceStressResult Stop(SteelExecutionStatus status, string code, string message)
                => new SteelServiceStressResult(input, basis, status, code, message, reference, Scope, sectionClass, classificationSource,
                    stressModel, family, shearModel, null, trace);

            internal SteelServiceStressResult Execute(SteelSectionClassification classification, CancellationToken cancellation)
            {
                var a = input.EffectiveActions; var s = input.Section;
                foreach (var v in new[] { ("N", a.N, "N"), ("V1", a.V1, "N"), ("V2", a.V2, "N"), ("T", a.T, "Nmm"), ("M1", a.M1, "Nmm"), ("M2", a.M2, "Nmm") })
                    Add(v.Item1, v.Item2, v.Item3);
                foreach (var n in input.NeglectedActions)
                { Add("Neglected." + n.Component, n.Value, n.Unit); Add("Neglected." + n.Component + ".Limit", n.Limit, n.Unit); }
                if (basis == null)
                    return Stop(SteelExecutionStatus.MissingInput, "ServiceStress.BasisRequired",
                        "Declare gammaM,ser with its source and the applicability source of the stress limitation; none is inferred.");
                Add("Fy", s.YieldStrength, "MPa"); Add("GammaMSer", basis.GammaMSer, "1");
                if (a.N == 0 && a.V1 == 0 && a.V2 == 0 && a.T == 0 && a.M1 == 0 && a.M2 == 0)
                    return Stop(SteelExecutionStatus.NotRequired, "ServiceStress.NoActions", "No action at this cut; no stress conclusion is implied.");
                if (a.T != 0)
                    return Stop(SteelExecutionStatus.NotSupported, "ServiceStress.Torsion.NotIntegrated",
                        "The library computes no St Venant or warping stresses: torsion must be zero (or below the negligible-action limit).");
                if (s.Unperforated != true)
                    return s.Unperforated == false
                        ? Stop(SteelExecutionStatus.NotSupported, "ServiceStress.PerforatedSection.NotQualified", "Service stresses of perforated sections require their own qualified treatment.")
                        : Stop(SteelExecutionStatus.MissingInput, "ServiceStress.NetSection.DeclarationRequired", "Declare an unperforated section before using the gross-section stresses.");
                var section = s.ReadOnlySection;
                try { SteelMethodSelector.ValidateGeometry(section); }
                catch (ArgumentException ex) { return Stop(SteelExecutionStatus.InvalidInput, "ServiceStress.Geometry.Invalid", ex.Message); }

                // Principal actions back to the geometry axes, where the supported shapes are symmetric (Ixy = 0).
                double angle = s.PrincipalAxisAngle, c = Math.Cos(angle), sn = Math.Sin(angle);
                double mx = c * a.M1 - sn * a.M2, my = sn * a.M1 + c * a.M2, vx = c * a.V1 - sn * a.V2, vy = sn * a.V1 + c * a.V2;
                double area = section.Area, ixx = section.Jxx, iyy = section.Jyy;
                Add("PrincipalAxisAngle", angle, "rad"); Add("Mx", mx, "Nmm"); Add("My", my, "Nmm"); Add("Vx", vx, "N"); Add("Vy", vy, "N");
                Add("Area", area, "mm2"); Add("Ixx", ixx, "mm4"); Add("Iyy", iyy, "mm4");
                if (!(ixx > 0) || !(iyy > 0) || !(Math.Abs(section.Jxy) <= 1e-8 * Math.Sqrt(ixx * iyy)))
                    return Stop(SteelExecutionStatus.NotSupported, "ServiceStress.Geometry.NotQualified", "The section geometry axes must be principal (Ixy = 0).");
                ServiceStressModel model;
                try
                {
                    model = ServiceStressModel.Create(section, vx, vy, out string code, out string message);
                    if (model == null) return Stop(SteelExecutionStatus.NotSupported, "ServiceStress." + code, message);
                }
                catch (ArgumentException ex) { return Stop(SteelExecutionStatus.InvalidInput, "ServiceStress.Geometry.Invalid", ex.Message); }
                family = model.Family; shearModel = model.ShearModel;
                Add("ShearModel.Ixx", model.ShearIxx, "mm4"); Add("ShearModel.Iyy", model.ShearIyy, "mm4");

                double Sigma(double x, double y) => a.N / area + mx * y / ixx - my * x / iyy;
                var gross = model.IsCircle ? CircleMaxima(model, Sigma, area, mx, my, ixx, iyy, vx, vy) : LineMaxima(model, Sigma);
                cancellation.ThrowIfCancellationRequested();

                var normal = gross[0];
                if ((a.N != 0 || a.M1 != 0 || a.M2 != 0) && gross[3].Sigma < 0)
                {
                    SteelMethodSelector.ClassificationInfo info;
                    try { info = SteelMethodSelector.Classification(input, section, classification, true); }
                    catch (NotSupportedException ex) { return Stop(SteelExecutionStatus.NotSupported, "ServiceStress.Classification.NotQualified", ex.Message); }
                    catch (ArgumentException ex) { return Stop(SteelExecutionStatus.InvalidInput, "ServiceStress.Classification.Invalid", ex.Message); }
                    if (info.Failure != null)
                        return Stop(info.Failure.Status == SteelSelectionStatus.MissingInput ? SteelExecutionStatus.MissingInput
                            : info.Failure.Status == SteelSelectionStatus.InvalidInput ? SteelExecutionStatus.InvalidInput : SteelExecutionStatus.NotSupported,
                            "ServiceStress." + info.Failure.DiagnosticCode, info.Failure.Message);
                    sectionClass = info.Class; classificationSource = info.Source;
                    Add("SectionClass", info.Class.Value, "1");
                }
                else classificationSource = "No compressed fibre: local buckling classification not required";

                bool bound = sectionClass == 4;
                if (bound)
                {
                    SteelEffectiveSectionResult effective;
                    try
                    {
                        effective = SteelEffectiveSectionSolver.Solve(new SteelVerificationInput(s, new SteelActions(a.N, 0, 0, 0, a.M1, a.M2), input.Code),
                            cancellation: cancellation);
                    }
                    catch (NotSupportedException ex) { return Stop(SteelExecutionStatus.NotSupported, "ServiceStress.Class4.NotQualified", ex.Message); }
                    catch (InvalidOperationException ex) { return Stop(SteelExecutionStatus.CalculationError, "ServiceStress.Class4.NotConverged", ex.Message); }
                    var st = effective.State; var g = st.Geometry;
                    Add("AreaEffective", st.Area, "mm2"); Add("CentroidShiftX", st.CentroidShiftX, "mm"); Add("CentroidShiftY", st.CentroidShiftY, "mm");
                    Add("EffectiveIxx", st.Ixx, "mm4"); Add("EffectiveIyy", st.Iyy, "mm4"); Add("EffectiveIxy", st.Ixy, "mm4");
                    Add("EffectiveIterations", effective.Iterations, "1"); Add("EffectiveResidual", effective.Residual, "1");
                    StressPoint governing = null;
                    foreach (double x in new[] { g.MinX, g.MaxX })
                        foreach (double y in new[] { g.MaxY, g.MinY })
                        {
                            var p = new StressPoint("EffectiveSection.BoundingCorner", x - g.GrossX, y - g.GrossY, st.Stress(x, y, a.N, a.M1, a.M2), 0);
                            if (governing == null || Math.Abs(p.Sigma) > Math.Abs(governing.Sigma)) governing = p;
                        }
                    normal = governing;
                    stressModel = "EffectiveClass4Bound";
                }
                else stressModel = "GrossElastic";

                double fy = s.YieldStrength, limit = fy / basis.GammaMSer, shearLimit = limit / Math.Sqrt(3);
                Add("Limit.Normal", limit, "MPa"); Add("Limit.Shear", shearLimit, "MPa"); Add("Limit.Equivalent", limit, "MPa");
                var shear = gross[1];
                var values = new List<SteelServiceStressValue>
                {
                    new SteelServiceStressValue(SteelServiceStressCriterion.Normal, normal.Sigma, limit, normal.Sigma, normal.Tau, normal.Location, normal.X, normal.Y, bound,
                        "(7.1) sigma <= fy/gammaM,ser" + (bound ? "; class4 effective section bound" : "")),
                    new SteelServiceStressValue(SteelServiceStressCriterion.Shear, Math.Abs(shear.Tau), shearLimit, shear.Sigma, Math.Abs(shear.Tau), shear.Location, shear.X, shear.Y, false,
                        "(7.2) tau <= fy/(sqrt3 gammaM,ser)"),
                    bound
                        ? new SteelServiceStressValue(SteelServiceStressCriterion.Equivalent, Math.Sqrt(normal.Sigma * normal.Sigma + 3 * shear.Tau * shear.Tau), limit,
                            normal.Sigma, Math.Abs(shear.Tau), normal.Location + " + " + shear.Location, normal.X, normal.Y, true,
                            "(7.3) sqrt(sigma^2+3tau^2) <= fy/gammaM,ser; class4 bound of the maxima at different points")
                        : new SteelServiceStressValue(SteelServiceStressCriterion.Equivalent, gross[2].Equivalent, limit, gross[2].Sigma, Math.Abs(gross[2].Tau),
                            gross[2].Location, gross[2].X, gross[2].Y, false, "(7.3) sqrt(sigma^2+3tau^2) <= fy/gammaM,ser at the same point")
                };
                foreach (var v in values)
                {
                    string key = v.Criterion.ToString();
                    Add(key + ".Stress", v.Stress, "MPa"); Add(key + ".Sigma", v.Sigma, "MPa"); Add(key + ".Tau", v.Tau, "MPa");
                    Add(key + ".X", v.X, "mm"); Add(key + ".Y", v.Y, "mm"); Add(key + ".Utilization", v.Utilization, "1");
                }
                var status = values.All(v => v.Passed) ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed;
                return new SteelServiceStressResult(input, basis, status, null, null, reference, Scope, sectionClass, classificationSource,
                    stressModel, family, shearModel, values, trace);
            }
        }

        private sealed class StressPoint
        {
            internal string Location { get; }
            internal double X { get; }
            internal double Y { get; }
            internal double Sigma { get; }
            internal double Tau { get; }
            internal double Equivalent => Math.Sqrt(Sigma * Sigma + 3 * Tau * Tau);
            internal StressPoint(string location, double x, double y, double sigma, double tau)
            { Location = location; X = x; Y = y; Sigma = sigma; Tau = tau; }
        }

        // [0] max |sigma|, [1] max |tau|, [2] max equivalent, [3] min sigma (compressed fibre test); first maximum on ties.
        private static StressPoint[] LineMaxima(ServiceStressModel model, Func<double, double, double> sigma)
        {
            var best = new StressPoint[4];
            void Consider(int k, StressPoint p, double value, Func<StressPoint, double> measure)
            { if (best[k] == null || value > measure(best[k])) best[k] = p; }
            foreach (var line in model.Lines)
            {
                StressPoint At(double u)
                {
                    double x = line.X0 + (line.X1 - line.X0) * u, y = line.Y0 + (line.Y1 - line.Y0) * u;
                    return new StressPoint(line.Id, x, y, sigma(x, y), line.Tau.At(u));
                }
                foreach (double u in new[] { 0d, 1d })
                {
                    var p = At(u);
                    Consider(0, p, Math.Abs(p.Sigma), q => Math.Abs(q.Sigma));
                    Consider(3, p, -p.Sigma, q => -q.Sigma);
                }
                var tau = line.Tau;
                var shearCandidates = new List<double> { 0, 1 }; shearCandidates.AddRange(tau.Derivative().Roots(0, 1));
                foreach (double u in shearCandidates) { var p = At(u); Consider(1, p, Math.Abs(p.Tau), q => Math.Abs(q.Tau)); }
                double s0 = sigma(line.X0, line.Y0), s1 = sigma(line.X1, line.Y1);
                var sig = new StressPolynomial(s0, s1 - s0);
                var equivalent = sig * sig + 3 * (tau * tau);
                var e = At(equivalent.ArgMax(0, 1));
                Consider(2, e, e.Equivalent, q => q.Equivalent);
            }
            return best;
        }

        // Outer surface sigma(theta) = a + p cos + q sin, midline tau(theta) = c cos + d sin. The stationary points
        // of sigma^2+3tau^2 = A0 + A1 cos + B1 sin + A2 cos2 + B2 sin2 are the roots of a quartic in t = tan(theta/2)
        // on each half circle (|t| <= 1), so the maximum is exact.
        private static StressPoint[] CircleMaxima(ServiceStressModel model, Func<double, double, double> sigma,
            double area, double mx, double my, double ixx, double iyy, double vx, double vy)
        {
            double r = model.OuterRadius, k = 1 / (Math.PI * model.MidRadius * model.WallThickness);
            double sa = sigma(0, 0), sp = -my * r / iyy, sq = mx * r / ixx, tc = vy * k, td = -vx * k;
            StressPoint At(double theta)
            {
                double x = r * Math.Cos(theta), y = r * Math.Sin(theta);
                return new StressPoint("Wall.Outer", x, y, sigma(x, y), tc * Math.Cos(theta) + td * Math.Sin(theta));
            }
            double bending = Math.Sqrt(sp * sp + sq * sq);
            var normal = At(bending == 0 ? 0 : sa >= 0 ? Math.Atan2(sq, sp) : Math.Atan2(-sq, -sp));
            var compressed = At(bending == 0 ? 0 : Math.Atan2(-sq, -sp));
            var shear = At(tc == 0 && td == 0 ? 0 : Math.Atan2(td, tc));
            double a1 = 2 * sa * sp, b1 = 2 * sa * sq, a2 = (sp * sp - sq * sq) / 2 + 1.5 * (tc * tc - td * td), b2 = sp * sq + 3 * tc * td;
            var candidates = new List<double> { 0, Math.PI / 2, Math.PI, -Math.PI / 2 };
            foreach (int half in new[] { 1, -1 })
            {
                double h1 = half * a1, g1 = half * b1;
                var g = new StressPolynomial(g1 + 2 * b2, -2 * h1 - 8 * a2, -12 * b2, -2 * h1 + 8 * a2, -g1 + 2 * b2);
                candidates.AddRange(g.Roots(-1, 1).Select(t => (half > 0 ? 0 : Math.PI) + 2 * Math.Atan(t)));
            }
            StressPoint equivalent = null;
            foreach (double theta in candidates)
            {
                var p = At(theta);
                if (equivalent == null || p.Equivalent > equivalent.Equivalent) equivalent = p;
            }
            return new[] { normal, shear, equivalent, compressed };
        }
    }
}
