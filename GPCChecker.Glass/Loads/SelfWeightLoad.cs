using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Loads
{
    public class SelfWeightLoad : GPC.Model.Loads.SelfWeightLoad
    {


        public SelfWeightLoad(LoadCase loadCase, Vector3d gravityVector, double acceleration) 
            : base(loadCase, gravityVector, acceleration)
        {

        }

    }
}
