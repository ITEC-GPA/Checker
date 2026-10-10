using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    [System.Runtime.Serialization.DataContract(Name = "ConcreteSettings", Namespace = Configuration.ConfigurationArchive.Namespace)]
    public sealed class ConcreteVerificationOptions
    {
        [System.Runtime.Serialization.DataMember(Order = 1)]
        public StandardModelCode2010 Standard { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 2)]
        public SectionSolver.FailureAnalysisTypes Criterion { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 3)]
        public bool ConsiderTensileConcrete { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 4)]
        public int AngularDivisions { get; set; } = 64;
        [System.Runtime.Serialization.DataMember(Order = 5)]
        public double PsiRebar { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 6)]
        public double PsiTendon { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 7)]
        public string StandardEdition { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 8)]
        public string NationalAnnex { get; set; }
        /// <summary>Stress analysis of the serviceability stress limits (SectionChecks with Serviceability/StressLimits).</summary>
        [System.Runtime.Serialization.DataMember(Order = 9)]
        public SectionSolver.StressAnalysisTypes ServiceabilityAnalysis { get; set; } = SectionSolver.StressAnalysisTypes.NonLinear;
        /// <summary>Explicit factor on the concrete stress limits, 1 = none (for example 0.8 for thin castings when the standard requires it).</summary>
        [System.Runtime.Serialization.DataMember(Order = 10)]
        public double ConcreteStressLimitFactor { get; set; } = 1;
        /// <summary>
        /// Assigned cot θ of the shear checks (SectionChecks Shear/Axis1-2); null = chosen by the method within its range.
        /// It is also the strut inclination shared by torsion and shear in the Torsion task, which requires it.
        /// </summary>
        [System.Runtime.Serialization.DataMember(Order = 11)]
        public double? ShearCotTheta { get; set; }
        /// <summary>Duration of the load in the crack checks (kt): long term unless stated.</summary>
        [System.Runtime.Serialization.DataMember(Order = 12)]
        public CrackLoadDuration CrackLoadDuration { get; set; } = CrackLoadDuration.LongTerm;
        /// <summary>Design wlim, mm, of the crack checks where the standard admits it (Eurocode family, Model Code 2010); null = limit of the standard.</summary>
        [System.Runtime.Serialization.DataMember(Order = 13)]
        public double? CrackDesignLimit { get; set; }
        /// <summary>Null selects the legacy engine for historical configurations. Explicit selections require version and configuration.</summary>
        [System.Runtime.Serialization.DataMember(Order = 14)] public string CalculationEngineId { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 15)] public string CalculationEngineVersion { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 16)] public string CalculationEngineConfiguration { get; set; }
        public ConcreteSectionVerifier CreateVerifier() => CreateVerifier(new Configuration.ConcreteCalculationCatalog());
        public ConcreteSectionVerifier CreateVerifier(Configuration.ConcreteCalculationCatalog calculations)
            => CreateVerifier((calculations ?? throw new ArgumentNullException(nameof(calculations))).Resolve(this));
        public ConcreteSectionVerifier CreateVerifier(IConcreteCalculationFactory calculationFactory)
        {
            Configuration.ConcreteCalculationCatalog.ValidateSelection(this, calculationFactory);
            return new ConcreteSectionVerifier(calculationFactory, Standard, Criterion, ConsiderTensileConcrete, AngularDivisions, PsiRebar, PsiTendon,
                StandardEdition, NationalAnnex, ServiceabilityAnalysis, ConcreteStressLimitFactor, ShearCotTheta, CrackLoadDuration, CrackDesignLimit);
        }
    }
}
