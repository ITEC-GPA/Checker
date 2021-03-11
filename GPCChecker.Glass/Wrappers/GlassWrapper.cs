using System;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Restrains;
using GPC.Model.Loads;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassWrapper
    {
        protected GlassSurface _glassSurface;
        protected Glass _glass;

        protected List<Mesh> _meshes;

        protected Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> _meshGeometryRestrainVertices;
        protected Dictionary<Mesh, Dictionary<Load, int[]>> _meshLoadsVertexIndexes;
        protected Dictionary<Mesh, Dictionary<Load, int[]>> _meshLoadsFaceIndexes;

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
                    return _meshes;
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
                    return _meshGeometryRestrainVertices;
            }
        }
        
        public Dictionary<Mesh, Dictionary<Load, int[]>> MeshLoadsVertexIndexes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    SetUpMeshOptions();
                    GenerateMesh();
                    return _meshLoadsVertexIndexes;
                }
                else
                    return _meshLoadsVertexIndexes;
            }
        }

        public Dictionary<Mesh, Dictionary<Load, int[]>> MeshLoadsFaceIndexes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    SetUpMeshOptions();
                    GenerateMesh();
                    return _meshLoadsFaceIndexes;
                }
                else
                    return _meshLoadsFaceIndexes;
            }
        }


        protected GlassWrapper(GlassSurface glassSurface, Glass glass)
        {
            this._glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));
            this._glass = glass ?? throw new ArgumentNullException(nameof(glass));

            this._meshes = new List<Mesh>();
        }

        protected void SetUpMeshOptions()
        {
            Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.Delaunay;
            Mesh.GenerateMeshOptions.Recombine = false;
            Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
            Mesh.GenerateMeshOptions.Size = _glassSurface.Prototype.MeshSize;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;
            Mesh.GenerateMeshOptions.Transfinite = true;

            Mesh.GenerateMeshOptions.HealShapes = true;
            Mesh.GenerateMeshOptions.OptimizeIteration = 1;
            Mesh.GenerateMeshOptions.Optimize = true;
            Mesh.GenerateMeshOptions.Smoothing = 3;
        }

        public abstract void GenerateMesh();


        /// <summary>
        /// 
        /// </summary>
        /// <returns>The normal vector unitized</returns>
        public Vector3d GetNormalVector() => _glassSurface.Shape.GetNormalVector();
    }
}
