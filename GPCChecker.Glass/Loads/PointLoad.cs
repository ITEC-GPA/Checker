using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
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
    public class PointLoad : Model.Loads.PointLoad, IGlassLoad
    {
        
        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;
        private readonly GlassSurface.LoadRestrainCondition _loadRestrainCondition;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;
        public GlassSurface.LoadRestrainCondition LoadRestrainCondition => _loadRestrainCondition;

        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;


        public PointLoad(Vector3d force, Vector3d moment, Point3d point, IGlassLoadCase loadCase, CoordinateSystem cSys, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(force, moment, point, (LoadCaseBase)loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
            _loadRestrainCondition = GlassSurface.LoadRestrainCondition.AsSurface;
        }

        public PointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, IGlassLoadCase loadCase, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : this(f1, f2, f3, m1, m2, m3, point, loadCase, CoordinateSystem.Global, glassPanelPosition)
        {

        }

        public PointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, IGlassLoadCase loadCase,
                        CoordinateSystem coordinateSystem, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, (LoadCaseBase)loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
            _loadRestrainCondition = GlassSurface.LoadRestrainCondition.AsSurface;
        }

    }
}
