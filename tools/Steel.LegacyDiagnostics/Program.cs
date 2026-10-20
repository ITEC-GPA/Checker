using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.Sections.Steel;

// A diagnostic replay, NOT a test runner: failed assertions are recorded and execution continues.
// The canonical net472 suite remains the regression gate.
if (args.Length != 2) throw new ArgumentException("Expected output JSON path and original source path.");
var cases = new List<CaseReport>();
foreach (var method in typeof(SteelTests.EN1993p11Test).GetMethods()
    .Where(m => m.IsDefined(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute)))
    .OrderBy(m => m.Name))
{
    var report = new CaseReport { Name = method.Name };
    LegacyDiagnostics.Current = report;
    try { method.Invoke(new SteelTests.EN1993p11Test(), null); }
    catch (TargetInvocationException ex) { report.Exception = ex.InnerException?.ToString() ?? ex.ToString(); }
    cases.Add(report);
}
var output = new
{
    Purpose = "Diagnostic replay, not certification; expected values are unchanged.",
    Units = "N, mm, Nmm, MPa. Positive N denotes tension.",
    Runtime = Environment.Version.ToString(),
    SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),
    Assemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => !a.IsDynamic && a.GetName().Name!.StartsWith("GPC"))
        .Select(a => new { Name = a.FullName, a.Location,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray(),
    Cases = cases
};
File.WriteAllText(args[0], JsonSerializer.Serialize(output, new JsonSerializerOptions
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
}));
Console.WriteLine($"Recorded {cases.Count} cases, {cases.Sum(c => c.Assertions.Count)} assertions; " +
    $"{cases.Count(c => c.Exception != null || c.Assertions.Any(a => !a.Passed))} cases with discrepancies.");

public sealed class CaseReport
{
    public string Name { get; init; } = "";
    public List<object> Checks { get; } = new();
    public List<AssertionReport> Assertions { get; } = new();
    public string? Exception { get; set; }
}
public sealed record AssertionReport(bool Passed, string Expression, double? First,
    double? Second, double? Tolerance, int GeneratedLine);

public static class LegacyDiagnostics
{
    public static CaseReport Current { get; set; } = null!;

    public static void Capture(EN1993p11Checker checker)
    {
        var section = (SteelSection)checker.BeamElement.BeamProperty;
        Current.Checks.Add(new
        {
            Checker = Scalars(checker), Material = Scalars(checker.Material),
            Standard = Scalars(checker.EN1993P11), Options = Scalars(checker.EN1993P11Options),
            Section = Scalars(section), Shape = Scalars(section.SectionShape),
            ShapeType = section.SectionShape.GetType().FullName,
            Errors = checker.GetErrorLog().Select(e => e.ToString()).ToArray(),
            Stations = checker.EN1993p11BeamStationResults.Select(Scalars).ToArray()
        });
    }

    private static Dictionary<string, object?> Scalars(object value)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(decimal)) continue;
            try
            {
                var item = property.GetValue(value);
                result[property.Name] = type.IsEnum ? item?.ToString() : item;
            }
            catch (Exception ex) { result[property.Name] = $"Getter failed: {ex.Message}"; }
        }
        return result;
    }
}

public static class DiagnosticAssert
{
    public static void IsTrue(bool condition, [CallerArgumentExpression("condition")] string expression = "",
        [CallerLineNumber] int line = 0)
        => LegacyDiagnostics.Current.Assertions.Add(new(condition, expression, null, null, null, line));

    public static void AreEqual(double first, double second, double tolerance,
        [CallerArgumentExpression("first")] string firstExpression = "",
        [CallerArgumentExpression("second")] string secondExpression = "", [CallerLineNumber] int line = 0)
        => LegacyDiagnostics.Current.Assertions.Add(new(
            double.IsFinite(first) && double.IsFinite(second) && Math.Abs(first - second) <= tolerance,
            firstExpression + " == " + secondExpression, first, second, tolerance, line));
}

// Discovery markers for this isolated replay; no replacement is made in the test project.
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestClassAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestMethodAttribute : Attribute { }
}
