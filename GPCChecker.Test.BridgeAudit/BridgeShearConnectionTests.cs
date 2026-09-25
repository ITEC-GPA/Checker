using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

[TestClass, DoNotParallelize, TestCategory("BridgeShearConnection")]
public class BridgeShearConnectionTests
{
    static void Near(double expected, double actual, double tolerance = 1e-9) => Assert.AreEqual(expected, actual, tolerance * Math.Max(1, Math.Abs(expected)));
    [DataTestMethod]
    [DataRow(0d, 5.34)] [DataRow(1000d, 9.34)] [DataRow(500d, 25.36)] [DataRow(2000d, 6.34)]
    public void PanelCoefficientIncludesSquarePanel(double length, double expected)
    { Near(expected, BridgeShearConnection.Web(1000, 10, 355, 210000, 1.05, 1.1, 1.2, length).KTau); }
    [DataTestMethod] [DataRow(0.4, false)] [DataRow(0.8, false)] [DataRow(1.5, false)] [DataRow(1.5, true)]
    public void ReductionFollowsNormativeSlendernessBranches(double target, bool rigid)
    {
        double tau = 355 / (Math.Sqrt(3) * target * target);
        double thickness = 1000 * Math.Sqrt(tau * 12 * .91 / (5.34 * Math.PI * Math.PI * 210000));
        var r = BridgeShearConnection.Web(1000, thickness, 355, 210000, 1.05, 1.1, 1.2, rigidEndPost: rigid);
        Near(target, r.Slenderness);
        double expected = target == .4 ? 1.2 : rigid ? 1.37 / 2.2 : .83 / target;
        Near(expected, r.Chi); Assert.IsTrue(r.Resistance <= r.PlasticResistance);
    }
    [TestMethod]
    public void PublishedJrcPanelReproducesElasticCriticalStress()
    {
        var r = BridgeShearConnection.Web(2720, 18, 345, 210000, 1, 1.1, 1.2, 8000);
        Near(5.8024, r.KTau); Near(48.228, r.TauCritical, .0002);
        Assert.IsTrue(r.Slenderness > 2 && r.Chi < .42);
    }
    [TestMethod]
    public void PanelTransitionsAreContinuous()
    {
        var a = BridgeShearConnection.Web(1000, 10, 355, 210000, 1, 1.1, 1.2, 999.999999);
        var b = BridgeShearConnection.Web(1000, 10, 355, 210000, 1, 1.1, 1.2, 1000.000001);
        Near(a.KTau, b.KTau, 1e-8);
    }
    [DataTestMethod] [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidWebDimensionsRejected(double t) => Assert.ThrowsException<ArgumentException>(() => BridgeShearConnection.Web(1000, t, 355, 210000, 1, 1.1, 1.2));
    [TestMethod]
    public void StudReferenceAndFuCap()
    {
        var r = BridgeShearConnection.Stud(22, 200, 450, 35, 34077.14);
        Near(109478.22079589871, r.SteelResistance); Near(122628.829, r.ConcreteResistance, .0001);
        Near(r.SteelResistance, r.Resistance);
        Near(500, BridgeShearConnection.Stud(22, 200, 600, 35, 34077.14).UsedFu);
    }
    [DataTestMethod] [DataRow(3d, .8)] [DataRow(3.5, .9)] [DataRow(4d, 1d)] [DataRow(6d, 1d)]
    public void ShortStudConcreteFailure(double ratio, double alpha)
    {
        var r = BridgeShearConnection.Stud(19, ratio * 19, 500, 20, 30000);
        Near(alpha, r.Alpha); Near(.29 * alpha * 361 * Math.Sqrt(600000) / 1.25, r.Resistance);
    }
    [DataTestMethod] [DataRow(15d, 100d, 30d)] [DataRow(26d, 100d, 30d)] [DataRow(19d, 56d, 30d)] [DataRow(19d, 100d, 19d)] [DataRow(19d, 100d, 61d)]
    public void OutOfScopeStudRejected(double d, double h, double fck) => Assert.ThrowsException<ArgumentException>(() => BridgeShearConnection.Stud(d, h, 450, fck, 33000));
    [DataTestMethod] [DataRow(1000d)] [DataRow(3000d)]
    public void StiffenerRigidityAndZeroForce(double a)
    {
        var s = BridgeShearConnection.Stiffener(1800, 14, a, 150, 20, 355, 210000, 1.1, 0, 0);
        Near(a == 1000 ? 24004512 : 3704400, s.RequiredInertia);
        Near(0, s.AxialForce); Near(0, s.AdditionalDeflection); Near(0, s.Stress);
    }
    [TestMethod]
    public void StiffenerCompressionAndInstabilityAreNotHidden()
    {
        var basic = BridgeShearConnection.Stiffener(1800, 14, 3000, 150, 20, 355, 210000, 1.1, 800000, 0);
        var loaded = BridgeShearConnection.Stiffener(1800, 14, 3000, 150, 20, 355, 210000, 1.1, 800000, 1e6, 200000);
        Assert.IsTrue(loaded.AdditionalDeflection > basic.AdditionalDeflection && loaded.Stress > basic.Stress);
        var unstable = BridgeShearConnection.Stiffener(1800, 14, 3000, 30, 4, 355, 210000, 1.1, 1e7, 1e9);
        Assert.IsFalse(unstable.Stable); Assert.IsNull(unstable.StressRatio); Assert.IsFalse(unstable.Satisfactory);
        Assert.ThrowsException<ArgumentException>(() => BridgeShearConnection.Stiffener(1800, 14, 3000, 150, 20, 355, 210000, 1.1, double.NaN, 0));
    }
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void PlasticReferenceMatchesRectangleAndTwoFlanges(bool positive)
    {
        Near(355d * 20 * 1000 * 1000 / 4, BridgeBendingShear.PlasticMoment([new(-500, 500, 20, 355, 355)], positive));
        Near(355d * 400 * 20 * 1020, BridgeBendingShear.PlasticMoment([new(-520, -500, 400, 355, 355), new(500, 520, 400, 355, 355)], positive));
    }
    [DataTestMethod] [DataRow(40d, 90d, .4)] [DataRow(80d, 40d, .8)] [DataRow(80d, 75d, .95)] [DataRow(100d, 100d, 1.6)]
    public void InteractionThresholdsAndOverload(double moment, double shear, double expected)
    { Near(expected, BridgeBendingShear.Interaction(moment, 100, 40, shear, 100)); }
    static JsonObject Data(string norm = "NTC 2018 / EN 1994-2:2005")
    {
        var d = BridgeSection.Defaults(); BridgeSection.EnsureAccessoryDefaults(d); d["normativa"] = norm; d["pioli"] = true; d["classe4"] = false;
        d.Array("fasi")[0]!["V"] = 100; d.Array("fasi")[1]!["V"] = 400; d.Array("fasi")[2]!["V"] = -100;
        return d;
    }
    [TestMethod]
    public void StageFlowsSumWithSignsAndConstructionSteelIsExcluded()
    {
        var r = BridgeSection.Calculate(Data()); var last = r.Stages.Last(); var p = last.Studs!;
        Near(0, r.Stages[0].Studs!.Flow); Assert.IsFalse(r.Stages[0].Studs!.Enabled); Near(400, last.Shear!.V);
        Assert.IsTrue(p.Contributions[1].Flow > 0 && p.Contributions[2].Flow < 0);
        Near(p.Contributions.Sum(f => f.Flow + f.AdditionalFlow), p.Flow);
        Near(Math.Abs(p.Flow) * 200 / 2 / 1000, p.ForcePerStud);
        Near(p.ResistancePerStud * 1000 * 2 / 200, p.ResistancePerLength);
        Assert.IsNotNull(r.Json());
    }
    [TestMethod]
    public void NtcConnectionUsesEffectivePhasePropertiesAndIndependentStaticMoment()
    {
        var d = Data(); d["classe4"] = true; var r = BridgeSection.Calculate(d); var s = r.Stages.Last();
        var c = s.Contributions[1]; var q = s.Studs!.Contributions[1]; var g = r.Geometry;
        double nc = g.Width * g.SlabHeight / c.HomogenizationN;
        double expected = nc * (g.SlabHeight / 2 - c.Centroid);
        foreach (var b in g.Bars) expected += b.Area * (r.Materials.Es / r.Materials.Ea - 1 / c.HomogenizationN) * (b.Y - c.Centroid);
        Near(expected, q.StaticMoment); Near(c.Inertia, q.Inertia); Near(400000 * expected / c.Inertia, q.Flow);
    }
    [TestMethod]
    public void NtcAndEurocodeCrackedConnectionDifferIntentionally()
    {
        var ntc = Data(); ntc.Array("fasi")[2]!["tipo"] = "Soletta esclusa";
        var ec = (JsonObject)ntc.DeepClone(); ec["normativa"] = BridgeSection.Standards[1];
        var a = BridgeSection.Calculate(ntc).Stages.Last(); var b = BridgeSection.Calculate(ec).Stages.Last();
        Near(a.Contributions[2].Inertia, a.Studs!.Contributions[2].Inertia);
        Assert.IsTrue(b.Studs!.Contributions[2].Inertia > b.Contributions[2].Inertia);
    }
    [TestMethod]
    public void ShrinkageRequiresAssignedEndFlowAndDoesNotCreateVerticalShear()
    {
        var d = Data(); var shrink = BridgeSection.ShrinkagePhase(); shrink["epsilon_cs"] = -250; shrink["q_conn"] = -80; d.Array("fasi").Add(shrink);
        var r = BridgeSection.Calculate(d); var before = r.Stages[^2]; var after = r.Stages[^1];
        Near(before.Shear!.V, after.Shear!.V); Near(before.Studs!.Flow - 80, after.Studs!.Flow);
        Near(0, after.Studs.Contributions.Last().Flow); Near(-80, after.Studs.Contributions.Last().AdditionalFlow);
    }
    [TestMethod]
    public void PitchAndNumberScaleStudDemandWithoutChangingNormalStresses()
    {
        var d = Data(); var a = BridgeSection.Calculate(d).Stages.Last(); d["n_pioli"] = 4; d["passo_pioli"] = 300;
        var b = BridgeSection.Calculate(d).Stages.Last(); Near(a.Studs!.ForcePerStud * .75, b.Studs!.ForcePerStud);
        CollectionAssert.AreEqual(a.Points.Select(p => p.Stress).ToArray(), b.Points.Select(p => p.Stress).ToArray());
    }
    [TestMethod]
    public void SlsNeedsRareCombinationAndNoNewActionFactors()
    {
        var d = Data(); var slu = BridgeSection.Calculate(d).Stages.Last(); d["stato"] = "SLE rara";
        var rare = BridgeSection.Calculate(d).Stages.Last(); Near(.75 * slu.Studs!.ResistancePerStud, rare.Studs!.ResistancePerStud);
        Near(slu.Studs.ForcePerStud, rare.Studs.ForcePerStud); Assert.IsNull(rare.Shear!.Checks[0].Ratio);
        d["stato"] = "SLE quasi permanente"; Assert.IsNull(BridgeSection.Calculate(d).Stages.Last().Studs!.Checks[0].Ratio);
    }
    [TestMethod]
    public void UnsuitableStiffenersGiveNoResistanceBenefit()
    {
        var d = Data(); var bare = BridgeSection.Calculate(d).Stages.Last().Shear!;
        d["irrigidimenti"] = true; d["b_irr"] = 30; d["t_irr"] = 4;
        var invalid = BridgeSection.Calculate(d).Stages.Last().Shear!;
        Assert.IsFalse(invalid.UsesStiffeners); Near(bare.Web.Resistance, invalid.Web.Resistance);
        d["b_irr"] = 150; d["t_irr"] = 25;
        var valid = BridgeSection.Calculate(d).Stages.Last().Shear!;
        Assert.IsTrue(valid.UsesStiffeners); Assert.IsTrue(valid.Web.Resistance > bare.Web.Resistance);
    }
    [TestMethod]
    public void HighShearReportsPlasticOrConservativeElasticInteraction()
    {
        var d = Data(); d.Array("fasi")[2]!["V"] = 2500;
        var r = BridgeSection.Calculate(d).Stages.Last(); Assert.IsTrue(r.Shear!.Checks.Any(c => c.Name.StartsWith("Interazione M–V") && c.Ratio.HasValue));
        d.Array("fasi")[2]!["N"] = -100;
        r = BridgeSection.Calculate(d).Stages.Last(); Assert.IsTrue(r.Shear!.Checks.Any(c => c.Name.StartsWith("Interazione N–M–V") && c.Ratio is not null));
    }
    [TestMethod]
    public void DisabledAccessoriesIgnoreInvalidGeometryAndInputsRemainUnchanged()
    {
        var d = Data(); d["pioli"] = false; d["d_pioli"] = "invalid"; d["a_irr"] = "invalid";
        var snapshot = d.ToJsonString(); var r = BridgeSection.Calculate(d); Assert.IsFalse(r.Stages.Last().Studs!.Enabled); Assert.AreEqual(snapshot, d.ToJsonString());
    }
    [TestMethod]
    public void ShearOnlyReportIncludesEachStageResults()
    {
        var result = BridgeSection.Calculate(Data());
        using var stream = new MemoryStream(ReportBridge.Create("Verifica", result, new HashSet<string> { "taglio" }));
        using var zip = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        string xml = reader.ReadToEnd();
        Assert.AreEqual(3, xml.Split("Taglio e connessione della situazione").Length - 1);
        Assert.IsTrue(xml.Contains("Piolo · taglio longitudinale") && xml.Contains("Vbw,Rd"));
    }
    [TestMethod]
    public void EurocodeDisabledConnectionDoesNotReadUnusedPhiAndBridgeGammaIsCorrect()
    {
        var d = Data(BridgeSection.Standards[1]); d["pioli"] = false; d.Array("fasi")[2]!["tipo"] = "Soletta esclusa"; d.Array("fasi")[2]!["phi"] = "unused";
        Assert.IsNotNull(BridgeSection.Calculate(d));
        d.Remove("gamma_m1"); BridgeSection.EnsureAccessoryDefaults(d); Near(1.1, d.D("gamma_m1"));
    }
}

