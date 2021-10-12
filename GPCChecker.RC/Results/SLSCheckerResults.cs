using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections.Concrete;
using GPC.Geometry;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	public class SLSCheckerResults
	{		
		protected List<ResultType> _forces;
		protected IConcreteSection _section;
		protected double _workingRatio;

		//TODO: aggiungere tassi di lavoro necessari

		public List<ResultType> Forces => _forces;

		public IConcreteSection Section => _section;

		public SLSCheckerResults(List<ResultType> forces)
		{
			foreach (ResultType resultType in forces)
				if (resultType.GetType() != typeof(ResultBeamForces) && resultType.GetType() != typeof(ResultPlateForces))
					throw new ArgumentException("Result must be ResultBeamForces or ResultPlateForces");

			_forces = forces ?? throw new ArgumentNullException(nameof(forces));
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
