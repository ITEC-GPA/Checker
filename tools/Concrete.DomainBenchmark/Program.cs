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
        SectionSolver.FailureAnalysisTypes Criterion, double N, double Mx, double My, double MeshSize = 0);

    private static IEnumerable<Case> MeshCases()
    {
        foreach (var s in Sections().Where(s => s.Diameter == 0))
        foreach (var state in DomainPointContractTests.States)
        foreach (int divisions in new[] { 4, 12, 24 })
        foreach (int angle in new[] { 0, 30, 90 })
        foreach (var criterion in new[] { SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureAnalysisTypes.ConstantEccentricity })
        {
            double size = Math.Max(s.Width, s.Height), theta = angle * Math.PI / 180;
            yield return new Case($"Mesh/{s.Name}/ParabolaRectangle/{state}/{criterion}/{divisions}/{angle}", s,
                ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle, state, criterion, -100,
                .005 * size * Math.Cos(theta), .005 * size * Math.Sin(theta), size / divisions);
        }
    }

    private static IEnumerable<DomainPointContractTests.BenchSection> Sections()
    {
        var slender = new DomainPointContractTests.BenchSection { Name="S1",Width=1000,Height=200,Fck=25,
            Bars=Enumerable.Range(0,5).SelectMany(i=>new[] {new[] {-400+200.0*i,-60.0,12.0},new[] {-400+200.0*i,60.0,12.0}}).ToArray(),
            AxialKn=new[] {400.0,0.0,221.277,-1000.0,-2500.0} };
        return DomainPointContractTests.Sections.Concat(new[] {slender});
    }

    private static IEnumerable<Case> FocusCases(ConcreteMaterial.CompressionStressStrainDiagrams diagram = ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle)
    {
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
        if (args.Length == 3 && args[0] == "replay") return Replay(args[1], args[2]);
        if (args.Length < 1) throw new ArgumentException("Output JSONL path required; optional repeats (default 5), filter, catalogue (all/focus).");
        int repeats = args.Length > 1 ? int.Parse(args[1]) : 5;
        string catalogue=args.Length>3 ? args[3] : "all";
        if (catalogue!="all" && catalogue!="focus" && catalogue!="stress-small" && catalogue!="mesh") throw new ArgumentException("Unknown catalogue: "+catalogue);
        var catalogueCases = catalogue=="mesh" ? MeshCases() : catalogue=="focus" ? FocusCases() : catalogue=="stress-small"
            ? FocusCases(ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock).Where(c=>c.Id.Contains("/Plastic/ConstantEccentricity/-1/0.05/")) : Cases();
        var cases = catalogueCases.Where(c => args.Length < 3 || (args[2].StartsWith("!") ? !c.Id.Contains(args[2].Substring(1)) : c.Id.Contains(args[2]))).ToArray();
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
            if (c.MeshSize > 0) section.SetMeshSize(c.MeshSize);
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
                    iterations,evaluations,bracketIterations,path,accepted,error,n,mx,my,deltaN,angle,planeN,planeMx,planeMy,residuals,planeAccepted,planeResiduals,
                    meshSize=c.MeshSize, meshElements=section.Mesh.Faces.Count,
                    strainPlane=point==null ? null : new { x=point.StrainPlane.ReferencePoint.X,y=point.StrainPlane.ReferencePoint.Y,
                        epsilon=point.StrainPlane.StrainReferencePoint,chiX=point.StrainPlane.ChiX,chiY=point.StrainPlane.ChiY } }));
            }
        }
    }
    private static double? Finite(double x)=>double.IsNaN(x)||double.IsInfinity(x)?(double?)null:x;

    // Reintegrate planes from an earlier executable with the current integrator. This checks accuracy
    // across integration methods, rather than treating a legacy quadrature snapshot as exact geometry.
    private static int Replay(string source, string destination)
    {
        if (File.Exists(destination)) throw new IOException("Replay output already exists: " + destination);
        var catalogue = Cases().Concat(FocusCases()).Concat(MeshCases()).Concat(FocusCases(ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock))
            .GroupBy(c=>c.Id).ToDictionary(g=>g.Key,g=>g.First());
        using var output = new StreamWriter(destination);
        foreach (string line in File.ReadLines(source))
        {
            using var document=JsonDocument.Parse(line); var row=document.RootElement;
            if (row.GetProperty("run").GetInt32()!=1 || !row.TryGetProperty("strainPlane",out var p) || p.ValueKind==JsonValueKind.Null) continue;
            var c=catalogue[row.GetProperty("Id").GetString()];
            var section=DomainPointContractTests.BuildSection(c.Section,c.Diagram);
            if (c.MeshSize > 0) section.SetMeshSize(c.MeshSize);
            var solver=DomainPointContractTests.BuildChecker(section,c.State).SectionSolver;
            var plane=new StrainPlane(p.GetProperty("chiX").GetDouble(),p.GetProperty("chiY").GetDouble(),
                new Point2d(p.GetProperty("x").GetDouble(),p.GetProperty("y").GetDouble()),p.GetProperty("epsilon").GetDouble());
            var demand=DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section),c.N,c.Mx,c.My);
            var f=solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane),solver.SectionOption.ForceReferenceCoordinateSystem);
            var point=new FailureDomain.FailureDomainPoint(f,SectionSolver.FailureZones.F3A,plane,0);
            var convergence=DomainPointConvergence.Evaluate(solver,demand,point,c.Criterion);
            var concrete=solver.IntegrateSectionStress(plane);
            output.WriteLine(JsonSerializer.Serialize(new {c.Id,oldAccepted=row.GetProperty("accepted").GetBoolean(),
                oldPlaneAccepted=row.GetProperty("planeAccepted").GetBoolean(),geometricPlaneAccepted=convergence.Accepted,
                n=f.N/1000,mx=f.Mx/1e6,my=f.My/1e6,concreteN=concrete.N/1000,
                minStrain=section.Mesh.Vertices.Min(v=>plane.GetStrain(v.Point.X,v.Point.Y)),
                maxStrain=section.Mesh.Vertices.Max(v=>plane.GetStrain(v.Point.X,v.Point.Y)),
                threshold=section.ConcreteMaterial.StrainYCompression,
                residuals=convergence.Residuals.Select(r=>new {r.Quantity,r.Residual,r.Tolerance,r.Accepted}).ToArray()}));
        }
        return 0;
    }
}
