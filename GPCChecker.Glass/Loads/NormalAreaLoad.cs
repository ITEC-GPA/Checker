using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Loads
{
    public class NormalAreaLoad : Model.Loads.NormalAreaLoad, IGlassLoad
    {

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;

        private GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;


        public NormalAreaLoad(double pressure, Shape shape, LoadCaseBase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(pressure, shape, loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }



    }
}
