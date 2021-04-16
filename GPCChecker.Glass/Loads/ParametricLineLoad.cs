using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.LoadCases;


namespace GPC.Checker.Glasses.Loads
{
    /// <summary>
    /// ParametricPointRestrain is a point defined with parametric coordinates 
    /// </summary>
    public class ParametricLineLoad : GPC.Model.Loads.LineLoad, IParametricLoad
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="f1"></param>
        /// <param name="f2"></param>
        /// <param name="f3"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <param name="m3"></param>
        /// <param name="line">Line defined with parametric coordinates</param>
        /// <param name="loadCase"></param>
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCase loadCase) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="f1"></param>
        /// <param name="f2"></param>
        /// <param name="f3"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <param name="m3"></param>
        /// <param name="line">Line defined with parametric coordinates</param>
        /// <param name="loadCase"></param>
        /// <param name="coordinateSystem"></param>
        public ParametricLineLoad(double f1, double f2, double f3, double m1, double m2, double m3, Line3d line, LoadCase loadCase, CoordinateSystem coordinateSystem) 
            : base(f1, f2, f3, m1, m2, m3, line, loadCase, coordinateSystem)
        {

        }

        public ParametricLineLoad(Vector3d force, Vector3d moment, Line3d line, LoadCase loadCase, CoordinateSystem cSys)
            : base(force, moment, line, loadCase, cSys)
        {

        }

        public ParametricLineLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            
        }
    }
}
