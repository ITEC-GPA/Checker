using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model;
using GPC.Geometry;

namespace GPC.Checker.Common.Geometry
{
    public abstract class Section
    {
        private readonly Shapes _shapes;
        protected Section(Shapes shapes) 
        {
            this._shapes = shapes;
        }
        internal Shapes Shapes => _shapes;
    }
}
