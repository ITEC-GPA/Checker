using GPC.Model.Materials;

namespace GPC.Design.Walls;

/// <summary>Preliminary main reinforcement mass per metre of wall. The detailed take-off remains
/// WallReinforcementDetailing/WallBarScheduling; this is the existing undetailed-strip estimate.</summary>
public static class WallRebarQuantities
{
    // Model's canonical density is t/mm³. Keep the historical evaluation order of each estimate.
    internal const double SteelKgPerCubicMetre = Material.SteelDensity * 1e12;
    internal const double SteelKgPerSquareMmMetre = Material.SteelDensity * 1e6;

    public static double PrincipalSteelKgPerMetre(WallDetailInput input)
    {
        WallReinforcementDetailing.Validate(input);
        var g=input.Geometry;var r=input.Reinforcement;
        double lower=r.TwoZones?r.LowerHeight:g.Height,kg=0;
        foreach(var (zone,length) in new[]{(WallRebarZone.Stem,lower),(WallRebarZone.StemUpper,r.TwoZones?g.Height-lower:0),
            (WallRebarZone.Toe,g.Toe+g.StemBase/2),(WallRebarZone.Heel,g.Heel+g.StemBase/2)})
        {
            if(length==0) continue;
            if(!r.Zones.TryGetValue(zone,out var arm) || arm is null) throw new ArgumentException("Main reinforcement missing for "+zone);
            foreach(var face in new[]{arm.FirstFace,arm.OppositeFace})
            {
                ArgumentNullException.ThrowIfNull(face);
                WallReinforcementDetailing.Range(face.Count,2,30,nameof(face.Count));
                WallReinforcementDetailing.Range(face.Diameter,6,40,nameof(face.Diameter));
            }
            double other=arm.OppositeFace.Count*Math.Pow(arm.OppositeFace.Diameter,2);
            kg+=(arm.FirstFace.Count*Math.Pow(arm.FirstFace.Diameter,2)+other)*Math.PI/4*1e-6*length*SteelKgPerCubicMetre;
        }
        return kg;
    }
}
