using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass]
public class AnalyticConcreteEnvelopeTests
{
    private static ConcreteMaterialEN1992 Material() => new("C25", 25, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

    [TestMethod]
    public void AnalyticEnvelopeHasTheExactStressAndConsistentTangent()
    {
        var material = Material(); var law = new HistoryModelEnvelopeLaw(material);
        foreach (double e in new[] { 0, -.000125, -.0013, -.002, -.0035 })
        {
            var result = law.Evaluate(e, law.InitialState(), 1);
            double r = e / -.002;
            Assert.AreEqual(e <= -.002 ? -25 : -25 * r * (2 - r), result.Stress, 1e-12);
            Assert.AreEqual(e <= -.002 ? 0 : 25000 * (1 - r), result.Tangent, 1e-9);
            Assert.AreEqual(material.GetStress(e), result.Stress, 0);
            if (e < 0 && e > -.002)
            {
                const double h = 1e-9;
                double derivative = (law.Evaluate(e + h, law.InitialState(), 1).Stress - law.Evaluate(e - h, law.InitialState(), 1).Stress) / (2 * h);
                Assert.AreEqual(derivative, result.Tangent, 1e-5);
            }
        }
        Assert.ThrowsException<HistoryMaterialRangeException>(() => law.Evaluate(-.0035001, law.InitialState(), 1));
    }

    [TestMethod]
    public void HistoricalSelectionIsExplicitAndExistingEnvelopeSnapshotsRemainUnchanged()
    {
        var material = Material(); var analytic = new HistoryModelEnvelopeLaw(material);
#pragma warning disable CS0618
        material.ParabolaRectangleMethod = ConcreteParabolaRectangleMethod.TabulatedLegacy;
#pragma warning restore CS0618
        var historical = new HistoryModelEnvelopeLaw(material);
        const double e = -.000125;
        double expected = material.StressStrainTableCompression.GetStress(e);
        Assert.AreEqual(expected, historical.Evaluate(e, historical.InitialState(), 1).Stress, 0);
        Assert.AreEqual(-25 * (2.0 / 16 - 1.0 / 256), analytic.Evaluate(e, analytic.InitialState(), 1).Stress, 0);
        material.StressStrainTableCompression.SetStress(-2, 1);
        Assert.AreEqual(expected, historical.Evaluate(e, historical.InitialState(), 1).Stress, 0);
    }
}
