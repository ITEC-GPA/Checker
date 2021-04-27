using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Results;
using GPC.Model.Combinations;

namespace GPC.Checkers.Glasses.Checkers
{
    public class AstmChecker : Checker
    {


        public AstmChecker(GlassSurface glassSurface, List<Combination> combinations, ModelOptions options)
            : base(glassSurface, combinations, options)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.ASTME1300)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.ASTME1300} {GetCheckerName()} Checker");

        }

        public AstmChecker(GlassSurface glassSurface, ModelOptions options)
            : this(glassSurface, null, options)
        {

        }


        protected override string GetCheckerName() => "ASTM E1300 - 16";

    }
}
