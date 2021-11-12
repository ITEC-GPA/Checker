using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
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
        protected virtual (double N, double Mx, double My) CalculateConcreteStressResultant(StrainPlane strainPlane)
        {
            double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
            double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

            try
            {
                Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                {
                    (double N, double Mx, double My) forces = CalculateFaceStressResultant(ConcreteSection.Mesh.Faces[i + 1], strainPlane);

                    deltaNArray[i] = forces.N;
                    deltaMxArray[i] = forces.Mx;
                    deltaMyArray[i] = forces.My;
                });
            }
            catch (Exception e)
            {
                _log.Add($"Fail" + e.InnerException);
            }

            return (deltaNArray.Sum(), deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Calculate the resultants of face <paramref name="face"/>
        /// </summary>
        /// <param name="face"></param>
        /// <param name="strainPlane"></param>
        /// <param name="deltaN"></param>
        /// <param name="deltaMx"></param>
        /// <param name="deltaMy"></param>
        protected virtual (double N, double Mx, double My) CalculateFaceStressResultant(MeshFace face, StrainPlane strainPlane)
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
                deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))), 
                    points, gaussPointsTri);
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

            return (deltaN, deltaMx, deltaMy);
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual (double N, double Mx, double My) CalculateRebarsIntegration(StrainPlane strainPlane)
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


            return (deltaNArray.Sum(), deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        public virtual (double N, double Mx, double My) CalculateForces(StrainPlane strainPlane)
        {
            
            try
            {
                var concreteForces = CalculateConcreteStressResultant(strainPlane);
                var rebarsForces = CalculateRebarsIntegration(strainPlane);

                var forces = CalculateExternalForces(concreteForces.N  + rebarsForces.N,
                                                     concreteForces.Mx + rebarsForces.Mx,
                                                     concreteForces.My + rebarsForces.My,
                                                     SectionSolverOptions.Instance.DistanceFromCentroid);

                return (forces.N, forces.Mx, forces.My);
            }
            catch (Exception e)
            {
                _log.Add($"Fail" + e.InnerException);

                return (0,0,0);
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


        protected virtual (double N, double Mx, double My) CalculateAdimensionalForces(ResultBeamForces forces)
        {
            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); 
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return (forces.N / (b * h * GetFck()), forces.M1 / (b * h * h * GetFck()), forces.M2 / (b * b * h * GetFck()));
        }


        protected virtual (double N, double Mx, double My) CalculateAdimensionalForces(double N, double Mx, double My)
        {

            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); ;
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return (N / (b * h * GetFck()), Mx / (b * h * h * GetFck()), Mx / (b * b * h * GetFck()));
        }

        protected virtual ResultBeamForces CalculateExternalForces(ResultBeamForces forces, Point3d distanceFromCentroid)
        {
            double N = forces.N;
            double Mx = forces.M1 + N * distanceFromCentroid.Y;
            double My = forces.M2 + N * distanceFromCentroid.X;

            return new ResultBeamForces(N, forces.V1, forces.V2, forces.T, Mx, My, forces.CoordinateSystem);
        }

        protected virtual (double N, double Mx, double My) CalculateExternalForces(double inputN, double inputMx, double inputMy, Point3d distanceFromCentroid)
        {
            return (inputN, inputMx + inputN * distanceFromCentroid.Y, inputMy + inputN * distanceFromCentroid.X);
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
