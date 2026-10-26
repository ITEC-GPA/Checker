using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass]
public class SmallLinearLoadTests
{
    [DataTestMethod]
    [DataRow(2e-5, 0d)] [DataRow(-2e-5, 0d)]
    [DataRow(0d, 2e-5)] [DataRow(0d, -2e-5)]
    [DataRow(2e-4, 1e-4)] [DataRow(-2e-4, -1e-4)]
    [DataRow(2e-3, 1d)] [DataRow(-2e-3, -1d)]
    [DataRow(2d, 1000d)] [DataRow(-2d, -1000d)] [DataRow(0d, 0d)]
    public void SmallResultantsRetainIndependentEquilibrium(double forceN, double momentNmm)
    {
        var source = CompositeBridgeApiTests.Input();
        foreach (bool class4 in new[] { false, true })
        foreach (double phi in new[] { 0d, 2d })
        {
            var input = source with {
                Options = source.Options with { Class4 = class4 },
                Phases = [new() { Name = "Small FEM resultants", ForceKN = forceN / 1000, MomentKNm = momentNmm / 1e6,
                    Reference = BridgeLoadReference.GrossCentroid, Phi = phi }]
            };
            var contribution = HBridgeSection.Calculate(input).Stages.Single().Contributions.Single();
            double n = contribution.UniformStress * contribution.Area;
            double m = -contribution.StressSlope * contribution.SolverInertia - n * (contribution.Centroid - contribution.LoadY);
            Assert.AreEqual(forceN, n, 1e-10 * Math.Max(1, Math.Abs(forceN)), "Integrated N [N]");
            Assert.AreEqual(momentNmm, m, 1e-9 * Math.Max(1, Math.Abs(momentNmm)), "Integrated M [Nmm]");
        }
    }
}
