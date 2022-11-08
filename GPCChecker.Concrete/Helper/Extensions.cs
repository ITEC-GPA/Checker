using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Helper
{
	internal static class Extensions
	{
		public static ForceTuple ConvertToForceTuple(this ResultBeamForces resultBeamForces, CoordinateSystem coordinateSystem)
		{
			if (resultBeamForces.CoordinateSystem.Equals(coordinateSystem))
			{
				return new ForceTuple(resultBeamForces.N, resultBeamForces.M1, resultBeamForces.M2);
			}
			else if (resultBeamForces.CoordinateSystem.Origin.Equals(coordinateSystem.Origin))
			{
				//TODO: workaround per correggere errore dentro metodo ToCoordinateSystem da debuggare. 
				resultBeamForces = new ResultBeamForces(resultBeamForces.N, resultBeamForces.V1, resultBeamForces.V2, resultBeamForces.T,
					resultBeamForces.M1, -resultBeamForces.M2, resultBeamForces.CoordinateSystem, resultBeamForces.Id);
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

		public static ForceTuple ConvertToForceTuple(this ResultBeamForces resultBeamForces, IConcreteSection section)
		{
			CoordinateSystem sectionCS = new CoordinateSystem(section.Centroid, Vector2d.XAxis, Vector2d.YAxis);

			Vector3d eccentricity = resultBeamForces.CoordinateSystem.Origin.VectorTo(sectionCS.Origin);
			ResultBeamForces forcesConverted = resultBeamForces.ToCoordinateSystem(sectionCS);

			return new ForceTuple(forcesConverted.N, forcesConverted.M1 + forcesConverted.N * eccentricity.Y,
				forcesConverted.M2 - forcesConverted.N * eccentricity.X);
		}
	}
}
