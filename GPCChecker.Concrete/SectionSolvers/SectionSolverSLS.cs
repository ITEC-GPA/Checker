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
using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    public abstract class SectionSolverSLS : SectionSolver
    {
        protected ResultBeamForces _forces;
        protected Point2d _forceReferencePoint;



        public ResultBeamForces Forces => _forces;
        public Point2d ForceReferencePoint => _forceReferencePoint;


        public SectionSolverSLS(IConcreteSection section, ResultBeamForces forces, Standard standard, Point2d forceReferencePoint = default)
            : base(section, standard)
        {
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
            _forceReferencePoint = forceReferencePoint ?? section.Centroid; // se point2d è default (nullo) assegnamo il centroide della sezione
        }


        public SectionSolverSLS(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _forces = (ResultBeamForces)info.GetValue("Forces", typeof(ResultBeamForces));
            _forceReferencePoint = (Point2d)info.GetValue("Point2d", typeof(Point2d));
        }


        #region Solver

        public StrainPlane Solve()
        {
            return CalculateStrainPlaneStressAnalysis(Forces.ConvertToForceTuple(_forceReferencePoint), _forceReferencePoint, SectionSolverOptions.Instance.SLSconvergenceTolerance);
        }

        #endregion


        #region Equals - hashcode - operators

        public override bool Equals(object obj)
        {
            return obj is SectionSolverSLS sLS &&
                   base.Equals(obj) &&
                   EqualityComparer<ResultType>.Default.Equals(_forces, sLS._forces);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<ResultType>.Default.GetHashCode(_forces);
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Forces", _forces);
        }

        #endregion
    }
}
