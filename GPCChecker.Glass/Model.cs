using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;

namespace GPC.Checker.Glasses
{
    public class Model 
    {
        protected List<GlassSurface> _glassSurfaces;

        public Model() 
        {

        }

        public Model(List<GlassSurface> glassSurfaces)
        {
            this._glassSurfaces = glassSurfaces;
        }


    }
}
