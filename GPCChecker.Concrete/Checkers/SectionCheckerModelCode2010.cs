using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

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
            StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED,
            StandardEN1993p11 standardStructuralSteel = null)
            : base(checkerAttribute, options, standard,
                  new SectionSolverModelCode2010(checkerAttribute.Section, standard, options.ForceReferenceCoordinateSystem.Origin, considerTensileConcrete, id, standardStructuralSteel),
                  id, standardStructuralSteel)
        {
        }

        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options,
            StandardModelCode2010 standard, SectionSolverModelCode2010 solver, int id = ModelObjectId.IDUNASSIGNED,
            StandardEN1993p11 standardStructuralSteel = null)
            : base(checkerAttribute, options, standard, solver, id, standardStructuralSteel)
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
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsModelCode2010);

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

        /// <inheritdoc cref="SectionChecker.GetPlasticFailureDomainResult2dAsync"/>
        public async override Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010, teta);

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
                    var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsModelCode2010);

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
                    var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010, teta);

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
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010);
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
                    return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptionsModelCode2010);
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
                    return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptionsModelCode2010);
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
                    return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010);
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
            var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptionsModelCode2010);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetElasticFailureDomainResult"/>
        public override FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptionsModelCode2010);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        public override FailureDomainResult2d GetPlasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        public override FailureDomainResult2d GetElasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptionsModelCode2010);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult"/>
        public override StressAnalysisResult[] GetStressAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptionsModelCode2010);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(double)"/>
        public override StressAnalysisResult[] GetLinearStressAnalysisResult(double psi, double psiTendon = 0)
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptionsModelCode2010);
        }

        /// <inheritdoc cref="SectionChecker.GetStressAnalysisResult(ResultBeamForces)"/>
        public override StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
        {
            return _solver.GetStressAnalysisResult(forces, SectionCheckerOptionsModelCode2010);
        }

        /// <inheritdoc cref="SectionChecker.GetLinearStressAnalysisResult(ResultBeamForces, double)"/>
        public override StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi, double psiTendon = 0)
        {
            return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptionsModelCode2010);
        }

        public override FailureDomain.FailureDomainPoint CalculatePlasticFailureDomainPoint(ResultBeamForces force)
        {
            return _solver.CalculatePlasticDomainPoint(force, SectionCheckerOptionsModelCode2010);
        }

        public override FailureDomain.FailureDomainPoint CalculateElasticFailureDomainPoint(ResultBeamForces force)
        {
            return _solver.CalculateElasticDomainPoint(force, SectionCheckerOptionsModelCode2010);
        }

        #endregion

        #region Nested class

        [Serializable]
        public class SectionOptionsModelCode2010 : SectionOptions, ISerializable
        {
            public SectionOptionsModelCode2010(CoordinateSystem coordinateSystem, SectionSolver.FailureAnalysisTypes failureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
                : base(coordinateSystem, failureAnalysisType)
            {

            }

            public SectionOptionsModelCode2010()
                : base(CoordinateSystem.Global, SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
            {

            }

            protected SectionOptionsModelCode2010(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
            }
        }

        #endregion
    }
}
