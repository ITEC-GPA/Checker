using GPC.Geometry;
using GPC.Model.FreedomCases;
using GPC.Model.Restrains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Restrain
{
    /// <summary>
    /// ParametricLineLoad is a load defined with parametric coordinates 
    /// </summary>
    public class ParametricLineRestrain : GPC.Model.Restrains.LineRestrain, IParametricRestrain
    {

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, List<DofRestrain> restrains) 
            : base(line, freedomCase, restrains)
        {

        }

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, CoordinateSystem coordinateSystem, List<DofRestrain> restrains) 
            : base(line, freedomCase, coordinateSystem, restrains)
        {

        }

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, CoordinateSystem coordinateSystem, List<DofRestrain> restrains, Guid guid, string name) 
            : base(line, freedomCase, coordinateSystem, restrains, guid, name)
        {

        }

        public ParametricLineRestrain(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

    }
}
