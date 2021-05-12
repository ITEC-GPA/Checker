
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Glasses;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;

namespace GPC.Checkers.Glasses.Wrappers
{
    internal class DoubleInsulatingGlassWrapper : InsulatedGlassWrapper
    {

        protected GlassPanelWrapper _innerGlassPanelWrapper;
        protected GlassPanelWrapper _outerGlassPanelWrapper;


        internal DoubleInsulatingGlassWrapper(GlassSurface glassSurface, DoubleInsulatingGlass glass) 
            : base(glassSurface, glass)

        {
            SetUpWrappers();


        }

        protected override void SetUpWrappers()
        {
            DoubleInsulatingGlass igu = Glass as DoubleInsulatingGlass;

            if (igu is null)
                throw new ArgumentException();

            if (igu.GlassPanelInner is MonolithicGlass)
            {
                _innerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)igu.GlassPanelInner, GlassPanelWrapper.GlassPanelPositions.Internal);

            }
            else if (igu.GlassPanelInner is LaminatedGlass)
            {
                _innerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)igu.GlassPanelInner, GlassPanelWrapper.GlassPanelPositions.Internal);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (igu.GlassPanelOuter is MonolithicGlass)
            {
                _outerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)igu.GlassPanelInner, GlassPanelWrapper.GlassPanelPositions.External);

            }
            else if (igu.GlassPanelOuter is LaminatedGlass)
            {
                _outerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)igu.GlassPanelInner, GlassPanelWrapper.GlassPanelPositions.External);
            }
            else
            {
                throw new NotSupportedException();
            }
        }


        public override bool GenerateMesh()
        {
            //List<Mesh> meshes = new List<Mesh>();

            //_innerGlassPanelWrapper.GenerateMesh();
            //_outerGlassPanelWrapper.GenerateMesh();

            //if (_innerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
            //    throw new ArgumentException();

            //if (_outerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
            //    throw new ArgumentException();

            //meshes.AddRange(_innerGlassPanelWrapper.Meshes);
            //meshes.AddRange(_outerGlassPanelWrapper.Meshes);

            //this._meshes = meshes;
            return false;
        }
    }
}
