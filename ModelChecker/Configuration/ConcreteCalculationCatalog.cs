using System;
using System.Collections.Generic;
using System.Threading;
using GPC.Checkers.Concrete.Analysis;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Model.Checker.Configuration
{
    /// <summary>Runtime registry. Saved configurations contain identities, never CLR names or executable factories.</summary>
    public sealed class ConcreteCalculationCatalog
    {
        private readonly Dictionary<string, IConcreteCalculationFactory> _factories = new Dictionary<string, IConcreteCalculationFactory>(StringComparer.Ordinal);
        public ConcreteCalculationCatalog() { Register(new LegacyConcreteCalculationFactory()); }
        public void Register(IConcreteCalculationFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(factory.Id) || string.IsNullOrWhiteSpace(factory.Version) || factory.Configuration == null)
                throw new ArgumentException("IncompleteNumericalEngineIdentity");
            _factories.Add(factory.Id, new BoundFactory(factory));
        }
        public IConcreteCalculationFactory Resolve(ConcreteVerificationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var id = options.CalculationEngineId;
            if (id == null)
            {
                if (options.CalculationEngineVersion != null || options.CalculationEngineConfiguration != null)
                    throw new ArgumentException("NumericalEngineIdRequired");
                id = GPC.Checkers.Concrete.Analysis.LegacySectionCalculation.EngineId;
            }
            else if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(options.CalculationEngineVersion)
                || options.CalculationEngineConfiguration == null) throw new ArgumentException("PinnedNumericalEngineRequired");
            if (!_factories.TryGetValue(id, out var factory)) throw new NotSupportedException("UnknownNumericalEngine: " + id);
            ValidateSelection(options, factory);
            ((BoundFactory)factory).Validate();
            return factory;
        }
        internal static void ValidateSelection(ConcreteVerificationOptions options, IConcreteCalculationFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (options.CalculationEngineId != null && (options.CalculationEngineId != factory.Id
                || options.CalculationEngineVersion != factory.Version || options.CalculationEngineConfiguration != factory.Configuration))
                throw new InvalidOperationException("NumericalEngineBindingMismatch");
            if (options.CalculationEngineId == null && (options.CalculationEngineVersion != null || options.CalculationEngineConfiguration != null))
                throw new ArgumentException("NumericalEngineIdRequired");
        }
        private sealed class BoundFactory : IConcreteCalculationFactory
        {
            private readonly IConcreteCalculationFactory _source;
            public string Id { get; }
            public string Version { get; }
            public string Configuration { get; }
            public BoundFactory(IConcreteCalculationFactory source)
            { _source = source; Id = source.Id; Version = source.Version; Configuration = source.Configuration; }
            internal void Validate()
            {
                if (_source.Id != Id || _source.Version != Version || _source.Configuration != Configuration)
                    throw new InvalidOperationException("RegisteredNumericalEngineChanged");
            }
            public ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options)
            {
                Validate(); var session = _source.Create(section, standard, options) ?? throw new InvalidOperationException("NullNumericalSession"); Validate();
                var guarded = new GuardedSession(this, session); return new ConcreteCalculationSession(guarded, guarded);
            }
            private sealed class GuardedSession : ISectionResponseSolver, ISectionResistanceSolver
            {
                private readonly BoundFactory _owner;
                private readonly ConcreteCalculationSession _session;
                internal GuardedSession(BoundFactory owner, ConcreteCalculationSession session) { _owner = owner; _session = session; }
                public SectionResponse Solve(SectionAnalysisInput input, CancellationToken token)
                { _owner.Validate(); var result = _session.Response.Solve(input, token); _owner.Validate(); return result; }
                public SectionResistanceResponse SolveResistance(SectionAnalysisInput input, CancellationToken token)
                { _owner.Validate(); var result = _session.Resistance.SolveResistance(input, token); _owner.Validate(); return result; }
            }
        }
    }
}
