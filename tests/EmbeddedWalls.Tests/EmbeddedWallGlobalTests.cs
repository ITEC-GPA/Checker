using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class EmbeddedWallGlobalTests
{
    internal static EmbeddedWallModel Model()
    {
        EmbeddedWallSoil Layer() => new() { Top = 1000, Bottom = -20000, Soil = new Soil("silt", 18e-6, 20e-6, Math.PI / 6, .005, "given", .04),
            HorizontalModulus = .001, AtRestCoefficient = .5, StiffnessSource = "given" };
        return new() { CaseId = "K", Name = "continuous wall", Top = 1000, Bottom = -5000, TributaryWidth = 2000,
            Segments = [new() { Top = 1000, Bottom = -5000, FlexuralRigidity = 2e14, WeightPerLength = 30, SectionId = "R", Source = "600 mm wall, gamma=25 kN/m3, width 2 m" }],
            LeftSoils = [Layer()], RightSoils = [Layer()], Stages = [new() { Id = "F2", Left = new() { Elevation = 1000, Surcharge = .01 },
                Right = new() { Elevation = -1000, Surcharge = .005 }, DistributedLoad = 2, PointLoads = [new() { Elevation = 0, HorizontalForce = 10000, DownwardForce = 20000, Moment = 6e6 }] }] };
    }
    private static EmbeddedWallGlobalProfile Profile(int direction = 1, bool confirmed = true) => new(600, 20000, 20000, direction, confirmed,
        "Continuous diaphragm, characteristic input", new SlopeSearch(-16000, -1000, 1000, 16000, 6500, 15000, 3, 20, 0));
    private static SlopeFactors Factors(EmbeddedWallGlobalPrepared prepared) => SlopeFactors.FromCombination(
        new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single(), 0, 0, false,
        prepared.Section.Loads.ToDictionary(l => l.Id, _ => 1d));

    [TestMethod]
    public void StageGeometryWeightsAndExternalActionsRetainUnitsSignsAndOwnership()
    {
        var model = Model(); var prepared = EmbeddedWallGlobalStability.Prepare(model, "F2", Profile()); var section = prepared.Section;
        Assert.AreEqual(1000, prepared.ReferenceElevation); Assert.AreEqual("F2", prepared.StageId); Assert.AreEqual("K", prepared.CaseId);
        Assert.AreEqual(-2000, section.Surface[0].Y); Assert.AreEqual(0, section.Surface.Last().Y);
        var body = section.Bodies.Single(); var properties = SlopeGeometry.Properties(body.Polygon);
        Assert.AreEqual(600 * 6000, properties.Area, 1e-6); Assert.AreEqual(25e-6, body.UnitWeight, 1e-15);
        Assert.AreEqual(90, properties.Area * body.UnitWeight, 1e-12, "N/mm: total 180 kN in a 2 m strip.");
        var point = section.Loads.Single(l => l.Id == "PointLoad-0");
        Assert.AreEqual(-1000, point.Y); Assert.AreEqual(10, point.Vertical); Assert.AreEqual(5, point.Horizontal); Assert.AreEqual(3000, point.Moment);
        var distributed = section.Loads.Single(l => l.Id == "StructureDistributed");
        Assert.AreEqual(6, distributed.Horizontal); Assert.AreEqual(-3000, distributed.Y);
        Assert.AreEqual(.005, section.Loads.Single(l => l.Id == "RightSurcharge").Vertical);
        Assert.AreEqual(.01, section.Loads.Single(l => l.Id == "LeftSurcharge").Vertical);
        model.Stages[0].PointLoads[0].HorizontalForce = 99000; model.Segments[0].WeightPerLength = 1;
        Assert.AreEqual(5, point.Horizontal); Assert.AreEqual(25e-6, body.UnitWeight, 1e-15);
        var factors = Factors(prepared); Assert.AreEqual(1.25, factors.TanFrictionAngle); Assert.AreEqual(1.1, factors.ResistanceFactor);
        var result = EmbeddedWallGlobalStability.Calculate(prepared, [factors]);
        Assert.AreEqual(1, result.Cases.Count); Assert.IsTrue(result.Cases[0].Tried > 0);
    }

    [TestMethod]
    public void MirroringDirectionReversesHorizontalForcesAndGeneralizedCouplesOnly()
    {
        var right = Model(); var left = Model(); var stage = left.Stages[0]; (stage.Left, stage.Right) = (stage.Right, stage.Left);
        stage.DistributedLoad = -stage.DistributedLoad; stage.PointLoads[0].HorizontalForce = -stage.PointLoads[0].HorizontalForce;
        stage.PointLoads[0].Moment = -stage.PointLoads[0].Moment;
        var a = EmbeddedWallGlobalStability.Prepare(right, "F2", Profile(1)); var b = EmbeddedWallGlobalStability.Prepare(left, "F2", Profile(-1));
        CollectionAssert.AreEqual(a.Section.Surface.ToArray(), b.Section.Surface.ToArray());
        for (int i = 0; i < a.Section.Loads.Count; i++)
        {
            var x = a.Section.Loads[i]; var y = b.Section.Loads[i];
            Assert.AreEqual(x.Left, y.Left); Assert.AreEqual(x.Right, y.Right); Assert.AreEqual(x.Y, y.Y);
            Assert.AreEqual(x.Horizontal, y.Horizontal); Assert.AreEqual(x.Vertical, y.Vertical); Assert.AreEqual(x.Moment, y.Moment);
        }
    }

    [TestMethod]
    public void GlobalCaseAppliesA2M2R2OnceWithExplicitVariableCombinationFactors()
    {
        var prepared = EmbeddedWallGlobalStability.Prepare(Model(), "F2", Profile());
        var standard = new StandardNTC2018Geotechnics();
        var combination = standard.Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single();
        GeotechnicalActionSelection[] selections = [
            new("LeftSurcharge", GeotechnicalCharacteristicActionCategory.Variable, true, .3, "psi from specified synthetic combination"),
            new("RightSurcharge", GeotechnicalCharacteristicActionCategory.PermanentNonStructural, false, 1, "favourable in specified combination"),
            new("StructureDistributed", GeotechnicalCharacteristicActionCategory.PermanentStructural, true, 1, "permanent"),
            new("PointLoad-0", GeotechnicalCharacteristicActionCategory.Variable, true, 1, "leading")];
        var result = EmbeddedWallGlobalStability.CreateCase(prepared, combination, selections, false);
        Assert.AreEqual(.39, result.Factors.Loads["LeftSurcharge"], 1e-15);
        // NTC 2018, Table 6.2.I (Gazzetta Ufficiale 20/02/2018, p. 186): favourable G2 in A2 = 0.8.
        Assert.AreEqual(.8, result.Factors.Loads["RightSurcharge"]);
        Assert.AreEqual(1, result.Factors.Loads["StructureDistributed"]); Assert.AreEqual(1.3, result.Factors.Loads["PointLoad-0"]);
        Assert.AreEqual(1.25, result.Factors.TanFrictionAngle); Assert.AreEqual(1.25, result.Factors.EffectiveCohesion);
        Assert.AreEqual(1.1, result.Factors.ResistanceFactor); Assert.AreEqual(1.3, result.Actions[0].PartialFactor);
        Assert.AreEqual(.3, result.Actions[0].Selection.CombinationFactor); Assert.IsTrue(result.Actions.All(a => a.Selection.Source.Length > 0));
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallGlobalStability.CreateCase(prepared, combination, selections.Take(3).ToArray(), false));
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.CreateCase(prepared,
            standard.Combinations(GeotechnicalCheck.EmbeddedWallResistance, GeotechnicalSituation.PersistentTransient)[0], selections, false));
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.CreateCase(prepared,
            standard.Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.Seismic)[0], selections, false));
        Assert.ThrowsException<ArgumentException>(() => new GeotechnicalActionSelection("G", GeotechnicalCharacteristicActionCategory.PermanentStructural, false, .3, "invalid permanent psi"));
    }
    [TestMethod]
    public void UnsupportedGlobalSystemsAndMissingLoadFactorsAreNotSilentlyIgnored()
    {
        var m = Model();
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(m, "F2", Profile(confirmed: false)));
        m.Stages[0].Left.WaterElevation = 0; m.Stages[0].Right.WaterElevation = -1500;
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(m, "F2", Profile()), "No guessed piezometric interpolation below the wall.");
        m.Stages[0].Left.WaterElevation = -1500;
        var wet = EmbeddedWallGlobalStability.Prepare(m, "F2", Profile()); Assert.IsTrue(wet.Section.Water.All(p => p.Y == -2500));
        m.Stages[0].Left.WaterElevation = 2000;
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(m, "F2", Profile()), "Ponded boundary water requires external loads.");
        m = Model(); m.FixBottomDisplacement = true;
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(m, "F2", Profile()));
        m.FixBottomDisplacement = false; m.Supports = [new() { Id = "T", Elevation = 0, AxialStiffness = 1000 }];
        m.Stages[0].ActiveSupports = [new() { SupportId = "T" }];
        Assert.ThrowsException<NotSupportedException>(() => EmbeddedWallGlobalStability.Prepare(m, "F2", Profile()), "No local anchor reaction counted as an external global load.");
        var dry = EmbeddedWallGlobalStability.Prepare(Model(), "F2", Profile());
        var missing = SlopeFactors.FromCombination(new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single(), 0, 0, false);
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallGlobalStability.Calculate(dry, [missing]));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => EmbeddedWallGlobalStability.Calculate(dry, [Factors(dry)], cancellation.Token));
    }
}
