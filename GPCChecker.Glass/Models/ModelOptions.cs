using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
namespace GPC.Checker.Glasses.Models
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

        public ModelOptions()
        {
            GravityAxis = GravityAxes.Z;
            GravityPositiveAxis = false;
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
            int sign = GetGravitySign();
            return new Vector3d(sign * (GravityAxis == GravityAxes.X ? 1 : 0), sign * (GravityAxis == GravityAxes.Y ? 1 : 0), sign * (GravityAxis == GravityAxes.Z ? 1 : 0));
        }
    }
}
