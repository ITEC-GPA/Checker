
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Glasses;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    internal class DoubleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new DoubleInsulatingGlass Glass => (DoubleInsulatingGlass)_glass;

        protected GlassPanelWrapper _innerGlassPanelWrapper;
        protected GlassPanelWrapper _outerGlassPanelWrapper;


        internal DoubleInsulatingGlassWrapper(GlassSurface glassSurface, DoubleInsulatingGlass glass) 
            : base(glassSurface, glass)

        {
            SetUpWrappers();


        }

        protected override void SetUpWrappers()
        {

            if (Glass.GlassPanelInner is MonolithicGlass)
            {
                _innerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)Glass.GlassPanelInner);

            }
            else if (Glass.GlassPanelInner is LaminatedGlass)
            {
                _innerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)Glass.GlassPanelInner);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (Glass.GlassPanelOuter is MonolithicGlass)
            {
                _outerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)Glass.GlassPanelInner);

            }
            else if (Glass.GlassPanelOuter is LaminatedGlass)
            {
                _outerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)Glass.GlassPanelInner);
            }
            else
            {
                throw new NotSupportedException();
            }
        }


        public override void GenerateMesh()
        {
            List<Mesh> meshes = new List<Mesh>();

            _innerGlassPanelWrapper.GenerateMesh();
            _outerGlassPanelWrapper.GenerateMesh();

            if (_innerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
                throw new ArgumentException();

            if (_outerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
                throw new ArgumentException();

            meshes.AddRange(_innerGlassPanelWrapper.Meshes);
            meshes.AddRange(_outerGlassPanelWrapper.Meshes);

            this._meshes = meshes;
        }
    }
}
