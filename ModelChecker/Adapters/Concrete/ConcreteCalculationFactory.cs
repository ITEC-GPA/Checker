using System;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    /// <summary>Numerical options passed to a session factory, distinct from the normative verification specification.</summary>
    public sealed class ConcreteCalculationOptions
    {
        private readonly CoordinateSystem _reference;
        public CoordinateSystem Reference => (CoordinateSystem)_reference.Clone();
        public SectionSolver.FailureAnalysisTypes Criterion { get; }
        public SectionSolver.StressAnalysisTypes Analysis { get; }
        public bool TensileConcrete { get; }
        public int AngularDivisions { get; }
        public double PsiRebar { get; }
        public double PsiTendon { get; }
        internal ConcreteCalculationOptions(CoordinateSystem reference, SectionSolver.FailureAnalysisTypes criterion,
            SectionSolver.StressAnalysisTypes analysis, bool tension, int divisions, double psiRebar, double psiTendon)
        { _reference = (CoordinateSystem)reference.Clone(); Criterion = criterion; Analysis = analysis; TensileConcrete = tension;
          AngularDivisions = divisions; PsiRebar = psiRebar; PsiTendon = psiTendon; }
    }
    public sealed class ConcreteCalculationSession
    {
        public ISectionResponseSolver Response { get; }
        public ISectionResistanceSolver Resistance { get; }
        public ConcreteCalculationSession(ISectionResponseSolver response, ISectionResistanceSolver resistance)
        { Response = response ?? throw new ArgumentNullException(nameof(response)); Resistance = resistance ?? throw new ArgumentNullException(nameof(resistance)); }
    }
    /// <summary>Creates exclusively owned sessions. Invoked under the adapter's synchronization; no FEM graph or verification orchestration is passed.</summary>
    public interface IConcreteCalculationFactory
    {
        string Id { get; }
        string Version { get; }
        string Configuration { get; }
        ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options);
    }
    public sealed class LegacyConcreteCalculationFactory : IConcreteCalculationFactory
    {
        public string Id => LegacySectionCalculation.EngineId;
        public string Version => LegacySectionCalculation.Version;
        public string Configuration => "PlasticDomain;WorkingRatioForceScaleN=1000000;WorkingRatioLengthScaleMm=1000";
        public ConcreteCalculationSession Create(ReinforcedConcreteSection section, StandardModelCode2010 standard, ConcreteCalculationOptions options)
        {
            var nativeOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(options.Reference, options.Criterion,
                SectionSolver.FailureDomainTypes.Plastic, options.Analysis, options.PsiRebar, options.PsiTendon, options.TensileConcrete, options.AngularDivisions);
            var solver = new LegacySectionCalculation(new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), nativeOptions, standard, options.TensileConcrete));
            return new ConcreteCalculationSession(solver, solver);
        }
    }
}
