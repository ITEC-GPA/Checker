using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelBeamColumnWorkflowTest
{
    private static string Diagnostics(IEnumerable<CheckResult> results)=>string.Join(";",results.SelectMany(r=>r.Diagnostics).Select(d=>d.Message??d.Code));
    private static double Value(CheckResult r,string key)=>r.Details.Trace.Single(t=>t.Key==key).Value!.Value;

    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public void WholeWorkflowPreservesBiaxialStatesAcrossSubdivisionAndReversedIJ(bool ntc,bool reversed)
    {
        var single=SteelBeamColumnFrame.Create();var split=SteelBeamColumnFrame.Create(true,reversed);var catalog=EngineCatalog.BuiltIn();
        var expected=new Service().Verify(single,catalog.Compile(SteelBeamColumnFrame.Configure(single,ntc)));
        var config=ConfigurationArchive.Copy(SteelBeamColumnFrame.Configure(split,ntc));var request=catalog.Compile(config);
        var report=new Service().Verify(split,request);
        Assert.AreEqual(EngineeringOutcome.Satisfied,expected.Outcome,Diagnostics(expected.Jobs.SelectMany(j=>j.Results)));
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome,Diagnostics(report.Jobs.SelectMany(j=>j.Results)));
        Assert.AreEqual(66,report.Required);Assert.AreEqual(66,report.Executed);
        for(int j=0;j<4;j++) {
            var r=report.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            var e=expected.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            Assert.AreEqual(e.Utilization!.Value,r.Utilization!.Value,1e-12);
            bool second=j%2==1;
            Assert.AreEqual(second?30e6:10e6,Value(r,"Input.M1"),1e-6);
            Assert.AreEqual(second?5e6:8e6,Value(r,"Input.M2"),1e-6);
            Assert.AreEqual(0d,Value(r,"Member.Envelope.M1.Distance"));
            Assert.AreEqual(0d,Value(r,"Member.Envelope.M2.Distance")); // constant M2; first exported cut breaks ties
            Assert.IsTrue(r.MemberInput.Samples.All(s=>s.State.ConcomitantState==(second?"ULS-B":"ULS-A")));
            Assert.IsNull(r.Details.Metrics.Single().Demand); // interaction has no fictitious scalar force
            Assert.IsTrue(Value(r,"MemberStability.K22")>0);
            // Real section checks retain the constant concomitant M2 at each cut.
            foreach(var cut in report.Jobs[j].Results.Where(r=>r.Scope!=CheckScope.PhysicalMember))
                Assert.AreEqual(second?5e6:8e6,Math.Abs(Value(cut,"Input.M2")),1e-6);
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(restored,split,request));
        var trace=restored[0].Results.Single(r=>r.Scope==CheckScope.PhysicalMember).Details.Trace;
        Assert.IsTrue(trace.Any(t=>t.Key=="Member.MomentFactorSource"));
        Assert.IsTrue(trace.Any(t=>t.Key=="Member.Envelope.M2.Distance"&&t.Value==0));
        config.Engines[0].Parameters["Cm1"]="0.9";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,split,catalog.Compile(config)));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void DifferentControllingCutsDoNotMixStatesAndConcomitantBiaxialShearCompletes(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create(true,true,true);
        var report=new Service().Verify(model,EngineCatalog.BuiltIn().Compile(SteelBeamColumnFrame.Configure(model,ntc)));
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome); // both shear assessments are present in this example
        for(int j=0;j<4;j++) {
            var r=report.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,Diagnostics(new[]{r}));
            Assert.AreEqual(j%2==0?10e6:30e6,Value(r,"Input.M1"),1e-6);
            Assert.AreEqual(j%2==0?8e6:5e6,Value(r,"Input.M2"),1e-6);
            Assert.AreEqual(0d,Value(r,"Member.Envelope.M1.Distance"));Assert.AreEqual(3000d,Value(r,"Member.Envelope.M2.Distance"));
            Assert.IsTrue(report.Jobs[j].Results.Where(r=>r.Scope!=CheckScope.PhysicalMember)
                .All(r=>r.Outcome==EngineeringOutcome.Satisfied));
        }
    }

    private static CheckResult Single(GPC.Model.Models.Model model,Dictionary<string,string> options,bool ntc=false)
    {
        var plan=SteelReferenceFrame.Plan("C1","ULS-A",false);
        var input=BeamCheckPlan.PrepareActions(model,plan).WorkItems.Single().Member;
        options["MemberFingerprint"]=ModelValues.Fingerprint(new object[]{input.Snapshot});
        return new SteelMemberPipelineChecker(ntc?new StandardNTC2018Steel():new StandardEN1993p11(),ntc?"2018":"2005",parameters:options)
            .Verify(input,CancellationToken.None);
    }
    private static Dictionary<string,string> Options(GPC.Model.Models.Model model,bool ntc=false)
        =>SteelBeamColumnFrame.Configure(model,ntc).Engines[0].Parameters;
    private static void Susceptible(Dictionary<string,string> p)
    {
        // Adapter contract test: explicitly supplied independent bifurcation data,
        // not a calculation/certification of an unrestrained reference-frame member.
        p["TorsionSusceptible"]="true";p.Remove("LateralTorsionalExclusionSource");
        p["TorsionalExclusionSource"]="Synthetic adapter fixture: separately qualified non-governing torsional compression mode.";
        p["FlexuralTorsionalExclusionSource"]="Synthetic adapter fixture: separately qualified coupled compression mode.";
        p["Mcr.Value"]="100000000";p["Mcr.Unit"]="Nmm";p["Mcr.Source"]="Independent synthetic uniform-moment bifurcation fixture, 100kNm; not a frame-design result.";
    }

    [DataTestMethod] [DataRow(false,1)] [DataRow(false,2)] [DataRow(false,3)] [DataRow(true,1)] [DataRow(true,2)] [DataRow(true,3)]
    public void SusceptibleMembersHandleMajorMinorAndBiaxialBending(bool ntc,int axes)
    {
        var model=SteelBeamColumnFrame.Create(true,true);
        foreach(int id in new[]{100,101})foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[id],SteelReferenceFrame.Plan("C1","ULS-A").Results[0])) {
            if(axes==1){sample.ResultBeamForces.M2=0;sample.ResultBeamForces.V1=0;}
            if(axes==2){sample.ResultBeamForces.M1=0;sample.ResultBeamForces.V2=0;}
        }
        var options=Options(model,ntc);Susceptible(options);
        if(axes==2)foreach(string suffix in new[]{".Value",".Unit",".Source"})options.Remove("Mcr"+suffix);
        var r=Single(model,options,ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,Diagnostics(new[]{r}));
        Assert.AreEqual(axes==2?1d:0d,Value(r,"MemberStability.ChiLT")==1?1d:0d);
        Assert.IsTrue(r.Details.Trace.Any(t=>t.Key=="MemberStability.K21"));
    }

    [DataTestMethod] [DataRow("Cm1")] [DataRow("Cm2")] [DataRow("CmLT")] [DataRow("MomentFactorSource")]
    [DataRow("StabilityModelSource")] [DataRow("TorsionalExclusionSource")] [DataRow("FlexuralTorsionalExclusionSource")]
    [DataRow("ShearBucklingExclusionSource")] [DataRow("LateralTorsionalExclusionSource")]
    public void MissingMethodEvidenceCannotBecomeAPassingMember(string missing)
    {
        var model=SteelBeamColumnFrame.Create();var options=Options(model);options.Remove(missing);
        var r=Single(model,options);Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
    }

    [DataTestMethod] [DataRow("missing-mcr")] [DataRow("inconsistent-curve")] [DataRow("variable-N")]
    [DataRow("torque")] [DataRow("high-shear")]
    public void UnqualifiedConditionsLeaveTheMemberUnevaluated(string condition)
    {
        var model=SteelBeamColumnFrame.Create();var options=Options(model);var samples=ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[100],SteelReferenceFrame.Plan("C1","ULS-A").Results[0]);
        if(condition=="missing-mcr"){Susceptible(options);foreach(string suffix in new[]{".Value",".Unit",".Source"})options.Remove("Mcr"+suffix);}
        if(condition=="inconsistent-curve")options["TorsionalCurve"]="C";
        if(condition=="variable-N")samples.First().ResultBeamForces.N*=2;
        if(condition=="torque")samples.First().ResultBeamForces.T=1;
        if(condition=="high-shear")samples.First().ResultBeamForces.V2=1e8;
        var r=Single(model,options);Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
    }

    [TestMethod]
    public void PassingSectionsDoNotHideAMissingMemberAssessment()
    {
        var model=SteelBeamColumnFrame.Create();var config=SteelBeamColumnFrame.Configure(model,false);
        config.Engines[0].Parameters.Remove("MomentFactorSource");
        var report=new Service().Verify(model,EngineCatalog.BuiltIn().Compile(config));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,report.Outcome);
        Assert.IsTrue(report.Jobs[0].Results.Where(r=>r.Scope!=CheckScope.PhysicalMember).All(r=>r.Outcome==EngineeringOutcome.Satisfied));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,report.Jobs[0].Results.Single(r=>r.Scope==CheckScope.PhysicalMember).Outcome);
    }
}
