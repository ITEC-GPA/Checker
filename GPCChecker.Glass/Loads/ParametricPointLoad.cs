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
    /// ParametricPointLoad is a load defined with parametric coordinates 
    /// </summary>
    public class ParametricPointLoad : Model.Loads.PointLoad, IParametricLoad
    {
        public ParametricPointLoad(Vector3d force, Vector3d moment, Point3d point, LoadCase loadCase, CoordinateSystem cSys) 
            : base(force, moment, point, loadCase, cSys)
        {

        }

        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, string name = "") 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase, name)
        {

        }


        public ParametricPointLoad(double f1, double f2, double f3, double m1, double m2, double m3, Point3d point, LoadCase loadCase, CoordinateSystem coordinateSystem, string name = "") 
            : base(f1, f2, f3, m1, m2, m3, point, loadCase, coordinateSystem, name)
        {

        }

        public ParametricPointLoad(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

    }
}
