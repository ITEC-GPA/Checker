using System.Collections.Generic;

namespace GPC.Checkers.Glasses.LoadCases
{
    public class LoadCaseDurationTemperatureRestrainEqualityComparer : IEqualityComparer<IGlassLoadCase>
    {
        public bool Equals(IGlassLoadCase x, IGlassLoadCase y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            if (x.GetType() == y.GetType()) // controllo che il tipo sia lo stesso
            {

                if (x.LoadDuration.Equals(y.LoadDuration) && x.Temperature.Equals(y.Temperature) && x.LoadRestrainCondition.Equals(y.LoadRestrainCondition))
                {
                    return true;
                }

            }

            return false;
        }

        public int GetHashCode(IGlassLoadCase obj)
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + obj.LoadDuration.GetHashCode();
                hashCode = hashCode * -17 + obj.Temperature.GetHashCode();
                hashCode = hashCode * -17 + obj.LoadRestrainCondition.GetHashCode();
                return hashCode;
            }
        }
    }
}
