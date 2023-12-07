using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class FailureDomainResult : CheckerResultType, ISerializable
    {
        #region Variables

        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain _failureDomain;
        protected List<FailureDomain.FailureDomainForce> _forces;
        protected Checkers.SectionChecker.SectionOptions _sectionOption;

        protected int _failureSectionSubdivision;

        #endregion

        #region Properties

        public FailureDomain Domain => _failureDomain;

        public SectionSolver.FailureDomainTypes FailureDomainAnalysisType { get => _failureDomain.FailureDomainAnalysisTypes; set => _failureDomain.FailureDomainAnalysisTypes = value; }

        public CoordinateSystem CoordinateSystem => _sectionOption.ForceReferenceCoordinateSystem;

        public SectionSolver.FailureAnalysisTypes FailureAnalysisType { get => _sectionOption.FailureAnalysisType; set => _sectionOption.FailureAnalysisType = value; }

        #endregion

        #region Constructor

        public FailureDomainResult(IConcreteSection section, FailureDomain failureDomain, IEnumerable<ResultBeamForces> forces,
            SectionSolver solver, Standard standard, Checkers.SectionChecker.SectionOptions sectionOption, int id = IDUNASSIGNED,
            Standard standardStructuralSteel = null)
            : base(section, standard, id, standardStructuralSteel)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce>();
            _sectionOption = sectionOption ?? throw new ArgumentNullException();

            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i],
                        _sectionSolver.CalculateDomainPoint(forcesList[i])));
                }
            }

            _failureSectionSubdivision = 20;
        }

        protected FailureDomainResult(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _sectionSolver = (SectionSolver)info.GetValue("SectionSolver", typeof(SectionSolver));
            _failureDomain = (FailureDomain)info.GetValue("FailureDomain", typeof(FailureDomain));
            _sectionOption = (Checkers.SectionChecker.SectionOptions)info.GetValue("SectionOption", typeof(Checkers.SectionChecker.SectionOptions));
            _forces = (List<FailureDomain.FailureDomainForce>)info.GetValue("Forces", typeof(List<FailureDomain.FailureDomainForce>));
            _failureSectionSubdivision = info.GetInt32("Subdivision");
        }

        #endregion

        #region Public Async Methods

        /// <summary>
        /// Adds a new single force asynchronously
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public async Task<FailureDomain.FailureDomainPoint> AddForceAsync(ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() =>
            {
                failureDomainPoint = _sectionSolver.CalculateDomainPoint(force);
                _forces.Add(new FailureDomain.FailureDomainForce(force, failureDomainPoint));
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Adds a new range of forces asynchronously
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        public async Task<FailureDomain.FailureDomainPoint[]> AddForcesAsync(IEnumerable<ResultBeamForces> forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Any(f => f.Id == -1))
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint[] failureDomainPoint = null;

            await Task.Run(() =>
            {
                var forcesList = forces.ToList();
                failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

                for (int i = 0; i < forcesList.Count(); i++)
                {
                    var point = _sectionSolver.CalculateDomainPoint(forcesList[i]);
                    _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                    failureDomainPoint[i] = point;
                }
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Update an extisting force racalculating the domain point asynchronously
        /// </summary>
        /// <param name="id">The id of the original force</param>
        /// <param name="force">The new force</param>
        /// <returns>The new corresponding point in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        /// <exception cref="KeyNotFoundException">Thrown when did not found a force with the given id</exception>
        public async Task<FailureDomain.FailureDomainPoint> UpdateForceAsync(int id, ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            int index = _forces.FindIndex(f => f.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException(nameof(id));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() =>
            {
                failureDomainPoint = _sectionSolver.CalculateDomainPoint(force);
                _forces.RemoveAt(index);
                _forces.Insert(index, new FailureDomain.FailureDomainForce(force, failureDomainPoint));
            });

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only axial force of <paramref name="forces"/> is used</remarks>
        public async Task<FailureDomain2d> CalculateDomainConstantAxialForceAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() =>
            {
                failureDomain2D = CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(CoordinateSystem));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the plastic failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public async Task<FailureDomain2d> CalculateDomainConstantMomentsRatioAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() =>
            {
                failureDomain2D = CalculateFailureDomainConstantMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        public virtual async Task<FailureDomainResult2d> CalculateFailureDomainResultConstantAxialForceAsync(ResultBeamForces forces)
        {
            FailureDomainResult2d failureDomainForces = null;

            await Task.Run(() =>
            {
                failureDomainForces = new FailureDomainResult2d(_section, CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(CoordinateSystem)),
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption, IDUNASSIGNED, _standardStructuralSteel);
            });

            return failureDomainForces;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public virtual async Task<FailureDomainResult2d> CalculateFailureDomainResultConstantMomentsRatioAsync(ResultBeamForces forces)
        {
            FailureDomainResult2d failureDomainForces = null;

            await Task.Run(() =>
            {
                failureDomainForces = new FailureDomainResult2d(_section, CalculateFailureDomainConstantMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem)),
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption, IDUNASSIGNED, _standardStructuralSteel);
            });

            return failureDomainForces;
        }

        /// <summary>
        /// Calculate a new single force asynchronously
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <remarks>Force is not added to list of forces</remarks>
        public async Task<FailureDomain.FailureDomainPoint> CalculateForceAsync(ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() =>
            {
                failureDomainPoint = _sectionSolver.CalculateDomainPoint(force);
            });

            return failureDomainPoint;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a new single force
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public FailureDomain.FailureDomainPoint AddForce(ResultBeamForces force)
        {
            if (force is null || force.Id == -1)
            {
                throw new ArgumentNullException(nameof(force));
            }
            else if (force.Id == -1)
            {
                throw new ArgumentException(nameof(force));
            }

            var point = _sectionSolver.CalculateDomainPoint(force);
            _forces.Add(new FailureDomain.FailureDomainForce(force, point));

            return point;
        }

        /// <summary>
        /// Adds a new range of forces
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        public FailureDomain.FailureDomainPoint[] AddForces(ResultBeamForces[] forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Any(f => f.Id == -1))
            {
                throw new ArgumentException(nameof(forces));
            }

            List<ResultBeamForces> forcesList = forces.ToList();
            var failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

            for (int i = 0; i < forcesList.Count(); i++)
            {
                var point = _sectionSolver.CalculateDomainPoint(forcesList[i]);
                _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                failureDomainPoint[i] = point;
            }

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate a new single force
        /// </summary>
        /// <param name="force">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <remarks>Force is not added to list of forces</remarks>
        public FailureDomain.FailureDomainPoint CalculateForce(ResultBeamForces force)
        {
            if (force is null)
            {
                throw new ArgumentNullException(nameof(force));
            }
            return _sectionSolver.CalculateDomainPoint(force);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        public virtual FailureDomainResult2d CalculateDomainConstantAxialForce(ResultBeamForces forces)
        {
            return new FailureDomainResult2d(_section, CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(CoordinateSystem)),
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption, IDUNASSIGNED, _standardStructuralSteel);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public virtual FailureDomainResult2d CalculateDomainConstantMomentsRatio(ResultBeamForces forces)
        {
            return new FailureDomainResult2d(_section, CalculateFailureDomainConstantMomentsRatio(forces.ConvertToForceTuple(CoordinateSystem)),
                new ResultBeamForces[] { forces }, _sectionSolver, _standard, _sectionOption, IDUNASSIGNED, _standardStructuralSteel);
        }

        public void ClearForces()
        {
            _forces.Clear();
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        protected virtual FailureDomain2d CalculateDomainConstantAxialForce(ForceTuple forces, int axialForceSubdivision = 50)
        {
            if (forces == null)
                throw new ArgumentException();

            if (forces.N > _failureDomain.DomainPoints.FirstOrDefault().FirstOrDefault().NRd || forces.N < _failureDomain.DomainPoints.LastOrDefault().LastOrDefault().NRd)
                return null;

            FailureDomain failureDomain = _failureDomain;

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[failureDomain.DomainPoints.Length];

            Parallel.For(0, failureDomain.DomainPoints.Length, (i, state) =>
            {
                for (int j = 1; j < failureDomain.DomainPoints[i].Length; j++)
                {
                    FailureDomain.FailureDomainPoint vA = failureDomain.DomainPoints[i][j - 1];
                    FailureDomain.FailureDomainPoint vB = failureDomain.DomainPoints[i][j];

                    if (forces.N == vB.NRd)
                    {
                        points[i] = vB;
                        break;
                    }

                    if (forces.N > vB.NRd && forces.N < vA.NRd)
                    {
                        double mx = Utilities.Maths.Interpolation.GetLinearInterpolation(vA.NRd, vB.NRd, vA.MxRd, vB.MxRd, forces.N);
                        double my = Utilities.Maths.Interpolation.GetLinearInterpolation(vA.NRd, vB.NRd, vA.MyRd, vB.MyRd, forces.N);

                        // FailureIndex
                        SectionSolver.FailureZones _failureIndex = (SectionSolver.FailureZones)Math.Min((int)vA.FailureIndex, (int)vB.FailureIndex);

                        double weightA = Utilities.Maths.Interpolation.GetLinearInterpolation(vA.NRd, vB.NRd, 0, 1, forces.N);
                        double weightB = 1 - weightA;

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
                        var immA = _sectionSolver.GetImmersione(vA, _failureIndex);
                        var immB = _sectionSolver.GetImmersione(vB, _failureIndex);
                        double immersione = immA * weightA + immB * weightB;

                        StrainPlane strainPlane = _sectionSolver.BuildPlane(theta, _sectionOption.FailureDomainType, _failureIndex, immersione);

                        points[i] = new FailureDomain.FailureDomainPoint(new ForceTuple(forces.N, mx, my), _failureIndex, strainPlane, immersione);

                        break;
                    }
                }
            });

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.ConstantN);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        protected virtual FailureDomain2d CalculateFailureDomainConstantMomentsRatio(ForceTuple forces)
        {
            if (_failureDomain.FailureDomainAnalysisTypes == SectionSolver.FailureDomainTypes.Plastic)
                return CalculatePlasticDomainCostantMomentsRatio(forces, _failureSectionSubdivision);
            else
                return CalculateElasticDomainMomentsRatio(forces, _failureSectionSubdivision);
        }

        protected virtual FailureDomain2d CalculatePlasticDomainCostantMomentsRatio(ForceTuple forces, int subdivision = 20)
        {
            if (subdivision <= 2)
                throw new Exception();

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);
            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[forceTuples.Length];

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                var force = new ResultBeamForces(forceTuples[i].N, 0.0, 0.0, 0.0, forceTuples[i].Mx, forceTuples[i].My, _sectionSolver.SectionOption.ForceReferenceCoordinateSystem);
                if (forceTuples[i].Mx != 0 || forceTuples[i].My != 0)
                    points[i] = _sectionSolver.CalculateDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantN);
                else
                    points[i] = _sectionSolver.CalculateDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            });

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.ConstantMxMy);
        }

        protected virtual FailureDomain2d CalculateElasticDomainMomentsRatio(ForceTuple forces, int subdivision = 10)
        {
            if (subdivision <= 2)
                throw new Exception();

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[2 * subdivision + 2];

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                var force = new ResultBeamForces(forceTuples[i].N, 0.0, 0.0, 0.0, forceTuples[i].Mx, forceTuples[i].My, _sectionSolver.SectionOption.ForceReferenceCoordinateSystem);
                if (forceTuples[i].Mx != 0 || forceTuples[i].My != 0)
                    points[i] = _sectionSolver.CalculateDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantN);
                else
                    points[i] = _sectionSolver.CalculateDomainPoint(force, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            });

            return new FailureDomain2d(points, FailureDomainResult2d.DomainTypes.ConstantMxMy);
        }

        protected ForceTuple[] CalculateRadialForces(ForceTuple forces, int subdivision = 10)
        {
            ForceTuple[] forceTuples = new ForceTuple[2 * subdivision];

            double nMax = Domain.DomainPoints[0].Select(i => i.NRd).Max();
            double nMin = Domain.DomainPoints[0].Select(i => i.NRd).Min();

            for (int i = 0; i < subdivision / 2.0; i++)
            {
                forceTuples[i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.Mx * 2, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.My * 2, i));

                forceTuples[subdivision / 2 + i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.Mx * 2, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.My * 2, 0.0, i));

                forceTuples[2 * subdivision - 1 - i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.Mx * 2, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.My * 2, i + 1));

                forceTuples[subdivision / 2 + subdivision - 1 - i] = new ForceTuple(
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.Mx * 2, 0.0, i + 1),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.My * 2, 0.0, i + 1));
            }

            return forceTuples;
        }

        #endregion

        #region Enumerator Methods

        /// <summary>
        /// Tells if there is a force with the given id
        /// </summary>
        /// <param name="id">The id to check</param>
        /// <returns>True if the force exists</returns>
        public bool ContainsForceWithId(int id)
        {
            return _forces.Any(force => force.Id == id);
        }

        public IEnumerator<FailureDomain.FailureDomainForce> GetEnumerator()
        {
            return _forces.GetEnumerator();
        }

        public FailureDomain.FailureDomainForce[] GetFailureDomainForces()
        {
            return _forces.ToArray();
        }

        #endregion

        #region Equals, hashcode, operators

        public List<string> GetLog()
        {
            return _sectionSolver.GetLog();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is FailureDomainResult result &&
                   base.Equals(obj) &&
                   EqualityComparer<SectionSolver>.Default.Equals(_sectionSolver, result._sectionSolver) &&
                   EqualityComparer<FailureDomain>.Default.Equals(_failureDomain, result._failureDomain) &&
                   EqualityComparer<Checkers.SectionChecker.SectionOptions>.Default.Equals(_sectionOption, result._sectionOption);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _sectionSolver.GetHashCode();
                hashCode = hashCode * -17 + _failureDomain.GetHashCode();
                hashCode = hashCode * -17 + _sectionOption.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("SectionSolver", _sectionSolver);
            info.AddValue("FailureDomain", _failureDomain);
            info.AddValue("SectionOption", _sectionOption);
            info.AddValue("Forces", _forces);
            info.AddValue("Subdivision", _failureSectionSubdivision);
        }

        public static bool operator ==(FailureDomainResult left, FailureDomainResult right)
        {
            return EqualityComparer<FailureDomainResult>.Default.Equals(left, right);
        }

        public static bool operator !=(FailureDomainResult left, FailureDomainResult right)
        {
            return !(left == right);
        }

        #endregion
    }
}