
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
    public class LineLoad : GPC.Model.Loads.LineLoad, IGlassLoad
    {

        private GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;


        public LineLoad(Vector3d force, Vector3d moment, Line3d line, LoadCaseBase loadCase, CoordinateSystem cSys, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(force, moment, line, loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public LineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCaseBase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public LineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCaseBase loadCase, CoordinateSystem coordinateSystem, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }


    }
}
