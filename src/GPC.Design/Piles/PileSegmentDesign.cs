using GPC.Checkers.Concrete.Piles;
using GPC.Model.Checking.Contracts;

namespace GPC.Design.Piles;

public sealed record PileDesignLayout(int Count,double Diameter,double LinkDiameter,double LinkSpacing);
public enum PileDesignStage { Layout, Resistance }
public sealed record PileDesignProgress(PileDesignStage Stage,int Evaluated,int Total,PileDesignLayout Layout,int CompletedAxial=0,int TotalAxial=0);
public sealed record PileDesignExclusion(PileDesignLayout Layout,string Diagnostic);
public sealed record PileSegmentDesignResult(PileDesignLayout? Selected,int Evaluated,IReadOnlyList<PileDesignExclusion> GeometricExclusions);

/// <summary>Discrete segment design over the existing Concrete engines. Actions are concomitant kN/kNm at depths in m;
/// layout dimensions are mm. Each factory call transfers a new, exclusive section/solver to the search.
/// Geometrical ordering is PileReinforcement.Layouts; no assumption of monotone resistance is made.</summary>
public static class PileSegmentDesign
{
    public static PileSegmentDesignResult Search(IEnumerable<int> counts,IEnumerable<double> diameters,IEnumerable<double> linkDiameters,
        IEnumerable<double> spacings,IEnumerable<PileAction> sourceActions,double start,double end,double pileLength,
        PileSeismicSettings sourceSeismic,Func<PileDesignLayout,PileRcParameters> factory,CancellationToken cancellation=default,
        IProgress<PileDesignProgress>? progress=null,int maximumParallelism=0)
    {
        ArgumentNullException.ThrowIfNull(factory);ArgumentNullException.ThrowIfNull(sourceActions);ArgumentNullException.ThrowIfNull(sourceSeismic);
        cancellation.ThrowIfCancellationRequested();
        if(!double.IsFinite(start)||!double.IsFinite(end)||!double.IsFinite(pileLength)||start<0||end<=start||pileLength<end||maximumParallelism<0)
            throw new ArgumentException("Valid segment bounds, pile length and parallelism required.");
        var seismic=new PileSeismicSettings {Enabled=sourceSeismic.Enabled,HeadLength=sourceSeismic.HeadLength,Links=sourceSeismic.Links,
            SeismicActionsConfirmed=sourceSeismic.SeismicActionsConfirmed,ElasticMomentConfirmed=sourceSeismic.ElasticMomentConfirmed,StandardNtc2018=sourceSeismic.StandardNtc2018};
        if(seismic.Enabled&&(!seismic.SeismicActionsConfirmed||!seismic.ElasticMomentConfirmed||seismic.Links!=PileSeismicLinks.SingleHoops||!seismic.StandardNtc2018))
            throw new ArgumentException("Confirmed seismic actions, elastic moment, single hoops and NTC profile required.");
        var all=sourceActions.Select(a=>new PileAction {Depth=a.Depth,Side=a.Side,N=a.N,V=a.V,M=a.M}).ToArray();
        if(all.Any(a=>!double.IsFinite(a.Depth)||!double.IsFinite(a.N)||!double.IsFinite(a.V)||!double.IsFinite(a.M)))throw new ArgumentException("Finite concomitant actions required.");
        const double boundaryToleranceMetres=1e-9;
        var actions=all.Where(a=>a.Depth>=start-boundaryToleranceMetres&&a.Depth<=end+boundaryToleranceMetres).ToArray();
        if(actions.Length==0)throw new ArgumentException("No design actions in the segment.");
        var layouts=PileReinforcement.Layouts(counts,diameters,linkDiameters,spacings)
            .Select(l=>new PileDesignLayout(l.Count,l.Diameter,l.LinkDiameter,l.LinkSpacing)).ToArray();
        var screen=new[]{actions.MaxBy(a=>Math.Abs(a.M))!,actions.MaxBy(a=>Math.Abs(a.V))!,actions.MinBy(a=>a.N)!,actions.MaxBy(a=>a.N)!}.Distinct().ToArray();
        var exclusions=new List<PileDesignExclusion>();int evaluated=0;
        foreach(var layout in layouts)
        {
            cancellation.ThrowIfCancellationRequested();evaluated++;
            progress?.Report(new(PileDesignStage.Layout,evaluated,layouts.Length,layout));
            try
            {
                PileReinforcement Create()
                {
                    cancellation.ThrowIfCancellationRequested();
                    var parameters=factory(layout)??throw new ArgumentException("Section factory returned no parameters.");
                    if(!parameters.DesignActionsConfirmed||!parameters.ShearModelConfirmed)throw new ArgumentException("Design actions and shear model must be confirmed.");
                    return new(parameters);
                }
                var service=Create();
                if(service.ConstructionChecks(actions.Max(a=>a.N)).Any(c=>c.Passed==false))continue;
                if(seismic.Enabled&&service.CheckHeadSeismic(seismic,start,end,pileLength,[]).Checks.Any(c=>c.Passed==false))continue;
                if(screen.Any(a=>service.Check(a).Outcome!=EngineeringOutcome.Satisfied))continue;
                // One table per complete layout; no interpolation and no sharing across different configurations.
                service.PrepareResistance(actions.Select(a=>a.N),Create,new PileResistanceTable(),cancellation,maximumParallelism,
                    (done,total)=>progress?.Report(new(PileDesignStage.Resistance,evaluated,layouts.Length,layout,done,total)));
                var verified=actions.Select(service.Check).ToArray();
                if(verified.Any(c=>c.Outcome!=EngineeringOutcome.Satisfied))continue;
                if(seismic.Enabled&&service.CheckHeadSeismic(seismic,start,end,pileLength,verified).Checks.Any(c=>c.Passed!=true))continue;
                cancellation.ThrowIfCancellationRequested();
                return new(layout,evaluated,exclusions.AsReadOnly());
            }
            catch(ArgumentException ex) { exclusions.Add(new(layout,ex.Message)); }
        }
        cancellation.ThrowIfCancellationRequested();return new(null,evaluated,exclusions.AsReadOnly());
    }
}
