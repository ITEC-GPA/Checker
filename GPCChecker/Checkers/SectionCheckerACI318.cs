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
    public class SectionCheckerACI318 : SectionChecker, ISerializable
    {
        public StandardACI318 StandardACI318 => (StandardACI318)_standard;

        public SectionOptionsStandardACI318 SectionCheckerOptionsACI318 => (SectionOptionsStandardACI318)_options;


        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerACI318(SectionCheckerAttribute checkerAttribute, SectionOptionsStandardACI318 options,
            StandardACI318 standard, bool haveSpiral, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, 
                  new SectionSolverACI318(checkerAttribute.Section, standard, haveSpiral, considerTensileConcrete), id)
        {

        }

        #region Public Async

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResultAsync"/>
        public async override Task<FailureDomainResult> GetPlasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsACI318);

                    if (_checkerAttributes.ULSResults != null && failureDomainResult != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResultAsync"/>
        public async override Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsACI318, teta);

                    if (_checkerAttributes.ULSResults != null && failureDomainResult != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResultAsync"/>
        public async override Task<FailureDomainResult> GetElasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsACI318);

                    if (_checkerAttributes.ULSResults != null && failureDomainResult != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult2dAsync"/>
        public async override Task<FailureDomainResult2d> GetElasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsACI318, teta);

                    if (_checkerAttributes.ULSResults != null && failureDomainResult != null)
                        failureDomainResult.AddForces(_checkerAttributes.ULSResults);

                    return failureDomainResult;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResultAsync"/>
        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsACI318);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResultAsync(ResultBeamForces, double)"/>
        public async override Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi, double psiTendon = 0)
        {

            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptionsACI318);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResultAsync(double)"/>
        public async override Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi, double psiTendon = 0)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptionsACI318);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResultAsync(ResultBeamForces)"/>
        public async override Task<StressAnalysisResult> GetStressAnalysisResultAsync(ResultBeamForces forces)
        {
            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsACI318);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        #endregion

        #region Internal

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResult"/>
        public override FailureDomainResult GetPlasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsACI318);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResult2d"/>
        public override FailureDomainResult2d GetPlasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsACI318);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult"/>
        public override FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsACI318);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult2d"/>
        public override FailureDomainResult2d GetElasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsACI318);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult"/>
        public override StressAnalysisResult[] GetStressAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsACI318);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(double)"/>
        public override StressAnalysisResult[] GetLinearStressAnalysisResult(double psi, double psiTendon = 0)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon,
                SectionCheckerOptionsACI318);
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult(ResultBeamForces)"/>
        public override StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
		{
            return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsACI318);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(ResultBeamForces, double)"/>
        public override StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi, double psiTendon = 0)
		{
            return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptionsACI318);
        }

        public override FailureDomain.FailureDomainPoint CalculatePlasticFailureDomainPoint(ResultBeamForces force)
		{
            return _solver.CalculatePlasticDomainPoint(force, SectionCheckerOptionsACI318);
        }

        public override FailureDomain.FailureDomainPoint CalculateElasticFailureDomainPoint(ResultBeamForces force)
        {
            return _solver.CalculateElasticDomainPoint(force, SectionCheckerOptionsACI318);
        }

        #endregion


        [Serializable]
        public class SectionOptionsStandardACI318 : SectionOptions, ISerializable
        {
            public SectionOptionsStandardACI318(CoordinateSystem coordinateSystem,
                SectionSolver.FailureAnalysisTypes failureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
                : base(coordinateSystem, failureAnalysisType)
            {

            }

            protected SectionOptionsStandardACI318(SerializationInfo info, StreamingContext context)
                :base(info, context) 
            {
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
            }
        }
    }
}
