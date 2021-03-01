using System;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Checker.Glasses.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassWrapper
    {
        protected GlassSurface _glassSurface;
        protected Glass _glass;

        protected List<Mesh> _meshes;

        protected virtual Glass Glass => _glass;

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
