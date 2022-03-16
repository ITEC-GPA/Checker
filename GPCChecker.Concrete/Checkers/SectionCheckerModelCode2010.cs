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
		#region Properties

		public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public SectionOptionsModelCode2010 SectionCheckerOptionsModelCode2010 => (SectionOptionsModelCode2010)_options;

		#endregion

		#region Constructor

		/// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
		public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options, 
            StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED)
            : base(checkerAttribute, options, standard, new SectionSolverModelCode2010(checkerAttribute.Section, standard, considerTensileConcrete), id)
        {

        }

        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options,
            StandardModelCode2010 standard, SectionSolverModelCode2010 solver)
            : base(checkerAttribute, options, standard, solver)
		{

		}

        #endregion

        #region Public Async

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResultAsync"/>
        public async override Task<FailureDomainResult> GetPlasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

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

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResult2dAsync"/>
        public async override Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

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

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResultAsync"/>
        public async override Task<FailureDomainResult> GetElasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

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

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult2dAsync"/>
        public async override Task<FailureDomainResult2d> GetElasticFailureDomainResult2dAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

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

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResultAsync"/>
        public async override Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResultAsync(ResultBeamForces, double)"/>
        public async override Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi)
        {

            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResult(forces, psi, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResultAsync(double)"/>
        public async override Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                { 
                    return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
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
                    return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
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
            var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult"/>
        public override FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        public override FailureDomainResult2d GetPlasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        public override FailureDomainResult2d GetElasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult"/>
        public override StressAnalysisResult[] GetStressAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(double)"/>
        public override StressAnalysisResult[] GetLinearStressAnalysisResult(double psi)
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult(ResultBeamForces)"/>
        public override StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
		{
            return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(ResultBeamForces, double)"/>
        public override StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi)
		{
            return _solver.GetLinearStressAnalysisResult(forces, psi, SectionCheckerOptionsModelCode2010.ForceReferenceCoordinateSystem);
        }

		#endregion


		[Serializable]
        public class SectionOptionsModelCode2010 : SectionOptions, ISerializable
        {
            public SectionOptionsModelCode2010(CoordinateSystem coordinateSystem)
                : base(coordinateSystem)
            {

            }

            protected SectionOptionsModelCode2010(SerializationInfo info, StreamingContext context)
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
