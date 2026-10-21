using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.Seepage;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

/// <summary>October 2026 review of seepage and hydraulic checks. Expected values: closed forms (Harr 1962, Van Genuchten-Mualem,
/// NTC 2018 §6.2.4.2) from tools/attesi/paratie_revisione/filtrazione/testcases.py.</summary>
[TestClass]
public class SeepageIndependentTests
{
    [TestMethod]
    public void SheetPileInFiniteLayerMatchesHarr()
    {
        // Harr: cutoff s = T/2 in a layer T = 10 m, H = 4 m, k = 0.01 mm/s: Q/kH = 0.5, head H/2 below the tip by antisymmetry.
        var soil = new SeepageMaterial("s", .01, .01, 0, "given");
        var d = new EmbeddedWallSeepageDomain { CaseId = "harr", LeftExtent = 80000, RightExtent = 80000, Bottom = -10000,
            WallTop = 0, WallBottom = -5000, WallThickness = 20, MaximumElementLength = 500,
            LeftGround = 0, RightGround = 0, LeftHead = 4000, RightHead = 0, LeftLayers = [new(0, -10000, soil)], RightLayers = [new(0, -10000, soil)] };
        var r = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        Assert.AreEqual(20, r.Inflow, .03 * 20, "P1 mesh 500 mm: +2.1 % versus Harr.");
        Assert.AreEqual(0, r.Balance, 1e-6);
        Assert.AreEqual(2000, SeepageWallCoupling.PressureAt(r, 0, -5000) / SoilUnits.WaterUnitWeight - 5000, .01);
    }

    [TestMethod]
    public void VanGenuchtenMualemMatchesFiftyDigitArithmetic()
    {
        foreach (var (alpha, n, suction, se, kr) in new[] { (.001, 1.5, -3000.0, .5444516301110671, 2.395834263404739e-3),
            (.01, 3, -1e5, 9.999999993333334e-7, 4.444444435555555e-22), (.0005, 1.2, -20000, .6245558358115804, 8.134864271519904e-5) })
        {
            var c = UnsaturatedConductivity.At(new(alpha, n), suction);
            Assert.AreEqual(se, c.EffectiveSaturation, 1e-10 * se); Assert.AreEqual(kr, c.RelativeConductivity, 1e-10 * kr);
        }
    }

    [TestMethod]
    public void ProfileZeroCrossingControlsTheSaturatedWeight()
    {
        var model = SeepageTests.Rectangle(2, 4); model.Heads = model.Nodes.Select((p, i) => new SeepageHead(i, 600)).ToArray();
        var field = SeepageAnalysis.Calculate(model); Assert.IsTrue(field.IsComplete, field.Diagnostic);
        var profile = SeepageWallCoupling.Profile(field, 500, 1000, 0, true);
        var expected = new[] { (1000.0, 0.0), (750, 0), (600, 0), (500, 9.81e-4), (250, 3.4335e-3), (0, 5.886e-3) };
        Assert.AreEqual(expected.Length, profile.Count);
        for (int i = 0; i < expected.Length; i++)
        { Assert.AreEqual(expected[i].Item1, profile[i].Elevation, 1e-9); Assert.AreEqual(expected[i].Item2, profile[i].Pressure, 1e-12); }
        var soil = new EmbeddedWallSoil { Soil = new Soil("s", 18e-6, 20e-6, Math.PI / 6, 0, "given"), Top = 1000, Bottom = 0,
            AtRestCoefficient = .5, HorizontalModulus = .001, StiffnessSource = "given" };
        var p = WallSoilPressure.At([soil], new() { Elevation = 1000, SaturationFromPorePressures = true, PorePressures = profile, PorePressureSource = "FEM" }, 0);
        // Saturated only below the zero crossing at 600 mm: 18·0.4 + 20·0.6 kN/m² = 19.2 kPa (19.5 kPa without the crossing).
        Assert.AreEqual(.0192, p.VerticalTotal, 1e-12); Assert.AreEqual(.005886, p.Water, 1e-12); Assert.AreEqual(.013314, p.VerticalEffective, 1e-12);
    }

    [TestMethod]
    public void FreeOutflowGradientsUseNtc2018ResistanceFactors()
    {
        // ic = (γsat − γw)/γw with γsat 20 kN/m³; γR = 3 on the mean and 2 on the exit gradient.
        double ic = (20e-6 - SoilUnits.WaterUnitWeight) / SoilUnits.WaterUnitWeight;
        var input = new HydraulicGradientInput { CaseId = "K", StageId = "F", ElementId = "Colonna", SaturatedUnitWeight = 20e-6,
            UpwardGradient = .3, Measure = HydraulicGradientMeasure.Mean, PredominantlyVerticalFlow = true, FreeOutflow = true, ApplicabilitySource = "review" };
        var mean = HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(ic / 3, mean.Capacity!.Value, 1e-12); Assert.AreEqual(.866437684004, mean.Utilization!.Value, 1e-11);
        Assert.AreEqual(EngineeringOutcome.Satisfied, mean.Outcome);
        input.Measure = HydraulicGradientMeasure.Exit; input.UpwardGradient = .6;
        var exit = HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(ic / 2, exit.Capacity!.Value, 1e-12); Assert.AreEqual(1.155250245339, exit.Utilization!.Value, 1e-11);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, exit.Outcome);
    }
}
