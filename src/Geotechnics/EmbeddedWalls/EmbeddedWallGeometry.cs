using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public static partial class EmbeddedWallAnalysis
{
    /// <summary>Independent shorter wall, cut from a fully defined longest template. No soil/property extrapolation,
    /// support/load removal or artificial toe restraint. Soil profiles remain available below the new toe.
    /// Assigned pore-pressure profiles can only be retained with explicit confirmation that they are prescribed
    /// independently of wall geometry; a seepage solution must otherwise be recomputed for each candidate.</summary>
    public static EmbeddedWallModel WithToe(EmbeddedWallModel longestTemplate, double toeElevation,
        bool preserveIndependentPorePressures = false)
    {
        Validate(longestTemplate);
        Require(Finite(toeElevation) && toeElevation >= longestTemplate.Bottom && toeElevation < longestTemplate.Top,
            "The candidate toe must lie inside the fully defined longest wall.");
        Require(!longestTemplate.FixBottomDisplacement && !longestTemplate.FixBottomRotation,
            "Embedment search cannot move an assigned toe restraint.");
        Require(preserveIndependentPorePressures || longestTemplate.Stages.All(s => s.Left.PorePressures.Count == 0 && s.Right.PorePressures.Count == 0),
            "Recompute candidate-dependent pore pressures; preserving prescribed profiles needs explicit confirmation.");
        var result = Snapshot(longestTemplate);
        result.Bottom = toeElevation;
        result.Segments = result.Segments.Where(s => s.Top > toeElevation).ToArray();
        result.Segments[result.Segments.Count - 1].Bottom = toeElevation;
        Validate(result); // A support or action below the candidate toe is an input error, never silently discarded.
        return result;
    }
}
