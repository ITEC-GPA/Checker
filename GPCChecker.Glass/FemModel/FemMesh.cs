using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model.Elements;
using GPC.Model.FEM.Attributes;
using GPC.Model.Elements.Glasses;
using GPC.Geometry.Meshes;
using GPC.Geometry;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemMesh 
    {
        private List<Node> _nodes;

        private List<Plate> _plates;

        private int _surfaceId;


        public List<Node> Nodes => _nodes;

        public List<Plate> Plates => _plates;

        public int SurfaceId => _surfaceId;


        /// <summary>
        /// Per fem mesh si intende un insieme di plates collegati tra loro. Es. un vetro monolitico. Un vetro laminato modellato a multielement è composto da due mesh
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="faces"></param>
        /// <param name="properties">List of properties associated to faces</param>
        /// <param name="pointRestrainVertexIndex"></param>
        /// <param name="nodeAttributeIndex"></param>
        /// <param name="plateAttributeIndex"></param>
        public FemMesh(int surfaceID, List<MeshVertex> vertices, List<MeshFace> faces, List<IFemGlassProperty> properties, Dictionary<int, Restrain> pointRestrainVertexIndex, 
                                                  Dictionary<INodeFemAttribute, int[]> nodeAttributeIndex, Dictionary<IPlateFemAttribute, int[]> plateAttributeIndex)
        {
            _surfaceId = surfaceID;
            _nodes = new List<Node>();
            _plates = new List<Plate>();

            if (faces.Count != properties.Count)
                throw new ArgumentException("Lenght of faces and properties list are different");

            foreach (var vertex in vertices)
            {
                if (pointRestrainVertexIndex.ContainsKey(vertex.Id))
                {
                    this._nodes.Add(new Node(vertex.Point, vertex.Id, pointRestrainVertexIndex[vertex.Id]));
                }
                else
                {
                    this._nodes.Add(new Node(vertex.Point, vertex.Id, null));
                }
            }

            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i].IsQuad)
                    this._plates.Add(new Plate(properties[i], faces[i].Id, _nodes.Where(j => j.NodeIndex == faces[i].A).First(), _nodes.Where(j => j.NodeIndex == faces[i].B).First(),
                                                     _nodes.Where(j => j.NodeIndex == faces[i].C).First(), _nodes.Where(j => j.NodeIndex == faces[i].D).First()));
                else
                    this._plates.Add(new Plate(properties[i], faces[i].Id, _nodes.Where(j => j.NodeIndex == faces[i].A).First(), _nodes.Where(j => j.NodeIndex == faces[i].B).First(),
                                                     _nodes.Where(j => j.NodeIndex == faces[i].C).First()));
            }


            foreach (IPlateFemAttribute att in plateAttributeIndex.Keys)
            {
                foreach (int id in plateAttributeIndex[att])
                {
                    this._plates.Where(i => i.Index == id).FirstOrDefault().AddAttribute(att);
                }
            }

            foreach (INodeFemAttribute att in nodeAttributeIndex.Keys)
            {
                foreach (int id in nodeAttributeIndex[att])
                {
                    this._nodes.Where(i => i.NodeIndex == id).FirstOrDefault().AddAttribute(att);
                }
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="nodeId">Nodes globalId will be setted starting from next int</param>
        /// <param name="plateId">Plate globalId will be setted starting from next int</param>
        public void SetGlobalIds(ref int nodeId, ref int plateId)
        {
            foreach(var node in _nodes)
            {
                node.GlobalId = nodeId++;
            }
            foreach (var face in _plates)
            {
                face.GlobalId = plateId++;
            }
        }

    }
}
