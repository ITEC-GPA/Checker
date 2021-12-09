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
    public class SectionCheckerModelCode2010 : SectionChecker, ISerializable
    {

        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public SectionOptionsModelCode2010 SectionCheckerOptionsModelCode2010 => (SectionOptionsModelCode2010)_options;


        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options, StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, new SectionSolverModelCode2010(checkerAttribute.Section, standard, considerTensileConcrete), id)
        {

        }

        #region Public Async

        public async override Task<FailureDomainResult> GetPlasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult();

                    if (_checkerAttributes.ULSResults != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async override Task<FailureDomainResult> GetElasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult();

                    if (_checkerAttributes.ULSResults != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async override Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi)
        {

            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResult(forces, psi, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async override Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async override Task<StressAnalysisResult> GetStressAnalysisResultAsync(ResultBeamForces forces)
        {
            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        #endregion

        #region Internal

        internal override FailureDomainResult GetPlasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        internal override FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult();

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

        internal override StressAnalysisResult[] GetLinearStressAnalysisResult(double psi)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
        }

		internal override StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
		{
            return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
        }

		internal override StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi)
		{
            return _solver.GetLinearStressAnalysisResult(forces, psi, SectionCheckerOptionsModelCode2010.ForceReferencePointCentroidDistance);
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
