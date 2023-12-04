using GPC.Checker.Helper;
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

        #endregion

        #region Variables

        protected double _stressAnalysisTolerance;
        protected double _failureAnalysisAngularTolerance;
        protected double _failureAnalysisDistanceTolerance;
        protected double _failureAnalysisIntersectionTolerance;
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
        protected SectionChecker.SectionOptions _sectionOption;

        #endregion

        #region Properties

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard StandardStructuralSteel => _standardStructuralSteel;

        public bool ConsiderTensileConcrete { get => _considerTensileConcrete; internal set => _considerTensileConcrete = value; }

        public int TetaDiscretization { get => _tetaDiscretization; set => _tetaDiscretization = value; }

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
            _failureAnalysisIntersectionTolerance = 10;

            _considerTensileConcrete = considerTensileConcrete;
            _tetaDiscretization = 64;

            _gaussIntegrationQuadPoints = QuadrangleGaussPoints.GaussPointNumber.Quad400;
            _gaussIntegrationTriPoints = TriangleGaussPoints.GaussPointNumber.Tri79;
            _gaussIntegrationLinePoints = LineGaussPoints.GaussPointNumber.Line32;

            _globalCoordinateGaussPointsMesh = GetMeshGlobalCoordinateGaussPointsLinearShapeFunction();
            _steelSectionsThinWallsBreaked = BreakThinwallAtConcreteIntesections();
            _globalCoordinateGaussPointsThinWalls = GetThinWallsGlobalCoordinateGaussPointsLinearShapeFunction();
            _sectionOption = sectionOption;
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

        public virtual FailureDomainResult GetFailureDomainResult(Checkers.SectionChecker.SectionOptions sectionOption)
        {
            if (sectionOption.FailureDomainType == FailureDomainTypes.Elastic)
                return GetElasticFailureDomainResult(sectionOption);
            else if (sectionOption.FailureDomainType == FailureDomainTypes.Plastic)
                return GetPlasticFailureDomainResult(sectionOption);
            else
                return null;
        }

        public virtual FailureDomainResult2d GetFailureDomainResult2d(Checkers.SectionChecker.SectionOptions sectionOption)
        {
            if (sectionOption.FailureDomainType == FailureDomainTypes.Elastic)
                return GetElasticFailureDomainResult2d(sectionOption);
            else if (sectionOption.FailureDomainType == FailureDomainTypes.Plastic)
                return GetPlasticFailureDomainResult2d(sectionOption);
            else
                return null;
        }

        public virtual FailureDomainResult GetElasticFailureDomainResult(Checkers.SectionChecker.SectionOptions sectionOption)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization, zoneDiscretization, FailureDomainTypes.Elastic);

            return new FailureDomainResult(ConcreteSection,
                CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic),
                null, this, _standard, sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult2d GetElasticFailureDomainResult2d(Checkers.SectionChecker.SectionOptions sectionOption, double angle = 0)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete && _concreteSection.SteelSections.Count == 0)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            var strainPlanes = CalculateDesignFailureStrainPlanes(2, zoneDiscretization, FailureDomainTypes.Elastic, angle);

            return new FailureDomainResult2d(ConcreteSection,
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic).RebuildFailureDomain()),
                null, this, _standard, sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult GetPlasticFailureDomainResult(Checkers.SectionChecker.SectionOptions sectionOption)
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
                CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic), null, this, _standard,
                sectionOption, Id, _standardStructuralSteel);
        }

        public virtual FailureDomainResult2d GetPlasticFailureDomainResult2d(Checkers.SectionChecker.SectionOptions sectionOption, double angle = 0)
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
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic).RebuildFailureDomain()), null, this, _standard,
                sectionOption, Id, _standardStructuralSteel);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, false, null, null, Id,
                    _standardStructuralSteel);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetStressAnalysisResult(ResultBeamForces force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                    CalculateStrainPlaneStressAnalysis(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, false, null, null, Id,
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

        public virtual StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces force, double psi, double? psiTendon, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                CalculateStrainPlaneLinearStressAnalysis(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                sectionOption.ForceReferenceCoordinateSystem, psi, psiTendon,
                _stressAnalysisTolerance), this, _standard, true, psi, psiTendon, Id,
                _standardStructuralSteel);
        }

        internal FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces force, FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var forceToReferenceSystem = force.ToCoordinateSystemWithEccentricity(_sectionOption.ForceReferenceCoordinateSystem);
            if (_sectionOption.FailureDomainType == FailureDomainTypes.Elastic)
                return CalculateElasticDomainPoint(new ForceTuple(forceToReferenceSystem), failureAnalysisTypeOverride);
            else if (_sectionOption.FailureDomainType == FailureDomainTypes.Plastic)
                return CalculatePlasticDomainPoint(new ForceTuple(forceToReferenceSystem), failureAnalysisTypeOverride);
            else
                return null;
        }

        internal FailureDomain.FailureDomainPoint CalculatePlasticDomainPoint(ForceTuple force, FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var failureAnalysisTypes = failureAnalysisTypeOverride is null ? _sectionOption.FailureAnalysisType : failureAnalysisTypeOverride.Value;
            return CalculateDomainPoint(force, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic, failureAnalysisTypes, _failureAnalysisAngularTolerance, _failureAnalysisDistanceTolerance);
        }

        internal FailureDomain.FailureDomainPoint CalculateElasticDomainPoint(ForceTuple force, FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            var failureAnalysisTypes = failureAnalysisTypeOverride is null ? _sectionOption.FailureAnalysisType : failureAnalysisTypeOverride.Value;
            return CalculateDomainPoint(force, _sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic, failureAnalysisTypes, _failureAnalysisAngularTolerance, _failureAnalysisDistanceTolerance);
        }

        internal FailureDomain.FailureDomainPoint CalculateDomainPoint(ResultBeamForces resultBeamForce, Mesh domainMesh,
            Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            return CalculateDomainPoint(domainMesh, resultBeamForce, vertexToDomainPoint, _sectionOption.FailureAnalysisType, _sectionOption.FailureDomainType, _failureAnalysisIntersectionTolerance);
        }

        public virtual FailureDomain.FailureDomainPoint[] CalculateDomainPoint(ResultBeamForces[] force, Mesh domainMesh,
            Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            FailureDomain.FailureDomainPoint[] result = new FailureDomain.FailureDomainPoint[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                result[i] = CalculateDomainPoint(domainMesh, force[i], vertexToDomainPoint, sectionOption.FailureAnalysisType, sectionOption.FailureDomainType, _failureAnalysisIntersectionTolerance);
            });

            return result;
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
        protected virtual ForceTuple CalculateForceResultantForDomain(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
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

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(Vector3d vector, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(new ForceTuple(vector.Z, vector.X, vector.Y), coordinateSystem, failureDomainType, failureAnalysisType,
                _failureAnalysisAngularTolerance, _failureAnalysisDistanceTolerance);
        }

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(targetLocalForces, coordinateSystem, failureDomainType, failureAnalysisType,
                _failureAnalysisAngularTolerance, _failureAnalysisDistanceTolerance);
        }

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType,
            double angularTolerance = 1e-3, double distanceTolerance = 1e-4)
        {
            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    break;
                case FailureAnalysisTypes.ConstantN:
                    if (targetLocalForces.Mx == 0 && targetLocalForces.My == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    if (targetLocalForces.My == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    if (targetLocalForces.Mx == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    break;
                default:
                    break;
            }

            var rebarIsInsideAssociation = ConcreteSection.GetRebarIsInsideAssociation();

            ForceTuple adimOutputForces = ConvertToAdimensionalForces(targetLocalForces);

            Vector3d vectorEd = null;
            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    vectorEd = new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM, targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N);
                    break;
                case FailureAnalysisTypes.ConstantN:
                    vectorEd = new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM, targetLocalForces.My / FROM_KNM_TO_NM, 0.0);
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    vectorEd = new Vector3d(0, targetLocalForces.My / FROM_KNM_TO_NM, 0);
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    vectorEd = new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM, 0, 0);
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    vectorEd = new Vector3d(0, 0, targetLocalForces.N / FROM_KN_TO_N);
                    break;
            }

            // Valori di primo tentativo
            FailureZones failureIndex = FailureZones.F3A;
            double eta = 0.5;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.40;
                    }
                    else if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.50;
                    }
                    else if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.6;
                    }
                    else if (Math.Abs(adimOutputForces.N) < 1e-5)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.60;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-2 && Math.Abs(adimOutputForces.My) < 1e-2)
                    {
                        failureIndex = FailureZones.F3B;
                        eta = 0.8;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.9;
                    }
                    else
                    {
                        failureIndex = FailureZones.F3B;
                        eta = 0.8;
                    }
                    break;
                case FailureAnalysisTypes.ConstantN:
                case FailureAnalysisTypes.ConstantNMx:
                case FailureAnalysisTypes.ConstantNMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.90;
                    }
                    else if (adimOutputForces.N < 0.2)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.95;
                    }
                    else if (adimOutputForces.N < 0.4)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.25;
                    }
                    else if (adimOutputForces.N < 0.6)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.5;
                    }
                    else if (adimOutputForces.N < 1)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.75;
                    }
                    else
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.95;
                    }
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.1;
                    }
                    else
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.5;
                    }
                    break;
            }
            if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
            {
                if (_integrationReferencePoint.Y - ConcreteSection.GetHomogenizedCentroid(out _, out _).Y > 0)
                    teta = Math.PI;
            }

            int id = 1;
            List<DeformationFieldsPoint> tensionRotationPoints, tensionRotationPointsF1, compressionRotationPoints;
            double minDistanceCompression, elasticEpsilonTension;
            CalculateRotationPointsPerMaterial(teta, failureDomainType, out tensionRotationPoints, out tensionRotationPointsF1, out compressionRotationPoints, out minDistanceCompression, out elasticEpsilonTension);
            var strainPlane = CalculateStrainPlaneMultiPoints(teta, failureIndex, eta, failureIndex == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, id);

            teta = strainPlane.Teta;

            ForceTuple forces = GetExternalForces(CalculateForceResultantForDomain(strainPlane, rebarIsInsideAssociation), coordinateSystem);
            ForceTuple adimIncrement = ConvertToAdimensionalForces(forces - targetLocalForces);

            (double deltaTeta, double deltaEta, Vector3d distanceToTarget) increment;

            double angle = -1;
            bool exit = false;
            bool pointOutOfDomain = false;

            var closestPoint = new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);

            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    angle = new Vector3d(forces.Mx / FROM_KNM_TO_NM, forces.My / FROM_KNM_TO_NM, forces.N / FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM,
                        targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N));
                    if (Math.Abs(angle) < angularTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantN:
                    angle = new Vector3d(forces.Mx / FROM_KNM_TO_NM, forces.My / FROM_KNM_TO_NM, 0).AngleTo(new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM,
                        targetLocalForces.My / FROM_KNM_TO_NM, 0));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(angle) < angularTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    angle = new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM, targetLocalForces.My / FROM_KNM_TO_NM, forces.N / FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM,
                        targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.Mx) < distanceTolerance && Math.Abs(adimIncrement.My) < distanceTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    angle = new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM, forces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM,
                        targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(adimIncrement.Mx) < distanceTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    angle = new Vector3d(forces.Mx / FROM_KNM_TO_NM, targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N).AngleTo(new Vector3d(targetLocalForces.Mx / FROM_KNM_TO_NM,
                        targetLocalForces.My / FROM_KNM_TO_NM, targetLocalForces.N / FROM_KN_TO_N));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(adimIncrement.My) < distanceTolerance)
                        exit = true;
                    break;
            }

            if (!exit)
            {
                Line3d externalForcesLine = null;
                switch (failureAnalysisType)
                {
                    case FailureAnalysisTypes.ConstantEccentricity:
                        externalForcesLine = new Line3d(new Point3d(0, 0, 0), targetLocalForces);
                        break;
                    case FailureAnalysisTypes.ConstantN:
                        externalForcesLine = new Line3d(new Point3d(0, 0, targetLocalForces.N), targetLocalForces);
                        break;
                    case FailureAnalysisTypes.ConstantMxMy:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, targetLocalForces.My, 0),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N - FROM_KN_TO_N));
                        break;
                    case FailureAnalysisTypes.ConstantNMx:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, 0, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                    case FailureAnalysisTypes.ConstantNMy:
                        externalForcesLine = new Line3d(new Point3d(0, targetLocalForces.My, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                }

                if (failureAnalysisType == FailureAnalysisTypes.ConstantMxMy)
                {
                    FailureDomain.FailureDomainPoint pointBuffer = CalculateDomainPoint(targetLocalForces, coordinateSystem, failureDomainType,
                        FailureAnalysisTypes.ConstantEccentricity, angularTolerance, distanceTolerance);
                    FailureDomain.FailureDomainForce failureDomainForce = new FailureDomain.FailureDomainForce(
                        new ResultBeamForces(targetLocalForces.N, 0, 0, 0, targetLocalForces.Mx, targetLocalForces.My, coordinateSystem), pointBuffer);

                    double wr = failureDomainForce.CalculateWorkingRatio(failureAnalysisType, FROM_KNM_TO_NM, FROM_KN_TO_N);
                    if (wr > 1)
                        pointOutOfDomain = true;
                }

                do
                {
                    if (id < 200 && !pointOutOfDomain)
                    {
                        try
                        {
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, eta, externalForcesLine, angle,
                                failureDomainType, rebarIsInsideAssociation);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            return null;
                        }
                        SetIncrement(failureDomainType, ref failureIndex, ref teta, ref eta, increment.deltaTeta, increment.deltaEta);

                        id++;
                        CalculateRotationPointsPerMaterial(teta, failureDomainType, out tensionRotationPoints, out tensionRotationPointsF1, out compressionRotationPoints, out minDistanceCompression, out elasticEpsilonTension);
                        strainPlane = CalculateStrainPlaneMultiPoints(teta, failureIndex, eta, failureIndex == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension, id);
                        forces = GetExternalForces(CalculateForceResultantForDomain(strainPlane, rebarIsInsideAssociation), coordinateSystem);

                        ForceTuple incrementForce = new ForceTuple(increment.distanceToTarget.Z, increment.distanceToTarget.X, increment.distanceToTarget.Y);
                        adimIncrement = ConvertToAdimensionalForces(incrementForce);

                        double angleBuffer = Math.PI;

                        switch (failureAnalysisType)
                        {
                            case FailureAnalysisTypes.ConstantEccentricity:
                                angleBuffer = new Vector3d(forces.Mx / FROM_KNM_TO_NM, forces.My / FROM_KNM_TO_NM, forces.N / FROM_KN_TO_N).AngleTo(vectorEd);
                                break;
                            case FailureAnalysisTypes.ConstantN:
                                angleBuffer = new Vector3d(forces.Mx / FROM_KNM_TO_NM, forces.My / FROM_KNM_TO_NM, 0).AngleTo(vectorEd);
                                break;
                            case FailureAnalysisTypes.ConstantNMx:
                            case FailureAnalysisTypes.ConstantNMy:
                            case FailureAnalysisTypes.ConstantMxMy:
                                angleBuffer = new Vector3d((forces.Mx - targetLocalForces.Mx) / FROM_KNM_TO_NM, (forces.My - targetLocalForces.My) / FROM_KNM_TO_NM,
                                    (forces.N - targetLocalForces.N) / FROM_KN_TO_N).AngleTo(vectorEd);
                                break;
                        }

                        if (angleBuffer < angle)
                        {
                            closestPoint = new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);
                            angle = angleBuffer;
                        }

                        if ((Math.Abs(adimIncrement.N) < distanceTolerance &&
                            Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                            Math.Abs(adimIncrement.My) < distanceTolerance))
                            break;
                    }
                    else
                    {
                        _log.Add("Fail to calculate point on domain");
                        if (failureIndex == FailureZones.F2A || failureIndex == FailureZones.F2B)
                        {
                            if (angle < 250 * _failureAnalysisAngularTolerance)
                                return closestPoint;
                        }
                        else if (failureIndex == FailureZones.F3A || failureIndex == FailureZones.F3B || failureIndex == FailureZones.F4)
                        {
                            if (angle < 20 * _failureAnalysisAngularTolerance)
                                return closestPoint;
                        }
                        return null;
                    }

                    switch (failureAnalysisType)
                    {
                        case FailureAnalysisTypes.ConstantEccentricity:
                            if (Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantN:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantMxMy:
                            if (Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                                Math.Abs(adimIncrement.My) < distanceTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantNMx:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance &&
                                Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                                Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantNMy:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance &&
                                Math.Abs(adimIncrement.My) < distanceTolerance &&
                                Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                    }

                } while (!exit);
            }

            return new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane, eta);
        }

        protected (double deltaTeta, double deltaEta, Vector3d distanceToTarget) CalculateIncrement(ForceTuple iterationPoint,
            StrainPlane inputStrainPlane, FailureZones inputFailureZone, double inputImmersioneNelCampo, Line3d externalForcesLine,
            double deltaAngle, FailureDomainTypes failureDomainType, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var adimIteractionPoint = ConvertToAdimensionalForces(iterationPoint);

            double dTeta;
            double dEta;

            switch (inputFailureZone)
            {
                case FailureZones.F1:
                    dTeta = 0.25;
                    dEta = 0.25;
                    break;

                case FailureZones.F2A:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.001);
                    break;

                case FailureZones.F2B:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0005);
                    break;

                case FailureZones.F3A:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(0.01 * Math.Min(deltaAngle, 0.01), 0.00001);
                    break;

                case FailureZones.F3B:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.00001);
                    dEta = Math.Max(0.01 * Math.Min(deltaAngle, 0.01), 0.000001);
                    break;

                default:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    break;
            }

            double dNdTeta;
            double dMxdTeta;
            double dMydTeta;
            double dNdImm;
            double dMxdImm;
            double dMydImm;

            double nonLinearErrorTeta;
            double nonLinearErrorEta;
            double dTetaBuffer = dTeta;
            double dEtaBuffer = dEta;

            int etaCounter = 1;
            int tetaCounter = 1;

            // derivate parziali rispetto a teta
            do
            {
                if (tetaCounter < 10)
                {
                    var tetaPlus = inputStrainPlane.Teta + dTetaBuffer;
                    CalculateRotationPointsPerMaterial(tetaPlus, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPointsPlusdTeta, out List<DeformationFieldsPoint> tensionRotationPointsF1PlusdTeta, out List<DeformationFieldsPoint> compressionRotationPointsPlusdTeta, out double minDistanceCompressionPlusdTeta, out double elasticEpsilonTensionPlusdTeta);
                    StrainPlane strainPlanePlusdTeta = CalculateStrainPlaneMultiPoints(tetaPlus, inputFailureZone, inputImmersioneNelCampo, inputFailureZone == FailureZones.F1 ? tensionRotationPointsF1PlusdTeta : tensionRotationPointsPlusdTeta, compressionRotationPointsPlusdTeta, minDistanceCompressionPlusdTeta, elasticEpsilonTensionPlusdTeta);

                    var tetaMinus = inputStrainPlane.Teta - dTetaBuffer;
                    CalculateRotationPointsPerMaterial(tetaMinus, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPointsMinusdTeta, out List<DeformationFieldsPoint> tensionRotationPointsF1MinusdTeta, out List<DeformationFieldsPoint> compressionRotationPointsMinusdTeta, out double minDistanceCompressionMinusdTeta, out double elasticEpsilonTensionMinusdTeta);
                    StrainPlane strainPlaneMinusdTeta = CalculateStrainPlaneMultiPoints(tetaMinus, inputFailureZone, inputImmersioneNelCampo, inputFailureZone == FailureZones.F1 ? tensionRotationPointsF1MinusdTeta : tensionRotationPointsMinusdTeta, compressionRotationPointsMinusdTeta, minDistanceCompressionMinusdTeta, elasticEpsilonTensionMinusdTeta);

                    var forcesPlusTeta = CalculateForceResultantForDomain(strainPlanePlusdTeta, rebarIsInsideAssociation);
                    var forcesMinusTeta = CalculateForceResultantForDomain(strainPlaneMinusdTeta, rebarIsInsideAssociation);

                    dNdTeta = (forcesPlusTeta.N - forcesMinusTeta.N) / (2.0 * dTetaBuffer);
                    dMxdTeta = (forcesPlusTeta.Mx - forcesMinusTeta.Mx) / (2.0 * dTetaBuffer);
                    dMydTeta = (forcesPlusTeta.My - forcesMinusTeta.My) / (2.0 * dTetaBuffer);

                    dTetaBuffer += 2.0 * dTeta;

                    var adimForcePlusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusTeta.N, forcesPlusTeta.Mx, forcesPlusTeta.My));
                    var adimForceMinusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusTeta.N, forcesMinusTeta.Mx, forcesMinusTeta.My));

                    double nonLinearErrorTetaBuffer = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusTeta.N + adimForceMinusTeta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusTeta.Mx + adimForceMinusTeta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusTeta.My + adimForceMinusTeta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorTetaBuffer) < 0.00001)
                        nonLinearErrorTetaBuffer = 0.00001;

                    nonLinearErrorTeta = Math.Sqrt(Math.Max(Math.Abs(adimForcePlusTeta.N - adimForceMinusTeta.N),
                        Math.Max(Math.Abs(adimForcePlusTeta.Mx - adimForceMinusTeta.Mx),
                        Math.Abs(adimForcePlusTeta.My - adimForceMinusTeta.My))) / Math.Sqrt(nonLinearErrorTetaBuffer));

                    tetaCounter++;
                }
                else
                {
                    if (inputFailureZone == FailureZones.F1)
                        return (+0.5, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.1, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while ((dNdTeta == 0.0 && (dMxdTeta == 0.0 || dMydTeta == 0.0)) || (dMxdTeta == 0.0 && dMydTeta == 0.0));


            // derivate parziali rispetto a immersione nel campo
            do
            {
                if (etaCounter < 10)
                {
                    var immersioneNelCampoNext = inputImmersioneNelCampo + dEtaBuffer;
                    var inputFailureZoneNext = inputFailureZone;
                    var immersioneNelCampoPrev = inputImmersioneNelCampo - dEtaBuffer;
                    var inputFailureZonePrev = inputFailureZone;

                    // With the next two while loops, we want to handle the transition to the next field (for example,
                    // the transition from F2B to F3A) in order to find the tangent.
                    // Problem emerged with tests on ACI.
                    while (immersioneNelCampoNext >= 1.0 && (int)inputFailureZoneNext < 6)
                    {
                        immersioneNelCampoNext -= 1.0;
                        inputFailureZoneNext++;
                    }
                    while (immersioneNelCampoNext < 0.0 && (int)inputFailureZoneNext > 1)
                    {
                        immersioneNelCampoNext += 1.0;
                        inputFailureZoneNext--;
                    }

                    CalculateRotationPointsPerMaterial(inputStrainPlane.Teta, failureDomainType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);

                    StrainPlane strainPlanePlusdImm = CalculateStrainPlaneMultiPoints(inputStrainPlane.Teta, inputFailureZoneNext, Math.Min(immersioneNelCampoNext, 1.0), inputFailureZoneNext == FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);
                    StrainPlane strainPlaneMinusdImm = CalculateStrainPlaneMultiPoints(inputStrainPlane.Teta, inputFailureZonePrev, Math.Max(immersioneNelCampoPrev, 0.0), inputFailureZonePrev == FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);

                    var forcesPlusEta = CalculateForceResultantForDomain(strainPlanePlusdImm, rebarIsInsideAssociation);
                    var forcesMinusEta = CalculateForceResultantForDomain(strainPlaneMinusdImm, rebarIsInsideAssociation);

                    dNdImm = (forcesPlusEta.N - forcesMinusEta.N) / (2.0 * dEtaBuffer);
                    dMxdImm = (forcesPlusEta.Mx - forcesMinusEta.Mx) / (2.0 * dEtaBuffer);
                    dMydImm = (forcesPlusEta.My - forcesMinusEta.My) / (2.0 * dEtaBuffer);

                    dEtaBuffer += 10.0 * dEta;

                    var adimForcePlusEta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusEta.N, forcesPlusEta.Mx, forcesPlusEta.My));
                    var adimForceMinusEta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusEta.N, forcesMinusEta.Mx, forcesMinusEta.My));

                    double nonLinearErrorEtaBuffer = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusEta.N + adimForceMinusEta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusEta.Mx + adimForceMinusEta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusEta.My + adimForceMinusEta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorEtaBuffer) < 0.00001)
                        nonLinearErrorEtaBuffer = 0.00001;

                    nonLinearErrorEta = Math.Sqrt(Math.Max(Math.Abs(adimForcePlusEta.N - adimForceMinusEta.N),
                        Math.Max(Math.Abs(adimForcePlusEta.Mx - adimForceMinusEta.Mx),
                        Math.Abs(adimForcePlusEta.My - adimForceMinusEta.My))) / Math.Sqrt(nonLinearErrorEtaBuffer));

                    etaCounter++;
                }
                else
                {
                    if (inputFailureZone == FailureZones.F1)
                        return (+0.0, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else if (inputFailureZone == FailureZones.F2A)
                        return (+0.0, -0.05, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.0, +0.01, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while ((dNdImm == 0.0 && (dMxdImm == 0.0 || dMydImm == 0.0)) || (dMxdImm == 0.0 && dMydImm == 0.0));

            Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
            v1.Unitize();
            Vector3d v2 = new Vector3d(dMxdImm, dMydImm, dNdImm);
            v2.Unitize();

            // vettore uscente dal punto M di test
            Vector3d gradient = v1 ^ v2;
            gradient.Unitize();

            // k dell'equazione del piano tangente alla superficie in M    //      A*x + B*y + C*z + k = 0
            double k = -(gradient.X * iterationPoint.Mx + gradient.Y * iterationPoint.My + gradient.Z * iterationPoint.N);

            // piano tangente 
            Plane planeTg = new Plane(gradient.X, gradient.Y, gradient.Z, k);

            // punto di intersezione tra raggio delle forze sollecitanti e il piano tangente
            bool intersect = planeTg.IntersectWithRay(externalForcesLine, out Point3d intersectionPoint);

            if (!intersect || double.IsNaN(intersectionPoint.X) || double.IsNaN(intersectionPoint.Y) || double.IsNaN(intersectionPoint.Z))
            {
                if (inputFailureZone == FailureZones.F1)
                    return (+0.5, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                else if (inputFailureZone == FailureZones.F2A)
                    return (+0.01, -0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                else
                    return (+0.01, +0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
            }
            else
            {
                Vector3d displacementVector = new Vector3d(iterationPoint, intersectionPoint);

                Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);
                Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);

                if ((dNdTeta != 0 || dNdImm != 0) && (dMxdTeta != 0 || dMxdImm != 0) &&
                    dMxdTeta * dNdImm - dNdTeta * dMxdImm != 0)
                {
                    partialDerivatives[0, 0] = dMxdTeta;
                    partialDerivatives[1, 0] = dNdTeta;

                    partialDerivatives[0, 1] = dMxdImm;
                    partialDerivatives[1, 1] = dNdImm;

                    inputVector[0, 0] = displacementVector.X;
                    inputVector[1, 0] = displacementVector.Z;
                }
                else if ((dNdTeta != 0 || dNdImm != 0) && (dMydTeta != 0 || dMydImm != 0) &&
                    dMydTeta * dNdImm - dNdTeta * dMydImm != 0)
                {
                    partialDerivatives[0, 0] = dMydTeta;
                    partialDerivatives[1, 0] = dNdTeta;

                    partialDerivatives[0, 1] = dMydImm;
                    partialDerivatives[1, 1] = dNdImm;

                    inputVector[0, 0] = displacementVector.Y;
                    inputVector[1, 0] = displacementVector.Z;
                }
                else
                {
                    partialDerivatives[0, 0] = dMxdTeta;
                    partialDerivatives[1, 0] = dMydTeta;

                    partialDerivatives[0, 1] = dMxdImm;
                    partialDerivatives[1, 1] = dMydImm;

                    inputVector[0, 0] = displacementVector.X;
                    inputVector[1, 0] = displacementVector.Y;
                }

                Matrix<double> results = partialDerivatives.Inverse() * inputVector;

                if (nonLinearErrorEta > 1.0)
                    nonLinearErrorEta = 1.0;
                if (nonLinearErrorTeta > 1.0)
                    nonLinearErrorTeta = 1.0;

                var a = ConvertToAdimensionalForces(new ForceTuple(displacementVector.Z, displacementVector.X, displacementVector.Y));
                var b = new Vector3d(0, a.My, 0);
                var c = new Vector3d(a.Mx, 0, a.N);


                double reductionFactorTeta;
                double reductionFactorEta;

                if (inputFailureZone == FailureZones.F3B)
                {
                    if (c.Length > 0.01)
                        reductionFactorEta = 0.3;
                    else
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.5, 0.2, nonLinearErrorEta);

                    if (b.Length > 0.001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.05, 0.25, nonLinearErrorTeta);
                    else if (b.Length > 0.0001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.25, 0.1, nonLinearErrorTeta);
                    else
                        reductionFactorTeta = 0.05;
                }
                else if (inputFailureZone == FailureZones.F3A)
                {
                    if (c.Length > 0.1)
                        reductionFactorEta = 0.2;
                    else if (c.Length > 0.01)
                        reductionFactorEta = 0.2;
                    else if (c.Length > 0.001)
                        reductionFactorEta = 0.2;
                    else
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.1, 0.1, nonLinearErrorEta);

                    if (b.Length > 0.001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.05, 0.25, nonLinearErrorTeta);
                    else if (b.Length > 0.0001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.25, 0.1, nonLinearErrorTeta);
                    else
                        reductionFactorTeta = 0.05;
                }
                else if (inputFailureZone == FailureZones.F2B)
                {
                    if (c.Length > 0.1)
                        reductionFactorEta = 0.5;
                    else if (c.Length > 0.01)
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.01, 1.0, 0.5, nonLinearErrorEta);
                    else if (c.Length > 0.0025)
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.5, 0.3, nonLinearErrorEta);
                    else
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.3, 0.1, nonLinearErrorEta);

                    if (b.Length > 0.001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.05, 0.25, nonLinearErrorTeta);
                    else if (b.Length > 0.0001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.25, 0.1, nonLinearErrorTeta);
                    else
                        reductionFactorTeta = 0.05;
                }
                else if (inputFailureZone == FailureZones.F2A)
                {
                    if (c.Length > 0.01)
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.01, 1.0, 0.5, nonLinearErrorEta);
                    else if (c.Length > 0.0025)
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.5, 0.3, nonLinearErrorEta);
                    else
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.3, 0.1, nonLinearErrorEta);

                    if (b.Length > 0.001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.05, 0.25, nonLinearErrorTeta);
                    else if (b.Length > 0.0001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.25, 0.1, nonLinearErrorTeta);
                    else
                        reductionFactorTeta = 0.05;
                }
                else
                {
                    if (c.Length > 0.01)
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.01, 1.0, 0.5, nonLinearErrorEta);
                    else
                        reductionFactorEta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.5, 0.2, nonLinearErrorEta);

                    if (b.Length > 0.001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.05, 0.25, nonLinearErrorTeta);
                    else if (b.Length > 0.0001)
                        reductionFactorTeta = Utilities.Maths.Interpolation.GetLinearInterpolation(1.0, 0.001, 0.25, 0.1, nonLinearErrorTeta);
                    else
                        reductionFactorTeta = 0.05;
                }

                // The following condition was calibrated to converge the tests.
                if (_concreteSection.IsCompositeSteelConcrete)
                {
                    //if (/*inputFailureZone == FailureZones.F2A ||*/
                    //    inputFailureZone == FailureZones.F3A || inputFailureZone == FailureZones.F3B)
                    //{
                    //    reductionFactorEta = Math.Min(4.0 * reductionFactorEta, 1.5);
                    //    reductionFactorTeta = Math.Min(4.0 * reductionFactorTeta, 1.5);
                    //}
                    //else if (inputFailureZone == FailureZones.F2B)
                    //{
                    //    reductionFactorEta = Math.Min(2.0 * reductionFactorEta, 1.0);
                    //    reductionFactorTeta = Math.Min(2.0 * reductionFactorTeta, 1.0);
                    //}
                    //else if (inputFailureZone == FailureZones.F1)
                    if (inputFailureZone == FailureZones.F1)
                    {
                        reductionFactorEta *= 0.005;
                        reductionFactorTeta *= 0.005;
                    }
                    else if (inputFailureZone == FailureZones.F2A)
                    {
                        reductionFactorEta *= 0.05;
                        reductionFactorTeta *= 0.1;
                    }
                    else if (inputFailureZone == FailureZones.F2B)
                    {
                        reductionFactorEta *= 0.25;
                    }
                }

                double deltaTeta = results[0, 0] * reductionFactorTeta;
                double deltaEta = results[1, 0] * reductionFactorEta;

                return (deltaTeta, deltaEta, displacementVector);
            }
        }

        protected void SetIncrement(FailureDomainTypes analysisType, ref FailureZones failureZone, ref double teta, ref double eta, double deltaTeta, double deltaEta)
        {
            deltaEta = deltaEta > 0.35 ? 0.35 : deltaEta;
            deltaEta = deltaEta < -0.35 ? -0.35 : deltaEta;

            deltaTeta = deltaTeta > Math.PI / 7.0 ? Math.PI / 7.0 : deltaTeta;
            deltaTeta = deltaTeta < -Math.PI / 7.0 ? -Math.PI / 7.0 : deltaTeta;

            // piano di nuovo tentativo
            teta += deltaTeta;

            if (failureZone == FailureZones.F3B && eta + deltaEta < 0)
            {
                deltaEta *= 0.5;
            }

            eta += (deltaEta - (int)deltaEta);
            failureZone += (int)deltaEta;

            switch (analysisType)
            {
                case FailureDomainTypes.Plastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    if (_concreteSection.ConcreteMaterial.CompressionStressStrainDiagram == ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock)
                    {
                        if (failureZone == FailureZones.F2A)
                        {
                            CalculateRotationPointsPerMaterial(teta, analysisType, out List<DeformationFieldsPoint> tensionRotationPoints, out List<DeformationFieldsPoint> tensionRotationPointsF1, out List<DeformationFieldsPoint> compressionRotationPoints, out double minDistanceCompression, out double elasticEpsilonTension);
                            StrainPlane strainPlane = CalculateStrainPlaneMultiPoints(teta, failureZone, eta, failureZone == SectionSolver.FailureZones.F1 ? tensionRotationPointsF1 : tensionRotationPoints, compressionRotationPoints, minDistanceCompression, elasticEpsilonTension);

                            double strain = strainPlane.GetStrain(compressionRotationPoints.Last().Point);
                            if (strain < _concreteSection.ConcreteMaterial.StrainYCompression)
                            {
                                failureZone++;
                                eta = 0.10;
                            }
                        }
                    }
                    break;

                case FailureDomainTypes.Plastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3A)
                            eta = 0.99;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    break;

                case FailureDomainTypes.Elastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Concrete:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3B || failureZone == FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == FailureZones.F2B)
                            failureZone++;
                    }
                    break;

                case FailureDomainTypes.Elastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3B || failureZone == FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == FailureZones.F3B)
                            failureZone++;

                        if (failureZone == FailureZones.F2B)
                            failureZone++;
                    }
                    break;

                default:
                    throw new Exception();
            }

            failureZone = (int)failureZone < 1 ? FailureZones.F1 : failureZone;
            failureZone = (int)failureZone > 6 ? FailureZones.F4 : failureZone;
        }

        /// <summary>
        /// Given a solicitation finds the point on the strength domain and work rate based on the search method of approaching the surface.
        /// The calculation of the strain plane is by interpolation and is much less accurate than the iterative/direct method.
        /// This method is good for always finding an working ratio, which is always in favor of safety.
        /// If the starting mesh does not have too many elements then it is also a very performing method.
        /// </summary>
        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(Mesh domainMesh, ResultBeamForces resultBeamForce,
            in Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint, FailureAnalysisTypes failureAnalysisType,
            FailureDomainTypes failureDomainType, double lenghtTolerance = GeometryBase.Tolerance)
        {
            double workingRatio = -1;
            SectionSolver.FailureZones _failureIndex = FailureZones.F1;
            double immersione = -1;
            StrainPlane strainPlane = null;
            ForceTuple forceTuple = new ForceTuple();

            Point3d rayOrigin = null; // Must be inside the mesh volume.

            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    rayOrigin = Point3d.Origin;
                    break;

                case FailureAnalysisTypes.ConstantN:
                    rayOrigin = new Point3d(0.0, 0.0, resultBeamForce.N);
                    break;

                case FailureAnalysisTypes.ConstantMxMy:
                    rayOrigin = new Point3d(resultBeamForce.M1, resultBeamForce.M2, 0.0);
                    break;

                case FailureAnalysisTypes.ConstantNMx:
                    rayOrigin = new Point3d(resultBeamForce.M1, 0.0, resultBeamForce.N);
                    break;

                case FailureAnalysisTypes.ConstantNMy:
                    rayOrigin = new Point3d(0.0, resultBeamForce.M2, resultBeamForce.N);
                    break;
            }

            // For some surface approach methods there may not be an intersection, for these cases we need to do a control
            // specifically to change the actual surface approach method used.
            // The origin of the ray rayOrigin will also determine the ratio and must be internal to the domain.
            if (failureAnalysisType != FailureAnalysisTypes.ConstantEccentricity || Point3d.Origin.DistanceTo(rayOrigin) > lenghtTolerance)
            {
                double rayOriginWorkingRatioOrigin = workingRatioSearch(Point3d.Origin, rayOrigin, out _);
                // If the origin point of the ray is outside then enforce the use of ConstantEccentricity.
                if (rayOriginWorkingRatioOrigin >= 1.0)
                    rayOrigin = Point3d.Origin;
            }

            // Now the working ratio search.
            var forcePoint = new Point3d(resultBeamForce.M1, resultBeamForce.M2, resultBeamForce.N);
            workingRatio = workingRatioSearch(rayOrigin, forcePoint, out KeyValuePair<Point3d, MeshBase> intersection);

            // If a solution has been found assigns the deformation plane.
            if (workingRatio != -1 && intersection.Value != null)
            {
                forceTuple = new ForceTuple(intersection.Key.Z, intersection.Key.X, intersection.Key.Y);

                if (intersection.Value is MeshVertex intersectionVertex)
                {
                    var failDomainPoint = vertexToDomainPoint[intersectionVertex];
                    _failureIndex = failDomainPoint.FailureIndex;
                    immersione = failDomainPoint.Immersione;
                    strainPlane = failDomainPoint.StrainPlane;
                }
                else if (intersection.Value is MeshEdge intersectionEdge)
                {
                    // Calculates linear interpolation weights.
                    var vA = domainMesh.Vertices[intersectionEdge.A];
                    var vB = domainMesh.Vertices[intersectionEdge.B];
                    double distB = vB.Point.DistanceTo(forcePoint);
                    double distA = vA.Point.DistanceTo(forcePoint);
                    double weightA = distB / (distA + distB);
                    double weightB = distA / (distA + distB);

                    // Get failure domain points.
                    var failA = vertexToDomainPoint[vA];
                    var failB = vertexToDomainPoint[vB];

                    // Make interpolation.
                    if (failA != null && failB != null)
                    {
                        // FailureIndex
                        _failureIndex = (SectionSolver.FailureZones)Math.Min((int)failA.FailureIndex, (int)failB.FailureIndex);

                        // Theta
                        var thetaA = failA.StrainPlane.Teta;
                        var thetaB = failB.StrainPlane.Teta;
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
                        var immA = GetImmersione(failA, _failureIndex);
                        var immB = GetImmersione(failB, _failureIndex);
                        immersione = immA * weightA + immB * weightB;

                        // StrainPlane
                        strainPlane = BuildPlane(theta, failureDomainType, _failureIndex, immersione);
                    }
                }
                else if (intersection.Value is MeshFace intersectionFace)
                {
                    // Calculates linear interpolation weights.
                    var vA = domainMesh.Vertices[intersectionFace.A];
                    var vB = domainMesh.Vertices[intersectionFace.B];
                    var vC = domainMesh.Vertices[intersectionFace.C];
                    double areaA = new Vector3d((vB.Point - forcePoint) ^ (vC.Point - forcePoint)).Length;
                    double areaB = new Vector3d((vC.Point - forcePoint) ^ (vA.Point - forcePoint)).Length;
                    double areaC = new Vector3d((vA.Point - forcePoint) ^ (vB.Point - forcePoint)).Length;
                    double areaTOT = areaA + areaB + areaC;
                    double weightA = areaA / areaTOT;
                    double weightB = areaB / areaTOT;
                    double weightC = areaC / areaTOT;

                    // Get failure domain points.
                    var failA = vertexToDomainPoint[vA];
                    var failB = vertexToDomainPoint[vB];
                    var failC = vertexToDomainPoint[vC];

                    // Make interpolation.
                    if (failA != null && failB != null && failC != null)
                    {
                        // FailureIndex
                        _failureIndex = (SectionSolver.FailureZones)Math.Min((int)failA.FailureIndex, Math.Min((int)failB.FailureIndex, (int)failC.FailureIndex));

                        // Theta
                        var thetaA = failA.StrainPlane.Teta;
                        var thetaB = failB.StrainPlane.Teta;
                        var thetaC = failC.StrainPlane.Teta;
                        // Make them close together.
                        if (Math.Abs(thetaA - thetaB) > Math.PI)
                        {
                            if (thetaA < thetaB)
                                thetaA += 2.0 * Math.PI;
                            else
                                thetaB += 2.0 * Math.PI;
                        }
                        if (Math.Abs(thetaA - thetaC) > Math.PI)
                        {
                            thetaC += 2.0 * Math.PI;
                        }
                        double theta = thetaA * weightA + thetaB * weightB + thetaC * weightC;

                        // Immersione
                        var immA = GetImmersione(failA, _failureIndex);
                        var immB = GetImmersione(failB, _failureIndex);
                        var immC = GetImmersione(failC, _failureIndex);
                        immersione = immA * weightA + immB * weightB + immC * weightC;

                        // StrainPlane
                        strainPlane = BuildPlane(theta, failureDomainType, _failureIndex, immersione);
                    }
                }
                else
                    return null;
            }

            return new FailureDomain.FailureDomainPoint(forceTuple, _failureIndex, strainPlane, immersione) { WorkingRatio = workingRatio };


            // Internal utility.
            double workingRatioSearch(Point3d pointOrigin, Point3d pointToSearch, out KeyValuePair<Point3d, MeshBase> meshIntersection)
            {
                Point3d targetPoint;
                bool isRatioZero = pointOrigin.DistanceTo(pointToSearch) < lenghtTolerance;

                if (!isRatioZero)
                {
                    targetPoint = pointToSearch;
                }
                else
                {
                    // This is a special case with ratio=0.
                    switch (failureAnalysisType)
                    {
                        case FailureAnalysisTypes.ConstantNMx:
                            targetPoint = pointOrigin + new Point3d(0.0, FROM_KNM_TO_NM, 0.0);
                            break;

                        case FailureAnalysisTypes.ConstantN:
                        case FailureAnalysisTypes.ConstantNMy:
                            targetPoint = pointOrigin + new Point3d(FROM_KNM_TO_NM, 0.0, 0.0);
                            break;

                        case FailureAnalysisTypes.ConstantEccentricity:
                        case FailureAnalysisTypes.ConstantMxMy:
                        default:
                            targetPoint = pointOrigin + new Point3d(0.0, 0.0, FROM_KN_TO_N);
                            break;
                    }
                }
                Line3d semiRay = new Line3d(pointOrigin, targetPoint);
                var intersOnDomain = domainMesh.GetIntersectionWihtSemiInfiniteRay(semiRay, true, lenghtTolerance);
                if (intersOnDomain.Count == 0)
                    return -1;

                // Find the key with the smallest distance and get the corresponding pair from the dictionary.
                var closestEntryOnDomain = intersOnDomain.OrderBy(pair => pair.Key.DistanceTo(pointOrigin)).FirstOrDefault();

                if (closestEntryOnDomain.Key is null)
                    return -1;

                meshIntersection = closestEntryOnDomain;

                if (!isRatioZero)
                    return pointOrigin.DistanceTo(pointToSearch) / pointOrigin.DistanceTo(closestEntryOnDomain.Key);
                else
                    return 0.0;
            }
        }

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

        protected virtual ForceTuple GetExternalForces(ForceTuple forceTuple, CoordinateSystem forceReferenceCoordinateSystem)
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
