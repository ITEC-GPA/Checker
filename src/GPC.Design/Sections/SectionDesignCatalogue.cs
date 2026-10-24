using GPC.Model.Materials;
namespace GPC.Design.Sections;

public enum SectionStirrupMode { Optimize, Keep, None, WithOrWithout }
public sealed record SectionDesignLayout(int Top,int Bottom,int Side,double TopPhi,double BottomPhi,double SidePhi,bool Stirrups,double StirrupPhi,double Spacing);
public sealed record SectionDesignCatalogueInput(bool Circular,double Width,double Height,bool Symmetric,SectionStirrupMode Stirrups,
    bool CurrentStirrups,double CurrentDiameter,double CurrentSpacing,
    IEnumerable<double> Diameters,IEnumerable<int> TopCounts,IEnumerable<int> BottomCounts,IEnumerable<int> SideCounts,IEnumerable<int> CircularCounts,
    IEnumerable<double> StirrupDiameters,IEnumerable<double> StirrupSpacings);

public static class SectionDesignCatalogue
{
    public static IReadOnlyList<SectionDesignLayout> Create(SectionDesignCatalogueInput source,SectionDesignObjective objective)
    {
        Guard.NotNull(source);Guard.NotNull(objective);
        if(!PortableMath.IsFinite(source.Width)||source.Width<=0||!PortableMath.IsFinite(source.Height)||source.Height<=0)throw new ArgumentException("Positive section dimensions required.");
        var dias=Diameters(source.Diameters);var top=Counts(source.TopCounts,2);var bottom=Counts(source.BottomCounts,2);
        var side=Counts(source.SideCounts,0);var circular=Counts(source.CircularCounts,4);var links=Diameters(source.StirrupDiameters);
        var steps=source.StirrupSpacings?.Distinct().OrderBy(orderValue => orderValue).ToArray()??throw new ArgumentNullException(nameof(source.StirrupSpacings));
        if(steps.Length is <1 or >20||steps.Any(v=>!PortableMath.IsFinite(v)||v<20||v>1000))throw new ArgumentException("Invalid stirrup spacings.");
        if(source.Stirrups==SectionStirrupMode.Keep&&source.CurrentStirrups&&
            (!PortableMath.IsFinite(source.CurrentDiameter)||source.CurrentDiameter<=0||!PortableMath.IsFinite(source.CurrentSpacing)||source.CurrentSpacing<=0))throw new ArgumentException("Current links are incomplete.");
        var stirrups=source.Stirrups switch
        {
            SectionStirrupMode.Keep=>new[]{(source.CurrentStirrups,source.CurrentDiameter,source.CurrentSpacing)},
            SectionStirrupMode.None=>new[]{(false,0d,0d)},
            SectionStirrupMode.Optimize or SectionStirrupMode.WithOrWithout=>links.SelectMany(d=>steps.Select(s=>(true,d,s)))
                .Concat(source.Stirrups==SectionStirrupMode.WithOrWithout?[(false,0d,0d)]:[]).ToArray(),
            _=>throw new ArgumentException("Unknown stirrup mode.")
        };
        var list=new List<SectionDesignLayout>();
        void Add(SectionDesignLayout layout){list.Add(layout);if(list.Count>100000)throw new ArgumentException("Più di 100.000 configurazioni: ridurre gli elenchi o usare la disposizione simmetrica.");}
        foreach(var (present,phi,spacing) in stirrups)foreach(double topPhi in dias)
        {
            if(source.Circular){foreach(int n in circular)Add(new(n,0,0,topPhi,topPhi,topPhi,present,phi,spacing));continue;}
            foreach(int nt in top)foreach(int nb in bottom)foreach(double bottomPhi in dias)foreach(int ns in side)
            {
                if(source.Symmetric&&(nt!=nb||topPhi!=bottomPhi))continue;
                Add(new(nt,nb,ns,topPhi,bottomPhi,bottomPhi,present,phi,spacing));
            }
        }
        if(list.Count==0)throw new ArgumentException("Nessuna disposizione: controllare gli elenchi, soprattutto i numeri comuni con simmetria attiva.");
        // Stable priority only: full ranking uses the exact quantities; no interval is ruled out by this bound.
        return Array.AsReadOnly(list.OrderBy(l=>ApproximateScore(l,source.Circular,source.Width,source.Height,objective)).ToArray());
    }
    public static double ApproximateScore(SectionDesignLayout layout,bool circular,double width,double height,SectionDesignObjective objective)
    {
        double area=Math.PI/4*(layout.Top*layout.TopPhi*layout.TopPhi+layout.Bottom*layout.BottomPhi*layout.BottomPhi+2*layout.Side*layout.SidePhi*layout.SidePhi);
        double perimeter=circular?Math.PI*width:2*(height+width);
        int kinds=new[]{layout.TopPhi,layout.BottomPhi,layout.SidePhi}.Concat(layout.Stirrups?[layout.StirrupPhi]:[]).Distinct().Count();
        const double density=Material.SteelDensity*1e6;
        return SectionDesignQuantities.Score(new(area,area*density,layout.Stirrups?Math.PI*layout.StirrupPhi*layout.StirrupPhi/4*perimeter/layout.Spacing*density:0,
            layout.Top+layout.Bottom+2*layout.Side,kinds,layout.Top+layout.Bottom+2*layout.Side+(layout.Stirrups?1000/layout.Spacing:0)),objective);
    }
    static double[] Diameters(IEnumerable<double> source)
    {
        Guard.NotNull(source);var values=source.Distinct().OrderBy(orderValue => orderValue).ToArray();
        if(values.Length is <1 or >12||values.Any(v=>!PortableMath.IsFinite(v)||v<4||v>50))throw new ArgumentException("Invalid bar diameters.");return values;
    }
    static int[] Counts(IEnumerable<int> source,int minimum)
    {
        Guard.NotNull(source);var values=source.Distinct().OrderBy(orderValue => orderValue).ToArray();
        if(values.Length is <1 or >30||values.Any(v=>v<minimum||v>100))throw new ArgumentException("Invalid bar counts.");return values;
    }
}
