using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemPlate : MeshFace
    {
        public int GlobalId { get; set; }

        public FemPlate(MeshFace face) 
            : base(face)
        {

        }

        public FemPlate(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public FemPlate(int id, int a, int b, int c) 
            : base(id, a, b, c)
        {

        }

        public FemPlate(int id, int a, int b, int c, int d) 
            : base(id, a, b, c, d)
        {

        }

    }
}
