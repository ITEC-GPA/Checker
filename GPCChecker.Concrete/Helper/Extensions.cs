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
        public static ForceTuple ConvertToForceTuple(this ResultBeamForces resultBeamForces, Point2d centroid)
        {
            if (centroid.Equals(resultBeamForces.CoordinateSystem.Origin))
            {
                var forcesConverted = resultBeamForces.ToCoordinateSystem(new CoordinateSystem(centroid, Vector3d.XAxis, Vector3d.YAxis));

                return new ForceTuple(forcesConverted.N, forcesConverted.M1, forcesConverted.M2);
            }
            else
            {
                Vector2d eccentricity = centroid.VectorTo(resultBeamForces.CoordinateSystem.Origin);

                var forcesConverted = resultBeamForces.ToCoordinateSystem(new CoordinateSystem(centroid, Vector3d.XAxis, Vector3d.YAxis));

                return new ForceTuple(forcesConverted.N, forcesConverted.M1 + forcesConverted.N * eccentricity.X, forcesConverted.M2 + +forcesConverted.N * eccentricity.Y);
            }
        }

        public static ForceTuple ConvertToForceTuple(this ResultBeamForces resultBeamForces, Vector2d distanceRefPointToCentroid)
        {
            if (distanceRefPointToCentroid == Vector2d.Zero)
            {
                return new ForceTuple(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2);
            }
            else
            {               
                return new ForceTuple(resultBeamForces.N, resultBeamForces.M1 - resultBeamForces.N * distanceRefPointToCentroid.Y, 
                    resultBeamForces.M2 + resultBeamForces.N * distanceRefPointToCentroid.X);
            }
        }

    }
}
