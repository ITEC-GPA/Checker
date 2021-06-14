using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Glasses.Loads
{
    /// <summary>
    /// ParametricPointLoad is a load defined with parametric coordinates 
    /// </summary>
    public class ParametricPointLoad : Model.Loads.PointLoad, IParametricLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;

        public ParametricPointLoad(Vector3d force, Vector3d moment, Point3d point, LoadCase loadCase, CoordinateSystem cSys, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(force, moment, point, loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }


        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, CoordinateSystem coordinateSystem, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricPointLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

    }
}
