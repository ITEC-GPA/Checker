using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Model.LoadCases;


namespace GPC.Checkers.Glasses.Loads
{
    /// <summary>
    /// ParametricPointRestrain is a point defined with parametric coordinates 
    /// </summary>
    public class ParametricLineLoad : Model.Loads.LineLoad, IParametricLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;

        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;


        /// <param name="f1"></param>
        /// <param name="f2"></param>
        /// <param name="f3"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <param name="m3"></param>
        /// <param name="line">Line defined with parametric coordinates</param>
        /// <param name="loadCase"></param>
        /// <param name="glassPanelPosition"></param>
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, IGlassLoadCase loadCase, 
                                GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, (LoadCaseBase)loadCase)
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
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, IGlassLoadCase loadCase, 
                                CoordinateSystem coordinateSystem, 
                                GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, (LoadCaseBase)loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }


        public ParametricLineLoad(Vector3d force, Vector3d moment, Line3d line, IGlassLoadCase loadCase, 
                                CoordinateSystem cSys, 
                                GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External)
            : base(force, moment, line, (LoadCaseBase)loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        
        public ParametricLineLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is ParametricLineLoad load &&
                   _glassPanelPositions == load._glassPanelPositions &&
                   base.Equals(obj);
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _glassPanelPositions.GetHashCode();
                return hashCode;
            }
        }


        public static bool operator ==(ParametricLineLoad obj1, ParametricLineLoad obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(ParametricLineLoad obj1, ParametricLineLoad obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
