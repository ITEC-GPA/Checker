
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    internal class TripleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new TripleInsulatingGlass Glass => (TripleInsulatingGlass)_glass;

        protected GlassPanelWrapper _outerGlassPanelWrapper;
        protected GlassPanelWrapper _centerGlassPanelWrapper;
        protected GlassPanelWrapper _innerGlassPanelWrapper;



        internal TripleInsulatingGlassWrapper(GlassSurface glassSurface, TripleInsulatingGlass glass) 
            : base(glassSurface, glass)
        {

        }

        protected override void SetUpWrappers()
        {
            throw new NotImplementedException();
        }



        public override void GenerateMesh()
        {
            List<Mesh> meshes = new List<Mesh>();

            _innerGlassPanelWrapper.GenerateMesh();
            _centerGlassPanelWrapper.GenerateMesh();
            _outerGlassPanelWrapper.GenerateMesh();

            if (_innerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
                throw new ArgumentException();

            if (_centerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
                throw new ArgumentException();

            if (_outerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
                throw new ArgumentException();

            meshes.AddRange(_innerGlassPanelWrapper.Meshes);
            meshes.AddRange(_centerGlassPanelWrapper.Meshes);
            meshes.AddRange(_outerGlassPanelWrapper.Meshes);

            this._meshes = meshes;
        }
    }
}
