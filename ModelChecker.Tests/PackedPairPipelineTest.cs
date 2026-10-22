using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class PackedPairPipelineTest
{
    private static (GPC.Model.Models.Model Model, BeamCheckPlanRequest Plan, Dictionary<string, string> Options) Fixture(bool channel, bool shortLeg = false, bool compactChord = false)
    {
        Section component = channel ? new SectionC(200, 8, 80, 12, 80, 12, "C") : new SectionL(100, compactChord ? 16 : 10, 150, compactChord ? 16 : 10, "L");
        component.SetEdgeTypeFromSteelType(Section.SectionTypes.Rolled);
        var pair = channel ? SectionBuiltUp.DoubleChannel((SectionC)component, 10, true, "2C")
            : SectionBuiltUp.DoubleAngle((SectionL)component, 10, shortLeg ? SectionBuiltUp.AnglePairJoinedLeg.Short : SectionBuiltUp.AnglePairJoinedLeg.Long, "2L");
        var model = ElementScopeCharacterizationTest.Model(axial: -1e5, shear: 0, configureBeforeResults: m =>
        {
            foreach (var beam in m.BeamElements.Values)
            {
                var property = new SteelSection(pair, SteelMaterialEN1993Data.S355);
                beam.BeamProperty = property; beam.Assignments.Sections.Clear();
                beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = property });
            }
        });
        model.PhysicalMembers.Add("S1", new PhysicalMemberDefinition("S1", new[] { new BeamMemberPart(250, true) }, "NodeToNode", source: "Packed-pair test"));
        var plan = SteelMemberPipelineTest.Fixture().Plan;
        var options = SteelMemberPipelineTest.Options(model, plan);
        foreach (var key in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) options.Remove(key);
        options["AnalysisMode"] = "Compression";
        options["CompressionCurve1"] = "C"; options["CompressionCurve2"] = "C"; options["TorsionalCurve"] = "C";
        options["Ncr1.Value"] = "3000"; options["Ncr1.Unit"] = "KN"; options["Ncr1.Source"] = "Independent global flexural eigenproblem axis1";
        options["Ncr2.Value"] = "1500"; options["Ncr2.Unit"] = "KN"; options["Ncr2.Source"] = "Independent global flexural eigenproblem axis2";
        options["TorsionalExclusionSource"] = "Explicit continuous twist restraint in this fixture";
        options["FlexuralTorsionalExclusionSource"] = "Explicit continuous twist restraint in this fixture";
        options["MemberClass"] = "3"; options["ClassificationSource"] = "Conservative independent component classification";
        options["LoadPatternReference"] = "Constant concentric compression of the complete packed pair";
        options["Packed.Pitch"] = "200"; options["Packed.Thickness"] = "10"; options["Packed.GussetThickness"] = "10";
        options["Packed.ChordCompression"] = "60000"; options["Packed.ChordCompressionResistance"] = "100000";
        options["Packed.ChordTension"] = "0"; options["Packed.ChordTensionResistance"] = "100000";
        options["Packed.ConnectionDemand"] = "10000"; options["Packed.ConnectionResistance"] = "50000";
        options["Packed.DetailingSource"] = "200 mm maximum including end panels; full packing and gusset contact";
        options["Packed.AnalysisSource"] = "Synthetic complete local envelopes including imperfections, second order and introduction eccentricities";
        options["Packed.ResistanceSource"] = "Synthetic minimum design resistances including local stability and every connection failure mode";
        return (model, plan, options);
    }

    [TestMethod]
    public void GlobalClassDeclarationCannotHideAClass4LocalAngle()
    {
        var (model,plan,p)=Fixture(false);
        p.Remove("Packed.ChordCompressionResistance");p.Remove("Packed.ChordTensionResistance");
        p["Packed.ChordModel"]="UniformFork";p["Packed.ChordModelSource"]="Explicit local fork model";
        var r=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Message!=null&&d.Message.Contains("Class4")));
    }

    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public void ComputedChordResistanceIsOptionalAndPersisted(bool channel,bool ntc)
    {
        var (model,plan,p)=Fixture(channel,compactChord:true);
        p.Remove("Packed.ChordCompressionResistance");p.Remove("Packed.ChordTensionResistance");
        p["Packed.ChordModel"]="UniformFork";p["Packed.ChordModelSource"]="Each chord panel: centroidal axial force, no M/V, fork/free warping over pitch";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var request=SteelMemberPipelineTest.Request(plan,p,ntc?new StandardNTC2018Steel():new StandardEN1993p11());
        var report=new Service().Verify(model,request);var result=report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied,result.Outcome,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
        Assert.IsTrue(result.Details.Trace.Any(t=>t.Key=="MemberStability.PackedPair.Chord.NcrFT"));
        Assert.IsTrue(result.Details.Trace.Any(t=>t.Key=="Member.PackedPair.ChordModelSource"&&t.Expression==p["Packed.ChordModelSource"]));
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(CheckReportArchive.Load(stream),model,request));
        p["Packed.ChordCompressionResistance"]="1e9";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p)).Outcome);
    }

    [DataTestMethod]
    [DataRow(false, false, false)] [DataRow(false, true, false)] [DataRow(true, false, false)]
    [DataRow(false, false, true)] [DataRow(false, true, true)] [DataRow(true, false, true)]
    public void BothCodesPersistThePackingQualificationAndLocalEnvelope(bool channel, bool shortLeg, bool ntc)
    {
        var (model, plan, options) = Fixture(channel, shortLeg);
        var code = ntc ? new StandardNTC2018Steel() : new StandardEN1993p11();
        var request = SteelMemberPipelineTest.Request(plan, options, code);
        var report = new Service().Verify(model, request); var result = report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, string.Join(";", result.Diagnostics.Select(d => d.Message ?? d.Code)));
        Assert.AreEqual(.6, result.Utilization!.Value, 1e-12);
        Assert.IsTrue(result.Details.Trace.Any(t => t.Key == "Member.PackedPair.LocalAnalysisSource" && t.Expression == options["Packed.AnalysisSource"]));
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var restored = CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(restored, model, request));
        options["Packed.Pitch"] = "201";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(restored, model, SteelMemberPipelineTest.Request(plan, options, code)));
    }

    [DataTestMethod]
    [DataRow("Packed.ChordCompressionResistance", "40000")]
    [DataRow("Packed.ChordTension", "120000")]
    [DataRow("Packed.ConnectionResistance", "8000")]
    public void PassingGlobalMemberCannotHideAFailingLocalCheck(string key, string value)
    {
        var (model, plan, options) = Fixture(false); options[key] = value;
        var result = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, options)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome);
        Assert.IsTrue(result.Utilization > 1);
    }

    [DataTestMethod]
    [DataRow("Packed.AnalysisSource")] [DataRow("Packed.Pitch")] [DataRow("Packed.ConnectionResistance")]
    [DataRow("TorsionalExclusionSource")] [DataRow("all-packing")]
    public void MissingQualificationsCannotPass(string missing)
    {
        var (model, plan, options) = Fixture(true);
        if (missing == "all-packing") foreach (var key in options.Keys.Where(k => k.StartsWith("Packed.")).ToArray()) options.Remove(key);
        else options.Remove(missing);
        var result = new Service().Verify(model, SteelMemberPipelineTest.Request(plan, options)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome); Assert.IsNull(result.Utilization);
    }
}
