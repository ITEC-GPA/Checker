using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class SectionChecker : Checker, ISerializable
    {

        protected readonly SectionSolver _solver;
        protected readonly SectionCheckerAttribute _checkerAttributes;


        public SectionOptions SectionCheckerOptions => (SectionOptions)_options;


        /// <param name="checkerAttribute">This rapresent one section and multiple forces applied</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <param name="solver"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionOptions options, Standard standard, SectionSolver solver, int id = IDUNASSIGNED)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        }


        internal abstract FailureDomainResult GetFailureDomainResult();

        public abstract Task<FailureDomainResult> GetFailureDomainResultAsync();

        internal abstract StressAnalysisResult[] GetStressAnalysisResult();

        public abstract Task<StressAnalysisResult[]> GetStressAnalysisResultAsync();

        public abstract class SectionOptions : Options
        {

            public Vector2d ForceReferencePointCentroidDistance { get; }
            public bool PlasticFailureDomain { get; }


            public SectionOptions()
            {
                ForceReferencePointCentroidDistance = Vector2d.Zero;
                PlasticFailureDomain = true;
            }

            public SectionOptions(Vector2d forceReferencePointCentroidDistance, bool plasticFailureDomain)
            {
                ForceReferencePointCentroidDistance = forceReferencePointCentroidDistance;
                PlasticFailureDomain = plasticFailureDomain;
            }

            public override bool Equals(object obj)
            {
                return obj is SectionOptions options && ForceReferencePointCentroidDistance.Equals(options.ForceReferencePointCentroidDistance)
                                                     && PlasticFailureDomain.Equals(options.PlasticFailureDomain);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = -17;
                    hashCode = hashCode * -23 + ForceReferencePointCentroidDistance.GetHashCode();
                    hashCode = hashCode * -23 + PlasticFailureDomain.GetHashCode();
                    return hashCode; 
                }
            }

            public static bool operator ==(SectionOptions left, SectionOptions right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(SectionOptions left, SectionOptions right)
            {
                return !(left == right);
            }
        }
    }
}
