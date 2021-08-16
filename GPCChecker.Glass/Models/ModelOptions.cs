using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
namespace GPC.Checkers.Glasses.Models
{
    public class ModelOptions
    {
        public enum GravityAxes
        {
            X, 
            Y, 
            Z,
        }

        /// <summary>
        /// Rapresent the global axis where the gravity is acting
        /// </summary>
        public GravityAxes GravityAxis { get; set; }

        /// <summary>
        /// Rapresent the gravity direction. <see langword="False"/> means that gravity is directed in the opposite direction of axis: <see cref="GravityAxis"/>
        /// </summary>
        public bool GravityPositiveAxis { get; set; }

        /// <summary>
        /// Rapresent width of the line load strip used in the eq thickness analysis
        /// </summary>
        public double LineLoadWidthEqThickness { get; set; }

        /// <summary>
        /// Rapresent lenght of the point load square side used in the eq thickness analysis
        /// </summary>
        public double PointLoadWidthEqThickness { get; set; }


        public ModelOptions()
        {
            GravityAxis = GravityAxes.Z;
            GravityPositiveAxis = false;
            LineLoadWidthEqThickness = 20;
            PointLoadWidthEqThickness = 20;
        }

        /// <summary>
        /// Get the sign of the gravity
        /// </summary>
        /// <returns>-1 if <see cref="GravityPositiveAxis"/> is <see langword="False"/>. I.e. gravity is directed in the opposite direction of axis: <see cref="GravityAxis"/>. Otherwise 1 </returns>
        public int GetGravitySign()
        {
            return GravityPositiveAxis ? 1 : -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns> The gravity vector</returns>
        public Vector3d GetGravityVector()
        {
            return new Vector3d(GravityAxis == GravityAxes.X ? 1 : 0, 
                                GravityAxis == GravityAxes.Y ? 1 : 0, 
                                GravityAxis == GravityAxes.Z ? 1 : 0);
        }
    }
}
