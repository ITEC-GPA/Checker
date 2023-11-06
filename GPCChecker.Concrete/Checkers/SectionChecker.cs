using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Results;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class SectionChecker : Checker, ISerializable
    {
        #region Fields

        /// <summary>
        /// Standard for steel structural sections, like for example IPE300 inside reinforced concrete.
        /// </summary>
        protected readonly Standard _standardStructuralSteel;

        protected readonly SectionSolver _solver;

        protected readonly SectionCheckerAttribute _checkerAttributes;

        #endregion

        #region Properties

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
        public async Task<FailureDomainResult> GetPlasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptions);

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

        /// <summary>
        /// Calculate the plastic failure domain 2d and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public async Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptions, teta);

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

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public async Task<FailureDomainResult> GetElasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptions);

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

        /// <summary>
        /// Calculate the elastic failure domain2d and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public async Task<FailureDomainResult2d> GetElasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptions, teta);

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

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptions);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi, double psiTendon = 0)
        {

            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptions);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="psi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi, double psiTendon = 0)
        {

            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptions);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="psi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult> GetStressAnalysisResultAsync(ResultBeamForces forces)
        {
            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResult(forces, SectionCheckerOptions);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        #endregion

        #region Public Method

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult GetPlasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult(SectionCheckerOptions);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult(SectionCheckerOptions);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult2d GetPlasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(SectionCheckerOptions);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult2d GetElasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult2d(SectionCheckerOptions);

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult[] GetStressAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
        {
            return _solver.GetStressAnalysisResult(forces, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult[] GetLinearStressAnalysisResult(double psi, double psiTendon = 0)
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi, double psiTendon = 0)
        {
            return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the plastic domain point for input force
        /// </summary>
        /// <returns>The failure domain point</returns>
        public FailureDomain.FailureDomainPoint CalculatePlasticFailureDomainPoint(ResultBeamForces force)
        {
            return _solver.CalculatePlasticDomainPoint(force, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the elastic domain point for input force
        /// </summary>
        /// <returns>The failure domain point</returns>
        public FailureDomain.FailureDomainPoint CalculateElasticFailureDomainPoint(ResultBeamForces force)
        {
            return _solver.CalculateElasticDomainPoint(force, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the plastic domain point for input force with intersection method
        /// </summary>
        /// <returns>The failure domain point</returns>
        public FailureDomain.FailureDomainPoint CalculatePlasticFailureDomainPoint(ResultBeamForces force, Mesh domainMesh, Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            return _solver.CalculateDomainPoint(force, domainMesh, vertexToDomainPoint, SectionCheckerOptions.FailureAnalysisType, SectionSolver.FailureDomainTypes.Plastic);
        }

        /// <summary>
        /// Calculate the elastic domain point for input force with intersection method
        /// </summary>
        /// <returns>The failure domain point</returns>
        public FailureDomain.FailureDomainPoint CalculateElasticFailureDomainPoint(ResultBeamForces force, Mesh domainMesh, Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            return _solver.CalculateDomainPoint(force, domainMesh, vertexToDomainPoint, SectionCheckerOptions.FailureAnalysisType, SectionSolver.FailureDomainTypes.Elastic);
        }

        /// <summary>
        /// Calculate the plastic domain points for input forces with intersection method
        /// </summary>
        /// <returns>The failure domain points</returns>
        public FailureDomain.FailureDomainPoint[] CalculatePlasticFailureDomainPoint(ResultBeamForces[] forces, Mesh domainMesh, Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            return _solver.CalculateDomainPoint(forces, domainMesh, vertexToDomainPoint, SectionCheckerOptions.FailureAnalysisType, SectionSolver.FailureDomainTypes.Plastic);
        }

        /// <summary>
        /// Calculate the elastic domain points for input forces with intersection method
        /// </summary>
        /// <returns>The failure domain points</returns>
        public FailureDomain.FailureDomainPoint[] CalculateElasticFailureDomainPoint(ResultBeamForces[] forces, Mesh domainMesh, Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            return _solver.CalculateDomainPoint(forces, domainMesh, vertexToDomainPoint, SectionCheckerOptions.FailureAnalysisType, SectionSolver.FailureDomainTypes.Elastic);
        }

        #endregion

        #region Nested class

        [Serializable]
        public abstract class SectionOptions : Options, ISerializable
        {
            public CoordinateSystem ForceReferenceCoordinateSystem { get; set; }

            public SectionSolver.FailureAnalysisTypes FailureAnalysisType { get; set; }

            public SectionSolver.FailureDomainTypes FailureDomainType { get; set; }

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
