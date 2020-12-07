using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Checkers
{
    public class AstmGlassChecker : GlassChecker
    {
        public AstmGlassChecker(Model model, CheckParameters checkParameters) : base(model, checkParameters)
        {

        }

        public override void SetUpFemModels()
        {
            throw new NotImplementedException();
        }

        protected override string GetCheckerName()
        {
            return "ASTM E1300 - 16";
        }
    }
}
