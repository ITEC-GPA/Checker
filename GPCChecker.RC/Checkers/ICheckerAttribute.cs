using GPC.Model.Sections.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
	public interface ICheckerAttribute
	{
		IConcreteSection[] Sections { get; }
	}
}
