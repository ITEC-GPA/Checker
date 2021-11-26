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
    public class SectionCheckerModelCode2010 : SectionChecker, ISerializable
    {

        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public SectionOptionsModelCode2010 SectionCheckerOptionsModelCode2010 => (SectionOptionsModelCode2010)_options;


        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options, StandardModelCode2010 standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, new SectionSolverModelCode2010(checkerAttribute.Section, standard), id)
        {

        }

        #region Public Async

        public async override Task<FailureDomainResult> GetFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                var failureDomainResult = _solver.GetFailurePlasticDomainResults();

                if (_checkerAttributes.ULSResults != null)
                    failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                return failureDomainResult;
            });
        }

        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
            });
        }

        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync(double n)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                return _solver.GetStressAnalysisResult(_checkerAttributes.SLSResults, n, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
            });
        }

        #endregion

        #region Internal

        internal FailureDomainResult GetElasticDomain()
        {
            var failureDomainResult = _solver.GetFailureElasticDomainResult();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }
        
        internal override FailureDomainResult GetFailureDomainResult()
        {
            var failureDomainResult = _solver.GetFailurePlasticDomainResults();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        internal override StressAnalysisResult[] GetStressAnalysisResult()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
        }

        internal override StressAnalysisResult[] GetStressAnalysisResult(double n)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResult(_checkerAttributes.SLSResults, n, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
        }




		#endregion

		public class SectionOptionsModelCode2010 : SectionOptions
        {

            public SectionOptionsModelCode2010()
                : base()
            {

            }

            public SectionOptionsModelCode2010(Point2d axialForceReferencePoint, bool plasticFailureDomain)
                : base(axialForceReferencePoint, plasticFailureDomain)
            {

            }
        }
    }
}
