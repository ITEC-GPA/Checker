using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Serviceability;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    /// <summary>Physical-member serviceability (deformation) engine: vertical deflection, drift between the span ends and the
    /// natural frequency of the member, from the member-node displacements that BeamCheckPlan prepares for a deformation task
    /// (MemberCheckSpecification.Category/Deformation). Limits, support schemes, precamber, coverage and mass state are declared
    /// parameters with their sources; a missing declaration is a precise missing input, never a default limit.</summary>
    public sealed class SteelMemberServiceabilityChecker : IMaterialChecker, IPhysicalMemberVerifier
    {
        public const string EngineId = "Steel.Serviceability.Member";
        internal const string Limitations = "EC3:2005 7.2 with EN1990 A1.4 / NTC2018 4.2.4.2 member deformations from the exported member-node displacements of one declared state, in member axes: vertical deflection relative to the chord of the design span or to the supported end of a cantilever (maximum over the exported nodes only, declared coverage; precamber at the governing node), variable-action deflection from a declared state or a linear difference of states, relative displacement of the span ends against h/n, natural frequency f=17.75/sqrt(delta[mm]) of a simply supported member with uniform mass or a declared qualified coefficient. Every limit, scheme and source is declared: no table value of the standards is built in. Building-level drift of several column lines, floor response/acceleration criteria, dynamic analyses and time-dependent deflections are outside.";
        private readonly Dictionary<string, string> _parameters;
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public StandardEN1993p11 Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        public string Id => EngineId;
        public string Version => NativeResults.Version(typeof(SteelMemberDeformation));
        public string Configuration => ModelValues.Fingerprint(new object[] { Id, Code, Edition, NationalAnnex, _parameters });
        public CheckStandardContext Standard => new CheckStandardContext(Code.Name, Edition, NationalAnnex, Id, Configuration, Limitations);
        private bool Ntc => Code.GetType() == typeof(StandardNTC2018Steel);

        public SteelMemberServiceabilityChecker(StandardEN1993p11 code, string edition, string nationalAnnex = null, IDictionary<string, string> parameters = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(edition)) throw new ArgumentException("ExplicitSteelEditionRequired");
            Edition = edition; NationalAnnex = nationalAnnex;
            _parameters = parameters == null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            var fields = SteelParameterSchemas.MemberServiceability.Parameters.ToDictionary(p => p.Key, StringComparer.Ordinal);
            foreach (var pair in _parameters)
                if (!fields.TryGetValue(pair.Key, out var field) || string.IsNullOrWhiteSpace(pair.Value)) throw new ArgumentException("UnknownOrEmptySteelServiceabilityParameter: " + pair.Key);
                else if (!field.Accepts(pair.Value)) throw new ArgumentException("InvalidSteelServiceabilityParameter: " + pair.Key);
            Parameters = new ReadOnlyDictionary<string, string>(_parameters);
        }
        public bool Accepts(BeamElement element) => element?.BeamProperty is ISteelSection;
        public bool Supports(string methodId, CheckMechanism mechanism) => methodId == Id && mechanism == CheckMechanism.Serviceability
            && (Code.GetType() == typeof(StandardEN1993p11) && Edition == "2005" || Ntc && Edition == "2018");
        public IMaterialCheckSession CreateSession() => new NoSectionSession();
        private sealed class NoSectionSession : IMaterialCheckSession
        {
            public int CreatedCheckers => 0;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return NativeResults.Unavailable("SteelServiceability.PhysicalMemberRequired"); }
        }

        /// <summary>A declaration the task needs is absent: reported as missing input with its own code.</summary>
        private sealed class MissingDeclaration : Exception
        {
            internal string Code { get; }
            internal MissingDeclaration(string code, string message) : base(message) { Code = code; }
        }
        private string Text(string key, string code) => _parameters.TryGetValue(key, out var value) ? value
            : throw new MissingDeclaration(code, "Declare " + key + ".");
        private double Number(string key, string code) => double.Parse(Text(key, code), NumberStyles.Float, CultureInfo.InvariantCulture);
        private bool Has(string key) => _parameters.ContainsKey(key);
        private void Forbid(string reason, params string[] keys)
        {
            var present = keys.Where(Has).ToArray();
            if (present.Length != 0) throw new ArgumentException(reason + ": " + string.Join(", ", present));
        }

        private string Clause(MemberDeformationCriterion criterion)
        {
            switch (criterion)
            {
                case MemberDeformationCriterion.VerticalDeflection: return Ntc ? "NTC 2018 4.2.4.2.1 (delta_max, delta_2; Tab. 4.2.XII limits declared)" : "EN 1993-1-1:2005 7.2.1; EN 1990 A1.4.3 (w_max = w_tot - w_c, w_3)";
                case MemberDeformationCriterion.HorizontalDisplacement: return Ntc ? "NTC 2018 4.2.4.2.2 (Delta/h; Tab. 4.2.XIII limits declared)" : "EN 1993-1-1:2005 7.2.2; EN 1990 A1.4.3 (u_i)";
                default: return Ntc ? "NTC 2018 4.2.4.2.3" : "EN 1993-1-1:2005 7.2.3; EN 1990 A1.4.4";
            }
        }

        public CheckResult Verify(PhysicalMemberCheckInput input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var deformation = input?.Snapshot?.Deformation;
            if (deformation == null) return NativeResults.Missing("SteelServiceability.DeformationInputRequired");
            if (!Supports(input.MethodId, input.Mechanism)) return NativeResults.Unavailable("SteelServiceability.MethodOrNormNotQualified");
            var spec = deformation.Specification; var snapshot = input.Snapshot;
            var trace = new List<CheckCalculationValue> {
                new CheckCalculationValue("Deformation.Criterion", null, null, spec.Key),
                new CheckCalculationValue("Deformation.Category", null, null, deformation.Category.ToString()),
                new CheckCalculationValue("Member.SpanStart", snapshot.SpanStart, "mm", "Design span start along the physical member"),
                new CheckCalculationValue("Member.SpanEnd", snapshot.SpanEnd, "mm", "Design span end along the physical member"),
                new CheckCalculationValue("Member.DeformableLength", snapshot.DeformableLength, "mm", "Member length without rigid zones (reference only)"),
                new CheckCalculationValue("Deformation.Nodes", deformation.Total.Count, "1", "Exported member nodes of the task state on the span; no interpolation") };
            try
            {
                List<SteelDeformationCheck> checks;
                switch (spec.Criterion)
                {
                    case MemberDeformationCriterion.VerticalDeflection: checks = Deflection(snapshot, deformation, spec, trace); break;
                    case MemberDeformationCriterion.HorizontalDisplacement: checks = Drift(snapshot, deformation, spec, trace); break;
                    case MemberDeformationCriterion.Vibration: checks = Vibration(snapshot, deformation, spec, trace); break;
                    default: return NativeResults.Unavailable("SteelServiceability.UnknownCriterion");
                }
                token.ThrowIfCancellationRequested();
                return Decision(checks, deformation, trace);
            }
            catch (MissingDeclaration ex)
            {
                var result = NativeResults.Missing(ex.Code);
                result.Diagnostics.Add(ModelDiagnostic.Error("SteelServiceability.InputDetail", message: ex.Message)); return result;
            }
            catch (ArgumentException ex)
            {
                var result = NativeResults.Missing("SteelServiceability.InvalidOrMissingInput");
                result.Diagnostics.Add(ModelDiagnostic.Error("SteelServiceability.InputDetail", message: ex.Message)); return result;
            }
            catch (NotSupportedException ex) { return NativeResults.Unavailable("SteelServiceability.NotSupported", ex.Message); }
        }

        private static double Component(MemberDisplacementSample s, MemberDeformationDirection direction)
            => direction == MemberDeformationDirection.Axis1 ? s.D1 : direction == MemberDeformationDirection.Axis2 ? s.D2
                : throw new NotSupportedException("TransverseDirectionOnlyForHorizontalDisplacement");
        private static IEnumerable<SteelDisplacementPoint> Points(IEnumerable<MemberDisplacementSample> samples, MemberDeformationDirection direction)
            => samples.Select(s => new SteelDisplacementPoint(s.Location.Distance, Component(s, direction), "Node " + s.NodeId.ToString(CultureInfo.InvariantCulture)));

        private List<SteelDeformationCheck> Deflection(MemberInputSnapshot snapshot, MemberDeformationSnapshot deformation, MemberDeformationSpecification spec, List<CheckCalculationValue> trace)
        {
            string schemeText = Text("Deflection.Scheme", "SteelServiceability.MissingDeflectionScheme");
            var scheme = (SteelDeflectionScheme)Enum.Parse(typeof(SteelDeflectionScheme), schemeText, false);
            string schemeSource = Text("Deflection.SchemeSource", "SteelServiceability.MissingDeflectionScheme");
            string coverage = Text("Deformation.CoverageSource", "SteelServiceability.MissingCoverageSource");
            bool max = Has("Deflection.MaxRatio") || Has("Deflection.MaxSource"), variable = Has("Deflection.VariableRatio") || Has("Deflection.VariableSource");
            if (!max && !variable) throw new MissingDeclaration("SteelServiceability.MissingDeflectionLimit", "Declare Deflection.MaxRatio/MaxSource and/or Deflection.VariableRatio/VariableSource.");
            bool basis = spec.VariableBasis != MemberVariableDeflectionBasis.None;
            if (variable && !basis) throw new MissingDeclaration("SteelServiceability.MissingVariableDeflectionState", "The task declares no variable-action state (MemberDeformationSpecification.VariableBasis).");
            if (basis && !variable) throw new MissingDeclaration("SteelServiceability.MissingVariableDeflectionLimit", "The task declares a variable-action state: declare Deflection.VariableRatio and Deflection.VariableSource.");
            double span = snapshot.SpanEnd - snapshot.SpanStart, reference = span;
            if (scheme == SteelDeflectionScheme.SupportedEnds) Forbid("CantileverReferenceOnSupportedSpan", "Deflection.CantileverLengthFactor", "Deflection.CantileverLengthSource");
            else
            {
                double factor = Number("Deflection.CantileverLengthFactor", "SteelServiceability.MissingCantileverReference");
                trace.Add(new CheckCalculationValue("Deflection.CantileverLengthFactor", factor, "1", Text("Deflection.CantileverLengthSource", "SteelServiceability.MissingCantileverReference")));
                reference = factor * span;
            }
            var total = SteelDeflectionProfile.Create(Points(deformation.Total, spec.Direction), snapshot.SpanStart, snapshot.SpanEnd, scheme);
            trace.Add(new CheckCalculationValue("Deflection.Scheme", null, null, scheme + ": " + schemeSource));
            trace.Add(new CheckCalculationValue("Deflection.Coverage", null, null, coverage));
            trace.Add(new CheckCalculationValue("Deflection.Span", span, "mm", "SpanEnd - SpanStart"));
            trace.Add(new CheckCalculationValue("Deflection.ReferenceLength", reference, "mm", scheme == SteelDeflectionScheme.SupportedEnds ? "L = span" : "L = factor x cantilever length"));
            trace.Add(new CheckCalculationValue("Deflection.ReferenceAtStart", total.ReferenceAtStart, "mm", "Support translation at the span start (" + spec.Direction + ")"));
            trace.Add(new CheckCalculationValue("Deflection.ReferenceAtEnd", total.ReferenceAtEnd, "mm", "Support translation at the span end (" + spec.Direction + ")"));
            trace.Add(new CheckCalculationValue("Deflection.Total", total.Governing.Value, "mm", "Signed total deflection relative to the reference at " + total.Governing.Label));
            trace.Add(new CheckCalculationValue("Deflection.Total.Location", total.Governing.Distance, "mm", total.Governing.Label));
            var checks = new List<SteelDeformationCheck>();
            if (max)
            {
                double ratio = Number("Deflection.MaxRatio", "SteelServiceability.MissingDeflectionLimit");
                string source = Text("Deflection.MaxSource", "SteelServiceability.MissingDeflectionLimit");
                double precamber = Number("Deflection.Precamber", "SteelServiceability.MissingPrecamber");
                if (precamber != 0) trace.Add(new CheckCalculationValue("Deflection.PrecamberSource", null, null, Text("Deflection.PrecamberSource", "SteelServiceability.MissingPrecamberSource")));
                trace.Add(new CheckCalculationValue("Deflection.Precamber", precamber, "mm", "Signed unloaded offset at the governing node; delta_max = |delta_tot + precamber|"));
                trace.Add(new CheckCalculationValue("Deflection.MaxRatio", ratio, "1", "L/n"));
                checks.Add(SteelMemberDeformation.MaximumDeflection(total, precamber, new SteelDeformationLimit(ratio, reference, source), Clause(spec.Criterion)));
            }
            else Forbid("PrecamberWithoutMaximumDeflection", "Deflection.Precamber", "Deflection.PrecamberSource");
            if (variable)
            {
                double ratio = Number("Deflection.VariableRatio", "SteelServiceability.MissingVariableDeflectionLimit");
                string source = Text("Deflection.VariableSource", "SteelServiceability.MissingVariableDeflectionLimit");
                var state = SteelDeflectionProfile.Create(Points(deformation.Variable, spec.Direction), snapshot.SpanStart, snapshot.SpanEnd, scheme);
                var profile = spec.VariableBasis == MemberVariableDeflectionBasis.Difference ? total.Minus(state) : state;
                trace.Add(new CheckCalculationValue("Deflection.VariableBasis", null, null, spec.VariableBasis == MemberVariableDeflectionBasis.Difference
                    ? "Task state minus " + spec.VariableSelection.Case + " (linear superposition)" : "Declared variable-action state " + spec.VariableSelection.Case));
                trace.Add(new CheckCalculationValue("Deflection.VariableRatio", ratio, "1", "L/n"));
                checks.Add(SteelMemberDeformation.VariableDeflection(profile, new SteelDeformationLimit(ratio, reference, source), Clause(spec.Criterion)));
            }
            return checks;
        }

        private List<SteelDeformationCheck> Drift(MemberInputSnapshot snapshot, MemberDeformationSnapshot deformation, MemberDeformationSpecification spec, List<CheckCalculationValue> trace)
        {
            double ratio = Number("Drift.Ratio", "SteelServiceability.MissingDriftLimit");
            string source = Text("Drift.Source", "SteelServiceability.MissingDriftLimit");
            var start = deformation.Total.FirstOrDefault(s => Math.Abs(s.Location.Distance - snapshot.SpanStart) <= 1e-7);
            var end = deformation.Total.LastOrDefault(s => Math.Abs(s.Location.Distance - snapshot.SpanEnd) <= 1e-7);
            if (start == null || end == null) throw new ArgumentException("DisplacementPointsRequiredAtBothSpanEnds");
            double height = snapshot.SpanEnd - snapshot.SpanStart;
            bool both = spec.Direction == MemberDeformationDirection.Transverse;
            double a1 = both ? start.D1 : Component(start, spec.Direction), b1 = both ? end.D1 : Component(end, spec.Direction);
            var check = SteelMemberDeformation.RelativeDisplacement(a1, b1, both ? start.D2 : (double?)null, both ? end.D2 : (double?)null,
                new SteelDeformationLimit(ratio, height, source), Clause(spec.Criterion));
            trace.Add(new CheckCalculationValue("Drift.Height", height, "mm", "h = SpanEnd - SpanStart"));
            trace.Add(new CheckCalculationValue("Drift.Ratio", ratio, "1", "h/n"));
            trace.Add(new CheckCalculationValue("Drift.Delta1", end.D1 - start.D1, "mm", "Relative displacement along member axis 1, node " + end.NodeId + " minus node " + start.NodeId));
            trace.Add(new CheckCalculationValue("Drift.Delta2", end.D2 - start.D2, "mm", "Relative displacement along member axis 2"));
            trace.Add(new CheckCalculationValue("Drift.Direction", null, null, spec.Direction == MemberDeformationDirection.Transverse ? "Magnitude of both transverse components" : spec.Direction.ToString()));
            return new List<SteelDeformationCheck> { check };
        }

        private List<SteelDeformationCheck> Vibration(MemberInputSnapshot snapshot, MemberDeformationSnapshot deformation, MemberDeformationSpecification spec, List<CheckCalculationValue> trace)
        {
            double minimum = Number("Vibration.MinimumFrequency", "SteelServiceability.MissingFrequencyLimit");
            string source = Text("Vibration.Source", "SteelServiceability.MissingFrequencyLimit");
            string scheme = Text("Vibration.Scheme", "SteelServiceability.MissingVibrationScheme");
            string schemeSource = Text("Vibration.SchemeSource", "SteelServiceability.MissingVibrationScheme");
            string mass = Text("Vibration.MassStateSource", "SteelServiceability.MissingMassStateSource");
            string basis = Text("Vibration.Deflection", "SteelServiceability.MissingVibrationDeflectionBasis");
            string coverage = Text("Deformation.CoverageSource", "SteelServiceability.MissingCoverageSource");
            double coefficient;
            if (scheme == "SimplySupported")
            {
                Forbid("CoefficientOnSimplySupported", "Vibration.Coefficient", "Vibration.CoefficientSource");
                coefficient = SteelMemberDeformation.SimplySupportedFrequencyCoefficient;
                trace.Add(new CheckCalculationValue("Vibration.Coefficient", coefficient, "Hz mm^0.5", "Simply supported, uniform mass: (pi/2) sqrt(5 g/384), g = 9806.65 mm/s^2"));
            }
            else
            {
                coefficient = Number("Vibration.Coefficient", "SteelServiceability.MissingFrequencyCoefficient");
                trace.Add(new CheckCalculationValue("Vibration.Coefficient", coefficient, "Hz mm^0.5", Text("Vibration.CoefficientSource", "SteelServiceability.MissingFrequencyCoefficient")));
            }
            var profile = SteelDeflectionProfile.Create(Points(deformation.Total, spec.Direction), snapshot.SpanStart, snapshot.SpanEnd, SteelDeflectionScheme.SupportedEnds);
            var governing = basis == "RelativeToChord" ? profile.Governing
                : Points(deformation.Total, spec.Direction).Aggregate((a, b) => Math.Abs(b.Value) > Math.Abs(a.Value) ? b : a);
            double delta = Math.Abs(governing.Value);
            if (!(delta > 0)) throw new NotSupportedException("The mass state gives no deflection along " + spec.Direction + ": no frequency estimate.");
            double frequency = SteelMemberDeformation.NaturalFrequency(delta, coefficient);
            trace.Add(new CheckCalculationValue("Vibration.Scheme", null, null, scheme + ": " + schemeSource));
            trace.Add(new CheckCalculationValue("Vibration.MassState", null, null, mass));
            trace.Add(new CheckCalculationValue("Vibration.Coverage", null, null, coverage));
            trace.Add(new CheckCalculationValue("Vibration.Deflection", delta, "mm", (basis == "RelativeToChord" ? "Relative to the chord of the supports" : "Total, support displacements included") + " at " + governing.Label));
            trace.Add(new CheckCalculationValue("Vibration.Deflection.Location", governing.Distance, "mm", governing.Label));
            trace.Add(new CheckCalculationValue("Vibration.Frequency", frequency, "Hz", "f = K / sqrt(delta[mm])"));
            return new List<SteelDeformationCheck> { SteelMemberDeformation.Frequency(frequency, minimum, governing.Distance, governing.Label, Clause(spec.Criterion), source) };
        }

        private CheckResult Decision(IReadOnlyList<SteelDeformationCheck> checks, MemberDeformationSnapshot deformation, List<CheckCalculationValue> trace)
        {
            var metrics = checks.Select(c => new CheckMetric(c.Key, c.Demand, c.Capacity, c.Unit, c.Utilization, c.Passed, c.Clause + "; limit: " + c.Source)).ToArray();
            var points = checks.Where(c => c.Location.HasValue).Select(c => new CheckPointValue(c.Key, 0, 0, c.Location.Value,
                c.Key == "Vibration.Frequency" ? c.Capacity : c.Demand, c.Unit, "PhysicalMember: x3 = distance along the member, " + c.LocationLabel)).ToArray();
            foreach (var c in checks) trace.Add(new CheckCalculationValue(c.Key + ".Utilization", c.Utilization, "1", c.Key == "Vibration.Frequency" ? "f_min / f" : "demand / limit"));
            var details = new ServiceabilityCheckDetails(Id, "member-deformation:demand/limit (frequency: f_min/f)", deformation.Category.ToString(), metrics, trace, points);
            var result = new CheckResult { Details = details, Execution = ExecutionStatus.Completed, Data = DataStatus.Ready, Mechanism = CheckMechanism.Serviceability,
                Utilization = checks.Max(c => c.Utilization) };
            result.Outcome = checks.All(c => c.Passed) ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
            return result;
        }
    }
}
