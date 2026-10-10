using GPC.Model.Models;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Checker;
using GPC.Model.Persistence;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

/// <summary>
/// Materials and durability: ReinforcedConcreteSection.DurabilityData feeds cmin,dur, the cover additions and the minimum strength of the
/// exposures into the 1D detailing task. Fixture: RC 300x500 C25/30 (fck 25 MPa), 4Ø16 with the axes 50 mm from the edges (geometric cover of the
/// bars 42 mm), links Ø8/200, nominal cover 34 mm to the links, Δcdev 5 mm, dg 20 mm.
/// </summary>
[TestClass]
public class DurabilityDetailingTest
{
    private static GPC.Model.Models.Model Model(ConcreteDurabilityData? durability, double? givenCdur = null, double deviation = 5, bool detailing = true)
    {
        var m = ElementScopeCharacterizationTest.Model();
        m.PhysicalMembers.Add("T1", new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }, "NodeToNode", source: "fixture"));
        var section = m.BeamElements[250].Assignments.Sections[0].Section;
        section.ShearData = new ConcreteShearData(new ConcreteShearReinforcement(8, 200, section.Rebars.First().RebarMaterial),
            new ConcreteShearDirection(500, 250, 402, true, 2), new ConcreteShearDirection(300, 450, 402, true, 2), "fixture drawings", 20);
        if (detailing) section.DetailingData = new ConcreteDetailingData(34, givenCdur, deviation, 300, 300, 300, 20, "fixture drawings", compressionBarsRestrained: true, endZonesConfirmed: true);
        section.DurabilityData = durability;
        return m;
    }

    private static ConcreteDurabilityData Data(params string[] exposures) => new ConcreteDurabilityData(exposures, 50, "durability report");

    private static CheckResult Run(GPC.Model.Models.Model m, StandardModelCode2010? standard = null, bool column = false)
    {
        string method = column ? ConcreteSectionVerifier.ColumnDetailingMethod : ConcreteSectionVerifier.BeamDetailingMethod;
        var plan = new BeamCheckPlanRequest { MemberIds = new[] { "T1" }, Settings = "durability", SectionMechanisms = new CheckMechanism[0],
            Results = new[] { new ResultSelection { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1", Category = CombinationCategory.Ultimate } },
            MemberChecks = new[] { new MemberCheckSpecification { MemberId = "T1", MethodId = method, Mechanism = CheckMechanism.Detailing,
                Context = new MemberDesignContext(globalStateConfirmed: true) } } };
        var request = new ModelCheckRequest();
        request.Jobs.Add(new ModelCheckJob { Name = "durability", BeamPlan = plan, Options = new ConcreteVerificationOptions { Standard = standard ?? new StandardNTC2018Concrete(),
            Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity } });
        return new Service().Verify(m, request).Jobs[0].Results.Single();
    }

    private static string Describe(CheckResult r) => r.Data + " " + r.Outcome + " " + string.Join(",", r.Diagnostics.Select(d => d.Code + ":" + d.Message))
        + " | " + string.Join(", ", r.Details?.Metrics.Select(x => x.Key + " " + x.Demand + "/" + x.Capacity + " " + x.Passed) ?? new string[0]);
    private static CheckMetric Metric(CheckResult r, string key) => r.Details!.Metrics.Single(x => x.Key == key);
    private static CheckCalculationValue Trace(CheckResult r, string key) => ((DetailingCheckDetails)r.Details!).Trace.Single(x => x.Key == key);

    [TestMethod]
    public void NtcCoverFromTheExposureReplacesTheMissingCmin()
    {
        // XC1: ordinary, Cmin = UNI 11104 C25/30, C0 = C35/45; fck 25 < C0 → table 20 + 5 = 25 mm; cnom ≥ 25 + 5 = 30 ≤ 34 mm.
        var r = Run(Model(Data("XC1")));
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Describe(r));
        Assert.AreEqual(30, Metric(r, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(34, Metric(r, "NominalCover").Demand!.Value, 1e-12);
        Assert.AreEqual(42 - 30, Metric(r, "BarCoverMargin").Demand!.Value, 1e-9, "bars: max(10; 25; 16) + 5");
        Assert.AreEqual(25, Trace(r, "cmin,dur").Value!.Value, 1e-12); StringAssert.Contains(Trace(r, "cmin,dur").Expression, "(UNI 11104)");
        var strength = Metric(r, "MinimumStrength"); Assert.AreEqual(25, strength.Demand!.Value); Assert.AreEqual(25, strength.Capacity!.Value); Assert.AreEqual(true, strength.Passed);
        StringAssert.Contains(strength.Reference, "UNI 11104");
        StringAssert.Contains(Trace(r, "Exposures").Expression, "XC1, 50 years, durability report");
        // Without durability data and without cmin,dur the cover stays pending, as before.
        var none = Run(Model(null));
        Assert.AreEqual(DataStatus.Insufficient, none.Data, Describe(none)); StringAssert.Contains(none.Diagnostics.Single(d => d.Code == "DetailingPending").Message, "NominalCover");
        Assert.IsFalse(none.Details!.Metrics.Any(x => x.Key == "MinimumStrength"));
    }

    [TestMethod]
    public void NtcLowStrengthLifeAndQualityChangeTheCover()
    {
        // XC3 with UNI 11104 C30/37: fck 25 < Cmin → +5 mm (30 mm, cnom 35 > 34) and the minimum strength fails.
        var xc3 = Run(Model(Data("XC3")));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, xc3.Outcome, Describe(xc3));
        Assert.AreEqual(35, Metric(xc3, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(false, Metric(xc3, "MinimumStrength").Passed);
        // Pertinent Cmin given (C25/30): no addition, the UNI 11104 class still fails.
        var pertinent = Run(Model(new ConcreteDurabilityData(new[] { "XC3" }, 50, "durability report", pertinentCmin: 25)));
        Assert.AreEqual(30, Metric(pertinent, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(true, Metric(pertinent, "NominalCover").Passed);
        Assert.IsFalse(Trace(pertinent, "cmin,dur").Expression.Contains("UNI 11104")); Assert.AreEqual(false, Metric(pertinent, "MinimumStrength").Passed);
        // 100 years: +10 mm (XC1: 35 mm, cnom 40 > 34); quality control: −5 mm (XC1: 20 mm, cnom 25).
        var life = Run(Model(new ConcreteDurabilityData(new[] { "XC1" }, 100, "durability report")));
        Assert.AreEqual(40, Metric(life, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(EngineeringOutcome.NotSatisfied, life.Outcome);
        var quality = Run(Model(new ConcreteDurabilityData(new[] { "XC1" }, 50, "durability report", ntcQualityReduction: true)));
        Assert.AreEqual(25, Metric(quality, "NominalCover").Capacity!.Value, 1e-12);
        // Very aggressive XD3 alone (Cmin C35/45 by UNI 11104, C0 C45/55): 40 + 5 (fck < C0) + 5 (fck < Cmin) = 50 mm, cnom 55 mm.
        var xd3 = Run(Model(Data("XD3")));
        Assert.AreEqual(55, Metric(xd3, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(35, Metric(xd3, "MinimumStrength").Capacity!.Value);
        // XF3 alone is an aggressive environment for NTC (no corrosion class needed).
        Assert.AreEqual(35 + 5, Metric(Run(Model(Data("XF3"))), "NominalCover").Capacity!.Value, 1e-12);
        // CNR-DT 200 uses the NTC rules for the RC member.
        Assert.AreEqual(Metric(xc3, "NominalCover").Capacity, Metric(Run(Model(Data("XC3")), new StandardCNR200()), "NominalCover").Capacity);
    }

    [TestMethod]
    public void EurocodeStructuralClassesAdditionsAndIndicativeStrength()
    {
        // XC3, S4: 25 mm; cnom 30 ≤ 34; Table E.1N C25/30 = fck.
        var en = Run(Model(Data("XC3")), new StandardEN1992p11());
        Assert.AreEqual(EngineeringOutcome.Satisfied, en.Outcome, Describe(en));
        Assert.AreEqual(30, Metric(en, "NominalCover").Capacity!.Value, 1e-12); StringAssert.Contains(Trace(en, "cmin,dur").Expression, "XC3 S4 25");
        StringAssert.Contains(Metric(en, "MinimumStrength").Reference, "E.1N");
        // 100 years: S6, 35 mm → 40 > 34; S6 with strength reduction and quality control: S4 again.
        Assert.AreEqual(40, Metric(Run(Model(new ConcreteDurabilityData(new[] { "XC3" }, 100, "r")), new StandardEN1992p11()), "NominalCover").Capacity!.Value, 1e-12);
        var reduced = Run(Model(new ConcreteDurabilityData(new[] { "XC3" }, 100, "r", strengthReduction: true, specialQualityControl: true)), new StandardEN1992p11());
        Assert.AreEqual(35, Metric(reduced, "NominalCover").Capacity!.Value, 1e-12, "fck 25 < C35/45: no strength reduction, quality −1 → S5 30 mm");
        // Abrasion XM2 (+10) and rough surface (+5): 25 + 15 + 5 = 45 mm; the bars lose the same 15 mm.
        var abrasion = Run(Model(new ConcreteDurabilityData(new[] { "XC3" }, 50, "r", roughSurface: true, abrasion: 10)), new StandardEN1992p11());
        Assert.AreEqual(45, Metric(abrasion, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(42 - 45, Metric(abrasion, "BarCoverMargin").Demand!.Value, 1e-9);
        // Against soil: cnom ≥ 75 mm.
        var soil = Run(Model(new ConcreteDurabilityData(new[] { "XC2" }, 50, "r", ground: 75)), new StandardEN1992p11());
        Assert.AreEqual(75, Metric(soil, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(EngineeringOutcome.NotSatisfied, soil.Outcome);
        // XD1 (S4 35 mm, cnom 40) and C30/37 indicative: both fail.
        var xd1 = Run(Model(Data("XD1")), new StandardEN1992p11());
        Assert.AreEqual(false, Metric(xd1, "NominalCover").Passed); Assert.AreEqual(false, Metric(xd1, "MinimumStrength").Passed);
        // XF4 is not in Table E.1N: XC4 governs (C30/37) and the trace lists XF4.
        var xf4 = Run(Model(Data("XC4", "XF4")), new StandardEN1992p11());
        Assert.AreEqual(30, Metric(xf4, "MinimumStrength").Capacity!.Value); StringAssert.Contains(Trace(xf4, "MinimumStrength not defined").Expression, "XF4");
        // XF3 alone does not define cmin,dur in EC2: insufficient data.
        var xf3 = Run(Model(Data("XF3")), new StandardEN1992p11());
        Assert.AreEqual(DataStatus.Insufficient, xf3.Data); Assert.IsTrue(xf3.Diagnostics.Any(d => d.Code == "DurabilityOutsideMethodRange"), Describe(xf3));
    }

    [TestMethod]
    public void ItalianAndDanishAnnexesUseTheirOwnTables()
    {
        // XC1 + XF2: EN max(C20/25; C25/30) = 25 satisfied; DM 2012 max(C25/30; C30/37) = 30 not satisfied.
        Assert.AreEqual(true, Metric(Run(Model(Data("XC1", "XF2")), new StandardEN1992p11()), "MinimumStrength").Passed);
        var uni = Run(Model(Data("XC1", "XF2")), new StandardUNIEN1992p11());
        Assert.AreEqual(30, Metric(uni, "MinimumStrength").Capacity!.Value); Assert.AreEqual(EngineeringOutcome.NotSatisfied, uni.Outcome);
        // DK NA column: XC1 10 mm, cnom 15; passive class 12 MPa → satisfied.
        var ds = Run(Model(Data("XC1")), new StandardDSEN1992p11(), column: true);
        Assert.AreEqual(EngineeringOutcome.Satisfied, ds.Outcome, Describe(ds));
        Assert.AreEqual(Math.Max(10, 8) + 5, Metric(ds, "NominalCover").Capacity!.Value, 1e-12); Assert.AreEqual(12, Metric(ds, "MinimumStrength").Capacity!.Value);
        // XC3: moderate, 30 MPa > 25 → not satisfied.
        Assert.AreEqual(false, Metric(Run(Model(Data("XC3")), new StandardDSEN1992p11(), column: true), "MinimumStrength").Passed);
        // Δcdev 4 mm < 5 mm (DK NA 4.4.1.3(1)P): insufficient.
        var deviation = Run(Model(Data("XC1"), deviation: 4), new StandardDSEN1992p11(), column: true);
        Assert.AreEqual(DataStatus.Insufficient, deviation.Data); StringAssert.Contains(deviation.Diagnostics.Single(d => d.Code == "DurabilityOutsideMethodRange").Message, "5 mm");
        // 100 years is not implemented in the DK table: pending rule; with cmin,dur given the cover is still checked.
        var life = Run(Model(new ConcreteDurabilityData(new[] { "XC1" }, 100, "r"), givenCdur: 25), new StandardDSEN1992p11(), column: true);
        Assert.AreEqual(DataStatus.NotSupported, life.Data, Describe(life)); Assert.AreEqual(EngineeringOutcome.NotEvaluated, life.Outcome);
        StringAssert.Contains(life.Diagnostics.Single(d => d.Code == "DetailingRuleNotImplemented").Message, "DurabilityCover");
        Assert.AreEqual(30, Metric(life, "NominalCover").Capacity!.Value, 1e-12);
    }

    [TestMethod]
    public void GivenAndComputedCminDurTheLargerGoverns()
    {
        // Given 25, computed X0 (EN S4: 10 mm): 25 governs; given 15, computed XC3 25: 25 governs. Both are traced.
        var given = Run(Model(Data("X0"), givenCdur: 25), new StandardEN1992p11());
        Assert.AreEqual(30, Metric(given, "NominalCover").Capacity!.Value, 1e-12);
        Assert.AreEqual(10, Trace(given, "cmin,dur").Value!.Value, 1e-12); Assert.AreEqual(25, Trace(given, "cmin,dur given").Value!.Value, 1e-12);
        var computed = Run(Model(Data("XC3"), givenCdur: 15), new StandardEN1992p11());
        Assert.AreEqual(30, Metric(computed, "NominalCover").Capacity!.Value, 1e-12);
        // Durability data without detailing data: the covers and widths are still missing.
        var missing = Run(Model(Data("XC1"), detailing: false));
        Assert.IsTrue(missing.Diagnostics.Any(d => d.Code == "MissingDetailingData"), Describe(missing));
    }

    [TestMethod]
    public void HistoricRebarsKeepNominalMinimaApartFromTheConstitutiveLaw()
    {
        var data = GPC.Model.Data.Steel.SteelMaterialDM1996Data.HistoricRebars;
        CollectionAssert.AreEqual(new[] { "FeB22k", "FeB32k", "FeB38k", "FeB44k" }, data.Select(x => x.Name).ToArray());
        CollectionAssert.AreEqual(new[] { true, true, false, false }, data.Select(x => x.Smooth).ToArray(), "tables 1-I (smooth) and 2-I (ribbed)");
        CollectionAssert.AreEqual(new[] { 24.0, 23, 14, 12 }, data.Select(x => x.MinimumElongation).ToArray());
        var materials = new[] { GPC.Model.Data.Steel.SteelMaterialDM1996Data.FeB22k, GPC.Model.Data.Steel.SteelMaterialDM1996Data.FeB32k,
            GPC.Model.Data.Steel.SteelMaterialDM1996Data.FeB38k, GPC.Model.Data.Steel.SteelMaterialDM1996Data.FeB44k };
        for (int i = 0; i < materials.Length; i++)
        {
            Assert.AreEqual(data[i].Name, materials[i].Name);
            Assert.AreEqual(.01, materials[i].StrainUTension, 1e-15, "A5 never becomes the ultimate strain");
        }
        CollectionAssert.AreEqual(new[] { 215.0, 315, 375, 430 }, materials.Select(x => x.Fyk).ToArray());
        CollectionAssert.AreEqual(new[] { 335.0, 490, 450, 540 }, materials.Select(x => x.Fu).ToArray());
        Assert.AreEqual(12, GPC.Model.Data.Steel.SteelMaterialDM1996Data.Historic("FeB44k").MinimumElongation);
        Assert.ThrowsException<ArgumentException>(() => GPC.Model.Data.Steel.SteelMaterialDM1996Data.Historic("feb44k"));
        Assert.ThrowsException<ArgumentException>(() => GPC.Model.Data.Steel.SteelMaterialDM1996Data.Historic("B450C"));
        Assert.ThrowsException<ArgumentException>(() => GPC.Model.Data.Steel.SteelMaterialDM1996Data.Historic(null!));
    }

    [TestMethod]
    public void DurabilityDataAreValidatedVersionedAndArchived()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new ConcreteDurabilityData(null!, 50, "r"));
        foreach (var bad in new Action[]
        {
            () => new ConcreteDurabilityData(new string[0], 50, "r"), () => new ConcreteDurabilityData(new[] { "XC9" }, 50, "r"),
            () => new ConcreteDurabilityData(new[] { "xc1" }, 50, "r"), () => new ConcreteDurabilityData(new[] { "XC1", "XC1" }, 50, "r"),
            () => new ConcreteDurabilityData(new[] { "X0", "XC1" }, 50, "r"), () => new ConcreteDurabilityData(new[] { "XC1" }, 50, " "),
            () => new ConcreteDurabilityData(new[] { "XC1" }, 50, null!)
        }) Assert.ThrowsException<ArgumentException>(bad);
        foreach (var bad in new Action[]
        {
            () => new ConcreteDurabilityData(new[] { "XC1" }, 75, "r"), () => new ConcreteDurabilityData(new[] { "XC1" }, 0, "r"),
            () => new ConcreteDurabilityData(new[] { "XC1" }, 50, "r", abrasion: 7), () => new ConcreteDurabilityData(new[] { "XC1" }, 50, "r", ground: 50),
            () => new ConcreteDurabilityData(new[] { "XC1" }, 50, "r", pertinentCmin: 11.9), () => new ConcreteDurabilityData(new[] { "XC1" }, 50, "r", pertinentCmin: 90.1),
            () => new ConcreteDurabilityData(new[] { "XC1" }, 50, "r", pertinentCmin: double.NaN)
        }) Assert.ThrowsException<ArgumentOutOfRangeException>(bad);
        Assert.AreEqual(12, new ConcreteDurabilityData(new[] { "X0" }, 100, "r", pertinentCmin: 12).PertinentCmin);
        Assert.AreEqual(15, new ConcreteDurabilityData(new[] { "XC4", "XF4", "XA3" }, 50, "r", abrasion: 15, ground: 40).Abrasion);
        // The list is copied: changing the caller's array does not change the data.
        var codes = new[] { "XC3" }; var data = new ConcreteDurabilityData(codes, 50, "r"); codes[0] = "XS3";
        Assert.AreEqual("XC3", data.Exposures[0]);
        Assert.AreEqual(new ConcreteDurabilityData(new[] { "XC3", "XF1" }, 50, "r"), new ConcreteDurabilityData(new[] { "XC3", "XF1" }, 50, "r"));
        Assert.AreNotEqual(new ConcreteDurabilityData(new[] { "XC3", "XF1" }, 50, "r"), new ConcreteDurabilityData(new[] { "XC3", "XF1" }, 50, "s"));
        Assert.AreNotEqual(new ConcreteDurabilityData(new[] { "XC3" }, 50, "r"), new ConcreteDurabilityData(new[] { "XC3" }, 50, "r", roughSurface: true));

        // Revision: durability data change the verification fingerprint, not the analysis; archive round trip.
        var m = Model(Data("XC1")); var section = m.BeamElements[250].Assignments.Sections[0].Section;
        string analysis = m.AnalysisFingerprint(), verification = m.VerificationFingerprint("x");
        section.DurabilityData = new ConcreteDurabilityData(new[] { "XC2", "XF1" }, 100, "revised report", strengthReduction: true, specialQualityControl: true,
            roughSurface: true, abrasion: 5, ground: 40, ntcQualityReduction: true, pertinentCmin: 28);
        Assert.AreNotEqual(verification, m.VerificationFingerprint("x")); Assert.AreEqual(analysis, m.AnalysisFingerprint());
        using var stream = new MemoryStream(); ModelArchive.Save(m, stream); stream.Position = 0;
        var loaded = ModelArchive.Load(stream).BeamElements[250].Assignments.Sections[0].Section;
        Assert.AreEqual(section.DurabilityData, loaded.DurabilityData); Assert.AreEqual(section, loaded);
        // A section without durability data keeps its content: the entry is written only when present.
        static string Saved(GPC.Model.Models.Model model) { using var s = new MemoryStream(); ModelArchive.Save(model, s); return System.Text.Encoding.UTF8.GetString(s.ToArray()); }
        Assert.IsFalse(Saved(Model(null)).Contains("DurabilityData")); Assert.IsTrue(Saved(m).Contains("DurabilityData"));
        Assert.AreNotEqual(Model(null).VerificationFingerprint("x"), Model(Data("XC1")).VerificationFingerprint("x"));
    }
}
