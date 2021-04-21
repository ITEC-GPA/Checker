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

namespace GPC.Checker.Glasses.Restrain
{
    /// <summary>
    /// ParametricPointRestrain is a point defined with parametric coordinates 
    /// </summary>
    [UI(Description = "Point restrain", Group = "Parametric restraints", Kind = "Parametric restrain")]
    public class ParametricPointRestrain : PointRestrain, IParametricRestrain
    {


        public ParametricPointRestrain(Point3d point, FreedomCase freedomCase, List<DofRestrain> restrains) 
            : base(point, freedomCase, restrains)
        {
        }

        public ParametricPointRestrain(Point3d point, FreedomCase freedomCase, CoordinateSystem coordinateSystem, List<DofRestrain> restrains) 
            : base(point, freedomCase, coordinateSystem, restrains)
        {
        }

        public ParametricPointRestrain(Point3d point, FreedomCase freedomCase, List<DofRestrain> restrains, string name)
            : base(point, freedomCase, CoordinateSystem.Global, restrains, Guid.NewGuid(), name)
        {
        }

        public ParametricPointRestrain(Point3d point, FreedomCase freedomCase, CoordinateSystem coordinateSystem, List<DofRestrain> restrains, Guid guid, string name) 
            : base(point, freedomCase, coordinateSystem, restrains, guid, name)
        {
        }

        public ParametricPointRestrain(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
