using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Elements;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemNode : MeshVertex
    {
        private Restrain _restrain;
        private int _globalId;


        public int GlobalId { get { return _globalId; } set { _globalId = value; } }

        public Restrain Restrain => _restrain;

        public FemNode(Point3d point) 
            : base(point)
        {

        }

        public FemNode(MeshVertex vertex) 
            : base(vertex)
        {

        }

        public FemNode(MeshVertex vertex, Restrain restrain)
            : this(vertex)
        {
            _restrain = restrain;
        }

        public FemNode(int id, Point3d point) 
            : base(id, point)
        {

        }

        protected FemNode(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public bool[] GetRestrains() => _restrain.GetRestrains();

        public double[] GetStiffnesses() => _restrain.GetStiffnesses();

        public override string ToString()
        {
            return base.ToString() + " GID: " + GlobalId;
        }
    }
}
