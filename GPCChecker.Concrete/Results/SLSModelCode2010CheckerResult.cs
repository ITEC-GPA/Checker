using GPC.Checkers.Concrete.ConcreteCheckerSolver;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Concrete.Results
{
	public class SLSModelCode2010CheckerResult : SLSCheckerResults
	{
		public StandardModelCode2010 ModelCode2010 => (StandardModelCode2010)_standard;

		public SLSModelCode2010CheckerResult(IConcreteSection section, ResultType force, StrainPlane strainPlane, StandardModelCode2010 standard, int id = -1) 
			: base(section, force, strainPlane, standard, id)
		{
		}

		public override double GetConcreteTension(Point3d point)
		{
			double strain = GetStrain(point);
			double Fcd = SolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);
			return SolverHelper.CalculateSigmaC(strain, Fcd, ConcreteSection);
		}

		public override double[] GetVerticesTension()
		{
			double Fcd = SolverHelper.CalculateFcd(ConcreteSection, ModelCode2010);

			double[] strains = GetVerticesStrain();
			double[] tensions = new double[strains.Length];

			for (int i = 0; i < strains.Length; i++)
				tensions[i] = SolverHelper.CalculateSigmaC(strains[i], Fcd, ConcreteSection);

			return tensions;
		}
	}
}
