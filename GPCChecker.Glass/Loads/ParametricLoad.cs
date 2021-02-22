using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Loads
{
    public abstract class ParametricLoad : GPC.Model.Loads.Load
    {


        public ParametricLoad(LoadCase loadCase)
            : base(loadCase, Guid.NewGuid())
        {

        }

        public ParametricLoad(LoadCase loadCase, Guid guid) 
            : base(loadCase, guid)
        {

        }

        public ParametricLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public override GeometryBase GetGeometry()
        {
            throw new NotImplementedException();
        }
    }
}
