using GPC.Model.Checking.Contracts;

namespace GPC.Design.Walls;

public enum WallBarKind { Main, Secondary, Tie }
public enum WallBarNote { None, InclinedStemTransition, LongitudinalEndsToDetail, TieShapeToDetail }
public enum WallScheduleWarningCode { DetailChecksIncomplete, DetailSummaryDisabled, LongerThanStock, MissingTieDevelopment, Preliminary }
public sealed record WallScheduleOptions(double PanelLength, double EndCover, double StockLength, int TiesPerMetre, double TieCutLength,
    bool DetailSummaryEnabled);
public sealed record WallBarLeg(string Name, double Length);
public sealed record WallScheduledBar(string Mark, WallRebarZone Zone, WallRebarFace? Face, WallBarKind Kind, double Diameter,
    int Quantity, double? CutLength, double Spacing, double Mandrel, double Lap, IReadOnlyList<WallBarLeg> Legs,
    IReadOnlyList<WallBarPoint> Shape, WallBarNote Note, double? ZoneStart = null, double? ZoneEnd = null, int? TieRows = null)
{
    public double? TotalLength => Quantity * CutLength;
    public double? Weight => TotalLength * Math.PI * Diameter * Diameter / 4 * WallRebarQuantities.SteelKgPerSquareMmMetre;
}
public sealed record WallScheduleWarning(WallScheduleWarningCode Code, string? Mark = null, int? Count = null);
public sealed record WallScheduleResult(double PanelLength, double EndCover, double StockLength,
    IReadOnlyList<WallScheduledBar> Bars, IReadOnlyList<WallScheduleWarning> Warnings)
{
    public double KnownWeight => Bars.Sum(b => b.Weight ?? 0);
    public bool CompleteQuantities => Bars.All(b => b.CutLength is not null);
}
/// <summary>Preliminary take-off of the entered reinforcement, not a certification of a fabrication drawing.
/// Panel, stock, cut lengths and shape coordinates in m; cover, diameters, spacing, laps and tie cut input in mm.</summary>
public static class WallBarScheduling
{
    public static WallScheduleResult Calculate(WallDetailInput input, WallScheduleOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); Guard.NotNull(options);
        WallReinforcementDetailing.Validate(input);
        WallReinforcementDetailing.Range(options.PanelLength,.2,100,nameof(options.PanelLength));
        WallReinforcementDetailing.Range(options.EndCover,0,200,nameof(options.EndCover));
        WallReinforcementDetailing.Range(options.StockLength,1,18,nameof(options.StockLength));
        var g=input.Geometry; var r=input.Reinforcement;
        double length=options.PanelLength,endCover=options.EndCover,stock=options.StockLength,cover=input.Cover/1000;
        double h=g.Height,t=g.Slab,a=g.Toe,s=g.StemBase,heel=g.Heel;
        bool two=r.TwoZones; double split=two?r.LowerHeight:h;
        var details=WallReinforcementDetailing.Calculate(input,token);
        var bars=new List<WallScheduledBar>(); var warnings=new List<WallScheduleWarning>();
        foreach(var b in details.Bars)
        {
            token.ThrowIfCancellationRequested();
            double span=length*1000-2*endCover-b.Diameter;
            if(span<=0 || b.Spacing<=0) throw new ArgumentException("Panel too short for end cover and bars.");
            int count=checked((int)Math.Ceiling(span/b.Spacing-1e-10)+1);
            var legs=new List<WallBarLeg>();
            if(b.Mandrel>0)
            {
                legs.Add(new("A",Distance(b.Path[0],b.Path[1])));
                legs.Add(new("B",Distance(b.Path[1],b.Path[2])));
                legs.Add(new("Arc90",(b.Mandrel+b.Diameter)/2000*Math.PI/2));
                legs.Add(new("C",Distance(b.Path[b.Path.Count - 2],b.Path[b.Path.Count - 1])));
            }
            else legs.Add(new("A",b.Length));
            bars.Add(new(b.Mark,b.Zone,b.Face,WallBarKind.Main,b.Diameter,count,legs.Sum(l=>l.Length),span/(count-1),b.Mandrel,b.Lap,
                legs.AsReadOnly(),b.Path,b.Zone==WallRebarZone.Stem && b.Face==WallRebarFace.Downstream && s!=g.StemTop?WallBarNote.InclinedStemTransition:WallBarNote.None));
        }
        int secondaryMark=0;
        foreach(var key in PortableEnum.GetValues<WallRebarZone>())
        {
            if(key==WallRebarZone.StemUpper && !two || key==WallRebarZone.Toe && a==0 || key==WallRebarZone.Heel && heel==0) continue;
            token.ThrowIfCancellationRequested();
            var arm=r.Zones[key];double dia=arm.SecondaryDiameter,step=arm.SecondarySpacing;
            bool stem=key is WallRebarZone.Stem or WallRebarZone.StemUpper,upper=key==WallRebarZone.StemUpper;
            double start=stem?upper?split:cover+dia/2000:key==WallRebarZone.Toe || a==0?cover+dia/2000:a+s/2;
            double end=stem?upper || !two?h-cover-dia/2000:split:key==WallRebarZone.Toe && heel>0?a+s/2:a+s+heel-cover-dia/2000;
            double span=end-start;
            if(span<=0) throw new ArgumentException("Zone too short for secondary reinforcement: "+key);
            // Shared boundaries belong to the following zone, never count the same bar twice.
            bool omitLast=key==WallRebarZone.Stem && two || key==WallRebarZone.Toe && heel>0;
            int intervals=Math.Max(1,checked((int)Math.Ceiling(span*1000/step-1e-10)));
            int count=intervals+(omitLast?0:1);double cut=length-2*endCover/1000;
            if(cut<=0) throw new ArgumentException("Nonpositive net length of secondary bars.");
            for(int face=0;face<2;face++)
                bars.Add(new("S"+(++secondaryMark),key,stem?face==0?WallRebarFace.Upstream:WallRebarFace.Downstream:face==0?WallRebarFace.Bottom:WallRebarFace.Top,
                    WallBarKind.Secondary,dia,count,cut,span*1000/intervals,0,0,Array.AsReadOnly(new[]{new WallBarLeg("A",cut)}),
                    Array.AsReadOnly(new[]{new WallBarPoint(0,0),new WallBarPoint(cut,0)}),WallBarNote.LongitudinalEndsToDetail,start,end));
        }
        if(two)
        {
            WallReinforcementDetailing.Range(options.TiesPerMetre,1,30,nameof(options.TiesPerMetre));
            WallReinforcementDetailing.Range(options.TieCutLength,0,10000,nameof(options.TieCutLength));
            double cut=options.TieCutLength;
            int rows=checked((int)Math.Ceiling(details.Bars.Max(b=>b.Lap)/input.Options.TieSpacing)+1);
            int count=checked(rows*(int)Math.Ceiling(options.TiesPerMetre*length-1e-10));
            bars.Add(new("C1",WallRebarZone.Stem,null,WallBarKind.Tie,input.Options.TieDiameter,count,cut==0?null:cut/1000,input.Options.TieSpacing,
                0,0,Array.Empty<WallBarLeg>(),Array.Empty<WallBarPoint>(),WallBarNote.TieShapeToDetail,TieRows:rows));
        }
        int failed=details.Checks.Count(c=>c.Outcome!=EngineeringOutcome.Satisfied);
        if(failed>0) warnings.Add(new(WallScheduleWarningCode.DetailChecksIncomplete,Count:failed));
        if(!options.DetailSummaryEnabled) warnings.Add(new(WallScheduleWarningCode.DetailSummaryDisabled));
        foreach(var b in bars.Where(b=>b.CutLength>stock)) warnings.Add(new(WallScheduleWarningCode.LongerThanStock,b.Mark));
        if(bars.Any(b=>b.CutLength is null)) warnings.Add(new(WallScheduleWarningCode.MissingTieDevelopment));
        warnings.Add(new(WallScheduleWarningCode.Preliminary));
        token.ThrowIfCancellationRequested();
        return new(length,endCover,stock,bars.AsReadOnly(),warnings.AsReadOnly());
    }
    static double Distance(WallBarPoint a,WallBarPoint b)=>PortableMath.Hypot(a.X-b.X,a.Y-b.Y);
}
