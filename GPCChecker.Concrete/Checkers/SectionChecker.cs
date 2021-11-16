using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class SectionChecker : Checker, ISerializable
    {

        protected readonly SectionCheckerAttribute _checkerAttributes;


        public SectionOptions SectionCheckerOptions => (SectionOptions)_options;


        /// <param name="checkerAttribute">This rapresent one section and multiple forces applied</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionOptions options, Standard standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
        }


        internal abstract FailureDomainResult GetFailureDomainResult();

        public abstract Task<FailureDomainResult> GetFailureDomainResultAsync();

        internal abstract StressAnalysisResult[] GetStressAnalysisResult();

        public abstract Task<StressAnalysisResult[]> GetStressAnalysisResultAsync();

        public abstract class SectionOptions : Options
        {

            public Point2d AxialForceReferencePoint { get; }

            public SectionOptions()
            {
                AxialForceReferencePoint = Point2d.Origin;
            }

            public SectionOptions(Point2d axialForceReferencePoint)
            {
                AxialForceReferencePoint = axialForceReferencePoint;
            }

            public override bool Equals(object obj)
            {
                return obj is SectionOptions options && AxialForceReferencePoint.Equals(options.AxialForceReferencePoint);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return -17 * AxialForceReferencePoint.GetHashCode();
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
