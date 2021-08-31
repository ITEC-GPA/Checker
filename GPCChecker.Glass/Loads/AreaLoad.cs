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
    public class AreaLoad : Model.Loads.AreaLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;

        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;


        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;


        public AreaLoad(double p1, double p2, double p3, Shape shape, IGlassLoadCase loadCase, 
                        GlassPanelWrapper.GlassPanelPositions glassPanelPositions = GlassPanelWrapper.GlassPanelPositions.External) 
            : base(p1, p2, p3, shape, (LoadCaseBase)loadCase)
        {
            _glassPanelPositions = glassPanelPositions;
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is AreaLoad load && _glassPanelPositions == load._glassPanelPositions &&
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


        public static bool operator ==(AreaLoad obj1, AreaLoad obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(AreaLoad obj1, AreaLoad obj2)
        {
            return !(obj1 == obj2);
        }
    }
}
