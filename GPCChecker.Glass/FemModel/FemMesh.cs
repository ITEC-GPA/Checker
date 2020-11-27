using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.FEM.Attributes;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemMesh : Mesh
    {
        public FemMesh(List<MeshVertex> vertices, List<MeshFace> faces, Dictionary<int, Restrain> pointRestrainVertexIndex, Dictionary<INodeFemAttribute, int[]> pointLoadVertexIndex, 
            Dictionary<IPlateFemAttribute, int[]> plateLoadIndex)
        {
            foreach (var vertex in vertices)
            {
                if (pointRestrainVertexIndex.ContainsKey(vertex.Id))
                {
                    this._vertices.Add(new FemNode(vertex, pointRestrainVertexIndex[vertex.Id], null));
                }
                else
                {
                    this._vertices.Add(new FemNode(vertex));
                }
            }

            foreach (var face in faces)
            {
                this._faces.Add(new FemPlate(face, null));
            }

            foreach (IPlateFemAttribute load in plateLoadIndex.Keys)
            {
                foreach (int id in plateLoadIndex[load])
                {
                    (this._faces.Where(i => i.Id == id).FirstOrDefault() as FemPlate).Attributes.Add(load);
                }
            }

            foreach (INodeFemAttribute load in pointLoadVertexIndex.Keys)
            {
                foreach (int id in pointLoadVertexIndex[load])
                {
                    (this._vertices.Where(i => i.Id == id).FirstOrDefault() as FemNode).Attributes.Add(load);
                }
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

                if ((vertex as FemNode).Restrain != null)
                {
                    Console.WriteLine((vertex as FemNode).GlobalId + " " + vertex.Point.X.ToString() + " " + vertex.Point.Y.ToString() + " " + vertex.Point.Z.ToString());
                }
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
