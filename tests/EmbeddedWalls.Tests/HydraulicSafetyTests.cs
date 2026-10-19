using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.Seepage;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class HydraulicSafetyTests
{
    [TestMethod]
    public void CancellationFromProgressPreventsPublishingEvenAnInitiallyEquilibratedField()
    {
        var model = SeepageTests.Rectangle(3, 4); model.Heads = model.Nodes.Select((p, i) => new SeepageHead(i, 2000)).ToArray();
        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsException<OperationCanceledException>(() => SeepageAnalysis.Calculate(model, cancellation: cancellation.Token, progress: new CancelProgress(cancellation)));
    }
    private sealed class CancelProgress(CancellationTokenSource cancellation) : IProgress<SeepageProgress>
    { public void Report(SeepageProgress value) => cancellation.Cancel(); }
    [TestMethod]
    public void UpliftUsesCharacteristicActionsAndDoesNotFactorRdAgain()
    {
        var input = new HydraulicUpliftInput { CaseId = "K1", StageId = "F2", ElementId = "Tampone", AdditionalDesignResistance = 3000,
            ResistanceSource = "Rd assegnata nel collaudo, già con M2", CharacteristicActions = [
                new() { Id = "Acqua", UpwardForce = 15000, Source = "integrale netto caratteristico" },
                new() { Id = "Peso", UpwardForce = -20000, Source = "peso totale" },
                new() { Id = "G2", Category = HydraulicActionCategory.PermanentNonStructural, UpwardForce = -3000, Source = "carico permanente" },
                new() { Id = "Qup", Category = HydraulicActionCategory.Variable, UpwardForce = 1000, Source = "variabile" },
                new() { Id = "Qdown", Category = HydraulicActionCategory.Variable, UpwardForce = -100000, Source = "variabile favorevole esclusa" }] };
        var r = HydraulicSafety.Uplift(input, new StandardNTC2018Geotechnics());
        // Independent NTC balance: 1.1*15000+1.5*1000=18000; .9*20000+.8*3000+3000=23400.
        Assert.AreEqual(18000, r.Demand!.Value, 1e-9); Assert.AreEqual(23400, r.Capacity!.Value, 1e-9);
        Assert.AreEqual(18000.0 / 23400, r.Utilization!.Value, 1e-12); Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome);
        Assert.AreEqual(0, r.Actions.Last().Factor); Assert.AreEqual("F2", r.StageId); Assert.AreEqual("K1", r.CaseId);
        input.ResistanceSource = ""; Assert.AreEqual(EngineeringOutcome.NotEvaluated, HydraulicSafety.Uplift(input, new StandardNTC2018Geotechnics()).Outcome);
        input.AdditionalDesignResistance = 0; input.CharacteristicActions = [input.CharacteristicActions[0]];
        r = HydraulicSafety.Uplift(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, r.Outcome); Assert.AreEqual(0, r.Capacity); Assert.IsNull(r.Utilization);
    }

    [TestMethod]
    public void MeanAndExitGradientsHaveDifferentSafetyFactors()
    {
        var input = new HydraulicGradientInput { CaseId = "K", StageId = "F", ElementId = "Colonna", SaturatedUnitWeight = 2 * SoilUnits.WaterUnitWeight,
            UpwardGradient = .4, Measure = HydraulicGradientMeasure.Mean, PredominantlyVerticalFlow = true, FreeOutflow = true,
            ApplicabilitySource = "Colonna omogenea satura verticale, superficie libera, caso analitico" };
        var r = HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(1.0 / 3, r.Capacity!.Value, 1e-12); Assert.AreEqual(1.2, r.Utilization!.Value, 1e-12); Assert.AreEqual(EngineeringOutcome.NotSatisfied, r.Outcome);
        input.Measure = HydraulicGradientMeasure.Exit; r = HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(.5, r.Capacity); Assert.AreEqual(.8, r.Utilization); Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome);
        input.PredominantlyVerticalFlow = false; r = HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics());
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Capacity);
        input.PredominantlyVerticalFlow = true; input.FreeOutflow = false;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics()).Outcome);
        input.FreeOutflow = true; input.SaturatedUnitWeight = SoilUnits.WaterUnitWeight;
        Assert.ThrowsException<ArgumentException>(() => HydraulicSafety.FreeOutflow(input, new StandardNTC2018Geotechnics()));
    }

    [TestMethod]
    public void LoadedExitUsesExcessPressureAndUnseepedEffectiveStress()
    {
        var input = new HydraulicLoadedExitInput { CaseId = "K", StageId = "F", ElementId = "Punto", PorePressure = .03, HydrostaticPressure = .01,
            EffectiveStressWithoutSeepage = .03, LoadedOutflow = true, PredominantlyVerticalFlow = true, ApplicabilitySource = "Frontiera caricata; flusso verticale" };
        var ntc = new StandardNTC2018Geotechnics(); var r = HydraulicSafety.LoadedOutflow(input, ntc);
        Assert.AreEqual(.02, r.Demand!.Value, 1e-12); Assert.AreEqual(.015, r.Capacity!.Value, 1e-12);
        Assert.AreEqual(4.0 / 3, r.Utilization!.Value, 1e-12); Assert.AreEqual(EngineeringOutcome.NotSatisfied, r.Outcome);
        input.Situation = GeotechnicalSituation.Seismic;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, HydraulicSafety.LoadedOutflow(input, ntc).Outcome);
        input.Situation = GeotechnicalSituation.PersistentTransient;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, HydraulicSafety.LoadedOutflow(input, new StandardEN1997p1()).Outcome);
        input.HydrostaticPressure = .04; Assert.AreEqual(0, HydraulicSafety.LoadedOutflow(input, ntc).Demand);
    }

    [TestMethod]
    public void AffineFieldGivesExactGradientAndIntegratedForce()
    {
        var model = SeepageTests.Rectangle(8, 7);
        model.Heads = model.Nodes.Select((p, i) => new SeepageHead(i, 2000 - .4 * p.X - .5 * p.Z))
            .Where(h => model.Nodes[h.Node].X is 0 or 1000 || model.Nodes[h.Node].Z is 0 or 1000).ToArray();
        var field = SeepageAnalysis.Calculate(model); Assert.IsTrue(field.IsComplete, field.Diagnostic);
        Assert.AreEqual(.5, SeepageSampling.MeanUpwardGradient(field, 453, 987, 23), 1e-9);
        var gradients = SeepageSampling.GradientsAt(field, 500, 1000);
        Assert.AreEqual(.5, gradients.MaximumUpwardGradient, 1e-9); Assert.AreEqual(.4, gradients.MaximumAbsoluteHorizontalGradient, 1e-9);
        // h mean=1800 mm at z=0, pressure on upper face=gamma_w*500; width=2000, length=1000.
        Assert.AreEqual(25506, SeepageSampling.NetUpwardForce(field, 0, 1000, 0, 2000, SoilUnits.WaterUnitWeight * 500), 1e-6);
        Assert.ThrowsException<ArgumentException>(() => SeepageSampling.NetUpwardForce(field, -1, 1000, 0, 2000, 0));
        Assert.ThrowsException<ArgumentException>(() => SeepageSampling.Line(new SeepageResult(), 0, 0, 1, 1));
    }

    [TestMethod]
    public void ClippedSuctionIntegralIncludesItsExactZeroCrossing()
    {
        var model = SeepageTests.Rectangle(3, 4); model.Materials = [new("VG", .01, .01, 0, "test", new(.001, 2))];
        // All nodal heads prescribed: this tests exact sampling/integration, independently of solving an equilibrium interior.
        model.Heads = model.Nodes.Select((p, i) => new SeepageHead(i, 1000 - 2 * p.X)).ToArray();
        var field = SeepageAnalysis.Calculate(model); Assert.IsTrue(field.IsComplete, field.Diagnostic);
        Assert.AreEqual(0, SeepageSampling.NetUpwardForce(field, 0, 1000, 0, 1000, 0), 1e-6);
        // Positive triangular half: .5*500mm*1000mm*gamma_w*1000mm = 2452.5N.
        Assert.AreEqual(2452.5, SeepageSampling.NetUpwardForce(field, 0, 1000, 0, 1000, 0, true), 1e-6);
    }

    [TestMethod]
    public void CurvedWaterTableWeightFollowsExplicitPressureZoneAndSurvivesSnapshot()
    {
        var soil = new EmbeddedWallSoil { Soil = new Soil("test", 18e-6, 20e-6, Math.PI / 6, 0, "synthetic"), Top = 0, Bottom = -5000,
            AtRestCoefficient = .5, HorizontalModulus = .001, StiffnessSource = "given" };
        var ground = new EmbeddedWallGround { Elevation = 0, WaterElevation = -4000, SaturationFromPorePressures = true, PorePressureSource = "head -1000mm at the face",
            PorePressures = [new(0, 0), new(-1000, 0), new(-5000, .03924)] };
        var p = WallSoilPressure.At([soil], ground, -5000);
        Assert.AreEqual(.098, p.VerticalTotal, 1e-12); Assert.AreEqual(.05876, p.VerticalEffective, 1e-12);
        var model = new EmbeddedWallModel { Name = "test", CaseId = "K", Top = 0, Bottom = -5000, FixBottomDisplacement = true, FixBottomRotation = true,
            Segments = [new() { Top = 0, Bottom = -5000, FlexuralRigidity = 1e14, SectionId = "S", Source = "given" }], LeftSoils = [soil], RightSoils = [soil],
            Stages = [new() { Id = "F", Left = ground, Right = ground }] };
        var result = EmbeddedWallAnalysis.Calculate(model); Assert.IsTrue(result.IsComplete, result.Diagnostic);
        Assert.AreEqual(.06862, result.Stages[0].Points.Last().LeftPressure, 1e-10);
        ground.SaturationFromPorePressures = false; Assert.AreEqual(.092, WallSoilPressure.At([soil], ground, -5000).VerticalTotal, 1e-12);
        ground.SaturationFromPorePressures = true; ground.PorePressures = [];
        Assert.ThrowsException<ArgumentException>(() => EmbeddedWallAnalysis.Validate(model));
    }
}
