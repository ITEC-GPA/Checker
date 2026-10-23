using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using ConcreteTests;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Geometry;

internal static class DomainBenchmarkCounters
{
    public static long Iterations, Integrations, BracketIterations;
    public static void Reset() { Iterations = Integrations = BracketIterations = 0; }
}

internal static class Program
{
    private sealed record Case(string Id, DomainPointContractTests.BenchSection Section,
        ConcreteMaterial.CompressionStressStrainDiagrams Diagram, SectionSolver.FailureDomainTypes State,
        SectionSolver.FailureAnalysisTypes Criterion, double N, double Mx, double My);

    private static IEnumerable<DomainPointContractTests.BenchSection> Sections()
    {
        var slender = new DomainPointContractTests.BenchSection { Name="S1",Width=1000,Height=200,Fck=25,
            Bars=Enumerable.Range(0,5).SelectMany(i=>new[] {new[] {-400+200.0*i,-60.0,12.0},new[] {-400+200.0*i,60.0,12.0}}).ToArray(),
            AxialKn=new[] {400.0,0.0,221.277,-1000.0,-2500.0} };
        return DomainPointContractTests.Sections.Concat(new[] {slender});
    }

    private static IEnumerable<Case> FocusCases()
    {
        const ConcreteMaterial.CompressionStressStrainDiagrams diagram = ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle;
        foreach (var s in Sections())
        foreach (var state in DomainPointContractTests.States)
        foreach (int angle in Enumerable.Range(0,12).Select(i=>30*i))
        {
            double x=Math.Cos(angle*Math.PI/180), y=Math.Sin(angle*Math.PI/180);
            double size=Math.Max(s.Diameter,Math.Max(s.Width,s.Height))/1000; // m
            foreach (double n in new[] {s.AxialKn[0],0,s.AxialKn[2],s.AxialKn[s.AxialKn.Length-2]})
                yield return new Case($"Focus/{s.Name}/{diagram}/{state}/ConstantN/{n}/{angle}",s,diagram,state,
                    SectionSolver.FailureAnalysisTypes.ConstantN,n,x,y);
            // Prescribed eccentricities, independent of either solver's result; compression and tension.
            foreach (int sign in new[] {-1,1})
            foreach (double ratio in new[] {.05,.2,.75,2.0})
                yield return new Case($"Focus/{s.Name}/{diagram}/{state}/ConstantEccentricity/{sign}/{ratio}/{angle}",s,diagram,state,
                    SectionSolver.FailureAnalysisTypes.ConstantEccentricity,sign*100,100*size*ratio*x,100*size*ratio*y);
            yield return new Case($"Focus/{s.Name}/{diagram}/{state}/ConstantEccentricity/pure-bending/{angle}",s,diagram,state,
                SectionSolver.FailureAnalysisTypes.ConstantEccentricity,0,25*x,25*y);
        }
        // Keep all original eccentricity cases, including the two C2 tail cases, as explicit anchors.
        foreach (var c in Cases().Where(c=>c.Diagram==diagram && c.Criterion==SectionSolver.FailureAnalysisTypes.ConstantEccentricity))
            yield return c;
    }

    private static IEnumerable<Case> Cases()
    {
        foreach (var s in Sections())
        foreach (var diagram in DomainPointContractTests.Diagrams)
        foreach (var state in DomainPointContractTests.States)
        {
            // Identical geometry and actions in both executables; no data regenerated from a solver result.
            double[] axial = { s.AxialKn[1], s.AxialKn[2], s.AxialKn[s.AxialKn.Length - 2] };
            foreach (double n in axial)
            foreach (double angle in new[] { 0.0, 30.0, 90.0, 210.0 })
            {
                double x = Math.Cos(angle * Math.PI / 180), y = Math.Sin(angle * Math.PI / 180);
                string id = $"{s.Name}/{diagram}/{state}/ConstantN/{n}/{angle}";
                yield return new Case(id,s,diagram,state,SectionSolver.FailureAnalysisTypes.ConstantN,n,x,y);
            }
            foreach (var criterion in new[] { SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
                SectionSolver.FailureAnalysisTypes.ConstantNMx, SectionSolver.FailureAnalysisTypes.ConstantNMy,
                SectionSolver.FailureAnalysisTypes.ConstantMxMy })
            foreach (int sign in new[] { -1, 1 })
                yield return new Case($"{s.Name}/{diagram}/{state}/{criterion}/{sign}",s,diagram,state,criterion,
                    axial[1],sign * 20,sign * 15);
        }
    }

    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length < 1) throw new ArgumentException("Output JSONL path required; optional repeats (default 5), filter, catalogue (all/focus).");
        int repeats = args.Length > 1 ? int.Parse(args[1]) : 5;
        string catalogue=args.Length>3 ? args[3] : "all";
        if (catalogue!="all" && catalogue!="focus") throw new ArgumentException("Unknown catalogue: "+catalogue);
        var cases = (catalogue=="focus" ? FocusCases() : Cases()).Where(c => args.Length < 3 || (args[2].StartsWith("!") ? !c.Id.Contains(args[2].Substring(1)) : c.Id.Contains(args[2]))).ToArray();
        if (cases.Select(c=>c.Id).Distinct().Count()!=cases.Length) throw new InvalidOperationException("Duplicate case IDs.");
        if (File.Exists(args[0])) throw new IOException("Measurement file already exists: "+args[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        using var output = new StreamWriter(args[0]);
        File.WriteAllText(args[0] + ".meta.json", JsonSerializer.Serialize(new {
            runtime=RuntimeInformation.FrameworkDescription, os=RuntimeInformation.OSDescription,
            processors=Environment.ProcessorCount, cases=cases.Length, catalogue, repeats, startUtc=DateTime.UtcNow,
            stopwatchFrequency=Stopwatch.Frequency, tieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), process=Environment.Is64BitProcess
        },new JsonSerializerOptions { WriteIndented=true }));
        // Warm the JIT on the same catalogue before collecting any samples.
        foreach (var c in cases.Take(8)) Run(c, -1, false);
        int done=0;
        foreach (var c in cases)
        {
            Run(c,repeats,true);
            if (++done % 50 == 0) { Console.WriteLine($"{done}/{cases.Length}"); output.Flush(); }
        }
        return 0;

        void Run(Case c,int count,bool write)
        {
            var timer=Stopwatch.StartNew();
            var section=DomainPointContractTests.BuildSection(c.Section,c.Diagram);
            var checker=DomainPointContractTests.BuildChecker(section,c.State);
            var solver=checker.SectionSolver;
            var force=DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section),c.N,c.Mx,c.My);
            timer.Stop(); double prepareMs=timer.Elapsed.TotalMilliseconds;
            for (int run=0; run <= Math.Max(0,count); run++)
            {
                int logStart=solver.GetLog().Count;
                DomainBenchmarkCounters.Reset();
                FailureDomain.FailureDomainPoint point=null;
                string error=null;
                timer.Restart();
                try { point=solver.CalculateConvergedDomainPoint(force,c.Criterion,CancellationToken.None); }
                catch (Exception e) { error=e.GetType().Name+": "+e.Message; }
                timer.Stop();
                double ms=timer.Elapsed.TotalMilliseconds;
                long iterations=DomainBenchmarkCounters.Iterations, evaluations=DomainBenchmarkCounters.Integrations,
                    bracketIterations=DomainBenchmarkCounters.BracketIterations;
                var log=solver.GetLog().Skip(logStart).ToArray();
                string path=log.Any(l=>l.Contains("intersection strategy used") || l.Contains("selected DomainPointStrategyIntersection")) ? "intersection" :
                    log.Any(l=>l.Contains("bisection strategy used") || l.Contains("selected DomainPointStrategyBisection")) ? "bisection" :
                    log.Any(l=>l.Contains("Fail to calculate")) ? "closest" : "iterative";
                bool accepted=false, planeAccepted=false;
                double? n=null,mx=null,my=null,deltaN=null,angle=null,planeN=null,planeMx=null,planeMy=null;
                object[] residuals=Array.Empty<object>(), planeResiduals=Array.Empty<object>();
                if (point!=null)
                {
                    n=Finite(point.NRd/1000); mx=Finite(point.MxRd/1e6); my=Finite(point.MyRd/1e6);
                    deltaN=Finite((point.NRd-force.N)/1000);
                    try {
                        var convergence=DomainPointConvergence.Evaluate(solver,force,point,c.Criterion);
                        accepted=convergence.Accepted;
                        residuals=convergence.Residuals.Select(r=>(object)new {r.Quantity,r.Unit,r.Residual,r.Tolerance,r.Accepted}).ToArray();
                        angle=convergence.Residuals.FirstOrDefault(r=>r.Quantity=="SearchAngle")?.Residual;
                        var integrated=solver.GetExternalForces(solver.CalculateForceResultantForDomain(point.StrainPlane),solver.SectionOption.ForceReferenceCoordinateSystem);
                        planeN=Finite((integrated.N-point.NRd)/1000);
                        planeMx=Finite((integrated.Mx-point.MxRd)/1e6);
                        planeMy=Finite((integrated.My-point.MyRd)/1e6);
                        var integratedPoint=new FailureDomain.FailureDomainPoint(integrated,point.FailureIndex,point.StrainPlane,point.Immersione);
                        var planeConvergence=DomainPointConvergence.Evaluate(solver,force,integratedPoint,c.Criterion);
                        planeAccepted=planeConvergence.Accepted;
                        planeResiduals=planeConvergence.Residuals.Select(r=>(object)new {r.Quantity,r.Unit,r.Residual,r.Tolerance,r.Accepted}).ToArray();
                    } catch(Exception e) { error=(error??"")+" validation: "+e.Message; }
                }
                if (write) output.WriteLine(JsonSerializer.Serialize(new { c.Id,section=c.Section.Name,diagram=c.Diagram.ToString(),state=c.State.ToString(),
                    criterion=c.Criterion.ToString(),demandN=c.N,demandMx=c.Mx,demandMy=c.My,run,cold=run==0,prepareMs,ms,
                    iterations,evaluations,bracketIterations,path,accepted,error,n,mx,my,deltaN,angle,planeN,planeMx,planeMy,residuals,planeAccepted,planeResiduals }));
            }
        }
    }
    private static double? Finite(double x)=>double.IsNaN(x)||double.IsInfinity(x)?(double?)null:x;
}
