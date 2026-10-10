using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Checker.Configuration;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Sections.Concrete;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;

namespace GPC.Model.Checker
{
    /// <summary>Explicit specialist data for one FEM station/state. History is supplied, never inferred from a terminal force vector.</summary>
    public sealed class CompositeBridgeCase
    {
        public int BeamId { get; set; }
        public double Station { get; set; }
        public string StationDomain { get; set; }
        public SectionSide Side { get; set; }
        public ResultSelection State { get; set; }
        public HBridgeInput Input { get; set; }
        public CoordinateSystem SectionAxes { get; set; }
        /// <summary>Native y-coordinate of the point to which Model's prepared moments refer.</summary>
        public double? ActionReferenceY { get; set; }
        public bool HistoryAndReferenceConfirmed { get; set; }
        public string Source { get; set; }
    }
    public sealed class CompositeBridgeMaterialChecker : IMaterialChecker
    {
        public CompositeBridgeCase[] Cases { get; }
        public BridgeStandard Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        private readonly BridgeHistoryDefinition _history;
        public BridgeHistoryDefinition History => _history == null ? null : ConfigurationArchive.CopyData(_history);
        public string Id => _history == null ? "CompositeBridge.Section" : "CompositeBridge.HistoryLinear";
        public string Version => NativeResults.Version(typeof(HBridgeSection));
        public string Configuration => ModelValues.Fingerprint(_history == null
            ? new object[] { Id, Code, Edition, NationalAnnex, Cases }
            : new object[] { Id, Code, Edition, NationalAnnex, Cases, _history });
        public CheckStandardContext Standard => new CheckStandardContext(Code.ToString(), Edition, NationalAnnex, Id, Configuration);
        public CompositeBridgeMaterialChecker(BridgeStandard code, string edition, IEnumerable<CompositeBridgeCase> cases, string nationalAnnex = null)
            : this(code, edition, cases, nationalAnnex, null) { }
        public CompositeBridgeMaterialChecker(BridgeStandard code, string edition, IEnumerable<CompositeBridgeCase> cases,
            string nationalAnnex, BridgeHistoryDefinition history)
        {
            _history = history == null ? null : ConfigurationArchive.CopyData(history);
            _history?.CreateOptions();
            if (!Enum.IsDefined(typeof(BridgeStandard), code) || string.IsNullOrWhiteSpace(edition)) throw new ArgumentException("ExplicitBridgeStandardRequired");
            Code = code; Edition = edition; NationalAnnex = nationalAnnex;
            Cases = (cases ?? throw new ArgumentNullException(nameof(cases))).ToArray();
            if (Cases.Any(c => c == null)) throw new ArgumentException("NullBridgeCase");
        }
        public bool Accepts(BeamElement element) => element.BeamProperty is ReinforcedConcreteSection rc && rc.SteelSections.Count > 0;
        public IMaterialCheckSession CreateSession() => new Session(this);
        private sealed class Session : IMaterialCheckSession
        {
            private readonly CompositeBridgeMaterialChecker _owner;
            private readonly Dictionary<string, HBridgeAnalysisResult> _analyses = new Dictionary<string, HBridgeAnalysisResult>();
            public int CreatedCheckers => _analyses.Count;
            internal Session(CompositeBridgeMaterialChecker owner) { _owner = owner; }
            private static bool Close(double a, double b) => Math.Abs(a - b) <= 1e-8 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
            private static bool Aligned(Vector3d a, Vector3d b) => Close(a.X, b.X) && Close(a.Y, b.Y) && Close(a.Z, b.Z);
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (_owner.Edition != (_owner.Code == BridgeStandard.Ntc2018 ? "2018" : "2005")) return NativeResults.Unavailable("BridgeEditionNotQualified");
                if (_owner._history != null && mechanism != CheckMechanism.Serviceability)
                    return NativeResults.Unavailable("BridgeHistoryMethodNotQualified", "Linear construction history is connected only to SLE stress limits; no shear or SLU capacity is inferred.");
                if (mechanism != CheckMechanism.Serviceability && mechanism != CheckMechanism.Shear)
                    return NativeResults.Unavailable("BridgeMethodNotConnected", "Connected methods: SLE point stress limits and SLU web shear. No full bridge or member certificate.");
                var matches = _owner.Cases.Where(c => c.BeamId == input.Element.Id && c.StationDomain == input.Sample.StationDomain
                    && c.Station == input.Sample.ParametricDistance && c.Side == input.Sample.Side && c.State != null
                    && !string.IsNullOrWhiteSpace(c.State.ConcomitantState)
                    && ResultQueries.Samples<global::GPC.Model.Results.Locations.StationResultBeamForces>(input.Element, c.State).Contains(input.Sample)).ToArray();
                if (matches.Length != 1) return NativeResults.Missing("MissingOrAmbiguousBridgeHistory");
                var binding = matches[0]; var data = binding.Input;
                if (data == null || !binding.HistoryAndReferenceConfirmed || string.IsNullOrWhiteSpace(binding.Source) || !binding.ActionReferenceY.HasValue || binding.SectionAxes == null)
                    return NativeResults.Missing("ExplicitBridgeHistoryAndReferenceRequired");
                if (data.Options.Standard != _owner.Code) return NativeResults.Missing("BridgeStandardMismatch");
                if (data.Geometry.SectionType != BridgeSteelSectionType.H) return NativeResults.Unavailable("BridgeShapeNotConnected", "Initial FEM binding supports H sections; native inclined-H/box methods require further mapping validation.");
                if (data.Phases == null || data.Phases.Length == 0 || data.Phases.Any(p => p == null)) return NativeResults.Missing("MissingBridgePhases");
                var phases = data.Phases.Where(p => p.Active).ToArray();
                if (phases.Length == 0 || phases.Any(p => p.Reference != BridgeLoadReference.CommonElevation)
                    || !Close(data.Options.CommonLoadY, binding.ActionReferenceY.Value)) return NativeResults.Missing("BridgeLoadReferenceMismatch");
                Axes.Validate(binding.SectionAxes); var frame = input.Forces.CoordinateSystem;
                if (!Aligned(binding.SectionAxes.V1, frame.V1) || !Aligned(binding.SectionAxes.V2, frame.V2) || !Aligned(binding.SectionAxes.V3, frame.V3))
                    return NativeResults.Missing("BridgeSectionAxesMismatch");
                if (input.Forces.M2 != 0 || input.Forces.V1 != 0 || input.Forces.T != 0 || phases.Any(p => p.TorsionKNm != 0))
                    return NativeResults.Unavailable("BridgeRequiresNM1V2");
                var loads = phases.Where(p => p.Kind != BridgePhaseKind.Shrinkage).ToArray();
                if (!Close(loads.Sum(p => p.ForceKN) * 1000, input.Forces.N) || !Close(loads.Sum(p => p.MomentKNm) * 1e6, input.Forces.M1)
                    || !Close(loads.Sum(p => p.ShearKN) * 1000, input.Forces.V2)) return NativeResults.Missing("BridgeHistoryDoesNotMatchFemState");
                if (input.Element.Assignments.Sections.Count != 0) return NativeResults.Unavailable("BridgeVariableSectionBindingNotConnected");
                var expected = HBridgeSection.NativeSection(data);
                if (ModelValues.Fingerprint(new object[] { expected }) != ModelValues.Fingerprint(new object[] { input.Property }))
                    return NativeResults.Missing("BridgeSectionDoesNotMatchModel");
                if (mechanism == CheckMechanism.Serviceability && data.Options.LimitState == BridgeLimitState.Ultimate)
                    return NativeResults.Missing("BridgeSleCategoryRequired");
                if (mechanism == CheckMechanism.Shear && data.Options.LimitState != BridgeLimitState.Ultimate)
                    return NativeResults.Missing("BridgeUlsCategoryRequired");
                string fingerprint = ModelValues.Fingerprint(new object[] { data, _owner._history });
                if (!_analyses.TryGetValue(fingerprint, out var analysis))
                {
                    try { analysis = _owner._history == null ? HBridgeSection.Calculate(data, token)
                        : HBridgeHistoryResults.Calculate(data, _owner._history.CreateOptions(), token); }
                    catch (HistoryConvergenceException ex)
                    {
                        var rejected = NativeResults.Missing("BridgeHistoryNotConverged"); rejected.Execution = ExecutionStatus.Error;
                        rejected.Diagnostics.Add(ModelDiagnostic.Error("BridgeHistoryFailureLocation", message: "Phase " + ex.PhaseIndex + ": " + ex.PhaseName + "; substep " + ex.Substep + "; " + ex.Message));
                        return rejected;
                    }
                    _analyses.Add(fingerprint, analysis);
                }
                var stage = analysis.Stages.Last();
                var metrics = new List<CheckMetric>(); var points = new List<CheckPointValue>();
                if (mechanism == CheckMechanism.Serviceability)
                {
                    int index = 0;
                    foreach (var p in stage.Points.Where(p => p.Active))
                    {
                        string key = p.Material + ":" + p.Name + (_owner._history == null ? "" : "@" + index++);
                        metrics.Add(new CheckMetric(key, p.Stress, p.Limit, "MPa", p.Utilization));
                        points.Add(new CheckPointValue(key, 0, p.Y, 0, p.Stress, "MPa", "Native bridge section: y=0 at steel top; orientation in input snapshot"));
                    }
                }
                else
                {
                    // Only the named web resistance check, not every accessory check nor MaxUtilization of normal stresses.
                    var shear = stage.Shear?.Checks.FirstOrDefault();
                    if (shear == null) return NativeResults.Missing("MissingBridgeWebShearOutcome");
                    metrics.Add(new CheckMetric("WebShear", shear.Demand, shear.Resistance, shear.Unit, shear.Ratio, reference: shear.Note));
                }
                string method = _owner.Id + (mechanism == CheckMechanism.Shear ? ".WebShear" : ".SleStress");
                var warnings = analysis.Stages.SelectMany(s => s.Warnings).Distinct().Concat(new[] { analysis.Scope,
                    "Only the selected final-state check is included. Intermediate situations, connectors, details, fatigue and global stability are separate required checks." });
                var numericalTrace = new List<CheckCalculationValue>();
                var convergence = _owner._history == null
                    ? new CheckConvergence(analysis.Stages.All(s => s.Residual <= 1e-7), analysis.Stages.Sum(s => s.Iterations), analysis.Stages.Max(s => s.Residual), 1e-7, "Native effective-width iteration")
                    : HistoryConvergence(analysis, _owner._history, numericalTrace);
                var details = new NativeMethodDetails(method, mechanism == CheckMechanism.Shear ? "Final-state web shear resistance only" : "Final-state active material point stress limits only",
                    fingerprint, metrics, new[] { new CheckCalculationValue("ActivePhases", phases.Length, "1", binding.Source),
                        new CheckCalculationValue("CommonLoadY", data.Options.CommonLoadY, "mm", "Explicit reference of prepared FEM moments"),
                        new CheckCalculationValue("GammaM0", data.Options.GammaM0, "1", "Native options"),
                        new CheckCalculationValue("GammaM1", data.Options.GammaM1, "1", "Native options"),
                        new CheckCalculationValue("GammaC", data.Options.GammaC, "1", "Native options"),
                        new CheckCalculationValue("GammaS", data.Options.GammaS, "1", "Native options"),
                        new CheckCalculationValue("AlphaCC", data.Options.AlphaCC, "1", "Native options") }.Concat(numericalTrace), warnings, points, convergence);
                if (!convergence.Converged)
                {
                    var rejected = NativeResults.Missing("BridgeNumericalConvergenceRejected"); rejected.Details = details; return rejected;
                }
                return NativeResults.Decision(details);
            }
            private static CheckConvergence HistoryConvergence(HBridgeAnalysisResult result, BridgeHistoryDefinition options, List<CheckCalculationValue> trace)
            {
                double maxRatio = 0; int iterations = 0;
                foreach (var stage in result.Stages)
                {
                    var state = stage.GetHistory().State;
                    double length = Math.Max(1, state.Fibers.Max(f => f.Fiber.Y) - state.Fibers.Min(f => f.Fiber.Y));
                    double ft = options.ForceTolerance + options.RelativeTolerance * Math.Max(Math.Abs(state.N), Math.Abs(state.MomentAtOrigin) / length);
                    double mt = options.MomentTolerance + options.RelativeTolerance * Math.Max(Math.Abs(state.MomentAtOrigin), Math.Abs(state.N) * length);
                    double ratio = Math.Max(Math.Abs(state.ForceResidual) / ft, Math.Max(Math.Abs(state.MomentResidual) / mt, Math.Abs(state.EffectiveResidual) / options.EffectiveTolerance));
                    maxRatio = Math.Max(maxRatio, ratio); iterations += state.NewtonIterations;
                    string suffix = "@" + state.Index;
                    trace.Add(new CheckCalculationValue("ForceResidual" + suffix, state.ForceResidual, "N", state.Name));
                    trace.Add(new CheckCalculationValue("ForceTolerance" + suffix, ft, "N", "Native combined absolute/relative tolerance"));
                    trace.Add(new CheckCalculationValue("MomentResidual" + suffix, state.MomentResidual, "Nmm", state.Name));
                    trace.Add(new CheckCalculationValue("MomentTolerance" + suffix, mt, "Nmm", "Native combined absolute/relative tolerance"));
                    trace.Add(new CheckCalculationValue("EffectiveResidual" + suffix, state.EffectiveResidual, "1", state.Name));
                    trace.Add(new CheckCalculationValue("EffectiveTolerance" + suffix, options.EffectiveTolerance, "1", "Native effective area tolerance"));
                }
                return new CheckConvergence(maxRatio <= 1, iterations, maxRatio, 1, "Maximum normalized N/M/effective-area residual over accepted history phases");
            }
        }
    }
}
