using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections.Concrete;
using GPC.Geometry;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Model.Standards;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	public class SLSCheckerResults : CheckerResultType
	{
		protected readonly ResultType _force;
		protected readonly IStrainPlane _strainPlane;
		protected readonly Standard _standard;
		protected double _workingRatio;

		//TODO: aggiungere tassi di lavoro necessari

		public ResultType Force => _force;

		public IStrainPlane StrainPlane => _strainPlane;

		public Standard Standard => _standard;


		public SLSCheckerResults(IConcreteSection section, ResultType force, IStrainPlane strainPlane, Standard standard, int id = IDUNASSIGNED)
			: base(section, id)
		{
			if (force.GetType() != typeof(ResultBeamForces) && force.GetType() != typeof(ResultPlateForces))
				throw new ArgumentException("Result must be ResultBeamForces or ResultPlateForces");

			_force = force ?? throw new ArgumentNullException(nameof(force));
			_strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
			_standard = standard ?? throw new ArgumentNullException(nameof(standard));
		}


		protected double GetTension(Point2d point)
		{
			throw new NotImplementedException();
		}

		protected double[] GetVerticesTension()
		{
			throw new NotImplementedException();
		}

		protected double GetStrain(Point2d point)
		{
			throw new NotImplementedException();
		}

		protected double[] GetVerticesStrain()
		{
			throw new NotImplementedException();
		}

		// TODO: implementare verifiche SLS (fessurazione)
	}
}
