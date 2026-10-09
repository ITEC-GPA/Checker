namespace GPC.Checkers.Geotechnics.Piles;

public sealed class PileSegment
{
    public string Id { get; set; } = "";
    public double Start { get; set; }
    public double End { get; set; }
    public bool Inherited { get; set; } = true;
    public double Length => End-Start;
}

/// <summary>Depths in m; compression and weight in kN and kN/m. No axial soil resistance.</summary>
public static class PileSegments
{
    public static void Validate(IReadOnlyList<PileSegment> segments,double length)
    {
        if(segments.Count==0||!Finite(length)||length<=0)throw new ArgumentException("Definire i tratti del palo.");
        double end=0;var ids=new HashSet<string>();
        foreach(var s in segments){if(!ids.Add(s.Id)||string.IsNullOrWhiteSpace(s.Id)||!Finite(s.Start)||!Finite(s.End)||Math.Abs(s.Start-end)>1e-8||s.End<=s.Start)throw new ArgumentException("Tratti: identificativi univoci, quote crescenti e copertura continua richiesti.");end=s.End;}
        if(Math.Abs(end-length)>1e-8)throw new ArgumentException("I tratti devono coprire tutta la lunghezza del palo.");
        if(!segments[0].Inherited)throw new ArgumentException("Il primo tratto deve essere collegato alla sezione principale.");
    }
    public static double ConcreteWeight(double diameterMetres,double reinforcedConcreteUnitWeight)
    {Positive(diameterMetres);Nonnegative(reinforcedConcreteUnitWeight);return Math.PI*diameterMetres*diameterMetres/4*reinforcedConcreteUnitWeight;}
    /// <summary>Grout fills the geotechnical cross-section except the steel annulus. Zero grout weight explicitly excludes grout.</summary>
    public static double TubeWeight(double geotechnicalDiameter,double tubeDiameterMm,double thicknessMm,double steelUnitWeight,double groutUnitWeight)
    {
        Positive(geotechnicalDiameter);Positive(tubeDiameterMm);Positive(thicknessMm);Nonnegative(steelUnitWeight);Nonnegative(groutUnitWeight);
        if(2*thicknessMm>=tubeDiameterMm||tubeDiameterMm>geotechnicalDiameter*1000)throw new ArgumentException("Geometria CHS non valida per il peso.");
        double steel=Math.PI*thicknessMm*(tubeDiameterMm-thicknessMm)/1e6;
        return steel*steelUnitWeight+(Math.PI*geotechnicalDiameter*geotechnicalDiameter/4-steel)*groutUnitWeight;
    }
    public static IReadOnlyList<PileSegment> Split(IReadOnlyList<PileSegment> segments,string id,double depth)
    {
        var result=new List<PileSegment>();foreach(var s in segments){if(s.Id!=id){result.Add(s);continue;}if(!Finite(depth)||depth<=s.Start||depth>=s.End)throw new ArgumentException("Quota di divisione interna al tratto richiesta.");result.Add(new PileSegment{Id=s.Id,Start=s.Start,End=depth,Inherited=s.Inherited});result.Add(new PileSegment{Id=Guid.NewGuid().ToString("N"),Start=depth,End=s.End,Inherited=s.Inherited});}return result;
    }
    static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    static void Positive(double x){if(!Finite(x)||x<=0)throw new ArgumentException("Valore positivo richiesto.");}
    static void Nonnegative(double x){if(!Finite(x)||x<0)throw new ArgumentException("Peso unitario non negativo richiesto.");}
}
