using GPC.Checker.Results.ResultType;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
    /// <summary>
    /// The strain plane - epsilon(x,y) = epsilon0 - chi * ((-sin(teta)*x + cos(teta)*y)
    /// </summary>

    [Serializable]
    public sealed class StrainPlane : ModelObjectId, ISerializable, IEquatable<StrainPlane>
    {
        #region Variables

        private readonly Point2d _referencePoint;
        private readonly double _chiY;
        private readonly double _chiX;
        private readonly double _strainReferencePoint;

        #endregion

        #region Properties

        /// <summary>
        /// The point where is set <see cref="StrainReferencePoint"/>
        /// </summary>
        public Point2d ReferencePoint => _referencePoint;

        /// <summary>
        /// The angle between the strain plane and the plane of section
        /// </summary>
        public double Teta => CalculateTeta();

        /// <summary>
        /// The curvature of the strain plane
        /// </summary>
        public double Chi => CalculateChi();

        /// <summary>
        /// The value of the strain in the <see cref="ReferencePoint"/>
        /// </summary>
        public double StrainReferencePoint => _strainReferencePoint;

        /// <summary>
        /// The curvature of the strain plane along X axis
        /// </summary>
        public double ChiX => _chiX;

        /// <summary>
        /// The curvature of the strain plane along Y axis
        /// </summary>
        public double ChiY => _chiY;

        #endregion

        #region Constructor

        public StrainPlane(Point2d centerOfStrainPlane, double teta, double chi, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
            : base(id, name)
        {
            _referencePoint = centerOfStrainPlane;
            _chiX = chi * Math.Sin(teta);
            _chiY = -chi * Math.Cos(teta);
            _strainReferencePoint = epsilonCenterOfStrainPlane;
        }

        public StrainPlane(double chiX, double chiY, Point2d centerOfStrainPlane, double epsilonCenterOfStrainPlane, int id = IDUNASSIGNED, string name = "")
            : base(id, name)
        {
            _referencePoint = centerOfStrainPlane;
            _chiX = chiX;
            _chiY = chiY;
            _strainReferencePoint = epsilonCenterOfStrainPlane;
        }

        private StrainPlane(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _referencePoint = (Point2d)info.GetValue("ReferecePoint", typeof(Point2d));
            _chiX = info.GetDouble("ChiX");
            _chiY = info.GetDouble("ChiY");
            _strainReferencePoint = info.GetDouble("StrainReferencePoint");
        }

        #endregion

        #region Public Methods

        public double GetStrain(Point2d point)
        {
            return _strainReferencePoint + ChiX * (point.X - _referencePoint.X) + ChiY * (point.Y - _referencePoint.Y);
        }

        public double GetStrain(double x, double y)
        {
            return _strainReferencePoint + ChiX * (x - _referencePoint.X) + ChiY * (y - _referencePoint.Y);
        }

        public Line2d GetNeutralAxisRespectReferencePoint()
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d(-StrainReferencePoint / _chiX, 100.0), new Point2d(-StrainReferencePoint / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, -StrainReferencePoint / _chiY), new Point2d(-100.0, -StrainReferencePoint / _chiY));
            else
            {
                Line2d line = new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(-StrainReferencePoint / _chiX, 0.0));
                Vector2d vector = line.ToVector();
                vector.Unitize();

                return new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(0.0 + vector.X, -StrainReferencePoint / _chiY + vector.Y));
            }
        }

        public Line2d GetNeutralAxis()
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d(-StrainReferencePoint / _chiX, 100.0), new Point2d(-StrainReferencePoint / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, -StrainReferencePoint / _chiY), new Point2d(-100.0, -StrainReferencePoint / _chiY));
            else
                return new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY + _chiX / _chiY * _referencePoint.X),
                    new Point2d(-StrainReferencePoint / _chiX + _chiY / _chiX * _referencePoint.Y, 0.0));
        }

        public Line2d GetConstantStrainAxis(double strain)
        {
            if (_chiX == 0 && _chiY == 0)
                return null;
            else if (_chiY == 0 && _chiX != 0)
                return new Line2d(new Point2d((strain - StrainReferencePoint) / _chiX, 100.0), new Point2d((strain - StrainReferencePoint) / _chiX, -100.0));
            else if (_chiX == 0 && _chiY != 0)
                return new Line2d(new Point2d(100.0, (strain - StrainReferencePoint) / _chiY), new Point2d(-100.0, (strain - StrainReferencePoint) / _chiY));
            else
            {
                Line2d line = new Line2d(new Point2d(0.0, -StrainReferencePoint / _chiY), new Point2d(-StrainReferencePoint / _chiX, 0.0));
                Vector2d vector = line.ToVector();
                vector.Unitize();

                return new Line2d(new Point2d(0.0, (strain - StrainReferencePoint) / _chiY), new Point2d(0.0 + vector.X, (strain - StrainReferencePoint) / _chiY + vector.Y));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_section"></param>
        /// <param name="resultBeamForces"></param>
        /// <param name="_sectionSolver"></param>
        /// <param name="_standard"></param>
        /// <param name="linearAnalysis"></param>
        /// <param name="psiR">psi used by rebars and structural steel.</param>
        /// <param name="psiT">psi used by tendons.</param>
        /// <param name="standardStructuralSteel"></param>
        /// <returns></returns>
        public StrainPlaneResult CalculateStrainPlaneResult(IConcreteSection _section, ResultBeamForces resultBeamForces, SectionSolver _sectionSolver, Standard _standard,
            bool linearAnalysis = false, double psiR = 0, double psiT = 0, StandardEN1993p11 standardStructuralSteel = null)
        {
            double sigmaCMax = double.MinValue;
            double sigmaSMax = double.MinValue;
            double sigmaPMax = double.MinValue;
            double sigmaSSMax = double.MinValue;
            double sigmaCMin = double.MaxValue;
            double sigmaSMin = double.MaxValue;
            double sigmaPMin = double.MaxValue;
            double sigmaSSMin = double.MaxValue;

            double epsilonCMax = double.MinValue;
            double epsilonPMax = double.MinValue;
            double epsilonSMax = double.MinValue;
            double epsilonSSMax = double.MinValue;
            double epsilonCMin = double.MaxValue;
            double epsilonSMin = double.MaxValue;
            double epsilonPMin = double.MaxValue;
            double epsilonSSMin = double.MaxValue;

            double netHeight;
            double neutralAxisDistance = double.NaN;
            double neutralAxisAngle;

            if (this != null)
            {
                for (int i = 0; i < _section.Shape.Fill.Count; i++)
                {
                    double strain = GetStrain(_section.Shape.Fill[i]);
                    if (strain > epsilonCMax)
                    {
                        epsilonCMax = strain;

                        double sigmaCMaxbuffer;
                        if (linearAnalysis)
                            sigmaCMaxbuffer = _sectionSolver.CalculateElasticSigmaC(epsilonCMax);
                        else
                        {
                            var buff = _section.ConcreteMaterial.CalculateDesignStressConcrete(_standard, epsilonCMax);
                            if (buff <= 0)
                                sigmaCMaxbuffer = buff;
                            else
                            {
                                if (_sectionSolver.ConsiderTensileConcrete == true)
                                    sigmaCMaxbuffer = buff;
                                else
                                    sigmaCMaxbuffer = 0;
                            }
                        }

                        if (sigmaCMaxbuffer > sigmaCMax)
                            sigmaCMax = sigmaCMaxbuffer;

                    }
                    if (strain < epsilonCMin)
                    {
                        epsilonCMin = strain;
                        double sigmaCMinbuffer;
                        if (linearAnalysis)
                            sigmaCMinbuffer = _sectionSolver.CalculateElasticSigmaC(epsilonCMin);
                        else
                        {
                            var buff = _section.ConcreteMaterial.CalculateDesignStressConcrete(_standard, epsilonCMin);
                            if (buff <= 0)
                                sigmaCMinbuffer = buff;
                            else
                            {
                                if (_sectionSolver.ConsiderTensileConcrete == true)
                                    sigmaCMinbuffer = buff;
                                else
                                    sigmaCMinbuffer = 0;
                            }
                        }

                        if (sigmaCMinbuffer < sigmaCMin)
                            sigmaCMin = sigmaCMinbuffer;
                    }
                }

                for (int r = 0; r < _section.RebarsCount; r++)
                {
                    ReinforcedConcreteRebar rebar = _section.Rebars.ElementAt(r);
                    double strain = GetStrain(rebar.Position);

                    if (rebar.EpsilonP > 0)
                    {
                        if (strain > epsilonPMax)
                        {
                            epsilonPMax = strain;

                            if (linearAnalysis)
                                sigmaPMax = _sectionSolver.CalculateElasticSigmaS(psiT, rebar, strain);
                            else
                                sigmaPMax = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
                        }
                        if (strain < epsilonPMin)
                        {
                            epsilonPMin = strain;

                            if (linearAnalysis)
                                sigmaPMin = _sectionSolver.CalculateElasticSigmaS(psiT, rebar, strain);
                            else
                                sigmaPMin = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
                        }
                    }
                    else
                    {
                        if (strain > epsilonSMax)
                        {
                            epsilonSMax = strain;

                            if (linearAnalysis)
                                sigmaSMax = _sectionSolver.CalculateElasticSigmaS(psiR, rebar, strain);
                            else
                                sigmaSMax = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
                        }
                        if (strain < epsilonSMin)
                        {
                            epsilonSMin = strain;

                            if (linearAnalysis)
                                sigmaSMin = _sectionSolver.CalculateElasticSigmaS(psiR, rebar, strain);
                            else
                                sigmaSMin = rebar.RebarMaterial.CalculateDesignStress(_standard, strain, rebar.EpsilonP);
                        }
                    }
                }

                foreach (var steelSection in _section.SteelSections)
                {
                    for (int i = 0; i < steelSection.Section.Shape.Fill.Count; i++)
                    {
                        var localVertex = steelSection.Section.Shape.Fill[i];
                        var globalVertex = steelSection.PositionToGlobal(localVertex);

                        double strain = GetStrain(globalVertex);

                        if (strain > epsilonSSMax)
                        {
                            epsilonSSMax = strain;

                            if (linearAnalysis)
                                sigmaSMax = _sectionSolver.CalculateElasticSigmaS(psiR, steelSection.Section, strain);
                            else
                                sigmaSMax = steelSection.Section.SteelMaterial.CalculateDesignStress(_standard, strain);
                        }
                        if (strain < epsilonSMin)
                        {
                            epsilonSMin = strain;

                            if (linearAnalysis)
                                sigmaSMin = _sectionSolver.CalculateElasticSigmaS(psiR, steelSection.Section, strain);
                            else
                                sigmaSMin = steelSection.Section.SteelMaterial.CalculateDesignStress(_standard, strain);
                        }
                    }
                }

                if (linearAnalysis)
                {
                    epsilonSMax *= (1 + psiR);
                    epsilonSMin *= (1 + psiR);
                    epsilonPMax *= (1 + psiT);
                    epsilonPMin *= (1 + psiT);
                }

                var neutralAxis = GetNeutralAxisRespectReferencePoint();
                neutralAxis?.Move(ReferencePoint.X, ReferencePoint.Y);

                double angle = Teta;
                neutralAxisAngle = angle.ToDegrees();

                var dist = _sectionSolver.CalculateMaxMinSectionDistances(angle);

                double dmaxRebar = dist.dmaxRebar;
                double dmaxConcrete = dist.dmaxConcrete;
                double dminConcrete = dist.dminConcrete;
                int dMaxVertexIndex = dist.dMaxVertexIndex;

                if (_section.ConcreteMaterial.ConcreteType == ConcreteMaterial.ConcreteTypes.FRC)
                    netHeight = dmaxConcrete - dminConcrete;
                else if (_section.RebarsCount == 0)
                    netHeight = 0;
                else
                    netHeight = dmaxRebar - dminConcrete;

                if (neutralAxis != null)
                    neutralAxisDistance = new Line3d(neutralAxis).DistanceTo(_section.Shape.Fill[dMaxVertexIndex]);
            }
            else
            {
                sigmaCMax = double.NaN;
                sigmaSMax = double.NaN;
                sigmaPMax = double.NaN;
                sigmaSSMax = double.NaN;
                sigmaCMin = double.NaN;
                sigmaSMin = double.NaN;
                sigmaPMin = double.NaN;
                sigmaSSMin = double.NaN;
                epsilonCMax = double.NaN;
                epsilonPMax = double.NaN;
                epsilonSMax = double.NaN;
                epsilonSSMax = double.NaN;
                epsilonCMin = double.NaN;
                epsilonSMin = double.NaN;
                epsilonPMin = double.NaN;
                epsilonSSMin = double.NaN;
                netHeight = double.NaN;
                neutralAxisDistance = double.NaN;
                neutralAxisAngle = double.NaN;
            }

            return new StrainPlaneResult(_section, resultBeamForces, this, _sectionSolver, _standard, _id, standardStructuralSteel)
            {
                SigmaCMax = sigmaCMax,
                SigmaSMax = sigmaSMax,
                SigmaPMax = sigmaPMax,
                SigmaSSMax = sigmaSSMax,
                SigmaCMin = sigmaCMin,
                SigmaSMin = sigmaSMin,
                SigmaPMin = sigmaPMin,
                SigmaSSMin = sigmaSSMin,
                EpsilonCMax = epsilonCMax,
                EpsilonSMax = epsilonSMax,
                EpsilonPMax = epsilonPMax,
                EpsilonSSMax = epsilonSSMax,
                EpsilonCMin = epsilonCMin,
                EpsilonSMin = epsilonSMin,
                EpsilonPMin = epsilonPMin,
                EpsilonSSMin = epsilonSSMin,
                NetHeight = netHeight,
                NeutralAxisAngle = neutralAxisAngle,
                NeutralAxisDistance = neutralAxisDistance,
                LinearAnalysis = linearAnalysis,
                PsiR = psiR,
                PsiT = psiT,
            };
        }

        #endregion

        #region Private Methods

        private double CalculateTeta()
        {
            if (_chiY != 0)
            {
                double teta = Math.Atan(-_chiX / _chiY); // -π/2 ≤ θ ≤ π/2
                if (_chiY > 0)
                    teta -= Math.PI; // -3*π/2 ≤ θ ≤ -π/2

                return teta; // -3*π/2 ≤ θ ≤ π/2
            }

            return Math.Atan2(-_chiX, _chiY); // -π ≤ θ ≤ π
        }

        private double CalculateChi()
        {
            if (Math.Abs(Teta) < GeometryBase.GetDefaultTolerance() ||
                Math.Abs(Math.Abs(Teta) - Math.PI) < GeometryBase.GetDefaultTolerance())
                return -ChiY / Math.Cos(Teta);

            else if (Math.Abs(Math.Abs(Teta) - Math.PI / 2.0) < GeometryBase.GetDefaultTolerance() ||
                Math.Abs(Math.Abs(Teta) - 3.0 * Math.PI / 2.0) < GeometryBase.GetDefaultTolerance())
                return ChiX / Math.Sin(Teta);

            else
                return -ChiY / Math.Cos(Teta);
        }

        #endregion

        #region Equals, hashcode, operators

        public bool Equals(StrainPlane other)
        {
            return !(other is null) &&
                   EqualityComparer<Point2d>.Default.Equals(_referencePoint, other._referencePoint) &&
                   _chiX == other._chiX &&
                   _chiY == other._chiY &&
                   _strainReferencePoint == other._strainReferencePoint;
        }

        public override bool Equals(object obj)
        {
            return Equals((StrainPlane)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -23;
                hashCode = hashCode * -17 + EqualityComparer<Point2d>.Default.GetHashCode(_referencePoint);
                hashCode = hashCode * -17 + _chiX.GetHashCode();
                hashCode = hashCode * -17 + _chiY.GetHashCode();
                hashCode = hashCode * -17 + _strainReferencePoint.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ReferecePoint", _referencePoint, typeof(Point2d));
            info.AddValue("ChiX", _chiX, typeof(double));
            info.AddValue("ChiY", _chiY, typeof(double));
            info.AddValue("StrainReferencePoint", _strainReferencePoint, typeof(double));
        }

        #endregion
    }
}
