using GPC.Checkers.Geotechnics;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeotechnicsTests;

/// <summary>Design soil parameters and action factors from the Model geotechnical standards (N, mm, MPa, rad).</summary>
[TestClass]
public class DesignValuesTests
{
    private static readonly Soil Clay = new("Clay", 19 * SoilUnits.KiloNewtonPerCubicMetre, 20 * SoilUnits.KiloNewtonPerCubicMetre, 26 * SoilUnits.Degree,
        10 * SoilUnits.KiloPascal, "test report", undrainedShearStrength: 70 * SoilUnits.KiloPascal);

    // M2 (NTC 2018 Tab. 6.2.II): tan φ'd = tan 26° / 1.25 = 0.39020 → φ'd = 21.31°; c'd = 10/1.25 = 8 kPa; cu,d = 70/1.4 = 50 kPa.
    [TestMethod]
    public void MaterialFactorsGiveTheDesignParameters()
    {
        var ntc = new StandardNTC2018Geotechnics();
        var m2 = new DesignSoil(Clay, ntc.MaterialSet("M2"));
        Assert.AreEqual(21.31, m2.FrictionAngle / SoilUnits.Degree, .01);
        Assert.AreEqual(8 * SoilUnits.KiloPascal, m2.EffectiveCohesion, 1e-15);
        Assert.AreEqual(50 * SoilUnits.KiloPascal, m2.UndrainedShearStrength!.Value, 1e-15);
        Assert.AreEqual(Clay.UnitWeight, m2.UnitWeight);
        var m1 = new DesignSoil(Clay, ntc.MaterialSet("M1"));
        Assert.AreEqual(Clay.FrictionAngle, m1.FrictionAngle, 1e-15);
        var sand = new DesignSoil(new Soil("Sand", 1.8e-5, 2e-5, .55, 0, "r"), ntc.MaterialSet("M2"));
        Assert.IsNull(sand.UndrainedShearStrength, "missing cu stays missing");
    }

    [TestMethod]
    public void ActionFactorsByCategoryAndSet()
    {
        var a1 = new StandardNTC2018Geotechnics().ActionSet("A1");
        Assert.AreEqual(1.3, GeotechnicalActions.Factor(a1, GeotechnicalActionCategory.Permanent, true));
        Assert.AreEqual(1.0, GeotechnicalActions.Factor(a1, GeotechnicalActionCategory.Permanent, false));
        Assert.AreEqual(1.5, GeotechnicalActions.Factor(a1, GeotechnicalActionCategory.NonStructuralPermanent, true));
        Assert.AreEqual(0, GeotechnicalActions.Factor(a1, GeotechnicalActionCategory.Variable, false));
        var en = new StandardEN1997p1().ActionSet("A1");
        Assert.AreEqual(1.35, GeotechnicalActions.Factor(en, GeotechnicalActionCategory.Permanent, true));
    }

    [TestMethod]
    public void UndefinedChecksAreNotSupportedNeverReplaced()
    {
        var en = new StandardEN1997p1();
        Assert.ThrowsException<NotSupportedException>(() => GeotechnicalActions.Required(en, GeotechnicalCheck.SlopeStability, GeotechnicalSituation.Seismic));
        Assert.ThrowsException<NotSupportedException>(() => GeotechnicalActions.Required(en, GeotechnicalCheck.PileTransverse, GeotechnicalSituation.PersistentTransient));
        Assert.AreEqual(2, GeotechnicalActions.Required(en, GeotechnicalCheck.SlopeStability, GeotechnicalSituation.PersistentTransient).Count);
        Assert.AreEqual("A1+M1+R3", GeotechnicalActions.Required(new StandardNTC2018Geotechnics(), GeotechnicalCheck.PileTransverse, GeotechnicalSituation.PersistentTransient).Single().Name);
    }
}
