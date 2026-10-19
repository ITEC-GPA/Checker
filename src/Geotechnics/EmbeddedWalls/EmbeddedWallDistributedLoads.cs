using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public static partial class EmbeddedWallAnalysis
{
    // Mesh boundaries include every interval end in every stage. Gauss stations are strictly
    // interior, so jumps at endpoints need no averaging or artificial nodal force.
    private static double IntervalLoad(EmbeddedWallStage stage, double elevation)
    {
        double load = 0;
        foreach (var q in stage.DistributedLoads)
            if (elevation < q.Top && elevation > q.Bottom)
                load += q.TopLoad + (q.BottomLoad - q.TopLoad) * (q.Top - elevation) / (q.Top - q.Bottom);
        return load;
    }
}
