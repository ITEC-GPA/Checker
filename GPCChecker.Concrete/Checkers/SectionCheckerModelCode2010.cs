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
        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptions options, StandardModelCode2010 standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, id)
        {

        }

        public override FailureDomainResult GetFailureDomainResult()
        {
            var solver = new SectionSolverModelCode2010(_checkerAttributes.Section, StandardModelCode2010);

            return solver.GetFailureDomainResults(_checkerAttributes.ULSResults);
        }

        public async override Task<FailureDomainResult> GetFailureDomainResultAsync()
        {
            FailureDomainResult failureDomainResult = null;

            await Task.Run(() => {
                var solver = new SectionSolverModelCode2010(_checkerAttributes.Section, StandardModelCode2010);

                failureDomainResult = solver.GetFailureDomainResults(_checkerAttributes.ULSResults);
            });

            return failureDomainResult;
        }


        public override StressAnalysisResult[] GetStressAnalysisResult()
        {
            var solver = new SectionSolverModelCode2010(_checkerAttributes.Section, StandardModelCode2010);

            if (_checkerAttributes.SLSResults is null)
                return null;

            return solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.AxialForceReferencePoint);
        }


        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {
            StressAnalysisResult[] stressAnalysisResult = null;

            if (_checkerAttributes.SLSResults is null)
                return null;

            await Task.Run(() => {
                var solver = new SectionSolverModelCode2010(_checkerAttributes.Section, StandardModelCode2010);

                stressAnalysisResult = solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.AxialForceReferencePoint);
            });

            return stressAnalysisResult;
        }


        public class SectionOptionsModelCode2010 : SectionOptions
        {
            public SectionOptionsModelCode2010()
            {

            }

            public SectionOptionsModelCode2010(Point2d axialForceReferencePoint)
                : base(axialForceReferencePoint)
            {

            }
        }
    }
}
