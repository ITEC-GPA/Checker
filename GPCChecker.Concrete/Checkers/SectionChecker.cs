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
using GPC.Model.Results;
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

		#region Public Async Method

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
		public abstract Task<FailureDomainResult> GetPlasticFailureDomainResultAsync();

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract Task<FailureDomainResult> GetElasticFailureDomainResultAsync();

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult[]> GetStressAnalysisResultAsync();

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult> GetStressAnalysisResultAsync(ResultBeamForces forces);

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double phi);

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double phi);

        #endregion

        #region Internal Method

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal abstract FailureDomainResult GetPlasticFailureDomainResult();

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal abstract FailureDomainResult GetElasticFailureDomainResult();

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal abstract StressAnalysisResult[] GetStressAnalysisResult();

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal abstract StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces);

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal abstract StressAnalysisResult[] GetLinearStressAnalysisResult(double phi);

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal abstract StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double phi);

		#endregion

		public abstract class SectionOptions : Options
        {
            public CoordinateSystem ForceReferenceCoordinateSystem { get; }

            public SectionOptions()
            {
                ForceReferenceCoordinateSystem = CoordinateSystem.Global;
            }

            public SectionOptions(CoordinateSystem forceReferencePointCoordinateSystem)
            {
                ForceReferenceCoordinateSystem = forceReferencePointCoordinateSystem;
            }

            public override bool Equals(object obj)
            {
                return obj is SectionOptions options && ForceReferenceCoordinateSystem.Equals(options.ForceReferenceCoordinateSystem);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = -17;
                    hashCode = hashCode * -23 + ForceReferenceCoordinateSystem.GetHashCode();
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
