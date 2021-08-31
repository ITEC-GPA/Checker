
using GPC.Geometry;
using GPC.Model.LoadCases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;

namespace GPC.Checkers.Glasses.Loads
{
    public class LineLoad : Model.Loads.LineLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;


        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;

        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;


        /// <inheritdoc cref="GPC.Model.Loads.LineLoad.LineLoad(Vector3d, Vector3d, Line3d, LoadCaseBase, CoordinateSystem)"/>
        public LineLoad(Vector3d force, Vector3d moment, Line3d line, IGlassLoadCase loadCase, CoordinateSystem cSys, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External ) 
            : base(force, moment, line, (LoadCaseBase)loadCase, cSys)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        /// <inheritdoc cref="GPC.Model.Loads.LineLoad.LineLoad(double, double, double, double, double, double, Line3d, LoadCaseBase)"/>
        public LineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, IGlassLoadCase loadCase,
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, (LoadCaseBase)loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }


        /// <inheritdoc cref="GPC.Model.Loads.LineLoad.LineLoad(double, double, double, double, double, double, Line3d, LoadCaseBase, CoordinateSystem)"/>
        public LineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, IGlassLoadCase loadCase, CoordinateSystem coordinateSystem, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, line, (LoadCaseBase)loadCase, coordinateSystem)
        {
            _glassPanelPositions = glassPanelPosition;
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is LineLoad load &&
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


        public static bool operator ==(LineLoad obj1, LineLoad obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(LineLoad obj1, LineLoad obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
