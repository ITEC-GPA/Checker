using GPC.Model.Elements.Glasses;
using GPC.Model.Combinations;
using System;
using System.Collections.Generic;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;

namespace GPC.Checker.Glasses
{
    public class Model 
    {
        #region Variables

        protected List<GlassSurface> _glassSurfaces;

        protected List<Combination> _combinations;

        #endregion

        #region Properties

        public List<GlassSurface> GlassSurfaces => _glassSurfaces;

        #endregion


        #region Public constructors

        public Model()
            : this(new List<GlassSurface>(), new List<Combination>())
        {

        }

        public Model(List<GlassSurface> glassSurfaces)
            : this(glassSurfaces, new List<Combination>())
        {

        }

        public Model(List<GlassSurface> glassSurfaces, List<Combination> combinations)
        {
            this._glassSurfaces = glassSurfaces ?? new List<GlassSurface>();
            this._combinations = combinations ?? new List<Combination>();
        }

        #endregion

        #region Public methods


        public void AddSurface(GlassSurface glassSurface)
        {
            _glassSurfaces.Add(glassSurface);
        }

        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        } 

        #endregion
    }
}
