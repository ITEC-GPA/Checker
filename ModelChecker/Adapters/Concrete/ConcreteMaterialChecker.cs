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
        public bool Accepts(BeamElement element) => element.BeamProperty is ReinforcedConcreteSection rc && rc.SteelSections.Count == 0;
        public IMaterialCheckSession CreateSession() => new Session(CreateVerifier());
        private sealed class Session : ISectionCheckSession
        {
            private readonly ConcreteSectionVerifier _verifier;
            internal Session(ConcreteSectionVerifier verifier) { _verifier = verifier; }
            public int CreatedCheckers => _verifier.CreatedCheckers;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
                => SectionCheckExecution.Run(BeamCheckPreparation.Prepare(input.Model, input.Element.Id, input.Sample, input.Settings), mechanism, _verifier, token);
            public bool Supports(SectionCheckSpecification check) => _verifier.Supports(check);
            public CheckResult Verify(BeamActionInput input, SectionCheckSpecification check, CancellationToken token)
                => SectionCheckExecution.Run(BeamCheckPreparation.Prepare(input.Model, input.Element.Id, input.Sample, input.Settings), check, _verifier, token);
        }
    }
}
