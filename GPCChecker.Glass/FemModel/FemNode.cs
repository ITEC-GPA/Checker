using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.FEM.Attributes;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemNode : MeshVertex
    {
        private Restrain _restrain;

        private int _globalId;

        private List<INodeFemAttribute> _attributes;

        public int GlobalId { get { return _globalId; } set { _globalId = value; } }

        public Restrain Restrain => _restrain;

        public List<INodeFemAttribute> Attributes { get { return _attributes; } set { _attributes = value; } }



        public FemNode(MeshVertex vertex, Restrain restrain, List<INodeFemAttribute> attributes)
            : base(vertex)
        {
            _restrain = restrain;
            _attributes = attributes ?? new List<INodeFemAttribute>() ;
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
