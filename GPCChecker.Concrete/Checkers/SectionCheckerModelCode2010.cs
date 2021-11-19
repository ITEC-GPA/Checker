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


        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, int)"/>
        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options, StandardModelCode2010 standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, new SectionSolverModelCode2010(checkerAttribute.Section, standard), id)
        {

        }

        #region Public Async

        public async override Task<FailureDomainResult> GetFailureDomainResultAsync()
        {
            FailureDomainResult failureDomainResult = null;

            await Task.Run(() =>
            {
                failureDomainResult = _solver.GetFailureDomainResults(_checkerAttributes.ULSResults);
            });

            return failureDomainResult;
        }

        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {
            StressAnalysisResult[] stressAnalysisResult = null;

            if (_checkerAttributes.SLSResults is null)
                return null;

            await Task.Run(() =>
            {
                stressAnalysisResult = _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.AxialForceReferencePoint);
            });

            return stressAnalysisResult;
        }





        #endregion

        #region Internal

        internal override FailureDomainResult GetFailureDomainResult()
        {
            return _solver.GetFailureDomainResults(_checkerAttributes.ULSResults);
        }

        internal override StressAnalysisResult[] GetStressAnalysisResult()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.AxialForceReferencePoint);
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
