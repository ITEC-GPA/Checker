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
    [DataRow("LateralTorsionalExclusionSource")] // ShearBucklingExclusionSource: exact H now screened automatically, see below
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

    private static IEnumerable<StationResultBeamForces> C1A(GPC.Model.Models.Model model)
        =>ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[100],SteelReferenceFrame.Plan("C1","ULS-A").Results[0]);

    [DataTestMethod] [DataRow(false,"zero")] [DataRow(true,"zero")] [DataRow(false,"tension")] [DataRow(true,"tension")]
    [DataRow(false,"varying-tension")] [DataRow(true,"varying-tension")]
    public void BeamColumnModeCoversBiaxialBendingWithoutCompression(bool ntc,string axial)
    {
        // H300x150x12/18 S355 (R=0), ULS-A: M1max=10kNm, M2=8kNm, class 1 (web d/tw=22, flanges c/t=3.83).
        // N>=0 gives n1=n2=0: k11=Cm1=1, k22=Cm2=1, k12=.6 k22, k21=.6 k11 (EC3 Table B.1, Circolare C4.2.IV, classes 1-2).
        // Wpl1=2*150*18*141+12*132^2=970488 mm3, Wpl2=2*18*150^2/4+264*12^2/4=212004 mm3.
        var model=SteelBeamColumnFrame.Create();int k=0;
        foreach(var sample in C1A(model))sample.ResultBeamForces.N=axial=="zero"?0:axial=="tension"?2e4:2e4+1e3*k++;
        var r=Single(model,Options(model,ntc),ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,Diagnostics(new[]{r}));
        double g=ntc?1.05:1,m1=10e6*g/(970488*355d),m2=8e6*g/(212004*355d),expected=Math.Max(m1+.6*m2,.6*m1+m2);
        Assert.AreEqual(expected,r.Utilization!.Value,expected*1e-9);
        Assert.AreEqual(1,Value(r,"MemberStability.K11"),1e-12);Assert.AreEqual(.6,Value(r,"MemberStability.K12"),1e-12);
        Assert.AreEqual(.6,Value(r,"MemberStability.K21"),1e-12);Assert.AreEqual(1,Value(r,"MemberStability.K22"),1e-12);
        if(axial!="zero")Assert.AreEqual(2e4,Value(r,"MemberStability.TensionNeglectedAsFavourable"));
        Assert.AreEqual(axial=="varying-tension",r.Details.Trace.Any(t=>t.Key=="Member.Envelope.N"&&t.Value==2e4));
        // Mixed compression and tension along the span is not a constant compression: still refused.
        C1A(model).First().ResultBeamForces.N=-2e4;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Single(model,Options(model,ntc),ntc).Outcome);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExactHShearBucklingIsScreenedAutomaticallyAndAnExplicitSourceWins(bool ntc)
    {
        // Was NotEvaluated without ShearBucklingExclusionSource. H300 S355 web hw/tw=264/12=22 <= 72 eps/eta:
        // EC3 6.2.6(6) with eta=1.2 of StandardEN1993p11: 72*0.8136/1.2=48.8; NTC eq. 4.2.27, eta=1: 58.6. V1=0: no flange screen.
        var model=SteelBeamColumnFrame.Create();var options=Options(model,ntc);options.Remove("ShearBucklingExclusionSource");
        var r=Single(model,options,ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,Diagnostics(new[]{r}));
        Assert.AreEqual(1,Value(r,"MemberStability.ShearAssessment2.Excluded"));
        Assert.AreEqual(22,Value(r,"MemberStability.ShearAssessment2.WebSlenderness"),1e-12);
        Assert.AreEqual(72*Math.Sqrt(235d/355)/(ntc?1:1.2),Value(r,"MemberStability.ShearAssessment2.WebSlendernessLimit"),1e-9);
        Assert.IsFalse(r.Details.Trace.Any(t=>t.Key.StartsWith("MemberStability.ShearAssessment1")));
        // By hand: A=8568 mm2, I1=(150*300^3-138*264^3)/12=125902944, I2=2*18*150^3/12+264*12^3/12=10163016 mm4, L1=L2=3000,
        // curves b/c, N=-20kN, M1=10kNm, M2=8kNm, Cm=1, classes 1-2 restrained (Table B.1 / C4.2.IV with the NTC kzz envelope).
        double g=ntc?1.05:1,nrk=8568*355d,l1=Math.Sqrt(nrk/(Math.PI*Math.PI*210000*125902944/9e6)),l2=Math.Sqrt(nrk/(Math.PI*Math.PI*210000*10163016/9e6));
        static double Chi(double l,double a){double p=.5*(1+a*(l-.2)+l*l);return 1/(p+Math.Sqrt(p*p-l*l));}
        double n1=2e4*g/(Chi(l1,.34)*nrk),n2=2e4*g/(Chi(l2,.49)*nrk),m1=10e6*g/(970488*355d),m2=8e6*g/(212004*355d);
        double k11=1+Math.Min(l1-.2,.8)*n1,k22=1+Math.Min(2*l2-.6,1.4)*n2;if(ntc)k22=Math.Max(k22,1+Math.Min(2*l1-.6,1.4)*n1);
        double expected=Math.Max(n1+k11*m1+.6*k22*m2,n2+.6*k11*m1+k22*m2);
        Assert.AreEqual(expected,r.Utilization!.Value,expected*1e-9);
        var explicitSource=Single(model,Options(model,ntc),ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied,explicitSource.Outcome);Assert.AreEqual(r.Utilization,explicitSource.Utilization);
        Assert.IsFalse(explicitSource.Details.Trace.Any(t=>t.Key.StartsWith("MemberStability.ShearAssessment")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BendingModeSendsBiaxialMembersToBeamColumnAndMinorBendingAloneNeedsNoLtb(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();
        var options=new Dictionary<string,string>{["AnalysisMode"]="Bending",["Unperforated"]="true",["LoadPatternReference"]="C1 ULS-A",
            ["ParameterSet"]="test policy",["McrBasis"]="UniformMoment",["Mcr.Value"]="1000000000",["Mcr.Unit"]="Nmm",["Mcr.Source"]="synthetic fixture"};
        var biaxial=Single(model,new Dictionary<string,string>(options),ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,biaxial.Outcome);
        Assert.IsTrue(biaxial.Diagnostics.Any(d=>d.Code=="SteelMember.BiaxialBendingRequiresBeamColumn"),Diagnostics(new[]{biaxial}));
        // Tension with M2 alone: no lateral-torsional demand; N, M2 and shear stay with the section engine.
        foreach(var sample in C1A(model)) {sample.ResultBeamForces.N=1e4;sample.ResultBeamForces.M1=0;sample.ResultBeamForces.V2=0;}
        var minor=Single(model,options,ntc);
        Assert.AreEqual("NotRequired",minor.Details.Trace.Single(t=>t.Key=="MemberStability.Status").Expression);
        Assert.AreEqual(0,minor.Diagnostics.Count,Diagnostics(new[]{minor}));
    }
}
