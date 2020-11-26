using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Loads;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemNode : MeshVertex
    {
        private Restrain _restrain;
        private int _globalId;

        private GlobalPointLoad _globalPointLoad;


        public int GlobalId { get { return _globalId; } set { _globalId = value; } }



        public Restrain Restrain => _restrain;
        public GlobalPointLoad GlobalPointLoad { get { return _globalPointLoad; } set { _globalPointLoad = value; } }



        public FemNode(MeshVertex vertex, Restrain restrain, GlobalPointLoad globalPointLoad)
            : base(vertex)
        {
            _restrain = restrain;
            _globalPointLoad = globalPointLoad;
        }

        public FemNode(MeshVertex vertex)
            : this(vertex, null, null)
        {

        }
        
        public FemNode(int id, Point3d point) 
            : base(id, point)
        {

        }

        public FemNode(Point3d point)
            : base(point)
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
