using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Wrappers;

namespace GPC.Checkers.Glasses.Loads
{
    public class AreaLoad : Model.Loads.AreaLoad, IGlassLoad
    {

        private GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;


        public AreaLoad(double p1, double p2, double p3, Shape shape, LoadCaseBase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPositions = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(p1, p2, p3, shape, loadCase)
        {
            _glassPanelPositions = glassPanelPositions;
        }


        public AreaLoad(double p1, double p2, double p3, Shape shape, LoadCaseBase loadCase, CoordinateSystem coordinateSystem, GlassPanelWrapper.GlassPanelPositions glassPanelPositions = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(p1, p2, p3, shape, loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPositions;
        }


    }
}
