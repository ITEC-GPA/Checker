using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using CompositeBridgePerformance;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;

string Arg(string name, string fallback) {
    int i = Array.IndexOf(args, "--" + name);
    return i < 0 ? fallback : i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing " + name);
}
if (args.Contains("--profile-history")) {
    foreach (bool reduced in new[] { false, true }) {
        var cases = BridgePerformanceCases.Station(0, reduced);
        _ = HBridgeHistoryResults.Calculate(cases[0]);
        for (int repeat = 0; repeat < 3; repeat++) {
            foreach (bool full in new[] { false, true }) {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before = GC.GetTotalAllocatedBytes(true); double checksum = 0;
                var watch = Stopwatch.StartNew();
                foreach (var d in cases) {
                    var h = full ? HBridgeHistoryResults.Calculate(d).Stages.Last().GetHistory()!.State
                        : HBridgeHistoryAnalysis.Calculate(d).Stages.Last();
                    checksum += h.TotalPlane.AxialStrain + h.TotalPlane.Curvature;
                }
                watch.Stop();
                Console.WriteLine(JsonSerializer.Serialize(new { Class4 = reduced, FullResult = full, Repeat = repeat,
                    WallMs = watch.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetTotalAllocatedBytes(true) - before, Checksum = checksum }));
            }
        }
    }
    return;
}
string method = Arg("method", "cumulative"), execution = Arg("execution", "serial");
bool class4 = bool.Parse(Arg("class4", "true"));
int stations = int.Parse(Arg("stations", "1")), repetitions = int.Parse(Arg("repetitions", "3"));
int degree = int.Parse(Arg("degree", Math.Min(8, Environment.ProcessorCount).ToString()));
string output = Path.GetFullPath(Arg("out", "benchmark.json")), reference = Arg("reference", "");
if (method is not ("cumulative" or "history") || execution is not ("serial" or "stations" or "variables")
    || stations < 1 || repetitions < 1 || degree < 1) throw new ArgumentException("Invalid benchmark options.");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
if (File.Exists(output) || File.Exists(Path.ChangeExtension(output, ".values.bin")))
    throw new IOException("Benchmark outputs already exist. Choose a new iteration directory; references must not be overwritten.");
var setup = Stopwatch.StartNew();
var inputs = Enumerable.Range(0, stations).Select(s => BridgePerformanceCases.Station(s, class4)).ToArray();
setup.Stop();
var settings = new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Linear };
var parallel = new ParallelOptions { MaxDegreeOfParallelism = degree };

// Full public analysis, all four stages retained by the API. Projection is included and identical for all runs.
double[] Values(HBridgeAnalysisResult result) {
    var values = new List<double>();
    foreach (var stage in result.Stages) {
        if (!double.IsFinite(stage.Residual) || stage.Residual > 1e-7) throw new InvalidOperationException("Effective equilibrium failure.");
        if (method == "cumulative") {
            foreach (var contribution in stage.Contributions)
                if (!double.IsFinite(contribution.EquilibriumResidual) || contribution.EquilibriumResidual > 1e-5)
                    throw new InvalidOperationException("Force equilibrium failure.");
        } else {
            var h = stage.GetHistory()!.State;
            double length = Math.Max(1, h.Fibers.Max(f => f.Fiber.Y) - h.Fibers.Min(f => f.Fiber.Y));
            double fn = settings.Solver.ForceTolerance + settings.Solver.RelativeTolerance * Math.Max(Math.Abs(h.N), Math.Abs(h.MomentAtOrigin) / length);
            double fm = settings.Solver.MomentTolerance + settings.Solver.RelativeTolerance * Math.Max(Math.Abs(h.MomentAtOrigin), Math.Abs(h.N) * length);
            if (Math.Abs(h.ForceResidual) > fn || Math.Abs(h.MomentResidual) > fm) throw new InvalidOperationException("History equilibrium failure.");
        }
        // The same fixed physical locations. History adds extrema with load-dependent ordering; keep those covered in unit tests instead.
        values.AddRange(stage.Points.Take(8).Select(p => p.Stress));
        values.AddRange(new[] { stage.Effective.TopWidth, stage.Effective.BottomWidth, stage.Effective.WebTop, stage.Effective.WebBottom });
    }
    if (values.Any(v => !double.IsFinite(v))) throw new InvalidOperationException("Non-finite benchmark result.");
    return values.ToArray();
}

double[][][] Run() {
    var result = new double[stations][][];
    void Station(int s) {
        var list = inputs[s]; var rows = new double[list.Length][];
        if (method == "cumulative" && execution != "variables") {
            var batch = HBridgeSection.Calculate(list);
            for (int i = 0; i < list.Length; i++) rows[i] = Values(batch[i].Analysis ?? throw batch[i].Error!);
        } else {
            void Combo(int i) => rows[i] = Values(method == "cumulative" ? HBridgeSection.Calculate(list[i]) : HBridgeHistoryResults.Calculate(list[i], settings));
            if (execution == "variables") Parallel.For(0, list.Length, parallel, Combo);
            else for (int i = 0; i < list.Length; i++) Combo(i);
        }
        result[s] = rows;
    }
    if (execution == "stations") Parallel.For(0, stations, parallel, Station);
    else for (int s = 0; s < stations; s++) Station(s);
    return result;
}
double[] Flatten(double[][][] data) => data.SelectMany(s => s).SelectMany(c => c).ToArray();
double[]? baseline = null;
if (reference.Length > 0) {
    using var reader = new BinaryReader(File.OpenRead(reference));
    baseline = new double[reader.ReadInt32()];
    for (int i = 0; i < baseline.Length; i++) baseline[i] = reader.ReadDouble();
    if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing reference data.");
}
double largestError = 0; long changedBits = 0;
void Compare(double[] expected, double[] actual) {
    if (expected.Length != actual.Length) throw new InvalidOperationException("Result length changed.");
    for (int i = 0; i < actual.Length; i++) {
        if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i])) changedBits++;
        double error = Math.Abs(expected[i] - actual[i]); largestError = Math.Max(largestError, error);
        // Stresses MPa / effective lengths mm: absolute floor 1e-7 and relative 2e-8, fixed before optimization.
        if (error > 1e-7 + 2e-8 * Math.Abs(expected[i])) throw new InvalidOperationException($"Numerical regression at {i}: {expected[i]:R} -> {actual[i]:R}");
    }
}
var measurements = new List<object>();
double[]? first = null;
for (int repeat = 0; repeat <= repetitions; repeat++) {
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long allocated = GC.GetTotalAllocatedBytes(true);
    int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
    using var process = Process.GetCurrentProcess();
    var cpu = process.TotalProcessorTime; var watch = Stopwatch.StartNew();
    var result = Run();
    watch.Stop(); var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
    long bytes = GC.GetTotalAllocatedBytes(true) - allocated;
    var values = Flatten(result);
    if (baseline is not null) Compare(baseline, values);
    if (first is null) first = values; else Compare(first, values);
    measurements.Add(new { Repeat = repeat, FirstExecution = repeat == 0, WallMs = watch.Elapsed.TotalMilliseconds, CpuMs = cpuMs,
        AllocatedBytes = bytes, Gen0 = GC.CollectionCount(0) - gen0, Gen1 = GC.CollectionCount(1) - gen1, Gen2 = GC.CollectionCount(2) - gen2 });
    Console.WriteLine($"{method} class4={class4} {stations}x100 {execution} run={repeat}: {watch.Elapsed.TotalMilliseconds:F1} ms wall; {cpuMs:F1} ms CPU; {bytes / 1048576d:F1} MiB allocated");
}
string vectorFile = Path.ChangeExtension(output, ".values.bin");
using (var writer = new BinaryWriter(File.Create(vectorFile))) {
    writer.Write(first!.Length); foreach (double value in first) writer.Write(value);
}
string sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(vectorFile)));
File.WriteAllText(output, JsonSerializer.Serialize(new {
    Dataset = "G1-G2-Ritiro-Q-v1", Method = method, Class4 = class4, Stations = stations, Combinations = 100,
    Execution = execution, Degree = degree, Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
    LogicalProcessors = Environment.ProcessorCount, SetupMs = setup.Elapsed.TotalMilliseconds,
    TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"), ServerGC = GCSettings.IsServerGC,
    Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
    BridgeVersion = typeof(HBridgeSection).Assembly.GetName().Version!.ToString(),
    Reference = reference, LargestAbsoluteDifference = largestError, ChangedBits = changedBits,
    ValuesFile = vectorFile, ValuesSha256 = sha, Measurements = measurements
}, new JsonSerializerOptions { WriteIndented = true }));
