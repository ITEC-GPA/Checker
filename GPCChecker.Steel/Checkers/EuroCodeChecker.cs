using GPC.Model.Standards;

namespace GPC.Checkers.Steel.Checkers
{
	public abstract class EuroCodeChecker : BeamChecker
	{
		public EuroCodeChecker(BeamCheckerAttributes attributes, Options options, Standard standard, int id = IDUNASSIGNED, string name = "")
			: base(attributes, options, standard, id, name)
		{

		}

		public override void PerformCheck()
		{

		}
	}
}
