using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Glasses;
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
        private readonly GlassSurface.LoadRestrainCondition _loadRestrainCondition;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;
        public GlassSurface.LoadRestrainCondition LoadRestrainCondition => _loadRestrainCondition;


        public ParametricPointLoad(Vector3d force, Vector3d moment, Point3d point, LoadCase loadCase, CoordinateSystem cSys, 
                                    GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(force, moment, point, loadCase, cSys)
        {
            _loadRestrainCondition = GlassSurface.LoadRestrainCondition.AsSurface;
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, 
                                    GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase)
        {
            _loadRestrainCondition = GlassSurface.LoadRestrainCondition.AsSurface;
            _glassPanelPositions = glassPanelPosition;
        }


        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, 
                                CoordinateSystem coordinateSystem, 
                                GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase, coordinateSystem)
        {
            _loadRestrainCondition = GlassSurface.LoadRestrainCondition.AsSurface;
            _glassPanelPositions = glassPanelPosition;
        }

        public ParametricPointLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is ParametricPointLoad load &&
                   _glassPanelPositions == load._glassPanelPositions &&
                   _loadRestrainCondition == load._loadRestrainCondition &&
                   base.Equals(obj);
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _glassPanelPositions.GetHashCode();
                hashCode = hashCode * -17 + _loadRestrainCondition.GetHashCode();
                return hashCode;
            }
        }


        public static bool operator ==(ParametricPointLoad obj1, ParametricPointLoad obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(ParametricPointLoad obj1, ParametricPointLoad obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
