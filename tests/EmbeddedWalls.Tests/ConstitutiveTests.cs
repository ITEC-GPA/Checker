using GPC.Checkers.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class ConstitutiveTests
{
    [TestMethod]
    public void PlasticCycleHasResidualOffsetAndElasticUnloading()
    {
        // Independent arithmetic: p0=0.05 MPa, k=0.01 MPa/mm, [0.02,0.20].
        var first = WallConstitutiveLaw.Evaluate(20, 0, .01, .05, .02, .20);
        Assert.AreEqual(.20, first.Pressure, 1e-14);
        Assert.AreEqual(5, first.PlasticOffset, 1e-12);
        var unload = WallConstitutiveLaw.Evaluate(10, first.PlasticOffset, .01, .05, .02, .20);
        Assert.AreEqual(.10, unload.Pressure, 1e-14);
        Assert.AreEqual(.01, unload.Tangent, 1e-14);
        Assert.AreEqual(5, unload.PlasticOffset, 1e-12);
        var active = WallConstitutiveLaw.Evaluate(0, unload.PlasticOffset, .01, .05, .02, .20);
        Assert.AreEqual(.02, active.Pressure, 1e-14);
        Assert.AreEqual(3, active.PlasticOffset, 1e-12);
        Assert.AreEqual(5, first.PlasticOffset, 1e-12, "Trial evaluations cannot change earlier results.");
    }

    [TestMethod]
    public void InvalidBoundsAndNonFiniteInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => WallConstitutiveLaw.Evaluate(0, 0, 0, 1, 0, 2));
        Assert.ThrowsException<ArgumentException>(() => WallConstitutiveLaw.Evaluate(0, 0, 1, 1, 2, 1));
        Assert.ThrowsException<ArgumentException>(() => WallConstitutiveLaw.Evaluate(double.NaN, 0, 1, 1, 0, 2));
    }
}
