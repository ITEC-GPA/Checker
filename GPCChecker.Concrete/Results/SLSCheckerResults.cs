using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections.Concrete;
using GPC.Geometry;
using GPC.Checkers.Concrete.ConcreteCheckerSolver;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
	public abstract class SLSCheckerResults : CheckerResultType
	{
		protected readonly ResultType _force;
		protected readonly StrainPlane _strainPlane;

		protected double _workingRatio;

		//TODO: aggiungere tassi di lavoro necessari

		public ResultType Force => _force;

		public StrainPlane StrainPlane => _strainPlane;


		public SLSCheckerResults(IConcreteSection section, ResultType force, StrainPlane strainPlane, Standard standard, int id = IDUNASSIGNED)
			: base(section, standard, id)
		{
			if (force.GetType() != typeof(ResultBeamForces) && force.GetType() != typeof(ResultPlateForces))
				throw new ArgumentException("Result must be ResultBeamForces or ResultPlateForces");

			_force = force ?? throw new ArgumentNullException(nameof(force));
			_strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
		}


		public virtual double GetConcreteTension(Point3d point)
		{
			throw new NotImplementedException();
		}

		public virtual double[] GetVerticesTension()
		{
			throw new NotImplementedException();
		}

		public double GetStrain(Point3d point)
		{
			return ConcreteSolverHelper.CalculateStrain(StrainPlane, point);
		}

		public double[] GetVerticesStrain()
		{
			List<Point3d> vertices = new List<Point3d>();

			vertices.AddRange(ConcreteSection.Shape.Fill);

			if (ConcreteSection.Shape.HasHoles)
				for (int i = 0; i < ConcreteSection.Shape.Holes.Count(); i++)
					vertices.AddRange(ConcreteSection.Shape.Holes[i]);

			double[] strains = new double[vertices.Count];

			for (int i = 0; i < strains.Length; i++)
				strains[i] = GetStrain(vertices[i]);

			return strains;
		}

		// TODO: implementare verifiche SLS (fessurazione)
	}
}
