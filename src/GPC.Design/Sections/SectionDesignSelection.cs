using GPC.Checkers.Concrete.Detailing;
namespace GPC.Design.Sections;

public sealed record SectionDesignRankEntry(int Id,SectionDesignMetric Metrics);
public sealed record SectionDesignRank(int Id,double Score,bool Pareto);
public sealed record SectionSecondaryDesign(SectionSecondaryReinforcement Reinforcement,SectionDesignMetric Metrics);

/// <summary>Deterministic ranking, budget sampling and secondary-mat catalogue policy for section design.</summary>
public static class SectionDesignSelection
{
    public static IReadOnlyList<SectionDesignRank> Rank(IEnumerable<SectionDesignRankEntry> source,SectionDesignObjective objective)
    {
        Guard.NotNull(source);var entries=source.ToArray();
        if(entries.Any(c=>c==null)||entries.Select(c=>c.Id).Distinct().Count()!=entries.Length)throw new ArgumentException("Unique candidate identities required.");
        var rows=entries.Select(c=>(Entry:c,Score:SectionDesignQuantities.Score(c.Metrics,objective))).OrderBy(c=>c.Score)
            .ThenBy(c=>c.Entry.Metrics.KgPerM).ThenBy(c=>c.Entry.Metrics.PiecesPerM).ThenBy(c=>c.Entry.Metrics.Diameters).ThenBy(c=>c.Entry.Id).ToArray();
        var frontier=new List<SectionDesignMetric>();var pareto=new HashSet<int>();
        foreach(var c in rows.OrderBy(c=>c.Entry.Metrics.KgPerM).ThenBy(c=>c.Entry.Metrics.PiecesPerM).ThenBy(c=>c.Entry.Metrics.Diameters))
        {
            var m=c.Entry.Metrics;
            if(frontier.Any(b=>b.KgPerM<=m.KgPerM&&b.PiecesPerM<=m.PiecesPerM&&b.Diameters<=m.Diameters&&
                (b.KgPerM<m.KgPerM||b.PiecesPerM<m.PiecesPerM||b.Diameters<m.Diameters)))continue;
            pareto.Add(c.Entry.Id);
            if(!frontier.Any(b=>b.KgPerM==m.KgPerM&&b.PiecesPerM==m.PiecesPerM&&b.Diameters==m.Diameters))frontier.Add(m);
        }
        return Array.AsReadOnly(rows.Select(c=>new SectionDesignRank(c.Entry.Id,c.Score,pareto.Contains(c.Entry.Id))).ToArray());
    }
    public static IReadOnlyList<int> SampleIndices(int planned,int budget)
    {
        if(planned<0||budget<=0)throw new ArgumentException("Nonnegative catalogue and positive budget required.");
        int count=Math.Min(planned,budget);if(count==0)return Array.Empty<int>();if(count==1)return new[]{planned-1};
        return Enumerable.Range(0,count).Select(i=>(int)((long)i*(planned-1)/(count-1))).ToArray();
    }
    public static SectionSecondaryDesign SelectSecondary(SectionDesignGeometry section,SectionDesignLinks links,
        SecondaryReinforcementKind kind,double minimumArea,double? maximumSpacing,bool critical,double outlineMinimumY,double outlineMaximumY,
        double aggregate,IEnumerable<double> sourceDiameters,IEnumerable<double> sourceSpacings,int maximumDiameterKinds,SectionDesignObjective objective)
    {
        Guard.NotNull(section);Guard.NotNull(links);Guard.NotNull(sourceDiameters);Guard.NotNull(sourceSpacings);
        if(kind is not (SecondaryReinforcementKind.Slab or SecondaryReinforcementKind.Wall)||!PortableMath.IsFinite(minimumArea)||minimumArea<0||
            maximumSpacing is {} provided&&(!PortableMath.IsFinite(provided)||provided<=0)||!PortableMath.IsFinite(outlineMinimumY)||!PortableMath.IsFinite(outlineMaximumY)||outlineMaximumY<=outlineMinimumY||
            !PortableMath.IsFinite(aggregate)||aggregate<0||maximumDiameterKinds<1)throw new ArgumentException("Invalid secondary-reinforcement constraints.");
        bool slab=kind==SecondaryReinforcementKind.Slab;
        if(slab)
        {
            double middle=(outlineMinimumY+outlineMaximumY)/2;
            double upper=section.Bars.Where(b=>b.Y>=middle).Sum(b=>b.Area),lower=section.Bars.Where(b=>b.Y<middle).Sum(b=>b.Area);
            minimumArea=Math.Max(minimumArea,2*PlateDetailingRules.SlabSecondaryArea(Math.Max(upper,lower))*1000/section.Width);
        }
        double maximum=maximumSpacing??(slab?PlateDetailingRules.SlabSecondarySpacing(section.Height,critical):PlateDetailingRules.WallHorizontalMaximumSpacing);
        var diameters=sourceDiameters.Distinct().OrderBy(orderValue => orderValue).ToArray();var spacings=sourceSpacings.Distinct().OrderBy(orderValue => orderValue).ToArray();
        if(diameters.Length==0||spacings.Length==0||diameters.Concat(spacings).Any(v=>!PortableMath.IsFinite(v)||v<=0))throw new ArgumentException("Positive finite secondary catalogue required.");
        SectionSecondaryDesign? best=null;double bestScore=0;
        foreach(double phi in diameters)foreach(double spacing in spacings)
        {
            double area=2*Math.PI*phi*phi/4*1000/spacing;
            if(area<minimumArea||spacing>maximum||spacing-phi<PlateDetailingRules.MinimumClearSpacing(phi,aggregate))continue;
            double thickness=slab?section.Height:Math.Min(section.Width,section.Height);
            double remaining=thickness-2*(section.Cover+section.Bars.Max(b=>b.Diameter)+phi+(links.Present?links.Diameter:0));
            if(remaining<Math.Max(20,aggregate+5))continue;
            var mat=new SectionSecondaryReinforcement(kind,area,spacing,phi);var metrics=SectionDesignQuantities.Calculate(section,links,mat);
            if(metrics.Diameters>maximumDiameterKinds)continue;double score=SectionDesignQuantities.Score(metrics,objective);
            if(best==null||score<bestScore||score==bestScore&&metrics.KgPerM<best.Metrics.KgPerM){best=new(mat,metrics);bestScore=score;}
        }
        return best??throw new ArgumentException("No feasible secondary mat in the catalogue.");
    }
}
