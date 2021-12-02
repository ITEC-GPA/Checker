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

        #region Variables
        
        protected double _stressAnalysisTolerance;
        protected double _failureAnalysisTolerance;
        protected bool _considerTensileConcrete;

        protected IConcreteSection _concreteSection;
        protected Standard _standard;

        protected List<string> _log; 
        
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
            _failureAnalysisTolerance = 1e-3;

            _considerTensileConcrete = considerTensileConcrete;
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

        public virtual FailureDomainResult GetFailureElasticDomainResult()
        {
            return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(SectionSolverOptions.Instance.MomentsDiscretizations,
                                            SectionSolverOptions.Instance.FailureZonesDiscretizations), null, this, Standard, Id);
        }

        public virtual FailureDomainResult GetFailurePlasticDomainResults()
        {


            return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(SectionSolverOptions.Instance.MomentsDiscretizations,
                                            SectionSolverOptions.Instance.FailureZonesDiscretizations), null, this, Standard, Id);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, Vector2d forceReferencePointDistance)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(forceReferencePointDistance),
                    _stressAnalysisTolerance), this, Standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResult(ResultBeamForces[] force, double n, Vector2d forceReferencePointDistance)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneNMethodAnalysis(force[i].ConvertToForceTuple(forceReferencePointDistance), n,
                    _stressAnalysisTolerance), this, Standard, Id);
            });

            return stressAnalysisResults;
        }

        internal virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces)
        {
            return CalculateDomainPoint(targetLocalForces, _failureAnalysisTolerance);
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
        protected virtual ForceTuple IntegrateFaceStress(double n, MeshFace face, StrainPlane strainPlane)
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
        protected virtual ForceTuple IntegrateSectionStress(double n, StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

            try
            {
                Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                {
                    var forces = IntegrateFaceStress(n, ConcreteSection.Mesh.Faces[i + 1], strainPlane);

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
        protected virtual ForceTuple IntegrateRebarStress(double n, StrainPlane strainPlane)

        {
            var rebars = ConcreteSection.GetRebars();

            double[] deltaNArray = new double[rebars.Length];
            double[] deltaMxArray = new double[rebars.Length];
            double[] deltaMyArray = new double[rebars.Length];

            Parallel.For(0, rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaC = CalculateElasticSigmaC(strain);
                double sigmaS = CalculateSigmaS(n, strain);

                deltaNArray[i] = (sigmaS - sigmaC) * rebars[i].Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - ConcreteSection.Centroid.X);
            });

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> with homogenization coefficient <paramref name="n"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(double n, StrainPlane strainPlane)
        {
            try
            {
                return IntegrateSectionStress(n, strainPlane) + IntegrateRebarStress(n, strainPlane);
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

        internal double CalculateSigmaS(double n, double strain)
        {
            return _concreteSection.ConcreteMaterial.E * n * strain;
        }

        protected virtual (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete)
            CalculateMaxMinSectionDistances(double teta)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);

            double dminRebar = double.MaxValue;
            double dmaxRebar = double.MinValue;
            double dmaxConcrete = double.MinValue;
            double dminConcrete = double.MaxValue;

            int dMinRebarIndex = -1;
            int dMaxRebarIndex = -1;
            int dMaxVertexIndex = -1;
            int dMinVertexIndex = -1;

            var rebars = ConcreteSection.GetRebars();

            for (int r = 0; r < rebars.Length; r++)
            {
                double w1 = (rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta - (rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
                if (w1 <= dminRebar)
                {
                    dminRebar = w1;
                    dMinRebarIndex = r;
                }

                if (w1 >= dmaxRebar)
                {
                    dmaxRebar = w1;
                    dMaxRebarIndex = r;
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

            return (dMinRebarIndex, dminRebar, dMaxRebarIndex, dmaxRebar, dMinVertexIndex, dminConcrete, dMaxVertexIndex, dmaxConcrete);
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

        #region Protected method - Failure domain

        /// <summary>
        /// Calculate the failure domain <see cref="FailureDomain"/> of the section
        /// </summary>
        /// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
        /// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
        protected virtual FailureDomain CalculateFailureDomain(int momentsDiscretizations, (FailureZones, int)[] normalDiscretizations)
        {
            if (momentsDiscretizations < 2)
                return null;

            double deltaTeta = 2 * Math.PI / (momentsDiscretizations);
            momentsDiscretizations++;

            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[momentsDiscretizations][];

            try
            {

                Parallel.For(0, momentsDiscretizations, (i) =>
                {
                    (StrainPlane, FailureZones)[] strainPlanes = CalculateFailureStrainPlanes((i * deltaTeta), normalDiscretizations);

                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes.Length];

                    for (int j = 0; j < strainPlanes.Length; j++)
                    {
                        domainPoints[i][j] = new FailureDomain.FailureDomainPoint(CalculateForceResultant(strainPlanes[j]), strainPlanes[j].Item2, strainPlanes[j].Item1);
                    }

                    //Parallel.For(0, strainPlanes.Length, (j) =>
                    //{
                    //    domainPoints[i][j] = new FailureDomain.FailureDomainPoint(CalculateForceResultant(strainPlanes[j]), strainPlanes[j].Item2, strainPlanes[j].Item1);
                    //});
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

        /// <summary>
        /// Calculate the strain planes for angle <paramref name="teta"/>
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
        /// <returns></returns>
        protected virtual (StrainPlane, FailureZones)[] CalculateFailureStrainPlanes(double teta, (FailureZones, int)[] zoneSubdivision)
        {
            (StrainPlane, FailureZones)[] strainPlanes = new (StrainPlane, FailureZones)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];

            (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) sectionDistances
                = CalculateMaxMinSectionDistances(teta);

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
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision, sectionDistances, subIndex), failureZones);
                                subIndex++;
                            }

                            break;
                        }

                    case FailureZones.F5:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision, sectionDistances, subIndex), failureZones);
                                subIndex++;
                            }

                            strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, 1.0, sectionDistances, subIndex), failureZones);

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
            (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex, double dmaxRebar, int dMinVertexIndex, double dminConcrete,
            int dMaxVertexIndex, double dmaxConcrete) distances, int id = -1)
        {
            if (immersioneNelCampo > 1.0 || immersioneNelCampo < 0.0)
                throw new ArgumentException("ImmersioneNelCampo cannot be greater than 1 and less than 0");

            StrainPlane strainPlane;

            double chiSx;
            double chiDx;
            double chi;

            var rebars = ConcreteSection.GetRebars();
            switch (failureIndex)
            {
                case FailureZones.F1:

                    chiSx = 0;
                    chiDx = GetDesignUltimateStrainRebar(distances.dMinRebarIndex) / (distances.dmaxConcrete - distances.dminRebar);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(rebars[distances.dMinRebarIndex].Position, teta, chi,
                        GetDesignUltimateStrainRebar(distances.dMinRebarIndex), id);
                    break;


                case FailureZones.F2A:

                    chiSx = GetDesignUltimateStrainRebar(distances.dMinRebarIndex) / (distances.dmaxConcrete - distances.dminRebar);
                    chiDx = (GetDesignUltimateStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetYieldingStrainConcreteCompression())) /
                        (distances.dmaxConcrete - distances.dminRebar);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(rebars[distances.dMinRebarIndex].Position, teta, chi,
                        GetDesignUltimateStrainRebar(distances.dMinRebarIndex), id);
                    break;


                case FailureZones.F2B:

                    chiSx = (GetDesignUltimateStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetYieldingStrainConcreteCompression())) /
                    (distances.dmaxConcrete - distances.dminRebar);
                    chiDx = (GetDesignUltimateStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                        (distances.dmaxConcrete - distances.dminRebar);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(rebars[distances.dMinRebarIndex].Position, teta, chi,
                        GetDesignUltimateStrainRebar(distances.dMinRebarIndex), id);
                    break;


                case FailureZones.F3A:

                    chiSx = (GetDesignUltimateStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                        (distances.dmaxConcrete - distances.dminRebar);
                    chiDx = (GetDesignYieldingStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                        (distances.dmaxConcrete - distances.dminRebar);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi,
                        GetUltimateStrainConcreteCompression(), id);
                    break;


                case FailureZones.F3B:

                    chiSx = (GetDesignYieldingStrainRebar(distances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                        (distances.dmaxConcrete - distances.dminRebar);
                    chiDx = Math.Abs(GetUltimateStrainConcreteCompression()) / (distances.dmaxConcrete - distances.dminRebar);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi,
                        GetUltimateStrainConcreteCompression(), id);
                    break;


                case FailureZones.F4:

                    chiSx = Math.Abs(GetUltimateStrainConcreteCompression()) / (distances.dmaxConcrete - distances.dminRebar);
                    chiDx = Math.Abs(GetUltimateStrainConcreteCompression()) / (distances.dmaxConcrete - distances.dminConcrete);

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(ConcreteSection.Shape.Fill[distances.dMaxVertexIndex], teta, chi,
                        GetUltimateStrainConcreteCompression(), id);
                    break;


                case FailureZones.F5:

                    double fraction = GetYieldingStrainPureCompression() / GetUltimateStrainConcreteCompression();
                    double heigth = distances.dmaxConcrete - distances.dminConcrete;

                    Point2d strainPlaneCenter = new Point2d((distances.dmaxConcrete - (1 - fraction) * heigth) * (-Math.Sin(teta)) + ConcreteSection.Centroid.X,
                        (distances.dmaxConcrete - (1 - fraction) * heigth) * (Math.Cos(teta)) + ConcreteSection.Centroid.Y);

                    chiSx = Math.Abs(GetUltimateStrainConcreteCompression()) / (distances.dmaxConcrete - distances.dminConcrete);
                    chiDx = 0.0;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    strainPlane = new StrainPlane(strainPlaneCenter, teta, chi, GetYieldingStrainPureCompression(), id);
                    break;


                default:
                    return null;
            }

            return strainPlane;
        }


        #endregion

        #region Protected method - Point on failure domain

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, double angularTolerance = 1e-3, double distanceTolerance = 1e-4)
        {
            ForceTuple adimOutputForces = ConvertToAdimensionalForces(targetLocalForces);
            Vector3d vectorEd = new Vector3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N);

            // Valori di primo tentativo
            FailureZones failureIndex;
            double immersione;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
            {
                failureIndex = FailureZones.F1;
                immersione = 0.75;
            }
            else if (adimOutputForces.N > 0.0)
            {
                failureIndex = FailureZones.F2A;
                immersione = 0.5;
            }
            else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
            {
                failureIndex = FailureZones.F5;
                immersione = 1.0;
            }
            else if (adimOutputForces.N < Math.Min(adimOutputForces.Mx, adimOutputForces.My))
            {
                failureIndex = FailureZones.F3B;
                immersione = 0.5;
            }
            else
            {
                failureIndex = FailureZones.F3B;
                immersione = 0.25;
            }

            var distances = CalculateMaxMinSectionDistances(teta);
            StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, distances);

            //teta = strainPlane.Teta;

            //if (targetLocalForces.My < 0)
            //    teta *= -1;

            //if (targetLocalForces.Mx < 0)
            //    teta += Math.PI;
            //if (targetLocalForces.My < 0)
            //    teta *= -1;

            int id = 1;

            ForceTuple forces = CalculateForceResultant(strainPlane);
            ForceTuple adimIncrement = ConvertToAdimensionalForces(forces);

            (double deltaTeta, double deltaImmersione, Vector3d distanceToTarget) increment;

            double angle = new Vector3d(forces.Mx, forces.My, forces.N).AngleTo(new Vector3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));

            if (angle > angularTolerance)
            {
                do
                {
                    if (id < 100)
                    {
                        try
                        {
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, immersione, targetLocalForces, angle);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            return null;
                        }

                        increment.deltaImmersione = increment.deltaImmersione > 0.6 ? 0.6 : increment.deltaImmersione;
                        increment.deltaImmersione = increment.deltaImmersione < -0.6 ? -0.6 : increment.deltaImmersione;

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
                        strainPlane = CalculateStrainPlane(teta, failureIndex, immersione, distances, id);
                        forces = CalculateForceResultant(strainPlane);

                        ForceTuple incrementForce = new ForceTuple(increment.distanceToTarget.Z, increment.distanceToTarget.X, increment.distanceToTarget.Y);
                        adimIncrement = ConvertToAdimensionalForces(incrementForce);

                        angle = new Vector3d(forces.Mx, forces.My, forces.N).AngleTo(vectorEd);

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
            StrainPlane inputStrainPlane, FailureZones inputFailureZone, double inputImmersioneNelCampo, ForceTuple externalForces, double deltaAngle)
        {
            var adimIteractionPoint = ConvertToAdimensionalForces(iterationPoint);
            Line3d externalForcesLine = new Line3d(new Point3d(0, 0, 0), externalForces);
            Vector3d externalForcesVector = new Vector3d(new Point3d(0, 0, 0), externalForces);

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
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.1), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.1), 0.0005);
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
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.00001);
                    break;

                default:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.005), 0.0001);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.0001), 0.0001);
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

                    StrainPlane strainPlanePlusdTeta = CalculateStrainPlane(inputStrainPlane.Teta + dTetaBuffer, inputFailureZone,
                        inputImmersioneNelCampo, distancesPlusTeta);
                    StrainPlane strainPlaneMinusdTeta = CalculateStrainPlane(inputStrainPlane.Teta - dTetaBuffer, inputFailureZone,
                        inputImmersioneNelCampo, distancesMinusTeta);

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
                    return (0.1, 0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));

            } while (dNdTeta == 0.0 || (dMxdTeta == 0.0 && dMydTeta == 0.0));


            // derivate parziali rispetto a immersione nel campo
            do
            {
                if (etaCounter < 50)
                {
                    var distances = CalculateMaxMinSectionDistances(inputStrainPlane.Teta);

                    StrainPlane strainPlanePlusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Min(inputImmersioneNelCampo + dEtaBuffer, 1.0), distances);
                    StrainPlane strainPlaneMinusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Max(inputImmersioneNelCampo - dEtaBuffer, 0.0), distances);

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
                    return (0.1, 0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));

            } while (dNdImm == 0.0 || (dMxdImm == 0.0 && dMydImm == 0.0));


            Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
            v1.Unitize();
            Vector3d v2 = new Vector3d(dMxdImm, dMydImm, dNdImm);
            v2.Unitize();

            // vettore uscente dal punto M di test
            Vector3d gradient = v1 ^ v2;
            gradient.Unitize();

            // vettore che indica la direzione dell'incremento
            Vector3d s = gradient ^ (externalForces ^ gradient);
            s.Unitize();

            // k dell'equazione del piano tangente alla superficie in M
            // A*x + B*y + C*z + k = 0
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
                Vector3d v = new Vector3d(iterationPoint, intersectionPoint);

                Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);

                partialDerivatives[0, 0] = dMxdTeta;
                partialDerivatives[1, 0] = dNdTeta;

                partialDerivatives[0, 1] = dMxdImm;
                partialDerivatives[1, 1] = dNdImm;

                Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);
                inputVector[0, 0] = v.X;
                inputVector[1, 0] = v.Z;

                Matrix<double> results = partialDerivatives.Inverse() * inputVector;

                double dT;
                double dE;


                if (dTeta >= 0.1)
                    dT = Math.Pow(dTeta, 0.75);
                else if (dTeta > 0.01)
                    dT = Math.Pow(dTeta, 0.5);
                else if (dTeta > 0.001)
                    dT = Math.Pow(dTeta, 0.3);
                else
                    dT = Math.Pow(dTeta, 0.2);

                if (dEta >= 0.1)
                    dE = Math.Pow(dEta, 0.75);
                else if (dEta > 0.01)
                    dE = Math.Pow(dEta, 0.5);
                else if (dEta > 0.001)
                    dE = Math.Pow(dEta, 0.3);
                else
                    dE = Math.Pow(dEta, 0.2);

                if (Math.Abs(results[1, 0]) < 0.01)
                    dE = 1.0;
                else if (Math.Abs(results[1, 0]) < 0.1)
                    dE = 0.5;


                double deltaTeta = results[0, 0] * dT / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorTeta), 1.0));
                double deltaImmersione = results[1, 0] * dE / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorEta), 1.0));

                double dotProduct = externalForcesVector * v;

                if (iterationPoint.Mx < 0 && iterationPoint.My < 0)
                {
                    deltaTeta *= -1;
                }
                else if (iterationPoint.Mx < 0 && iterationPoint.My > 0)
                {
                    deltaTeta *= -1;
                }
                else if (iterationPoint.Mx > 0 && iterationPoint.My < 0)
                {

                }
                else if (iterationPoint.Mx > 0 && iterationPoint.My > 0)
                {

                }


                return (deltaTeta, deltaImmersione, v);
            }
        }

        #endregion

        #region Protected method - Stress SLS

        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, double tolerance = 1e-5)
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

            if (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance ||
                Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance ||
                Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance)
            {
                FailureDomain.FailureDomainPoint pointOnDomain = CalculateDomainPoint(localForces, _failureAnalysisTolerance);

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

        #region Protected method - N method

        protected StrainPlane CalculateStrainPlaneNMethodAnalysis(ForceTuple localForces, double n, double tolerance = 1e-5)
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

            ForceTuple iterationForces = CalculateForceResultant(n, strainPlane);
            ForceTuple iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);

            if (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance ||
                Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance ||
                Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance)
            {
                do
                {
                    if (id < 50)
                    {
                        try
                        {
                            (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) increment =
                                CalculateIncrementStressAnalysis(n, strainPlane, localForces - iterationForces);

                            // piano di nuovo tentativo
                            id++;
                            chiX += increment.deltaChiX;
                            chiY += increment.deltaChiY;
                            strainReferencePoint += increment.deltaStrainRefPoint;
                            strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                            iterationForces = CalculateForceResultant(n, strainPlane);
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

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(double n, StrainPlane inputStrainPlane, ForceTuple forceTuple)
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

            var forcesPlusdChiX = CalculateForceResultant(n, strainPlanePlusdChiX);
            var forcesMinusdChiX = CalculateForceResultant(n, strainPlaneMinusdChiX);

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            var forcesPlusdChiY = CalculateForceResultant(n, strainPlanePlusdChiY);
            var forcesMinusdChiY = CalculateForceResultant(n, strainPlaneMinusdChiY);

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            var forcesPlusStrain = CalculateForceResultant(n, strainPlanePlusStrain);
            var forcesMinusStrain = CalculateForceResultant(n, strainPlaneMinusStrain);

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

        protected virtual ResultBeamForces GetLocalForces(ResultBeamForces externalForces, Point2d forceReferencePoint)
        {
            return new ResultBeamForces(externalForces.N,
                                        externalForces.V1,
                                        externalForces.V2,
                                        externalForces.T,
                                        externalForces.M1 + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                        externalForces.M2 + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X),
                                        new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected ForceTuple GetLocalForces(ForceTuple externalForces, Point2d forceReferencePoint)
        {
            return new ForceTuple(externalForces.N,
                                  externalForces.Mx + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                  externalForces.My + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X));
        }

        protected virtual ResultBeamForces GetExternalForces(ResultBeamForces localForces, Point2d forceReferencePoint)
        {
            return new ResultBeamForces(localForces.N,
                                        localForces.V1,
                                        localForces.V2,
                                        localForces.T,
                                        localForces.M1 + localForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                        localForces.M2 + localForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X),
                                        new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected virtual ForceTuple GetExternalForces(ForceTuple forceTuple, Point2d forceReferencePoint)
        {
            return new ForceTuple(forceTuple.N,
                                  forceTuple.Mx + forceTuple.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                  forceTuple.My + forceTuple.N * (ConcreteSection.Centroid.X - forceReferencePoint.X));
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
