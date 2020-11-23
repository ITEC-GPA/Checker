using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemNode : MeshVertex
    {
        public int GlobalId { get; set; }

        public FemNode(Point3d point) 
            : base(point)
        {

        }

        public FemNode(MeshVertex vertex) 
            : base(vertex)
        {

        }

        public FemNode(int id, Point3d point) 
            : base(id, point)
        {

        }

        protected FemNode(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public override string ToString()
        {
            return base.ToString() + " GID: " + GlobalId;
        }
    }
}
