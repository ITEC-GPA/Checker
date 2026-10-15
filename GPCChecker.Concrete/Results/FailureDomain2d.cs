using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class FailureDomain2d : ModelObject, ISerializable
    {
        #region Variables

        protected readonly FailureDomain.FailureDomainPoint[] _domainPoints;
        protected readonly Dictionary<FailureDomain.FailureDomainPoint, Point2d> _domainPoints2dAssociation;
        protected readonly FailureDomainResult2d.DomainTypes _domainType;
        private readonly double _momentAxis;

        /// <summary>Below this |cos α| (α the direction of a moment) the axis is taken as the My axis: the tolerance of ANTHEA's CheckerDomain2D.</summary>
        private const double AxisCosineTolerance = 1e-8;

        /// <summary>Below this projection on the moment axis (N mm) a point of the meridian is taken as having no moment in the plane.</summary>
        private const double MomentInPlaneTolerance = 1.0;

        #endregion

        #region Properties

        public FailureDomain.FailureDomainPoint[] DomainPoints => _domainPoints;

        /// <summary>
        /// Dictionary of association between 3d domain points and 2d domain points: for <see cref="FailureDomainResult2d.DomainTypes.ConstantN"/>
        /// (MxRd, MyRd); for <see cref="FailureDomainResult2d.DomainTypes.ConstantMxMy"/> (NRd, m) with m the modulus of the moment with the sign of
        /// its projection MxRd cos α + MyRd sin α on the <see cref="MomentAxis"/> α (a point whose projection is below 1 N mm, as a pole of the
        /// meridian or a point with a transverse moment only, gets the projection itself, about 0)
        /// </summary>
        public Dictionary<FailureDomain.FailureDomainPoint, Point2d> DomainPoints2dAssociation => _domainPoints2dAssociation;

        public FailureDomainResult2d.DomainTypes DomainType => _domainType;

        /// <summary>
        /// The axis of the 2d moment of a <see cref="FailureDomainResult2d.DomainTypes.ConstantMxMy"/> (N–M) domain, in radians in the force
        /// reference axes of the points, in (-π/2; π/2]: the 2d moment m of a point (and of a force, which <see cref="FailureDomainResult2d"/>
        /// takes from M1) is positive when the moment points along (cos α, sin α), that is on the side of positive Mx, or of positive My when the
        /// axis is the My axis (|cos α| ≤ 1e-8). It generalizes the sign by Mx (by My when Mx is below 1 N mm) of the earlier versions, which
        /// mixed the two branches of a meridian with a transverse moment (bug SLU-1 of 15/10/2026). NaN for the domains at constant N and for a
        /// domain deserialized from an earlier version
        /// </summary>
        public double MomentAxis => _momentAxis;

        #endregion

        #region Constructor

        /// <summary>
        /// The 2d domain of the points. For an N–M domain (<see cref="FailureDomainResult2d.DomainTypes.ConstantMxMy"/>) the points are a
        /// meridian in the direction of positive moment followed by the opposite one, and the <see cref="MomentAxis"/> is taken from the points:
        /// the direction of the sum of the moments of the first half minus the second half
        /// </summary>
        public FailureDomain2d(FailureDomain.FailureDomainPoint[] domainPoints, FailureDomainResult2d.DomainTypes domainType)
            : this(domainPoints, domainType, domainType == FailureDomainResult2d.DomainTypes.ConstantN ? double.NaN : AxisOfPoints(domainPoints))
        {
        }

        /// <summary>
        /// The 2d domain of the points with the axis of the 2d moment of an N–M domain declared by the caller (see <see cref="MomentAxis"/>)
        /// </summary>
        /// <param name="domainPoints">The points</param>
        /// <param name="domainType">The type of domain</param>
        /// <param name="momentDirection">For an N–M domain the direction of the moment of the meridian (radians, force reference axes, any sign:
        /// it is reduced to (-π/2; π/2]); ignored for a domain at constant N</param>
        internal FailureDomain2d(FailureDomain.FailureDomainPoint[] domainPoints, FailureDomainResult2d.DomainTypes domainType, double momentDirection)
        {
            _domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
            _domainType = domainType;
            _momentAxis = domainType == FailureDomainResult2d.DomainTypes.ConstantN ? double.NaN : NormalizeMomentAxis(momentDirection);
            _domainPoints2dAssociation = new Dictionary<FailureDomain.FailureDomainPoint, Point2d>();
            CalculateDomainPoints2dAssociation();
        }

        protected FailureDomain2d(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _domainPoints = (FailureDomain.FailureDomainPoint[])info.GetValue("FailureDomainPoints", typeof(FailureDomain.FailureDomainPoint[]));
            _domainType = (FailureDomainResult2d.DomainTypes)info.GetValue("DomainType", typeof(FailureDomainResult2d.DomainTypes));
            _domainPoints2dAssociation = (Dictionary<FailureDomain.FailureDomainPoint, Point2d>)info.GetValue("DomainPoints2dAssociation",
                typeof(Dictionary<FailureDomain.FailureDomainPoint, Point2d>));
            _momentAxis = double.NaN;
            foreach (SerializationEntry entry in info)
                if (entry.Name == "MomentAxis")
                    _momentAxis = (double)entry.Value;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Return the domain point for input 2d forces <paramref name="point"/>
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        internal (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) GetDomainPoint(Point2d point, SectionSolver sectionSolver, Checkers.SectionChecker.SectionOptions sectionOptions)
        {
            return GetDomainPoint(point.X, point.Y, sectionSolver, sectionOptions);
        }

        /// <summary>
        /// Return the domain point for input 2d forces <paramref name="x"/>, <paramref name="y"/>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected (FailureDomain.FailureDomainPoint failureDomainPoint, Point2d point2D) GetDomainPoint(double x, double y, SectionSolver sectionSolver, Checkers.SectionChecker.SectionOptions sectionOptions)
        {
            for (int i = 0; i < _domainPoints.Length; i++)
            {
                Point2d point1 = _domainPoints2dAssociation[_domainPoints[i]];
                FailureDomain.FailureDomainPoint vA = _domainPoints[i];
                FailureDomain.FailureDomainPoint vB;
                Point2d point2;

                if (i != _domainPoints.Length - 1)
                {
                    vB = _domainPoints[i + 1];
                    point2 = _domainPoints2dAssociation[_domainPoints[i + 1]];
                }
                else
                {
                    vB = _domainPoints[0];
                    point2 = _domainPoints2dAssociation[_domainPoints[0]];
                }

                Line2d line = new Line2d(new Point2d(0, 0), new Point2d(x, y));
                Line2d edge = new Line2d(point1, point2);

                if (edge.GetIntersectionWithInfiniteLine(line, out Point2d intersection, 1))
                {
                    if ((Math.Sign(x) == Math.Sign(intersection.X) || x == 0 || Math.Abs(point1.X) < 1) && ((Math.Sign(y) == Math.Sign(intersection.Y)) || y == 0 || Math.Abs(point1.Y) < 1))
                    {
                        if (edge.IsPointOnLine(intersection, 2))
                        {
                            if (_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
                            {
                                double mx = intersection.X;
                                double my = intersection.Y;
                                ForceTuple forceTuple = new ForceTuple(_domainPoints[i].NRd, mx, my);

                                // FailureIndex
                                SectionSolver.FailureZones _failureIndex = (SectionSolver.FailureZones)Math.Min((int)vA.FailureIndex, (int)vB.FailureIndex);

                                // Weights of the strain planes of the ends at the parameter of the intersection on the side (0 at vA). Before
                                // (found with bug SLU-1 of 15/10/2026) the parameter itself, taken from Mx only, was the weight of vA: the plane
                                // was the one of the other end of the side.
                                double t = EdgeParameter(point1, point2, intersection);
                                double weightA = 1 - t;
                                double weightB = t;

                                // Theta
                                var thetaA = vA.StrainPlane.Teta;
                                var thetaB = vB.StrainPlane.Teta;

                                // Make them close together.
                                if (Math.Abs(thetaA - thetaB) > Math.PI)
                                {
                                    if (thetaA < thetaB)
                                        thetaA += 2.0 * Math.PI;
                                    else
                                        thetaB += 2.0 * Math.PI;
                                }
                                double theta = thetaA * weightA + thetaB * weightB;

                                // Immersione
                                var immA = sectionSolver.GetImmersione(vA, _failureIndex);
                                var immB = sectionSolver.GetImmersione(vB, _failureIndex);
                                double immersione = immA * weightA + immB * weightB;

                                StrainPlane strainPlane = sectionSolver.BuildPlane(theta, sectionOptions.FailureDomainType, _failureIndex, immersione);

                                return (new FailureDomain.FailureDomainPoint(forceTuple, _failureIndex, strainPlane, immersione), intersection);
                            }
                            else
                            {
                                // The point of the 3d domain on the side of the meridian at the parameter of the intersection, component by
                                // component. Before (bug SLU-1 of 15/10/2026) the 2d point was rotated into 3d with GetCoordinateSystem: right
                                // only for a meridian of positive Mx (N = N cos α - M sin α, Mx = N sin α + M cos α, My = 0 otherwise), and the
                                // weights of the strain plane were the interpolation parameter itself (1 at vB), taken from Mx also for a
                                // meridian of My.
                                double t = EdgeParameter(point1, point2, intersection);
                                ForceTuple forceTuple = new ForceTuple(vA.NRd + t * (vB.NRd - vA.NRd), vA.MxRd + t * (vB.MxRd - vA.MxRd),
                                    vA.MyRd + t * (vB.MyRd - vA.MyRd));

                                // FailureIndex
                                SectionSolver.FailureZones _failureIndex = (SectionSolver.FailureZones)Math.Min((int)vA.FailureIndex, (int)vB.FailureIndex);

                                double weightA = 1 - t;
                                double weightB = t;

                                // Theta
                                var thetaA = vA.StrainPlane.Teta;
                                var thetaB = vB.StrainPlane.Teta;

                                // Make them close together.
                                if (Math.Abs(thetaA - thetaB) > Math.PI)
                                {
                                    if (thetaA < thetaB)
                                        thetaA += 2.0 * Math.PI;
                                    else
                                        thetaB += 2.0 * Math.PI;
                                }
                                double theta = thetaA * weightA + thetaB * weightB;

                                // Immersione
                                var immA = sectionSolver.GetImmersione(vA, _failureIndex);
                                var immB = sectionSolver.GetImmersione(vB, _failureIndex);
                                double immersione = immA * weightA + immB * weightB;

                                StrainPlane strainPlane = sectionSolver.BuildPlane(theta, sectionOptions.FailureDomainType, _failureIndex, immersione);

                                return (new FailureDomain.FailureDomainPoint(forceTuple, _failureIndex, strainPlane, immersione), intersection);
                            }
                        }
                    }
                }
            }

            return (new FailureDomain.FailureDomainPoint(new ForceTuple(), SectionSolver.FailureZones.F1,
                new StrainPlane(0, 0, new Point2d(0, 0), 0), 0.0), new Point2d());
        }

        protected void CalculateDomainPoints2dAssociation()
        {
            // caso N costante
            if (_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
            {
                for (int i = 0; i < _domainPoints.Length; i++)
                    if (_domainPoints[i] != null)
                        _domainPoints2dAssociation.Add(_domainPoints[i], new Point2d(_domainPoints[i].MxRd, _domainPoints[i].MyRd));
            }
            else // caso Mx/My costante
            {
                // The sign of the 2d moment is the sign of the projection of the moment on the moment axis (see DomainPoints2dAssociation):
                // before, the sign of Mx (of My when |Mx| < 1 N mm) mixed the two branches of a meridian of My with a transverse Mx.
                double cos = Math.Cos(_momentAxis), sin = Math.Sin(_momentAxis);
                for (int i = 0; i < _domainPoints.Length; i++)
                    if (_domainPoints[i] != null)
                    {
                        double projection = _domainPoints[i].MxRd * cos + _domainPoints[i].MyRd * sin;
                        double moment = Math.Abs(projection) < MomentInPlaneTolerance ? projection
                            : Math.Sign(projection) * Math.Sqrt(Math.Pow(_domainPoints[i].MxRd, 2) + Math.Pow(_domainPoints[i].MyRd, 2));

                        if (!_domainPoints2dAssociation.ContainsKey(_domainPoints[i]))
                            _domainPoints2dAssociation.Add(_domainPoints[i], new Point2d(_domainPoints[i].NRd, moment));
                    }
            }
        }

        /// <summary>
        /// The axis of the 2d moment (-π/2; π/2] of a moment direction: the direction itself or the opposite one, on the side of positive Mx, or of
        /// positive My when |cos| ≤ 1e-8 (the rule of the sign of ANTHEA's CheckerDomain2D)
        /// </summary>
        internal static double NormalizeMomentAxis(double direction)
        {
            if (double.IsNaN(direction) || double.IsInfinity(direction))
                return 0.0;
            double cos = Math.Cos(direction), sin = Math.Sin(direction);
            if (Math.Abs(cos) > AxisCosineTolerance ? cos < 0 : sin < 0)
            {
                cos = -cos;
                sin = -sin;
            }
            return Math.Abs(cos) > AxisCosineTolerance ? Math.Atan2(sin, cos) : Math.PI / 2;
        }

        /// <summary>
        /// The direction of the moment of an N–M meridian given as points only: the sum of the moments of the first half of the points minus the
        /// sum of the second half (the meridian of positive moment followed by the opposite one); 0 if the points have no moment
        /// </summary>
        private static double AxisOfPoints(FailureDomain.FailureDomainPoint[] domainPoints)
        {
            if (domainPoints == null)
                return 0.0;
            double mx = 0, my = 0;
            for (int i = 0; i < domainPoints.Length; i++)
                if (domainPoints[i] != null)
                {
                    double sign = 2 * i < domainPoints.Length ? 1 : -1;
                    mx += sign * domainPoints[i].MxRd;
                    my += sign * domainPoints[i].MyRd;
                }
            return mx == 0 && my == 0 ? 0.0 : Math.Atan2(my, mx);
        }

        /// <summary>
        /// The parameter of the point <paramref name="point"/> on the segment from <paramref name="start"/> (0) to <paramref name="end"/> (1),
        /// from its projection on the segment, limited to [0; 1]; 0 for a segment of zero length
        /// </summary>
        private static double EdgeParameter(Point2d start, Point2d end, Point2d point)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y, length2 = dx * dx + dy * dy;
            if (!(length2 > 0))
                return 0.0;
            double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / length2;
            return Math.Max(0.0, Math.Min(1.0, t));
        }

        protected CoordinateSystem GetCoordinateSystem()
        {
            if (_domainType == FailureDomainResult2d.DomainTypes.ConstantN)
            {
                return new CoordinateSystem(new Point3d(0, 0, _domainPoints[0].NRd), Vector3d.XAxis, Vector3d.YAxis);
            }
            else
            {
                CoordinateSystem coordinateSystem = CoordinateSystem.Global;
                coordinateSystem.RotateV3(Math.Atan2(-_domainPoints[(int)(_domainPoints.Length / 4.0)].MyRd,
                    _domainPoints[(int)(_domainPoints.Length / 4.0)].MxRd));

                return coordinateSystem;
            }
        }

        #endregion

        #region Equals, hashcode, operators

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("FailureDomainPoints", _domainPoints);
            info.AddValue("DomainType", _domainType);
            info.AddValue("DomainPoints2dAssociation", _domainPoints2dAssociation);
            info.AddValue("MomentAxis", _momentAxis);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is FailureDomain2d domain &&
                   base.Equals(obj) &&
                   EqualityComparer<FailureDomain.FailureDomainPoint[]>.Default.Equals(_domainPoints, domain._domainPoints) &&
                   _domainType == domain._domainType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<FailureDomain.FailureDomainPoint[]>.Default.GetHashCode(_domainPoints);
                hashCode = hashCode * -17 + _domainType.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}