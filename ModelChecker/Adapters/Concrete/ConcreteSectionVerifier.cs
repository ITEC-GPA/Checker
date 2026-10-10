using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Globalization;
using System.Runtime.Serialization;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Core;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>Optional headless adapter to the installed Checker. No FEM solving or resistance formula is implemented here.</summary>
    public sealed partial class ConcreteSectionVerifier : ISectionCheckVerifier
    {
        private static readonly object Sync = new object();
        private readonly StandardModelCode2010 _standard;
        private readonly SectionSolver.FailureAnalysisTypes _criterion;
        private readonly bool _considerTension;
        private readonly int _angularDivisions;
        private readonly double _psiRebar, _psiTendon;
        private readonly string _edition, _nationalAnnex;
        private readonly SectionSolver.StressAnalysisTypes _serviceabilityAnalysis;
        private readonly double _concreteStressLimitFactor;
        private CheckStandardContext _standardSnapshot;
        private readonly IConcreteCalculationFactory _calculationFactory;
        private readonly Dictionary<string, Tuple<ReinforcedConcreteSection, ConcreteCalculationSession>> _checkers = new Dictionary<string, Tuple<ReinforcedConcreteSection, ConcreteCalculationSession>>();
        private int _createdCheckers;
        public int CreatedCheckers { get { lock (Sync) return _createdCheckers; } }
        public string Version => typeof(SectionCheckerModelCode2010).Assembly.GetName().Version.ToString();
        public IReadOnlyCollection<CheckMechanism> Capabilities { get; } = Array.AsReadOnly(new[] {CheckMechanism.UlsBiaxialSection});
        public ConcreteSectionVerifier(StandardModelCode2010 standard, SectionSolver.FailureAnalysisTypes criterion, bool considerTension,
            int angularDivisions, double psiRebar, double psiTendon)
            : this(standard, criterion, considerTension, angularDivisions, psiRebar, psiTendon, null, null) { }

        public ConcreteSectionVerifier(StandardModelCode2010 standard, SectionSolver.FailureAnalysisTypes criterion, bool considerTension,
            int angularDivisions, double psiRebar, double psiTendon, string standardEdition, string nationalAnnex)
            : this(standard, criterion, considerTension, angularDivisions, psiRebar, psiTendon, standardEdition, nationalAnnex,
                SectionSolver.StressAnalysisTypes.NonLinear, 1) { }

        /// <param name="serviceabilityAnalysis">Stress analysis used for the serviceability stress limits.</param>
        /// <param name="concreteStressLimitFactor">Explicit factor on the concrete stress limits (1 = none), for example 0.8 for thin castings.</param>
        /// <param name="shearCotTheta">Assigned cot θ for the shear checks; null lets each method choose it within its range.</param>
        /// <param name="crackLoadDuration">Load duration of the crack checks.</param>
        /// <param name="crackDesignLimit">Design wlim of the crack checks, mm, where the standard admits it; null = limit of the standard.</param>
        public ConcreteSectionVerifier(StandardModelCode2010 standard, SectionSolver.FailureAnalysisTypes criterion, bool considerTension,
            int angularDivisions, double psiRebar, double psiTendon, string standardEdition, string nationalAnnex,
            SectionSolver.StressAnalysisTypes serviceabilityAnalysis, double concreteStressLimitFactor, double? shearCotTheta = null,
            CrackLoadDuration crackLoadDuration = CrackLoadDuration.LongTerm, double? crackDesignLimit = null)
        {
            if (shearCotTheta.HasValue && (double.IsNaN(shearCotTheta.Value) || double.IsInfinity(shearCotTheta.Value) || shearCotTheta <= 0))
                throw new ArgumentOutOfRangeException(nameof(shearCotTheta));
            if (!Enum.IsDefined(typeof(CrackLoadDuration), crackLoadDuration)) throw new ArgumentOutOfRangeException(nameof(crackLoadDuration));
            if (crackDesignLimit.HasValue && (double.IsNaN(crackDesignLimit.Value) || double.IsInfinity(crackDesignLimit.Value) || crackDesignLimit <= 0))
                throw new ArgumentOutOfRangeException(nameof(crackDesignLimit));
            _calculationFactory = new LegacyConcreteCalculationFactory();
            _shearCotTheta = shearCotTheta; _crackLoadDuration = crackLoadDuration; _crackDesignLimit = crackDesignLimit;
            _standard=standard ?? throw new ArgumentNullException(nameof(standard));_criterion=criterion;_considerTension=considerTension;
            _edition = standardEdition ?? DeclaredEdition(standard.GetType()); _nationalAnnex = nationalAnnex;
            if (!Enum.IsDefined(typeof(SectionSolver.FailureAnalysisTypes), criterion)) throw new ArgumentOutOfRangeException(nameof(criterion));
            if (!Enum.IsDefined(typeof(SectionSolver.StressAnalysisTypes), serviceabilityAnalysis)) throw new ArgumentOutOfRangeException(nameof(serviceabilityAnalysis));
            if (double.IsNaN(concreteStressLimitFactor) || concreteStressLimitFactor <= 0 || concreteStressLimitFactor > 1) throw new ArgumentOutOfRangeException(nameof(concreteStressLimitFactor));
            if(angularDivisions<4) throw new ArgumentOutOfRangeException(nameof(angularDivisions));
            if(double.IsNaN(psiRebar)||double.IsInfinity(psiRebar)||double.IsNaN(psiTendon)||double.IsInfinity(psiTendon)) throw new ArgumentException("Finite psi values required.");
            _angularDivisions=angularDivisions;_psiRebar=psiRebar;_psiTendon=psiTendon;
            _serviceabilityAnalysis = serviceabilityAnalysis; _concreteStressLimitFactor = concreteStressLimitFactor;
        }

        /// <summary>Uses the same verification methods with an explicitly supplied numerical implementation.</summary>
        public ConcreteSectionVerifier(IConcreteCalculationFactory calculationFactory, StandardModelCode2010 standard,
            SectionSolver.FailureAnalysisTypes criterion, bool considerTension, int angularDivisions, double psiRebar, double psiTendon,
            string standardEdition, string nationalAnnex, SectionSolver.StressAnalysisTypes serviceabilityAnalysis,
            double concreteStressLimitFactor, double? shearCotTheta = null, CrackLoadDuration crackLoadDuration = CrackLoadDuration.LongTerm,
            double? crackDesignLimit = null)
            : this(standard, criterion, considerTension, angularDivisions, psiRebar, psiTendon, standardEdition, nationalAnnex,
                  serviceabilityAnalysis, concreteStressLimitFactor, shearCotTheta, crackLoadDuration, crackDesignLimit)
        {
            _calculationFactory = calculationFactory ?? throw new ArgumentNullException(nameof(calculationFactory));
            if (string.IsNullOrWhiteSpace(calculationFactory.Id) || string.IsNullOrWhiteSpace(calculationFactory.Version))
                throw new ArgumentException("Numerical engine identity/version required.", nameof(calculationFactory));
        }

        // Only editions explicitly documented by these concrete source types are inferred. Subclasses may differ.
        private static string DeclaredEdition(Type type) => type == typeof(StandardNTC2018Concrete) ? "2018"
            : type == typeof(StandardEN1992p11) ? "2004/AC:2010" : type == typeof(StandardModelCode2010) ? "2010" : null;
        public CheckStandardContext StandardContext
        {
            get
            {
                lock (Sync)
                {
                    var configuration = Configuration;
                    if (_standardSnapshot == null || _standardSnapshot.Parameters != configuration)
                        _standardSnapshot = new CheckStandardContext(_standard.Name, _edition, _nationalAnnex, _standard.GetType().FullName, configuration);
                    return _standardSnapshot;
                }
            }
        }

        public string Configuration
        {
            get
            {
                var info=new SerializationInfo(_standard.GetType(),new FormatterConverter());
                _standard.GetObjectData(info,new StreamingContext());
                var entries=new SortedDictionary<string,string>(StringComparer.Ordinal);
                foreach(SerializationEntry entry in info)
                    if(entry.Value is double || entry.Value is int || entry.Value is bool || entry.Value is string)
                        entries[entry.Name]=Convert.ToString(entry.Value,CultureInfo.InvariantCulture);
                entries["StandardType"]=_standard.GetType().FullName;entries["Criterion"]=_criterion.ToString();
                entries["ConsiderTensileConcrete"]=_considerTension.ToString();entries["AngularDivisions"]=_angularDivisions.ToString(CultureInfo.InvariantCulture);
                entries["PsiRebar"]=_psiRebar.ToString("R",CultureInfo.InvariantCulture);entries["PsiTendon"]=_psiTendon.ToString("R",CultureInfo.InvariantCulture);
                entries["VerificationImplementationVersion"] = Version;
                entries["NumericalEngine"] = _calculationFactory.Id; entries["NumericalEngineVersion"] = _calculationFactory.Version;
                entries["NumericalOptions"] = _calculationFactory.Configuration;
                entries["FailureDomain"]="Plastic";entries["StressAnalysis"]="NonLinear";entries["WorkingRatioForceScaleN"]="1000000";entries["WorkingRatioLengthScaleMm"]="1000";
                entries["StandardEdition"] = _edition ?? "undeclared"; entries["NationalAnnex"] = _nationalAnnex ?? "undeclared";
                entries["ServiceabilityStressAnalysis"] = _serviceabilityAnalysis.ToString();
                entries["ServiceabilityConcreteLimitFactor"] = _concreteStressLimitFactor.ToString("R", CultureInfo.InvariantCulture);
                AddShearConfiguration(entries);
                AddTorsionConfiguration(entries);
                AddCrackConfiguration(entries);
                return string.Join("\n",entries.Select(p=>p.Key+"="+p.Value));
            }
        }

        public CheckResult Verify(BeamCheckInput input, CheckMechanism mechanism, CancellationToken cancellationToken)
        {
            if(mechanism!=CheckMechanism.UlsBiaxialSection) return new CheckResult {Data=DataStatus.NotSupported,Outcome=EngineeringOutcome.NotEvaluated};
            cancellationToken.ThrowIfCancellationRequested();
            var forces = SectionForces(input, out var reference);
            lock(Sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = Checker(input.Section, reference, SectionSolver.StressAnalysisTypes.NonLinear);
                var point=entry.Item2.Resistance.SolveResistance(new SectionAnalysisInput(forces), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (point == null) return Failure("CheckerDomainPointMissing");
                if (point.Diagnostics.Status != CalculationStatus.Completed)
                    return WithNumericalDiagnostics(Failure(point.Diagnostics.AxialEquilibrium?.Accepted == false
                        ? "CheckerAxialEquilibriumRejected" : point.Diagnostics.ResistanceConvergence?.Accepted == false
                            ? "CheckerSearchConstraintsRejected" : "CheckerCalculationIncomplete"), point.Diagnostics);
                if (!MatchesEngine(point.Diagnostics) || point.Criterion != _criterion.ToString()) return Failure("NumericalResistanceContractMismatch");
                if (point.IsZeroDemand)
                {
                    if (!SameForces(point.ZeroDemandInput.Forces, forces) || forces.N != 0 || forces.M1 != 0 || forces.M2 != 0
                        || input.Section.Rebars.Any(r => r.EpsilonP != 0)) return Failure("NumericalZeroDemandContractMismatch");
                    var zero = NativeResults.Decision(new NativeMethodDetails("Concrete.PlasticSectionDomain",
                        "Zero N-M1-M2 demand without initial rebar strain; no resistant point, shear/torsion excluded.",
                        ModelValues.Fingerprint(new object[] { input.Section }), new[] { new CheckMetric("N-M1-M2", 0, null, "1", 0, true) }, null, null, null, null, _criterion.ToString()));
                    zero.EngineVersion = Version; zero.Standard = StandardContext;
                    return WithEdition(WithNumericalDiagnostics(zero, point.Diagnostics));
                }
                var convergence = point.Diagnostics.ResistanceConvergence;
                if (convergence == null || !convergence.Accepted || convergence.Criterion != _criterion
                    || !SameForces(convergence.Demand.Forces, forces)
                    || convergence.Capacity.Forces.N != point.N || convergence.Capacity.Forces.M1 != point.M1
                    || convergence.Capacity.Forces.M2 != point.M2
                    || ModelValues.Fingerprint(new object[] { convergence.Capacity.Forces.CoordinateSystem })
                        != ModelValues.Fingerprint(new object[] { forces.CoordinateSystem }))
                    return Failure("NumericalResistanceConvergenceContractMismatch");
                double ratio=point.Utilization.Value;
                if(double.IsNaN(ratio)||double.IsInfinity(ratio)||ratio<0) return Failure("CheckerInvalidRatio");
                var strain = point.Strain;
                if (strain == null) return Failure("CheckerStrainPlaneMissing");
                var result = new CheckResult {Execution=ExecutionStatus.Completed,Data=DataStatus.Ready,
                    Outcome=ratio<=1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,Utilization=ratio,EngineVersion=Version,
                    Standard = StandardContext,
                    Details = new SectionResistanceDetails("Concrete.PlasticSectionDomain", _criterion.ToString(), new BeamForceSnapshot(forces),
                        point.N.Value, point.M1.Value, point.M2.Value, ratio, point.FailureMode,
                        strain.X, strain.Y, strain.Epsilon, strain.ChiX, strain.ChiY) };
                return WithEdition(WithNumericalDiagnostics(result, point.Diagnostics));
            }
        }
        private static CheckResult WithNumericalDiagnostics(CheckResult result, SolverDiagnostics diagnostics)
        {
            var evidence = diagnostics.AxialEquilibrium;
            result.Diagnostics.Add(new ModelDiagnostic {
                Code = evidence == null ? "NumericalCalculationStatus" : "NumericalAxialEquilibrium",
                Severity = diagnostics.Status == CalculationStatus.Completed ? DiagnosticSeverity.Information : DiagnosticSeverity.Error,
                Message = evidence == null ? diagnostics.Status + ": " + diagnostics.Message
                    : string.Format(CultureInfo.InvariantCulture, "{0}: Nrequested={1:R} N; Nactual={2:R} N; residual={3:R} N; tolerance={4:R} N; accepted={5}",
                        diagnostics.Status, evidence.Requested, evidence.Actual, evidence.Residual, evidence.Tolerance, evidence.Accepted)
            });
            if (diagnostics.ResistanceConvergence != null)
                foreach (var residual in diagnostics.ResistanceConvergence.Residuals)
                    result.Diagnostics.Add(new ModelDiagnostic {
                        Code = "NumericalSearchConstraint." + residual.Quantity,
                        Severity = residual.Accepted ? DiagnosticSeverity.Information : DiagnosticSeverity.Error,
                        Message = string.Format(CultureInfo.InvariantCulture, "{0}: residual={1:R} {2}; tolerance={3:R} {2}; accepted={4}",
                            diagnostics.ResistanceConvergence.Criterion, residual.Residual, residual.Unit, residual.Tolerance, residual.Accepted)
                    });
            return result;
        }
        private static bool SameForces(ResultBeamForces actual, ResultBeamForces expected) =>
            actual.N == expected.N && actual.V1 == expected.V1 && actual.V2 == expected.V2 && actual.T == expected.T
                && actual.M1 == expected.M1 && actual.M2 == expected.M2
                && ModelValues.Fingerprint(new object[] { actual.CoordinateSystem }) == ModelValues.Fingerprint(new object[] { expected.CoordinateSystem });
        private bool MatchesEngine(SolverDiagnostics diagnostics) => diagnostics != null && diagnostics.Engine == _calculationFactory.Id
            && diagnostics.Version == _calculationFactory.Version;
        private bool MatchesResponse(SectionResponse response, ResultBeamForces forces, SectionSolver.StressAnalysisTypes analysis)
        {
            if (response == null || !MatchesEngine(response.Diagnostics) || response.Linear != (analysis == SectionSolver.StressAnalysisTypes.Linear)
                || response.Linear && (response.PsiRebar != _psiRebar || response.PsiTendon != _psiTendon)) return false;
            var actual = response.Input.Forces;
            return actual.N == forces.N && actual.V1 == forces.V1 && actual.V2 == forces.V2 && actual.T == forces.T
                && actual.M1 == forces.M1 && actual.M2 == forces.M2
                && ModelValues.Fingerprint(new object[] { actual.CoordinateSystem }) == ModelValues.Fingerprint(new object[] { forces.CoordinateSystem });
        }
        private CheckResult WithEdition(CheckResult result)
        {
            if (!result.Standard.HasDeclaredEdition) result.Diagnostics.Add(new ModelDiagnostic { Code = "StandardEditionUndeclared",
                Severity = DiagnosticSeverity.Warning, Message = "Specify the edition for this standard implementation before normative reporting." });
            return result;
        }
        private static CheckResult Failure(string code) => new CheckResult {Execution=ExecutionStatus.Error,Data=DataStatus.Insufficient,
            Outcome=EngineeringOutcome.NotEvaluated,Diagnostics=new List<ModelDiagnostic>{ModelDiagnostic.Error(code)}};

        // Section geometry is expressed in its 2D coordinates; input already contains components in those section axes.
        // The section force reduction point must be explicitly the section centroid for this adapter.
        private static ResultBeamForces SectionForces(BeamCheckInput input, out CoordinateSystem reference)
        {
            reference = new CoordinateSystem(input.Section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var f = input.Forces;
            return new ResultBeamForces(f.N, f.V1, f.V2, f.T, f.M1, f.M2, reference);
        }

        /// <summary>Cached native checker per section, configuration and stress analysis type. Call under <see cref="Sync"/>.</summary>
        private Tuple<ReinforcedConcreteSection, ConcreteCalculationSession> Checker(ReinforcedConcreteSection section, CoordinateSystem reference, SectionSolver.StressAnalysisTypes stress)
        {
            var key = ModelValues.Fingerprint(new object[] { section, Configuration, stress });
            if (!_checkers.TryGetValue(key, out var entry) || ModelValues.Fingerprint(new object[] { entry.Item1, Configuration, stress }) != key)
            {
                var created = _calculationFactory.Create(ModelValues.CopyValue(section), ModelValues.CopyValue(_standard), new ConcreteCalculationOptions(reference,
                    _criterion, stress, _considerTension, _angularDivisions, _psiRebar, _psiTendon))
                    ?? throw new InvalidOperationException("NullNumericalSession");
                entry = Tuple.Create(section, created); _checkers[key] = entry; _createdCheckers++;
            }
            return entry;
        }

        partial void AddShearConfiguration(SortedDictionary<string, string> entries);
        partial void AddTorsionConfiguration(SortedDictionary<string, string> entries);
        partial void AddCrackConfiguration(SortedDictionary<string, string> entries);
    }
}
