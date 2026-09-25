using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass]
public class BridgeLocalDetailsTests
{
    private static void Near(double expected, double actual, double tolerance = 1e-7) => Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * tolerance);
    private static StiffenerDetail Detail(double bl = 100, double tl = 20, double br = 100, double tr = 20,
        double n = 0, double web = 0, double x = 0, double z = 0, double aR = 3000, double available = 1500, double beta = 1) =>
        BridgeLocalDetails.Stiffener(1000, 10, 1000, 3000, aR, available, available, bl, tl, br, tr, 235, 210000, 1.1, n, web, x, z, beta);
    private static JsonObject Data()
    {
        var d = BridgeSection.Defaults(); BridgeSection.EnsureAccessoryDefaults(d);
        foreach (JsonObject p in d.Array("fasi").OfType<JsonObject>()) { p["N"] = 0; p["Mx"] = 0; p["V"] = 0; }
        return d;
    }
    [TestMethod] public void SymmetricGeometryMatchesHandIntegration()
    {
        var r = Detail(); Near(7000, r.Area); Near(0, r.CentroidX); Near(15458333.333333333, r.InertiaOut);
        Near(22633333.333333333, r.InertiaIn); Near(300, r.WebStrip); Near(0, r.Stress); Assert.IsTrue(r.Satisfactory);
    }
    [TestMethod] public void OneSidedUsesShiftedCentroidAndParallelAxis()
    {
        var l = Detail(br: 0, tr: 0); var r = Detail(bl: 0, tl: 0);
        Near(5000, l.Area); Near(-22, l.CentroidX); Near(22, r.CentroidX);
        Near(5321666.666666667, l.InertiaOut); Near(l.InertiaOut, r.InertiaOut);
        Near(22, l.Eccentricity); Near(-22, r.Eccentricity);
    }
    [DataTestMethod] [DataRow(-45d)] [DataRow(0d)] [DataRow(80d)]
    public void MirroredStiffenerAndLoadHaveSameResult(double x)
    {
        var l = Detail(br: 0, tr: 0, n: 40000, x: x); var r = Detail(bl: 0, tl: 0, n: 40000, x: -x);
        Near(l.Stress, r.Stress); Near(l.Deflection, r.Deflection);
    }
    [TestMethod] public void LoadAtCentroidEliminatesRealEccentricityButNotImperfection()
    {
        var a = Detail(br: 0, tr: 0, n: 50000); var b = Detail(br: 0, tr: 0, n: 50000, x: -22);
        Near(0, b.Eccentricity); Assert.IsTrue(b.Stress < a.Stress); Assert.IsTrue(b.MomentOut > 0);
    }
    [TestMethod] public void EccentricCompressionMatchesSecantSolution()
    {
        var r = Detail(n: 100000, x: 10, z: 4);
        double ex = 100000 / r.CriticalOut, ez = 100000 / r.CriticalIn;
        double mx = 1e6 / Math.Cos(Math.PI / 2 * Math.Sqrt(ex)) + 500000 / (1 - ex);
        double mz = 400000 / Math.Cos(Math.PI / 2 * Math.Sqrt(ez)) + 500000 / (1 - ez);
        Near(mx, r.MomentOut); Near(mz, r.MomentIn);
        Near(100000d / 7000 + mx * 105 / r.InertiaOut + mz * 150 / r.InertiaIn, r.Stress);
    }
    [TestMethod] public void AdjacentPanelsAndMissingWebStripMatter()
    {
        Assert.IsTrue(Detail(n: 20000, web: 1e5, aR: 1000).Stress > Detail(n: 20000, web: 1e5).Stress);
        Assert.IsTrue(Detail(available: 50).Area < Detail().Area);
        Near(4000, Detail(available: 0).Area);
    }
    [TestMethod] public void CriticalLoadCannotProduceFavourableFiniteRatio()
    {
        var unloaded = Detail(); var unstable = Detail(n: Math.Min(unloaded.CriticalOut, unloaded.CriticalIn) * 1.01);
        Assert.IsFalse(unstable.Stable); Assert.IsNull(unstable.StressRatio); Assert.IsFalse(unstable.Satisfactory);
    }
    [DataTestMethod] [DataRow(.5)] [DataRow(2.1)] [DataRow(double.NaN)]
    public void InvalidRestraintRejected(double factor) => Assert.ThrowsException<ArgumentException>(() => Detail(beta: factor));
    [TestMethod] public void ThinnerPlateFailsLocalAndTorsionalChecks()
    {
        var r = Detail(bl: 200, tl: 4, br: 0, tr: 0);
        Assert.IsTrue(r.LocalRatio > 1); Assert.IsTrue(r.TorsionRatio > 1); Assert.IsFalse(r.Satisfactory);
    }
    [TestMethod] public void WeldResistanceMatchesNormativeExpression() => Near(360 / Math.Sqrt(3) / 1.25, BridgeLocalDetails.WeldStrength(360, 1, 1.25));
    [TestMethod] public void AnchorageNtcAndEcFloorsAreDifferent()
    {
        Near(280, BridgeLocalDetails.AnchorageLength(14, 100, 3, 1.5, true, true));
        Near(140, BridgeLocalDetails.AnchorageLength(14, 100, 3, 1.5, true, false));
        double good = BridgeLocalDetails.AnchorageLength(16, 400, 2, 1.5, true, false);
        Near(good / .7, BridgeLocalDetails.AnchorageLength(16, 400, 2, 1.5, false, false));
    }
    [DataTestMethod] [DataRow(.4, .25, .4)] [DataRow(.4, .5, .4)] [DataRow(.4, .75, .65)] [DataRow(.4, 1, 1.4)] [DataRow(.4, -.75, .65)]
    public void ElasticInteractionThresholdAndSigns(double normal, double shear, double expected) => Near(expected, BridgeBendingShear.ElasticInteraction(normal, shear, 1));
    [TestMethod] public void SlabTrussAndMinimumMatchHandCalculation()
    {
        var r = BridgeLocalDetails.SlabSurface(300, 250, 2, 30, 20, 450, 400, 1);
        Near(.75, r.RequiredSteel); Near(.08 * Math.Sqrt(30) / 450 * 250, r.MinimumSteel);
        Near(1320, r.StrutResistance); Near(.375, r.SteelRatio!.Value);
    }
    [TestMethod] public void TransverseBendingAndMissingReinforcementAreNotIgnored()
    {
        var r = BridgeLocalDetails.SlabSurface(300, 250, 0, 30, 20, 450, 400, 1, 1);
        Near(1.375, r.RequiredSteel); Assert.IsNull(r.SteelRatio);
        Assert.IsTrue(BridgeLocalDetails.SlabSurface(1500, 250, 10, 30, 20, 450, 400, 1).StrutRatio > 1);
    }
    [TestMethod] public void ZeroFlowStillRequiresMinimumTransverseSteel()
    { var r = BridgeLocalDetails.SlabSurface(0, 250, 0, 30, 20, 450, 400, 1); Assert.IsTrue(r.RequiredSteel > 0); Assert.IsNull(r.SteelRatio); }
    [DataTestMethod] [DataRow(.9)] [DataRow(2.1)] [DataRow(double.NaN)]
    public void SlabInvalidAngleRejected(double cot) => Assert.ThrowsException<ArgumentException>(() => BridgeLocalDetails.SlabSurface(300, 250, 2, 30, 20, 450, 400, cot));
    [TestMethod] public void StudFatigueAtTwoMillionCyclesAndInteraction()
    {
        double area = Math.PI * 22 * 22 / 4, range = 72 * area * 2 / 200;
        var r = BridgeLocalDetails.StudFatigue(range, 200, 2, 22, 40, true, 1, 1.25, 1.35);
        Near(72, r.StressRange); Near(1, r.StudRatio); Near(.675, r.FlangeRatio); Near(1.675 / 1.3, r.Ratio);
        Near(1, BridgeLocalDetails.StudFatigue(range, 200, 2, 22, 40, false, 1, 1.25, 1.35).Ratio);
    }
    [TestMethod] public void MoreStudsReduceRangeButDoNotChangeFlangeStress()
    {
        var a = BridgeLocalDetails.StudFatigue(300, 200, 2, 22, 10, true, 1, 1.25, 1.35);
        var b = BridgeLocalDetails.StudFatigue(300, 200, 4, 22, 10, true, 1, 1.25, 1.35);
        Near(a.StressRange / 2, b.StressRange); Near(a.FlangeRatio, b.FlangeRatio);
    }
    [TestMethod] public void AllOptionalFeaturesLeaveNormalPhaseResultsUnchanged()
    {
        var d = Data(); d.Array("fasi")[1]!["Mx"] = 300; var before = BridgeSection.Calculate(d);
        d["irrigidimenti"] = true; d["lati_irr"] = "Solo sinistra"; d["t_irr"] = 30;
        d["appoggio"] = true; d["R_app"] = 200; d["pioli"] = true; d["armatura_trasv"] = true;
        d["fatica_pioli"] = true; d["q_fat_min"] = -20; d["q_fat_max"] = 200;
        var after = BridgeSection.Calculate(d);
        for (int i = 0; i < before.Stages.Count; i++)
        {
            CollectionAssert.AreEqual(before.Stages[i].Points.Select(p => p.Stress).ToArray(), after.Stages[i].Points.Select(p => p.Stress).ToArray());
            Assert.AreEqual(before.Stages[i].Effective, after.Stages[i].Effective);
            CollectionAssert.AreEqual(before.Stages[i].Contributions.ToArray(), after.Stages[i].Contributions.ToArray());
        }
    }
    [TestMethod] public void EndPostGainsRigidCurveOnlyAfterAllChecks()
    {
        var d = Data(); d["appoggio"] = true; d["pos_app"] = "Estremità sinistra"; d["terminale_rigido"] = true;
        d["R_app"] = 0; d["e_term"] = 500; d["a_app_sx"] = "ignored";
        var r = BridgeSection.Calculate(d).Stages.Last().Shear!; Assert.IsTrue(r.RigidEndPost);
        d["lati_app"] = "Solo sinistra"; Assert.IsFalse(BridgeSection.Calculate(d).Stages.Last().Shear!.RigidEndPost);
        d["lati_app"] = "Bilaterali simmetrici"; d["e_term"] = 100; Assert.IsFalse(BridgeSection.Calculate(d).Stages.Last().Shear!.RigidEndPost);
        d["e_term"] = 500; d["aw_app"] = 1; Assert.IsFalse(BridgeSection.Calculate(d).Stages.Last().Shear!.RigidEndPost);
    }
    [TestMethod] public void DisabledFeaturesDoNotValidateDormantInputsOrMutateArchive()
    {
        var d = Data(); foreach (string k in new[] { "R_app", "q_fat_min", "cot_trasv", "a_irr_dx" }) d[k] = "invalid";
        string json = d.ToJsonString(); BridgeSection.Calculate(d); Assert.AreEqual(json, d.ToJsonString());
    }
    [TestMethod] public void MissingBearingReactionDoesNotBecomeZero()
    { var d = Data(); d["appoggio"] = true; Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d)); }
    [TestMethod] public void AbsentBottomReinforcementFailsTheEnclosingSurface()
    {
        var d = Data(); d["pioli"] = true; d["armatura_trasv"] = true; d["d_trasv_inf"] = 0; d["s_trasv_inf"] = "ignored";
        var r = BridgeSection.Calculate(d).Stages.Last().Studs!;
        Assert.IsTrue(r.Checks.Any(c => c.Name.StartsWith("Soletta b–b") && c.Name.Contains("armatura") && c.Ratio is null));
    }
    [TestMethod] public void NearPhysicalSlabEdgeRequiresUBarAndSixDiameters()
    {
        var d = Data(); d["pioli"] = true; d["armatura_trasv"] = true; d["bordo_cls_sx"] = 100;
        var checks = BridgeSection.Calculate(d).Stages.Last().Studs!.Checks;
        Assert.IsTrue(checks.Single(c => c.Name == "Splitting al bordo · distanza piolo").Ratio > 1);
        Assert.IsTrue(checks.Single(c => c.Name == "Splitting al bordo · forcine a U").Ratio > 1);
    }
    [TestMethod] public void FatigueIsIndependentOfStageVAndNeedsItsOwnInputs()
    {
        var d = Data(); d["pioli"] = true; d["fatica_pioli"] = true;
        Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
        d["q_fat_min"] = -100; d["q_fat_max"] = 100; var a = BridgeSection.Calculate(d).Stages.Last().Studs!;
        d.Array("fasi")[2]!["V"] = 500; var b = BridgeSection.Calculate(d).Stages.Last().Studs!;
        Near(a.Checks.Single(c => c.Name.StartsWith("Fatica pioli")).Ratio!.Value, b.Checks.Single(c => c.Name.StartsWith("Fatica pioli")).Ratio!.Value);
        d["q_fat_min"] = 200; Assert.ThrowsException<ArgumentException>(() => BridgeSection.Calculate(d));
    }
    [DataTestMethod] [DataRow(500d, "S355")] [DataRow(-500d, "S355")] [DataRow(0d, "S420")]
    public void GeneralNmvProvidesFiniteConservativeCheck(double axial, string steel)
    {
        var d = Data(); d["acciaio"] = steel; d.Array("fasi")[2]!["N"] = axial; d.Array("fasi")[2]!["Mx"] = 200;
        double vr = BridgeSection.Calculate(d).Stages.Last().Shear!.Web.Resistance / 1000;
        d.Array("fasi")[2]!["V"] = .75 * vr;
        var r = BridgeSection.Calculate(d).Stages.Last().Shear!;
        var c = r.Checks.Single(c => c.Name.StartsWith("Interazione N–M–V")); Assert.IsNotNull(c.Ratio);
        Near(.25, r.Details!.Single(v => v.Name.EndsWith("termine di taglio")).Value);
        Assert.IsTrue(c.Ratio > .25);
    }
    [TestMethod] public void AxialBoundsAreTranslatedConsistently()
    {
        var blocks = new[] { new BridgeBendingShear.Block(-10, 10, 10, 100, 100) };
        var r = BridgeBendingShear.AtAxialForce(blocks, 10000);
        Near(75000, r.Maximum); Near(-75000, r.Minimum); Near(5, r.AxisPositive);
        var translated = BridgeBendingShear.AtAxialForce(new[] { new BridgeBendingShear.Block(0, 20, 10, 100, 100) }, 10000);
        Near(r.Maximum - 100000, translated.Maximum); Assert.IsFalse(BridgeBendingShear.AtAxialForce(blocks, 20001).Feasible);
    }
}
