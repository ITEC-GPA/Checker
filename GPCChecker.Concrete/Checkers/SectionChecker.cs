using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using GPC.Geometry;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class SectionChecker : Checker, ISerializable
    {
        #region Fields

        protected readonly SectionSolver _solver;

        protected readonly SectionCheckerAttribute _checkerAttributes;

        #endregion

        #region Properties

        public SectionOptions SectionCheckerOptions => (SectionOptions)_options;

        public SectionSolver SectionSolver => _solver;

        public SectionCheckerAttribute SectionCheckerAttribute => _checkerAttributes;

        #endregion

        #region Constructors

        /// <param name="checkerAttribute">This rapresent one section and multiple forces applied</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <param name="solver"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionOptions options, Standard standard,
            SectionSolver solver, int id = IDUNASSIGNED)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _solver.TetaDiscretization = options.TetaDiscretization;
            _solver.ConsiderTensileConcrete = options.ConsiderTensileConcrete;
        }

        #endregion

        #region Public Async Methods

        /// <summary>
        /// Calculate the failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public async Task<FailureDomainResult> GetFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    FailureDomainResult failureDomainResult = null;

                    if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Plastic)
                        failureDomainResult = _solver.GetPlasticFailureDomainResult();
                    else if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Elastic)
                        failureDomainResult = _solver.GetElasticFailureDomainResult();

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
        public async Task<FailureDomainResult2d> GetFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    FailureDomainResult2d failureDomainResult = null;
                    if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Plastic)
                        failureDomainResult = _solver.GetPlasticFailureDomainResult2d(teta);
                    else if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Elastic)
                        failureDomainResult = _solver.GetElasticFailureDomainResult2d(teta);

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
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult> GetTensionAnalysisResultAsync(ResultBeamForces forces)
        {
            if (forces is null)
                return null;

            if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.Linear)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        return _solver.GetLinearStressAnalysisResult(forces, SectionCheckerOptions.PsiCoefficientRebar, SectionCheckerOptions.PsiCoefficientTendon);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
            }
            else if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.NonLinear)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        return _solver.GetStressAnalysisResult(forces);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public async Task<StressAnalysisResult[]> GetTensionAnalysisResultAsync()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.Linear)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptions.PsiCoefficientRebar, SectionCheckerOptions.PsiCoefficientTendon, SectionCheckerOptions);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
            }
            else if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.NonLinear)
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calculate the failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult GetFailureDomainResult()
        {
            FailureDomainResult failureDomainResult;
            if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Plastic)
                failureDomainResult = _solver.GetPlasticFailureDomainResult();
            else if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Elastic)
                failureDomainResult = _solver.GetElasticFailureDomainResult();
            else
                failureDomainResult = null;

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        public FailureDomainResult2d GetFailureDomainResult2d()
        {
            FailureDomainResult2d failureDomainResult;
            if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Plastic)
                failureDomainResult = _solver.GetPlasticFailureDomainResult2d();
            else if (SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Elastic)
                failureDomainResult = _solver.GetElasticFailureDomainResult2d();
            else
                failureDomainResult = null;

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult[] GetTensionAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.NonLinear)
                return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults);
            else if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.Linear)
                return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, SectionCheckerOptions.PsiCoefficientRebar, SectionCheckerOptions.PsiCoefficientTendon, SectionCheckerOptions);
            else
                return null;
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        public StressAnalysisResult GetTensionAnalysisResult(ResultBeamForces forces)
        {
            if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.NonLinear)
                return _solver.GetStressAnalysisResult(forces);
            else if (SectionCheckerOptions.StressAnalysisType == SectionSolver.StressAnalysisTypes.Linear)
                return _solver.GetLinearStressAnalysisResult(forces, SectionCheckerOptions.PsiCoefficientRebar, SectionCheckerOptions.PsiCoefficientTendon);
            else
                return null;
        }

        public void SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes domainPointStrategyTypes)
        {
            _solver.SetDomainPointStrategy(domainPointStrategyTypes);
        }

        #endregion

        #region Internal Async Methods

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal async Task<StressAnalysisResult> GetLinearStressAnalysisResultAsync(ResultBeamForces forces, double psi, double psiTendon = 0)
        {

            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon);
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
        internal async Task<StressAnalysisResult[]> GetStressAnalysisResultAsync()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults);
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
        internal async Task<StressAnalysisResult[]> GetLinearStressAnalysisResultAsync(double psi, double psiTendon = 0)
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
        internal async Task<StressAnalysisResult> GetStressAnalysisResultAsync(ResultBeamForces forces)
        {
            if (forces is null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return _solver.GetStressAnalysisResult(forces);
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal async Task<FailureDomainResult> GetPlasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult();

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
        internal async Task<FailureDomainResult2d> GetPlasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetPlasticFailureDomainResult2d(teta);

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
        internal async Task<FailureDomainResult> GetElasticFailureDomainResultAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult();

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
        internal async Task<FailureDomainResult2d> GetElasticFailureDomainResult2dAsync(double teta = 0)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var failureDomainResult = _solver.GetElasticFailureDomainResult2d(teta);

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

        #endregion

        #region Internal Methods

        /// <summary>
        /// Calculate the plastic domain point for input force
        /// </summary>
        /// <returns>The failure domain point</returns>
        public FailureDomain.FailureDomainPoint CalculateFailureDomainPoint(ResultBeamForces force, SectionSolver.FailureAnalysisTypes? failureAnalysisTypeOverride = null)
        {
            return _solver.CalculateDomainPoint(force, failureAnalysisTypeOverride);
        }

        /// <summary>
        /// Calculate the domain points for input forces with intersection method
        /// </summary>
        /// <returns>The failure domain points</returns>
        internal FailureDomain.FailureDomainPoint[] CalculateFailureDomainPoint(ResultBeamForces[] forces)
        {
            return _solver.CalculateDomainPoint(forces);
        }

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal FailureDomainResult GetPlasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal FailureDomainResult GetElasticFailureDomainResult()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the plastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal FailureDomainResult2d GetPlasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetPlasticFailureDomainResult2d();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the elastic failure domain and calculate the domain point for each forces
        /// </summary>
        /// <returns>The failure domain results</returns>
        internal FailureDomainResult2d GetElasticFailureDomainResult2d()
        {
            var failureDomainResult = _solver.GetElasticFailureDomainResult2d();

            if (_checkerAttributes.ULSResults != null)
                failureDomainResult.AddForces(_checkerAttributes.ULSResults);

            return failureDomainResult;
        }

        /// <summary>
        /// Calculate the stress analysis for each forces
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal StressAnalysisResult[] GetStressAnalysisResult()
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetStressAnalysisResults(_checkerAttributes.SLSResults);
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal StressAnalysisResult GetStressAnalysisResult(ResultBeamForces forces)
        {
            return _solver.GetStressAnalysisResult(forces);
        }

        /// <summary>
        /// Calculate the stress analysis for each forces with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal StressAnalysisResult[] GetLinearStressAnalysisResult(double psi, double psiTendon = 0)
        {
            if (_checkerAttributes.SLSResults is null)
                return null;

            return _solver.GetLinearStressAnalysisResults(_checkerAttributes.SLSResults, psi, psiTendon, SectionCheckerOptions);
        }

        /// <summary>
        /// Calculate the stress analysis for <paramref name="forces"/> with creep coefficient <paramref name="phi"/>
        /// </summary>
        /// <returns>The stress analysis results</returns>
        internal StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces forces, double psi, double psiTendon = 0)
        {
            return _solver.GetLinearStressAnalysisResult(forces, psi, psiTendon);
        }

        #endregion

        #region Nested class

        [Serializable]
        public abstract class SectionOptions : Options, ISerializable
        {
            public CoordinateSystem ForceReferenceCoordinateSystem { get; set; }

            public SectionSolver.FailureAnalysisTypes FailureAnalysisType { get; set; }

            public SectionSolver.FailureDomainTypes FailureDomainType { get; set; }

            public SectionSolver.StressAnalysisTypes StressAnalysisType { get; set; }

            public double PsiCoefficientRebar { get; set; }

            public double PsiCoefficientTendon { get; set; }

            public bool ConsiderTensileConcrete { get; set; }

            public int TetaDiscretization { get; set; }

            public SectionSolver.DomainPointStrategyTypes DomainPointStrategy { get; set; }

            public SectionOptions()
            {
                ForceReferenceCoordinateSystem = CoordinateSystem.Global;
            }

            public SectionOptions(CoordinateSystem forceReferencePointCoordinateSystem, SectionSolver.FailureAnalysisTypes failureAnalysisType, SectionSolver.FailureDomainTypes failureDomainType,
                SectionSolver.StressAnalysisTypes stressAnalysisType, double psiCoefficientRebar, double psiCoefficientTendon, bool considerTensileConcrete, int tetaDiscretization)
            {
                ForceReferenceCoordinateSystem = forceReferencePointCoordinateSystem;
                FailureAnalysisType = failureAnalysisType;
                FailureDomainType = failureDomainType;
                StressAnalysisType = stressAnalysisType;
                PsiCoefficientRebar = psiCoefficientRebar;
                PsiCoefficientTendon = psiCoefficientTendon;
                ConsiderTensileConcrete = considerTensileConcrete;
                TetaDiscretization = tetaDiscretization;
                DomainPointStrategy = SectionSolver.DomainPointStrategyTypes.Iterative;
            }

            protected SectionOptions(SerializationInfo info, StreamingContext context)
            {
                ForceReferenceCoordinateSystem = (CoordinateSystem)info.GetValue("ForceReferenceCoordinateSystem", typeof(CoordinateSystem));
                FailureAnalysisType = (SectionSolver.FailureAnalysisTypes)info.GetValue("FailureAnalysisType", typeof(SectionSolver.FailureAnalysisTypes));
                FailureDomainType = (SectionSolver.FailureDomainTypes)info.GetValue("FailureDomainType", typeof(SectionSolver.FailureDomainTypes));
                StressAnalysisType = (SectionSolver.StressAnalysisTypes)info.GetValue("StressAnalysisType", typeof(SectionSolver.StressAnalysisTypes));
                PsiCoefficientRebar = info.GetDouble("PsiCoefficientRebar");
                PsiCoefficientTendon = info.GetDouble("PsiCoefficientTendon");
                ConsiderTensileConcrete = info.GetBoolean("ConsiderTensileConcrete");
                TetaDiscretization = info.GetInt16("TetaDiscretization");
                DomainPointStrategy = (SectionSolver.DomainPointStrategyTypes)info.GetValue("DomainPointStrategy", typeof(SectionSolver.DomainPointStrategyTypes));
            }

            public override bool Equals(object obj)
            {
                return obj is SectionOptions options &&
                    ForceReferenceCoordinateSystem.Equals(options.ForceReferenceCoordinateSystem) &&
                    FailureDomainType.Equals(options.FailureDomainType) &&
                    StressAnalysisType.Equals(options.StressAnalysisType) &&
                    PsiCoefficientRebar.Equals(options.PsiCoefficientRebar) &&
                    PsiCoefficientTendon.Equals(options.PsiCoefficientTendon) &&
                    ConsiderTensileConcrete.Equals(options.ConsiderTensileConcrete) &&
                    TetaDiscretization.Equals(options.TetaDiscretization) &&
                    FailureAnalysisType.Equals(options.FailureAnalysisType);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = -17;
                    hashCode = hashCode * -23 + ForceReferenceCoordinateSystem.GetHashCode();
                    hashCode = hashCode * -23 + FailureAnalysisType.GetHashCode();
                    hashCode = hashCode * -23 + FailureDomainType.GetHashCode();
                    hashCode = hashCode * -23 + StressAnalysisType.GetHashCode();
                    hashCode = hashCode * -23 + PsiCoefficientRebar.GetHashCode();
                    hashCode = hashCode * -23 + ConsiderTensileConcrete.GetHashCode();
                    hashCode = hashCode * -23 + TetaDiscretization.GetHashCode();
                    hashCode = hashCode * -23 + PsiCoefficientTendon.GetHashCode();
                    return hashCode;
                }
            }

            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ForceReferenceCoordinateSystem", ForceReferenceCoordinateSystem);
                info.AddValue("FailureAnalysisType", FailureAnalysisType);
                info.AddValue("FailureDomainType", FailureDomainType);
                info.AddValue("StressAnalysisType", StressAnalysisType);
                info.AddValue("PsiCoefficientRebar", PsiCoefficientRebar);
                info.AddValue("PsiCoefficientTendon", PsiCoefficientTendon);
                info.AddValue("ConsiderTensileConcrete", ConsiderTensileConcrete);
                info.AddValue("TetaDiscretization", TetaDiscretization);
                info.AddValue("DomainPointStrategy", DomainPointStrategy);
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
