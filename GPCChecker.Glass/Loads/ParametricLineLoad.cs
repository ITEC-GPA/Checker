using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Loads
{
    public class ParametricLineLoad : ParametricLoad
    {


        public ParametricLineLoad(LoadCase loadCase) 
            : base(loadCase)
        {
            throw new NotImplementedException();
        }

        public ParametricLineLoad(LoadCase loadCase, Guid guid) 
            : base(loadCase, guid)
        {

        }

        public ParametricLineLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }
    }
}
