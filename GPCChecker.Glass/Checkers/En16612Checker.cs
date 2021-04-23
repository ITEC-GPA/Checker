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
using GPC.Checker.Glasses.Models;

namespace GPC.Checker.Glasses.Checkers
{
    public class En16612Checker : Checker
    {


        public En16612Checker(GlassSurface glassSurface, List<Combination> combinations, ModelOptions options)
            : base(glassSurface, combinations, options)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.EN16612)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.EN16612} {GetCheckerName()} Checker");

        }
        

        public En16612Checker(GlassSurface glassSurface, ModelOptions options)
            : this(glassSurface, null, options)
        {

        }


        protected override string GetCheckerName() => "EN 16612 - 2019";

    }
}
