using GPC.Model.Checking;
using System;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Sections.Concrete;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Execution;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    /// <summary>Routes ordinary RC to the existing section adapter; step 4 capabilities are not implied.</summary>
    public sealed class ConcreteMaterialChecker : IMaterialChecker
    {
        public ConcreteVerificationOptions Options { get; }
        private readonly IConcreteCalculationFactory _calculationFactory;
        public string Id => "Concrete.Section";
        public string Version => NativeResults.Version(typeof(GPC.Checkers.Concrete.Checkers.SectionChecker));
        public string Configuration => CreateVerifier().Configuration;
        public CheckStandardContext Standard => CreateVerifier().StandardContext;
        public ConcreteMaterialChecker(ConcreteVerificationOptions options) : this(options, new GPC.Model.Checker.Configuration.ConcreteCalculationCatalog().Resolve(options)) { }
        public ConcreteMaterialChecker(ConcreteVerificationOptions options, IConcreteCalculationFactory calculationFactory)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _calculationFactory = calculationFactory ?? throw new ArgumentNullException(nameof(calculationFactory));
            GPC.Model.Checker.Configuration.ConcreteCalculationCatalog.ValidateSelection(options, calculationFactory);
        }
        internal ConcreteSectionVerifier CreateVerifier() => Options.CreateVerifier(_calculationFactory);
        /// <summary>The verifiers of this checker read only their input (ConcreteSectionVerifier.ReadsOnlyItsInput), and so do its members: the
        /// built-in numerical engine and a standard of GPCModel.</summary>
        internal bool ReadsOnlyItsInput => GPC.Model.Checker.Configuration.ConcreteCalculationCatalog.IsBuiltIn(_calculationFactory)
            && Options.Standard?.GetType().Assembly == typeof(GPC.Model.Standards.StandardModelCode2010).Assembly;
        public bool Accepts(BeamElement element) => element.BeamProperty is ReinforcedConcreteSection rc && rc.SteelSections.Count == 0;
        public IMaterialCheckSession CreateSession() => new Session(CreateVerifier());
        internal sealed class Session : ISectionCheckSession
        {
            private readonly ConcreteSectionVerifier _verifier;
            internal Session(ConcreteSectionVerifier verifier) { _verifier = verifier; }
            /// <summary>The reads of the run of the orchestrator (ModelChecker.VerifyMaterialPlans), which ends them after any code that may change
            /// the model; null (a session used on its own): every task prepares its cut and reads the model again.</summary>
            internal SectionCheckRun Reads;
            public int CreatedCheckers => _verifier.CreatedCheckers;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
                => Reads != null && ReferenceEquals(Reads.Model, input.Model)
                    ? Reads.Run(Reads.Prepare(input.Element.Id, input.Sample, input.Settings), mechanism, _verifier, _verifier.ReadsOnlyItsInput, token)
                    : SectionCheckExecution.Run(BeamCheckPreparation.Prepare(input.Model, input.Element.Id, input.Sample, input.Settings), mechanism, _verifier, token);
            public bool Supports(SectionCheckSpecification check) => _verifier.Supports(check);
            public CheckResult Verify(BeamActionInput input, SectionCheckSpecification check, CancellationToken token)
                => Reads != null && ReferenceEquals(Reads.Model, input.Model)
                    ? Reads.Run(Reads.Prepare(input.Element.Id, input.Sample, input.Settings), check, _verifier, _verifier.ReadsOnlyItsInput, token)
                    : SectionCheckExecution.Run(BeamCheckPreparation.Prepare(input.Model, input.Element.Id, input.Sample, input.Settings), check, _verifier, token);
        }
    }
}
