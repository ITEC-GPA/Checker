using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Loads;
using GPC.Checkers.Glasses.LoadCases;


namespace GPC.Checkers.Glasses.Loads
{
    /// <summary>
    /// Compare two <see cref="Load"/> using the load duration, temperature and load geometry as comparer
    /// </summary>
    [Serializable]
    public class LoadDurationTemperatureAndGeometryEqualityComparer : IEqualityComparer<Load>
    {

        /// <returns>
        /// <para>True if both <paramref name="x"/> and <paramref name="y"/> are null</para>
        /// <para>True if both geometries are equals</para>
        /// <para>True if both loadduration and temperature are equals</para>
        /// </returns>
        /// <remarks> Only <see cref="Load"/> is used as equality parameter </remarks>
        bool IEqualityComparer<Load>.Equals(Load x, Load y)
        {
            if (!(x.LoadCase is LoadCase) || !(y.LoadCase is LoadCase))
                throw new ArgumentException();


            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            if (x.GetType() == y.GetType()) // controllo che il tipo di carico sia lo stesso
            {
                if (x.GetGeometryBase() == y.GetGeometryBase()) // controllo che la geometria del carico sia la stessa
                {
                    if (((LoadCase)x.LoadCase).LoadDuration.Equals(((LoadCase)y.LoadCase).LoadDuration) // controllo che load duration e temperature siano uguali
                        && ((LoadCase)y.LoadCase).Temperature.Equals(((LoadCase)y.LoadCase).Temperature))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// <remarks> Only temperature, loaduration and geometry is used as equality parameter </remarks>
        int IEqualityComparer<Load>.GetHashCode(Load obj)
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + ((LoadCase)obj.LoadCase).LoadDuration.GetHashCode();
                hashCode = hashCode * -17 + ((LoadCase)obj.LoadCase).Temperature.GetHashCode();
                hashCode = hashCode * -17 + obj.GetGeometryBase().GetHashCode();
                return hashCode;
            }
        }

    }
}
