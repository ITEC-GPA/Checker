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
        #region Fields

        protected readonly Standard _standardStructuralSteel;

        protected readonly SectionSolver _solver;

        protected readonly SectionCheckerAttribute _checkerAttributes;

        #endregion

        #region Properties

        /// <summary>
        /// Standard for steel structural sections, like for example IPE300 inside reinforced concrete.
        /// </summary>
        public Standard StandardStructuralSteel => _standardStructuralSteel;

        public SectionOptions SectionCheckerOptions => (SectionOptions)_options;

        public SectionSolver SectionSolver => _solver;

        #endregion

        #region Constructors

        /// <param name="checkerAttribute">This rapresent one section and multiple forces applied</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <param name="solver"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionOptions options, Standard standard, 
            SectionSolver solver, int id = IDUNASSIGNED, Standard standardStructuralSteel = null)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _standardStructuralSteel = standardStructuralSteel;
        }

        #endregion

        #region Public Async Method

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract Task<FailureDomainResult> GetPlasticFailureDomainResultAsync();

        /// <summary>
        /// Calculate the plastic failure domain 2d and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync(double teta = 0);

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract Task<FailureDomainResult> GetElasticFailureDomainResultAsync();

        /// <summary>
        /// Calculate the elastic failure domain2d and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract Task<FailureDomainResult2d> GetElasticFailureDomainResult2dAsync(double teta = 0);

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
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="psi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi, double psiTendon = 0);

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="psi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi, double psiTendon = 0);

        #endregion

        #region Public Method

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract FailureDomainResult GetPlasticFailureDomainResult();

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract FailureDomainResult GetElasticFailureDomainResult();

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract FailureDomainResult2d GetPlasticFailureDomainResult2d();

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public abstract FailureDomainResult2d GetElasticFailureDomainResult2d();

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract StressAnalysisResult[] GetStressAnalysisResult();

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces);

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract StressAnalysisResult[] GetLinearStressAnalysisResult(double phi, double psiTendon = 0);

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public abstract StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double phi, double psiTendon = 0);

        public abstract FailureDomain.FailureDomainPoint CalculatePlasticFailureDomainPoint(ResultBeamForces force);

        public abstract FailureDomain.FailureDomainPoint CalculateElasticFailureDomainPoint(ResultBeamForces force);

        #endregion

        #region Nested class

        [Serializable]
        public abstract class SectionOptions : Options, ISerializable
        {
            public CoordinateSystem ForceReferenceCoordinateSystem { get; set; }

            public SectionSolver.FailureAnalysisTypes FailureAnalysisType { get; set; }

            public SectionOptions()
            {
                ForceReferenceCoordinateSystem = CoordinateSystem.Global;
            }

            public SectionOptions(CoordinateSystem forceReferencePointCoordinateSystem, SectionSolver.FailureAnalysisTypes failureAnalysisType)
            {
                ForceReferenceCoordinateSystem = forceReferencePointCoordinateSystem;
                FailureAnalysisType = failureAnalysisType;
            }

            protected SectionOptions(SerializationInfo info, StreamingContext context) 
            {
                ForceReferenceCoordinateSystem = (CoordinateSystem)info.GetValue("ForceReferenceCoordinateSystem", typeof(CoordinateSystem));
                FailureAnalysisType = (SectionSolver.FailureAnalysisTypes)info.GetValue("FailureAnalysisType", typeof(SectionSolver.FailureAnalysisTypes));
            }

            public override bool Equals(object obj)
            {
                return obj is SectionOptions options && 
                    ForceReferenceCoordinateSystem.Equals(options.ForceReferenceCoordinateSystem) &&
                    FailureAnalysisType.Equals(options.FailureAnalysisType);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = -17;
                    hashCode = hashCode * -23 + ForceReferenceCoordinateSystem.GetHashCode();
                    hashCode = hashCode * -23 + FailureAnalysisType.GetHashCode();
                    return hashCode; 
                }
            }

            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ForceReferenceCoordinateSystem", ForceReferenceCoordinateSystem);
                info.AddValue("FailureAnalysisType", FailureAnalysisType);
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

        #endregion
    }
}
