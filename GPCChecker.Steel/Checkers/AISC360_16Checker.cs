using GPC.Model.Standards;

namespace GPC.Checkers.Steel.Checkers
{
	public class AISC360_16Checker : AISCChecker
	{
		public AISC360_16Checker(BeamCheckerAttributes attributes, Options options, Standard standard, int id = IDUNASSIGNED, string name = "")
			: base(attributes, options, standard, id, name)
		{

		}
	}
}
