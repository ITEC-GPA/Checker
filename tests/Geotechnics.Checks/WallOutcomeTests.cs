using System.Globalization;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeotechnicsTests;

[TestClass]
public class WallOutcomeTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(0d, 0d, EngineeringOutcome.Satisfied)]
    [DataRow(1d, 0d, EngineeringOutcome.NotSatisfied)]
    [DataRow(-1d, 2d, EngineeringOutcome.Satisfied)]
    [DataRow(2d, 1d, EngineeringOutcome.NotSatisfied)]
    public void AFiniteCheckRetainsItsEngineeringMeaning(double demand, double resistance, EngineeringOutcome expected)
    {
        var check = WallCheck.Of(WallCheckKind.Sliding, "C", demand, resistance);
        Assert.AreEqual(expected, check.Outcome);
        Assert.AreEqual(ExecutionStatus.Completed, check.Execution);
        Assert.AreEqual(DataStatus.Ready, check.Data);
    }

    [TestMethod]
    public void MissingResistanceDoesNotPassEvenWithAPositiveLabel()
    {
        var check = WallCheck.Of(WallCheckKind.Bearing, "C", 0, null, "Soddisfatta");
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, check.Outcome);
        Assert.AreEqual(ExecutionStatus.NotExecuted, check.Execution);
        Assert.AreEqual(DataStatus.Insufficient, check.Data);
    }

    [TestMethod]
    public void NonfiniteLegacyValuesNeverCertifyTheNewOutcome()
    {
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            foreach (var check in new[] { WallCheck.Of(WallCheckKind.Sliding, "C", invalid, 1), WallCheck.Of(WallCheckKind.Sliding, "C", 1, invalid) })
            {
                Assert.AreEqual(EngineeringOutcome.NotEvaluated, check.Outcome);
                Assert.AreEqual(ExecutionStatus.NotExecuted, check.Execution);
                Assert.AreEqual(DataStatus.Insufficient, check.Data);
            }
        }
    }

    [DataTestMethod]
    [DataRow(true, EngineeringOutcome.Satisfied)]
    [DataRow(false, EngineeringOutcome.NotSatisfied)]
    public void ContactAndLossOfEquilibriumAreEvaluatedOutcomes(bool stable, EngineeringOutcome expected)
    {
        var check = Contact(stable);
        Assert.AreEqual(expected, check.Outcome);
        Assert.AreEqual(DataStatus.Ready, check.Data);
        Assert.AreEqual(ExecutionStatus.Completed, check.Execution);
        if (!stable) Assert.IsNull(check.Ratio, "A missing ratio does not conceal loss of equilibrium");
    }

    static WallCheck Contact(bool stable) => RetainingWallAnalysis.Calculate(
        stable ? WallEdgeCaseTests.Wall() : WallEdgeCaseTests.Wall(toe: .01, heel: .01, gamma: 1),
        new[] { WallEdgeCaseTests.Row(WallLimitState.Ultimate) }).Checks.Single(c => c.Kind == WallCheckKind.Contact);

    [TestMethod]
    public void CaptureLegacyWallValuesAndLabels()
    {
        var checks = new[] {
            WallCheck.Of(WallCheckKind.Sliding, "zero", 0, 0),
            WallCheck.Of(WallCheckKind.Sliding, "zero-resistance", 1, 0),
            WallCheck.Of(WallCheckKind.Sliding, "pass", -1, 2),
            WallCheck.Of(WallCheckKind.Sliding, "fail", 2, 1),
            WallCheck.Of(WallCheckKind.Bearing, "unavailable", 1, null), Contact(true), Contact(false) };
        Assert.AreEqual(WallCheckStatus.ContactInCompression, checks[5].Status);
        Assert.AreEqual(WallCheckStatus.LossOfEquilibrium, checks[6].Status);
        string Number(double? n) => n.HasValue ? n.Value.ToString("R", CultureInfo.InvariantCulture) : "null";
        string? explicitPath = Environment.GetEnvironmentVariable("GPC_F4_WALL_CAPTURE");
        string target = explicitPath ?? Path.Combine(TestContext.TestResultsDirectory!, "wall-legacy.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var writer = new StreamWriter(new FileStream(target, FileMode.CreateNew, FileAccess.Write)))
            foreach (var c in checks) writer.WriteLine(string.Join("\t", c.Kind, c.Combination,
                Number(c.Demand), Number(c.Resistance), Number(c.Ratio), c.Status, c.Message));
        TestContext.AddResultFile(target);
    }
}
