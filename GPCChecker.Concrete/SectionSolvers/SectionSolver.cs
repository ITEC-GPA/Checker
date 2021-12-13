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
    public abstract class SectionSolver : ModelObjectId
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
            (FailureZones.F3A, 50),
            (FailureZones.F3B, 3),
            (FailureZones.F4, 2),
            (FailureZones.F5, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 5),
            (FailureZones.F3B, 5),
            (FailureZones.F4, 2),
            (FailureZones.F5, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 1),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 5),
            (FailureZones.F4, 3),
            (FailureZones.F5, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 3),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 5),
            (FailureZones.F4, 5),
            (FailureZones.F5, 5)
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
            /// Around P2. From 0 to 0 
            /// </summary>
            F4 = 6,

            /// <summary>
            /// Around P3. From 0 to espCyCost 
            /// </summary>
            F5 = 7,
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

        #endregion

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;


        public bool ConsiderTensileConcrete { get => _considerTensileConcrete; internal set => _considerTensileConcrete = value; }

        internal SectionSolver(IConcreteSection section, Standard standard, bool considerTensileConcrete, int id)
            : base(id)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _log = new List<string>();

            _stressAnalysisTolerance = 1e-5;
            _failureAnalysisAngularTolerance = 1e-2;

            _considerTensileConcrete = considerTensileConcrete;
            _tetaDiscretization = 64;
        }

        protected SectionSolver(SerializationInfo info, StreamingContext context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
        }


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

        #endregion

        #region Public method

        public virtual FailureDomainResult GetElasticFailureDomainResult(Vector2d forceReferencePointDistance)
        {
            MaterialTypes materialType;
            (FailureZones, int)[] zoneDiscretization;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
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

            return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(strainPlanes, forceReferencePointDistance), null, this, Standard,
                FailureDomainAnalysisTypes.Elastic, Id);
        }

        public virtual FailureDomainResult GetPlasticFailureDomainResult(Vector2d forceReferencePointDistance)
        {
            MaterialTypes materialType;
            (FailureZones, int)[] zoneDiscretization;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            if (materialType == MaterialTypes.Concrete)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;

            if (materialType == MaterialTypes.Concrete)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization,
                zoneDiscretization, FailureDomainAnalysisTypes.Plastic, materialType);

            return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(strainPlanes, forceReferencePointDistance), null, this, Standard,
                FailureDomainAnalysisTypes.Plastic, Id);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, Vector2d forceReferencePointDistance)
        {
            MaterialTypes materialType;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(forceReferencePointDistance), materialType,
                    _stressAnalysisTolerance), this, Standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetStressAnalysisResult(ResultBeamForces force, Vector2d forceReferencePointDistance)
        {
            MaterialTypes materialType;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            return new StressAnalysisResult(ConcreteSection, force,
                    CalculateStrainPlaneStressAnalysis(force.ConvertToForceTuple(forceReferencePointDistance), materialType,
                    _stressAnalysisTolerance), this, Standard, Id);
        }

        public virtual StressAnalysisResult[] GetLinearStressAnalysisResults(ResultBeamForces[] force, double psi, Vector2d forceReferencePointDistance)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneLinearStressAnalysis(force[i].ConvertToForceTuple(forceReferencePointDistance), psi,
                    _stressAnalysisTolerance), this, Standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces force, double psi, Vector2d forceReferencePointDistance)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                CalculateStrainPlaneLinearStressAnalysis(force.ConvertToForceTuple(forceReferencePointDistance), psi,
                _stressAnalysisTolerance), this, Standard, Id);
        }

        internal virtual FailureDomain.FailureDomainPoint CalculatePlasticDomainPoint(ForceTuple targetLocalForces)
		{
            MaterialTypes materialType;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
                materialType = MaterialTypes.FRC;
            else
                materialType = MaterialTypes.Concrete;

            return CalculateDomainPoint(targetLocalForces, FailureDomainAnalysisTypes.Plastic, materialType, _failureAnalysisAngularTolerance);
		}

        internal virtual FailureDomain.FailureDomainPoint CalculateElasticDomainPoint(ForceTuple targetLocalForces)
        {
            MaterialTypes materialType;

            if (ConcreteMaterial.GetType() == typeof(ConcreteMaterialModelCode2010FRC))
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
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriHighPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadHighPoints;
            }
            else if (value > 0.01)
            {
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriMidPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadMidPoints;
            }
            else
            {
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriLowPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadLowPoints;
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
        protected virtual ForceTuple IntegrateRebarStress(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Rebars.Count()];
            double[] deltaMxArray = new double[ConcreteSection.Rebars.Count()];
            double[] deltaMyArray = new double[ConcreteSection.Rebars.Count()];

            var rebars = ConcreteSection.GetRebars();

            Parallel.For(0, rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaS = CalculateStressRebar(rebars[i], strain);
                double sigmaC = CalculateSigmaC(strain);

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
        protected virtual ForceTuple CalculateForceResultant(StrainPlane strainPlane)
        {
            try
            {
                return IntegrateSectionStress(strainPlane) + IntegrateRebarStress(strainPlane);
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
        protected virtual ForceTuple CalculateForceResultant((StrainPlane, FailureZones) strainPlane)
        {
            try
            {
                return IntegrateSectionStress(strainPlane.Item1) + IntegrateRebarStress(strainPlane.Item1);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
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
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriHighPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadHighPoints;
            }
            else if (value > 0.01)
            {
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriMidPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadMidPoints;
            }
            else
            {
                gaussPointsTri = SectionSolverOptions.Instance.GaussIntegrationTriLowPoints;
                gaussPointsQuad = SectionSolverOptions.Instance.GaussIntegrationQuadLowPoints;
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
        protected virtual ForceTuple IntegrateRebarStress(double psi, StrainPlane strainPlane)
        {
            var rebars = ConcreteSection.GetRebars();

            double[] deltaNArray = new double[rebars.Length];
            double[] deltaMxArray = new double[rebars.Length];
            double[] deltaMyArray = new double[rebars.Length];

            Parallel.For(0, rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaC = CalculateElasticSigmaC(strain);
                double sigmaS = CalculateElasticSigmaS(psi, rebars[i], strain);

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
        protected virtual ForceTuple CalculateForceResultant(double psi, StrainPlane strainPlane)
        {
            try
            {
                return IntegrateSectionStressLinearElastic(strainPlane) + IntegrateRebarStress(psi, strainPlane);
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
                if (ConsiderTensileConcrete)
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
            return _concreteSection.ConcreteMaterial.E * (rebar.RebarMaterial.E / (_concreteSection.ConcreteMaterial.E / (1 + psi))) * strain;
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
        protected virtual FailureDomain CalculateFailureDomain((StrainPlane, FailureZones)[][] strainPlanes, Vector2d forceReferencePointDistance)
        {
            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[strainPlanes.Length][];

            try
            {
                Parallel.For(0, strainPlanes.Length, (i) =>
                {
                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];

                    Parallel.For(0, strainPlanes[i].Length, (j) =>
                    {
                        domainPoints[i][j] = new FailureDomain.FailureDomainPoint(GetExternalForces(CalculateForceResultant(strainPlanes[i][j]), forceReferencePointDistance),
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

            return new FailureDomain(domainPoints);
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
                    case FailureZones.F4:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision, 
                                    p1, p2, p3, p4, subIndex), failureZones);
                                subIndex++;
                            }

                            break;
                        }

                    case FailureZones.F5:
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
                    chiDx = Math.Abs(p2.epsilon) / p4.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F4:

                    chiSx = Math.Abs(p2.epsilon) / p4.distanceFromBaricentre;
                    chiDx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F5:

                    chiSx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;
                    chiDx = 0.0;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p3.point, teta, chi, p3.epsilon, id);


                default:
                    return null;
            }
        }

        protected virtual FailureDomain CalculateDomain2D(double teta, FailureDomainAnalysisTypes analysisType,
            MaterialTypes materialType, int subdivision = 20, double angularTolerance = 1.8e-2, double distanceTolerance = 1e-4)
		{
            double mx;
            double my;

            if(Math.Abs(teta) < angularTolerance)
			{
                mx = 100 * 1000000;
                my = 0;
			}
            else if (Math.Abs(teta) - Math.PI / 2.0 < angularTolerance)
            {
                mx = 0;
                my = 100 * 1000000;
            }
			else
			{
                my = 20 * 1000000;
                mx = my * Math.Tan(teta);
            }

            return CalculateDomain2D(new ForceTuple(0, mx, my), analysisType, materialType, subdivision, angularTolerance, distanceTolerance);
		}

        protected virtual FailureDomain CalculateDomain2D(ForceTuple forces, FailureDomainAnalysisTypes analysisType,
            MaterialTypes materialType, int subdivision = 20, double angularTolerance = 1.8e-2, double distanceTolerance = 1e-4)
        {
            if (subdivision <= 2)
                throw new Exception();

            FailureDomain.FailureDomainPoint[][] points = new FailureDomain.FailureDomainPoint[1][];
            points[0] = new FailureDomain.FailureDomainPoint[2 * subdivision + 2];

            ForceTuple[] forceTuples = new ForceTuple[2 * subdivision + 2];

            double nMax = ConcreteSection.AreaRebars * ConcreteSection.Rebars.FirstOrDefault().RebarMaterial.Fyk / 2.0;
            double nMin = ConcreteSection.Area * GetFck() / 2.0;


            for (int i = 0; i <= subdivision / 2.0; i++)
            {
                forceTuples[i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.Mx, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.My, i));

                forceTuples[subdivision / 2 + i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.Mx, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.My, 0.0, i));

                forceTuples[2 * subdivision + 1 - i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.Mx, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.My, i));

                forceTuples[subdivision / 2 + subdivision + 1 - i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.Mx, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.My, 0.0, i));
            }

            for (int i = 0; i < forceTuples.Length; i++)
            {
                Console.WriteLine($"Force {i}: N = {Math.Round(forceTuples[i].N / 1000)} kN, " +
                    $"Mx = {Math.Round(forceTuples[i].Mx / 1000000)} kNm, My = {Math.Round(forceTuples[i].My / 1000000)} kNm");
            }


            for (int i = 0; i < forceTuples.Length; i++)
            //Parallel.For(0, forceTuples.Length, (i) =>
            {
                points[0][i] = CalculateDomainPoint(forceTuples[i], analysisType, materialType, angularTolerance, distanceTolerance);
            }

            return new FailureDomain(points);
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
            ForceTuple adimOutputForces = ConvertToAdimensionalForces(targetLocalForces);
            Vector3d vectorEd = new Vector3d(targetLocalForces.Mx / 1000000, targetLocalForces.My / 1000000, targetLocalForces.N / 1000);

            // Valori di primo tentativo
            FailureZones failureIndex;
            double immersione;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            if(adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
			{
                failureIndex = FailureZones.F1;
                immersione = 0.75;
            }
            else if (adimOutputForces.N > 0.0)
            {
                failureIndex = FailureZones.F3A;
                immersione = 0.25;
            }             
            else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
			{
                failureIndex = FailureZones.F5;
                immersione = 1.0;
            }                
            else if (Math.Abs(adimOutputForces.N) < 1e-5)
            {
                failureIndex = FailureZones.F3A;
                immersione = 0.5;
            }
            else
            {
                failureIndex = FailureZones.F3B;
                immersione = 0.5;
            }

            int id = 1;
            var distances = CalculateMaxMinSectionDistances(teta);

            var p1 = GetP1(distances, analysisType, materialType);
            var p2 = GetP2(distances, analysisType);
            var p3 = GetP3(distances, analysisType);
            var p4 = GetP4(distances, analysisType, materialType);

            StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, p1, p2, p3, p4, id);

            teta = strainPlane.Teta;

            ForceTuple forces = CalculateForceResultant(strainPlane);
            ForceTuple adimIncrement;

            (double deltaTeta, double deltaImmersione, Vector3d distanceToTarget) increment;

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
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, immersione, targetLocalForces, angle, 
                                analysisType, materialType);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            return null;
                        }

                        increment.deltaImmersione = increment.deltaImmersione > 0.4 ? 0.4 : increment.deltaImmersione;
                        increment.deltaImmersione = increment.deltaImmersione < -0.4 ? -0.4 : increment.deltaImmersione;

                        increment.deltaTeta = increment.deltaTeta > Math.PI / 8.0 ? Math.PI / 8.0 : increment.deltaTeta;
                        increment.deltaTeta = increment.deltaTeta < -Math.PI / 8.0 ? -Math.PI / 8.0 : increment.deltaTeta;

                        // piano di nuovo tentativo
                        id++;
                        teta += increment.deltaTeta;

                        immersione += (increment.deltaImmersione - (int)increment.deltaImmersione);
                        failureIndex += (int)increment.deltaImmersione;

                        if (immersione < 0.0)
                        {
                            immersione++;
                            failureIndex--;
                        }
                        if (immersione > 1.0)
                        {
                            immersione--;
                            failureIndex++;
                        }

                        failureIndex = (int)failureIndex < 1 ? FailureZones.F1 : failureIndex;
                        failureIndex = (int)failureIndex > 7 ? FailureZones.F5 : failureIndex;

                        distances = CalculateMaxMinSectionDistances(teta);

                        p1 = GetP1(distances, analysisType, materialType);
                        p2 = GetP2(distances, analysisType);
                        p3 = GetP3(distances, analysisType);
                        p4 = GetP4(distances, analysisType, materialType);

                        strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, p1, p2, p3, p4, id);
                        forces = CalculateForceResultant(strainPlane);

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
                        _log.Add("Fail to calculate point on domain");
                        return null;
                    }
                } while (angle > angularTolerance);
            }

            return new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane);
        }

        protected (double deltaTeta, double deltaImmersione, Vector3d distanceToTarget) CalculateIncrement(ForceTuple iterationPoint,
            StrainPlane inputStrainPlane, FailureZones inputFailureZone, double inputImmersioneNelCampo, ForceTuple externalForces, 
            double deltaAngle, FailureDomainAnalysisTypes analysisType, MaterialTypes materialType)
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

                case FailureZones.F4:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.1), 0.0001);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.1), 0.00001);
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

                    var forcesPlusTeta = CalculateForceResultant(strainPlanePlusdTeta);
                    var forcesMinusTeta = CalculateForceResultant(strainPlaneMinusdTeta);

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

                    var forcesPlusEta = CalculateForceResultant(strainPlanePlusdImm);
                    var forcesMinusEta = CalculateForceResultant(strainPlaneMinusdImm);

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

                if (dTeta > 0.01)
                    dT = Math.Pow(dTeta, 0.5);
                else if (dTeta > 0.001)
                    dT = Math.Pow(dTeta, 0.3);
                else
                    dT = Math.Pow(dTeta, 0.2);

                if (dEta > 0.01)
                    dE = Math.Pow(dEta, 0.5);
                else if (dEta > 0.001)
                    dE = Math.Pow(dEta, 0.3);
                else
                    dE = Math.Pow(dEta, 0.2);

                if (Math.Abs(results[1, 0]) < 0.001)
                    dE = 1.0;
                else if (Math.Abs(results[1, 0]) < 0.01)
                    dE = 0.5;


                double deltaTeta = results[0, 0] * dT / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorTeta), 1.0));
                double deltaImmersione = results[1, 0] * dE / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorEta), 1.0));


                return (deltaTeta, deltaImmersione, displacementVector);
            }
        }

        #endregion

        #region Protected method - Stress SLS

        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, MaterialTypes materialType, double tolerance = 1e-5)
        {
            //ForceTuple targetLocalForces = GetLocalForces(externalForces, forceReferencePoint);
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces = CalculateForceResultant(strainPlane);
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
                                    (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) increment =
                                        CalculateIncrementStressAnalysis(strainPlane, localForces - iterationForces);

                                    // piano di nuovo tentativo
                                    id++;
                                    chiX += increment.deltaChiX;
                                    chiY += increment.deltaChiY;
                                    strainReferencePoint += increment.deltaStrainRefPoint;
                                    strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                                    iterationForces = CalculateForceResultant(strainPlane);
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

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(StrainPlane inputStrainPlane, ForceTuple forceTuple)
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

            var forcesPlusdChiX = CalculateForceResultant(strainPlanePlusdChiX);
            var forcesMinusdChiX = CalculateForceResultant(strainPlaneMinusdChiX);

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiY = CalculateForceResultant(strainPlanePlusdChiY);
            var forcesMinusdChiY = CalculateForceResultant(strainPlaneMinusdChiY);

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            var forcesPlusStrain = CalculateForceResultant(strainPlanePlusStrain);
            var forcesMinusStrain = CalculateForceResultant(strainPlaneMinusStrain);

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
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces = CalculateForceResultant(psi, strainPlane);
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
                            (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) increment =
                                CalculateIncrementLinearStressAnalysis(psi, strainPlane, localForces - iterationForces);

                            // piano di nuovo tentativo
                            id++;
                            chiX += increment.deltaChiX;
                            chiY += increment.deltaChiY;
                            strainReferencePoint += increment.deltaStrainRefPoint;
                            strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                            iterationForces = CalculateForceResultant(psi, strainPlane);
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

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementLinearStressAnalysis(double psi, StrainPlane inputStrainPlane, ForceTuple forceTuple)
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

            var forcesPlusdChiX = CalculateForceResultant(psi, strainPlanePlusdChiX);
            var forcesMinusdChiX = CalculateForceResultant(psi, strainPlaneMinusdChiX);

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiY = CalculateForceResultant(psi, strainPlanePlusdChiY);
            var forcesMinusdChiY = CalculateForceResultant(psi, strainPlaneMinusdChiY);

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            var forcesPlusStrain = CalculateForceResultant(psi, strainPlanePlusStrain);
            var forcesMinusStrain = CalculateForceResultant(psi, strainPlaneMinusStrain);

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
            return new ResultBeamForces(externalForces.N,
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

        protected virtual ResultBeamForces GetExternalForces(ResultBeamForces localForces, Vector2d forceReferencePoint)
        {
            return new ResultBeamForces(localForces.N,
                                        localForces.V1,
                                        localForces.V2,
                                        localForces.T,
                                        localForces.M1 + localForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                        localForces.M2 + localForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X),
                                        new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected virtual ForceTuple GetExternalForces(ForceTuple forceTuple, Vector2d forceReferencePoint)
        {
            return new ForceTuple(forceTuple.N,
                                  forceTuple.Mx + forceTuple.N * (forceReferencePoint.Y),
                                  forceTuple.My - forceTuple.N * (forceReferencePoint.X));
        }

        #endregion

        #region Equals - hashcode - operators - serialization

        public override bool Equals(object obj)
        {
            return obj is SectionSolver solver && _concreteSection.Equals(solver._concreteSection);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 23 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_concreteSection);
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ConcreteSection", _concreteSection);
            info.AddValue("Log", _log);
        }

        #endregion

        public List<string> GetLog()
        {
            return _log;
        }

    }
}
