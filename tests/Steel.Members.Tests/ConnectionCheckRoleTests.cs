using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>One role per check drives the outcome rules; ids and component names are labels only.</summary>
[TestClass]
public class ConnectionCheckRoleTests
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SpliceComponentsDeclareTheRoleOfEveryCheck(bool ntc)
    {
        var r = WeldedIBeamSpliceStandard.Verify(BridgeSpliceApplicationTests.Create(ntc, SpliceBoltCategory.B));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        foreach (var c in r.Cases)
        {
            Assert.IsTrue(c.Components.Detailing.Detailing.Checks.All(x => x.Role == ConnectionCheckRole.Detailing));
            Assert.IsTrue(c.Model.Checks.All(x => x.Role == ConnectionCheckRole.Applicability));
            Assert.IsTrue(c.AdditionalPlates.BlockHierarchy.Checks.All(x => x.Role == ConnectionCheckRole.Applicability));
            Assert.IsTrue(c.AdditionalPlates.CompactPlateDomain.Checks.All(x => x.Role == ConnectionCheckRole.Applicability));
            Assert.IsTrue(c.AdditionalPlates.DirectBlockTearing.Checks.All(x => x.Role == ConnectionCheckRole.Resistance));
            Assert.IsTrue(c.Components.Bolts.ComponentAssessment.Checks.All(x => x.Role == ConnectionCheckRole.Resistance));
        }
        var sls = r.SlipCases.Single(s => s.Input.Input.LimitState == BoltSlipLimitState.Serviceability);
        Assert.IsTrue(sls.Assessment.Checks.Count > 0);
        Assert.IsTrue(sls.Assessment.Checks.All(x => x.Role == ConnectionCheckRole.Serviceability));
        var report = BridgeSpliceReport.FromBolted(r);
        Assert.IsTrue(report.Rows.Where(x => x.ConnectionCheck?.Role == ConnectionCheckRole.Detailing).All(x => x.Role == SpliceReportRole.Applicability));
        Assert.IsTrue(report.Rows.Where(x => x.Component == "Detailing").Any());
        Assert.AreEqual(SpliceReportRole.Resistance, report.GoverningResistance.Role);
    }

    [TestMethod]
    public void ComposedIdentifiersKeepTheRole()
    {
        var r = WeldedIBeamSpliceStandard.Verify(BridgeSpliceApplicationTests.Create(false, SpliceBoltCategory.B));
        // The slip component composes child bolt-slip identifiers: the serviceability role survives.
        var slip = r.SlipCases.Single(s => s.Input.Input.LimitState == BoltSlipLimitState.Serviceability).Assessment;
        Assert.IsTrue(slip.Checks.All(x => x.Id.Contains('.') && x.Role == ConnectionCheckRole.Serviceability));
    }
}
