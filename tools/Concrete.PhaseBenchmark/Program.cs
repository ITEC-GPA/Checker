using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;

internal static class Program
{
    private static readonly CoordinateSystem Axes = new CoordinateSystem(Point3d.Origin, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
    internal static SectionChecker CreateChecker(bool linear = false)
    {
        var material = new ConcreteMaterialEN1992("C35", 35, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterial("B450", 200000, 450, 450, .075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        var outline = new Polygon2d(new[] { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) });
        var section = new ReinforcedConcreteSection(new Shape2d(outline, new Polygon2d[0]), material);
        foreach (double y in new[] { -250.0, 250.0 }) foreach (double x in new[] { -150.0, -50.0, 50.0, 150.0 })
            section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(20, steel), new Point2d(x, y)) });
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(Axes, SectionSolver.FailureAnalysisTypes.ConstantN,
            SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, linear, 32);
        return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options,
            new StandardNTC2018Concrete { AlphaCC = .85, GammaC = 1.5, GammaS = 1.15 }, false);
    }
    internal static ConcretePhase[] Phases(int index)
    {
        double scale = .7 + (index % 23) / 30.0;
        var phases = new List<ConcretePhase>();
        for (int k = 0; k < 2; k++)
        {
            phases.Add(ConcretePhase.Load("load " + k, new ResultBeamForces(-200000 * scale, 0, 0, 0, 4e6 * scale, -2e6 * scale, Axes)));
            phases.Add(ConcretePhase.ShrinkageMicrostrain("shrink " + k, -40));
            phases.Add(ConcretePhase.Load("unload " + k, new ResultBeamForces(40000 * scale, 0, 0, 0, -1e6 * scale, .5e6 * scale, Axes)));
        }
        return phases.Take(4 + index % 3).ToArray();
    }
    private static double Percentile(IEnumerable<double> values, double percentile)
    { var a = values.OrderBy(x => x).ToArray(); return a[(int)Math.Ceiling(percentile * a.Length) - 1]; }

    public static void Main(string[] args)
    {
        if (args.Contains("profile")) { Profile(); return; }
        string output = args.Length > 0 ? args[0] : "phase-benchmark.json";
        int count = args.Length > 1 ? int.Parse(args[1]) : 240;
        const int repetitions = 7;
        var inputs = Enumerable.Range(0, count).Select(Phases).ToArray();
        var prepared = CreateChecker();
        var modes = new List<(string Name, Func<(ConcretePhaseAnalysisResult[] Results, double[] Times)> Run)>();
        (ConcretePhaseAnalysisResult[], double[]) Serial(bool fresh)
        {
            var results = new ConcretePhaseAnalysisResult[count]; var times = new double[count];
            for (int i = 0; i < count; i++)
            {
                var watch = Stopwatch.StartNew(); results[i] = ConcretePhaseAnalysis.Calculate(fresh ? CreateChecker() : prepared, inputs[i]);
                times[i] = watch.Elapsed.TotalMilliseconds;
            }
            return (results, times);
        }
        modes.Add(("serial-prepared", () => Serial(false)));
        modes.Add(("serial-fresh", () => Serial(true)));
#if BATCH
        var histories = inputs.Select((p, i) => new ConcretePhaseHistory("H" + i, p)).ToArray();
        var requests = histories.Select(h => new ConcretePhaseBatchRequest(h, () => CreateChecker())).ToArray();
        foreach (int degree in new[] { 1, 2, 4, Math.Min(8, Environment.ProcessorCount) }.Distinct())
        {
            int d = degree;
            modes.Add(("same-section-d" + d, () => Extract(ConcretePhaseAnalysis.CalculateBatch(histories, () => CreateChecker(), d))));
            modes.Add(("different-sections-d" + d, () => Extract(ConcretePhaseAnalysis.CalculateBatch(requests, d))));
        }
#endif
        var report = new List<object>(); var reference = new List<double[]>();
        foreach (var mode in modes)
        {
            mode.Run(); mode.Run(); // JIT and caches outside measurement.
            var elapsed = new List<double>(); var allocation = new List<double>(); var latency = new List<double>();
            long iterations = 0; double maxResidual = 0;
            for (int run = 0; run < repetitions; run++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long bytes = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
                var result = mode.Run(); watch.Stop();
                elapsed.Add(watch.Elapsed.TotalMilliseconds); allocation.Add(GC.GetTotalAllocatedBytes(true) - bytes); latency.AddRange(result.Times);
                int responseIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var r = result.Results[i]; if (!r.Completed) throw new Exception(mode.Name + ": " + r.Message);
                    iterations += r.InitialIterations + r.Phases.Sum(p => p.Iterations);
                    for (int j = 0; j < r.Phases.Count; j++)
                    {
                        var s = r.Phases[j].State; maxResidual = Math.Max(maxResidual, s.NormalizedResidual);
                        var values = new[] { s.TotalStrain.Epsilon, s.TotalStrain.ChiX, s.TotalStrain.ChiY,
                            s.IntegratedForces.N, s.IntegratedForces.M1, s.IntegratedForces.M2, s.Points.Min(p => p.Stress), s.Points.Max(p => p.Stress) };
                        if (report.Count == 0 && run == 0) reference.Add(values);
                        else
                        {
                            var expected = reference[responseIndex];
                            for (int k = 0; k < values.Length; k++)
                                if (Math.Abs(values[k] - expected[k]) > (k < 3 ? 1e-11 : k < 6 ? .01 : 1e-6)) throw new Exception("Numerical batch mismatch: " + mode.Name);
                        }
                        responseIndex++;
                    }
                }
            }
            var row = new { mode = mode.Name, medianBatchMs = Percentile(elapsed, .5), p95BatchMs = Percentile(elapsed, .95),
                medianItemMs = Percentile(latency, .5), p95ItemMs = Percentile(latency, .95), medianAllocatedBytes = Percentile(allocation, .5),
                averageIterationsPerHistory = (double)iterations / repetitions / count, maxNormalizedResidual = maxResidual };
            report.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonSerializer.Serialize(new { count, phases = "4,5,6", repetitions, processors = Environment.ProcessorCount,
            runtime = Environment.Version.ToString(), report, reference }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Profile()
    {
        var checker = CreateChecker(); var context = new SectionSolver.PhaseSection(checker.SectionSolver);
        double[] q = { -.0007, .0002, -.0001 };
        var f = new GPC.Checkers.Concrete.Helper.ForceTuple(-1e6, 10e6, -4e6);
        void Measure(string name, Action action)
        {
            for (int i = 0; i < 20; i++) action();
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) action();
            Console.WriteLine(name + ": ms/10000=" + clock.Elapsed.TotalMilliseconds + ", bytes/call=" + (GC.GetAllocatedBytesForCurrentThread() - bytes) / 10000.0);
        }
        Measure("force integration", () => context.Evaluate(q, -.00016, out _));
        Measure("force axes conversion", () => context.External(f));
        Measure("law recognition", () => checker.SectionSolver.TryGetParabolaLaw(out _));
        Measure("rebar stress", () => checker.SectionSolver.CalculateStressRebar(checker.SectionSolver.ConcreteSection.GetRebars()[0], -.0007));
        context.Evaluate(q, -.00016, out var integrated);
        var plane = GPC.Checkers.Concrete.Analysis.SectionStrain.From(context.Plane(q));
        Measure("force defensive copy", () => new GPC.Checkers.Concrete.Analysis.SectionAnalysisInput(context.External(integrated)));
        var state = context.State(q, -.00016, integrated, integrated, null, true, 0);
        Console.WriteLine("response points=" + state.Points.Count);
        Measure("state snapshot", () => context.State(q, -.00016, integrated, integrated, null, true, 0));
    }
#if BATCH
    private static (ConcretePhaseAnalysisResult[], double[]) Extract(ConcretePhaseBatchResult result)
    {
        if (!result.Completed) throw new Exception("Batch failure: " + string.Join("; ", result.Items.Where(i => !i.Completed).Select(i => i.ErrorMessage ?? i.Result?.Message)));
        return (result.Items.Select(i => i.Result).ToArray(), result.Items.Select(i => i.ElapsedMilliseconds).ToArray());
    }
#endif
}
