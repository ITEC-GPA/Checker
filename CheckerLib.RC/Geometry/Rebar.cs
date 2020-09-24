using CheckerLib.RC.Materials;
using Utilities.Geometry;

namespace CheckerLib.RC.Geometry
{
    public class Rebar
    {
        public readonly double Diameter;
        public readonly double EffectiveArea;
        public readonly Point2d Position;
        public readonly RebarSteel RebarSteel;

        /// <summary>
        /// </summary>
        /// <param name="diameter">rebar diameter [mm]</param>
        /// <param name="effectiveAreaMm">Area to be used for calculations [mm2]</param>
        /// <param name="position"></param>
        /// <param name="rebarSteel"></param>
        internal Rebar(double diameter, double effectiveAreaMm, Point2d position, RebarSteel rebarSteel)
        {
            Diameter = diameter;
            EffectiveArea = effectiveAreaMm;
            Position = new Point2d(position);
            RebarSteel = rebarSteel;
        }
    }
}