using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Loads
{
    public class SelfWeightLoad : Model.Loads.SelfWeightLoad, IGlassLoad
    {
        private GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;


        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition
        {
            get => _glassPanelPositions;
            internal set
            {
                _glassPanelPositions = value;
            }
        }

        public GlassSurface.LoadRestrainCondition LoadRestrainCondition => GlassSurface.LoadRestrainCondition.AsSurface;

        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;



        public SelfWeightLoad(IGlassLoadCase loadCase, Vector3d gravityVector, double acceleration) 
            : base((Model.LoadCases.LoadCase)loadCase, gravityVector, acceleration)
        {

        }


    }
}
