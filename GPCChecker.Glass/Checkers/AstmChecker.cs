using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Results;

namespace GPC.Checker.Glasses.Checkers
{
    public class AstmChecker : Checker
    {
        public AstmChecker(GlassSurface glassSurface)
            : base(glassSurface)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.ASTME1300)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.ASTME1300.ToString()} {GetCheckerName()} Checker");

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
