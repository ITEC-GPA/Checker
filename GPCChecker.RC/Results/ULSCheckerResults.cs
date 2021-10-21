using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	public class ULSCheckerResults : CheckerResultType
	{
		protected readonly FailureDomain _failureDomain;
		protected List<ResultType> _forces;
		protected double _workingRatio;

		//TODO: aggiungere tassi di lavoro necessari


		public List<ResultType> Forces => _forces;

		public FailureDomain Domain => _failureDomain;


		public ULSCheckerResults(IConcreteSection section, FailureDomain failureDomain, List<ResultType> forces, int id = IDUNASSIGNED)
			:base(section, id)
		{
			_failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));

			foreach (ResultType resultType in forces)
				if (resultType.GetType() != typeof(ResultBeamForces) && resultType.GetType() != typeof(ResultPlateForces))
					throw new ArgumentException("Result must be ResultBeamForces or ResultPlateForces");

			_forces = forces ?? throw new ArgumentNullException(nameof(forces));
		}

		public void AddForces(ResultType forces)
		{
			_forces.Add(forces);
		}		
	}
}