
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checkers.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Checkers.Glasses.Loads;

namespace GPC.Checkers.Glasses.Wrappers
{
    internal class TripleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
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



        public override bool GenerateMesh()
        {
            //List<Mesh> meshes = new List<Mesh>();

            //_innerGlassPanelWrapper.GenerateMesh();
            //_centerGlassPanelWrapper.GenerateMesh();
            //_outerGlassPanelWrapper.GenerateMesh();

            //if (_innerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
            //    throw new ArgumentException();

            //if (_centerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
            //    throw new ArgumentException();

            //if (_outerGlassPanelWrapper.Meshes.Select(i => i == null).Any())
            //    throw new ArgumentException();

            //meshes.AddRange(_innerGlassPanelWrapper.Meshes);
            //meshes.AddRange(_centerGlassPanelWrapper.Meshes);
            //meshes.AddRange(_outerGlassPanelWrapper.Meshes);

            //this._meshes = meshes;
            return false;
        }


        /// <inheritdoc cref="InsulatedGlassWrapper.GetLoadSharing(IEnumerable{IGlassLoad}, Models.Prototype.Standards, bool, double)"/>>
        /// <returns>
        /// An array of loads. 
        /// First index: External slab load. 
        /// Second index: Central slab load.
        /// Third index: Internal slab load.
        /// </returns>
        internal override List<NormalAreaLoad>[] GetLoadSharing(IEnumerable<IGlassLoad> loads, Models.Prototype.Standards standard, 
                                                                bool compressibleGas, double cavitySealingPressure = 0.1)
        {
            throw new NotImplementedException();
        }


        public override double GetMinimumElasticModulus()
        {
            return Math.Min(Math.Min(_outerGlassPanelWrapper.GetElasticModulus(), 
                                     _centerGlassPanelWrapper.GetElasticModulus()), _innerGlassPanelWrapper.GetElasticModulus());
        }

        public override double GetMinimumPoissonRatio()
        {
            return Math.Min(Math.Min(_outerGlassPanelWrapper.GetPoissonRatio(),
                                     _centerGlassPanelWrapper.GetPoissonRatio()), _innerGlassPanelWrapper.GetPoissonRatio());
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return _outerGlassPanelWrapper.GetSelfWeightPerUnitArea() + _centerGlassPanelWrapper.GetSelfWeightPerUnitArea() 
                                                                      + _innerGlassPanelWrapper.GetSelfWeightPerUnitArea();
        }

        public override double GetSelfWeightTotal()
        {
            return _outerGlassPanelWrapper.GetSelfWeightTotal() + _centerGlassPanelWrapper.GetSelfWeightTotal() 
                                                                + _innerGlassPanelWrapper.GetSelfWeightTotal();
        }

        public override double GetMaximumDensity()
        {
            return Math.Max(Math.Max(_outerGlassPanelWrapper.GetDensity(), _centerGlassPanelWrapper.GetDensity()),
                                     _innerGlassPanelWrapper.GetDensity());
        }
    }
}
