using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.RC.ConcreteCheckerSolver
{
	public abstract class ConcreteSectionSolver
	{
		protected  IConcreteSection _concreteSection;

		public IConcreteSection ConcreteSection => _concreteSection;

		public ConcreteSectionSolver(IConcreteSection section)
		{
			_concreteSection = section;
		}
	}
}
