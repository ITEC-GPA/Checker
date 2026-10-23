using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

// Independent analytical/published references, N/mm/MPa.
// See tests/Validation/ConcreteBridge/SOURCES.txt; no ANTHEA or baseline regeneration.
[TestClass]
public class CriticalBranchReferenceTests
{
    private static void Close(double expected, double actual, string label)
        => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), label);

    [TestMethod]
    public void Jrc2012BridgeStudMatchesBothPublishedFailureModes()
    {
        // JRC68415, 6.7.1, printed pp.156-157, PDF178-179. SLU only: SLE arithmetic is inconsistent.
        var r = BridgeShearConnection.Stud(22, 200, 450, 35, 34077.14);
        Close(1, r.Alpha, "alpha");
        Assert.AreEqual(109.5, r.SteelResistance / 1000, .05, "published steel, kN");
        Assert.AreEqual(122.6, r.ConcreteResistance / 1000, .05, "published concrete, kN");
        Close(.8 * 450 * Math.PI * 22 * 22 / 4 / 1.25, r.Resistance, "steel governs");
        Assert.AreEqual(438, 4 * r.Resistance / 1000, .2, "four studs, published rounding");
    }

    [DataTestMethod]
    [DataRow(16.0, 3.0, .8)] [DataRow(22.0, 3.0, .8)] [DataRow(25.0, 3.0, .8)]
    [DataRow(16.0, 3.9999, .99998)] [DataRow(22.0, 4.0, 1.0)] [DataRow(25.0, 4.0001, 1.0)]
    public void StudHeightTransitionMatchesTheConcreteFailureBranch(double d, double hd, double alpha)
    {
        // Low concrete strength makes the concrete branch govern on both sides of h/d=4.
        var r = BridgeShearConnection.Stud(d, hd * d, 450, 20, 30000);
        double concrete = .29 * alpha * d * d * Math.Sqrt(20 * 30000) / 1.25;
        Close(alpha, r.Alpha, "alpha"); Close(concrete, r.ConcreteResistance, "concrete");
        Close(concrete, r.Resistance, "governing concrete failure");
    }

    [DataTestMethod]
    [DataRow(16.0)] [DataRow(22.0)] [DataRow(25.0)]
    public void StudJustBelowMinimumHeightIsRejected(double d)
        => Assert.ThrowsException<ArgumentException>(() => BridgeShearConnection.Stud(d, 2.9999 * d, 450, 35, 34000));

    [DataTestMethod]
    [DataRow(-1.0, 0.0)] [DataRow(-.999, 0.0)] [DataRow(-.5, 0.0)] [DataRow(0.0, 0.0)]
    [DataRow(.5, 0.0)] [DataRow(.999, 0.0)] [DataRow(1.0, 0.0)]
    [DataRow(-1.0, 137.0)] [DataRow(-.999, 137.0)] [DataRow(-.5, 137.0)] [DataRow(0.0, 137.0)]
    [DataRow(.5, 137.0)] [DataRow(.999, 137.0)] [DataRow(1.0, 137.0)]
    public void AxialMomentDomainMatchesClosedFormIncludingTranslatedPole(double n, double y0)
    {
        // Exact fully plastic rectangular section: M/Mpl = +/- (1-(N/Npl)^2).
        // With tensile-positive N and M=-integral(sigma*y), translate M by -N*y0.
        const double b = 200, h = 400, fy = 355;
        double npl = b * h * fy, mpl = b * h * h * fy / 4, axial = n * npl;
        var r = BridgeBendingShear.AtAxialForce(new[] { new BridgeBendingShear.Block(y0 - h / 2, y0 + h / 2, b, fy, fy) }, axial);
        Assert.IsTrue(r.Feasible);
        double resistance = mpl * (1 - n * n);
        // Absolute tolerance based on full capacity remains meaningful at the zero-capacity endpoints.
        Assert.AreEqual(-resistance - axial * y0, r.Minimum, mpl * 1e-10, "negative bound");
        Assert.AreEqual(resistance - axial * y0, r.Maximum, mpl * 1e-10, "positive bound");
        Close(y0 + n * h / 2, r.AxisPositive, "positive neutral axis");
        Close(y0 - n * h / 2, r.AxisNegative, "negative neutral axis");
        Close(npl, r.Compression, "compression limit"); Close(npl, r.Tension, "tension limit");
    }

    [DataTestMethod]
    [DataRow(-1.000001)] [DataRow(1.000001)]
    public void AxialForceOutsidePlasticDomainIsInfeasible(double n)
    {
        var r = BridgeBendingShear.AtAxialForce(new[] { new BridgeBendingShear.Block(-200, 200, 200, 355, 355) }, n * 200 * 400 * 355);
        Assert.IsFalse(r.Feasible);
    }

    [DataTestMethod]
    [DataRow(.8, .4999, .8)] [DataRow(.8, .5, .8)] [DataRow(.8, .5001, .800000016)]
    [DataRow(.5999, .9, .5999)] [DataRow(.6, .9, .6)] [DataRow(.6001, .9, .8561)]
    [DataRow(-.8, .8, .944)] [DataRow(.8, -.8, .944)] [DataRow(-.8, -.8, .944)]
    [DataRow(.8, 1.0, 1.2)]
    public void BendingShearUsesBothThresholdsAndAbsoluteDemands(double m, double v, double expected)
    {
        // Reference interaction, Mf/Mpl=.6: eta=|m| or |m|+.4*(2|v|-1)^2.
        // Both |v|>.5 AND |m|>.6 are required. No continuity is assumed at the flange threshold.
        Close(expected, BridgeBendingShear.Interaction(m * 1e9, 1e9, .6e9, v * 1e6, 1e6), "interaction");
    }

    [DataTestMethod]
    [DataRow(.4999, .8)] [DataRow(.5, .8)] [DataRow(.5001, .80000004)]
    [DataRow(.8, 1.16)] [DataRow(-.8, 1.16)] [DataRow(1.0, 1.8)]
    public void ElasticInteractionHasNoPlasticFlangeReserve(double v, double expected)
        => Close(expected, BridgeBendingShear.ElasticInteraction(.8, v * 1e6, 1e6), "elastic envelope");
}
