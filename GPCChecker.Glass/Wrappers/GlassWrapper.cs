using System;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Checkers.Glasses.Glasses;
using GPC.Model.Restrains;
using GPC.Model.Loads;

namespace GPC.Checkers.Glasses.Wrappers
{
    public abstract class GlassWrapper : Model.ModelObject
    {
        protected GlassSurface _glassSurface;
        protected Glass _glass;

        protected virtual Glass Glass => _glass;



        protected GlassWrapper(GlassSurface glassSurface, Glass glass)
        {
            this._glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));
            this._glass = glass ?? throw new ArgumentNullException(nameof(glass));
        }

        /// <summary>
        /// Generate the mesh of the glass
        /// </summary>
        public abstract bool GenerateMesh();


        /// <summary>
        /// 
        /// </summary>
        /// <returns>The normal vector unitized</returns>
        public Vector3d GetNormalVector() => _glassSurface.Shape.GetNormalVector();
    }
}
