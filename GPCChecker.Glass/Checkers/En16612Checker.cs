using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;
using GPC.Model.Loads;
using GPC.Model.Elements;
using GPC.Model.FEM.Properties;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Model.Restrains;
using GPC.Checker.Glasses.Results;
using GPC.Model.Combinations;

namespace GPC.Checker.Glasses.Checkers
{
    public class En16612Checker : Checker
    {


        public En16612Checker(GlassSurface glassSurface, List<Combination> globalCombinations)
            : base(glassSurface, globalCombinations)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.EN16612)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.EN16612} {GetCheckerName()} Checker");

        }
        

        public En16612Checker(GlassSurface glassSurface)
            : this(glassSurface, null)
        {

        }


        protected override string GetCheckerName() => "EN 16612 - 2019";

    }
}
