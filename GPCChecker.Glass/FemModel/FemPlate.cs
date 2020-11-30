using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.FEM.Attributes;
using GPC.Geometry.Meshes;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemPlate : MeshFace
    {
        public int GlobalId { get; set; }

        private List<IPlateFemAttribute> _attributes;

        public List<IPlateFemAttribute> Attributes { get { return _attributes; } set { _attributes = value; } }


        public FemPlate(MeshFace face, List<IPlateFemAttribute> attributes) 
            : base(face)
        {
            _attributes = attributes ?? new List<IPlateFemAttribute>();
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
