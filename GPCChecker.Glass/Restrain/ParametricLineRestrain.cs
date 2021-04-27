using GPC.Geometry;
using GPC.Model.FreedomCases;
using GPC.Model.Restrains;
using GPC.Utilities.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Restrain
{
    /// <summary>
    /// ParametricLineLoad is a load defined with parametric coordinates 
    /// </summary>
    [UI(Description = "Line restrain", Group = "Parametric restraints", Kind = "Parametric restrain")]
    public class ParametricLineRestrain : LineRestrain, IParametricRestrain
    {

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, List<DofRestrain> restrains) 
            : base(line, freedomCase, restrains)
        {
        }

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, CoordinateSystem coordinateSystem, List<DofRestrain> restrains) 
            : base(line, freedomCase, coordinateSystem, restrains)
        {
        }

        public ParametricLineRestrain(Line3d line, FreedomCase freedomCase, List<DofRestrain> restrains, string name)
            : base(line, freedomCase, CoordinateSystem.Global, restrains, Guid.NewGuid(), name)
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
