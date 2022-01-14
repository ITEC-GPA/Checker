using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    [Serializable]
    public abstract class SectionSolver : ModelObjectId, ISerializable
    {
		#region Public enum 

		/// <summary>
		/// Rapresent the discretization of the axial force in the solver for plastic analysis
		/// </summary>
		protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 1),
            (FailureZones.F2A, 1),
            (FailureZones.F2B, 1),
            (FailureZones.F3A, 30),
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
            (FailureZones.F3A, 10),
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
            (FailureZones.F3A, 25),
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
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material with no rebars
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRCNoRebars =
        {
            (FailureZones.F1, 2),
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

        public enum MaterialTypes
		{
            Concrete,
            FRC,
		}

        public enum FailureDomainAnalysisTypes
		{
            Elastic,
            Plastic,
		}

        #endregion

        #region Variables

        protected double _stressAnalysisTolerance;
        protected double _failureAnalysisAngularTolerance;
        protected bool _considerTensileConcrete;

        protected IConcreteSection _concreteSection;
        protected Standard _standard;

        protected List<string> _log;
        protected readonly int _tetaDiscretization;

        protected int _gaussIntegrationQuadLowPoints;
        protected int _gaussIntegrationQuadMidPoints;
        protected int _gaussIntegrationQuadHighPoints;
        protected int _gaussIntegrationTriLowPoints;
        protected int _gaussIntegrationTriMidPoints;
        protected int _gaussIntegrationTriHighPoints;

        #endregion

        #region Properties

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;

        public bool ConsiderTensileConcrete { get => _considerTensileConcrete; internal set => _considerTensileConcrete = value; }

		#endregion

		#region Constructor

		internal SectionSolver(IConcreteSection section, Standard standard, bool considerTensileConcrete, int id)
            : base(id)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _log = new List<string>();

            _stressAnalysisTolerance = 1e-5;
            _failureAnalysisAngularTolerance = 1e-3;

            _considerTensileConcrete = considerTensileConcrete;
            _tetaDiscretization = 32;

            _gaussIntegrationQuadLowPoints = 12;
            _gaussIntegrationQuadMidPoints = 49;
            _gaussIntegrationQuadHighPoints = 400;
            _gaussIntegrationTriLowPoints = 6;
            _gaussIntegrationTriMidPoints = 33;
            _gaussIntegrationTriHighPoints = 79;
        }

        protected SectionSolver(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
            _stressAnalysisTolerance = info.GetDouble("StressAnalysisTolerance");
            _failureAnalysisAngularTolerance = info.GetDouble("FailureAnalysisAngularTolerance");
            _tetaDiscretization = info.GetInt32("TetaDiscretization");
            _gaussIntegrationQuadLowPoints = info.GetInt32("GaussIntegrationQuadLowPoints");
            _gaussIntegrationQuadMidPoints = info.GetInt32("GaussIntegrationQuadMidPoints");
            _gaussIntegrationQuadHighPoints = info.GetInt32("GaussIntegrationQuadHighPoints");
            _gaussIntegrationTriLowPoints = info.GetInt32("GaussIntegrationTriLowPoints");
            _gaussIntegrationTriMidPoints = info.GetInt32("GaussIntegrationTriMidPoints");
            _gaussIntegrationTriHighPoints = info.GetInt32("GaussIntegrationTriHighPoints");
            _considerTensileConcrete = info.GetBoolean("ConsiderTensileConcrete");
        }

		#endregion

		#region Abstract Method

		protected abstract double GetDesignYieldingStrainRebar(ReinforcedConcreteRebar rebar);
        protected abstract double GetDesignYieldingStrainRebar(int rebar);
        protected abstract double GetDesignUltimateStrainRebar(ReinforcedConcreteRebar rebar);
        protected abstract double GetDesignUltimateStrainRebar(int rebar);

        protected abstract double GetUltimateStrainConcreteCompression();
        protected abstract double GetYieldingStrainConcreteCompression();
        protected abstract double GetYieldingStrainPureCompression();
        protected abstract double GetYieldingStrainConcreteTension();
        protected abstract double GetUltimateStrainConcreteTension();

        protected abstract double GetFck();

        /// <returns>The design concrete stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateSigmaC(double strain);

        /// <returns>The design steel stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain);

        protected abstract double GetReductionFactor(StrainPlane strainPlane);

        protected abstract ForceTuple CalculatePureCompressionReduction(ForceTuple force);

        #endregion

        #region Public method

        public virtual FailureDomainResult GetElasticFailureDomainResult(CoordinateSystem forceReferencePointCoordinateSystem)
        {
            MaterialTypes materialType;
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            if (materialType == MaterialTypes.Concrete)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (materialType == MaterialTypes.Concrete)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization,
                zoneDiscretization, FailureDomainAnalysisTypes.Elastic, materialType);

            return new FailureDomainResult(ConcreteSection, 
                CalculateFailureDomain(strainPlanes, forceReferencePointCoordinateSystem, FailureDomainAnalysisTypes.Elastic),
                null, this, _standard, forceReferencePointCoordinateSystem, Id);
        }

        public virtual FailureDomainResult GetPlasticFailureDomainResult(CoordinateSystem forceReferencePointCoordinateSystem)
        {
            MaterialTypes materialType;
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            if (materialType == MaterialTypes.Concrete)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
			{
                if(ConcreteSection.RebarsCount != 0)
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;
                else
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebars;
            }

            if (materialType == MaterialTypes.Concrete)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization,
                zoneDiscretization, FailureDomainAnalysisTypes.Plastic, materialType);

            return new FailureDomainResult(ConcreteSection, 
                CalculateFailureDomain(strainPlanes, forceReferencePointCoordinateSystem, FailureDomainAnalysisTypes.Plastic), null, this, _standard,
                forceReferencePointCoordinateSystem, Id);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, CoordinateSystem forceReferencePointCoordinateSystem)
        {
            MaterialTypes materialType;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(forceReferencePointCoordinateSystem), materialType,
                    _stressAnalysisTolerance), this, _standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetStressAnalysisResult(ResultBeamForces force, CoordinateSystem forceReferencePointCoordinateSystem)
        {
            MaterialTypes materialType;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            return new StressAnalysisResult(ConcreteSection, force,
                    CalculateStrainPlaneStressAnalysis(force.ConvertToForceTuple(forceReferencePointCoordinateSystem), materialType,
                    _stressAnalysisTolerance), this, _standard, Id);
        }

        public virtual StressAnalysisResult[] GetLinearStressAnalysisResults(ResultBeamForces[] force, double psi, CoordinateSystem forceReferencePointCoordinateSystem)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneLinearStressAnalysis(force[i].ConvertToForceTuple(forceReferencePointCoordinateSystem), psi,
                    _stressAnalysisTolerance), this, _standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces force, double psi, CoordinateSystem forceReferencePointCoordinateSystem)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                CalculateStrainPlaneLinearStressAnalysis(force.ConvertToForceTuple(forceReferencePointCoordinateSystem), psi,
                _stressAnalysisTolerance), this, _standard, Id);
        }

        internal virtual FailureDomain.FailureDomainPoint CalculatePlasticDomainPoint(ForceTuple targetLocalForces)
		{
            MaterialTypes materialType;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            return CalculateDomainPoint(targetLocalForces, FailureDomainAnalysisTypes.Plastic, materialType, _failureAnalysisAngularTolerance);
		}

        internal virtual FailureDomain.FailureDomainPoint CalculateElasticDomainPoint(ForceTuple targetLocalForces)
        {
            MaterialTypes materialType;

            if (_concreteSection.ConcreteMaterial.IsFiberReinforced())
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            return CalculateDomainPoint(targetLocalForces, FailureDomainAnalysisTypes.Elastic, materialType, _failureAnalysisAngularTolerance);
        }

        #endregion

        #region Protected method - SectionIntegration

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected virtual ForceTuple IntegrateSectionStress(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

            try
            {
                Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                {
                    var forces = IntegrateFaceStress(ConcreteSection.Mesh.Faces[i + 1], strainPlane);

                    deltaNArray[i] = forces.N;
                    deltaMxArray[i] = forces.Mx;
                    deltaMyArray[i] = forces.My;
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Calculate the resultants of face <paramref name="face"/>
        /// </summary>
        /// <param name="face"></param>
        /// <param name="strainPlane"></param>
        protected virtual ForceTuple IntegrateFaceStress(MeshFace face, StrainPlane strainPlane)
        {
            Point3d[] points = ConcreteSection.Mesh.GetFacePoints(face);

            double value = Math.Abs(ConcreteSection.Mesh.GetFaceArea(face) / ConcreteSection.Area);

            int gaussPointsTri;
            int gaussPointsQuad;

            if (value > 0.1)
            {
                gaussPointsTri = _gaussIntegrationTriHighPoints;
                gaussPointsQuad = _gaussIntegrationQuadHighPoints;
            }
            else if (value > 0.01)
            {
                gaussPointsTri = _gaussIntegrationTriMidPoints;
                gaussPointsQuad = _gaussIntegrationQuadMidPoints;
            }
            else
            {
                gaussPointsTri = _gaussIntegrationTriLowPoints;
                gaussPointsQuad = _gaussIntegrationQuadLowPoints;
            }

            double deltaN;
            double deltaMx;
            double deltaMy;

            if (face.IsTriangle)
            {
                deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))), points, gaussPointsTri);
                deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsTri);
                deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (x - ConcreteSection.Centroid.X), points, gaussPointsTri);
            }
            else if (face.IsQuad)
            {
                deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))),
                    points, gaussPointsQuad);
                deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsQuad);
                deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (x - ConcreteSection.Centroid.X), points, gaussPointsQuad);
            }
            else
                throw new NotSupportedException();

            return new ForceTuple(deltaN, deltaMx, deltaMy);
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        protected virtual ForceTuple IntegrateRebarStress(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            double[] deltaNArray = new double[ConcreteSection.Rebars.Count()];
            double[] deltaMxArray = new double[ConcreteSection.Rebars.Count()];
            double[] deltaMyArray = new double[ConcreteSection.Rebars.Count()];

            var rebars = ConcreteSection.GetRebars();

            Parallel.For(0, rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaS = CalculateStressRebar(rebars[i], strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateSigmaC(strain);

                deltaNArray[i] = (sigmaS - sigmaC) * rebars[i].Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - ConcreteSection.Centroid.X);
            });

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                return CalculatePureCompressionReduction((IntegrateSectionStress(strainPlane) + IntegrateRebarStress(strainPlane, rebarIsInsideAssociation)) * 
                    GetReductionFactor(strainPlane));
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant((StrainPlane, FailureZones) strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            return CalculateForceResultant(strainPlane.Item1, rebarIsInsideAssociation);
        }

        /// <summary>
        /// Calculate the resultants of face <paramref name="face"/>
        /// </summary>
        /// <param name="face"></param>
        /// <param name="strainPlane"></param>
        protected virtual ForceTuple IntegrateFaceStressLinearElastic(MeshFace face, StrainPlane strainPlane)
        {
            Point3d[] points = ConcreteSection.Mesh.GetFacePoints(face);

            double value = Math.Abs(ConcreteSection.Mesh.GetFaceArea(face) / ConcreteSection.Area);

            int gaussPointsTri;
            int gaussPointsQuad;

            if (value > 0.1)
            {
                gaussPointsTri = _gaussIntegrationTriHighPoints;
                gaussPointsQuad = _gaussIntegrationQuadHighPoints;
            }
            else if (value > 0.01)
            {
                gaussPointsTri = _gaussIntegrationTriMidPoints;
                gaussPointsQuad = _gaussIntegrationQuadMidPoints;
            }
            else
            {
                gaussPointsTri = _gaussIntegrationTriLowPoints;
                gaussPointsQuad = _gaussIntegrationQuadLowPoints;
            }

            double deltaN;
            double deltaMx;
            double deltaMy;

            if (face.IsTriangle)
            {
                deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))), points, gaussPointsTri);
                deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsTri);
                deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (x - ConcreteSection.Centroid.X), points, gaussPointsTri);
            }
            else if (face.IsQuad)
            {
                deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))),
                    points, gaussPointsQuad);
                deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsQuad);
                deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateElasticSigmaC(strainPlane.GetStrain(new Point2d(x, y))) *
                    (x - ConcreteSection.Centroid.X), points, gaussPointsQuad);
            }
            else
                throw new NotSupportedException();

            return new ForceTuple(deltaN, deltaMx, deltaMy);
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
            double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

            try
            {
                Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                {
                    var forces = IntegrateFaceStressLinearElastic(ConcreteSection.Mesh.Faces[i + 1], strainPlane);

                    deltaNArray[i] = forces.N;
                    deltaMxArray[i] = forces.Mx;
                    deltaMyArray[i] = forces.My;
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual ForceTuple IntegrateRebarLinearStress(double psi, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var rebars = ConcreteSection.GetRebars();

            double[] deltaNArray = new double[rebars.Length];
            double[] deltaMxArray = new double[rebars.Length];
            double[] deltaMyArray = new double[rebars.Length];

            Parallel.For(0, rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaS = CalculateElasticSigmaS(psi, rebars[i], strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateElasticSigmaC(strain);

                deltaNArray[i] = (sigmaS - sigmaC) * rebars[i].Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - ConcreteSection.Centroid.X);
            });

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> with homogenization coefficient and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(double psi, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                return IntegrateSectionStressLinearElastic(strainPlane) + IntegrateRebarLinearStress(psi, strainPlane, rebarIsInsideAssociation);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        /// <returns>The design concrete stress related to <paramref name="strain"/> with linear elastic stress-strain diagram</returns>
        internal double CalculateElasticSigmaC(double strain)
        {
            if (strain < 0)
            {
                // compressione
                return _concreteSection.ConcreteMaterial.E * strain;
            }
            else
            {
                // trazione
                if (_considerTensileConcrete)
                {
                    return _concreteSection.ConcreteMaterial.E * strain;
                }
                else
                {
                    return 0;
                }
            }

        }

        internal double CalculateElasticSigmaS(double psi, ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.E * (1 + psi) * strain + rebar.RebarMaterial.E * rebar.EpsilonP;
        }

        protected virtual (double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, 
            double dminConcrete, int dMaxVertexIndex, double dmaxConcrete)
            CalculateMaxMinSectionDistances(double teta)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);

            double dminRebar = double.MaxValue;
            double dmaxRebar = double.MinValue;
            double dmaxConcrete = double.MinValue;
            double dminConcrete = double.MaxValue;

            int dMinRebarId = -1;
            int dMaxRebarId = -1;
            int dMaxVertexIndex = -1;
            int dMinVertexIndex = -1;

            var rebars = ConcreteSection.GetRebars();

            for (int r = 0; r < rebars.Length; r++)
            {
                double w1 = (rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta - (rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
                if (w1 <= dminRebar)
                {
                    dminRebar = w1;
                    dMinRebarId = rebars[r].Id;
                }

                if (w1 >= dmaxRebar)
                {
                    dmaxRebar = w1;
                    dMaxRebarId = rebars[r].Id;
                }
            }

            //TODO: implementare con armature lineari

            for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
            {
                double w1 = (ConcreteSection.Shape.Fill[c].Y - ConcreteSection.Centroid.Y) * cosTeta -
                    (ConcreteSection.Shape.Fill[c].X - ConcreteSection.Centroid.X) * sinTeta;

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

            return (teta, dMinRebarId, dminRebar, dMaxRebarId, dmaxRebar, dMinVertexIndex, dminConcrete, dMaxVertexIndex, dmaxConcrete);
        }

        protected virtual ForceTuple ConvertToAdimensionalForces(ForceTuple forceTuple)
        {
            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); ;
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return new ForceTuple(forceTuple.N / (b * h * Math.Abs(GetFck())),
                forceTuple.Mx / (b * h * h * Math.Abs(GetFck())),
                forceTuple.My / (b * b * h * Math.Abs(GetFck())));
        }

        protected Dictionary<int, bool> GetRebarIsInsideAssociation()
        {
            Dictionary<int, bool> kvp = new Dictionary<int, bool>();

            ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();

            for (int i = 0; i < rebars.Length; i++)
            {
                if (ConcreteSection.Shape.IsPointInside(rebars[i].Position))
                    kvp.Add(i, true);
                else
                    kvp.Add(i, false);
            }

            return kvp;
        }

        #endregion

        #region Failure domain limit points

        protected virtual (double epsilon, Point2d point, double distanceFromBaricentre) GetP1((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainAnalysisTypes analysisType, MaterialTypes materialType)
        {
            switch (materialType)
            {
                case MaterialTypes.Concrete:

                    switch (analysisType)
                    {
                        case FailureDomainAnalysisTypes.Elastic:

                            return (GetDesignYieldingStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        case FailureDomainAnalysisTypes.Plastic:

                            return (GetDesignUltimateStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        default:
                            return (0.0, null, 0.0);
                    }

                case MaterialTypes.FRC:

                    switch (analysisType)
                    {
                        case FailureDomainAnalysisTypes.Elastic:

                            return (GetYieldingStrainConcreteTension(), ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        case FailureDomainAnalysisTypes.Plastic:

                            return (GetUltimateStrainConcreteTension(), ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        default:
                            return (0.0, null, 0.0);
                    }

                default:
                    return (0.0, null, 0.0);
            }
        }

        protected (double epsilon, Point2d point, double distanceFromBaricentre) GetP2((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainAnalysisTypes analysisType)
        {
            switch (analysisType)
            {
                case FailureDomainAnalysisTypes.Elastic:

                    return (GetYieldingStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                        (distances.dmaxConcrete - distances.dminRebar));

                case FailureDomainAnalysisTypes.Plastic:

                    return (GetUltimateStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                        (distances.dmaxConcrete - distances.dminRebar));

                default:
                    return (0.0, null, 0.0);
            }
        }

        protected (double epsilon, Point2d point, double distanceFromBaricentre) GetP3((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainAnalysisTypes analysisType)
        {
            switch (analysisType)
            {
                case FailureDomainAnalysisTypes.Elastic:

                    return (GetYieldingStrainPureCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                        (distances.dmaxConcrete - distances.dminConcrete));

                case FailureDomainAnalysisTypes.Plastic:

                    double fraction = GetYieldingStrainPureCompression() / GetUltimateStrainConcreteCompression();
                    double heigth = distances.dmaxConcrete - distances.dminConcrete;

                    Point2d strainPlaneCenter = new Point2d((distances.dmaxConcrete - (1.0 - fraction) * heigth) * (-Math.Sin(distances.teta)) + ConcreteSection.Centroid.X,
                        (distances.dmaxConcrete - (1.0 - fraction) * heigth) * (Math.Cos(distances.teta)) + ConcreteSection.Centroid.Y);

                    return (GetYieldingStrainPureCompression(), strainPlaneCenter, (distances.dmaxConcrete - distances.dminConcrete));

                default:
                    return (0.0, null, 0.0);
            }
        }

        protected virtual (double epsilon, Point2d point, double distanceFromBaricentre) GetP4((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainAnalysisTypes analysisType, MaterialTypes materialType)
        {
            switch (materialType)
            {
                case MaterialTypes.Concrete:

                    switch (analysisType)
                    {
                        case FailureDomainAnalysisTypes.Elastic:

                            return (0.0, ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        case FailureDomainAnalysisTypes.Plastic:

                            return (GetDesignYieldingStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        default:
                            return (0.0, null, 0.0);
                    }

                case MaterialTypes.FRC:

                    switch (analysisType)
                    {
                        case FailureDomainAnalysisTypes.Elastic:

                            return (0.0, ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        case FailureDomainAnalysisTypes.Plastic:

                            return (GetYieldingStrainConcreteTension(), ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        default:
                            return (0.0, null, 0.0);
                    }

                default:
                    return (0.0, null, 0.0);
            }
        }

        #endregion

        #region Protected method - Failure domain

        /// <summary>
        /// Calculate the failure domain <see cref="FailureDomain"/> of the section
        /// </summary>
        /// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
        /// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
        protected virtual FailureDomain CalculateFailureDomain((StrainPlane, FailureZones)[][] strainPlanes, CoordinateSystem forceCoordinateSystem,
            FailureDomainAnalysisTypes analysisType)
        {
            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[strainPlanes.Length][];
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();

            try
            {
                Parallel.For(0, strainPlanes.Length, (i) =>
                {
                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];

                    Parallel.For(0, strainPlanes[i].Length, (j) =>
                    {
                        domainPoints[i][j] = new FailureDomain.FailureDomainPoint(GetExternalForces(CalculateForceResultant(strainPlanes[i][j],
                            rebarIsInsideAssociation), forceCoordinateSystem),
                            strainPlanes[i][j].Item2, strainPlanes[i][j].Item1);
                    });
                });

            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }

            return new FailureDomain(domainPoints, analysisType);
        }

        protected virtual (StrainPlane, FailureZones)[][] CalculateDesignFailureStrainPlanes(int tetaDiscretizations, (FailureZones, int)[] zoneSubdivision,
            FailureDomainAnalysisTypes analysisType, MaterialTypes materialType)
        {
            if (tetaDiscretizations < 2)
                return null;

            double deltaTeta = 2 * Math.PI / (tetaDiscretizations);

            (StrainPlane, FailureZones)[][] strainPlanes = new (StrainPlane, FailureZones)[tetaDiscretizations][];

            try
            {
                Parallel.For(0, tetaDiscretizations, (i) =>
                {
                    strainPlanes[i] = CalculateFailureStrainPlanes((i * deltaTeta), zoneSubdivision, analysisType, materialType);
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
        protected virtual (StrainPlane, FailureZones)[] CalculateFailureStrainPlanes(double teta, (FailureZones, int)[] zoneSubdivision, 
            FailureDomainAnalysisTypes analysisType, MaterialTypes materialType)
        {
            (StrainPlane, FailureZones)[] strainPlanes = new (StrainPlane, FailureZones)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];

            var sectionDistances = CalculateMaxMinSectionDistances(teta);

            var p1 = GetP1(sectionDistances, analysisType, materialType);
            var p2 = GetP2(sectionDistances, analysisType);
            var p3 = GetP3(sectionDistances, analysisType);
            var p4 = GetP4(sectionDistances, analysisType, materialType);

            int subIndex = 0;

            foreach ((FailureZones, int) zone in zoneSubdivision)
            {
                FailureZones failureZones = zone.Item1;
                int subdivision = zone.Item2 + 1;

                switch (failureZones)
                {
                    case FailureZones.F1:
                    case FailureZones.F2A:
                    case FailureZones.F2B:
                    case FailureZones.F3A:
                    case FailureZones.F3B:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision, 
                                    p1, p2, p3, p4, subIndex), failureZones);
                                subIndex++;
                            }

                            break;
                        }

                    case FailureZones.F4:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision, 
                                    p1, p2, p3, p4, subIndex), failureZones);
                                subIndex++;
                            }

                            strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, 1.0, p1, p2, p3, p4, subIndex), failureZones);

                            break;
                        }

                    default:
                        return null;
                }
            }

            return strainPlanes;
        }

        /// <summary>
        /// Calculate the strain plane for angle <paramref name="teta"/> with input <see cref="FailureZones"/> 
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="failureIndex">The index of the failure zone</param>
        /// <param name="immersioneNelCampo"></param>
        /// <param name="distances"></param>
        /// <param name="id"></param>
        /// <returns>Strain Plane</returns>
        /// <remarks>Param distances can be calculated with CalculateMaxMinSectionDistances method</remarks>
        /// <exception cref="ArgumentException"></exception>
        protected virtual StrainPlane CalculateStrainPlane(double teta, FailureZones failureIndex, double immersioneNelCampo,
            (double epsilon, Point2d point, double distanceFromBaricentre) p1, (double epsilon, Point2d point, double distanceFromBaricentre) p2, 
            (double epsilon, Point2d point, double distanceFromBaricentre) p3, (double epsilon, Point2d point, double distanceFromBaricentre) p4, int id = -1)
        {
            if (immersioneNelCampo > 1.0 || immersioneNelCampo < 0.0)
                throw new ArgumentException("ImmersioneNelCampo cannot be greater than 1 and less than 0");

            double chiSx;
            double chiDx;
            double chi;

            switch (failureIndex)
            {
                case FailureZones.F1:

                    chiSx = 0;
                    chiDx = p1.epsilon / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F2A:

                    chiSx = p1.epsilon / p1.distanceFromBaricentre;
                    chiDx = (p1.epsilon + Math.Abs(p3.epsilon)) / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F2B:

                    chiSx = (p1.epsilon + Math.Abs(p3.epsilon)) / p1.distanceFromBaricentre;
                    chiDx = (p1.epsilon + Math.Abs(p2.epsilon)) / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F3A:

                    chiSx = (p1.epsilon + Math.Abs(p2.epsilon)) / p1.distanceFromBaricentre;
                    chiDx = (p4.epsilon + Math.Abs(p2.epsilon)) / p4.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F3B:

                    chiSx = (p4.epsilon + Math.Abs(p2.epsilon)) / p4.distanceFromBaricentre;
                    chiDx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F4:

                    chiSx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;
                    chiDx = 0.0;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p3.point, teta, chi, p3.epsilon, id);


                default:
                    return null;
            }
        }

        #endregion

        #region Protected method - Point on failure domain

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(Vector3d vector, FailureDomainAnalysisTypes analysisType,
            MaterialTypes materialType, double angularTolerance = 1e-3, double distanceTolerance = 1e-4)
		{
            return CalculateDomainPoint(vector, analysisType, materialType, angularTolerance, distanceTolerance);
        }

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, FailureDomainAnalysisTypes analysisType, 
            MaterialTypes materialType, double angularTolerance = 1e-3, double distanceTolerance = 1e-4)
        {
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();

            ForceTuple adimOutputForces = ConvertToAdimensionalForces(targetLocalForces);
            Vector3d vectorEd = new Vector3d(targetLocalForces.Mx / 1000000, targetLocalForces.My / 1000000, targetLocalForces.N / 1000);

            // Valori di primo tentativo
            FailureZones failureIndex;
            double eta;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
            {
                failureIndex = FailureZones.F1;
                eta = 0.75;
            }
            else if (adimOutputForces.N > 0.0)
            {
                failureIndex = FailureZones.F3A;
                eta = 0.25;
            }
            else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
            {
                failureIndex = FailureZones.F4;
                eta = 1.0;
            }
            else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-2 && Math.Abs(adimOutputForces.My) < 1e-2)
            {
                failureIndex = FailureZones.F4;
                eta = 0.5;
            }
            else if (Math.Abs(adimOutputForces.N) < 1e-5)
            {
                failureIndex = FailureZones.F3A;
                eta = 0.75;
            }
            else
            {
                failureIndex = FailureZones.F4;
                eta = 0.1;
            }

            int id = 1;
            var distances = CalculateMaxMinSectionDistances(teta);

            var p1 = GetP1(distances, analysisType, materialType);
            var p2 = GetP2(distances, analysisType);
            var p3 = GetP3(distances, analysisType);
            var p4 = GetP4(distances, analysisType, materialType);

            StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, eta, p1, p2, p3, p4, id);

            teta = strainPlane.Teta;

            ForceTuple forces = CalculateForceResultant(strainPlane, rebarIsInsideAssociation);
            ForceTuple adimIncrement;

            (double deltaTeta, double deltaEta, Vector3d distanceToTarget) increment;

            double angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, forces.N / 1000).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000, 
                targetLocalForces.My / 1000000, targetLocalForces.N / 1000));

            if (angle > angularTolerance)
            {
                do
                {
                    if (id < 100)
                    {
                        try
                        {
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, eta, targetLocalForces, angle,
                                analysisType, materialType, rebarIsInsideAssociation);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            return null;
                        }

                        SetIncrement(analysisType, materialType, ref failureIndex, ref teta, ref eta, increment.deltaTeta, increment.deltaEta);

                        distances = CalculateMaxMinSectionDistances(teta);

                        p1 = GetP1(distances, analysisType, materialType);
                        p2 = GetP2(distances, analysisType);
                        p3 = GetP3(distances, analysisType);
                        p4 = GetP4(distances, analysisType, materialType);

                        id++;
                        strainPlane = CalculateStrainPlane(teta, failureIndex, eta, p1, p2, p3, p4, id);
                        forces = CalculateForceResultant(strainPlane, rebarIsInsideAssociation);

                        ForceTuple incrementForce = new ForceTuple(increment.distanceToTarget.Z, increment.distanceToTarget.X, increment.distanceToTarget.Y);
                        adimIncrement = ConvertToAdimensionalForces(incrementForce);

                        angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, forces.N / 1000).AngleTo(vectorEd);

                        if ((Math.Abs(adimIncrement.N) < distanceTolerance &&
                            Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                            Math.Abs(adimIncrement.My) < distanceTolerance))
                            break;
                    }
                    else
                    {
                        FailureDomain.FailureDomainPoint domainPoint = null;
                        try
                        {
                            FailureDomain.FailureDomainPoint domainPointBuffer = CalculateDomainPoint(targetLocalForces, analysisType,
                                materialType, 10 * angularTolerance, 10 * distanceTolerance);

                            if (domainPointBuffer != null)
                                domainPoint = domainPointBuffer;
                            else
							{
                                _log.Add("Fail to calculate point on domain");
                                return domainPoint;
                            }
                        }
                        catch (Exception)
                        {
                            _log.Add("Fail to calculate point on domain");
                            return domainPoint;
                        }
                    }
                } while (angle > angularTolerance);
            }

            return new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane);
        }

        protected (double deltaTeta, double deltaEta, Vector3d distanceToTarget) CalculateIncrement(ForceTuple iterationPoint,
            StrainPlane inputStrainPlane, FailureZones inputFailureZone, double inputImmersioneNelCampo, ForceTuple externalForces, 
            double deltaAngle, FailureDomainAnalysisTypes analysisType, MaterialTypes materialType, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var adimIteractionPoint = ConvertToAdimensionalForces(iterationPoint);
            Line3d externalForcesLine = new Line3d(new Point3d(0, 0, 0), externalForces);

            double dTeta;
            double dEta;

            switch (inputFailureZone)
            {
                case FailureZones.F1:
                    dTeta = 0.25;
                    dEta = 0.25;
                    break;

                case FailureZones.F2A:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.1), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.1), 0.005);
                    break;

                case FailureZones.F2B:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0005);
                    break;

                case FailureZones.F3A:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.1), 0.0001);
                    dEta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.00001);
                    break;

                case FailureZones.F3B:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.1), 0.00001);
                    dEta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.000001);
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
            double nonLinearError = 0.1;

            double dTetaBuffer = dTeta;
            double dEtaBuffer = dEta;

            int etaCounter = 1;
            int tetaCounter = 1;

            // derivate parziali rispetto a teta
            do
            {
                if (tetaCounter < 50)
                {
                    var distancesPlusTeta = CalculateMaxMinSectionDistances(inputStrainPlane.Teta + dTetaBuffer);
                    var distancesMinusTeta = CalculateMaxMinSectionDistances(inputStrainPlane.Teta - dTetaBuffer);

                    var p1PlusTeta = GetP1(distancesPlusTeta, analysisType, materialType);
                    var p2PlusTeta = GetP2(distancesPlusTeta, analysisType);
                    var p3PlusTeta = GetP3(distancesPlusTeta, analysisType);
                    var p4PlusTeta = GetP4(distancesPlusTeta, analysisType, materialType);

                    var p1MinusTeta = GetP1(distancesMinusTeta, analysisType, materialType);
                    var p2MinusTeta = GetP2(distancesMinusTeta, analysisType);
                    var p3MinusTeta = GetP3(distancesMinusTeta, analysisType);
                    var p4MinusTeta = GetP4(distancesMinusTeta, analysisType, materialType);

                    StrainPlane strainPlanePlusdTeta = CalculateStrainPlane(distancesPlusTeta.teta, inputFailureZone,
                        inputImmersioneNelCampo, p1PlusTeta, p2PlusTeta, p3PlusTeta, p4PlusTeta);
                    StrainPlane strainPlaneMinusdTeta = CalculateStrainPlane(distancesMinusTeta.teta, inputFailureZone,
                        inputImmersioneNelCampo, p1MinusTeta, p2MinusTeta, p3MinusTeta, p4MinusTeta);

                    var forcesPlusTeta = CalculateForceResultant(strainPlanePlusdTeta, rebarIsInsideAssociation);
                    var forcesMinusTeta = CalculateForceResultant(strainPlaneMinusdTeta, rebarIsInsideAssociation);

                    dNdTeta = (forcesPlusTeta.N - forcesMinusTeta.N) / (2.0 * dTetaBuffer);
                    dMxdTeta = (forcesPlusTeta.Mx - forcesMinusTeta.Mx) / (2.0 * dTetaBuffer);
                    dMydTeta = (forcesPlusTeta.My - forcesMinusTeta.My) / (2.0 * dTetaBuffer);

                    dTetaBuffer += dTeta;

                    var adimForcePlusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusTeta.N, forcesPlusTeta.Mx, forcesPlusTeta.My));
                    var adimForceMinusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusTeta.N, forcesMinusTeta.Mx, forcesMinusTeta.My));

                    nonLinearErrorTeta = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusTeta.N + adimForceMinusTeta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusTeta.Mx + adimForceMinusTeta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusTeta.My + adimForceMinusTeta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorTeta) < 0.0001)
                        nonLinearErrorTeta = 0.0001;

                    nonLinearErrorTeta = Math.Sqrt(nonLinearError * Math.Max(Math.Abs(adimIteractionPoint.N),
                        Math.Max(Math.Abs(adimIteractionPoint.Mx), Math.Abs(adimIteractionPoint.My)) /
                        Math.Sqrt(nonLinearErrorTeta)));

                    tetaCounter++;
                }
                else
                    return (+0.1, -0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));

            } while (dNdTeta == 0.0 || (dMxdTeta == 0.0 && dMydTeta == 0.0));


            // derivate parziali rispetto a immersione nel campo
            do
            {
                if (etaCounter < 50)
                {
                    var distances = CalculateMaxMinSectionDistances(inputStrainPlane.Teta);

                    var p1Eta = GetP1(distances, analysisType, materialType);
                    var p2Eta = GetP2(distances, analysisType);
                    var p3Eta = GetP3(distances, analysisType);
                    var p4Eta = GetP4(distances, analysisType, materialType);

                    StrainPlane strainPlanePlusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Min(inputImmersioneNelCampo + dEtaBuffer, 1.0), p1Eta, p2Eta, p3Eta, p4Eta);
                    StrainPlane strainPlaneMinusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Max(inputImmersioneNelCampo - dEtaBuffer, 0.0), p1Eta, p2Eta, p3Eta, p4Eta);

                    var forcesPlusEta = CalculateForceResultant(strainPlanePlusdImm, rebarIsInsideAssociation);
                    var forcesMinusEta = CalculateForceResultant(strainPlaneMinusdImm, rebarIsInsideAssociation);

                    dNdImm = (forcesPlusEta.N - forcesMinusEta.N) / (2.0 * dEtaBuffer);
                    dMxdImm = (forcesPlusEta.Mx - forcesMinusEta.Mx) / (2.0 * dEtaBuffer);
                    dMydImm = (forcesPlusEta.My - forcesMinusEta.My) / (2.0 * dEtaBuffer);

                    dEtaBuffer += dEta;

                    var adimForcePlusEta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusEta.N, forcesPlusEta.Mx, forcesPlusEta.My));
                    var adimForceMinusEta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusEta.N, forcesMinusEta.Mx, forcesMinusEta.My));

                    nonLinearErrorEta = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusEta.N + adimForceMinusEta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusEta.Mx + adimForceMinusEta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusEta.My + adimForceMinusEta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorEta) < 0.0001)
                        nonLinearErrorEta = 0.0001;

                    nonLinearErrorEta = Math.Sqrt(nonLinearError * Math.Max(Math.Abs(adimIteractionPoint.N), Math.Max(Math.Abs(adimIteractionPoint.Mx),
                        Math.Abs(adimIteractionPoint.My)) / Math.Sqrt(nonLinearErrorEta)));

                    etaCounter++;
                }
                else
                    return (0.0, -0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));

            } while (dNdImm == 0.0 || (dMxdImm == 0.0 && dMydImm == 0.0));

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

            if (!intersect)
            {
                return (0.1, 0.3, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
            }
            else
            {
                Vector3d displacementVector = new Vector3d(iterationPoint, intersectionPoint);

                Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);

                partialDerivatives[0, 0] = dMxdTeta;
                partialDerivatives[1, 0] = dNdTeta;

                partialDerivatives[0, 1] = dMxdImm;
                partialDerivatives[1, 1] = dNdImm;

                Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);
                inputVector[0, 0] = displacementVector.X;
                inputVector[1, 0] = displacementVector.Z;

                Matrix<double> results = partialDerivatives.Inverse() * inputVector;


                double dT;
                double dE;

                if (dTeta >= 0.01)
                    dT = 0.05;
                else if (dTeta >= 0.001)
                    dT = 0.15;
                else
                    dT = 0.25;

                if (materialType == MaterialTypes.Concrete)
                {
                    if (inputFailureZone == FailureZones.F3A)
                    {
                        if (dEta >= 0.01)
                            dE = 0.25;
                        else if (dEta >= 0.001)
                            dE = 0.25;
                        else
                            dE = 0.5;
                    }
                    else if (inputFailureZone == FailureZones.F3B)
                    {
                        if (dEta >= 0.01)
                            dE = 0.25;
                        else if (dEta >= 0.001)
                            dE = 0.25;
                        else
                            dE = 0.5;
                    }
                    else
                    {
                        dE = 0.5;
                    }
                }
                else
                {
                    if (inputFailureZone == FailureZones.F3B)                        
                        dE = 0.1;
                    
                    else
                    {
                        if (dEta >= 0.01)
                            dE = 0.25;
                        else
                            dE = 0.5;
                    }
                }

                double deltaTeta = results[0, 0] * dT / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorTeta), 1.0));
                double deltaEta = results[1, 0] * dE / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorEta), 1.0));

                return (deltaTeta, deltaEta, displacementVector);
            }
        }

        protected void SetIncrement(FailureDomainAnalysisTypes analysisType, MaterialTypes materialType, ref FailureZones failureZone, 
            ref double teta, ref double eta, double deltaTeta, double deltaEta)
		{
            deltaEta = deltaEta > 0.4 ? 0.4 : deltaEta;
            deltaEta = deltaEta < -0.3 ? -0.3 : deltaEta;

            deltaTeta = deltaTeta > Math.PI / 8.0 ? Math.PI / 8.0 : deltaTeta;
            deltaTeta = deltaTeta < -Math.PI / 8.0 ? -Math.PI / 8.0 : deltaTeta;

            // piano di nuovo tentativo
            teta += deltaTeta;

            eta += (deltaEta - (int)deltaEta);
            failureZone += (int)deltaEta;

            if (analysisType == FailureDomainAnalysisTypes.Plastic && materialType == MaterialTypes.Concrete)
            {
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
            }
            else if (analysisType == FailureDomainAnalysisTypes.Plastic && materialType == MaterialTypes.FRC)
            {
                if (eta < 0.0)
                {
                    eta++;
                    failureZone--;

                    if (failureZone == FailureZones.F3A)					
                        eta = 0.98;                    
                }
                if (eta > 1.0)
                {
                    eta--;
                    failureZone++;
                }
            }
            else if (analysisType == FailureDomainAnalysisTypes.Elastic && materialType == MaterialTypes.Concrete)
            {
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
            }
            else if (analysisType == FailureDomainAnalysisTypes.Elastic && materialType == MaterialTypes.FRC)
            {
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
            }
            else
                throw new Exception();

            failureZone = (int)failureZone < 1 ? FailureZones.F1 : failureZone;
            failureZone = (int)failureZone > 6 ? FailureZones.F4 : failureZone;
        }

        #endregion

        #region Protected method - Stress SLS

        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, MaterialTypes materialType, double tolerance = 1e-5)
        {
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces = CalculateForceResultant(strainPlane, rebarIsInsideAssociation);
            ForceTuple iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);

            if (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance * tolerance)
            {
                FailureDomain.FailureDomainPoint pointOnDomain = CalculateDomainPoint(localForces, 
                    FailureDomainAnalysisTypes.Plastic, materialType, 2.0 * _failureAnalysisAngularTolerance);

                if(pointOnDomain != null)
				{
                    Vector3d vEd = new Vector3d(localForces, Point3d.Origin);
                    Vector3d vRd = new Vector3d(pointOnDomain.Point, Point3d.Origin);

                    if (vEd.Length < vRd.Length)
                    {
                        do
                        {
                            if (id < 50)
                            {

                                try
                                {
                                    (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) =
                                        CalculateIncrementStressAnalysis(strainPlane, localForces - iterationForces, 
                                        rebarIsInsideAssociation);

                                    // piano di nuovo tentativo
                                    id++;
                                    chiX += deltaChiX;
                                    chiY += deltaChiY;
                                    strainReferencePoint += deltaStrainRefPoint;
                                    strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                                    iterationForces = CalculateForceResultant(strainPlane, rebarIsInsideAssociation);
                                    iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);
                                }
                                catch (Exception e)
                                {
                                    _log.Add(e.Message);
                                    if (e.InnerException != null)
                                        _log.Add(e.InnerException.Message);
                                    throw;
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
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    _log.Add("Fail to calculate find strain plane");
                    return null;
                }
            }

            return strainPlane;
        }

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(StrainPlane inputStrainPlane, ForceTuple forceTuple,
            Dictionary<int, bool> rebarIsInsideAssociation)
        {
            ForceTuple forceTupleAdim = ConvertToAdimensionalForces(forceTuple);

            double deltaChiXLimit = Math.Abs(GetYieldingStrainPureCompression() / ConcreteSection.Shape.GetBoundingBox().Size.X);
            double dCX = 0.00001;
            if (forceTupleAdim.Mx != 0)
                dCX = 0.001 * Math.Max(Math.Abs(forceTupleAdim.Mx), 0.00001);

            double dChiX = dCX * deltaChiXLimit;

            double deltaChiYLimit = Math.Abs(GetYieldingStrainPureCompression() / ConcreteSection.Shape.GetBoundingBox().Size.Y);
            double dCY = 0.00001;
            if (forceTupleAdim.My != 0)
                dCY = 0.001 * Math.Max(Math.Abs(forceTupleAdim.My), 0.00001);

            double dChiY = dCY * deltaChiYLimit;

            double deltaStrainLimit = 1.0 / ConcreteSection.Area * Math.Abs(GetFck());
            double dS = 0.00001;
            if (forceTupleAdim.N != 0)
                dS = 0.0001 * Math.Max(Math.Abs(forceTupleAdim.N), 0.00001);

            double dStrain = dS * deltaStrainLimit;


            // derivate parziali rispetto a ChiX
            StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiX = CalculateForceResultant(strainPlanePlusdChiX, rebarIsInsideAssociation);
            var forcesMinusdChiX = CalculateForceResultant(strainPlaneMinusdChiX, rebarIsInsideAssociation);

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiY = CalculateForceResultant(strainPlanePlusdChiY, rebarIsInsideAssociation);
            var forcesMinusdChiY = CalculateForceResultant(strainPlaneMinusdChiY, rebarIsInsideAssociation);

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            var forcesPlusStrain = CalculateForceResultant(strainPlanePlusStrain, rebarIsInsideAssociation);
            var forcesMinusStrain = CalculateForceResultant(strainPlaneMinusStrain, rebarIsInsideAssociation);

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

            return (results[0, 0] * deltaChiXLimit, results[1, 0] * deltaChiYLimit, results[2, 0] * deltaStrainLimit);
        }


        #endregion

        #region Protected method - Linear stress method

        protected StrainPlane CalculateStrainPlaneLinearStressAnalysis(ForceTuple localForces, double psi, double tolerance = 1e-5)
        {
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces = CalculateForceResultant(psi, strainPlane, rebarIsInsideAssociation);
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
                                CalculateIncrementLinearStressAnalysis(psi, strainPlane, localForces - iterationForces,
                                rebarIsInsideAssociation);

                            // piano di nuovo tentativo
                            id++;
                            chiX += deltaChiX;
                            chiY += deltaChiY;
                            strainReferencePoint += deltaStrainRefPoint;
                            strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                            iterationForces = CalculateForceResultant(psi, strainPlane, rebarIsInsideAssociation);
                            iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            throw;
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

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementLinearStressAnalysis(double psi, 
            StrainPlane inputStrainPlane, ForceTuple forceTuple, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            ForceTuple forceTupleAdim = ConvertToAdimensionalForces(forceTuple);

            double deltaChiXLimit = Math.Abs(GetYieldingStrainPureCompression() / ConcreteSection.Shape.GetBoundingBox().Size.X);
            double dCX = 0.00001;
            if (forceTupleAdim.Mx != 0)
                dCX = 0.001 * Math.Max(Math.Abs(forceTupleAdim.Mx), 0.00001);

            double dChiX = dCX * deltaChiXLimit;

            double deltaChiYLimit = Math.Abs(GetYieldingStrainPureCompression() / ConcreteSection.Shape.GetBoundingBox().Size.Y);
            double dCY = 0.00001;
            if (forceTupleAdim.My != 0)
                dCY = 0.001 * Math.Max(Math.Abs(forceTupleAdim.My), 0.00001);

            double dChiY = dCY * deltaChiYLimit;

            double deltaStrainLimit = 1.0 / ConcreteSection.Area * Math.Abs(GetFck());
            double dS = 0.00001;
            if (forceTupleAdim.N != 0)
                dS = 0.0001 * Math.Max(Math.Abs(forceTupleAdim.N), 0.00001);

            double dStrain = dS * deltaStrainLimit;


            // derivate parziali rispetto a ChiX
            StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiX = CalculateForceResultant(psi, strainPlanePlusdChiX, rebarIsInsideAssociation);
            var forcesMinusdChiX = CalculateForceResultant(psi, strainPlaneMinusdChiX, rebarIsInsideAssociation);

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiY = CalculateForceResultant(psi, strainPlanePlusdChiY, rebarIsInsideAssociation);
            var forcesMinusdChiY = CalculateForceResultant(psi, strainPlaneMinusdChiY, rebarIsInsideAssociation);

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            var forcesPlusStrain = CalculateForceResultant(psi, strainPlanePlusStrain, rebarIsInsideAssociation);
            var forcesMinusStrain = CalculateForceResultant(psi, strainPlaneMinusStrain, rebarIsInsideAssociation);

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

            return (results[0, 0] * deltaChiXLimit, results[1, 0] * deltaChiYLimit, results[2, 0] * deltaStrainLimit);
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
                externalForces.M1 + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                externalForces.M2 + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X),
                new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected ForceTuple GetLocalForces(ForceTuple externalForces, Vector2d forceReferencePoint)
        {
            return new ForceTuple(externalForces.N,
                externalForces.Mx + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                externalForces.My + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X));
        }

        protected virtual ResultBeamForces GetExternalForces(ResultBeamForces localForces, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ResultBeamForces(
                localForces.N,
                localForces.V1,
                localForces.V2,
                localForces.T,
                localForces.M1 + localForces.N * (forceReferenceCoordinateSystem.Origin.Y - ConcreteSection.Centroid.Y),
                localForces.M2 + localForces.N * (forceReferenceCoordinateSystem.Origin.X - ConcreteSection.Centroid.X),
                new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected virtual ForceTuple GetExternalForces(ForceTuple forceTuple, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ForceTuple(forceTuple.N,
                forceTuple.Mx + forceTuple.N * (forceReferenceCoordinateSystem.Origin.Y - ConcreteSection.Centroid.Y),
                forceTuple.My - forceTuple.N * (forceReferenceCoordinateSystem.Origin.X - ConcreteSection.Centroid.X));
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
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ConcreteSection", _concreteSection);
            info.AddValue("Standard", _standard);
            info.AddValue("Log", _log);
            info.AddValue("StressAnalysisTolerance", _stressAnalysisTolerance);
            info.AddValue("FailureAnalysisAngularTolerance", _failureAnalysisAngularTolerance);
            info.AddValue("TetaDiscretization", _tetaDiscretization);
            info.AddValue("GaussIntegrationQuadLowPoints", _gaussIntegrationQuadLowPoints);
            info.AddValue("GaussIntegrationQuadMidPoints", _gaussIntegrationQuadMidPoints);
            info.AddValue("GaussIntegrationQuadHighPoints", _gaussIntegrationQuadHighPoints);
            info.AddValue("GaussIntegrationTriLowPoints", _gaussIntegrationTriLowPoints);
            info.AddValue("GaussIntegrationTriMidPoints", _gaussIntegrationTriMidPoints);
            info.AddValue("GaussIntegrationTriHighPoints", _gaussIntegrationTriHighPoints);
            info.AddValue("ConsiderTensileConcrete", _considerTensileConcrete);
        }
        
        public List<string> GetLog()
        {
            return _log;
        }

        #endregion

    }
}
