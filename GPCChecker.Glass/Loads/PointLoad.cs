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


        public PointLoad(Vector3d force, Vector3d moment, Point3d point, LoadCaseBase loadCase, CoordinateSystem cSys, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(force, moment, point, loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public PointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCaseBase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public PointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCaseBase loadCase, CoordinateSystem coordinateSystem, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }

    }
}
