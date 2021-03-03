using System;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Restrains;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassWrapper
    {
        protected GlassSurface _glassSurface;
        protected Glass _glass;

        protected List<Mesh> _meshes;

        protected Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> _meshGeometryRestrainVertices;
        //protected Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> _meshGeometryRestrainVertices;

        protected virtual Glass Glass => _glass;

        /// <summary>
        /// If meshes has not been generated yet, it will call <see cref="GlassWrapper.SetUpMeshOptions()"/> and then <see cref="GlassWrapper.GenerateMesh()"/> />
        /// </summary>
        public List<Mesh> Meshes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    SetUpMeshOptions();
                    GenerateMesh();
                    return _meshes;
                }
                else
                {
                    return _meshes;
                }
            }
        }

        public Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> MeshGeometryRestrainVertices
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    SetUpMeshOptions();
                    GenerateMesh();
                    return _meshGeometryRestrainVertices;
                }
                else
                {
                    return _meshGeometryRestrainVertices;
                }
            }
        }


        protected GlassWrapper(GlassSurface glassSurface, Glass glass)
        {
            this._glassSurface = glassSurface;
            this._glass = glass;

            this._meshes = new List<Mesh>();
        }

        protected void SetUpMeshOptions()
        {
            Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
            Mesh.GenerateMeshOptions.Recombine = true;
            Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
            Mesh.GenerateMeshOptions.Size = _glassSurface.Prototype.MeshSize;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;
        }

        public abstract void GenerateMesh();


        /// <summary>
        /// 
        /// </summary>
        /// <returns>The normal vector unitized</returns>
        public Vector3d GetNormalVector() => _glassSurface.Shape.GetNormalVector();
    }
}
