using GPC.Model.Checking.Contracts;

namespace GPC.Design.Walls;

public enum WallBarKind { Main, Secondary, Tie, Head }
public enum WallBarNote { None, FootTowardsValley, FootTowardsMountain, Continuous, Longitudinal, Tie, TieAssignedLength, Head }
public enum WallScheduleWarningCode { DetailChecksIncomplete, DetailSummaryDisabled, LongerThanStock, FootReversed }
/// <summary>Panel and stock length in m, end cover in mm, ties per metre of wall and row, tie cut length in mm (0 = calculated).</summary>
public sealed record WallScheduleOptions(double PanelLength, double EndCover, double StockLength, int TiesPerMetre, double TieCutLength,
    bool DetailSummaryEnabled);
/// <summary>A position of the bar schedule. Quantity is the number of pieces in the panel, CutLength the length of a piece (m, sum of the
/// legs rounded to the cm), Spacing the nominal spacing (mm). Longitudinal bars: Lines in the section, Pieces per line spliced by Lap
/// (mm); SectionPoints their positions in the section. Ties: rows of ties in the lap; SectionPoints the rows.</summary>
public sealed record WallScheduledBar(string Mark, WallRebarZone Zone, WallRebarFace? Face, WallBarKind Kind, double Diameter,
    int Quantity, double? CutLength, double Spacing, double Mandrel, double Lap, IReadOnlyList<WallBarLeg> Legs,
    IReadOnlyList<WallBarPoint> Shape, WallBarNote Note, double? ZoneStart = null, double? ZoneEnd = null, int? TieRows = null)
{
    public double? TotalLength => Quantity * CutLength;
    public double? Weight => TotalLength * Math.PI * Diameter * Diameter / 4 * WallRebarQuantities.SteelKgPerSquareMmMetre;
    public int? Lines { get; init; }
    public int? Pieces { get; init; }
    public IReadOnlyList<WallBarPoint> SectionPoints { get; init; } = [];
}
/// <summary>Total of a diameter: length (m), mass per metre (kg/m) and weight (kg).</summary>
public sealed record WallDiameterTotal(double Diameter, double Length, double KgPerMetre, double Weight);
public sealed record WallScheduleWarning(WallScheduleWarningCode Code, string? Mark = null, int? Count = null);
public sealed record WallScheduleResult(double PanelLength, double EndCover, double StockLength,
    IReadOnlyList<WallScheduledBar> Bars, IReadOnlyList<WallScheduleWarning> Warnings)
{
    public double KnownWeight => Bars.Sum(b => b.Weight ?? 0);
    public bool CompleteQuantities => Bars.All(b => b.CutLength is not null);
    public IReadOnlyList<WallDiameterTotal> Diameters { get; init; } = [];
    /// <summary>Concrete of the panel (m³) and steel per cubic metre of concrete (kg/m³), net weight without waste.</summary>
    public double ConcreteVolume { get; init; }
    public double Incidence => ConcreteVolume > 0 ? KnownWeight / ConcreteVolume : 0;
}
/// <summary>Bar schedule of a panel of wall, as on a reinforcement drawing: positions in drawing order, shapes with the legs at the centre
/// line rounded to the cm, pieces in the panel, longitudinal bars spliced along the panel, totals per diameter and incidence. Net weight
/// without waste. Panel, stock, cut lengths and shape coordinates in m; cover, diameters, spacing, laps and tie cut input in mm.</summary>
public static class WallBarScheduling
{
    public static WallScheduleResult Calculate(WallDetailInput input, WallScheduleOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); Guard.NotNull(options);
        WallReinforcementDetailing.Validate(input);
        WallReinforcementDetailing.Range(options.PanelLength,.2,100,nameof(options.PanelLength));
        WallReinforcementDetailing.Range(options.EndCover,0,200,nameof(options.EndCover));
        WallReinforcementDetailing.Range(options.StockLength,1,18,nameof(options.StockLength));
        var g=input.Geometry;
        double length=options.PanelLength,endCover=options.EndCover,stock=options.StockLength;
        var details=WallReinforcementDetailing.Calculate(input with {Options=input.Options with {StockLength=stock}},token);
        var bars=new List<WallScheduledBar>(); var warnings=new List<WallScheduleWarning>();
        int Pieces(double diameter,double spacing)
        {
            double span=length*1000-2*endCover-diameter;
            if(span<=0 || spacing<=0) throw new ArgumentException("Panel too short for end cover and bars.");
            // Bars at both ends of the panel, never more than the spacing of the calculation between them.
            return checked((int)Math.Ceiling(span/spacing-1e-10)+1);
        }
        foreach(var b in details.Bars)
        {
            token.ThrowIfCancellationRequested();
            var legs=Rounded(b.Legs);
            var note=b.Continuous?WallBarNote.Continuous:b.Foot==WallFootDirection.Valley?WallBarNote.FootTowardsValley:b.Foot==WallFootDirection.Mountain?WallBarNote.FootTowardsMountain:WallBarNote.None;
            bars.Add(new(b.Mark,b.Zone,b.Face,WallBarKind.Main,b.Diameter,Pieces(b.Diameter,b.Spacing),legs.Sum(l=>l.Length),b.Spacing,b.Mandrel,b.Lap,legs,b.Path,note));
            if(b.FootReversed) warnings.Add(new(WallScheduleWarningCode.FootReversed,b.Mark));
        }
        if(details.Head is { } head)
        {
            var legs=Rounded(head.Legs);
            bars.Add(new(head.Mark,head.Zone,null,WallBarKind.Head,head.Diameter,Pieces(head.Diameter,head.Spacing),legs.Sum(l=>l.Length),head.Spacing,head.Mandrel,0,legs,head.Path,WallBarNote.Head));
        }
        if(details.Tie is { } tie)
        {
            WallReinforcementDetailing.Range(options.TiesPerMetre,1,30,nameof(options.TiesPerMetre));
            WallReinforcementDetailing.Range(options.TieCutLength,0,10000,nameof(options.TieCutLength));
            var legs=Rounded(tie.Legs); double cut=options.TieCutLength>0?options.TieCutLength/1000:legs.Sum(l=>l.Length);
            int count=checked(tie.Rows*(int)Math.Ceiling(options.TiesPerMetre*length-1e-10));
            double middle=(tie.Path[0].X+tie.Path[tie.Path.Count-1].X)/2;
            bars.Add(new WallScheduledBar(tie.Mark,WallRebarZone.Stem,null,WallBarKind.Tie,tie.Diameter,count,cut,tie.Spacing,tie.Mandrel,0,legs,tie.Path,
                options.TieCutLength>0?WallBarNote.TieAssignedLength:WallBarNote.Tie,TieRows:tie.Rows)
                { SectionPoints=tie.Levels.Select(y=>new WallBarPoint(middle,y)).ToArray() });
        }
        double net=length-2*endCover/1000;
        if(net<=0) throw new ArgumentException("Nonpositive net length of longitudinal bars.");
        foreach(var l in details.Longitudinals)
        {
            token.ThrowIfCancellationRequested();
            // Spliced along the panel when longer than a stock bar: k equal pieces rounded up to the cm, k-1 laps l0.
            double lap=l.Lap/1000; int pieces=1; double piece=Centimetres(net,true);
            if(piece>stock)
            {
                if(stock<=lap) throw new ArgumentException("Stock bar not longer than the lap of the longitudinal bars.");
                pieces=Math.Max(2,checked((int)Math.Ceiling((net-lap)/(stock-lap)-1e-10)));
                while((piece=Centimetres((net+(pieces-1)*lap)/pieces,true))>stock) pieces++;
            }
            var legs=new[]{new WallBarLeg("A",piece){Kind=WallBarLegKind.Straight,From=new(0,0),To=new(piece,0)}};
            bars.Add(new WallScheduledBar(l.Mark,l.Zone,l.Face,WallBarKind.Secondary,l.Diameter,checked(l.Points.Count*pieces),piece,l.Spacing,0,pieces>1?l.Lap:0,
                legs,new[]{new WallBarPoint(0,0),new WallBarPoint(piece,0)},WallBarNote.Longitudinal)
                { Lines=l.Points.Count,Pieces=pieces,SectionPoints=l.Points });
        }
        int failed=details.Checks.Count(c=>c.Outcome!=EngineeringOutcome.Satisfied);
        if(failed>0) warnings.Insert(0,new(WallScheduleWarningCode.DetailChecksIncomplete,Count:failed));
        if(!options.DetailSummaryEnabled) warnings.Add(new(WallScheduleWarningCode.DetailSummaryDisabled));
        foreach(var b in bars.Where(b=>b.CutLength>stock)) warnings.Add(new(WallScheduleWarningCode.LongerThanStock,b.Mark));
        double kgPerMetre(double d)=>Math.PI*d*d/4*WallRebarQuantities.SteelKgPerSquareMmMetre;
        var diameters=bars.GroupBy(b=>b.Diameter).OrderBy(x=>x.Key).Select(x=>new WallDiameterTotal(x.Key,x.Sum(b=>b.TotalLength??0),kgPerMetre(x.Key),x.Sum(b=>b.Weight??0))).ToArray();
        double area=(g.Toe+g.StemBase+g.Heel)*g.Slab+(g.StemBase+g.StemTop)/2*g.Height;
        token.ThrowIfCancellationRequested();
        return new(length,endCover,stock,bars.AsReadOnly(),warnings.AsReadOnly()){Diameters=diameters,ConcreteVolume=area*length};
    }
    /// <summary>Legs at the centre line rounded to the cm (arcs developed); the cut length is their sum, as written on the shape.</summary>
    static IReadOnlyList<WallBarLeg> Rounded(IReadOnlyList<WallBarLeg> legs)=>legs.Where(l=>l.Length>0).Select(l=>l with {Length=Centimetres(l.Length,false)}).ToArray();
    static double Centimetres(double metres,bool up)=>(up?Math.Ceiling(metres*100-1e-9):Math.Round(metres*100,MidpointRounding.AwayFromZero))/100;
}
