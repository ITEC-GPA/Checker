using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace GPC.Checkers.Concrete.Results
{
    public class FailureDomainResult : CheckerResultType
    {
        protected readonly SectionSolver _sectionSolver;
        protected readonly FailureDomain _failureDomain;
        protected List<FailureDomain.FailureDomainForce> _forces;
        protected readonly SectionSolver.FailureDomainAnalysisTypes _analysisType;

        protected int _failureSectionSubdivision;

        public FailureDomain Domain => _failureDomain;

        public FailureDomainResult(
            IConcreteSection section,
            FailureDomain failureDomain,
            IEnumerable<ResultBeamForces> forces,
            SectionSolver solver,
            Standard standard,
            SectionSolver.FailureDomainAnalysisTypes analysisType,
            int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _forces = new List<FailureDomain.FailureDomainForce>();
            _analysisType = analysisType;

            if (forces != null)
            {
                var forcesList = forces.ToList();

                for (int i = 0; i < forces.Count(); i++)
                {
                    if(_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                        _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(section.Centroid))));
                    else
                        _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(section.Centroid))));
                }
            }

            _failureSectionSubdivision = 10; 
        }

        #region Public Async Methods

        /// <summary>
        /// Adds a new single force asynchronously
        /// </summary>
        /// <param name="forces">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        public async Task<FailureDomain.FailureDomainPoint> AddForceAsync(ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {
                if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));

                _forces.Add(new FailureDomain.FailureDomainForce(forces, failureDomainPoint));
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

            await Task.Run(() => {

                var forcesList = forces.ToList();
                failureDomainPoint = new FailureDomain.FailureDomainPoint[forcesList.Count];

                for (int i = 0; i < forcesList.Count(); i++)
                {
                    FailureDomain.FailureDomainPoint point;

                    if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
						point = _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));
                    else
                        point = _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));

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
        /// <param name="forces">The new force</param>
        /// <returns>The new corresponding point in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        /// <exception cref="KeyNotFoundException">Thrown when did not found a force with the given id</exception>
        public async Task<FailureDomain.FailureDomainPoint> UpdateForceAsync(int id, ResultBeamForces forces)
        {
            if (forces is null)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            int index = _forces.FindIndex(f => f.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException(nameof(id));
            }

            FailureDomain.FailureDomainPoint failureDomainPoint = null;

            await Task.Run(() => {

                if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                    failureDomainPoint = _sectionSolver.CalculatePlasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
                else
                    failureDomainPoint = _sectionSolver.CalculateElasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));

                _forces.RemoveAt(index);
                _forces.Insert(index, new FailureDomain.FailureDomainForce(forces, failureDomainPoint));
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

            await Task.Run(() => {
                failureDomain2D = CalculateDomainConstantAxialForce(forces.ConvertToForceTuple(ConcreteSection.Centroid));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the plastic failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public async Task<FailureDomain2d> CalculatePlasticDomainCostantAngleAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() => {
                failureDomain2D = CalculateFailureDomainCostantAngle(forces.ConvertToForceTuple(ConcreteSection.Centroid));
            });

            return failureDomain2D;
        }

        /// <summary>
        /// Calculate the elastic failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        public async Task<FailureDomain2d> CalculateElasticDomainCostantAngleAsync(ResultBeamForces forces)
        {
            FailureDomain2d failureDomain2D = null;

            await Task.Run(() => {
                failureDomain2D = CalculateElasticDomainCostantAngle(forces.ConvertToForceTuple(ConcreteSection.Centroid));
            });

            return failureDomain2D;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Adds a new single force
        /// </summary>
        /// <param name="forces">The force to add</param>
        /// <returns>The corresponding domain point</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces.Id == -1</exception>
        internal FailureDomain.FailureDomainPoint AddForce(ResultBeamForces forces)
        {
            if (forces is null || forces.Id == -1)
            {
                throw new ArgumentNullException(nameof(forces));
            }
            else if (forces.Id == -1)
            {
                throw new ArgumentException(nameof(forces));
            }

            FailureDomain.FailureDomainPoint point;

            if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                point = _sectionSolver.CalculatePlasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));
            else
                point = _sectionSolver.CalculateElasticDomainPoint(forces.ConvertToForceTuple(ConcreteSection.Centroid));

            _forces.Add(new FailureDomain.FailureDomainForce(forces, point));

            return point;
        }

        /// <summary>
        /// Adds a new range of forces
        /// </summary>
        /// <param name="forces">The forces array</param>
        /// <returns>The corresponding points in the domain</returns>
        /// <exception cref="ArgumentNullException">Thrown when forces is null</exception>
        /// <exception cref="ArgumentException">Thrown when forces contains items with Id == -1</exception>
        internal FailureDomain.FailureDomainPoint[] AddForces(ResultBeamForces[] forces)
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
                FailureDomain.FailureDomainPoint point;

                if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                    point = _sectionSolver.CalculatePlasticDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));
                else
                    point = _sectionSolver.CalculateElasticDomainPoint(forcesList[i].ConvertToForceTuple(ConcreteSection.Centroid));

                _forces.Add(new FailureDomain.FailureDomainForce(forcesList[i], point));
                failureDomainPoint[i] = point;
            }

            return failureDomainPoint;
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant value of axial force
        /// </summary>
        /// <param name="forces">Input forces</param>
        /// <returns>New FailureDomain2d</returns>
        /// <exception cref="ArgumentException"></exception>
        internal virtual FailureDomain2d CalculateDomainConstantAxialForce(ForceTuple forces)
        {
            if (forces == null)
                throw new ArgumentException();

            if (forces.N > _failureDomain.DomainPoints.FirstOrDefault().FirstOrDefault().NRd || forces.N < _failureDomain.DomainPoints.LastOrDefault().LastOrDefault().NRd)
                return null;

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[_failureDomain.DomainPoints.Length];

            for (int i = 0; i < _failureDomain.DomainPoints.Length; i++)
            {
                for (int j = 1; j < _failureDomain.DomainPoints[i].Length; j++)
                {
                    if (forces.N == _failureDomain.DomainPoints[i][j].NRd)
                    {
                        points[i] = _failureDomain.DomainPoints[i][j];
                        break;
                    }

                    if (forces.N > _failureDomain.DomainPoints[i][j].NRd && forces.N < _failureDomain.DomainPoints[i][j - 1].NRd)
                    {
                        double mx = Utilities.Maths.Interpolation.GetLinearInterpolation(_failureDomain.DomainPoints[i][j - 1].NRd, _failureDomain.DomainPoints[i][j].NRd,
                            _failureDomain.DomainPoints[i][j - 1].MxRd, _failureDomain.DomainPoints[i][j].MxRd, forces.N);
                        double my = Utilities.Maths.Interpolation.GetLinearInterpolation(_failureDomain.DomainPoints[i][j - 1].NRd, _failureDomain.DomainPoints[i][j].NRd,
                            _failureDomain.DomainPoints[i][j - 1].MyRd, _failureDomain.DomainPoints[i][j].MyRd, forces.N);

                        points[i] = new FailureDomain.FailureDomainPoint(new ForceTuple(forces.N, mx, my),
                            _failureDomain.DomainPoints[i][j].FailureIndex, null);

                        break;
                    }
                }
            }

            return new FailureDomain2d(points);
        }

        /// <summary>
        /// Calculate the failure domain 2d with costant ratio between Mx and My
        /// </summary>
        /// <param name="forces"></param>
        /// <returns>New FailureDomain2d</returns>
        /// <remarks>Only Mx and My of <paramref name="forces"/> are used</remarks>
        internal virtual FailureDomain2d CalculateFailureDomainCostantAngle(ForceTuple forces)
		{
            if (_analysisType == SectionSolver.FailureDomainAnalysisTypes.Plastic)
                return CalculatePlasticDomainCostantAngle(forces, _failureSectionSubdivision);
            else
                return CalculateElasticDomainCostantAngle(forces, _failureSectionSubdivision);
        }

        protected virtual FailureDomain2d CalculatePlasticDomainCostantAngle(ForceTuple forces, int subdivision = 20)
        {
            if (subdivision <= 2)
                throw new Exception();

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[2 * subdivision + 2];

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                points[i] = _sectionSolver.CalculatePlasticDomainPoint(forceTuples[i]);
            });

            return new FailureDomain2d(points);
        }

        protected virtual FailureDomain2d CalculateElasticDomainCostantAngle(ForceTuple forces, int subdivision = 20)
        {
            if (subdivision <= 2)
                throw new Exception();

            FailureDomain.FailureDomainPoint[] points = new FailureDomain.FailureDomainPoint[2 * subdivision + 2];

            ForceTuple[] forceTuples = CalculateRadialForces(forces, subdivision);

            Parallel.For(0, forceTuples.Length, (i) =>
            {
                points[i] = _sectionSolver.CalculateElasticDomainPoint(forceTuples[i]);
            });

            return new FailureDomain2d(points);
        }

        protected ForceTuple[] CalculateRadialForces(ForceTuple forces, int subdivision = 20)
		{
            ForceTuple[] forceTuples = new ForceTuple[2 * subdivision + 2];

            double nMax = ConcreteSection.AreaRebars * ConcreteSection.Rebars.FirstOrDefault().RebarMaterial.Fyk / 2.0;
            double nMin = ConcreteSection.Area * ConcreteSection.ConcreteMaterial.StressStrainTableCompression.GetMinimumStress() / 2.0;


            for (int i = 0; i <= subdivision / 2.0; i++)
            {
                forceTuples[i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.Mx, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, forces.My, i));

                forceTuples[subdivision / 2 + i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.Mx, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, forces.My, 0.0, i));

                forceTuples[2 * subdivision + 1 - i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMax, nMin / 2.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.Mx, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, 0.0, -forces.My, i));

                forceTuples[subdivision / 2 + subdivision + 1 - i] = new ForceTuple(Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, nMin / 2.0, nMin, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.Mx, 0.0, i),
                    Utilities.Maths.Interpolation.GetLinearInterpolation(0, subdivision / 2.0, -forces.My, 0.0, i));
            }
            return forceTuples;
        }

        #endregion

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

        public List<string> GetLog()
		{
            return _sectionSolver.GetLog();
		}
    }
}