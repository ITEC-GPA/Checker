using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

// A diagnostic replay, NOT a test runner: failed assertions are recorded and execution continues.
// The canonical net472 suite remains the regression gate.
if (args.Length != 3) throw new ArgumentException("Expected output JSON path, original source path and scenario.");
LegacyDiagnostics.Scenario = args[2];
var cases = new List<CaseReport>();
foreach (var method in typeof(SteelTests.EN1993p11Test).GetMethods()
    .Where(m => m.IsDefined(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute))
        && LegacyDiagnostics.Includes(m.Name))
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
    LegacyDiagnostics.Scenario,
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
    public static string Scenario { get; set; } = "Baseline";
    private static readonly string[] HollowCases =
        { "Sap_SectionRHSTest1", "Sap_SectionRHSTest2", "Sap_SectionCHSTest1" };

    public static bool Includes(string name) => Scenario switch
    {
        "Baseline" => true,
        "InteractionGamma105" => name == "SectionHInteraction2",
        "HollowColdFormed" or "HollowWelded" or "HollowAlpha049Only" => HollowCases.Contains(name),
        _ => throw new ArgumentException("Unknown diagnostic scenario: " + Scenario)
    };

    public static void Configure(EN1993p11Checker checker)
    {
        var section = (SteelSection)checker.BeamElement.BeamProperty;
        switch (Scenario)
        {
            case "HollowColdFormed":
                checker.BeamElement.BeamProperty = new SteelSection(section.SectionShape,
                    section.SteelMaterial, section.SectionType, Section.FormedTypes.ColdFormed);
                break;
            case "HollowWelded":
                checker.BeamElement.BeamProperty = new SteelSection(section.SectionShape,
                    section.SteelMaterial, Section.SectionTypes.Welded, section.FormedType);
                break;
            case "HollowAlpha049Only":
                // Controlled sensitivity experiment, NOT a normative preset or a production fix.
                checker.EN1993P11.AlphaImperfectionFactorForCurveA = .49;
                break;
            case "InteractionGamma105":
                checker.EN1993P11.GammaM1 = 1.05;
                break;
        }
    }

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
            Stations = checker.EN1993p11BeamStationResults.Select(Scalars).ToArray(),
            Curves = new CurveProbe(checker).SelectedCurves(section)
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

    private sealed class CurveProbe : EN1993p11Checker
    {
        public CurveProbe(EN1993p11Checker checker)
            : base(checker.BeamElement, checker.EN1993P11Options, checker.EN1993P11) { }
        public object SelectedCurves(SteelSection section)
            => new { Axis1 = GetBucklingCurve1Axis(section).ToString(), Axis2 = GetBucklingCurve2Axis(section).ToString() };
    }
}

public static class DiagnosticAssert
{
    public static void IsTrue(bool condition, string? message = null,
        [CallerArgumentExpression("condition")] string expression = "", [CallerLineNumber] int line = 0)
        => Record(condition, expression, message, line);

    public static void IsFalse(bool condition, string? message = null,
        [CallerArgumentExpression("condition")] string expression = "", [CallerLineNumber] int line = 0)
        => Record(!condition, "!(" + expression + ")", message, line);

    public static void AreEqual<T>(T first, T second, string? message = null,
        [CallerArgumentExpression("first")] string firstExpression = "",
        [CallerArgumentExpression("second")] string secondExpression = "", [CallerLineNumber] int line = 0)
        => Record(EqualityComparer<T>.Default.Equals(first, second),
            firstExpression + " == " + secondExpression, message, line);

    public static void AreEqual(double first, double second, double tolerance, string? message = null,
        [CallerArgumentExpression("first")] string firstExpression = "",
        [CallerArgumentExpression("second")] string secondExpression = "", [CallerLineNumber] int line = 0)
        => LegacyDiagnostics.Current.Assertions.Add(new(
            double.IsFinite(first) && double.IsFinite(second) && Math.Abs(first - second) <= tolerance,
            firstExpression + " == " + secondExpression + (message is null ? "" : " : " + message),
            first, second, tolerance, line));

    public static void Contains(string value, string substring, string? message = null,
        [CallerArgumentExpression("value")] string valueExpression = "",
        [CallerArgumentExpression("substring")] string substringExpression = "", [CallerLineNumber] int line = 0)
        => Record(value is not null && substring is not null && value.Contains(substring, StringComparison.Ordinal),
            valueExpression + " contains " + substringExpression, message, line);

    private static void Record(bool passed, string expression, string? message, int line)
        => LegacyDiagnostics.Current.Assertions.Add(new(passed,
            expression + (message is null ? "" : " : " + message), null, null, null, line));
}

// Discovery markers for this isolated replay; no replacement is made in the test project.
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestClassAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestMethodAttribute : Attribute { }
}
