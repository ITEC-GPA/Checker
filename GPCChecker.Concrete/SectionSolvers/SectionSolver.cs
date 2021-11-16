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


        protected IConcreteSection _concreteSection;
        protected Standard _standard;
        protected List<string> _log;


        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;


        internal SectionSolver(IConcreteSection section, Standard standard, int id)
            : base(id)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _log = new List<string>();
        }

        protected SectionSolver(SerializationInfo info, StreamingContext context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
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
        protected abstract double CalculateSigmaC(double strain);

        /// <returns>The design steel stress related to <paramref name="strain"/></returns>
        protected abstract double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain);

        #endregion


        #region Public method

        public virtual FailureDomainResult GetFailureDomainResults(ResultBeamForces[] forces)
        {
            if (forces == null)
                return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(SectionSolverOptions.Instance.MomentsDiscretizations,
                                            SectionSolverOptions.Instance.FailureZonesDiscretizations), null, Standard, Id);

            return new FailureDomainResult(ConcreteSection, CalculateFailureDomain(SectionSolverOptions.Instance.MomentsDiscretizations,
                                            SectionSolverOptions.Instance.FailureZonesDiscretizations), forces, Standard, Id);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, Point2d forceReferencePoint)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];


            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                                            CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(forceReferencePoint), forceReferencePoint, SectionSolverOptions.Instance.SLSconvergenceTolerance),
                                            Standard, Id);
            });

            return stressAnalysisResults;
        }

        #endregion

        #region SectionIntegration

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
                _log.Add($"Fail" + e.InnerException);
            }

            return new ForceTuple(deltaNArray.Sum(), deltaMxArray.Sum(), deltaMyArray.Sum());
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
                throw new Exception();

            return new ForceTuple(deltaN, deltaMx, deltaMy);
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual ForceTuple IntegrateRebarStress(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Rebars.Length];
            double[] deltaMxArray = new double[ConcreteSection.Rebars.Length];
            double[] deltaMyArray = new double[ConcreteSection.Rebars.Length];

            Parallel.For(0, ConcreteSection.Rebars.Length, (i) =>
            {
                double strain = strainPlane.GetStrain(ConcreteSection.Rebars[i].Position);
                double sigmaS = CalculateStressRebar(ConcreteSection.Rebars[i], strain);
                double sigmaC = CalculateSigmaC(strain);

                deltaNArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * ConcreteSection.Rebars[i].Area * (ConcreteSection.Rebars[i].Position.X - ConcreteSection.Centroid.X);
            });


            return new ForceTuple(deltaNArray.Sum(), deltaMxArray.Sum(), deltaMyArray.Sum());
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
                _log.Add($"Fail" + e.InnerException);

                return new ForceTuple(0, 0, 0);
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
                _log.Add($"Fail" + e.InnerException);

                return new ForceTuple(0, 0, 0);
            }
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

            for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
            {
                double w1 = (ConcreteSection.Rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta - (ConcreteSection.Rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
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

            return new ForceTuple(forceTuple.N / (b * h * GetFck()),
                                  forceTuple.Mx / (b * h * h * GetFck()),
                                  forceTuple.My / (b * b * h * GetFck()));
        }


        #endregion

        #region Failure domain


        /// <summary>
        /// Calculate the failure domain <see cref="FailureDomain"/> of the section
        /// </summary>
        /// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
        /// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
        protected virtual FailureDomain CalculateFailureDomain(int momentsDiscretizations, (FailureZones, int)[] normalDiscretizations)
        {
            if (momentsDiscretizations < 2)
                throw new ArgumentException();

            double deltaTeta = 2 * Math.PI / (momentsDiscretizations);
            momentsDiscretizations++;

            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[momentsDiscretizations][];
            (StrainPlane, FailureZones)[][] strainPlanes = new (StrainPlane, FailureZones)[momentsDiscretizations][];

            try
            {

                Parallel.For(0, momentsDiscretizations, (i) =>
                {
                    strainPlanes[i] = CalculateFailureStrainPlanes((i * deltaTeta), normalDiscretizations, GetYieldingStrainConcreteCompression(), GetUltimateStrainConcreteCompression());
                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];

                    Parallel.For(0, strainPlanes[i].Length, (j) =>
                    {
                        domainPoints[i][j] = new FailureDomain.FailureDomainPoint(CalculateForceResultant(strainPlanes[i][j]), strainPlanes[i][j].Item2, strainPlanes[i][j].Item1);
                    });
                });

            }
            catch (Exception e)
            {
                _log.Add($"Fail to calculate ULS strain planes" + e.InnerException);
            }

            return new FailureDomain(domainPoints);
        }

        /// <summary>
        /// Calculate the strain planes for angle <paramref name="teta"/>
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
        /// <returns></returns>
        protected virtual (StrainPlane, FailureZones)[] CalculateFailureStrainPlanes(double teta, (FailureZones, int)[] zoneSubdivision,
                                                        double strainYCompression, double strainUCompression)
        {

            (StrainPlane, FailureZones)[] strainPlanes = new (StrainPlane, FailureZones)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];


            (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) sectionDistances
                = CalculateMaxMinSectionDistances(teta);


            int subIndex = 0;

            double concreteRebarMaxDistance = sectionDistances.dmaxConcrete - sectionDistances.dminRebar;
            double concreteMaxDistance = sectionDistances.dmaxConcrete - sectionDistances.dminConcrete;
            double height = sectionDistances.dmaxConcrete + sectionDistances.dminConcrete;


            Point2d p3 = new Point2d((sectionDistances.dmaxConcrete + (height * (1.0 / Math.Abs(strainUCompression) - 1.0 / Math.Abs(strainYCompression)) * Math.Abs(strainYCompression))) * (-Math.Sin(teta)) + ConcreteSection.Centroid.X,
                                     (sectionDistances.dmaxConcrete + (height * (1.0 / Math.Abs(strainUCompression) - 1.0 / Math.Abs(strainYCompression)) * Math.Abs(strainYCompression))) * Math.Cos(teta) + ConcreteSection.Centroid.Y);

            double chiSx = 0;
            double chiDx = 0;

            foreach ((FailureZones, int) zone in zoneSubdivision)
            {
                FailureZones failureZones = zone.Item1;
                int subdivision = zone.Item2 + 1;

                switch (failureZones)   // campo 1
                {
                    case FailureZones.F1:

                        chiSx = 0;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = GetDesignUltimateStrainRebar(sectionDistances.dMaxRebarIndex) / concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
                                GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F2A:

                        chiSx = GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex) / concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = (GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetYieldingStrainConcreteCompression())) /
                            (sectionDistances.dmaxConcrete - sectionDistances.dminRebar); // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
                                GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F2B:

                        chiSx = (GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetYieldingStrainConcreteCompression()))
                            / concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = (GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression()))
                            / concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Rebars[sectionDistances.dMinRebarIndex].Position, teta, chi,
                                GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F3A:

                        chiSx = (GetDesignUltimateStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                            concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = (GetDesignYieldingStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                            concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi,
                                GetUltimateStrainConcreteCompression(), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F3B:

                        chiSx = (GetDesignYieldingStrainRebar(sectionDistances.dMinRebarIndex) + Math.Abs(GetUltimateStrainConcreteCompression())) /
                                concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = Math.Abs(GetUltimateStrainConcreteCompression()) / concreteRebarMaxDistance; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi,
                                GetUltimateStrainConcreteCompression(), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F4:

                        chiSx = Math.Abs(GetUltimateStrainConcreteCompression()) / concreteRebarMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = Math.Abs(GetUltimateStrainConcreteCompression()) / concreteMaxDistance; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(ConcreteSection.Shape.Fill[sectionDistances.dMaxVertexIndex], teta, chi,
                                GetUltimateStrainConcreteCompression(), subIndex), failureZones);
                            subIndex++;
                        }

                        break;

                    case FailureZones.F5:

                        chiSx = Math.Abs(GetUltimateStrainConcreteCompression()) / concreteMaxDistance;   // valore curvatura estremo Sx del campo i-esimo
                        chiDx = 0.0; // valore curvatura estremo Dx del campo i-esimo

                        for (int j = 0; j < subdivision; j++)
                        {
                            double chi = chiSx + j * (chiDx - chiSx) / subdivision;
                            strainPlanes[subIndex] = (new StrainPlane(p3, teta, chi, GetYieldingStrainPureCompression(), subIndex), failureZones);
                            subIndex++;
                        }

                        strainPlanes[subIndex] = (new StrainPlane(p3, teta, chiDx, GetYieldingStrainPureCompression(), subIndex), failureZones);
                        break;

                    default:
                        throw new NotImplementedException();

                }
            }


            return strainPlanes;
        }

        #endregion
        
        #region Stress SLS


        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple externalForces, Point2d forceReferencePoint, double tolerance = 1e-5)
        {
            ForceTuple targetLocalForces = GetLocalForces(externalForces, forceReferencePoint);

            ForceTuple targetLocalForcesAdmin = ConvertToAdimensionalForces(targetLocalForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);
            ForceTuple iterationForces = CalculateForceResultant(strainPlane);

            ForceTuple iterationForcesAdmin = ConvertToAdimensionalForces(iterationForces);

            if (iterationForcesAdmin > tolerance)
            {
                do
                {
                    try
                    {
                        (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) increment = CalculateIncrementStressAnalysis(strainPlane, targetLocalForcesAdmin - iterationForces);

                        // piano di nuovo tentativo
                        id++;
                        chiX += increment.deltaChiX;
                        chiY += increment.deltaChiY;
                        strainReferencePoint += increment.deltaStrainRefPoint;
                        strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                        iterationForces = CalculateForceResultant(strainPlane);

                        iterationForcesAdmin = ConvertToAdimensionalForces(iterationForces);
                    }
                    catch
                    {
                        _log.Add("Fail to calculate increment");
                        throw new Exception("Fail to calculate increment");
                    }


                } while (iterationForcesAdmin > tolerance);
            }
            return strainPlane;
        }

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(StrainPlane inputStrainPlane, ForceTuple forceTuple)
        {
            ForceTuple forceTupleAdmin = ConvertToAdimensionalForces(forceTuple);

            double deltaChiXLimit = Math.Abs(GetYieldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.X);
            double dCX = 0.00001;
            if (forceTupleAdmin.Mx != 0)
                dCX = 0.001 * Math.Max(forceTupleAdmin.Mx, 0.00001);

            double dChiX = dCX * deltaChiXLimit;

            double deltaChiYLimit = Math.Abs(GetYieldingStrainConcreteCompression() / ConcreteSection.Shape.GetBoundingBox().Size.Y);
            double dCY = 0.00001;
            if (forceTupleAdmin.My != 0)
                dCY = 0.001 * Math.Max(forceTupleAdmin.My, 0.00001);

            double dChiY = dCY * deltaChiYLimit;

            double deltaStrainLimit = 1.0 / (ConcreteSection.Area * GetFck());
            double dS = 0.00001;
            if (forceTupleAdmin.N != 0)
                dS = 0.0001 * Math.Max(forceTupleAdmin.N, 0.00001);

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

            double dNdStrain = (forcesPlusStrain.N - forcesMinusStrain.N) / (2.0 * dCY);
            double dMxdStrain = (forcesPlusStrain.Mx - forcesMinusStrain.Mx) / (2.0 * dCY);
            double dMydStrain = (forcesPlusStrain.My - forcesMinusStrain.My) / (2.0 * dCY);



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

        #region Convert forces local/global

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
