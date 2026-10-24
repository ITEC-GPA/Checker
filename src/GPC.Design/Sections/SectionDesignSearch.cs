using System.Collections.Concurrent;
using System.Diagnostics;
namespace GPC.Design.Sections;

public enum SectionSearchStage { Preliminary, Probes, Neighbours, Completion }
public sealed record SectionDesignCheck(string Key,string Name,string Combination,double? Ratio,double Target,bool? Passed,string Detail)
{
    /// <summary>Search target with the captured 1e-10 numerical tolerance. Missing/nonfinite/negative ratios fail explicitly.</summary>
    public static SectionDesignCheck FromRatio(string key,string name,string combination,double? ratio,double target,string detail)
    {
        if(!PortableMath.IsFinite(target)||target<=0||target>1)throw new ArgumentException("Design target must be in (0,1].");
        bool finite=ratio is double value&&PortableMath.IsFinite(value)&&value>=0;
        return new(key,name,combination,finite?ratio:null,target,finite&&ratio<=target+1e-10,detail);
    }
}
public sealed record PreparedSectionDesign<T>(int Id,T State,SectionDesignLayout Layout,SectionDesignMetric Metrics,IReadOnlyList<SectionDesignCheck> Checks,string LongitudinalKey);
public sealed record SectionDesignPreparation<T>(PreparedSectionDesign<T>? Candidate,string? Exclusion=null,SectionDesignCheck? Example=null);
public sealed record SectionDesignCandidate<T>(int Id,T State,SectionDesignMetric Metrics,IReadOnlyList<SectionDesignCheck> Checks,double Score,bool Pareto=false);
public sealed record SectionDesignProgress<T>(SectionSearchStage Stage,int Evaluated,int Planned,int Accepted,SectionDesignCandidate<T>? Best,int Workers,double Seconds,int PreliminaryChecked);
public sealed record SectionDesignSearchResult<T>(IReadOnlyList<SectionDesignCandidate<T>> Candidates,int Evaluated,int Planned,bool Completed,
    IReadOnlyDictionary<string,int> Exclusions,IReadOnlyDictionary<string,SectionDesignCheck> ExclusionExamples,
    double Seconds,double? FirstSolutionSeconds,int LongitudinalCalculations,int LongitudinalReuses,int ProbeSections);

/// <summary>Two-stage deterministic section search. The caller adapts a private document per candidate into existing GPC checks.
/// Native resistance is evaluated once per complete longitudinal key and reused only for the corresponding layouts.</summary>
public static class SectionDesignSearch
{
    public static SectionDesignSearchResult<T> Run<T>(IEnumerable<SectionDesignLayout> source,int budget,int workers,SectionDesignObjective objective,
        Func<int,SectionDesignLayout,CancellationToken,SectionDesignPreparation<T>> prepare,
        Func<T,CancellationToken,IReadOnlyList<SectionDesignCheck>> longitudinal,CancellationToken token=default,IProgress<SectionDesignProgress<T>>? progress=null)
    {
        Guard.NotNull(source);Guard.NotNull(objective);Guard.NotNull(prepare);Guard.NotNull(longitudinal);
        token.ThrowIfCancellationRequested();if(workers<1||workers>8||budget<1)throw new ArgumentException("Positive budget and 1–8 workers required.");
        var plans=source.ToArray();if(plans.Length==0||plans.Any(p=>p==null))throw new ArgumentException("Empty or incomplete design catalogue.");
        var accepted=new List<SectionDesignCandidate<T>>();var exclusions=new Dictionary<string,int>();var examples=new Dictionary<string,SectionDesignCheck>();
        var exampleIds=new Dictionary<string,int>();var prepared=new ConcurrentBag<PreparedSectionDesign<T>>();var gate=new object();
        int evaluated=0,preliminaryChecked=0,nativeCalculations=0,reuses=0,probeSections=0;
        var watch=Stopwatch.StartNew();long lastReport=-500;double? firstSolution=null;SectionDesignCandidate<T>? best=null;
        var phase=SectionSearchStage.Preliminary;var parallel=new ParallelOptions{MaxDegreeOfParallelism=workers,CancellationToken=token};
        void Complete(int id,SectionDesignCandidate<T>? candidate=null,string? reason=null,SectionDesignCheck? example=null)
        {
            lock(gate)
            {
                evaluated++;
                if(candidate!=null){accepted.Add(candidate);firstSolution??=watch.Elapsed.TotalSeconds;if(best==null||candidate.Score<best.Score||candidate.Score==best.Score&&candidate.Id<best.Id)best=candidate;}
                if(reason!=null)
                {
                    exclusions[reason]=exclusions.GetValueOrDefault(reason)+1;
                    if(example!=null&&(!exampleIds.TryGetValue(reason,out int previous)||id<previous)){exampleIds[reason]=id;examples[reason]=example;}
                }
                Report();
            }
        }
        void Report(bool force=false)
        {
            if(progress==null||!force&&watch.ElapsedMilliseconds-lastReport<300)return;
            progress.Report(new(phase,evaluated,plans.Length,accepted.Count,best,workers,watch.Elapsed.TotalSeconds,preliminaryChecked));lastReport=watch.ElapsedMilliseconds;
        }
        double Score(PreparedSectionDesign<T> candidate)=>SectionDesignQuantities.Score(candidate.Metrics,objective);
        void Prepare(int index)
        {
            token.ThrowIfCancellationRequested();int id=index+1;
            try
            {
                var preparation=prepare(id,plans[index],token);
                if(preparation.Candidate is not {} candidate){Complete(id,reason:preparation.Exclusion??"Candidate not available.",example:preparation.Example);return;}
                if(candidate.Id!=id||string.IsNullOrEmpty(candidate.LongitudinalKey))throw new ArgumentException("Candidate identity/key inconsistent with its plan.");
                var failed=candidate.Checks.FirstOrDefault(c=>c.Passed==false);
                if(failed!=null){Complete(id,reason:failed.Name,example:failed);return;}
                _=Score(candidate);prepared.Add(candidate with {Checks=Array.AsReadOnly(candidate.Checks.ToArray())});
            }
            catch(Exception ex) when(ex is not OperationCanceledException){Complete(id,reason:ex.Message);}
            finally{lock(gate){preliminaryChecked++;Report();}}
        }
        void Calculate(IEnumerable<PreparedSectionDesign<T>[]> sourceGroups,SectionSearchStage stage,bool probe=false)
        {
            phase=stage;lock(gate)Report(true);
            Parallel.ForEach(sourceGroups,parallel,group=>
            {
                token.ThrowIfCancellationRequested();var representative=group[0];
                try
                {
                    Interlocked.Increment(ref nativeCalculations);if(probe)Interlocked.Increment(ref probeSections);
                    var checks=longitudinal(representative.State,token).ToArray();Interlocked.Add(ref reuses,group.Length-1);
                    var failed=checks.FirstOrDefault(c=>c.Passed==false);
                    foreach(var c in group)
                    {
                        if(failed!=null)Complete(c.Id,reason:failed.Name,example:failed);
                        else if(c.Checks.Count+checks.Length==0)Complete(c.Id,reason:"No checks available for the candidate.");
                        else Complete(c.Id,new(c.Id,c.State,c.Metrics,Array.AsReadOnly(c.Checks.Concat(checks).ToArray()),Score(c)));
                    }
                }
                catch(Exception ex) when(ex is not OperationCanceledException){foreach(var c in group)Complete(c.Id,reason:ex.Message);}
            });
        }
        try
        {
            Report(true);Parallel.ForEach(SectionDesignSelection.SampleIndices(plans.Length,budget),parallel,Prepare);
            var groups=prepared.GroupBy(c=>c.LongitudinalKey).Select(g=>g.OrderBy(Score).ThenBy(c=>c.Id).ToArray()).OrderBy(g=>Score(g[0])).ThenBy(g=>g[0].Id).ToArray();
            var probes=Probe(groups,Score);Calculate(probes,SectionSearchStage.Probes,true);var done=probes.Select(g=>g[0].LongitudinalKey).ToHashSet();
            if(best!=null)
            {
                var reference=best;
                var neighbours=groups.Where(g=>!done.Contains(g[0].LongitudinalKey)&&Score(g[0])<=reference.Score)
                    .OrderBy(g=>Math.Abs(g[0].Metrics.SteelArea/reference.Metrics.SteelArea-1)+Math.Abs((double)g[0].Metrics.Bars/reference.Metrics.Bars-1))
                    .ThenBy(g=>Score(g[0])).Take(16).ToArray();
                Calculate(neighbours,SectionSearchStage.Neighbours);done.UnionWith(neighbours.Select(g=>g[0].LongitudinalKey));
            }
            Calculate(groups.Where(g=>!done.Contains(g[0].LongitudinalKey)),SectionSearchStage.Completion);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        var byId=accepted.ToDictionary(c=>c.Id);var ranked=SectionDesignSelection.Rank(accepted.Select(c=>new SectionDesignRankEntry(c.Id,c.Metrics)),objective)
            .Select(r=>byId[r.Id] with {Score=r.Score,Pareto=r.Pareto}).ToArray();
        bool completed=evaluated==plans.Length&&!token.IsCancellationRequested;Report(true);
        return new(Array.AsReadOnly(ranked),evaluated,plans.Length,completed,new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(exclusions),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string,SectionDesignCheck>(examples),watch.Elapsed.TotalSeconds,firstSolution,nativeCalculations,reuses,probeSections);
    }
    static PreparedSectionDesign<T>[][] Probe<T>(PreparedSectionDesign<T>[][] groups,Func<PreparedSectionDesign<T>,double> score)
    {
        if(groups.Length<=16)return groups;var chosen=new Dictionary<string,PreparedSectionDesign<T>[]>();
        void Add(PreparedSectionDesign<T>[] g)=>chosen.TryAdd(g[0].LongitudinalKey,g);
        void Extremes(Func<PreparedSectionDesign<T>,double> value)
        {var ordered=groups.OrderBy(g=>value(g[0])).ThenBy(g=>score(g[0])).ThenBy(g=>g[0].Id).ToArray();Add(ordered[0]);Add(ordered[ordered.Length - 1]);}
        var byArea=groups.OrderBy(g=>g[0].Metrics.SteelArea).ThenBy(g=>score(g[0])).ThenBy(g=>g[0].Id).ToArray();
        foreach(double fraction in new[]{.5,1,0,.75,.25,.875,.625,.375,.125})Add(byArea[(int)(fraction*(byArea.Length-1))]);
        Extremes(c=>c.Metrics.Bars);Extremes(c=>c.Layout.Top);Extremes(c=>c.Layout.Bottom);Extremes(c=>c.Layout.Side);
        Extremes(c=>c.Layout.TopPhi);Extremes(c=>c.Layout.BottomPhi);return chosen.Values.ToArray();
    }
}
