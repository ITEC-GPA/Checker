using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Loads;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemMesh : Mesh
    {

        public FemMesh(List<MeshVertex> vertices, List<MeshFace> faces, Dictionary<int, Restrain> pointRestrainVertexIndex, Dictionary<Load, int[]> pointLoadVertexIndex)
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
                this._faces.Add(new FemPlate(face));
            }

            foreach(Load load in pointLoadVertexIndex.Keys)
            {
                if (load is GlobalPointLoad gpl)
                {
                    foreach (int id in pointLoadVertexIndex[load])
                    {
                        (this._vertices.Where(i => i.Id == id).FirstOrDefault() as FemNode).GlobalPointLoad = gpl;
                    }
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
