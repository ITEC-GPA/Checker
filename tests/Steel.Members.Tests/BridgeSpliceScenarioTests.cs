using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Synthetic typological screening, not a design for an actual bridge.</summary>
[TestClass]
public class BridgeSpliceScenarioTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (bool ntc in new[] { false, true })
        foreach (var category in new[] { SpliceBoltCategory.B, SpliceBoltCategory.C })
        foreach (string scenario in new[] { "compact", "unequal-parent", "reverse", "axial", "high-shear", "web-24", "web-12", "wide-outer", "slender-parent", "overload" })
            yield return new object[] { ntc, category, scenario };
    }
    internal static WeldedIBeamSpliceForceInput Detail(string scenario, double scale = 1)
    {
        var a = scenario == "axial" ? new PlanarConnectionActions(2e5 * scale, 0, 0, 0, 0, 0)
            : scenario == "high-shear" ? new(2e5 * scale, -4e5 * scale, 0, 0, 0, 20e6 * scale)
            : new(2e5 * scale, -1e5 * scale, 0, 0, 0, 50e6 * scale);
        var d = WeldedIBeamSpliceStandardTests.Detail(a, scenario == "unequal-parent");
        var s = scenario == "slender-parent" ? new WeldedIBeamSpliceSection(d.Section.TopFlange, new(320, 3, 355, 510), d.Section.BottomFlange, 8, 8) : d.Section;
        var top = scenario == "wide-outer" ? new SpliceFlangeCoverDetail(new(360, 20, 355, 510), d.Top.EachInner, d.Top.InnerGap, d.Top.EndDistance, d.Top.Bolts) : d.Top;
        var web = scenario == "web-24" ? new ConnectionPlate(280, 24, 355, 510) : scenario == "web-12" ? new(280, 12, 355, 510) : d.EachWebCover;
        return new(s, top, d.Bottom, web, d.WebBolts, d.WebCoverEnd, d.ButtGap, a);
    }
    internal static WeldedIBeamSpliceStandardInput Workflow(bool ntc, SpliceBoltCategory category, string scenario)
    {
        double scale = scenario == "reverse" ? -1 : scenario == "overload" ? 40 : 1;
        var detail = Detail(scenario, scale);
        var parent = WeldedIBeamSpliceStandardTests.Parents(WeldedIBeamSpliceStandardTests.Input(ntc, detail));
        var slips = new[] { new WeldedSpliceSlipCase(category == SpliceBoltCategory.B ? "SLS" : "ULS.slip",
            WeldedIBeamSpliceSlipTests.Input(ntc, category == SpliceBoltCategory.B ? BoltSlipLimitState.Serviceability : BoltSlipLimitState.Ultimate,
                Detail(scenario, category == SpliceBoltCategory.B ? scale / 2 : scale)), category == SpliceBoltCategory.C ? "ULS" : null) };
        return new(new[] { new WeldedSpliceStandardCase("ULS", parent) }, category, slips);
    }
    [DataTestMethod, DynamicData(nameof(Cases), DynamicDataSourceType.Method)]
    public void BaselineRecordsSupportedAndExcludedTypologies(bool ntc, SpliceBoltCategory category, string scenario)
    {
        var r = WeldedIBeamSpliceStandard.Verify(Workflow(ntc, category, scenario));
        bool unsupported = scenario is "web-12" or "wide-outer" or "slender-parent" || scenario == "web-24" && ntc;
        Assert.AreEqual(!unsupported, r.IsStaticResistanceComplete, string.Join(";", r.MissingRequirements));
        if (unsupported) Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        if (scenario == "overload") Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        if (scenario is "compact" or "unequal-parent" or "reverse" or "axial") Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        string? path = Environment.GetEnvironmentVariable("GPC_SPLICE_SCENARIO_AUDIT");
        if (!string.IsNullOrWhiteSpace(path))
            File.AppendAllText(path, $"{scenario}\t{(ntc ? "NTC2018" : "EC3-2005")}\t{category}\t{r.Status}\t{r.GoverningSource?.Component}\t{string.Join(" | ", r.MissingRequirements)}{Environment.NewLine}");
    }
}
