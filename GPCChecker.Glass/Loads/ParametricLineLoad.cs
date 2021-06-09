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
    /// ParametricPointRestrain is a point defined with parametric coordinates 
    /// </summary>
    public class ParametricLineLoad : GPC.Model.Loads.LineLoad, IParametricLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;



        /// <param name="f1"></param>
        /// <param name="f2"></param>
        /// <param name="f3"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <param name="m3"></param>
        /// <param name="line">Line defined with parametric coordinates</param>
        /// <param name="loadCase"></param>
        /// <param name="glassPanelPosition"></param>
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCase loadCase, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }


        /// <param name="f1"></param>
        /// <param name="f2"></param>
        /// <param name="f3"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <param name="m3"></param>
        /// <param name="line">Line defined with parametric coordinates</param>
        /// <param name="loadCase"></param>
        /// <param name="coordinateSystem"></param>
        /// <param name="glassPanelPosition"></param>
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCase loadCase, CoordinateSystem coordinateSystem, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricLineLoad(Vector3d force, Vector3d moment, Line3d line, LoadCase loadCase, CoordinateSystem cSys, GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External)
            : base(force, moment, line, loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricLineLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            
        }
    }
}
