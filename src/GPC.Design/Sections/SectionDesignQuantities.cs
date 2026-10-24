using GPC.Model.Materials;

namespace GPC.Design.Sections;

public enum ReinforcementObjective { Weight, Pieces, DiameterKinds, Weighted }
public enum SecondaryReinforcementKind { None, Slab, Wall }
public sealed record DesignBar(double X,double Y,double Diameter,double Area);
public sealed record SectionDesignLinks(bool Present,double Diameter,double Spacing,double LegsX,double LegsY,double InternalLegs,double HookAllowance);
public sealed record SectionSecondaryReinforcement(SecondaryReinforcementKind Kind,double AreaPerMetre,double Spacing,double Diameter);
public sealed record SectionDesignMetric(double SteelArea,double LongitudinalKgPerM,double TransverseKgPerM,int Bars,int Diameters,double PiecesPerM)
{
    public double KgPerM=>LongitudinalKgPerM+TransverseKgPerM;
}
public sealed record SectionDesignObjective(ReinforcementObjective Kind,double WeightCost,double BarCost,double DiameterCost);
public sealed class SectionDesignGeometry
{
    public bool Circular {get;}
    public double Width {get;}
    public double Height {get;}
    public double Cover {get;}
    public double SteelArea {get;}
    public IReadOnlyList<DesignBar> Bars {get;}
    /// <summary>
    /// Width of the closed links of a non-circular section, mm: the web width of a T section, where <see cref="Width"/> is the extent of the outline
    /// (the flange). Defaults to <see cref="Width"/>; not used by circular sections (ANTHEA bug analysis 15/10/2026, ARM-3).
    /// </summary>
    public double LinkWidth {get;}
    public SectionDesignGeometry(bool circular,double width,double height,double cover,double steelArea,IEnumerable<DesignBar> bars)
        : this(circular,width,height,cover,steelArea,bars,width) {}
    /// <summary>Geometry with the width of the closed links (<see cref="LinkWidth"/>): positive, finite and not larger than <paramref name="width"/>.</summary>
    public SectionDesignGeometry(bool circular,double width,double height,double cover,double steelArea,IEnumerable<DesignBar> bars,double linkWidth)
    {
        Guard.NotNull(bars);
        if(!PortableMath.IsFinite(width)||width<=0||!PortableMath.IsFinite(height)||height<=0||!PortableMath.IsFinite(cover)||cover<0||!PortableMath.IsFinite(steelArea)||steelArea<=0)
            throw new ArgumentException("Positive finite dimensions and steel area, nonnegative cover required.");
        var owned=bars.ToArray();if(owned.Length==0||owned.Any(b=>b==null||!PortableMath.IsFinite(b.X)||!PortableMath.IsFinite(b.Y)||!PortableMath.IsFinite(b.Diameter)||b.Diameter<=0||!PortableMath.IsFinite(b.Area)||b.Area<=0))
            throw new ArgumentException("Finite bar geometry and positive area required.");
        if(!PortableMath.IsFinite(linkWidth)||linkWidth<=0||linkWidth>width)throw new ArgumentException("Link width must be positive, finite and not larger than the section width.");
        Circular=circular;Width=width;Height=height;Cover=cover;SteelArea=steelArea;Bars=Array.AsReadOnly(owned);LinkWidth=linkWidth;
    }
}

/// <summary>Section search quantities (mm, mm², kg/m), scoring and longitudinal torsion allocation.
/// Uses Model's canonical steel density. Transverse take-off is a preliminary geometric estimate.</summary>
public static class SectionDesignQuantities
{
    const double KgPerSquareMmMetre=Material.SteelDensity*1e6;
    public static SectionDesignMetric Calculate(SectionDesignGeometry section,SectionDesignLinks links,SectionSecondaryReinforcement secondary)
    {
        Guard.NotNull(section);Guard.NotNull(links);Guard.NotNull(secondary);
        if(!PortableEnum.IsDefined(secondary.Kind))throw new ArgumentException("Unknown secondary reinforcement kind.");
        var diameters=section.Bars.Select(b=>b.Diameter).ToList();double weight=0,pieces=section.Bars.Count;
        if(links.Present)
        {
            if(!PortableMath.IsFinite(links.Diameter)||links.Diameter<=0||!PortableMath.IsFinite(links.Spacing)||links.Spacing<=0||
                new[]{links.LegsX,links.LegsY,links.InternalLegs,links.HookAllowance}.Any(v=>!PortableMath.IsFinite(v)||v<0))
                throw new ArgumentException("Invalid link dimensions, legs or hook allowance.");
            // Closed links on the web (LinkWidth) of a T section, not on the flange (ARM-3); the diameter of a circular section.
            double phi=links.Diameter,spacing=links.Spacing,c=section.Cover+phi/2,width=(section.Circular?section.Width:section.LinkWidth)-2*c,height=section.Height-2*c;
            double extra=section.Circular?links.InternalLegs:Math.Max(0,links.LegsX-2)+Math.Max(0,links.LegsY-2);
            double length=section.Circular?Math.PI*width+extra*width:2*(width+height)+Math.Max(0,links.LegsX-2)*width+Math.Max(0,links.LegsY-2)*height;
            length+=links.HookAllowance*phi*(1+extra);
            if(length<=0)throw new ArgumentException("Link length incompatible with cover.");
            weight=Math.PI*phi*phi/4*length/spacing*KgPerSquareMmMetre;
            pieces+=(1+extra)*1000/spacing;diameters.Add(phi);
        }
        if(secondary.Kind!=SecondaryReinforcementKind.None)
        {
            if(new[]{secondary.AreaPerMetre,secondary.Spacing,secondary.Diameter}.Any(v=>!PortableMath.IsFinite(v)||v<0))throw new ArgumentException("Invalid secondary reinforcement.");
            double span=secondary.Kind==SecondaryReinforcementKind.Wall?Math.Max(section.Width,section.Height):section.Width;
            weight+=secondary.AreaPerMetre*span/1000*KgPerSquareMmMetre;
            if(secondary.Spacing>0)pieces+=2000/secondary.Spacing;
            if(secondary.Diameter>0)diameters.Add(secondary.Diameter);
        }
        var result=new SectionDesignMetric(section.SteelArea,section.SteelArea*KgPerSquareMmMetre,weight,section.Bars.Count,diameters.Distinct().Count(),pieces);
        if(!PortableMath.IsFinite(result.KgPerM)||!PortableMath.IsFinite(result.PiecesPerM))throw new ArithmeticException("Section quantities exceed numeric range.");
        return result;
    }
    public static double Score(SectionDesignMetric metric,SectionDesignObjective objective)
    {
        Guard.NotNull(metric);Guard.NotNull(objective);
        if(new[]{metric.SteelArea,metric.LongitudinalKgPerM,metric.TransverseKgPerM,metric.PiecesPerM,objective.WeightCost,objective.BarCost,objective.DiameterCost}.Any(v=>!PortableMath.IsFinite(v)||v<0)||metric.Bars<0||metric.Diameters<0)
            throw new ArgumentException("Finite nonnegative metrics and costs required.");
        double score=objective.Kind switch
        {
            ReinforcementObjective.Weight=>metric.KgPerM,ReinforcementObjective.Pieces=>metric.PiecesPerM,
            ReinforcementObjective.DiameterKinds=>metric.Diameters,
            ReinforcementObjective.Weighted=>objective.WeightCost*metric.KgPerM+objective.BarCost*metric.PiecesPerM+objective.DiameterCost*metric.Diameters,
            _=>throw new ArgumentException("Unknown reinforcement objective.")
        };
        if(!PortableMath.IsFinite(score))throw new ArithmeticException("Design score exceeds numeric range.");return score;
    }
    public static double ReservedArea(double totalArea,double reservePercent)
    {
        _=ReservedDiameterFactor(reservePercent);
        if(!PortableMath.IsFinite(totalArea)||totalArea<0)throw new ArgumentException("Finite nonnegative total steel area required.");
        return totalArea*reservePercent/100;
    }
    public static double ReservedDiameterFactor(double reservePercent)
    {
        if(!PortableMath.IsFinite(reservePercent)||reservePercent<=0||reservePercent>=100)throw new ArgumentException("Torsion reserve must be between 0 and 100 percent.");
        return Math.Sqrt(1-reservePercent/100);
    }
    public static double TorsionDistributionRatio(IEnumerable<DesignBar> source,double requiredArea,double reservePercent)
    {
        Guard.NotNull(source);_ = ReservedDiameterFactor(reservePercent);
        if(!PortableMath.IsFinite(requiredArea)||requiredArea<0)throw new ArgumentException("Finite nonnegative torsion area required.");
        var owned=source.ToArray();
        if(owned.Any(b=>b==null))throw new ArgumentException("Null torsion bar.");
        var bars=owned.OrderBy(b=>Math.Atan2(b.Y,b.X)).ToArray();
        if(bars.Length<3||bars.Any(b=>!PortableMath.IsFinite(b.X)||!PortableMath.IsFinite(b.Y)||!PortableMath.IsFinite(b.Area)||b.Area<=0))throw new ArgumentException("At least three perimeter bars with positive area required.");
        var lengths=bars.Select((b,i)=>PortableMath.Hypot(b.X-bars[(i+1)%bars.Length].X,b.Y-bars[(i+1)%bars.Length].Y)).ToArray();
        double perimeter=lengths.Sum();if(!PortableMath.IsFinite(perimeter)||perimeter<=0)throw new ArgumentException("Nondegenerate bar perimeter required.");
        double worst=0;
        for(int i=0;i<bars.Length;i++)
        {
            double required=requiredArea*(lengths[(i+bars.Length-1)%bars.Length]+lengths[i])/(2*perimeter);
            worst=Math.Max(worst,required/(bars[i].Area*reservePercent/100));
        }
        if(!PortableMath.IsFinite(worst))throw new ArithmeticException("Torsion allocation exceeds numeric range.");return worst;
    }
}
