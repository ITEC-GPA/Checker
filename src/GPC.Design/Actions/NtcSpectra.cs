namespace GPC.Design.Actions;

public enum SiteInterpolation { InverseDistance = 1, RuledSurface = 2 }
public sealed record HazardParameters(double AgG, double F0, double TcStarSeconds);
public sealed record SpectrumPoint(double PeriodSeconds, double AccelerationG);
public sealed record SiteNode(int Id, double Longitude, double Latitude, double XKm, double YKm, double Weight);
public sealed record HazardPeriod(int ReturnYears, HazardParameters Parameters, IReadOnlyList<SpectrumPoint> ReferenceSpectrum, IReadOnlyList<SpectrumPoint> TabulatedSpectrum);
public sealed record SiteHazard(double Longitude, double Latitude, int GridType, IReadOnlyList<SiteNode> Nodes, IReadOnlyList<HazardPeriod> Periods);
public sealed record SpectrumOptions(double NominalLifeYears = 50, double UseCoefficient = 1, string Soil = "A", int Topography = 1,
    double RelativeHeight = 0, double DampingPercent = 5, bool Inelastic = false, double Q0 = 3, bool RegularHeight = true, double VerticalQ = 1.5);
public sealed record SpectrumState(string State, double ReferenceYears, double ReturnYears, HazardParameters Parameters,
    double Ss, double Cc, double St, double Eta, double Tb, double Tc, double Td, double Q, double VerticalF, double VerticalAgG,
    IReadOnlyList<SpectrumPoint> Horizontal, IReadOnlyList<SpectrumPoint> Vertical, IReadOnlyList<SpectrumPoint> Reference);

/// <summary>Numerical compatibility with CSLP Spettri-NTC 1.0.3 (NTC 2008). All accelerations are in g, periods in seconds.</summary>
public static class NtcSpectra
{
    public const string ReferenceVersion = "CSLP Spettri-NTC 1.0.3 (NTC 2008)";
    private static readonly int[] returnYears = [30,50,72,101,140,201,475,975,2475];
    private static readonly double[] hazardPeriods = [0,.1,.15,.2,.3,.4,.5,.75,1,1.5,2];
    public static SiteHazard Site(double longitude, double latitude, SiteInterpolation interpolation)
    {
        FiniteRange(longitude,-180,180,nameof(longitude)); FiniteRange(latitude,-90,90,nameof(latitude));
        if(!PortableEnum.IsDefined(interpolation)) throw new ArgumentOutOfRangeException(nameof(interpolation));
        var grid = new Cslp103Grid(longitude, latitude);
        int status = (int)grid.Number("AG79"), kind = (int)grid.Number("AC51");
        if(status==0) throw new ArgumentException("Sito esterno al reticolo CSLP 1.0.3: non è possibile determinare lo spettro.");
        var nodes = new List<SiteNode>(); var raw = new List<(HazardNode Node,double Weight)>();
        double sum = interpolation == SiteInterpolation.InverseDistance ? grid.Number("AH2") : 1;
        for(int i=0;i<4;i++)
        {
            double weight = grid.Number("AH"+(interpolation==SiteInterpolation.InverseDistance ? i+3 : i+9))/sum;
            if(weight==0) continue;
            if(!PortableMath.IsFinite(weight)) throw new ArgumentException("Pesi di interpolazione CSLP non finiti per il sito.");
            int index=(int)grid.Number("AF"+(i+81))-1;
            var node=Cslp103Data.Nodes[index]; raw.Add((node,weight));
            nodes.Add(new(node.Id,node.Longitude,node.Latitude,node.X-grid.Number("AC3"),node.Y-grid.Number("AD3"),weight));
        }
        double Blend(int column) => raw.Sum(n=>n.Node.Values[column]*n.Weight);
        var periods=new List<HazardPeriod>();
        for(int i=0;i<9;i++)
        {
            var parameters = new HazardParameters(Blend(3*i)/10,Blend(3*i+1),Blend(3*i+2));
            var tabulated=hazardPeriods.Select((t,j)=>new SpectrumPoint(t,Blend(27+11*i+j))).ToArray();
            periods.Add(new(returnYears[i],parameters,HorizontalPoints(parameters,1,1,1,1,0),tabulated));
        }
        return new(Math.Round(longitude,5,MidpointRounding.AwayFromZero),Math.Round(latitude,5,MidpointRounding.AwayFromZero),kind,nodes.AsReadOnly(),periods.AsReadOnly());
    }
    public static IReadOnlyList<SpectrumState> Calculate(SiteHazard site, SpectrumOptions options, IReadOnlyList<HazardParameters>? assignedStates = null)
    {
        Validate(options);
        if(assignedStates is not null) {
            if(assignedStates.Count!=4) throw new ArgumentException("Assegnare i parametri dei quattro stati limite.");
            foreach(var h in assignedStates){FiniteRange(h.AgG,double.Epsilon,1,nameof(h.AgG));FiniteRange(h.F0,1,10,nameof(h.F0));FiniteRange(h.TcStarSeconds,double.Epsilon,2,nameof(h.TcStarSeconds));}
        }
        double vr=Math.Max(options.NominalLifeYears*options.UseCoefficient,35);
        string[] states=["SLO","SLD","SLV","SLC"]; double[] probabilities=[.81,.63,.10,.05];
        var results=new List<SpectrumState>();
        for(int i=0;i<4;i++)
        {
            // Compatibility: the workbook caps SLC at 2474.9, and uses 30.11 only in the SLV lower comparison.
            double tr=-vr/Math.Log(1-probabilities[i]); tr=tr<(i==2?30.11:30)?30:Math.Min(tr,i==3?2474.9:2475);
            var h=assignedStates?[i]??InterpolateReturnPeriod(site,tr); var (ss,cc)=SoilFactors(h,options.Soil);
            double st=1+(new[]{1d,1.2,1.2,1.4}[options.Topography-1]-1)*options.RelativeHeight;
            double q=options.Inelastic?options.Q0*(options.RegularHeight?1:.8):1;
            double eta=options.Inelastic?1/q:Damping(options.DampingPercent);
            // Both radio-button positions in the reference retain this SLU floor.
            double floor=i>=2?.2*h.AgG:0;
            double fv=1.35*h.F0*Math.Sqrt(h.AgG), agv=h.AgG*fv/h.F0;
            results.Add(new(states[i],vr,tr,h,ss,cc,st,eta,cc*h.TcStarSeconds/3,cc*h.TcStarSeconds,1.6+4*h.AgG,q,fv,agv,
                HorizontalPoints(h,ss,cc,st,eta,floor),VerticalPoints(h,st,options.VerticalQ),HorizontalPoints(h,1,1,1,1,0)));
        }
        return results.AsReadOnly();
    }
    public static HazardParameters InterpolateReturnPeriod(SiteHazard site,double tr)
    {
        FiniteRange(tr,30,2475,nameof(tr));
        int lo=Array.FindLastIndex(returnYears,t=>t<=tr), hi=Array.FindIndex(returnYears,t=>t>tr);
        if(hi<0) return site.Periods[lo].Parameters;
        var a=site.Periods[lo].Parameters; var b=site.Periods[hi].Parameters;
        double ratio=Math.Log(tr/returnYears[lo])/(b.AgG/a.AgG==1?1:Math.Log((double)returnYears[hi]/returnYears[lo]));
        double Value(double x,double y) => Math.Exp(Math.Log(x)+Math.Log(y/x)*ratio);
        return new(Value(a.AgG,b.AgG),Value(a.F0,b.F0),Value(a.TcStarSeconds,b.TcStarSeconds));
    }
    public static (double Ss,double Cc) SoilFactors(HazardParameters h,string soil) => soil switch
    {
        "A" => (1,1), "B" => (PortableMath.Clamp(1.4-.4*h.F0*h.AgG,1,1.2),1.1*Math.Pow(h.TcStarSeconds,-.2)),
        "C" => (PortableMath.Clamp(1.7-.6*h.F0*h.AgG,1,1.5),1.05*Math.Pow(h.TcStarSeconds,-.33)),
        "D" => (PortableMath.Clamp(2.4-1.5*h.F0*h.AgG,.9,1.8),1.25*Math.Pow(h.TcStarSeconds,-.5)),
        "E" => (PortableMath.Clamp(2-1.1*h.F0*h.AgG,1,1.6),1.15*Math.Pow(h.TcStarSeconds,-.4)),
        _=>throw new ArgumentException("Categoria di sottosuolo ammessa: A, B, C, D oppure E.")
    };
    public static double Damping(double percent) { FiniteRange(percent,0,100,nameof(percent)); return Math.Max(.55,Math.Sqrt(10/(5+percent))); }
    public static double ElasticHorizontal(HazardParameters h,double ss,double cc,double st,double eta,double period)
    {
        FiniteRange(period,0,double.MaxValue,nameof(period)); double tc=cc*h.TcStarSeconds,tb=tc/3,td=1.6+4*h.AgG;
        return period<tb ? h.AgG*ss*st*(1+(eta*h.F0-1)*period/tb) :
            period<tc ? h.AgG*ss*st*eta*h.F0 : period<td ? h.AgG*ss*st*eta*h.F0*tc/period : h.AgG*ss*st*eta*h.F0*tc*td/(period*period);
    }
    private static IReadOnlyList<SpectrumPoint> HorizontalPoints(HazardParameters h,double ss,double cc,double st,double eta,double floor)
    {
        double tc=h.TcStarSeconds*cc,td=1.6+4*h.AgG,initial=h.AgG*ss*st, plateau=Math.Max(initial*h.F0*eta,floor);
        var points=new List<SpectrumPoint>{new(0,initial),new(tc/3,plateau),new(tc,plateau)};
        double t=tc;
        for(int i=1;i<=21;i++) { t=i==21?td:t+(td-tc)/21; points.Add(new(t,Math.Max(plateau*tc/t,floor))); }
        for(int i=1;i<=21;i++) { t=i==21?4:t+(4-td)/21; points.Add(new(t,Math.Max(plateau*td*tc/(t*t),floor))); }
        return points.AsReadOnly();
    }
    private static IReadOnlyList<SpectrumPoint> VerticalPoints(HazardParameters h,double st,double q)
    {
        double fv=1.35*h.F0*Math.Sqrt(h.AgG),initial=h.AgG*fv/h.F0*st,plateau=h.AgG*fv/q*st;
        var points=new List<SpectrumPoint>{new(0,initial),new(.05,plateau),new(.15,plateau)}; double t=.15;
        for(int i=1;i<=10;i++) { t=i==10?1:t+.085; points.Add(new(t,plateau*.15/t)); }
        for(int i=1;i<=32;i++) { t=i==32?4:t+3d/32; points.Add(new(t,plateau*.15/(t*t))); }
        return points.AsReadOnly();
    }
    public static void Validate(SpectrumOptions o)
    {
        FiniteRange(o.NominalLifeYears,double.Epsilon,1e6,nameof(o.NominalLifeYears)); FiniteRange(o.UseCoefficient,double.Epsilon,10,nameof(o.UseCoefficient));
        if(o.Soil is not ("A" or "B" or "C" or "D" or "E")) throw new ArgumentException("Sottosuolo non ammesso.");
        if(o.Topography<1||o.Topography>4) throw new ArgumentOutOfRangeException(nameof(o.Topography));
        FiniteRange(o.RelativeHeight,0,1,nameof(o.RelativeHeight)); FiniteRange(o.DampingPercent,5,100,nameof(o.DampingPercent));
        FiniteRange(o.Q0,1,6,nameof(o.Q0)); FiniteRange(o.VerticalQ,1,1.5,nameof(o.VerticalQ));
    }
    internal static void FiniteRange(double value,double min,double max,string name)
    { if(!PortableMath.IsFinite(value)||value<min||value>max) throw new ArgumentOutOfRangeException(name,$"{name}: valore finito richiesto fra {min} e {max}."); }
}
