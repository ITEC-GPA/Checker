using GPC.Checker.Helper;
using GPC.Checker.SectionSolvers;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("GPCChecker.Test.Concrete")]
namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// Solver objects should generally not be instantiated directly (apart from tests) but always through a checker.
    /// </summary>
    [Serializable]
    public abstract class SectionSolver : ModelObjectId, ISerializable
    {
        #region Constant 

        // Units conversions
        public static readonly double FROM_N_TO_KN = 0.001;
        public static readonly double FROM_KN_TO_N = 1000;

        public static readonly double FROM_NM_TO_KNM = 0.000001;
        public static readonly double FROM_KNM_TO_NM = 1000000;

        #endregion

        #region Public enum 

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 1),
            (FailureZones.F2A, 1),
            (FailureZones.F2B, 1),
            (FailureZones.F3A, 35),
            (FailureZones.F3B, 5),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 15),
            (FailureZones.F4, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizationsComposite =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 15),
            (FailureZones.F3B, 5),
            (FailureZones.F4, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 30),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 3),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 5),
            (FailureZones.F4, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material with no rebars and hardening behaviour
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRCNoRebarsHardening =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 25),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material with no rebars and softening behaviour
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening =
        {
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 25),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        public enum FailureZones
        {
            /// <summary>
            /// Around P1. From espSu to 0 
            /// </summary>
            F1 = 1,

            /// <summary>
            /// Around P1. From 0 to espCy 
            /// </summary>
            F2A = 2,

            /// <summary>
            /// Around P1. From espCy to espCu 
            /// </summary>
            F2B = 3,

            /// <summary>
            /// Around P2. From espSu to espSy 
            /// </summary>
            F3A = 4,

            /// <summary>
            /// Around P2. From espSy to 0 
            /// </summary>
            F3B = 5,

            /// <summary>
            /// Around P3. From 0 to espCyCost 
            /// </summary>
            F4 = 6,
        }

        public enum FailureDomainTypes
        {
            Elastic,
            Plastic,
        }

        public enum StressAnalysisTypes
        {
            NonLinear,
            Linear,
        }

        public enum FailureAnalysisTypes
        {
            [Description("Constant N")]
            ConstantN,

            [Description("Constant eccentricity")]
            ConstantEccentricity,

            [Description("Constant Mx - My")]
            ConstantMxMy,

            [Description("Constant N and Mx")]
            ConstantNMx,

            [Description("Constant N and My")]
            ConstantNMy,
        }

        public enum DomainPointStrategyTypes
        {
            Iterative,
            Intersection
        }

        #endregion

        #region Variables

        protected double _stressAnalysisTolerance;
        protected double _failureAnalysisAngularTolerance;
        protected double _failureAnalysisDistanceTolerance;
        protected bool _considerTensileConcrete;

        protected IConcreteSection _concreteSection;
        protected Standard _standard;
        /// <summary>
        /// Standard for steel structural sections, like for example IPE300 inside reinforced concrete.
        /// </summary>
        protected readonly Standard _standardStructuralSteel;
        /// <summary>
        /// Point with respect to which the 3D domain is calculated and with respect to which stresses are transformed for verifications with the strength domain.<br/>
        /// <br/>
        /// 2023-11-02: This point has always been the center of gravity of the concrete section only.<br/>
        /// Now with mixed/composite sections it is necessary to use a point more like the center of gravity of the homogenized mixed/composite section.<br/>
        /// So the center of gravity of the concrete section is used as the initial value.
        /// </summary>
        protected readonly Point2d _integrationReferencePoint;

        protected List<string> _log;
        protected int _tetaDiscretization;

        protected QuadrangleGaussPoints.GaussPointNumber _gaussIntegrationQuadPoints;
        protected TriangleGaussPoints.GaussPointNumber _gaussIntegrationTriPoints;
        protected LineGaussPoints.GaussPointNumber _gaussIntegrationLinePoints;

        protected GaussIntegration.GlobalCoordinateGaussPoint[][] _globalCoordinateGaussPointsMesh;
        protected ThinWallSection.ThinWall[][] _steelSectionsThinWallsBreaked;
        protected GaussIntegration.GlobalCoordinateGaussPoint[][][] _globalCoordinateGaussPointsThinWalls;

        /// <summary>
        /// Section checker options.
        /// </summary>
        protected SectionChecker.SectionOptions _sectionOption;

        /// <summary>
        /// Save a copy to see if it changes.
        /// </summary>
        protected DomainPointStrategyTypes? _sectionOptionDomainPointStrategy;

        /// <summary>
        /// Concrete strategy of calculating the domain point.
        /// </summary>
        protected IDomainPointStrategy _calculateDomainPointStrategy;

        #endregion

        #region Properties

        internal double FailureAnalysisAngularTolerance => _failureAnalysisAngularTolerance;

        internal double FailureAnalysisDistanceTolerance => _failureAnalysisDistanceTolerance;

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard StandardStructuralSteel => _standardStructuralSteel;

        internal Point2d IntegrationReferencePoint => _integrationReferencePoint;

        public bool ConsiderTensileConcrete { get => _considerTensileConcrete; internal set => _considerTensileConcrete = value; }

        internal List<string> Log => _log;

        public int TetaDiscretization { get => _tetaDiscretization; set => _tetaDiscretization = value; }

        internal SectionChecker.SectionOptions SectionOption => _sectionOption;

        #endregion

        #region Constructor

        internal SectionSolver(IConcreteSection section, Standard standard, bool considerTensileConcrete, int id, Point2d integrationReferencePoint, Standard standardStructuralSteel, SectionChecker.SectionOptions sectionOption)
            : base(id)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _standardStructuralSteel = standardStructuralSteel;
            _integrationReferencePoint = integrationReferencePoint;

            _log = new List<string>();

            _stressAnalysisTolerance = 1e-5;
            _failureAnalysisAngularTolerance = 0.25e-3;
            _failureAnalysisDistanceTolerance = 0.5e-4;

            _considerTensileConcrete = considerTensileConcrete;
            _tetaDiscretization = 64;

            _gaussIntegrationQuadPoints = QuadrangleGaussPoints.GaussPointNumber.Quad400;
            _gaussIntegrationTriPoints = TriangleGaussPoints.GaussPointNumber.Tri79;
            _gaussIntegrationLinePoints = LineGaussPoints.GaussPointNumber.Line32;

            _globalCoordinateGaussPointsMesh = GetMeshGlobalCoordinateGaussPointsLinearShapeFunction();
            _steelSectionsThinWallsBreaked = BreakThinwallAtConcreteIntesections();
            _globalCoordinateGaussPointsThinWalls = GetThinWallsGlobalCoordinateGaussPointsLinearShapeFunction();
            _sectionOption = sectionOption;
            _sectionOptionDomainPointStrategy = null;
            SetDomainPointStrategy(_sectionOption.DomainPointStrategy);
        }

        protected SectionSolver(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _standardStructuralSteel = (StandardEN1993p11)info.GetValue("StandardStructuralSteel", typeof(StandardEN1993p11));
            _integrationReferencePoint = (Point2d)info.GetValue("IntegrationReferencePoint", typeof(Point2d));
            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
            _stressAnalysisTolerance = info.GetDouble("StressAnalysisTolerance");
            _failureAnalysisAngularTolerance = info.GetDouble("FailureAnalysisAngularTolerance");
            _tetaDiscretization = info.GetInt32("TetaDiscretization");
            _gaussIntegrationQuadPoints = (QuadrangleGaussPoints.GaussPointNumber)info.GetValue("GaussIntegrationQuadPoints", typeof(QuadrangleGaussPoints.GaussPointNumber));
            _gaussIntegrationTriPoints = (TriangleGaussPoints.GaussPointNumber)info.GetValue("GaussIntegrationTriPoints", typeof(TriangleGaussPoints.GaussPointNumber));
            _considerTensileConcrete = info.GetBoolean("ConsiderTensileConcrete");
            _sectionOption = (SectionChecker.SectionOptions)info.GetValue("SectionOption", typeof(SectionChecker.SectionOptions));
            _sectionOptionDomainPointStrategy = null;
            SetDomainPointStrategy(_sectionOption.DomainPointStrategy);
        }

        #endregion

        #region Abstract Method

        /// <summary>
        /// Calculate the design yelding strain for <paramref name="rebar"/> steel material in tension.
        /// </summary>
        /// <param name="rebar">The rebar</param>
        /// <returns>The yelding strain value</returns>
        protected double GetDesignYieldingStrainRebar(ReinforcedConcreteRebar rebar) => CalculateDesignYieldingStrainTensionRebar(rebar.RebarMaterial);
        protected double GetDesignYieldingStrainRebar(int rebarID) => CalculateDesignYieldingStrainTensionRebar(ConcreteSection.GetRebarById(rebarID).RebarMaterial);
        protected abstract double CalculateDesignYieldingStrainTensionRebar(SteelMaterial material);

        /// <summary>
        /// Calculate the design ultimate strain for <paramref name="rebar"/> steel material in tension.
        /// </summary>
        /// <param name="rebar">The rebar</param>
        /// <returns>The ultimate strain value</returns>
        protected double GetDesignUltimateStrainRebar(ReinforcedConcreteRebar rebar) => CalculateDesignUltimateStrainTensionRebar(rebar.RebarMaterial);
        protected double GetDesignUltimateStrainRebar(int rebarID) => CalculateDesignUltimateStrainTensionRebar(ConcreteSection.GetRebarById(rebarID).RebarMaterial);
        protected abstract double CalculateDesignUltimateStrainTensionRebar(SteelMaterial material);

        /// <summary>
        /// Calculate the design yelding strain for rebar steel material in compression.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        protected abstract double CalculateDesignYieldingStrainCompressionRebar(SteelMaterial material);

        /// <summary>
        /// Calculate the design yelding strain for rebar steel material in compression.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        protected abstract double CalculateDesignUltimateStrainCompressionRebar(SteelMaterial material);

        /// <summary>
        /// Calculate the design yelding strain for <paramref name="steelSection"/> steel material.
        /// </summary>
        /// <param name="steelSection">The steel section</param>
        /// <returns>The yelding strain value</returns>
        protected double GetDesignYieldingStrainStructuralSteel(ISteelSection steelSection) => CalculateDesignYieldingStrainTensionStructuralSteel(steelSection.SteelMaterial);
        protected abstract double CalculateDesignYieldingStrainTensionStructuralSteel(SteelMaterial material);

        /// <summary>
        /// Calculate the design ultimate strain for <paramref name="steelSection"/> steel material.
        /// </summary>
        /// <param name="steelSection">The steel section</param>
        /// <returns>The ultimate strain value</returns>
        protected double GetDesignUltimateStrainStructuralSteel(ISteelSection steelSection) => CalculateDesignUltimateStrainTensionStructuralSteel(steelSection.SteelMaterial);
        protected abstract double CalculateDesignUltimateStrainTensionStructuralSteel(SteelMaterial material);

        /// <summary>
        /// Calculate the design yelding strain for section setel material in compression.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        protected abstract double CalculateDesignYieldingStrainCompressionStructuralSteel(SteelMaterial material);

        /// <summary>
        /// Calculate the design ultimate strain for section setel material in compression.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        protected abstract double CalculateDesignUltimateStrainCompressionStructuralSteel(SteelMaterial material);

        /// <summary>
        /// Calculate the design ultimate strain for concrete material in compression
        /// </summary>
        /// <returns>The ultimate strain value</returns>
        protected abstract double GetUltimateStrainConcreteCompression();

        /// <summary>
        /// Calculate the design yelding strain for concrete material in compression
        /// </summary>
        /// <returns>The yelding strain value</returns>
        protected abstract double GetYieldingStrainConcreteCompression();

        /// <summary>
        /// Calculate the design yelding strain for concrete material in pure compression
        /// </summary>
        /// <returns>The yelding strain value</returns>
        protected abstract double GetYieldingStrainPureCompression();

        /// <summary>
        /// Calculate the design yelding strain for concrete material in tension
        /// </summary>
        /// <returns>The yelding strain value</returns>
        protected abstract double GetYieldingStrainConcreteTension();

        /// <summary>
        /// Calculate the design ultimate strain for concrete material in tension
        /// </summary>
        /// <returns>The ultimate strain value</returns>
        protected abstract double GetUltimateStrainConcreteTension();

        /// <summary>
        /// Calculate the characteristic compressive strength for concrete material in compression
        /// </summary>
        /// <returns>The characteristic compressive strength</returns>
        protected abstract double GetFck();

        /// <summary>
        /// Calculate the design concrete stress related to <paramref name="strain"/>
        /// </summary>
        /// <param name="strain"></param>
        /// <returns>The design concrete stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateSigmaC(double strain);

        /// <summary>
        /// Calculate design steel stress of <paramref name="rebar"/> related to <paramref name="strain"/>
        /// </summary>
        /// <param name="rebar">The input rebar</param>
        /// <param name="strain">The input strain</param>
        /// <returns>The design steel stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain);

        /// <summary>
        /// Calculate design steel stress of <paramref name="steelMaterial"/> related to <paramref name="strain"/>
        /// </summary>
        /// <param name="steelMaterial"></param>
        /// <param name="strain"></param>
        /// <param name="epsilonP"></param>
        /// <returns></returns>
        internal abstract double CalculateStressRebar(SteelMaterial steelMaterial, double strain, double epsilonP);

        /// <summary>
        /// Calculate design steel stress of <paramref name="steelSection"/> related to <paramref name="strain"/>
        /// </summary>
        /// <param name="steelSection">The input steel section</param>
        /// <param name="strain">The input strain</param>
        /// <returns>The design steel stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateStressStructuralSteel(ISteelSection steelSection, double strain);

        /// <summary>
        /// Calculate the reduction factor of input <paramref name="strainPlane"/> according to input standard
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>the reduction factor</returns>
        protected abstract double GetReductionFactor(StrainPlane strainPlane);

        /// <summary>
        /// Calculate the compression axial force limit according to input standard
        /// </summary>
        /// <returns>The compression axial force limi</returns>
        protected abstract double CalculateCompressionAxialForceLimit();

        #endregion

        #region Public method

        public virtual FailureDomainResult GetFailureDomainResult()
        {
            if (_sectionOption.FailureDomainType == FailureDomainTypes.Elastic)
                return GetElasticFailureDomainResult();
            else if (_sectionOption.FailureDomainType == FailureDomainTypes.Plastic)
                return GetPlasticFailureDomainResult();
            else
                return null;
        }

        public virtual FailureDomainResult2d GetFailureDomainResult2d()
        {
            if (_sectionOption.FailureDomainType == FailureDomainTypes.Elastic)
                return GetElasticFailureDomainResult2d();
            else if (_sectionOption.FailureDomainType == FailureDomainTypes.Plastic)
                return GetPlasticFailureDomainResult2d();
            else
                return null;
        }

        public virtual FailureDomainResult GetElasticFailureDomainResult()
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.IsCompositeSteelConcrete)
                zoneDiscretization = _elasticFailureZonesDiscretizationsComposite;
            else if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization, zoneDiscretization, FailureDomainTypes.Elastic);

            return new FailureDomainResult(ConcreteSection,
                CalculateFailureDomain(strainPlanes, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic),
                null, this, _standard, _sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult2d GetElasticFailureDomainResult2d(double angle = 0)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.IsCompositeSteelConcrete)
                zoneDiscretization = _elasticFailureZonesDiscretizationsComposite;
            else if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(2, zoneDiscretization, FailureDomainTypes.Elastic, angle);

            return new FailureDomainResult2d(ConcreteSection,
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic).RebuildFailureDomain()),
                null, this, _standard, _sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult GetPlasticFailureDomainResult()
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
            {
                if (ConcreteSection.RebarsCount != 0)
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;
                else
                {
                    if (_concreteSection.ConcreteMaterial.StressStrainTableTension.IsHardening())
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsHardening;
                    else
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening;
                }
            }

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization, zoneDiscretization, FailureDomainTypes.Plastic);

            return new FailureDomainResult(ConcreteSection,
                CalculateFailureDomain(strainPlanes, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic), null, this, _standard,
                _sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult2d GetPlasticFailureDomainResult2d(double angle = 0)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
            {
                if (ConcreteSection.RebarsCount != 0)
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;
                else
                {
                    if (_concreteSection.ConcreteMaterial.StressStrainTableTension.IsHardening())
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsHardening;
                    else
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening;
                }
            }

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(2, zoneDiscretization, FailureDomainTypes.Plastic, angle);

            return new FailureDomainResult2d(ConcreteSection,
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic).RebuildFailureDomain()), null, this, _standard,
                _sectionOption, Id, _standardStructuralSteel);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(_sectionOption.ForceReferenceCoordinateSystem),
                    _sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, false, null, null, Id,
                    _standardStructuralSteel);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetStressAnalysisResult(ResultBeamForces force)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                    CalculateStrainPlaneStressAnalysis(force.ConvertToForceTuple(_sectionOption.ForceReferenceCoordinateSystem),
                    _sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, false, null, null, Id,
                    _standardStructuralSteel);
        }

        public virtual StressAnalysisResult[] GetLinearStressAnalysisResults(ResultBeamForces[] force, double psi, double? psiTendon, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneLinearStressAnalysis(force[i].ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, psi, psiTendon,
                    _stressAnalysisTolerance), this, _standard, true, psi, psiTendon, Id,
                    _standardStructuralSteel);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces force, double psi, double? psiTendon)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                CalculateStrainPlaneLinearStressAnalysis(force.ConvertToForceTuple(_sectionOption.ForceReferenceCoordinateSystem),
                _sectionOption.ForceReferenceCoordinateSystem, psi, psiTendon,
                _stressAnalysisTolerance), this, _standard, true, psi, psiTendon, Id,
                _standardStructuralSteel);
        }

        internal FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces force, FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            return _calculateDomainPointStrategy.CalculateDomainPoint(force, failureAnalysisTypeOverride);
        }

        public FailureDomain.FailureDomainPoint[] CalculateDomainPoint(ResultBeamForces[] force)
        {
            FailureDomain.FailureDomainPoint[] result = new FailureDomain.FailureDomainPoint[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                result[i] = _calculateDomainPointStrategy.CalculateDomainPoint(force[i]);
            });

            return result;
        }

        #endregion

        #region Internal methods

        internal void SetDomainPointStrategy(DomainPointStrategyTypes domainPointStrategyTypes)
        {
            if (!_sectionOptionDomainPointStrategy.HasValue || _sectionOptionDomainPointStrategy.Value != domainPointStrategyTypes)
            {
                _sectionOptionDomainPointStrategy = domainPointStrategyTypes;
                switch (domainPointStrategyTypes)
                {
                    case DomainPointStrategyTypes.Iterative:
                        _calculateDomainPointStrategy = new DomainPointStrategyIterative(this);
                        break;
                    case DomainPointStrategyTypes.Intersection:
                        _calculateDomainPointStrategy = new DomainPointStrategyIntersection(this);
                        break;
                }
            }
        }

        #endregion

        #region Protected method - SectionIntegration

        protected virtual GaussIntegration.GlobalCoordinateGaussPoint[][] GetMeshGlobalCoordinateGaussPointsLinearShapeFunction()
        {
            return GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(ConcreteSection.Mesh, _gaussIntegrationQuadPoints, _gaussIntegrationTriPoints);
        }

        protected virtual GaussIntegration.GlobalCoordinateGaussPoint[][][] GetThinWallsGlobalCoordinateGaussPointsLinearShapeFunction()
        {
            GaussIntegration.GlobalCoordinateGaussPoint[][][] returnValue = new GaussIntegration.GlobalCoordinateGaussPoint[_concreteSection.SteelSections.Count][][];
            for (int i = 0; i < _concreteSection.SteelSections.Count; i++)
            {
                returnValue[i] = GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(_concreteSection.SteelSections[i], _steelSectionsThinWallsBreaked[i], _gaussIntegrationLinePoints);
            }
            return returnValue;
        }

        /// <summary>
        /// Prepare a list of broken thinwalls on the sides of the concrete.
        /// The purpose is to have entire thinwalls either inside or outside the concrete section.
        /// </summary>
        /// <returns></returns>
        private ThinWallSection.ThinWall[][] BreakThinwallAtConcreteIntesections()
        {
            // Get all concrete edes.
            var clsEdges = _concreteSection.Shape.Fill2d.Explode().ToList();
            if (_concreteSection.Shape.HasHoles)
                for (int i = 0; i < _concreteSection.Shape.Holes2d.Length; i++)
                    clsEdges.AddRange(_concreteSection.Shape.Holes2d[i].Explode());

            // Break all thiwall in concrete edges.
            var returnThinWalls = new ThinWallSection.ThinWall[_concreteSection.SteelSections.Count][];
            for (int i = 0; i < _concreteSection.SteelSections.Count; i++)
            {
                var steelSection = _concreteSection.SteelSections[i];

                if (steelSection.Section.SectionShape is ThinWallSection thinSection)
                {
                    // Move edges to local in steel section system.
                    var clsEdgeInLocal = new List<Line2d>();
                    foreach (var clsEdge in clsEdges)
                    {
                        clsEdgeInLocal.Add(new Line2d(
                            steelSection.PositionToLocal(clsEdge.Start),
                            steelSection.PositionToLocal(clsEdge.End)
                            ));
                    }
                    // Break all thinwalls in current section.
                    var thinWallsBreaked = thinSection.BreakThinWallsInEdges(clsEdgeInLocal);

                    // Are inside or outside?
                    for (int j = 0; j < thinWallsBreaked.Length; j++)
                    {
                        var thinWall = thinWallsBreaked[j];
                        var thinWallCenterid = steelSection.PositionToGlobal(thinWall.Point);
                        thinWall.IsInsideConcrete = _concreteSection.Shape.IsPointInside(thinWallCenterid);
                    }
                    returnThinWalls[i] = thinWallsBreaked.ToArray();
                }
            }
            return returnThinWalls;
        }

        #region Force resultant 

        internal ForceTuple CalculateForceResultantForTension(StrainPlane strainPlane)
        {
            return CalculateForceResultantForTension(strainPlane, ConcreteSection.GetRebarIsInsideAssociation());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultantForTension(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                double limitCompression = CalculateCompressionAxialForceLimit();
                ForceTuple force = IntegrateSectionStress(strainPlane) +
                    IntegrateRebarStress(strainPlane, rebarIsInsideAssociation) +
                    IntegrateStructuralSteelStress(strainPlane);

                return CalculateCompressionReduction(force, limitCompression);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        internal ForceTuple CalculateForceResultantForDomain(StrainPlane strainPlane)
        {
            return CalculateForceResultantForDomain(strainPlane, ConcreteSection.GetRebarIsInsideAssociation());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        internal virtual ForceTuple CalculateForceResultantForDomain(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                double limitCompression = CalculateCompressionAxialForceLimit();
                ForceTuple force = IntegrateSectionStress(strainPlane) +
                    IntegrateRebarStress(strainPlane, rebarIsInsideAssociation) +
                    IntegrateStructuralSteelStress(strainPlane);

                return CalculateCompressionReduction(force * GetReductionFactor(strainPlane), limitCompression);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        protected virtual ForceTuple[] CalculateForceResultantForDomain(StrainPlane[] strainPlanes, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                ForceTuple[] returnValue = new ForceTuple[strainPlanes.Length];

                ForceTuple[] concreteStresses = IntegrateSectionStress(strainPlanes);
                ForceTuple[] rebarStresses = IntegrateRebarStress(strainPlanes, rebarIsInsideAssociation);
                ForceTuple[] structuralSteelStresses = IntegrateStructuralSteelStress(strainPlanes);

                for (int i = 0; i < strainPlanes.Length; i++)
                    returnValue[i] = (concreteStresses[i] + rebarStresses[i] + structuralSteelStresses[i]) * GetReductionFactor(strainPlanes[i]);

                double limitCompression = CalculateCompressionAxialForceLimit();

                if (limitCompression != double.MinValue)
                {
                    for (int i = 0; i < returnValue.Length; i++)
                    {
                        if (i != 0)
                        {
                            if (returnValue[i - 1].N > limitCompression && returnValue[i].N < limitCompression)
                            {
                                double mx = Utilities.Maths.Interpolation.GetLinearInterpolation(returnValue[i - 1].N, returnValue[i].N, returnValue[i - 1].Mx, returnValue[i].Mx, limitCompression);
                                double my = Utilities.Maths.Interpolation.GetLinearInterpolation(returnValue[i - 1].N, returnValue[i].N, returnValue[i - 1].My, returnValue[i].My, limitCompression);
                                returnValue[i] = new ForceTuple(limitCompression, mx, my);
                            }
                            if (returnValue[i].N < limitCompression)
                            {
                                double ratio = limitCompression / returnValue[i].N;

                                returnValue[i] = returnValue[i] * ratio;
                                returnValue[i].N = limitCompression;
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> with homogenization coefficient and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(double psi, double? psiTendon, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                return IntegrateSectionStressLinearElastic(strainPlane) +
                    IntegrateRebarLinearStress(psi, psiTendon, strainPlane, rebarIsInsideAssociation) +
                    IntegrateStructuralSteelLinearStress(psi, strainPlane);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        #endregion

        #region Concrete Integration

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        internal virtual ForceTuple IntegrateSectionStress(StrainPlane strainPlane)
        {
            try
            {
                (double s, double, double) ret = GaussIntegration.IntegrationLinearShapeFunction((x, y) =>
                {
                    double sigmaC = CalculateSigmaC(strainPlane.GetStrain(x, y));
                    return (sigmaC, -sigmaC * (y - _integrationReferencePoint.Y), sigmaC * (x - _integrationReferencePoint.X));
                },
                _globalCoordinateGaussPointsMesh);

                return new ForceTuple(ret);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }
        }

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected virtual ForceTuple IntegrateSectionStressLinearElastic(StrainPlane strainPlane)
        {
            try
            {
                (double s, double, double) ret = GaussIntegration.IntegrationLinearShapeFunction((x, y) =>
                {
                    double sigmaC = CalculateElasticSigmaC(strainPlane.GetStrain(x, y));
                    return (sigmaC, -sigmaC * (y - _integrationReferencePoint.Y), sigmaC * (x - _integrationReferencePoint.X));
                },
                _globalCoordinateGaussPointsMesh);

                return new ForceTuple(ret);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }
        }

        protected virtual ForceTuple[] IntegrateSectionStress(StrainPlane[] strainPlane)
        {
            try
            {
                ForceTuple[] returnValue = new ForceTuple[strainPlane.Length];
                Func<double, double, (double, double, double)>[] functions = new Func<double, double, (double, double, double)>[strainPlane.Length];

                for (int i = 0; i < strainPlane.Length; i++)
                {
                    StrainPlane sp = strainPlane[i];
                    functions[i] = new Func<double, double, (double, double, double)>((x, y) =>
                        {
                            double sigmaC = CalculateSigmaC(sp.GetStrain(x, y));
                            return (sigmaC, sigmaC * (y - _integrationReferencePoint.Y), sigmaC * (x - _integrationReferencePoint.X));
                        });
                }

                (double, double, double)[] res = GaussIntegration.IntegrationLinearShapeFunction(functions, _globalCoordinateGaussPointsMesh);

                for (int j = 0; j < strainPlane.Length; j++)
                    returnValue[j] = new ForceTuple(res[j].Item1, -res[j].Item2, res[j].Item3);

                return returnValue;
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return null;
            }
        }

        /// <inheritdoc cref="IntegrateSectionStress(StrainPlane)"/>
        protected virtual async Task<ForceTuple> IntegrateSectionStressAsync(StrainPlane strainPlane)
        {
            return await Task.Run(() =>
            {
                return IntegrateSectionStress(strainPlane);
            });
        }

        /// <returns>The design concrete stress related to <paramref name="strain"/> with linear elastic stress-strain diagram</returns>
        public double CalculateElasticSigmaC(double strain)
        {
            if (strain < 0)
            {
                // compressione
                return _concreteSection.ConcreteMaterial.ElasticModulusCompression * strain;
            }
            else
            {
                // trazione
                if (_considerTensileConcrete)
                {
                    return _concreteSection.ConcreteMaterial.ElasticModulusTension * strain;
                }
                else
                {
                    return 0;
                }
            }

        }

        #endregion

        #region Rebar Integration

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        protected virtual ForceTuple IntegrateRebarStress(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var rebars = ConcreteSection.GetRebars();

            double deltaNArray = 0;
            double deltaMxArray = 0;
            double deltaMyArray = 0;

            for (int i = 0; i < rebars.Length; i++)
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaS = CalculateStressRebar(rebars[i], strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateSigmaC(strain);

                deltaNArray += (sigmaS - sigmaC) * rebars[i].Area;
                deltaMxArray += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - _integrationReferencePoint.Y);
                deltaMyArray += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - _integrationReferencePoint.X);
            }

            return new ForceTuple(deltaNArray, -deltaMxArray, deltaMyArray);
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlanes">The strain plane</param>
        protected virtual ForceTuple[] IntegrateRebarStress(StrainPlane[] strainPlanes, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            ForceTuple[] returnValue = new ForceTuple[strainPlanes.Length];

            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, strainPlanes.Length), (range) =>
            {
                for (int j = range.Item1; j < range.Item2; j++)
                    returnValue[j] = IntegrateRebarStress(strainPlanes[j], rebarIsInsideAssociation);
            });

            return returnValue;
        }

        /// <inheritdoc cref="IntegrateRebarStress(StrainPlane, Dictionary<int, bool>)"/>
        protected virtual async Task<ForceTuple> IntegrateRebarStressAsync(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            return await Task.Run(() =>
            {
                return IntegrateRebarStress(strainPlane, rebarIsInsideAssociation);
            });
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual ForceTuple IntegrateRebarLinearStress(double psi, double? psiTendon, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();

            double[] deltaNArray = new double[rebars.Length];
            double[] deltaMxArray = new double[rebars.Length];
            double[] deltaMyArray = new double[rebars.Length];

            Parallel.For(0, rebars.Length, (i) =>
            {
                ReinforcedConcreteRebar rebar = rebars[i];
                double strain = strainPlane.GetStrain(rebar.Position);
                double sigmaS;
                if (rebar.EpsilonP > 0)
                    sigmaS = CalculateElasticSigmaS(psiTendon.Value, rebar, strain);
                else
                    sigmaS = CalculateElasticSigmaS(psi, rebar, strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateElasticSigmaC(strain);

                deltaNArray[i] = (sigmaS - sigmaC) * rebar.Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * rebar.Area * (rebar.Position.Y - _integrationReferencePoint.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * rebar.Area * (rebar.Position.X - _integrationReferencePoint.X);
            });

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        public double CalculateElasticSigmaS(double psi, ReinforcedConcreteRebar rebar, double strain)
        {
            if (strain < 0)
                return rebar.RebarMaterial.ElasticModulusCompression * (1 + psi) * strain + rebar.RebarMaterial.ElasticModulusCompression * rebar.EpsilonP;
            else
                return rebar.RebarMaterial.ElasticModulusTension * (1 + psi) * strain + rebar.RebarMaterial.ElasticModulusTension * rebar.EpsilonP;
        }

        public double CalculateElasticSigmaS(double psi, SteelMaterial steelMaterial, double strain, double epsilonP)
        {
            if (strain < 0)
                return steelMaterial.ElasticModulusCompression * (1 + psi) * strain + steelMaterial.ElasticModulusCompression * epsilonP;
            else
                return steelMaterial.ElasticModulusTension * (1 + psi) * strain + steelMaterial.ElasticModulusTension * epsilonP;
        }

        #endregion

        #region Steel sections Integration

        /// <summary>
        /// Calculate the stress resultant of the steel sections parts.
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected ForceTuple IntegrateStructuralSteelStress(StrainPlane strainPlane)
        {
            double quadratureN = 0;
            double quadratureMx = 0;
            double quadratureMy = 0;

            for (int i = 0; i < ConcreteSection.SteelSections.Count; i++)
            {
                SteelSectionPosition steelSection = ConcreteSection.SteelSections[i];

                for (int j = 0; j < _steelSectionsThinWallsBreaked[i].Length; j++)
                {
                    // Make functions for stress.
                    double stressFunctionN(double x, double y)
                    {
                        var strain = strainPlane.GetStrain(x, y);
                        double sigmaC = _steelSectionsThinWallsBreaked[i][j].IsInsideConcrete ? CalculateSigmaC(strain) : 0.0;
                        return steelSection.Section.SteelMaterial.GetStress(strain) - sigmaC;
                    }
                    double stressFunctionMx(double x, double y) => stressFunctionN(x, y) * (y - _integrationReferencePoint.Y);
                    double stressFunctionMy(double x, double y) => stressFunctionN(x, y) * (x - _integrationReferencePoint.X);

                    var thickness = _steelSectionsThinWallsBreaked[i][j].T;
                    // Integrate functions.
                    quadratureN += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionN, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                    quadratureMx += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionMx, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                    quadratureMy += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionMy, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                }
            }
            return new ForceTuple(quadratureN, -quadratureMx, quadratureMy);
        }

        /// <summary>
        /// Calculate the resultant of all structural steel sections.
        /// </summary>
        /// <param name="strainPlanes">The strain plane</param>
        protected ForceTuple[] IntegrateStructuralSteelStress(StrainPlane[] strainPlanes)
        {
            ForceTuple[] returnValue = new ForceTuple[strainPlanes.Length];

            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, strainPlanes.Length), (range) =>
            {
                for (int j = range.Item1; j < range.Item2; j++)
                    returnValue[j] = IntegrateStructuralSteelStress(strainPlanes[j]);
            });

            return returnValue;
        }

        /// <summary>
        /// Calculate the resultant of all the steel sections
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected ForceTuple IntegrateStructuralSteelLinearStress(double psi, StrainPlane strainPlane)
        {
            double quadratureN = 0;
            double quadratureMx = 0;
            double quadratureMy = 0;

            for (int i = 0; i < ConcreteSection.SteelSections.Count; i++)
            {
                SteelSectionPosition steelSection = ConcreteSection.SteelSections[i];

                for (int j = 0; j < _steelSectionsThinWallsBreaked[i].Length; j++)
                {
                    // Make functions for stress.
                    double stressFunctionN(double x, double y)
                    {
                        var strain = strainPlane.GetStrain(x, y);
                        double sigmaC = _steelSectionsThinWallsBreaked[i][j].IsInsideConcrete ? CalculateSigmaC(strain) : 0.0;
                        return CalculateElasticSigmaS(psi, steelSection.Section, strain) - sigmaC;
                    }
                    double stressFunctionMx(double x, double y) => stressFunctionN(x, y) * (y - _integrationReferencePoint.Y);
                    double stressFunctionMy(double x, double y) => stressFunctionN(x, y) * (x - _integrationReferencePoint.X);

                    var thickness = _steelSectionsThinWallsBreaked[i][j].T;
                    // Integrate functions.
                    quadratureN += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionN, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                    quadratureMx += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionMx, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                    quadratureMy += GaussIntegration.IntegrationLinearShapeFunction(stressFunctionMy, _globalCoordinateGaussPointsThinWalls[i][j]) * thickness;
                }
            }
            return new ForceTuple(quadratureN, -quadratureMx, quadratureMy);
        }

        public double CalculateElasticSigmaS(double psi, SteelSection section, double strain)
        {
            if (strain < 0)
                return section.SteelMaterial.ElasticModulusCompression * (1 + psi) * strain;
            else
                return section.SteelMaterial.ElasticModulusTension * (1 + psi) * strain;
        }

        #endregion

        /// <summary>
        /// Calculates the distances of concrete vertices, reinforcing bars, and steel profiles from the concrete center of gravity.
        /// </summary>
        /// <param name="teta">Angle of the line with respect to which to calculate distances, counterclockwise angle with zero in x-positive.</param>
        /// <returns></returns>
        internal virtual BoundaryDistances CalculateMaxMinSectionDistances(double teta)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);

            double dminRebar = double.MaxValue;
            double dminRebarRatio = double.MaxValue;
            double dmaxRebar = double.MinValue;
            double dmaxRebarRatio = double.MinValue;
            double dmaxConcrete = double.MinValue;
            double dminConcrete = double.MaxValue;
            double dmaxStructuralSteel = double.MinValue;
            double dminStructuralSteel = double.MaxValue;

            int dMinRebarId = -1;
            int dMaxRebarId = -1;
            int dMaxVertexIndex = -1;
            int dMinVertexIndex = -1;
            int dMaxSteelSectionId = -1;
            Point2d dMaxSteelVertex = null;
            int dMinSteelSectionId = -1;
            Point2d dMinSteelVertex = null;

            var rebars = ConcreteSection.GetRebars();

            for (int r = 0; r < rebars.Length; r++)
            {
                double w1 = (rebars[r].Position.Y - _integrationReferencePoint.Y) * cosTeta - (rebars[r].Position.X - _integrationReferencePoint.X) * sinTeta;
                double eps = Math.Abs(rebars[r].RebarMaterial.StrainUTension);

                double ratio = w1 / eps;
                double radius = 0.5 * rebars[r].RebarSection.Diameter;

                if (ratio <= dminRebarRatio)
                {
                    dminRebarRatio = ratio;
                    dminRebar = w1 - radius;
                    dMinRebarId = rebars[r].Id;
                }

                if (ratio >= dmaxRebarRatio)
                {
                    dmaxRebarRatio = ratio;
                    dmaxRebar = w1 + radius;
                    dMaxRebarId = rebars[r].Id;
                }
            }

            // When calculating the distances of the points of the composite section to then determine the points p1...p6 do not use
            // the axes of the thinwalls but the actual outermost points of the profile.
            // If you use the midpoints of the thinwalls you bring a higher value of strain and tension to the outermost edge of the profile.
            // You could consider the axis of the thinwalls if you reduced the deformation(strain) of the innermost points of the thinwall axes.
            if (ConcreteSection.SteelSections != null)
            {
                for (int s = 0; s < ConcreteSection.SteelSections.Count; s++)
                {
                    var steelSection = ConcreteSection.SteelSections[s];

                    foreach (var vertex in steelSection.Section.Shape.Fill)
                    {
                        var point = steelSection.PositionToGlobal(vertex);
                        double w1 = (point.Y - _integrationReferencePoint.Y) * cosTeta - (point.X - _integrationReferencePoint.X) * sinTeta;

                        if (w1 >= dmaxStructuralSteel)
                        {
                            dMaxSteelSectionId = s;
                            dMaxSteelVertex = point;
                            dmaxStructuralSteel = w1;
                        }

                        if (w1 <= dminStructuralSteel)
                        {
                            dMinSteelSectionId = s;
                            dMinSteelVertex = point;
                            dminStructuralSteel = w1;
                        }
                    }
                }
            }

            for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
            {
                double w1 = (ConcreteSection.Shape.Fill[c].Y - _integrationReferencePoint.Y) * cosTeta -
                    (ConcreteSection.Shape.Fill[c].X - _integrationReferencePoint.X) * sinTeta;

                if (w1 >= dmaxConcrete)
                {
                    dMaxVertexIndex = c;
                    dmaxConcrete = w1;
                }

                if (w1 <= dminConcrete)
                {
                    dminConcrete = w1;
                    dMinVertexIndex = c;
                }
            }

            return new BoundaryDistances(teta, dMinRebarId, dminRebar, dMaxRebarId, dmaxRebar,
                dMinVertexIndex, dminConcrete, dMaxVertexIndex, dmaxConcrete,
                dMinSteelSectionId, dMinSteelVertex, dminStructuralSteel,
                dMaxSteelSectionId, dMaxSteelVertex, dmaxStructuralSteel);
        }

        /// <summary>
        /// Calculates all possible points of rotation in compression and tension given by all materials in the section.
        /// </summary>
        /// <param name="teta"></param>
        /// <param name="analysisType"></param>
        /// <param name="tensionRotationPoints">Points of rotation possible in tension.</param>
        /// <param name="tensionRotationPointsF1">Points of rotation possible in tension in zone F1.</param>
        /// <param name="compressionRotationPoints">Points of rotation possible in compression.</param>
        /// <param name="minDistanceCompression">Point with minimum y that can be compressed.</param>
        /// <param name="elasticEpsilonTension">Yield tensile strain at the farthest point (most in tension).</param>
        internal void CalculateRotationPointsPerMaterial(in double teta, in FailureDomainTypes analysisType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);
            var tensionPointsList = new List<DeformationFieldsPoint>();
            var tensionPointsListF1 = new List<DeformationFieldsPoint>(); // List very similar to the previous one with the only distinction for FRC sections changing the epsilon to F1 zone.
            var compressionPointsList = new List<DeformationFieldsPoint>();
            elasticEpsilonTension = double.MaxValue;
            double minDistanceTension = double.MaxValue; // Point with minimum y that can be tensioned.

            // *** Concrete ***
            {
                double dMaxConcrete = double.MinValue; // Y-coordinate max for concrete.
                Point3d dMaxConcretePoint = null;
                double dMinConcrete = double.MaxValue; // Y-coordinate min for concrete.
                Point3d dMinConcretePoint = null;

                // Tension limit from concrete, only if it is FRC.
                // Compression limit.
                for (int c = 0; c < _concreteSection.Shape.Fill.Count; c++)
                {
                    var vertex = _concreteSection.Shape.Fill[c];
                    double w1 = (vertex.Y - _integrationReferencePoint.Y) * cosTeta - (vertex.X - _integrationReferencePoint.X) * sinTeta;

                    if (w1 >= dMaxConcrete)
                    {
                        dMaxConcretePoint = vertex;
                        dMaxConcrete = w1;
                    }

                    if (w1 <= dMinConcrete)
                    {
                        dMinConcretePoint = vertex;
                        dMinConcrete = w1;
                    }
                }
                minDistanceCompression = dMinConcrete;
                // *** Concrete in tension ***
                // It can become the P1 point.
                if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC)
                {
                    double maxClsStrain = 0.0;
                    double maxClsStrainF1 = 0.0;

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            maxClsStrain = GetYieldingStrainConcreteTension();
                            break;

                        case FailureDomainTypes.Plastic:
                            {
                                maxClsStrain = Math.Min(0.02, GetUltimateStrainConcreteTension());
                                maxClsStrainF1 = Math.Min(0.01, maxClsStrain);
                            }
                            break;
                    }
                    tensionPointsList.Add(new DeformationFieldsPoint(maxClsStrain, dMinConcretePoint, dMinConcrete));
                    tensionPointsListF1.Add(new DeformationFieldsPoint(maxClsStrainF1, dMinConcretePoint, dMinConcrete));
                    minDistanceTension = dMinConcrete;
                    elasticEpsilonTension = GetYieldingStrainConcreteTension();
                }
                else if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete &&
                    _concreteSection.Rebars.Count() == 0 && _concreteSection.SteelSections.Count == 0)
                {
                    // Special case, there is only concrete, a traction point needs to be set.
                    var tensionPoint = new DeformationFieldsPoint(0.0, dMinConcretePoint, dMinConcrete);
                    tensionPointsList.Add(tensionPoint);
                    tensionPointsListF1.Add(tensionPoint);
                    minDistanceTension = dMinConcrete;
                    elasticEpsilonTension = 0.0;
                }
                // *** Concrete in compression ***
                // Also called P2.
                {
                    double minClsStrain = 0.0;

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            minClsStrain = GetYieldingStrainConcreteCompression();
                            break;

                        case FailureDomainTypes.Plastic:
                            minClsStrain = GetUltimateStrainConcreteCompression();
                            break;
                    }
                    compressionPointsList.Add(new DeformationFieldsPoint(minClsStrain, dMaxConcretePoint, dMaxConcrete));
                }
                // *** Concrete in uniform compression ***
                // Also called P3.
                if (analysisType == FailureDomainTypes.Plastic)
                {
                    double fraction = GetYieldingStrainPureCompression() / GetUltimateStrainConcreteCompression();
                    double heigth = dMaxConcrete - dMinConcrete;
                    double distPureConpression = dMaxConcrete - (1.0 - fraction) * heigth;

                    Point2d strainPlaneCenter = new Point2d(
                        distPureConpression * (-sinTeta) + _integrationReferencePoint.X,
                        distPureConpression * cosTeta + _integrationReferencePoint.Y);

                    compressionPointsList.Add(new DeformationFieldsPoint(GetYieldingStrainPureCompression(), strainPlaneCenter, distPureConpression));
                }
            }

            // *** Rebars ***
            // Tension and compression limit.
            // Group by material and epsilonP.
            {
                var rebars = _concreteSection.GetRebars();

                // Group by material and epsilonP.
                var rebarsPretensionedPerMaterialEpsilonP = rebars
                    .GroupBy(r => (r.RebarMaterial.Name, r.EpsilonP)) // Group rebars by material and epsilon_P.
                    .Select(group => new
                    {
                        RebarsMaterial = group.First().RebarMaterial, // Repeated material.
                        RebarsEpsilonP = group.First().EpsilonP, // Repeated EpsilonP.
                        RebarsArray = group.ToArray() // All rebars with this material.
                    })
                    .ToArray();

                foreach (var rebarsGroup in rebarsPretensionedPerMaterialEpsilonP)
                {
                    // For each group find the maximum and minimum Y coordinate.
                    double dMaxRebars = double.MinValue; // Y-coordinate max for rebar group.
                    Point3d dMaxRebarsPoint = null;
                    double dMinRebars = double.MaxValue; // Y-coordinate min for rebar group.
                    Point3d dMinRebarsPoint = null;

                    for (int r = 0; r < rebarsGroup.RebarsArray.Length; r++)
                    {
                        var rebar = rebarsGroup.RebarsArray[r];
                        // Consider the actual size of the bar.
                        double radius = 0.5 * rebar.RebarSection.Diameter;
                        var positiveDeltaRadius = new Vector2d(-radius * sinTeta, radius * cosTeta);
                        var positionMax = rebar.Position + positiveDeltaRadius;
                        var positionMin = rebar.Position - positiveDeltaRadius;

                        double w1Max = (positionMax.Y - _integrationReferencePoint.Y) * cosTeta - (positionMax.X - _integrationReferencePoint.X) * sinTeta;
                        double w1Min = (positionMin.Y - _integrationReferencePoint.Y) * cosTeta - (positionMin.X - _integrationReferencePoint.X) * sinTeta;

                        if (w1Max >= dMaxRebars)
                        {
                            dMaxRebarsPoint = positionMax;
                            dMaxRebars = w1Max;
                        }

                        if (w1Min <= dMinRebars)
                        {
                            dMinRebarsPoint = positionMin;
                            dMinRebars = w1Min;
                        }
                    }
                    if (dMinRebars < minDistanceCompression)
                        minDistanceCompression = dMinRebars;
                    if (dMinRebars < minDistanceTension)
                    {
                        minDistanceTension = dMinRebars;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                elasticEpsilonTension = 0.0;
                                break;

                            case FailureDomainTypes.Plastic:
                                elasticEpsilonTension = CalculateDesignYieldingStrainTensionRebar(rebarsGroup.RebarsMaterial) - rebarsGroup.RebarsEpsilonP;
                                break;
                        }
                    }
                    // *** Rebars in tension ***
                    // It can become the P1 point.
                    {
                        double maxRebarsStrain = 0.0;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                maxRebarsStrain = CalculateDesignYieldingStrainTensionRebar(rebarsGroup.RebarsMaterial) - rebarsGroup.RebarsEpsilonP;
                                break;

                            case FailureDomainTypes.Plastic:
                                maxRebarsStrain = CalculateDesignUltimateStrainTensionRebar(rebarsGroup.RebarsMaterial) - rebarsGroup.RebarsEpsilonP;
                                break;
                        }
                        // maxRebarsStrain should always be positive.
                        var tensionPoint = new DeformationFieldsPoint(maxRebarsStrain, dMinRebarsPoint, dMinRebars);
                        tensionPointsList.Add(tensionPoint);
                        tensionPointsListF1.Add(tensionPoint);
                    }
                    // *** Rebars in compression ***
                    {
                        double minRebarsStrain = 0.0;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                minRebarsStrain = CalculateDesignYieldingStrainCompressionRebar(rebarsGroup.RebarsMaterial) - rebarsGroup.RebarsEpsilonP;
                                break;

                            case FailureDomainTypes.Plastic:
                                minRebarsStrain = CalculateDesignUltimateStrainCompressionRebar(rebarsGroup.RebarsMaterial) - rebarsGroup.RebarsEpsilonP;
                                break;
                        }
                        compressionPointsList.Add(new DeformationFieldsPoint(minRebarsStrain, dMaxRebarsPoint, dMaxRebars));
                    }
                }
            }

            // *** Steel sections ***
            // Tension and compression limit.
            // Group by material.
            {
                // Group by material and epsilonP.
                var steelSectionPerMaterial = _concreteSection.SteelSections
                    .GroupBy(ss => ss.Section.SteelMaterial.Name) // Group steel sections by material.
                    .Select(group => new
                    {
                        SteelSectionsMaterial = group.First().Section.SteelMaterial, // Repeated material.
                        SteelSectionsArray = group.ToArray() // All steel sections with this material.
                    })
                    .ToArray();

                foreach (var steelSectionsGroup in steelSectionPerMaterial)
                {
                    double dmaxStructuralSteel = double.MinValue;
                    double dminStructuralSteel = double.MaxValue;
                    Point2d dmaxStructuralSteelVertex = null;
                    Point2d dminStructuralSteelVertex = null;

                    // When calculating the distances of the points of the composite section to then determine the points p1...p6 do not use
                    // the axes of the thinwalls but the actual outermost points of the profile.
                    // If you use the midpoints of the thinwalls you bring a higher value of strain and tension to the outermost edge of the profile.
                    // You could consider the axis of the thinwalls if you reduced the deformation(strain) of the innermost points of the thinwall axes.
                    for (int s = 0; s < steelSectionsGroup.SteelSectionsArray.Length; s++)
                    {
                        var steelSection = steelSectionsGroup.SteelSectionsArray[s];

                        foreach (var vertex in steelSection.Section.Shape.Fill)
                        {
                            var point = steelSection.PositionToGlobal(vertex);
                            double w1 = (point.Y - _integrationReferencePoint.Y) * cosTeta - (point.X - _integrationReferencePoint.X) * sinTeta;

                            if (w1 >= dmaxStructuralSteel)
                            {
                                dmaxStructuralSteelVertex = point;
                                dmaxStructuralSteel = w1;
                            }

                            if (w1 <= dminStructuralSteel)
                            {
                                dminStructuralSteelVertex = point;
                                dminStructuralSteel = w1;
                            }
                        }
                    }
                    if (dminStructuralSteel < minDistanceCompression)
                        minDistanceCompression = dminStructuralSteel;
                    if (dminStructuralSteel < minDistanceTension)
                    {
                        minDistanceTension = dminStructuralSteel;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                elasticEpsilonTension = 0.0;
                                break;

                            case FailureDomainTypes.Plastic:
                                elasticEpsilonTension = CalculateDesignYieldingStrainTensionStructuralSteel(steelSectionsGroup.SteelSectionsMaterial);
                                break;
                        }
                    }
                    // *** Steel sections in tension ***
                    // It can become the P1 point.
                    {
                        double maxSteelSectionsStrain = 0.0;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                maxSteelSectionsStrain = CalculateDesignYieldingStrainTensionStructuralSteel(steelSectionsGroup.SteelSectionsMaterial);
                                break;

                            case FailureDomainTypes.Plastic:
                                maxSteelSectionsStrain = CalculateDesignUltimateStrainTensionStructuralSteel(steelSectionsGroup.SteelSectionsMaterial);
                                break;
                        }
                        var tensionPoint = new DeformationFieldsPoint(maxSteelSectionsStrain, dminStructuralSteelVertex, dminStructuralSteel);
                        tensionPointsList.Add(tensionPoint);
                        tensionPointsListF1.Add(tensionPoint);
                    }
                    // *** Steel sections in compression ***
                    {
                        double minSteelSectionsStrain = 0.0;

                        switch (analysisType)
                        {
                            case FailureDomainTypes.Elastic:
                                minSteelSectionsStrain = CalculateDesignYieldingStrainCompressionStructuralSteel(steelSectionsGroup.SteelSectionsMaterial);
                                break;

                            case FailureDomainTypes.Plastic:
                                minSteelSectionsStrain = CalculateDesignUltimateStrainCompressionStructuralSteel(steelSectionsGroup.SteelSectionsMaterial);
                                break;
                        }
                        compressionPointsList.Add(new DeformationFieldsPoint(minSteelSectionsStrain, dmaxStructuralSteelVertex, dmaxStructuralSteel));
                    }
                }
            }

            // Remove all insignificant points.
            CleanRotationPointsPerMaterial(tensionPointsList.ToArray(), compressionPointsList.ToArray(), out tensionRotationPoints, out compressionRotationPoints);
            CleanRotationPointsPerMaterial(tensionPointsListF1.ToArray(), new DeformationFieldsPoint[] { }, out tensionRotationPointsF1, out _);
        }

        /// <summary>
        /// Remove all insignificant points so that only the effective rotation points remain.
        /// Each point is also associated with a rotation, that is, the minimum rotation in which the rotation point is used.
        /// </summary>
        /// <param name="tensionPoints">Rotation points in tension zone sorted by y-decreasing. The last point is the most distant and tensioned point.</param>
        /// <param name="compressionPoints">Rotation points in the compression zone sorted by y-increase. The last point is the most distant and compressed point.</param>
        internal void CleanRotationPointsPerMaterial(in DeformationFieldsPoint[] tensionPoints, in DeformationFieldsPoint[] compressionPoints,
            out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> compressionRotationPoints)
        {
            // *** Tension ***
            var tensionPointsList = tensionPoints.Distinct(new ComparerEqualRotationPoints()).ToList();
            tensionRotationPoints = new List<DeformationFieldsPoint>();
            if (tensionPoints.Length > 0)
            {
                // Select point with minum tension.
                var firstTensionRotationPoint = tensionPointsList.Aggregate((min, next) => next.Epsilon < min.Epsilon || (next.Epsilon == min.Epsilon && next.Distance < min.Distance) ? next : min);
                firstTensionRotationPoint.Angle = 0.0;
                tensionRotationPoints.Add(firstTensionRotationPoint);
                tensionPointsList.Remove(firstTensionRotationPoint);
                // Deletes all tension points with y greater than or equal to that of the first point.
                tensionPointsList.RemoveAll(p => p.Distance > firstTensionRotationPoint.Distance ||
                    (p.Distance == firstTensionRotationPoint.Distance && p.Epsilon >= firstTensionRotationPoint.Epsilon));
                // Of all remaining points, look for the one that imposes the lowest rotation.
                // This becomes the next point of rotation.
                while (tensionPointsList.Count > 0)
                {
                    // Find the point that imposes the minimum rotation.
                    double minAngle = double.MaxValue;
                    DeformationFieldsPoint minAnglepoint = null;
                    var lastRotationPoint = tensionRotationPoints.Last();
                    foreach (var tensionPoint in tensionPointsList)
                    {
                        double currAngle = (tensionPoint.Epsilon - lastRotationPoint.Epsilon) / (lastRotationPoint.Distance - tensionPoint.Distance);
                        if (currAngle < minAngle)
                        {
                            minAngle = currAngle;
                            minAnglepoint = tensionPoint;
                            minAnglepoint.Angle = minAngle;
                        }
                    }
                    // The point found becomes the next rotation point.
                    tensionRotationPoints.Add(minAnglepoint);
                    tensionPointsList.Remove(minAnglepoint);
                    // Deletes all tension points with y greater than or equal to this rotation point.
                    if (tensionPointsList.Count > 0)
                        tensionPointsList.RemoveAll(p => p.Distance > minAnglepoint.Distance ||
                            (p.Distance == minAnglepoint.Distance && p.Epsilon >= minAnglepoint.Epsilon));
                }
            }

            // *** Compression ***
            var compressionPointsList = compressionPoints.Distinct(new ComparerEqualRotationPoints()).ToList();
            compressionRotationPoints = new List<DeformationFieldsPoint>();
            if (compressionPoints.Length > 0)
            {
                // Select point with maximum compression.
                var firstCompressionRotationPoint = compressionPointsList.Aggregate((max, next) => next.Epsilon > max.Epsilon || (next.Epsilon == max.Epsilon && next.Distance > max.Distance) ? next : max);
                firstCompressionRotationPoint.Angle = 0.0;
                compressionRotationPoints.Add(firstCompressionRotationPoint);
                compressionPointsList.Remove(firstCompressionRotationPoint);
                // Deletes all compression points with y less than or equal to that of the first point.
                compressionPointsList.RemoveAll(p => p.Distance < firstCompressionRotationPoint.Distance);
                // Of all remaining points, look for the one that imposes the lowest rotation.
                // This becomes the next point of rotation.
                while (compressionPointsList.Count > 0)
                {
                    // Find the point that imposes the minimum rotation.
                    double minAngle = double.MaxValue;
                    DeformationFieldsPoint minAnglepoint = null;
                    var lastRotationPoint = compressionRotationPoints.Last();
                    foreach (var compressionPoint in compressionPointsList)
                    {
                        double currAngle = (compressionPoint.Epsilon - lastRotationPoint.Epsilon) / (lastRotationPoint.Distance - compressionPoint.Distance);
                        if (currAngle < minAngle)
                        {
                            minAngle = currAngle;
                            minAnglepoint = compressionPoint;
                            minAnglepoint.Angle = minAngle;
                        }
                    }
                    // The point found becomes the next rotation point.
                    compressionRotationPoints.Add(minAnglepoint);
                    compressionPointsList.Remove(minAnglepoint);
                    // Deletes all tension points with y greater than or equal to this rotation point.
                    if (compressionPointsList.Count > 0)
                        compressionPointsList.RemoveAll(p => p.Distance < minAnglepoint.Distance ||
                            (p.Distance == minAnglepoint.Distance && p.Epsilon <= minAnglepoint.Epsilon));
                }
            }
        }

        internal virtual ForceTuple ConvertToAdimensionalForces(ForceTuple forceTuple)
        {
            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            double fck = Math.Abs(GetFck());
            double denomN = b * h * fck;
            double denomMx = b * h * h * fck;
            double denomMy = b * b * h * fck;

            if (_concreteSection.IsCompositeSteelConcrete)
            {
                foreach (var steelSection in _concreteSection.SteelSections)
                {
                    double fy = fck * 15;
                    Point2d glpobG = steelSection.PositionToGlobal(steelSection.Section.Centroid);
                    denomN += steelSection.Section.Area * fy;
                    denomMx += (steelSection.Section.WelX + steelSection.Section.Area * Math.Abs(glpobG.Y - 0.5 * h)) * fy;
                    denomMy += (steelSection.Section.WelY + steelSection.Section.Area * Math.Abs(glpobG.X - 0.5 * b)) * fy;
                }
            }
            return new ForceTuple(forceTuple.N / denomN, forceTuple.Mx / denomMx, forceTuple.My / denomMy);
        }

        #endregion

        #region Protected method - Failure domain

        /// <summary>
        /// Calculate the failure domain <see cref="FailureDomain"/> of the section
        /// </summary>
        /// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
        /// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
        protected virtual FailureDomain CalculateFailureDomain((StrainPlane, FailureZones, double Immersione)[][] strainPlanes, CoordinateSystem forceCoordinateSystem,
            FailureDomainTypes failureDomainType)
        {
            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[strainPlanes.Length][];
            var rebarIsInsideAssociation = ConcreteSection.GetRebarIsInsideAssociation();

            try
            {
                StrainPlane[] strainPlaneArray = new StrainPlane[strainPlanes.Sum(i => i.Length)];
                ForceTuple[] results = new ForceTuple[strainPlaneArray.Length];

                for (int i = 0; i < strainPlanes.Length; i++)
                {
                    for (int j = 0; j < strainPlanes[i].Length; j++)
                    {
                        strainPlaneArray[j + i * strainPlanes[i].Length] = strainPlanes[i][j].Item1;
                    }
                }

                results = CalculateForceResultantForDomain(strainPlaneArray, rebarIsInsideAssociation);

                Parallel.For(0, strainPlanes.Length, (i) =>
                {
                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];
                    for (int j = 0; j < strainPlanes[i].Length; j++)
                    {
                        domainPoints[i][j] =
                            new FailureDomain.FailureDomainPoint(GetExternalForces(results[j + i * strainPlanes[i].Length], forceCoordinateSystem),
                            strainPlanes[i][j].Item2, strainPlanes[i][j].Item1, strainPlanes[i][j].Immersione);
                    }
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }

            return new FailureDomain(domainPoints, failureDomainType);
        }

        protected virtual (StrainPlane, FailureZones, double Immersione)[][] CalculateDesignFailureStrainPlanes(int tetaDiscretizations, (FailureZones, int)[] zoneSubdivision,
            FailureDomainTypes failureDomainType, double initialAngle = 0)
        {
            if (tetaDiscretizations < 2)
                return null;

            double deltaTeta = 2 * Math.PI / (tetaDiscretizations);
            var strainPlanes = new (StrainPlane, FailureZones, double Immersione)[tetaDiscretizations][];

            try
            {
                Parallel.For(0, tetaDiscretizations, (i) =>
                {
                    strainPlanes[i] = CalculateFailureStrainPlanes((initialAngle + i * deltaTeta), zoneSubdivision, failureDomainType);
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }

            return strainPlanes;
        }

        /// <summary>
        /// Calculate the strain planes for angle <paramref name="teta"/>
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
        /// <returns></returns>
        protected virtual (StrainPlane, FailureZones, double Immersione)[] CalculateFailureStrainPlanes(double teta, (FailureZones, int)[] zoneSubdivision,
            FailureDomainTypes failureDomainType)
        {
            var strainPlanes = new (StrainPlane, FailureZones, double Immersione)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];

            CalculateRotationPointsPerMaterial(teta, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);

            int subIndex = 0;

            for (int i = 0; i < zoneSubdivision.Length; i++)
            {
                FailureZones failureZones = zoneSubdivision[i].Item1;
                int subdivision = zoneSubdivision[i].Item2 + 1;

                for (int j = 0; j < subdivision; j++)
                {
                    double immersione = (double)j / (double)subdivision;
                    strainPlanes[subIndex] = (CalculateStrainPlaneMultiPoints(teta, failureZones, immersione, failureZones == FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, subIndex), failureZones, immersione);
                    subIndex++;
                }

                if (failureZones == FailureZones.F4)
                    strainPlanes[subIndex] = (CalculateStrainPlaneMultiPoints(teta, failureZones, 1.0, tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, subIndex), failureZones, 1.0);
            }

            return strainPlanes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="teta"></param>
        /// <param name="failureIndex"></param>
        /// <param name="immersioneNelCampo"></param>
        /// <param name="tensionRotationPoints"></param>
        /// <param name="compressionRotationPoints"></param>
        /// <param name="minDistanceCompression"></param>
        /// <param name="elasticEpsilonTension">Tension yelding epsilon in most tensioned point.</param>
        /// <param name="id"></param>
        /// <returns></returns>
        internal StrainPlane CalculateStrainPlaneMultiPoints(double teta, FailureZones failureIndex, double immersioneNelCampo,
            List<DeformationFieldsPoint> tensionRotationPoints, List<DeformationFieldsPoint> compressionRotationPoints,
            double minDistanceCompression, double elasticEpsilonTension, int id = -1)
        {
            double chiSx = 0.0, chiDx = 0.0, chi;
            var mostTensionedPoint = tensionRotationPoints.Last();
            var mostCompressedPoint = compressionRotationPoints.Last();
            double epsilonMostTension;
            double epsilonMostCompression;

            var chiF2B_F3Alimit = CalculateF2B_F3Alimit(); // To calculate epsilonMostTension and epsilonMostCompression.

            switch (failureIndex)
            {
                case FailureZones.F1:
                    {
                        chiSx = 0.0;
                        chiDx = CalculateF1_F2Alimit();
                    }
                    break;
                case FailureZones.F2A:
                    {
                        chiSx = CalculateF1_F2Alimit();
                        chiDx = CalculateF2A_F2Blimit();
                    }
                    break;
                case FailureZones.F2B:
                    {
                        chiSx = CalculateF2A_F2Blimit();
                        chiDx = chiF2B_F3Alimit;
                    }
                    break;
                case FailureZones.F3A:
                    {
                        chiSx = chiF2B_F3Alimit;
                        chiDx = CalculateF3A_F3Blimit();
                    }
                    break;
                case FailureZones.F3B:
                    {
                        chiSx = CalculateF3A_F3Blimit();
                        chiDx = CalculateF3B_F4limit();
                    }
                    break;
                case FailureZones.F4:
                    {
                        chiSx = CalculateF3B_F4limit();
                        chiDx = 0.0;
                    }
                    break;
            }
            if (failureIndex != FailureZones.F3A)
                chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
            else
            {
                // Linear increase in compressed area.
                double epsilon_p2 = Math.Abs(epsilonMostCompression);
                double y_sx_F2B = epsilon_p2 / chiSx;
                double y_dx_F3A = epsilon_p2 / chiDx;
                double y_immersione = y_sx_F2B + (y_dx_F3A - y_sx_F2B) * immersioneNelCampo;
                chi = epsilon_p2 / y_immersione;
            }

            DeformationFieldsPoint rotationPoint;

            switch (failureIndex)
            {
                case FailureZones.F1:
                case FailureZones.F2A:
                case FailureZones.F2B:
                    rotationPoint = FindRotationPointTensionZone(chi);
                    break;

                case FailureZones.F3A:
                case FailureZones.F3B:
                case FailureZones.F4:
                    rotationPoint = FindRotationPointCompressionZone(chi);
                    break;

                default:
                    return null;
            }
            return new StrainPlane(rotationPoint.Point, teta, chi, rotationPoint.Epsilon, id);

            // ***************************
            // *** Internal utilities. ***
            double CalculateF1_F2Alimit()
            {
                // The section goes from all tension to a compressed part.
                return FindChiInTensionZone(mostCompressedPoint.Distance, 0.0);
            }

            double CalculateF2A_F2Blimit()
            {
                // Point of transition, at the position with maximum y compressed and minimum epsilon limit.
                return FindChiInTensionZone(mostCompressedPoint.Distance, compressionRotationPoints.First().Epsilon);
            }

            double CalculateF2B_F3Alimit()
            {
                // Maximum angle of transition between control given by traction to compression.
                // Do a search for the maximum angle.
                int indexMostTension = tensionRotationPoints.Count - 1;
                int indexMostCompression = compressionRotationPoints.Count - 1;
                double angle;
                do
                {
                    angle = (tensionRotationPoints[indexMostTension].Epsilon - compressionRotationPoints[indexMostCompression].Epsilon) / (compressionRotationPoints[indexMostCompression].Distance - tensionRotationPoints[indexMostTension].Distance);

                    if (tensionRotationPoints[indexMostTension].Angle <= angle && compressionRotationPoints[indexMostCompression].Angle <= angle)
                        break;
                    else
                    {
                        if (tensionRotationPoints[indexMostTension].Angle == compressionRotationPoints[indexMostCompression].Angle)
                        {
                            indexMostTension--;
                            indexMostCompression--;
                        }
                        else if (tensionRotationPoints[indexMostTension].Angle > compressionRotationPoints[indexMostCompression].Angle)
                        {
                            indexMostTension--;
                        }
                        else
                        {
                            indexMostCompression--;
                        }
                    }
                } while (indexMostTension >= 0 && indexMostCompression >= 0);

                if (indexMostTension != tensionRotationPoints.Count - 1)
                    epsilonMostTension = tensionRotationPoints[indexMostTension].Epsilon + (tensionRotationPoints[indexMostTension].Distance - mostTensionedPoint.Distance) * angle;
                else
                    epsilonMostTension = tensionRotationPoints[indexMostTension].Epsilon;

                if (indexMostCompression != compressionRotationPoints.Count - 1)
                    epsilonMostCompression = compressionRotationPoints[indexMostCompression].Epsilon + (mostCompressedPoint.Distance - compressionRotationPoints[indexMostCompression].Distance) * angle;
                else
                    epsilonMostCompression = compressionRotationPoints[indexMostCompression].Epsilon;

                return angle;
            }

            double CalculateF3A_F3Blimit()
            {
                var minTensionInMostTensionedPoint = Math.Min(epsilonMostTension, elasticEpsilonTension);
                return FindChiInCompressionZone(mostTensionedPoint.Distance, minTensionInMostTensionedPoint);
            }

            double CalculateF3B_F4limit()
            {
                // The section changes to all compressed.
                return FindChiInCompressionZone(minDistanceCompression, 0.0);
            }

            // Find the maximum angle passing through (distance, epsilon) bounded by the tension zone.
            double FindChiInTensionZone(double distance, double epsilon)
            {
                for (int i = tensionRotationPoints.Count - 1; i >= 0; i--)
                {
                    double angle = (epsilon - tensionRotationPoints[i].Epsilon) / (tensionRotationPoints[i].Distance - distance);
                    if (tensionRotationPoints[i].Angle <= angle)
                        return angle;
                }
                return 0.0;
            }

            // Find the maximum angle passing through (distance, epsilon) bounded by the compression zone.
            double FindChiInCompressionZone(double distance, double epsilon)
            {
                for (int i = compressionRotationPoints.Count - 1; i >= 0; i--)
                {
                    double angle = (epsilon - compressionRotationPoints[i].Epsilon) / (compressionRotationPoints[i].Distance - distance);
                    if (compressionRotationPoints[i].Angle <= angle)
                        return angle;
                }
                return 0.0;
            }

            // Find rotation point from chi bounded by the tension zone.
            DeformationFieldsPoint FindRotationPointTensionZone(double chiTension)
            {
                for (int i = tensionRotationPoints.Count - 1; i >= 0; i--)
                {
                    if (tensionRotationPoints[i].Angle <= chiTension)
                        return tensionRotationPoints[i];
                }
                return tensionRotationPoints[0];
            }

            // Find rotation point from chi bounded by the tension zone.
            DeformationFieldsPoint FindRotationPointCompressionZone(double chiCompression)
            {
                for (int i = compressionRotationPoints.Count - 1; i >= 0; i--)
                {
                    if (compressionRotationPoints[i].Angle <= chiCompression)
                        return compressionRotationPoints[i];
                }
                return compressionRotationPoints[0];
            }
        }

        protected virtual FailureDomain2d ConvertFailureDomain3dTo2d(FailureDomain failureDomain)
        {
            if (failureDomain == null)
                throw new ArgumentNullException();
            if (failureDomain.DomainPoints.Length != 2)
                throw new ArgumentException();

            FailureDomain.FailureDomainPoint[] domainPointPositive = failureDomain.DomainPoints[0];
            FailureDomain.FailureDomainPoint[] domainPointNevative = failureDomain.DomainPoints[1];
            Array.Reverse(domainPointNevative);

            List<FailureDomain.FailureDomainPoint> domainPoints = domainPointPositive.ToList();
            domainPoints.AddRange(domainPointNevative);

            return new FailureDomain2d(domainPoints.ToArray(), FailureDomainResult2d.DomainTypes.ConstantMxMy);
        }

        protected virtual ForceTuple CalculateCompressionReduction(ForceTuple force, double limit)
        {
            if (force.N < limit)
                return new ForceTuple(limit, force.Mx, force.My);
            else
                return force;
        }

        #endregion

        #region Protected method - Point on failure domain

        internal StrainPlane BuildPlane(double theta, FailureDomainTypes failureDomainType, SectionSolver.FailureZones failureIndex, double immersione, int id = -1)
        {
            CalculateRotationPointsPerMaterial(theta, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);
            return CalculateStrainPlaneMultiPoints(theta, failureIndex, immersione, failureIndex == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, id);
        }

        internal double GetImmersione(FailureDomain.FailureDomainPoint fail, SectionSolver.FailureZones failureIndex)
        {
            return fail.Immersione != 0.0 || fail.FailureIndex <= failureIndex ? fail.Immersione : 1.0;
        }

        internal StrainPlane InterpolateStrainPlane(ForceTuple forces, FailureDomain.FailureDomainPoint vA, FailureDomain.FailureDomainPoint vB, FailureDomainTypes failureDomainType,
            out FailureDomain.FailureDomainPoint failureDomainPoint)
        {
            double distB = vB.Point.DistanceTo(forces);
            double distA = vA.Point.DistanceTo(forces);
            double weightA = distB / (distA + distB);
            double weightB = distA / (distA + distB);

            // FailureIndex
            FailureZones _failureIndex = (FailureZones)Math.Min((int)vA.FailureIndex, (int)vB.FailureIndex);

            // Theta
            var thetaA = vA.StrainPlane.Teta;
            var thetaB = vB.StrainPlane.Teta;

            // Make them close together.
            if (Math.Abs(thetaA - thetaB) > Math.PI)
            {
                if (thetaA < thetaB)
                    thetaA += 2.0 * Math.PI;
                else
                    thetaB += 2.0 * Math.PI;
            }

            double theta = thetaA * weightA + thetaB * weightB;

            // Immersione
            var immA = GetImmersione(vA, _failureIndex);
            var immB = GetImmersione(vB, _failureIndex);
            double immersione = immA * weightA + immB * weightB;

            StrainPlane strainPlane = BuildPlane(theta, failureDomainType, _failureIndex, immersione);

            var n = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(0, 1, vA.Point.Z, vB.Point.Z, weightA);
            var mx = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(0, 1, vA.Point.X, vB.Point.X, weightA);
            var my = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(0, 1, vA.Point.Y, vB.Point.Y, weightA);

            ForceTuple forceTuple = new ForceTuple(n, mx, my);

            failureDomainPoint = new FailureDomain.FailureDomainPoint(forceTuple, _failureIndex, strainPlane, immersione);
            return strainPlane;
        }

        #endregion

        #region Protected method - Stress SLS

        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double tolerance = 1e-5)
        {
            return CalculateStrainPlaneStressAnalysis(localForces, coordinateSystem, null, null, tolerance);
        }

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(StrainPlane inputStrainPlane, ForceTuple forceTuple,
            Dictionary<int, bool> rebarIsInsideAssociation, double? psiRebars, double? psiTendon)
        {
            ForceTuple forceTupleAdim = ConvertToAdimensionalForces(forceTuple);
            var bBox = ConcreteSection.Shape.GetBoundingBox();

            double deltaChiXLimit = 1.0 / bBox.Size.X;
            double dCX = 0.00001;
            if (forceTupleAdim.Mx != 0)
            {
                dCX = 0.01 * Math.Max(Math.Abs(forceTupleAdim.Mx), 0.00001);
                deltaChiXLimit *= Math.Max(Math.Abs(forceTupleAdim.Mx), 0.001);
            }
            else
                deltaChiXLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dChiX = dCX * deltaChiXLimit;

            double deltaChiYLimit = 1.0 / bBox.Size.Y;
            double dCY = 0.00001;
            if (forceTupleAdim.My != 0)
            {
                dCY = 0.01 * Math.Max(Math.Abs(forceTupleAdim.My), 0.00001);
                deltaChiYLimit *= Math.Max(Math.Abs(forceTupleAdim.My), 0.001);
            }
            else
                deltaChiYLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dChiY = dCY * deltaChiYLimit;

            double deltaStrainLimit = 1.0 / (bBox.Size.X * bBox.Size.Y);
            double dS = 0.00001;
            if (forceTupleAdim.N != 0)
            {
                dS = 0.001 * Math.Max(Math.Abs(forceTupleAdim.N), 0.00001);
                deltaStrainLimit *= Math.Max(Math.Abs(forceTupleAdim.N), 0.001);
            }
            else
                deltaStrainLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dStrain = dS * deltaStrainLimit;


            // derivate parziali rispetto a ChiX
            StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            ForceTuple forcesPlusdChiX;
            ForceTuple forcesMinusdChiX;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusdChiX = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusdChiX, rebarIsInsideAssociation);
                forcesMinusdChiX = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusdChiX, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusdChiX = CalculateForceResultantForTension(strainPlanePlusdChiX, rebarIsInsideAssociation);
                forcesMinusdChiX = CalculateForceResultantForTension(strainPlaneMinusdChiX, rebarIsInsideAssociation);
            }

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            ForceTuple forcesPlusdChiY;
            ForceTuple forcesMinusdChiY;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusdChiY = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusdChiY, rebarIsInsideAssociation);
                forcesMinusdChiY = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusdChiY, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusdChiY = CalculateForceResultantForTension(strainPlanePlusdChiY, rebarIsInsideAssociation);
                forcesMinusdChiY = CalculateForceResultantForTension(strainPlaneMinusdChiY, rebarIsInsideAssociation);
            }

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            ForceTuple forcesPlusStrain;
            ForceTuple forcesMinusStrain;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusStrain = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusStrain, rebarIsInsideAssociation);
                forcesMinusStrain = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusStrain, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusStrain = CalculateForceResultantForTension(strainPlanePlusStrain, rebarIsInsideAssociation);
                forcesMinusStrain = CalculateForceResultantForTension(strainPlaneMinusStrain, rebarIsInsideAssociation);
            }

            double dNdStrain = (forcesPlusStrain.N - forcesMinusStrain.N) / (2.0 * dS);
            double dMxdStrain = (forcesPlusStrain.Mx - forcesMinusStrain.Mx) / (2.0 * dS);
            double dMydStrain = (forcesPlusStrain.My - forcesMinusStrain.My) / (2.0 * dS);


            Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(3, 3);

            partialDerivatives[0, 0] = dNdChiX;
            partialDerivatives[1, 0] = dMxdChiX;
            partialDerivatives[2, 0] = dMydChiX;

            partialDerivatives[0, 1] = dNdChiY;
            partialDerivatives[1, 1] = dMxdChiY;
            partialDerivatives[2, 1] = dMydChiY;

            partialDerivatives[0, 2] = dNdStrain;
            partialDerivatives[1, 2] = dMxdStrain;
            partialDerivatives[2, 2] = dMydStrain;


            Matrix<double> inputVector = Matrix<double>.Build.Dense(3, 1);
            inputVector[0, 0] = forceTuple.N;
            inputVector[1, 0] = forceTuple.Mx;
            inputVector[2, 0] = forceTuple.My;

            Matrix<double> results = partialDerivatives.Inverse() * inputVector;

            double reductionFactor = 1.0;

            return (reductionFactor * results[0, 0] * deltaChiXLimit,
                reductionFactor * results[1, 0] * deltaChiYLimit,
                reductionFactor * results[2, 0] * deltaStrainLimit);
        }

        private StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double? psiRebars, double? psiTendon, double tolerance = 1e-5)
        {
            var rebarIsInsideAssociation = ConcreteSection.GetRebarIsInsideAssociation();
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = _integrationReferencePoint;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces;
            if (psiRebars.HasValue || psiTendon.HasValue)
                iterationForces = GetExternalForces(CalculateForceResultant(psiRebars.Value, psiTendon, strainPlane, rebarIsInsideAssociation), coordinateSystem);
            else
                iterationForces = GetExternalForces(CalculateForceResultantForTension(strainPlane, rebarIsInsideAssociation), coordinateSystem);

            ForceTuple iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);

            if (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance * tolerance)
            {
                do
                {
                    if (id < 50)
                    {
                        try
                        {
                            (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) =
                                CalculateIncrementStressAnalysis(strainPlane, localForces - iterationForces,
                                rebarIsInsideAssociation, psiRebars, psiTendon);

                            // piano di nuovo tentativo
                            id++;
                            chiX += deltaChiX;
                            chiY += deltaChiY;
                            strainReferencePoint += deltaStrainRefPoint;
                            strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                            if (psiRebars.HasValue || psiTendon.HasValue)
                                iterationForces = GetExternalForces(CalculateForceResultant(psiRebars.Value, psiTendon, strainPlane, rebarIsInsideAssociation), coordinateSystem);
                            else
                                iterationForces = GetExternalForces(CalculateForceResultantForTension(strainPlane, rebarIsInsideAssociation), coordinateSystem);

                            iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            _log.Add("Fail to calculate find strain plane");
                            return null;
                        }
                    }
                    else
                    {
                        _log.Add("Fail to calculate find strain plane");
                        return null;
                    }

                } while (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance ||
                     Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance ||
                     Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance);
            }

            return strainPlane;
        }

        #endregion

        #region Protected method - Linear stress method

        protected StrainPlane CalculateStrainPlaneLinearStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double psiRebars, double? psiTendon, double tolerance = 1e-5)
        {
            return CalculateStrainPlaneStressAnalysis(localForces, coordinateSystem, psiRebars, psiTendon, tolerance);
        }

        #endregion

        #region Protected method - Convert forces local/global

        protected virtual ResultBeamForces GetLocalForces(ResultBeamForces externalForces, Vector2d forceReferencePoint)
        {
            return new ResultBeamForces(
                externalForces.N,
                externalForces.V1,
                externalForces.V2,
                externalForces.T,
                externalForces.M1 + externalForces.N * (_integrationReferencePoint.Y - forceReferencePoint.Y),
                externalForces.M2 + externalForces.N * (_integrationReferencePoint.X - forceReferencePoint.X),
                new CoordinateSystem(_integrationReferencePoint, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected ForceTuple GetLocalForces(ForceTuple externalForces, Vector2d forceReferencePoint)
        {
            return new ForceTuple(externalForces.N,
                externalForces.Mx + externalForces.N * (_integrationReferencePoint.Y - forceReferencePoint.Y),
                externalForces.My + externalForces.N * (_integrationReferencePoint.X - forceReferencePoint.X));
        }

        protected virtual ResultBeamForces GetExternalForces(ResultBeamForces localForces, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ResultBeamForces(
                localForces.N,
                localForces.V1,
                localForces.V2,
                localForces.T,
                localForces.M1 + localForces.N * (forceReferenceCoordinateSystem.Origin.Y - _integrationReferencePoint.Y),
                localForces.M2 + localForces.N * (forceReferenceCoordinateSystem.Origin.X - _integrationReferencePoint.X),
                new CoordinateSystem(_integrationReferencePoint, Vector3d.XAxis, Vector3d.YAxis));
        }

        internal virtual ForceTuple GetExternalForces(ForceTuple forceTuple, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ForceTuple(forceTuple.N,
                forceTuple.Mx + forceTuple.N * (forceReferenceCoordinateSystem.Origin.Y - _integrationReferencePoint.Y),
                forceTuple.My - forceTuple.N * (forceReferenceCoordinateSystem.Origin.X - _integrationReferencePoint.X));
        }

        #endregion

        #region Equals - hashcode - operators - serialization

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is SectionSolver solver && _concreteSection.Equals(solver._concreteSection);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_concreteSection);
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _standardStructuralSteel.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ConcreteSection", _concreteSection);
            info.AddValue("Standard", _standard);
            info.AddValue("StandardStructuralSteel", _standardStructuralSteel);
            info.AddValue("IntegrationReferencePoint", _integrationReferencePoint);
            info.AddValue("Log", _log);
            info.AddValue("StressAnalysisTolerance", _stressAnalysisTolerance);
            info.AddValue("FailureAnalysisAngularTolerance", _failureAnalysisAngularTolerance);
            info.AddValue("TetaDiscretization", _tetaDiscretization);
            info.AddValue("GaussIntegrationQuadPoints", _gaussIntegrationQuadPoints);
            info.AddValue("GaussIntegrationTriPoints", _gaussIntegrationTriPoints);
            info.AddValue("ConsiderTensileConcrete", _considerTensileConcrete);
            info.AddValue("SectionOption", _sectionOption);
        }

        public List<string> GetLog()
        {
            return _log;
        }

        #endregion

        #region Nested class

        /// <summary>
        /// Ultity class, to delete duplicate rotation points.
        /// </summary>
        class ComparerEqualRotationPoints : IEqualityComparer<DeformationFieldsPoint>
        {
            public bool Equals(DeformationFieldsPoint x, DeformationFieldsPoint y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null)
                    return false;
                return x.Distance == y.Distance && x.Epsilon == y.Epsilon;
            }

            public int GetHashCode(DeformationFieldsPoint myObject)
            {
                unchecked
                {
                    int hashCode = 23;
                    hashCode = hashCode * -17 + myObject.Distance.GetHashCode();
                    hashCode = hashCode * -17 + myObject.Epsilon.GetHashCode();
                    return hashCode;
                }
            }
        }

        #endregion
    }
}
