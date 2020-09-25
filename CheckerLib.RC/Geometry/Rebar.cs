using CheckerLib.RC.Materials;
using Utilities.Geometry;

namespace CheckerLib.RC.Geometry
{
    public class Rebar
    {
        private readonly double _diameter;
        private readonly double _effectiveArea;
        private readonly Point2d _position;
        private readonly RebarSteel _rebarSteel;

        /// <summary>
        /// </summary>
        /// <param name="diameter">rebar diameter [mm]</param>
        /// <param name="effectiveAreaMm">Area to be used for calculations [mm2]</param>
        /// <param name="position"></param>
        /// <param name="rebarSteel"></param>
        internal Rebar(double diameter, double effectiveAreaMm, Point2d position, RebarSteel rebarSteel)
        {
            this._diameter = diameter;
            this._effectiveArea = effectiveAreaMm;
            this._position = new Point2d(position);
            this._rebarSteel = rebarSteel;
        }




    }
}