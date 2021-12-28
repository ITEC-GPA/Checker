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
    public class NormalAreaLoad : Model.Loads.NormalAreaLoad, IGlassLoad
    {

        private readonly GlassPanelWrapper.GlassPanelPositions _glassPanelPositions;


        public GlassPanelWrapper.GlassPanelPositions GlassPanelPosition => _glassPanelPositions;
        public IGlassLoadCase GlassLoadCase => (IGlassLoadCase)base.LoadCase;


        public NormalAreaLoad(double pressure, Shape shape, IGlassLoadCase loadCase, 
                              GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External)
            : base(Convert.ToDouble(pressure), shape, (LoadCaseBase)loadCase)
        {
            _glassPanelPositions = glassPanelPosition;
        }

        internal NormalAreaLoad(double pressure, Shape shape, IGlassLoadCase loadCase,
                                string name,
                                GlassPanelWrapper.GlassPanelPositions glassPanelPosition = GlassPanelWrapper.GlassPanelPositions.External)
                : this(pressure, shape, loadCase, glassPanelPosition)
        {
            _name = name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return obj is NormalAreaLoad load &&
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


        public static bool operator ==(NormalAreaLoad obj1, NormalAreaLoad obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(NormalAreaLoad obj1, NormalAreaLoad obj2)
        {
            return !(obj1 == obj2);
        }

    }
}
