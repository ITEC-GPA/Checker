using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Checkers.CompositeBridge.Viviani;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// The H section with an inclined web and the box girder in the bridge calculation: for N–Mx about the horizontal axis they are the equivalent H
/// (webs of total horizontal width n tw / cos α, top flanges of total width), local buckling, shear, studs and details are on the real plates
/// </summary>
[TestClass]
public class SectionTypeTests
{
    private const double Hw = 1800, Tw = 14, E = 300;

    private static HBridgeInput Base(bool class4 = false) => new()
    {
        Materials = new(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C),
        Geometry = new() { SlabWidth = 3000, SlabHeight = 250, WebHeight = Hw, WebThickness = Tw, TopWidth = 500, TopThickness = 25, BottomWidth = 700, BottomThickness = 30 },
        TopRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        Options = new() { Class4 = class4 },
        Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500, ShearKN = 300 },
            new() { Name = "G2", MomentKNm = 2000, ShearKN = 200, Phi = 2, PsiL = 1.1 },
            new() { Name = "Q", MomentKNm = 3000, ShearKN = 900 }]
    };

    private static HBridgeInput Inclined(bool class4 = false) => Base(class4) with { Geometry = Base().Geometry with { SectionType = BridgeSteelSectionType.InclinedWebH, WebOffset = E } };

    private static HBridgeInput Box(bool class4 = false, double offset = 250) => Base(class4) with
    {
        Geometry = Base().Geometry with { SectionType = BridgeSteelSectionType.Box, WebSpacing = 1800, WebOffset = offset, TopWidth = 450, BottomWidth = 1400, BottomThickness = 25 },
        Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 3000, ShearKN = 600 },
            new() { Name = "G2", MomentKNm = 4000, ShearKN = 400, Phi = 2, PsiL = 1.1 },
            new() { Name = "Q", MomentKNm = 6000, ShearKN = 1800 }]
    };

    private static void SameStresses(HBridgeAnalysisResult expected, HBridgeAnalysisResult actual, double tolerance = 1e-9)
    {
        for (int s = 0; s < expected.Stages.Count; s++)
        {
            double scale = expected.Stages[s].Points.Max(p => Math.Abs(p.Stress));
            for (int p = 0; p < expected.Stages[s].Points.Count; p++)
                Assert.AreEqual(expected.Stages[s].Points[p].Stress, actual.Stages[s].Points[p].Stress, tolerance * scale, expected.Stages[s].Points[p].Name);
        }
    }

    [TestMethod]
    public void InclinedWebGeometry()
    {
        BridgeGeometry g = HBridgeSection.Geometry(Inclined());
        double l = Math.Sqrt(Hw * Hw + E * E);
        Assert.AreEqual(BridgeSteelSectionType.InclinedWebH, g.SectionType);
        Assert.AreEqual(l, g.PlateLength, 1e-9);
        Assert.AreEqual(Tw, g.PlateThickness, 0);
        Assert.AreEqual(Math.Atan(E / Hw), g.WebAngle, 1e-12);
        Assert.AreEqual(Tw * l / Hw, g.WebThickness, 1e-12, "equivalent vertical web");
        // area and inertia of the real section of Model
        var real = new SectionHInclinedWeb(Hw + 25 + 30, Tw, 500, 25, 700, 30, E);
        Assert.AreEqual(real.Area, g.SteelArea, 1e-9 * real.Area);
        Assert.AreEqual(real.Jxx, g.SteelInertia, 1e-9 * real.Jxx);
        Assert.AreEqual(real.Centroid.Y - g.Height, g.SteelCentroid, 1e-9);
    }

    [TestMethod]
    public void InclinedWebGivesTheStressesOfTheEquivalentVerticalWeb()
    {
        var inclined = HBridgeSection.Calculate(Inclined());
        double tw = Tw * Math.Sqrt(Hw * Hw + E * E) / Hw;
        var vertical = HBridgeSection.Calculate(Base() with { Geometry = Base().Geometry with { WebThickness = tw } });
        SameStresses(vertical, inclined);
        Assert.IsTrue(inclined.Stages.Last().Warnings.Any(w => w.StartsWith("Anima inclinata")));
        Assert.IsFalse(vertical.Stages.Last().Warnings.Any(w => w.StartsWith("Anima inclinata")));
    }

    [TestMethod]
    public void InclinedWebLocalBucklingOnTheRealPlate()
    {
        // slender web: the plate is L x tw with the stresses of its ends; the strips along it become vertical heights times cos α
        var d = Inclined(class4: true) with { Geometry = Inclined().Geometry with { WebThickness = 10, WebHeight = 2400 } };
        var r = HBridgeSection.Calculate(d);
        BridgeGeometry g = r.Geometry;
        BridgeStage stage = r.Stages.Last();
        Assert.AreEqual(g.PlateLength, stage.Effective.Web.Width, 1e-9);
        Assert.AreEqual(10, stage.Effective.Web.Thickness, 0);
        Assert.IsTrue(stage.Effective.Web.Rho < 1, "the web is in class 4");
        // the strips come from the converged geometry, the plate is recalculated on the final stresses: equal within the convergence tolerance
        double cos = Math.Cos(g.WebAngle);
        Assert.AreEqual(stage.Effective.Web.EffectiveAtStart * cos, stage.Effective.WebTop, 1e-6 * g.PlateLength);
        Assert.AreEqual(stage.Effective.Web.EffectiveAtEnd * cos, stage.Effective.WebBottom, 1e-6 * g.PlateLength);
        // the same plate by hand with the stresses at the ends of the web
        double top = stage.Contributions.Sum(c => c.SteelStress(-g.TopThickness)), bottom = stage.Contributions.Sum(c => c.SteelStress(-g.TopThickness - g.WebHeight));
        BridgePlate plate = EffectivePlateReduction.InternalPlate(g.PlateLength, 10, top, bottom, 355);
        Assert.AreEqual(plate.Rho, stage.Effective.Web.Rho, 1e-6);
    }

    [TestMethod]
    public void InclinedWebShearInThePlaneOfTheWeb()
    {
        // V / cos α in the plane of a plate L x tw: the vertical resistance is cos α times the one of the plate
        var d = Inclined() with { Options = Base().Options with { Standard = BridgeStandard.Eurocode4 } };
        var r = HBridgeSection.Calculate(d);
        BridgeGeometry g = r.Geometry;
        var plate = BridgeShearConnection.Web(g.PlateLength, Tw, 355, 210000, d.Options.GammaM0, d.Options.GammaM1, d.Options.ShearEta, etaInShearArea: true);
        Assert.AreEqual(plate.Resistance * Math.Cos(g.WebAngle), r.Stages.Last().Shear!.Web.Resistance, 1e-9 * plate.Resistance);
        // average shear stress in the plate: V / (n hw tw)
        Assert.AreEqual(1400.0 * 1000 / (Hw * Tw), r.Stages.Last().Shear!.TauAverage, 1e-9);
    }

    [TestMethod]
    public void VerticalOffsetZeroIsTheH()
    {
        var h = HBridgeSection.Calculate(Base(class4: true));
        var zero = HBridgeSection.Calculate(Base(class4: true) with { Geometry = Base().Geometry with { SectionType = BridgeSteelSectionType.InclinedWebH, WebOffset = 0 } });
        SameStresses(h, zero, 1e-12);
        Assert.AreEqual(h.Stages.Last().Shear!.Web.Resistance, zero.Stages.Last().Shear!.Web.Resistance, 1e-9);
    }

    [TestMethod]
    public void BoxGeometry()
    {
        BridgeGeometry g = HBridgeSection.Geometry(Box());
        double l = Math.Sqrt(Hw * Hw + 250.0 * 250), twh = Tw * l / Hw;
        Assert.AreEqual(2, g.WebCount);
        Assert.AreEqual(2, g.TopFlangeCount);
        Assert.AreEqual(900, g.TopWidth, 0);
        Assert.AreEqual(450, g.TopFlangeWidth, 0);
        Assert.AreEqual(2 * twh, g.WebThickness, 1e-12);
        Assert.AreEqual(1800 - 500 - twh, g.BottomInternalWidth, 1e-9);
        Assert.AreEqual((1400 - 1300 - twh) / 2, g.BottomOutstandWidth, 1e-9);
        var real = new SectionSteelBox(Hw + 25 + 25, Tw, 450, 25, 1400, 25, 1800, 1300);
        Assert.AreEqual(real.Area, g.SteelArea, 1e-9 * real.Area);
        Assert.AreEqual(real.Jxx, g.SteelInertia, 1e-9 * real.Jxx);
    }

    [TestMethod]
    public void BoxGivesTheStressesOfTheEquivalentH()
    {
        var box = HBridgeSection.Calculate(Box());
        double twh = Tw * Math.Sqrt(Hw * Hw + 250.0 * 250) / Hw;
        var h = HBridgeSection.Calculate(Box() with { Geometry = Box().Geometry with { SectionType = BridgeSteelSectionType.H, WebThickness = 2 * twh, TopWidth = 900, WebOffset = 0, WebSpacing = 0 } });
        SameStresses(h, box);
        Assert.IsTrue(box.Stages.Last().Warnings.Any(w => w.StartsWith("Cassoncino")));
    }

    [TestMethod]
    public void BoxBottomFlangeIsAnInternalPlate()
    {
        // hogging: the bottom flange is compressed; internal plate between the webs (k = 4) and outstands beyond them
        var d = Box(class4: true) with
        {
            Geometry = Box().Geometry with { BottomThickness = 12, BottomWidth = 1500 },
            Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = -3000, ShearKN = 600 },
                new() { Name = "G2", Kind = BridgePhaseKind.ConcreteExcluded, MomentKNm = -6000, ShearKN = 900 }]
        };
        var r = HBridgeSection.Calculate(d);
        BridgeGeometry g = r.Geometry;
        BridgeEffective e = r.Stages.Last().Effective;
        Assert.AreEqual(g.BottomInternalWidth, e.Bottom.Width, 1e-9);
        Assert.AreEqual(4.0, e.Bottom.KSigma, 1e-9, "uniform compression of an internal plate");
        Assert.IsTrue(e.Bottom.Rho < 1);
        Assert.IsNotNull(e.BottomOutstand);
        Assert.AreEqual(g.BottomOutstandWidth, e.BottomOutstand!.Width, 1e-9);
        Assert.AreEqual(2 * g.WebHorizontalThickness + e.Bottom.EffectiveAtStart + e.Bottom.EffectiveAtEnd + 2 * e.BottomOutstand.EffectiveAtStart, e.BottomWidth, 1e-9);
        double sigma = r.Stages.Last().Contributions.Sum(c => c.SteelStress(-g.Height));
        BridgePlate plate = EffectivePlateReduction.InternalPlate(g.BottomInternalWidth, 12, sigma, sigma, 355);
        Assert.AreEqual(plate.Rho, e.Bottom.Rho, 2e-3);
    }

    [TestMethod]
    public void BoxShearAndStudsOnTwoWebsAndTwoFlanges()
    {
        var d = Box() with { Studs = new() { Enabled = true, CountPerRow = 2, Diameter = 22, Height = 150, LongitudinalPitch = 200, TransversePitch = 120, Fu = 450, GammaV = 1.25,
            HeadDiameter = 35, HeadThickness = 10, RequiredCover = 20 } };
        var r = HBridgeSection.Calculate(d);
        BridgeGeometry g = r.Geometry;
        var plate = BridgeShearConnection.Web(g.PlateLength, Tw, 355, 210000, d.Options.GammaM0, d.Options.GammaM1, d.Options.ShearEta);
        Assert.AreEqual(2 * Math.Cos(g.WebAngle) * plate.Resistance, r.Stages.Last().Shear!.Web.Resistance, 1e-9 * plate.Resistance);
        BridgeStudResult studs = r.Stages.Last().Studs!;
        // total flow on 2 flanges x 2 studs per row
        Assert.AreEqual(Math.Abs(studs.Flow) * 200 / 4 / 1000, studs.ForcePerStud, 1e-9);
        Assert.AreEqual(4 * studs.ResistancePerStud * 1000 / 200, studs.ResistancePerLength, 1e-9);
        Assert.IsTrue(r.Stages.Last().Warnings.Any(w => w.Contains("due piattabande superiori")));
    }

    [TestMethod]
    public void BoxHistoryEqualsCumulativeOnTheGrossSection()
    {
        var d = Box();
        var cumulative = HBridgeSection.Calculate(d);
        var history = HBridgeHistoryResults.Calculate(d);
        var g = cumulative.Geometry;
        // the cumulative method integrates the plates of the composite phases on their middle line (Checker): 1e-5 of the stresses
        foreach (double y in new[] { 0.0, -g.TopThickness, -g.TopThickness - g.WebHeight, -g.Height })
        {
            double expected = cumulative.Stages.Last().Contributions.Sum(c => c.SteelStress(y));
            Assert.AreEqual(expected, history.Stages.Last().Contributions.Sum(c => c.SteelStress(y)), 1e-4 * Math.Abs(expected), $"y = {y}");
        }
    }

    [TestMethod]
    public void HistoryStressesAtTheFacesOfThePlates()
    {
        // steel only: the history gives σ = M (y - yc) / I also at the faces of the plates (before, the stress of the outermost fiber, inside the plate)
        var d = Base() with { Phases = [Base().Phases[0]] };
        var history = HBridgeHistoryResults.Calculate(d);
        BridgeGeometry g = history.Geometry;
        foreach (double y in new[] { 0.0, -g.TopThickness, -g.TopThickness - g.WebHeight, -g.Height })
        {
            double navier = -1500e6 * (y - g.SteelCentroid) / g.SteelInertia;
            Assert.AreEqual(navier, history.Stages.Last().Contributions.Sum(c => c.SteelStress(y)), 1e-9 * Math.Abs(navier), $"y = {y}");
        }
    }

    [TestMethod]
    public void InvalidSectionsAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Geometry(Box() with { Geometry = Box().Geometry with { WebSpacing = 300 } }), "flanges overlapping");
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Geometry(Box() with { Geometry = Box().Geometry with { BottomWidth = 1000 } }), "webs outside the bottom flange");
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Geometry(Inclined() with { Geometry = Inclined().Geometry with { WebOffset = 2000 } }), "more than 45°");
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Geometry(Box() with { Geometry = Box().Geometry with { SecondBottomEnabled = true, SecondBottomWidth = 500, SecondBottomThickness = 20 } }));
        Assert.ThrowsException<ArgumentException>(() => VivianiComparisons.ToViviani(Box()));
    }
}
