using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Utilities.Converters;
using GPC.Utilities.Extensions;
using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    [Serializable]
    public abstract class SectionSolver : ModelObjectId, ISerializable
    {
        #region Public enum 

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 1),
            (FailureZones.F2A, 1),
            (FailureZones.F2B, 1),
            (FailureZones.F3A, 30),
            (FailureZones.F3B, 5),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizations =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 10),
            (FailureZones.F4, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 25),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for elastic analysis for FRC material
        /// </summary>
        protected readonly (FailureZones, int)[] _elasticFailureZonesDiscretizationsFRC =
        {
            (FailureZones.F1, 3),
            (FailureZones.F2A, 5),
            (FailureZones.F3A, 5),
            (FailureZones.F4, 5)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material with no rebars and hardening behaviour
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRCNoRebarsHardening =
        {
            (FailureZones.F1, 2),
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 25),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver for plastic analysis for FRC material with no rebars and softening behaviour
        /// </summary>
        protected readonly (FailureZones, int)[] _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening =
        {
            (FailureZones.F2A, 5),
            (FailureZones.F2B, 5),
            (FailureZones.F3A, 25),
            (FailureZones.F3B, 1),
            (FailureZones.F4, 4)
        };

        public enum FailureZones
        {
            /// <summary>
            /// Around P1. From espSu to 0 
            /// </summary>
            F1 = 1,

            /// <summary>
            /// Around P1. From 0 to espCy 
            /// </summary>
            F2A = 2,

            /// <summary>
            /// Around P1. From espCy to espCu 
            /// </summary>
            F2B = 3,

            /// <summary>
            /// Around P2. From espSu to espSy 
            /// </summary>
            F3A = 4,

            /// <summary>
            /// Around P2. From espSy to 0 
            /// </summary>
            F3B = 5,

            /// <summary>
            /// Around P3. From 0 to espCyCost 
            /// </summary>
            F4 = 6,
        }

        public enum FailureDomainTypes
        {
            Elastic,
            Plastic,
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum FailureAnalysisTypes
        {
            [Description("Constant N")]
            ConstantN,

            [Description("Constant eccentricity")]
            ConstantEccentricity,

            [Description("Constant Mx - My")]
            ConstantMxMy,

            [Description("Constant N and Mx")]
            ConstantNMx,

            [Description("Constant N and My")]
            ConstantNMy,
        }

        #endregion

        #region Variables

        protected double _stressAnalysisTolerance;
        protected double _failureAnalysisAngularTolerance;
        protected bool _considerTensileConcrete;

        protected IConcreteSection _concreteSection;
        protected Standard _standard;

        protected List<string> _log;
        protected int _tetaDiscretization;

        protected QuadrangleGaussPoints.GaussPointNumber _gaussIntegrationQuadPoints;
        protected TriangleGaussPoints.GaussPointNumber _gaussIntegrationTriPoints;

        protected GaussIntegration.GlobalCoordinateGaussPoint[][] _globalCoordinateGaussPoints;

        #endregion

        #region Properties

        public IConcreteSection ConcreteSection => _concreteSection;

        public ConcreteMaterial ConcreteMaterial => _concreteSection.ConcreteMaterial;

        public Standard Standard => _standard;

        public bool ConsiderTensileConcrete { get => _considerTensileConcrete; internal set => _considerTensileConcrete = value; }

        #endregion

        #region Constructor

        internal SectionSolver(IConcreteSection section, Standard standard, bool considerTensileConcrete, int id)
            : base(id)
        {
            _concreteSection = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _log = new List<string>();

            _stressAnalysisTolerance = 1e-5;
            _failureAnalysisAngularTolerance = 1.0e-3;

            _considerTensileConcrete = considerTensileConcrete;
            _tetaDiscretization = 16;

            _gaussIntegrationQuadPoints = QuadrangleGaussPoints.GaussPointNumber.Quad400;
            _gaussIntegrationTriPoints = TriangleGaussPoints.GaussPointNumber.Tri79;

            _globalCoordinateGaussPoints = GetGlobalCoordinateGaussPointsLinearShapeFunction();
        }

        protected SectionSolver(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _concreteSection = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _log = (List<string>)info.GetValue("Log", typeof(List<string>));
            _stressAnalysisTolerance = info.GetDouble("StressAnalysisTolerance");
            _failureAnalysisAngularTolerance = info.GetDouble("FailureAnalysisAngularTolerance");
            _tetaDiscretization = info.GetInt32("TetaDiscretization");
            _gaussIntegrationQuadPoints = (QuadrangleGaussPoints.GaussPointNumber)info.GetValue("GaussIntegrationQuadPoints", typeof(QuadrangleGaussPoints.GaussPointNumber));
            _gaussIntegrationTriPoints = (TriangleGaussPoints.GaussPointNumber)info.GetValue("GaussIntegrationTriPoints", typeof(TriangleGaussPoints.GaussPointNumber));
            _considerTensileConcrete = info.GetBoolean("ConsiderTensileConcrete");
        }

        #endregion

        #region Abstract Method

        protected abstract double GetDesignYieldingStrainRebar(ReinforcedConcreteRebar rebar);
        protected abstract double GetDesignYieldingStrainRebar(int rebar);
        protected abstract double GetDesignUltimateStrainRebar(ReinforcedConcreteRebar rebar);
        protected abstract double GetDesignUltimateStrainRebar(int rebar);

        protected abstract double GetUltimateStrainConcreteCompression();
        protected abstract double GetYieldingStrainConcreteCompression();
        protected abstract double GetYieldingStrainPureCompression();
        protected abstract double GetYieldingStrainConcreteTension();
        protected abstract double GetUltimateStrainConcreteTension();

        protected abstract double GetFck();

        /// <returns>The design concrete stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateSigmaC(double strain);

        /// <returns>The design steel stress related to <paramref name="strain"/></returns>
        internal abstract double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain);

        protected abstract double GetReductionFactor(StrainPlane strainPlane);

        protected abstract ForceTuple CalculatePureCompressionReduction(ForceTuple force);

        #endregion

        #region Public method

        public virtual FailureDomainResult GetElasticFailureDomainResult(Checkers.SectionChecker.SectionOptions sectionOption)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization,
                zoneDiscretization, FailureDomainTypes.Elastic);

            return new FailureDomainResult(ConcreteSection,
                CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic),
                null, this, _standard, sectionOption, Id);
        }

        public virtual FailureDomainResult2d GetElasticFailureDomainResult2d(Checkers.SectionChecker.SectionOptions sectionOption, double angle = 0)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                zoneDiscretization = _elasticFailureZonesDiscretizations;
            else
                zoneDiscretization = _elasticFailureZonesDiscretizationsFRC;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(2,
                zoneDiscretization, FailureDomainTypes.Elastic, angle);

            return new FailureDomainResult2d(ConcreteSection,
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Elastic).RebuildFailureDomain()),
                null, this, _standard, sectionOption, Id);
        }

        public virtual FailureDomainResult GetPlasticFailureDomainResult(Checkers.SectionChecker.SectionOptions sectionOption)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
            {
                if (ConcreteSection.RebarsCount != 0)
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;
                else
                {
                    if (_concreteSection.ConcreteMaterial.StressStrainTableTension.IsHardening())
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsHardening;
                    else
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening;
                }
            }

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(_tetaDiscretization, zoneDiscretization, FailureDomainTypes.Plastic);

            return new FailureDomainResult(ConcreteSection,
                CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic), null, this, _standard,
                sectionOption, Id);
        }

        public virtual FailureDomainResult2d GetPlasticFailureDomainResult2d(Checkers.SectionChecker.SectionOptions sectionOption, double angle = 0)
        {
            (FailureZones, int)[] zoneDiscretization;

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                zoneDiscretization = _plasticFailureZonesDiscretizations;
            else
            {
                if (ConcreteSection.RebarsCount != 0)
                    zoneDiscretization = _plasticFailureZonesDiscretizationsFRC;
                else
                {
                    if (_concreteSection.ConcreteMaterial.StressStrainTableTension.IsHardening())
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsHardening;
                    else
                        zoneDiscretization = _plasticFailureZonesDiscretizationsFRCNoRebarsSoftening;
                }
            }

            if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                if (ConcreteSection.RebarsCount == 0)
                    return null;

            (StrainPlane, FailureZones)[][] strainPlanes = CalculateDesignFailureStrainPlanes(2,
                zoneDiscretization, FailureDomainTypes.Plastic, angle);

            return new FailureDomainResult2d(ConcreteSection,
                ConvertFailureDomain3dTo2d(CalculateFailureDomain(strainPlanes, sectionOption.ForceReferenceCoordinateSystem, FailureDomainTypes.Plastic).RebuildFailureDomain()), null, this, _standard,
                sectionOption, Id);
        }

        public virtual StressAnalysisResult[] GetStressAnalysisResults(ResultBeamForces[] force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneStressAnalysis(force[i].ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetStressAnalysisResult(ResultBeamForces force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                    CalculateStrainPlaneStressAnalysis(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, _stressAnalysisTolerance), this, _standard, Id);
        }

        public virtual StressAnalysisResult[] GetLinearStressAnalysisResults(ResultBeamForces[] force, double psi, double? psiTendon, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            StressAnalysisResult[] stressAnalysisResults = new StressAnalysisResult[force.Length];

            Parallel.For(0, force.Length, (i) =>
            {
                stressAnalysisResults[i] = new StressAnalysisResult(ConcreteSection, force[i],
                    CalculateStrainPlaneLinearStressAnalysis(force[i].ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                    sectionOption.ForceReferenceCoordinateSystem, psi, psiTendon,
                    _stressAnalysisTolerance), this, _standard, Id);
            });

            return stressAnalysisResults;
        }

        public virtual StressAnalysisResult GetLinearStressAnalysisResult(ResultBeamForces force, double psi, double? psiTendon, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return new StressAnalysisResult(ConcreteSection, force,
                CalculateStrainPlaneLinearStressAnalysis(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem),
                sectionOption.ForceReferenceCoordinateSystem, psi, psiTendon,
                _stressAnalysisTolerance), this, _standard, Id);
        }

        public virtual FailureDomain.FailureDomainPoint CalculatePlasticDomainPoint(ResultBeamForces force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return CalculatePlasticDomainPoint(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem), sectionOption.ForceReferenceCoordinateSystem, sectionOption.FailureAnalysisType);
        }

        public virtual FailureDomain.FailureDomainPoint CalculateElasticDomainPoint(ResultBeamForces force, Checkers.SectionChecker.SectionOptions sectionOption)
        {
            return CalculateElasticDomainPoint(force.ConvertToForceTuple(sectionOption.ForceReferenceCoordinateSystem), sectionOption.ForceReferenceCoordinateSystem, sectionOption.FailureAnalysisType);
        }

        public virtual FailureDomain.FailureDomainPoint CalculatePlasticDomainPoint(ForceTuple force,
            CoordinateSystem coordinateSystem, FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(force, coordinateSystem, FailureDomainTypes.Plastic, failureAnalysisType, _failureAnalysisAngularTolerance);
        }

        public virtual FailureDomain.FailureDomainPoint CalculateElasticDomainPoint(ForceTuple force, CoordinateSystem coordinateSystem,
            FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(force, coordinateSystem, FailureDomainTypes.Elastic, failureAnalysisType, _failureAnalysisAngularTolerance);
        }

        #endregion

        #region Public Setter

        public void SetTetaDiscretization(int discretization)
        {
            _tetaDiscretization = discretization;
        }

        #endregion

        #region Protected method - SectionIntegration

        protected virtual GaussIntegration.GlobalCoordinateGaussPoint[][] GetGlobalCoordinateGaussPointsLinearShapeFunction()
        {
            return GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(ConcreteSection.Mesh, _gaussIntegrationQuadPoints, _gaussIntegrationTriPoints);
        }

        #region Force resultant 

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                return CalculatePureCompressionReduction((IntegrateSectionStress(strainPlane) + IntegrateRebarStress(strainPlane, rebarIsInsideAssociation)) * GetReductionFactor(strainPlane));
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        protected virtual ForceTuple[] CalculateForceResultant(StrainPlane[] strainPlanes, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                ForceTuple[] returnValue = new ForceTuple[strainPlanes.Length];

                ForceTuple[] concreteStresses = IntegrateSectionStress(strainPlanes);
                ForceTuple[] rebarStresses = IntegrateRebarStress(strainPlanes, rebarIsInsideAssociation);

                for (int i = 0; i < strainPlanes.Length; i++)
                    returnValue[i] = CalculatePureCompressionReduction(concreteStresses[i] + rebarStresses[i]) * GetReductionFactor(strainPlanes[i]);

                return returnValue;
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return null;
            }
        }

        /// <inheritdoc cref="CalculateForceResultant(StrainPlane, Dictionary{int, bool})"/>
        protected virtual async Task<ForceTuple> CalculateForceResultantAsync((StrainPlane, FailureZones) strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            return await Task.Run(() =>
            {
                return CalculateForceResultant(strainPlane, rebarIsInsideAssociation);
            });
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant((StrainPlane, FailureZones) strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            return CalculateForceResultant(strainPlane.Item1, rebarIsInsideAssociation);
        }

        /// <summary>
        /// Integrate the stress on the section given by the <paramref name="strainPlane"/> with homogenization coefficient and gives the resultant forces
        /// </summary>
        /// <returns>The forces in the local reference system</returns>
        protected virtual ForceTuple CalculateForceResultant(double psi, double? psiTendon, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            try
            {
                return IntegrateSectionStressLinearElastic(strainPlane) + IntegrateRebarLinearStress(psi, psiTendon, strainPlane, rebarIsInsideAssociation);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                _log.Add(e.InnerException.Message);
                return new ForceTuple();
            }
        }

        #endregion

        #region Concrete Integration

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected virtual ForceTuple IntegrateSectionStress(StrainPlane strainPlane)
        {
            try
            {
                (double s, double, double) ret = GaussIntegration.IntegrationLinearShapeFunction((x, y) =>
                {
					double sigmaC = CalculateSigmaC(strainPlane.GetStrain(x, y));
                    return (sigmaC, -sigmaC * (y - ConcreteSection.Centroid.Y), sigmaC * (x - ConcreteSection.Centroid.X));
                },
                _globalCoordinateGaussPoints);

                return new ForceTuple(ret);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }
        }

        /// <summary>
        /// Calculate the stress resultant of the concrete part
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <returns>
        /// <para>The axial force resultant</para>
        /// <para>The bending moment about X-axis resultant</para>
        /// <para>The bending moment about Y-axis resultant</para>
        /// </returns>
        protected virtual ForceTuple IntegrateSectionStressLinearElastic(StrainPlane strainPlane)
        {
            try
            {
                (double s, double, double) ret = GaussIntegration.IntegrationLinearShapeFunction((x, y) =>
                {
                    double sigmaC = CalculateElasticSigmaC(strainPlane.GetStrain(x, y));
                    return (sigmaC, -sigmaC * (y - ConcreteSection.Centroid.Y), sigmaC * (x - ConcreteSection.Centroid.X));
                },
                _globalCoordinateGaussPoints);

                return new ForceTuple(ret);
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return new ForceTuple();
            }
        }

        protected virtual ForceTuple[] IntegrateSectionStress(StrainPlane[] strainPlane)
        {
            try
            {
                ForceTuple[] returnValue = new ForceTuple[strainPlane.Length];
                Func<double, double, (double, double, double)>[] functions = new Func<double, double, (double, double, double)>[strainPlane.Length];

                for (int i = 0; i < strainPlane.Length; i++)
                {
                    StrainPlane sp = strainPlane[i];
                    functions[i] = new Func<double, double, (double, double, double)>((x, y) => 
                        {
							double sigmaC = CalculateSigmaC(sp.GetStrain(x, y));
                            return (sigmaC, sigmaC * (y - ConcreteSection.Centroid.Y), sigmaC * (x - ConcreteSection.Centroid.X));
                        });
                }

                (double, double, double)[] res = GaussIntegration.IntegrationLinearShapeFunction(functions, _globalCoordinateGaussPoints);

                for (int j = 0; j < strainPlane.Length; j++)
                    returnValue[j] = new ForceTuple(res[j].Item1, -res[j].Item2, res[j].Item3);

                return returnValue;
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                return null;
            }
        }

        /// <inheritdoc cref="IntegrateSectionStress(StrainPlane)"/>
        protected virtual async Task<ForceTuple> IntegrateSectionStressAsync(StrainPlane strainPlane)
        {
            return await Task.Run(() =>
            {
                return IntegrateSectionStress(strainPlane);
            });
        }

        /// <returns>The design concrete stress related to <paramref name="strain"/> with linear elastic stress-strain diagram</returns>
        public double CalculateElasticSigmaC(double strain)
        {
            if (strain < 0)
            {
                // compressione
                return _concreteSection.ConcreteMaterial.E * strain;
            }
            else
            {
                // trazione
                if (_considerTensileConcrete)
                {
                    return _concreteSection.ConcreteMaterial.E * strain;
                }
                else
                {
                    return 0;
                }
            }

        }

        #endregion

        #region Rebar Integration

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        protected virtual ForceTuple IntegrateRebarStress(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var rebars = ConcreteSection.GetRebars();

            double deltaNArray = 0;
            double deltaMxArray = 0;
            double deltaMyArray = 0;

            for (int i = 0; i < rebars.Length; i++)
            {
                double strain = strainPlane.GetStrain(rebars[i].Position);
                double sigmaS = CalculateStressRebar(rebars[i], strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateSigmaC(strain);

                deltaNArray += (sigmaS - sigmaC) * rebars[i].Area;
                deltaMxArray += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray += (sigmaS - sigmaC) * rebars[i].Area * (rebars[i].Position.X - ConcreteSection.Centroid.X);

            }

            return new ForceTuple(deltaNArray, -deltaMxArray, deltaMyArray);
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlanes">The strain plane</param>
        protected virtual ForceTuple[] IntegrateRebarStress(StrainPlane[] strainPlanes, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            ForceTuple[] returnValue = new ForceTuple[strainPlanes.Length];

            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, strainPlanes.Length), (range) =>
            {
                for (int j = range.Item1; j < range.Item2; j++)
                    returnValue[j] = IntegrateRebarStress(strainPlanes[j], rebarIsInsideAssociation);
            });

            return returnValue;
        }

        /// <inheritdoc cref="IntegrateRebarStress(StrainPlane, Dictionary<int, bool>)"/>
        protected virtual async Task<ForceTuple> IntegrateRebarStressAsync(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            return await Task.Run(() =>
            {
                return IntegrateRebarStress(strainPlane, rebarIsInsideAssociation);
            });
        }

        /// <summary>
        /// Calculate the resultant of all the rebars
        /// </summary>
        /// <param name="strainPlane">The strain plane</param>
        /// <param name="deltaN">The axial force resultant</param>
        /// <param name="deltaMx">The bending moment about X-axis resultant</param>
        /// <param name="deltaMy">The bending moment about Y-axis resultant</param>
        protected virtual ForceTuple IntegrateRebarLinearStress(double psi, double? psiTendon, StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)
        {
			ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();

            double[] deltaNArray = new double[rebars.Length];
            double[] deltaMxArray = new double[rebars.Length];
            double[] deltaMyArray = new double[rebars.Length];

            Parallel.For(0, rebars.Length, (i) =>
            {
				ReinforcedConcreteRebar rebar = rebars[i];
                double strain = strainPlane.GetStrain(rebar.Position);
                double sigmaS;
                if (rebar.EpsilonP > 0)
                    sigmaS = CalculateElasticSigmaS(psiTendon.Value, rebar, strain);
                else
                    sigmaS = CalculateElasticSigmaS(psi, rebar, strain);

                double sigmaC = 0;
                if (rebarIsInsideAssociation[i])
                    sigmaC = CalculateElasticSigmaC(strain);

                deltaNArray[i] = (sigmaS - sigmaC) * rebar.Area;
                deltaMxArray[i] = (sigmaS - sigmaC) * rebar.Area * (rebar.Position.Y - ConcreteSection.Centroid.Y);
                deltaMyArray[i] = (sigmaS - sigmaC) * rebar.Area * (rebar.Position.X - ConcreteSection.Centroid.X);
            });

            return new ForceTuple(deltaNArray.Sum(), -deltaMxArray.Sum(), deltaMyArray.Sum());
        }

        public double CalculateElasticSigmaS(double psi, ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.E * (1 + psi) * strain + rebar.RebarMaterial.E * rebar.EpsilonP;
        }

        #endregion

        protected virtual (double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar, int dMinVertexIndex,
            double dminConcrete, int dMaxVertexIndex, double dmaxConcrete)
            CalculateMaxMinSectionDistances(double teta)
        {
            double cosTeta = Math.Cos(teta);
            double sinTeta = Math.Sin(teta);

            double dminRebar = double.MaxValue;
            double dmaxRebar = double.MinValue;
            double dmaxConcrete = double.MinValue;
            double dminConcrete = double.MaxValue;

            int dMinRebarId = -1;
            int dMaxRebarId = -1;
            int dMaxVertexIndex = -1;
            int dMinVertexIndex = -1;

            var rebars = ConcreteSection.GetRebars();

            for (int r = 0; r < rebars.Length; r++)
            {
                double w1 = (rebars[r].Position.Y - ConcreteSection.Centroid.Y) * cosTeta - (rebars[r].Position.X - ConcreteSection.Centroid.X) * sinTeta;
                if (w1 <= dminRebar)
                {
                    dminRebar = w1;
                    dMinRebarId = rebars[r].Id;
                }

                if (w1 >= dmaxRebar)
                {
                    dmaxRebar = w1;
                    dMaxRebarId = rebars[r].Id;
                }
            }

            //TODO: implementare con armature lineari

            for (int c = 0; c < ConcreteSection.Shape.Fill.Count; c++)
            {
                double w1 = (ConcreteSection.Shape.Fill[c].Y - ConcreteSection.Centroid.Y) * cosTeta -
                    (ConcreteSection.Shape.Fill[c].X - ConcreteSection.Centroid.X) * sinTeta;

                if (w1 >= dmaxConcrete)
                {
                    dMaxVertexIndex = c;
                    dmaxConcrete = w1;
                }

                if (w1 <= dminConcrete)
                {
                    dminConcrete = w1;
                    dMinVertexIndex = c;
                }
            }

            return (teta, dMinRebarId, dminRebar, dMaxRebarId, dmaxRebar, dMinVertexIndex, dminConcrete, dMaxVertexIndex, dmaxConcrete);
        }

        protected virtual ForceTuple ConvertToAdimensionalForces(ForceTuple forceTuple)
        {
            BoundingBox2d bBox = _concreteSection.Shape.Get2dBoundingBox(); ;
            double h = bBox.Size.Y;
            double b = bBox.Size.X;

            return new ForceTuple(forceTuple.N / (b * h * Math.Abs(GetFck())),
                forceTuple.Mx / (b * h * h * Math.Abs(GetFck())),
                forceTuple.My / (b * b * h * Math.Abs(GetFck())));
        }

        protected Dictionary<int, bool> GetRebarIsInsideAssociation()
        {
            Dictionary<int, bool> kvp = new Dictionary<int, bool>();

            ReinforcedConcreteRebar[] rebars = ConcreteSection.GetRebars();

            for (int i = 0; i < rebars.Length; i++)
            {
                if (ConcreteSection.Shape.IsPointInside(rebars[i].Position))
                    kvp.Add(i, true);
                else
                    kvp.Add(i, false);
            }

            return kvp;
        }

        #endregion

        #region Failure domain limit points

        protected virtual (double epsilon, Point2d point, double distanceFromBaricentre) GetP1((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainTypes analysisType, FailureZones failureZone)
        {
            switch (_concreteSection.ConcreteMaterial.ConcreteType)
            {
                case ConcreteMaterial.ConcreteTypes.Normal:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            return (GetDesignYieldingStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        case FailureDomainTypes.Plastic:
                            return (GetDesignUltimateStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        default:
                            return (0.0, null, 0.0);
                    }

                case ConcreteMaterial.ConcreteTypes.FRC:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            return (GetYieldingStrainConcreteTension(), ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        case FailureDomainTypes.Plastic:
                            {
                                double strain = Math.Min(0.02, GetUltimateStrainConcreteTension());

                                if (failureZone == FailureZones.F1)
                                    strain = Math.Min(0.01, strain);

                                return (strain, ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                    (distances.dmaxConcrete - distances.dminConcrete));
                            }
                        default:
                            return (0.0, null, 0.0);
                    }

                default:
                    return (0.0, null, 0.0);
            }
        }

        protected (double epsilon, Point2d point, double distanceFromBaricentre) GetP2((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainTypes analysisType)
        {
            switch (_concreteSection.ConcreteMaterial.ConcreteType)
            {
                case ConcreteMaterial.ConcreteTypes.Normal:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            return (GetYieldingStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                                (distances.dmaxConcrete - distances.dminRebar));

                        case FailureDomainTypes.Plastic:
                            return (GetUltimateStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                                (distances.dmaxConcrete - distances.dminRebar));

                        default:
                            return (0.0, null, 0.0);
                    }

                case ConcreteMaterial.ConcreteTypes.FRC:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:
                            return (GetYieldingStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        case FailureDomainTypes.Plastic:
                            return (GetUltimateStrainConcreteCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        default:
                            return (0.0, null, 0.0);
                    }

                default:
                    return (0.0, null, 0.0);
            }
        }


        protected (double epsilon, Point2d point, double distanceFromBaricentre) GetP3((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainTypes analysisType)
        {
            switch (analysisType)
            {
                case FailureDomainTypes.Elastic:
                    return (GetYieldingStrainPureCompression(), ConcreteSection.Shape.Fill[distances.dMaxVertexIndex],
                        (distances.dmaxConcrete - distances.dminConcrete));

                case FailureDomainTypes.Plastic:
                    double fraction = GetYieldingStrainPureCompression() / GetUltimateStrainConcreteCompression();
                    double heigth = distances.dmaxConcrete - distances.dminConcrete;

                    Point2d strainPlaneCenter = new Point2d((distances.dmaxConcrete - (1.0 - fraction) * heigth) * (-Math.Sin(distances.teta)) + ConcreteSection.Centroid.X,
                        (distances.dmaxConcrete - (1.0 - fraction) * heigth) * (Math.Cos(distances.teta)) + ConcreteSection.Centroid.Y);

                    return (GetYieldingStrainPureCompression(), strainPlaneCenter, (distances.dmaxConcrete - distances.dminConcrete));

                default:
                    return (0.0, null, 0.0);
            }
        }

        protected virtual (double epsilon, Point2d point, double distanceFromBaricentre) GetP4((double teta, int dMinRebarId, double dminRebar,
            int dMaxRebarId, double dmaxRebar, int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete) distances,
            FailureDomainTypes analysisType)
        {
            switch (_concreteSection.ConcreteMaterial.ConcreteType)
            {
                case ConcreteMaterial.ConcreteTypes.Normal:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:

                            return (0.0, ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        case FailureDomainTypes.Plastic:

                            return (GetDesignYieldingStrainRebar(distances.dMinRebarId), ConcreteSection.GetRebarById(distances.dMinRebarId).Position,
                                (distances.dmaxConcrete - distances.dminRebar));

                        default:
                            return (0.0, null, 0.0);
                    }

                case ConcreteMaterial.ConcreteTypes.FRC:

                    switch (analysisType)
                    {
                        case FailureDomainTypes.Elastic:

                            return (0.0, ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        case FailureDomainTypes.Plastic:

                            return (GetYieldingStrainConcreteTension(), ConcreteSection.Shape.Fill[distances.dMinVertexIndex],
                                (distances.dmaxConcrete - distances.dminConcrete));

                        default:
                            return (0.0, null, 0.0);
                    }

                default:
                    return (0.0, null, 0.0);
            }
        }

        #endregion

        #region Protected method - Failure domain

        /// <summary>
        /// Calculate the failure domain <see cref="FailureDomain"/> of the section
        /// </summary>
        /// <param name="momentsDiscretizations">Number of discretizations of X-axis and Y-axis (moment around Z-axis)</param>
        /// <param name="normalDiscretizations">Number of discretizations of Z-axis (axial force)</param>
        protected virtual FailureDomain CalculateFailureDomain((StrainPlane, FailureZones)[][] strainPlanes, CoordinateSystem forceCoordinateSystem,
            FailureDomainTypes failureDomainType)
        {
            FailureDomain.FailureDomainPoint[][] domainPoints = new FailureDomain.FailureDomainPoint[strainPlanes.Length][];
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();

            try
            {
                StrainPlane[] strainPlaneArray = new StrainPlane[strainPlanes.Sum(i => i.Length)];
                ForceTuple[] results = new ForceTuple[strainPlaneArray.Length];

                for (int i = 0; i < strainPlanes.Length; i++)
                {
                    for (int j = 0; j < strainPlanes[i].Length; j++)
                    {
                        strainPlaneArray[j + i * strainPlanes[i].Length] = strainPlanes[i][j].Item1;
                    }
                }

                results = CalculateForceResultant(strainPlaneArray, rebarIsInsideAssociation);

                Parallel.For(0, strainPlanes.Length, (i) =>
                {
                    domainPoints[i] = new FailureDomain.FailureDomainPoint[strainPlanes[i].Length];
                    for (int j = 0; j < strainPlanes[i].Length; j++)
                    {
                        domainPoints[i][j] =
                            new FailureDomain.FailureDomainPoint(GetExternalForces(results[j + i * strainPlanes[i].Length], forceCoordinateSystem), strainPlanes[i][j].Item2, strainPlanes[i][j].Item1);
                    }
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }

            return new FailureDomain(domainPoints, failureDomainType);
        }

        protected virtual (StrainPlane, FailureZones)[][] CalculateDesignFailureStrainPlanes(int tetaDiscretizations, (FailureZones, int)[] zoneSubdivision,
            FailureDomainTypes failureDomainType, double initialAngle = 0)
        {
            if (tetaDiscretizations < 2)
                return null;

            double deltaTeta = 2 * Math.PI / (tetaDiscretizations);
            (StrainPlane, FailureZones)[][] strainPlanes = new (StrainPlane, FailureZones)[tetaDiscretizations][];

            try
            {
                Parallel.For(0, tetaDiscretizations, (i) =>
                {
                    strainPlanes[i] = CalculateFailureStrainPlanes((initialAngle + i * deltaTeta), zoneSubdivision, failureDomainType);
                });
            }
            catch (Exception e)
            {
                _log.Add(e.Message);
                if (e.InnerException != null)
                    _log.Add(e.InnerException.Message);
                return null;
            }

            return strainPlanes;
        }

        /// <summary>
        /// Calculate the strain planes for angle <paramref name="teta"/>
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="zoneSubdivision">Number of subdivision for each failure zone</param>
        /// <returns></returns>
        protected virtual (StrainPlane, FailureZones)[] CalculateFailureStrainPlanes(double teta, (FailureZones, int)[] zoneSubdivision,
            FailureDomainTypes failureDomainType)
        {
            (StrainPlane, FailureZones)[] strainPlanes = new (StrainPlane, FailureZones)[zoneSubdivision.Select(i => i.Item2).Sum() + zoneSubdivision.Length + 1];

            var sectionDistances = CalculateMaxMinSectionDistances(teta);

            var p2 = GetP2(sectionDistances, failureDomainType);
            var p3 = GetP3(sectionDistances, failureDomainType);
            var p4 = GetP4(sectionDistances, failureDomainType);

            int subIndex = 0;

            for (int i = 0; i < zoneSubdivision.Length; i++)
            {
                FailureZones failureZones = zoneSubdivision[i].Item1;
                int subdivision = zoneSubdivision[i].Item2 + 1;

                var p1 = GetP1(sectionDistances, failureDomainType, failureZones);

                switch (failureZones)
                {
                    case FailureZones.F1:
                    case FailureZones.F2A:
                    case FailureZones.F2B:
                    case FailureZones.F3A:
                    case FailureZones.F3B:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision,
                                    p1, p2, p3, p4, subIndex), failureZones);
                                subIndex++;
                            }

                            break;
                        }

                    case FailureZones.F4:
                        {
                            for (int j = 0; j < subdivision; j++)
                            {
                                strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, (double)j / (double)subdivision,
                                    p1, p2, p3, p4, subIndex), failureZones);
                                subIndex++;
                            }

                            strainPlanes[subIndex] = (CalculateStrainPlane(teta, failureZones, 1.0, p1, p2, p3, p4, subIndex), failureZones);

                            break;
                        }

                    default:
                        return null;
                }
            }

            return strainPlanes;
        }

        /// <summary>
        /// Calculate the strain plane for angle <paramref name="teta"/> with input <see cref="FailureZones"/> 
        /// </summary>
        /// <param name="teta">The angle of rotation of the axis</param>
        /// <param name="failureIndex">The index of the failure zone</param>
        /// <param name="immersioneNelCampo"></param>
        /// <param name="distances"></param>
        /// <param name="id"></param>
        /// <returns>Strain Plane</returns>
        /// <remarks>Param distances can be calculated with CalculateMaxMinSectionDistances method</remarks>
        /// <exception cref="ArgumentException"></exception>
        protected virtual StrainPlane CalculateStrainPlane(double teta, FailureZones failureIndex, double immersioneNelCampo,
            (double epsilon, Point2d point, double distanceFromBaricentre) p1, (double epsilon, Point2d point, double distanceFromBaricentre) p2,
            (double epsilon, Point2d point, double distanceFromBaricentre) p3, (double epsilon, Point2d point, double distanceFromBaricentre) p4, int id = -1)
        {
            if (immersioneNelCampo > 1.0 || immersioneNelCampo < 0.0)
                throw new ArgumentException("ImmersioneNelCampo cannot be greater than 1 and less than 0");

            double chiSx;
            double chiDx;
            double chi;

            switch (failureIndex)
            {
                case FailureZones.F1:

                    chiSx = 0;
                    chiDx = p1.epsilon / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F2A:

                    if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC &&
                        !_concreteSection.ConcreteMaterial.StressStrainTableTension.IsHardening() &&
                        _concreteSection.RebarsCount == 0)
                        chiSx = (p1.epsilon + 0.3 * Math.Abs(p3.epsilon)) / p1.distanceFromBaricentre;
                    else
                        chiSx = p1.epsilon / p1.distanceFromBaricentre;

                    chiDx = (p1.epsilon + Math.Abs(p3.epsilon)) / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F2B:

                    chiSx = (p1.epsilon + Math.Abs(p3.epsilon)) / p1.distanceFromBaricentre;
                    chiDx = (p1.epsilon + Math.Abs(p2.epsilon)) / p1.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p1.point, teta, chi, p1.epsilon, id);


                case FailureZones.F3A:

                    chiSx = (p1.epsilon + Math.Abs(p2.epsilon)) / p1.distanceFromBaricentre;
                    chiDx = (p4.epsilon + Math.Abs(p2.epsilon)) / p4.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F3B:

                    chiSx = (p4.epsilon + Math.Abs(p2.epsilon)) / p4.distanceFromBaricentre;
                    chiDx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p2.point, teta, chi, p2.epsilon, id);


                case FailureZones.F4:

                    chiSx = Math.Abs(p2.epsilon) / p3.distanceFromBaricentre;
                    chiDx = 0.0;

                    chi = chiSx + immersioneNelCampo * (chiDx - chiSx);
                    return new StrainPlane(p3.point, teta, chi, p3.epsilon, id);


                default:
                    return null;
            }
        }

        protected virtual FailureDomain2d ConvertFailureDomain3dTo2d(FailureDomain failureDomain)
        {
            if (failureDomain == null)
                throw new ArgumentNullException();
            if (failureDomain.DomainPoints.Length != 2)
                throw new ArgumentException();

            FailureDomain.FailureDomainPoint[] domainPointPositive = failureDomain.DomainPoints[0];
            FailureDomain.FailureDomainPoint[] domainPointNevative = failureDomain.DomainPoints[1];
            Array.Reverse(domainPointNevative);

            List<FailureDomain.FailureDomainPoint> domainPoints = domainPointPositive.ToList();
            domainPoints.AddRange(domainPointNevative);

            return new FailureDomain2d(domainPoints.ToArray(), FailureDomainResult2d.DomainTypes.ConstantMxMy);
        }

        #endregion

        #region Protected method - Point on failure domain

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(Vector3d vector, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(new ForceTuple(vector.Z, vector.X, vector.Y), coordinateSystem, failureDomainType, failureAnalysisType, _failureAnalysisAngularTolerance);
        }

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType)
        {
            return CalculateDomainPoint(targetLocalForces, coordinateSystem, failureDomainType, failureAnalysisType, _failureAnalysisAngularTolerance);
        }

        protected virtual FailureDomain.FailureDomainPoint CalculateDomainPoint(ForceTuple targetLocalForces, CoordinateSystem coordinateSystem,
            FailureDomainTypes failureDomainType, FailureAnalysisTypes failureAnalysisType,
            double angularTolerance = 1e-3, double distanceTolerance = 1e-4)
        {
            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    break;
                case FailureAnalysisTypes.ConstantN:
                    if (targetLocalForces.Mx == 0 && targetLocalForces.My == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    if (targetLocalForces.My == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    if (targetLocalForces.Mx == 0)
                        return null;
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    break;
                default:
                    break;
            }

            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();

            ForceTuple adimOutputForces = ConvertToAdimensionalForces(targetLocalForces);

            Vector3d vectorEd = null;
            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    vectorEd = new Vector3d(targetLocalForces.Mx / 1000000, targetLocalForces.My / 1000000, targetLocalForces.N / 1000);
                    break;
                case FailureAnalysisTypes.ConstantN:
                    vectorEd = new Vector3d(targetLocalForces.Mx / 1000000, targetLocalForces.My / 1000000, 0.0);
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    vectorEd = new Vector3d(targetLocalForces.Mx / 1000000, 0, targetLocalForces.N / 1000);
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    vectorEd = new Vector3d(0, targetLocalForces.My / 1000000, targetLocalForces.N / 1000);
                    break;
            }

            // Valori di primo tentativo
            FailureZones failureIndex = FailureZones.F3A;
            double eta = 0.5;
            double teta = Math.Atan2(targetLocalForces.My, targetLocalForces.Mx);

            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.65;
                    }
                    else if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.85;
                    }
                    else if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.95;
                    }
                    else if (Math.Abs(adimOutputForces.N) < 1e-5)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.90;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-2 && Math.Abs(adimOutputForces.My) < 1e-2)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.5;
                    }
                    else if (adimOutputForces.N < 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-7 && Math.Abs(adimOutputForces.My) < 1e-7)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.9;
                    }
                    else
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.2;
                    }
                    break;
                case FailureAnalysisTypes.ConstantN:
                case FailureAnalysisTypes.ConstantNMx:
                case FailureAnalysisTypes.ConstantNMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.85;
                    }
                    else if (adimOutputForces.N < 0.2)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.9;
                    }
                    else if (adimOutputForces.N < 0.4)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.25;
                    }
                    else if (adimOutputForces.N < 0.6)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.5;
                    }
                    else if (adimOutputForces.N < 1)
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.75;
                    }
                    else
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.95;
                    }
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    if (adimOutputForces.N > 0.0)
                    {
                        failureIndex = FailureZones.F3A;
                        eta = 0.1;
                    }
                    else
                    {
                        failureIndex = FailureZones.F4;
                        eta = 0.5;
                    }
                    break;
            }
            if (adimOutputForces.N > 0.0 && Math.Abs(adimOutputForces.Mx) < 1e-10 && Math.Abs(adimOutputForces.My) < 1e-10)
            {
                if (ConcreteSection.Centroid.Y - ConcreteSection.GetHomogenizedCentroid(out _, out _).Y > 0)
                    teta = Math.PI;
            }

            int id = 1;
            var distances = CalculateMaxMinSectionDistances(teta);

            var p1 = GetP1(distances, failureDomainType, failureIndex);
            var p2 = GetP2(distances, failureDomainType);
            var p3 = GetP3(distances, failureDomainType);
            var p4 = GetP4(distances, failureDomainType);

            StrainPlane strainPlane = CalculateStrainPlane(teta, failureIndex, eta, p1, p2, p3, p4, id);

            teta = strainPlane.Teta;

            ForceTuple forces = GetExternalForces(CalculateForceResultant(strainPlane, rebarIsInsideAssociation), coordinateSystem);
            ForceTuple adimIncrement = ConvertToAdimensionalForces(forces - targetLocalForces);

            (double deltaTeta, double deltaEta, Vector3d distanceToTarget) increment;

            double angle = -1;
            bool exit = false;

            switch (failureAnalysisType)
            {
                case FailureAnalysisTypes.ConstantEccentricity:
                    angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, forces.N / 1000).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000,
                        targetLocalForces.My / 1000000, targetLocalForces.N / 1000));
                    if (Math.Abs(angle) < angularTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantN:
                    angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, 0).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000,
                        targetLocalForces.My / 1000000, 0));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(angle) < angularTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantMxMy:
                    angle = new Vector3d(targetLocalForces.Mx / 1000000, targetLocalForces.My / 1000000, forces.N / 1000).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000,
                        targetLocalForces.My / 1000000, targetLocalForces.N / 1000));
                    if (Math.Abs(adimIncrement.Mx) < distanceTolerance && Math.Abs(adimIncrement.My) < distanceTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantNMx:
                    angle = new Vector3d(targetLocalForces.Mx / 1000000, forces.My / 1000000, targetLocalForces.N / 1000).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000,
                        targetLocalForces.My / 1000000, targetLocalForces.N / 1000));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(adimIncrement.Mx) < distanceTolerance)
                        exit = true;
                    break;
                case FailureAnalysisTypes.ConstantNMy:
                    angle = new Vector3d(forces.Mx / 1000000, targetLocalForces.My / 1000000, targetLocalForces.N / 1000).AngleTo(new Vector3d(targetLocalForces.Mx / 1000000,
                        targetLocalForces.My / 1000000, targetLocalForces.N / 1000));
                    if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(adimIncrement.My) < distanceTolerance)
                        exit = true;
                    break;
            }

            if (!exit)
            {
                Line3d externalForcesLine = null;
                switch (failureAnalysisType)
                {
                    case FailureAnalysisTypes.ConstantEccentricity:
                        externalForcesLine = new Line3d(new Point3d(0, 0, 0), targetLocalForces);
                        break;
                    case FailureAnalysisTypes.ConstantN:
                        externalForcesLine = new Line3d(new Point3d(0, 0, targetLocalForces.N), targetLocalForces);
                        break;
                    case FailureAnalysisTypes.ConstantMxMy:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, targetLocalForces.My, 0),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N - 1000));
                        break;
                    case FailureAnalysisTypes.ConstantNMx:
                        externalForcesLine = new Line3d(new Point3d(targetLocalForces.Mx, 0, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                    case FailureAnalysisTypes.ConstantNMy:
                        externalForcesLine = new Line3d(new Point3d(0, targetLocalForces.My, targetLocalForces.N),
                            new Point3d(targetLocalForces.Mx, targetLocalForces.My, targetLocalForces.N));
                        break;
                }

                do
                {
                    if (id < 100)
                    {

                        try
                        {
                            increment = CalculateIncrement(forces, strainPlane, failureIndex, eta, externalForcesLine, angle,
                                failureDomainType, rebarIsInsideAssociation);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            return null;
                        }

                        SetIncrement(failureDomainType, ref failureIndex, ref teta, ref eta, increment.deltaTeta, increment.deltaEta);

                        distances = CalculateMaxMinSectionDistances(teta);

                        p1 = GetP1(distances, failureDomainType, failureIndex);
                        p2 = GetP2(distances, failureDomainType);
                        p3 = GetP3(distances, failureDomainType);
                        p4 = GetP4(distances, failureDomainType);

                        id++;
                        strainPlane = CalculateStrainPlane(teta, failureIndex, eta, p1, p2, p3, p4, id);
                        forces = GetExternalForces(CalculateForceResultant(strainPlane, rebarIsInsideAssociation), coordinateSystem);

                        ForceTuple incrementForce = new ForceTuple(increment.distanceToTarget.Z, increment.distanceToTarget.X, increment.distanceToTarget.Y);
                        adimIncrement = ConvertToAdimensionalForces(incrementForce);

                        switch (failureAnalysisType)
                        {
                            case FailureAnalysisTypes.ConstantEccentricity:
                                angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, forces.N / 1000).AngleTo(vectorEd);
                                break;
                            case FailureAnalysisTypes.ConstantN:
                                angle = new Vector3d(forces.Mx / 1000000, forces.My / 1000000, 0).AngleTo(vectorEd);
                                break;
                            case FailureAnalysisTypes.ConstantNMx:
                                angle = new Vector3d(forces.Mx / 1000000, 0, forces.N / 1000).AngleTo(vectorEd);
                                break;
                            case FailureAnalysisTypes.ConstantNMy:
                                angle = new Vector3d(0, forces.My / 1000000, forces.N / 1000).AngleTo(vectorEd);
                                break;
                        }

                        if ((Math.Abs(adimIncrement.N) < distanceTolerance &&
                            Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                            Math.Abs(adimIncrement.My) < distanceTolerance))
                            break;
                    }
                    else
                    {
                        _log.Add("Fail to calculate point on domain");
                        return null;
                    }

                    switch (failureAnalysisType)
                    {
                        case FailureAnalysisTypes.ConstantEccentricity:
                            if (Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantN:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance && Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantMxMy:
                            if (Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                                Math.Abs(adimIncrement.My) < distanceTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantNMx:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance &&
                                Math.Abs(adimIncrement.Mx) < distanceTolerance &&
                                Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                        case FailureAnalysisTypes.ConstantNMy:
                            if (Math.Abs(adimIncrement.N) < distanceTolerance &&
                                Math.Abs(adimIncrement.My) < distanceTolerance &&
                                Math.Abs(angle) < angularTolerance)
                                exit = true;
                            break;
                    }

                } while (!exit);
            }

            return new FailureDomain.FailureDomainPoint(forces, failureIndex, strainPlane);
        }

        protected (double deltaTeta, double deltaEta, Vector3d distanceToTarget) CalculateIncrement(ForceTuple iterationPoint,
            StrainPlane inputStrainPlane, FailureZones inputFailureZone, double inputImmersioneNelCampo, Line3d externalForcesLine,
            double deltaAngle, FailureDomainTypes failureDomainType, Dictionary<int, bool> rebarIsInsideAssociation)
        {
            var adimIteractionPoint = ConvertToAdimensionalForces(iterationPoint);

            double dTeta;
            double dEta;

            switch (inputFailureZone)
            {
                case FailureZones.F1:
                    dTeta = 0.25;
                    dEta = 0.25;
                    break;

                case FailureZones.F2A:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.1), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.1), 0.001);
                    break;

                case FailureZones.F2B:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.005);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0005);
                    break;

                case FailureZones.F3A:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.1), 0.0001);
                    dEta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.00001);
                    break;

                case FailureZones.F3B:
                    dTeta = Math.Max(0.1 * Math.Min(deltaAngle, 0.1), 0.00001);
                    dEta = Math.Max(0.1 * Math.Min(deltaAngle, 0.01), 0.000001);
                    break;

                default:
                    dTeta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    dEta = Math.Max(Math.Min(deltaAngle, 0.01), 0.0001);
                    break;
            }

            double dNdTeta;
            double dMxdTeta;
            double dMydTeta;
            double dNdImm;
            double dMxdImm;
            double dMydImm;

            double nonLinearErrorTeta;
            double nonLinearErrorEta;
            double nonLinearError = 0.1;

            double dTetaBuffer = dTeta;
            double dEtaBuffer = dEta;

            int etaCounter = 1;
            int tetaCounter = 1;

            // derivate parziali rispetto a teta
            do
            {
                if (tetaCounter < 10)
                {
                    var distancesPlusTeta = CalculateMaxMinSectionDistances(inputStrainPlane.Teta + dTetaBuffer);
                    var distancesMinusTeta = CalculateMaxMinSectionDistances(inputStrainPlane.Teta - dTetaBuffer);

                    var p1PlusTeta = GetP1(distancesPlusTeta, failureDomainType, inputFailureZone);
                    var p2PlusTeta = GetP2(distancesPlusTeta, failureDomainType);
                    var p3PlusTeta = GetP3(distancesPlusTeta, failureDomainType);
                    var p4PlusTeta = GetP4(distancesPlusTeta, failureDomainType);

                    var p1MinusTeta = GetP1(distancesMinusTeta, failureDomainType, inputFailureZone);
                    var p2MinusTeta = GetP2(distancesMinusTeta, failureDomainType);
                    var p3MinusTeta = GetP3(distancesMinusTeta, failureDomainType);
                    var p4MinusTeta = GetP4(distancesMinusTeta, failureDomainType);

                    StrainPlane strainPlanePlusdTeta = CalculateStrainPlane(distancesPlusTeta.teta, inputFailureZone,
                        inputImmersioneNelCampo, p1PlusTeta, p2PlusTeta, p3PlusTeta, p4PlusTeta);
                    StrainPlane strainPlaneMinusdTeta = CalculateStrainPlane(distancesMinusTeta.teta, inputFailureZone,
                        inputImmersioneNelCampo, p1MinusTeta, p2MinusTeta, p3MinusTeta, p4MinusTeta);

                    var forcesPlusTeta = CalculateForceResultant(strainPlanePlusdTeta, rebarIsInsideAssociation);
                    var forcesMinusTeta = CalculateForceResultant(strainPlaneMinusdTeta, rebarIsInsideAssociation);

                    dNdTeta = (forcesPlusTeta.N - forcesMinusTeta.N) / (2.0 * dTetaBuffer);
                    dMxdTeta = (forcesPlusTeta.Mx - forcesMinusTeta.Mx) / (2.0 * dTetaBuffer);
                    dMydTeta = (forcesPlusTeta.My - forcesMinusTeta.My) / (2.0 * dTetaBuffer);

                    dTetaBuffer += dTeta;

                    var adimForcePlusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusTeta.N, forcesPlusTeta.Mx, forcesPlusTeta.My));
                    var adimForceMinusTeta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusTeta.N, forcesMinusTeta.Mx, forcesMinusTeta.My));

                    nonLinearErrorTeta = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusTeta.N + adimForceMinusTeta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusTeta.Mx + adimForceMinusTeta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusTeta.My + adimForceMinusTeta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorTeta) < 0.0001)
                        nonLinearErrorTeta = 0.0001;

                    nonLinearErrorTeta = Math.Sqrt(nonLinearError * Math.Max(Math.Abs(adimIteractionPoint.N),
                        Math.Max(Math.Abs(adimIteractionPoint.Mx), Math.Abs(adimIteractionPoint.My)) /
                        Math.Sqrt(nonLinearErrorTeta)));

                    tetaCounter++;
                }
                else
                {
                    if (inputFailureZone == FailureZones.F1)
                        return (+0.5, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.1, +0.0, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while (dNdTeta == 0.0 || (dMxdTeta == 0.0 && dMydTeta == 0.0));


            // derivate parziali rispetto a immersione nel campo
            do
            {
                if (etaCounter < 10)
                {
                    var distances = CalculateMaxMinSectionDistances(inputStrainPlane.Teta);

                    var p1Eta = GetP1(distances, failureDomainType, inputFailureZone);
                    var p2Eta = GetP2(distances, failureDomainType);
                    var p3Eta = GetP3(distances, failureDomainType);
                    var p4Eta = GetP4(distances, failureDomainType);

                    StrainPlane strainPlanePlusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Min(inputImmersioneNelCampo + dEtaBuffer, 1.0), p1Eta, p2Eta, p3Eta, p4Eta);
                    StrainPlane strainPlaneMinusdImm = CalculateStrainPlane(inputStrainPlane.Teta, inputFailureZone,
                        Math.Max(inputImmersioneNelCampo - dEtaBuffer, 0.0), p1Eta, p2Eta, p3Eta, p4Eta);

                    var forcesPlusEta = CalculateForceResultant(strainPlanePlusdImm, rebarIsInsideAssociation);
                    var forcesMinusEta = CalculateForceResultant(strainPlaneMinusdImm, rebarIsInsideAssociation);

                    dNdImm = (forcesPlusEta.N - forcesMinusEta.N) / (2.0 * dEtaBuffer);
                    dMxdImm = (forcesPlusEta.Mx - forcesMinusEta.Mx) / (2.0 * dEtaBuffer);
                    dMydImm = (forcesPlusEta.My - forcesMinusEta.My) / (2.0 * dEtaBuffer);

                    dEtaBuffer += dEta;

                    var adimForcePlusEta = ConvertToAdimensionalForces(new ForceTuple(forcesPlusEta.N, forcesPlusEta.Mx, forcesPlusEta.My));
                    var adimForceMinusEta = ConvertToAdimensionalForces(new ForceTuple(forcesMinusEta.N, forcesMinusEta.Mx, forcesMinusEta.My));

                    nonLinearErrorEta = Math.Max(Math.Max(
                        Math.Abs((adimForcePlusEta.N + adimForceMinusEta.N) / 2.0 - adimIteractionPoint.N),
                        Math.Abs((adimForcePlusEta.Mx + adimForceMinusEta.Mx) / 2.0 - adimIteractionPoint.Mx)),
                        Math.Abs((adimForcePlusEta.My + adimForceMinusEta.My) / 2.0 - adimIteractionPoint.My));

                    if (Math.Abs(nonLinearErrorEta) < 0.0001)
                        nonLinearErrorEta = 0.0001;

                    nonLinearErrorEta = Math.Sqrt(nonLinearError * Math.Max(Math.Abs(adimIteractionPoint.N), Math.Max(Math.Abs(adimIteractionPoint.Mx),
                        Math.Abs(adimIteractionPoint.My)) / Math.Sqrt(nonLinearErrorEta)));

                    etaCounter++;
                }
                else
                {
                    if (inputFailureZone == FailureZones.F1)
                        return (+0.0, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else if (inputFailureZone == FailureZones.F2A)
                        return (+0.0, -0.25, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                    else
                        return (+0.0, +0.01, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                }

            } while (dNdImm == 0.0 || (dMxdImm == 0.0 && dMydImm == 0.0));

            Vector3d v1 = new Vector3d(dMxdTeta, dMydTeta, dNdTeta);
            v1.Unitize();
            Vector3d v2 = new Vector3d(dMxdImm, dMydImm, dNdImm);
            v2.Unitize();

            // vettore uscente dal punto M di test
            Vector3d gradient = v1 ^ v2;
            gradient.Unitize();

            // k dell'equazione del piano tangente alla superficie in M    //      A*x + B*y + C*z + k = 0
            double k = -(gradient.X * iterationPoint.Mx + gradient.Y * iterationPoint.My + gradient.Z * iterationPoint.N);

            // piano tangente 
            Plane planeTg = new Plane(gradient.X, gradient.Y, gradient.Z, k);

            // punto di intersezione tra raggio delle forze sollecitanti e il piano tangente
            bool intersect = planeTg.IntersectWithRay(externalForcesLine, out Point3d intersectionPoint);

            if (!intersect)
            {
                if (inputFailureZone == FailureZones.F1)
                    return (+0.5, +0.5, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
                else
                    return (+0.1, +0.1, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue));
            }
            else
            {
                Vector3d displacementVector = new Vector3d(iterationPoint, intersectionPoint);

                Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(2, 2);

                partialDerivatives[0, 0] = dMxdTeta;
                partialDerivatives[1, 0] = dNdTeta;

                partialDerivatives[0, 1] = dMxdImm;
                partialDerivatives[1, 1] = dNdImm;

                Matrix<double> inputVector = Matrix<double>.Build.Dense(2, 1);
                inputVector[0, 0] = displacementVector.X;
                inputVector[1, 0] = displacementVector.Z;

                Matrix<double> results = partialDerivatives.Inverse() * inputVector;


                double dT;
                double dE;

                if (dTeta > 0.010)
                    dT = 0.05;
                else if (dTeta == 0.01)
                    dT = 0.1;
                else if (dTeta > 0.001)
                    dT = 0.15;
                else
                    dT = 0.25;

                if (_concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal)
                {
                    if (inputFailureZone == FailureZones.F3A)
                    {
                        if (dEta >= 0.01)
                            dE = 0.35;
                        else if (dEta >= 0.001)
                            dE = 0.5;
                        else if (dEta >= 0.0005)
                            dE = 0.5;
                        else
                            dE = 0.5;
                    }
                    else if (inputFailureZone == FailureZones.F3B)
                    {
                        if (dEta >= 0.01)
                            dE = 0.25;
                        else if (dEta >= 0.001)
                            dE = 0.35;
                        else
                            dE = 0.5;
                    }
                    else if (inputFailureZone == FailureZones.F2B || inputFailureZone == FailureZones.F2A)
                    {
                        if (dEta >= 0.01)
                            dE = 0.3;
                        else if (dEta >= 0.001)
                            dE = 0.4;
                        else
                            dE = 0.75;
                    }
                    else
                    {
                        dE = 0.5;
                    }
                }
                else
                {
                    if (inputFailureZone == FailureZones.F3A || inputFailureZone == FailureZones.F2A ||
                        inputFailureZone == FailureZones.F2B)
                    {
                        if (dEta >= 0.01)
                            dE = 0.15;
                        else if (dEta >= 0.001)
                            dE = 0.25;
                        else if (dEta >= 0.0005)
                            dE = 0.35;
                        else
                            dE = 0.5;
                    }
                    else if (inputFailureZone == FailureZones.F3B)
                        dE = 0.1;

                    else
                    {
                        if (dEta >= 0.01)
                            dE = 0.25;
                        else
                            dE = 0.5;
                    }
                }

                double deltaTeta = results[0, 0] * dT / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorTeta), 1.0));
                double deltaEta = results[1, 0] * dE / Math.Sqrt(Math.Max(Math.Abs(nonLinearErrorEta), 1.0));

                return (deltaTeta, deltaEta, displacementVector);
            }
        }

        protected void SetIncrement(FailureDomainTypes analysisType, ref FailureZones failureZone, ref double teta, ref double eta, double deltaTeta, double deltaEta)
        {
            deltaEta = deltaEta > 0.42 ? 0.42 : deltaEta;
            deltaEta = deltaEta < -0.32 ? -0.32 : deltaEta;

            deltaTeta = deltaTeta > Math.PI / 7.0 ? Math.PI / 7.0 : deltaTeta;
            deltaTeta = deltaTeta < -Math.PI / 7.0 ? -Math.PI / 7.0 : deltaTeta;

            // piano di nuovo tentativo
            teta += deltaTeta;

            eta += (deltaEta - (int)deltaEta);
            failureZone += (int)deltaEta;

            switch (analysisType)
            {
                case FailureDomainTypes.Plastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    break;
                case FailureDomainTypes.Plastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3A)
                            eta = 0.98;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;
                    }
                    break;
                case FailureDomainTypes.Elastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.Normal:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3B || failureZone == FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == FailureZones.F2B)
                            failureZone++;
                    }
                    break;
                case FailureDomainTypes.Elastic when _concreteSection.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC:
                    if (eta < 0.0)
                    {
                        eta++;
                        failureZone--;

                        if (failureZone == FailureZones.F3B || failureZone == FailureZones.F2B)
                            failureZone--;
                    }
                    if (eta > 1.0)
                    {
                        eta--;
                        failureZone++;

                        if (failureZone == FailureZones.F3B)
                            failureZone++;

                        if (failureZone == FailureZones.F2B)
                            failureZone++;
                    }
                    break;
                default:
                    throw new Exception();
            }

            failureZone = (int)failureZone < 1 ? FailureZones.F1 : failureZone;
            failureZone = (int)failureZone > 6 ? FailureZones.F4 : failureZone;
        }

        #endregion

        #region Protected method - Stress SLS

        protected StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double tolerance = 1e-5)
        {
           return CalculateStrainPlaneStressAnalysis(localForces, coordinateSystem, null, null, tolerance);
        }

        protected (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) CalculateIncrementStressAnalysis(StrainPlane inputStrainPlane, ForceTuple forceTuple,
            Dictionary<int, bool> rebarIsInsideAssociation, double? psiRebars, double? psiTendon)
        {
            ForceTuple forceTupleAdim = ConvertToAdimensionalForces(forceTuple);
            var bBox = ConcreteSection.Shape.GetBoundingBox();

            double deltaChiXLimit = 1.0 / bBox.Size.X;
            double dCX = 0.00001;
            if (forceTupleAdim.Mx != 0)
            {
                dCX = 0.01 * Math.Max(Math.Abs(forceTupleAdim.Mx), 0.00001);
                deltaChiXLimit *= Math.Max(Math.Abs(forceTupleAdim.Mx), 0.001);
            }
            else
                deltaChiXLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dChiX = dCX * deltaChiXLimit;

            double deltaChiYLimit = 1.0 / bBox.Size.Y;
            double dCY = 0.00001;
            if (forceTupleAdim.My != 0)
            {
                dCY = 0.01 * Math.Max(Math.Abs(forceTupleAdim.My), 0.00001);
                deltaChiYLimit *= Math.Max(Math.Abs(forceTupleAdim.My), 0.001);
            }
            else
                deltaChiYLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dChiY = dCY * deltaChiYLimit;

            double deltaStrainLimit = 1.0 / (bBox.Size.X * bBox.Size.Y);
            double dS = 0.00001;
            if (forceTupleAdim.N != 0)
            {
                dS = 0.001 * Math.Max(Math.Abs(forceTupleAdim.N), 0.00001);
                deltaStrainLimit *= Math.Max(Math.Abs(forceTupleAdim.N), 0.001);
            }
            else
                deltaStrainLimit *= Math.Abs(GetYieldingStrainPureCompression());

            double dStrain = dS * deltaStrainLimit;


            // derivate parziali rispetto a ChiX
            StrainPlane strainPlanePlusdChiX = new StrainPlane(inputStrainPlane.ChiX + dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiX = new StrainPlane(inputStrainPlane.ChiX - dChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            ForceTuple forcesPlusdChiX;
            ForceTuple forcesMinusdChiX;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusdChiX = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusdChiX, rebarIsInsideAssociation);
                forcesMinusdChiX = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusdChiX, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusdChiX = CalculateForceResultant(strainPlanePlusdChiX, rebarIsInsideAssociation);
                forcesMinusdChiX = CalculateForceResultant(strainPlaneMinusdChiX, rebarIsInsideAssociation);
            }

            double dNdChiX = (forcesPlusdChiX.N - forcesMinusdChiX.N) / (2.0 * dCX);
            double dMxdChiX = (forcesPlusdChiX.Mx - forcesMinusdChiX.Mx) / (2.0 * dCX);
            double dMydChiX = (forcesPlusdChiX.My - forcesMinusdChiX.My) / (2.0 * dCX);


            // derivate parziali rispetto a ChiY
            StrainPlane strainPlanePlusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY + dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);
            StrainPlane strainPlaneMinusdChiY = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY - dChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint);

            ForceTuple forcesPlusdChiY;
            ForceTuple forcesMinusdChiY;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusdChiY = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusdChiY, rebarIsInsideAssociation);
                forcesMinusdChiY = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusdChiY, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusdChiY = CalculateForceResultant(strainPlanePlusdChiY, rebarIsInsideAssociation);
                forcesMinusdChiY = CalculateForceResultant(strainPlaneMinusdChiY, rebarIsInsideAssociation);
            }

            double dNdChiY = (forcesPlusdChiY.N - forcesMinusdChiY.N) / (2.0 * dCY);
            double dMxdChiY = (forcesPlusdChiY.Mx - forcesMinusdChiY.Mx) / (2.0 * dCY);
            double dMydChiY = (forcesPlusdChiY.My - forcesMinusdChiY.My) / (2.0 * dCY);


            // derivate parziali rispetto a epsilon
            StrainPlane strainPlanePlusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint + dStrain);
            StrainPlane strainPlaneMinusStrain = new StrainPlane(inputStrainPlane.ChiX, inputStrainPlane.ChiY,
                inputStrainPlane.ReferencePoint, inputStrainPlane.StrainReferencePoint - dStrain);

            ForceTuple forcesPlusStrain;
            ForceTuple forcesMinusStrain;

            if (psiRebars.HasValue || psiTendon.HasValue)
            {
                forcesPlusStrain = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlanePlusStrain, rebarIsInsideAssociation);
                forcesMinusStrain = CalculateForceResultant(psiRebars.Value, psiTendon, strainPlaneMinusStrain, rebarIsInsideAssociation);
            }
            else
            {
                forcesPlusStrain = CalculateForceResultant(strainPlanePlusStrain, rebarIsInsideAssociation);
                forcesMinusStrain = CalculateForceResultant(strainPlaneMinusStrain, rebarIsInsideAssociation);
            }

            double dNdStrain = (forcesPlusStrain.N - forcesMinusStrain.N) / (2.0 * dS);
            double dMxdStrain = (forcesPlusStrain.Mx - forcesMinusStrain.Mx) / (2.0 * dS);
            double dMydStrain = (forcesPlusStrain.My - forcesMinusStrain.My) / (2.0 * dS);


            Matrix<double> partialDerivatives = Matrix<double>.Build.Dense(3, 3);

            partialDerivatives[0, 0] = dNdChiX;
            partialDerivatives[1, 0] = dMxdChiX;
            partialDerivatives[2, 0] = dMydChiX;

            partialDerivatives[0, 1] = dNdChiY;
            partialDerivatives[1, 1] = dMxdChiY;
            partialDerivatives[2, 1] = dMydChiY;

            partialDerivatives[0, 2] = dNdStrain;
            partialDerivatives[1, 2] = dMxdStrain;
            partialDerivatives[2, 2] = dMydStrain;


            Matrix<double> inputVector = Matrix<double>.Build.Dense(3, 1);
            inputVector[0, 0] = forceTuple.N;
            inputVector[1, 0] = forceTuple.Mx;
            inputVector[2, 0] = forceTuple.My;

            Matrix<double> results = partialDerivatives.Inverse() * inputVector;

            return (results[0, 0] * deltaChiXLimit,
                results[1, 0] * deltaChiYLimit,
                results[2, 0] * deltaStrainLimit);
        }

        private StrainPlane CalculateStrainPlaneStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double? psiRebars, double? psiTendon, double tolerance = 1e-5)
        {
            Dictionary<int, bool> rebarIsInsideAssociation = GetRebarIsInsideAssociation();
            ForceTuple targetLocalForcesAdim = ConvertToAdimensionalForces(localForces);

            // Valori di primo tentativo
            Point3d referencePoint = ConcreteSection.Centroid;
            double chiX = 0;
            double chiY = 0;
            double strainReferencePoint = 0;
            int id = 1;

            // piano di primo tentativo. baricentrico e ruotato di teta = 0;
            StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

            ForceTuple iterationForces;
            if(psiRebars.HasValue || psiTendon.HasValue)
                iterationForces = GetExternalForces(CalculateForceResultant(psiRebars.Value, psiTendon, strainPlane, rebarIsInsideAssociation), coordinateSystem);
            else
                iterationForces = GetExternalForces(CalculateForceResultant(strainPlane, rebarIsInsideAssociation), coordinateSystem);

            ForceTuple iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);

            if (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance * tolerance ||
                Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance * tolerance)
            {
                do
                {
                    if (id < 50)
                    {
                        try
                        {
                            (double deltaChiX, double deltaChiY, double deltaStrainRefPoint) =
                                CalculateIncrementStressAnalysis(strainPlane, localForces - iterationForces,
                                rebarIsInsideAssociation, psiRebars, psiTendon);

                            // piano di nuovo tentativo
                            id++;
                            chiX += deltaChiX;
                            chiY += deltaChiY;
                            strainReferencePoint += deltaStrainRefPoint;
                            strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainReferencePoint, id);

                            if (psiRebars.HasValue || psiTendon.HasValue)
                                iterationForces = GetExternalForces(CalculateForceResultant(psiRebars.Value, psiTendon, strainPlane, rebarIsInsideAssociation), coordinateSystem);
                            else
                                iterationForces = GetExternalForces(CalculateForceResultant(strainPlane, rebarIsInsideAssociation), coordinateSystem);

                            iterationForcesAdim = ConvertToAdimensionalForces(iterationForces);
                        }
                        catch (Exception e)
                        {
                            _log.Add(e.Message);
                            if (e.InnerException != null)
                                _log.Add(e.InnerException.Message);
                            _log.Add("Fail to calculate find strain plane");
                            return null;
                        }
                    }
                    else
                    {
                        _log.Add("Fail to calculate find strain plane");
                        return null;
                    }

                } while (Math.Abs(iterationForcesAdim.N - targetLocalForcesAdim.N) > tolerance ||
                     Math.Abs(iterationForcesAdim.Mx - targetLocalForcesAdim.Mx) > tolerance ||
                     Math.Abs(iterationForcesAdim.My - targetLocalForcesAdim.My) > tolerance);
            }

            return strainPlane;
        }

        #endregion

        #region Protected method - Linear stress method

        protected StrainPlane CalculateStrainPlaneLinearStressAnalysis(ForceTuple localForces, CoordinateSystem coordinateSystem, double psiRebars, double? psiTendon, double tolerance = 1e-5)
        {
            return CalculateStrainPlaneStressAnalysis(localForces, coordinateSystem, psiRebars, psiTendon, tolerance);
        }

        #endregion

        #region Protected method - Convert forces local/global

        protected virtual ResultBeamForces GetLocalForces(ResultBeamForces externalForces, Vector2d forceReferencePoint)
        {
            return new ResultBeamForces(
                externalForces.N,
                externalForces.V1,
                externalForces.V2,
                externalForces.T,
                externalForces.M1 + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                externalForces.M2 + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X),
                new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected ForceTuple GetLocalForces(ForceTuple externalForces, Vector2d forceReferencePoint)
        {
            return new ForceTuple(externalForces.N,
                externalForces.Mx + externalForces.N * (ConcreteSection.Centroid.Y - forceReferencePoint.Y),
                externalForces.My + externalForces.N * (ConcreteSection.Centroid.X - forceReferencePoint.X));
        }

        protected virtual ResultBeamForces GetExternalForces(ResultBeamForces localForces, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ResultBeamForces(
                localForces.N,
                localForces.V1,
                localForces.V2,
                localForces.T,
                localForces.M1 + localForces.N * (forceReferenceCoordinateSystem.Origin.Y - ConcreteSection.Centroid.Y),
                localForces.M2 + localForces.N * (forceReferenceCoordinateSystem.Origin.X - ConcreteSection.Centroid.X),
                new CoordinateSystem(ConcreteSection.Centroid, Vector3d.XAxis, Vector3d.YAxis));
        }

        protected virtual ForceTuple GetExternalForces(ForceTuple forceTuple, CoordinateSystem forceReferenceCoordinateSystem)
        {
            return new ForceTuple(forceTuple.N,
                forceTuple.Mx + forceTuple.N * (forceReferenceCoordinateSystem.Origin.Y - ConcreteSection.Centroid.Y),
                forceTuple.My - forceTuple.N * (forceReferenceCoordinateSystem.Origin.X - ConcreteSection.Centroid.X));
        }

        #endregion

        #region Equals - hashcode - operators - serialization

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is SectionSolver solver && _concreteSection.Equals(solver._concreteSection);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_concreteSection);
                hashCode = hashCode * -17 + _standard.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ConcreteSection", _concreteSection);
            info.AddValue("Standard", _standard);
            info.AddValue("Log", _log);
            info.AddValue("StressAnalysisTolerance", _stressAnalysisTolerance);
            info.AddValue("FailureAnalysisAngularTolerance", _failureAnalysisAngularTolerance);
            info.AddValue("TetaDiscretization", _tetaDiscretization);
            info.AddValue("GaussIntegrationQuadPoints", _gaussIntegrationQuadPoints);
            info.AddValue("GaussIntegrationTriPoints", _gaussIntegrationTriPoints);
            info.AddValue("ConsiderTensileConcrete", _considerTensileConcrete);
        }

        public List<string> GetLog()
        {
            return _log;
        }

        #endregion
    }
}
