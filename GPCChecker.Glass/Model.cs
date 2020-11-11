using GPC.Checker.Glasses.Wrappers;
using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public GlassWrapper GetWrappers()
        {
            throw new NotImplementedException();
        }
    }
}
