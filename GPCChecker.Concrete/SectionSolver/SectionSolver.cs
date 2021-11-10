using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolver
{

    [Serializable]
    public abstract class SectionSolver
    {
        #region Variables

        protected IConcreteSection _concreteSection;
        protected Standard _standard;
        protected List<string> _log;
        protected SectionChecker.Options _options;

        #endregion

        #region Properties

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;

        #endregion

        #region Public Constructor

        public SectionSolver(IConcreteSection section, Standard standard, SectionChecker.Options options)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _log = new List<string>();
        }

        public SectionSolver(SerializationInfo info, StreamingContext context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Checker.Options)info.GetValue("Checker.Options", typeof(SectionChecker.Options));

            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
        }

        #endregion

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

        #endregion

        #region Virtual Method

        /// <summary>
        /// Return the strain value of the <paramref name="pointToTest"/>
        /// </summary>
        protected virtual double CalculateStrain(StrainPlane strainPlane, Point3d pointToTest)
        {
            return SolverHelper.CalculateStrain(strainPlane, pointToTest);
        }

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected async virtual Task<(double deltaN, double deltaMx, double deltaMy)> CalculateConcreteStressResultantAsync(StrainPlane strainPlane)
        {

            (double deltaN, double deltaMx, double deltaMy) task = await Task.Run(() =>
            {
                double[] deltaNArray = new double[ConcreteSection.Mesh.FacesCount];
                double[] deltaMxArray = new double[ConcreteSection.Mesh.FacesCount];
                double[] deltaMyArray = new double[ConcreteSection.Mesh.FacesCount];

                try
                {
                    Parallel.For(0, ConcreteSection.Mesh.FacesCount, (i) =>
                    {
                        CalculateFaceStressResultant(ConcreteSection.Mesh.Faces[i + 1], strainPlane, out double deltaNBuffer, out double deltaMxBuffer, out double deltaMyBuffer);

                        deltaNArray[i] = deltaNBuffer;
                        deltaMxArray[i] = deltaMxBuffer;
                        deltaMyArray[i] = deltaMyBuffer;
                    });
                }
                catch (Exception e)
                {
                    _log.Add($"Fail" + e.InnerException);
                }

                return (deltaNArray.Sum(), deltaMxArray.Sum(), deltaMyArray.Sum());
            });


            return task;
        }

        /// <summary>
        /// Calculate the resultants of face <paramref name="face"/>
        /// </summary>
        /// <param name="face"></param>
        /// <param name="strainPlane"></param>
        /// <param name="deltaN"></param>
        /// <param name="deltaMx"></param>
        /// <param name="deltaMy"></param>
        protected virtual void CalculateFaceStressResultant(MeshFace face, StrainPlane strainPlane, out double deltaN, out double deltaMx, out double deltaMy)
        {
            Point3d[] points = ConcreteSection.Mesh.GetFacePoints(face);

            if (face.IsTriangle)
            {
                deltaN = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))),
                    points, 79);
                deltaMx = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (y - ConcreteSection.Centroid.Y), points, 79);
                deltaMy = GaussIntegration.IntegrationTriangularLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (x - ConcreteSection.Centroid.X), points, 79);
            }

            else if (face.IsQuad)
            {
                deltaN = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))),
                    points, 121);
                deltaMx = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (y - ConcreteSection.Centroid.Y), points, 121);
                deltaMy = GaussIntegration.IntegrationQuadrilateralLinearShapeFunction((x, y) => CalculateSigmaC(CalculateStrain(strainPlane, new Point3d(x, y, 0))) *
                    (x - ConcreteSection.Centroid.X), points, 121);
            }
            else
                throw new Exception();
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected async virtual Task<(double deltaN, double deltaMx, double deltaMy)> CalculateRebarsIntegrationAsync(StrainPlane strainPlane)
        {

            (double deltaN, double deltaMx, double deltaMy) task = await Task.Run(() =>
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
            });

            return task;
        }

        /// <summary>
        /// Calculate the <see cref="FailureDomain.FailureDomainPoint"/> respect the strain plane <paramref name="strainPlane"/>
        /// </summary>
        /// <param name="strainPlane"></param>
        /// <returns></returns>
        public virtual void CalculateForces(StrainPlane strainPlane, out double N, out double Mx, out double My)
        {

            try
            {
                var task = Task.WhenAll(new[] { CalculateConcreteStressResultantAsync(strainPlane), CalculateRebarsIntegrationAsync(strainPlane) });

                N = task.Result[0].deltaN + task.Result[1].deltaN;
                Mx = -(task.Result[0].deltaMx + task.Result[1].deltaMx);
                My = task.Result[0].deltaMy + task.Result[1].deltaMy;

            }
            catch (Exception e)
            {
                _log.Add($"Fail" + e.InnerException);
                N = 0;
                Mx = 0;
                My = 0;
            }

        }

        protected virtual void CalculateRelativeDistance(double teta, out int dMinRebarIndex, out int dMaxRebarIndex, out int dMinVertexIndex, out int dMaxVertexIndex)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);

            double dminSteel = double.MaxValue;
            double dmaxSteel = double.MinValue;
            double dmaxConcrete = double.MinValue;
            double dminConcrete = double.MaxValue;

            dMinRebarIndex = -1;
            dMaxRebarIndex = -1;
            dMaxVertexIndex = -1;
            dMinVertexIndex = -1;

            for (int r = 0; r < ConcreteSection.Rebars.Count(); r++)
            {
                double w1 = (ConcreteSection.Rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta -
                    (ConcreteSection.Rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
                if (w1 <= dminSteel)
                {
                    dminSteel = w1;
                    dMinRebarIndex = r;
                }

                if (w1 >= dmaxSteel)
                {
                    dmaxSteel = w1;
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
        }

        protected virtual void CalculateAdimensionalForces(ResultBeamForces forces, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
        {
            CalculateAdimensionalForces(forces.N, forces.M1, forces.M2, out adimAxialForce, out adimBendingMomentX, out adimBendingMomentY);
        }

        protected virtual void CalculateAdimensionalForces(double N, double Mx, double My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
        {
            BoundingBox3d bBox = ConcreteSection.Shape.GetBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            adimAxialForce = N / (h * b * ConcreteSection.ConcreteMaterial.Fck);
            adimBendingMomentX = Mx / (h * b * h * ConcreteSection.ConcreteMaterial.Fck);
            adimBendingMomentY = My / (b * b * h * ConcreteSection.ConcreteMaterial.Fck);
        }

        protected virtual ResultBeamForces CalculateExternalForces(ResultBeamForces forces, Point3d distanceFromCentroid)
        {
            double N = forces.N;
            double Mx = forces.M1 + N * distanceFromCentroid.Y;
            double My = forces.M2 + N * distanceFromCentroid.X;

            return new ResultBeamForces(N, forces.V1, forces.V2, forces.T, Mx, My, forces.CoordinateSystem);
        }

        protected virtual void CalculateExternalForces(double inputN, double inputMx, double inputMy, Point3d distanceFromCentroid, out double N, out double Mx, out double My)
        {
            N = inputN;
            Mx = inputMx + N * distanceFromCentroid.Y;
            My = inputMy + N * distanceFromCentroid.X;
        }

        #endregion

        #region Public override methods

        public override bool Equals(object obj)
        {
            return obj is SectionSolver solver &&
                   EqualityComparer<IConcreteSection>.Default.Equals(_concreteSection, solver._concreteSection);
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

            info.AddValue("Standard", _standard);
            info.AddValue("Checker.Options", _options);
        }

        #endregion
    }
}
