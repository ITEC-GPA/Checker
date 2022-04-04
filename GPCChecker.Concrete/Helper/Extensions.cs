using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Model.Results;

namespace GPC.Checkers.Concrete.Helper
{
    internal static class Extensions
    {
        public static ForceTuple ConvertToForceTuple(this ResultBeamForces resultBeamForces, CoordinateSystem coordinateSystem)
        {
            if(resultBeamForces.CoordinateSystem.Equals(coordinateSystem))
            {
                return new ForceTuple(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2);
            }
            else if (resultBeamForces.CoordinateSystem.Origin.Equals(coordinateSystem.Origin))
            {
                ResultBeamForces forcesConverted = resultBeamForces.ToCoordinateSystem(coordinateSystem);
                return new ForceTuple(forcesConverted.N, forcesConverted.M1, forcesConverted.M2);
            }
            else
            {
                Vector3d eccentricity = resultBeamForces.CoordinateSystem.Origin.VectorTo(coordinateSystem.Origin);
				ResultBeamForces forcesConverted = resultBeamForces.ToCoordinateSystem(coordinateSystem);

                return new ForceTuple(forcesConverted.N, forcesConverted.M1 + forcesConverted.N * eccentricity.Y, 
                    forcesConverted.M2 - forcesConverted.N * eccentricity.X);
            }
        }
    }
}
