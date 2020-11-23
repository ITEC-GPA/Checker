using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemMesh : Mesh
    {
        public FemMesh()
        {

        }

        public FemMesh(List<MeshVertex> vertices, List<MeshFace> faces)
        {
            foreach (var vertex in vertices)
            {
                this._vertices.Add(new FemNode(vertex));
            }

            foreach (var face in faces)
            {
                this._faces.Add(new FemPlate(face));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nodeId">Nodes globalId will be setted starting from next int</param>
        /// <param name="faceId">Faces globalId will be setted starting from next int</param>
        public void SetGlobalIds(ref int nodeId, ref int faceId)
        {
            foreach(var vertex in _vertices)
            {
                (vertex as FemNode).GlobalId = nodeId++;
            }
            foreach (var face in _faces)
            {
                (face as FemPlate).GlobalId = faceId++;
            }
        }

        protected FemMesh(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }
    }
}
