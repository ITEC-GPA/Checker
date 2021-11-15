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
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    [Serializable]
    public abstract class SectionSolver
    {

        protected IConcreteSection _concreteSection;
        protected Standard _standard;
        protected List<string> _log;


        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;


        public SectionSolver(IConcreteSection section, Standard standard)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _log = new List<string>();
        }

        public SectionSolver(SerializationInfo info, StreamingContext context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
        }


        #region Abstract Method

        protected abstract double CalculateYeldingStrainSteel(ReinforcedConcreteRebar rebar);
        protected abstract double CalculateYeldingStrainSteel(int rebar);
        protected abstract double CalculateUltimateStrainSteel(ReinforcedConcreteRebar rebar);
        protected abstract double CalculateUltimateStrainSteel(int rebar);
        protected abstract double CalculateUltimateStrainConcreteCompression();
        protected abstract double CalculateYeldingStrainConcreteCompression();
        protected abstract double CalculateLimitStrainCostantCompression();
        protected abstract double CalculateUltimateStrainConcreteTension();
        protected abstract double CalculateSigmaC(double strain);
        protected abstract double CalculateStressSteel(ReinforcedConcreteRebar rebar, double strain);
        protected abstract double GetFck();
        protected abstract double GetStrainYCompression();
        protected abstract double GetStrainUCompression();

        #endregion

        #region Virtual Method


        /// <summary>
        /// Return the strain value of the <paramref name="pointToTest"/>
        /// </summary>
        protected virtual double CalculateStrain(StrainPlane inputStrainPlane, Point3d pointToTest)
        {
            return SectionSolverHelper.CalculateStrain(inputStrainPlane, pointToTest);
        }

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual ForceTuple CalculateConcreteStressResultant(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

            try
            {
                Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                {
                    var forces = CalculateFaceStressResultant(ConcreteSection.Mesh.Faces[i + 1], strainPlane);

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
        protected virtual ForceTuple CalculateFaceStressResultant(MeshFace face, StrainPlane strainPlane)
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
                deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))), points, gaussPointsTri);
                deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsTri);
                deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (x - ConcreteSection.Centroid.X), points, gaussPointsTri);
            }
            else if (face.IsQuad)
            {
                deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))),
                    points, gaussPointsQuad);
                deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (y - ConcreteSection.Centroid.Y), points, gaussPointsQuad);
                deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
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
        protected virtual ForceTuple CalculateRebarsIntegration(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Rebars.Length];
            double[] deltaMxArray = new double[ConcreteSection.Rebars.Length];
            double[] deltaMyArray = new double[ConcreteSection.Rebars.Length];

            Parallel.For(0, ConcreteSection.Rebars.Length, (i) =>
            {
                double strain = CalculateStrain(strainPlane, ConcreteSection.Rebars[i].Position);
                double sigmaS = CalculateStressSteel(ConcreteSection.Rebars[i], strain);
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
        public virtual ForceTuple CalculateForces(StrainPlane strainPlane)
        {
            
            try
            {
                var concreteForces = CalculateConcreteStressResultant(strainPlane);
                var rebarsForces = CalculateRebarsIntegration(strainPlane);

                var externalForces = GetExternalForces(concreteForces + rebarsForces, SectionSolverOptions.Instance.DistanceFromCentroid);

                return new ForceTuple(externalForces.N, externalForces.Mx, externalForces.My);
            }
            catch (Exception e)
            {
                _log.Add($"Fail" + e.InnerException);

                return new ForceTuple(0,0,0);
            }

        }

        protected virtual (int dMinRebarIndex, double dminRebar, int dMaxRebarIndex,  double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) 
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


        protected virtual ForceTuple CalculateAdimensionalForces(ResultBeamForces forces)
        {
            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); 
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return new ForceTuple(forces.N  / (b * h * GetFck()), 
                                  forces.M1 / (b * h * h * GetFck()), 
                                  forces.M2 / (b * b * h * GetFck()));
        }


        protected virtual ForceTuple CalculateAdimensionalForces(ForceTuple forceTuple)
        {

            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); ;
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return new ForceTuple(forceTuple.N  / (b * h * GetFck()), 
                                  forceTuple.Mx / (b * h * h * GetFck()),
                                  forceTuple.My / (b * b * h * GetFck()));
        }

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

        protected virtual ForceTuple GetLocalForces(ForceTuple externalForces, Point2d forceReferencePoint)
        {

            return new ForceTuple(externalForces.N,
                                  externalForces.Mx + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                                  externalForces.My + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X);
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

        #region Public override methods

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

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ConcreteSection", _concreteSection);
            info.AddValue("Log", _log);
        }

        #endregion

        #region Public methods

        public List<string> GetLog()
        {
            return _log;
        }

        #endregion
    }
}
