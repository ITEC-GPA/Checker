using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.RC.ConcreteCheckerSolver
{
	public class ConcreteSectionSolverEN1992 : ConcreteSectionSolver
	{
		protected StandardEN1992p11 _standard;

		public StandardEN1992p11 EN1992P11 => _standard;


		public ConcreteSectionSolverEN1992(IConcreteSection concreteSection, StandardEN1992p11 standard)
			:base(concreteSection)
		{
			_standard = standard;
		}


	}
}
