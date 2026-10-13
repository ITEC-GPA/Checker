using GPC.Checkers.Concrete.Detailing;

namespace GPC.Design.Walls;

/// <summary>Concomitant section moment in Nmm and thickness in mm, for the one-metre wall strip.</summary>
public sealed record WallDesignAction(double Moment,double Thickness);
public sealed record WallDesignLayout(int Count,double Diameter,double SecondaryDiameter,double SecondarySpacing);
public sealed record WallDesignCheck(double? Ratio,bool Detailing);
public sealed record WallDesignResult(WallDesignLayout? Selected,int Evaluated);

/// <summary>Discrete symmetric reinforcement design. Section checks are supplied by the existing Concrete wall session;
/// the delegate must evaluate a fresh candidate and must not change the caller's document.</summary>
public static class WallReinforcementDesign
{
    static readonly double[] Diameters=[8,10,12,14,16,18,20,22,24,26,28,30,32,40];
    public static WallDesignResult Search(WallRebarZone zone,int maximumCount,double maximumDiameter,double target,
        double fyd,double stemBase,IEnumerable<WallDesignAction> sourceActions,
        Func<WallDesignLayout,IReadOnlyList<WallDesignCheck>> evaluate,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(sourceActions);ArgumentNullException.ThrowIfNull(evaluate);
        cancellation.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(zone)||maximumCount<4||maximumCount>30||!double.IsFinite(maximumDiameter)||maximumDiameter<8||maximumDiameter>40||
            !double.IsFinite(target)||target<.1||target>1||!double.IsFinite(fyd)||fyd<=0||!double.IsFinite(stemBase)||stemBase<=0)
            throw new ArgumentException("Invalid wall design catalogue, strength or target.");
        var actions=sourceActions.ToArray();
        if(actions.Length==0||actions.Any(a=>a==null||!double.IsFinite(a.Moment)||!double.IsFinite(a.Thickness)||a.Thickness<=0))
            throw new ArgumentException("Finite moments and positive section thicknesses required.");
        int evaluated=0;
        var candidates=from dia in Diameters where dia<=maximumDiameter from n in Enumerable.Range(4,maximumCount-3)
                       orderby n*dia*dia,dia select (dia,n);
        foreach(var (dia,count) in candidates)
        {
            cancellation.ThrowIfCancellationRequested();
            double area=count*Math.PI*dia*dia/4;
            if(actions.Any(a=>Math.Abs(a.Moment)>area*fyd*a.Thickness))continue;
            evaluated++;
            var candidate=new WallDesignLayout(count,dia,10,SecondarySpacing(zone,area,stemBase));
            IReadOnlyList<WallDesignCheck> checks;
            try{checks=evaluate(candidate);}catch(ArgumentException){continue;}
            if(!Accepts(checks,target))continue;
            cancellation.ThrowIfCancellationRequested();return new(candidate,evaluated);
        }
        return new(null,evaluated);
    }
    public static bool Accepts(IEnumerable<WallDesignCheck> source,double target)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!double.IsFinite(target)||target<.1||target>1)throw new ArgumentException("Target must be in [0.1,1].");
        var checks=source.ToArray();
        return checks.Length>0&&checks.All(c=>c!=null&&c.Ratio is double r&&double.IsFinite(r)&&r>=0&&r<=(c.Detailing?1:target));
    }
    // Initial catalogue policy (not a construction certificate): Ø10, 50–200 mm in steps of 10.
    // The final wall detail checks still verify the actual geometry, spacing, anchors and lap requirements.
    static double SecondarySpacing(WallRebarZone zone,double area,double stemBase)
    {
        double required=zone is WallRebarZone.Stem or WallRebarZone.StemUpper
            ? Math.Max(PlateDetailingRules.WallHorizontalMainRatio*area,PlateDetailingRules.WallHorizontalConcreteRatio/2*stemBase*1000)
            : PlateDetailingRules.SlabSecondaryArea(area);
        return Math.Max(50,Math.Min(200,Math.Floor(Math.PI*100/4*1000/required/10)*10));
    }
    public static int ContinuousStemCount(int lower,int upper)
    {
        if(lower<2||upper<2)throw new ArgumentException("Both faces need at least two bars.");
        return Math.Max(lower,upper);
    }
}
