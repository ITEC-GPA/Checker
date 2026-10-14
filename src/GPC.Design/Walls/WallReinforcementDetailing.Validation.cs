namespace GPC.Design.Walls;

public static partial class WallReinforcementDetailing
{
    internal static void Validate(WallDetailInput input)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Geometry);
        ArgumentNullException.ThrowIfNull(input.Reinforcement); ArgumentNullException.ThrowIfNull(input.Material);
        ArgumentNullException.ThrowIfNull(input.Options); ArgumentNullException.ThrowIfNull(input.Options.CoverSpecification);
        var g=input.Geometry; var r=input.Reinforcement; var m=input.Material; var o=input.Options;
        Range(g.Height,.2,15,nameof(g.Height)); Range(g.Slab,.15,4,nameof(g.Slab));
        Range(g.StemBase,.15,8,nameof(g.StemBase)); Range(g.StemTop,.15,g.StemBase,nameof(g.StemTop));
        Range(g.Toe,0,12,nameof(g.Toe)); Range(g.Heel,0,12,nameof(g.Heel)); Range(input.Cover,15,150,nameof(input.Cover));
        if(r.TwoZones && (!double.IsFinite(r.LowerHeight) || r.LowerHeight<=0 || r.LowerHeight>=g.Height))
            throw new ArgumentException("Lower zone height must lie strictly between zero and wall height.");
        foreach(double strength in new[]{m.Fcd,m.Fyd,m.Fctk05,m.GammaC})
            if(!double.IsFinite(strength) || strength<=0) throw new ArgumentException("Positive finite material strengths and coefficients required.");
        Range(o.Aggregate,4,63,nameof(o.Aggregate)); Range(o.LapClearDistance,0,100,nameof(o.LapClearDistance)); Range(o.LapPercent,1,100,nameof(o.LapPercent));
        if(o.CoverSpecification.DesignLife is not (50 or 100)) throw new ArgumentException("Cover design life must be 50 or 100 years.");
        if(r.TwoZones) { Range(o.TieDiameter,6,32,nameof(o.TieDiameter)); Range(o.TieSpacing,50,400,nameof(o.TieSpacing)); }
        foreach(var zone in Enum.GetValues<WallRebarZone>())
        {
            if(zone==WallRebarZone.StemUpper && !r.TwoZones || zone==WallRebarZone.Toe && g.Toe==0 || zone==WallRebarZone.Heel && g.Heel==0) continue;
            if(!r.Zones.TryGetValue(zone,out var bars) || bars is null || bars.FirstFace is null || bars.OppositeFace is null)
                throw new ArgumentException("Reinforcement is missing for "+zone);
            foreach(var face in new[]{bars.FirstFace,bars.OppositeFace})
            { Range(face.Diameter,6,40,nameof(face.Diameter)); Range(face.Count,2,30,nameof(face.Count)); }
            Range(bars.SecondaryDiameter,6,32,nameof(bars.SecondaryDiameter)); Range(bars.SecondarySpacing,50,500,nameof(bars.SecondarySpacing));
            Range(bars.AnchorLength,0,10000,nameof(bars.AnchorLength)); Range(bars.LapLength,0,10000,nameof(bars.LapLength));
            if(bars.BendDiameter!=0) Range(bars.BendDiameter,1,2000,nameof(bars.BendDiameter));
        }
    }
    internal static void Range(double value,double min,double max,string name)
    { if(!double.IsFinite(value) || value<min || value>max) throw new ArgumentOutOfRangeException(name,value,$"Expected [{min}; {max}]."); }
}
