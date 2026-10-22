using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Attributes;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelPhysicalMemberWorkflowTest
{
    [TestMethod]
    public void DirectPreparedMemberUsesTheSameQualifiedAdapter()
    {
        var model=SteelReferenceFrame.Create(true,true);var plan=SteelReferenceFrame.Plan("C1","ULS-A",false);
        var options=SteelReferenceFrame.Configure(model,false).Engines[0].Parameters;
        var input=BeamCheckPlan.PrepareActions(model,plan).WorkItems.Single().Member;
        options["MemberFingerprint"]=ModelValues.Fingerprint(new object[]{input.Snapshot});
        var result=new SteelMemberPipelineChecker(new StandardEN1993p11(),"2005",parameters:options).Verify(input,CancellationToken.None);
        Assert.AreEqual(EngineeringOutcome.Satisfied,result.Outcome);
    }
    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public void SubdivisionAndReversedConnectivityPreserveThePhysicalChecks(bool ntc,bool reversed)
    {
        var catalog=EngineCatalog.BuiltIn();var single=SteelReferenceFrame.Create();var split=SteelReferenceFrame.Create(true,reversed);
        var reference=new Service().Verify(single,catalog.Compile(SteelReferenceFrame.Configure(single,ntc)));
        var config=ConfigurationArchive.Copy(SteelReferenceFrame.Configure(split,ntc));var request=catalog.Compile(config);
        var actual=new Service().Verify(split,request);
        Assert.AreEqual(66,actual.Required);Assert.AreEqual(66,actual.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied,actual.Outcome,string.Join(";",actual.Jobs.SelectMany(j=>j.Results).SelectMany(r=>r.Diagnostics).Select(d=>d.Message??d.Code)));
        for(int k=0;k<6;k++) {
            var r=reference.Jobs[k].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            var a=actual.Jobs[k].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            Assert.AreEqual(r.Utilization!.Value,a.Utilization!.Value,1e-12);
            Assert.AreEqual(r.Details.Metrics.Single().Demand,a.Details.Metrics.Single().Demand);
            Assert.AreEqual(2d,a.Details.Trace.Single(t=>t.Key=="Member.Geometry.ElementCount").Value);
            Assert.AreEqual(10,a.MemberInput.Samples.Count);
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(actual.Jobs,stream);stream.Position=0;
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(CheckReportArchive.Load(stream),split,request));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExplicitDesignSpanAndEffectiveLengthsAreIndependentOfTheMesh(bool ntc)
    {
        var model=SteelReferenceFrame.Create(true,true);var plan=SteelReferenceFrame.Plan("C1","ULS-A",false);
        plan.MemberChecks[0].Start=750;plan.MemberChecks[0].End=2250;
        plan.MemberChecks[0].Context=new MemberDesignContext(effectiveLength1:4000,effectiveLength2:5000,
            lateralTorsionalLength:1500,lengthSource:"External restraint analysis; different from 1500mm span and FE lengths",globalStateConfirmed:true);
        var options=SteelReferenceFrame.Configure(model,ntc).Engines.Single(e=>e.Id=="C1-ULS-A-member").Parameters;
        var work=BeamCheckPlan.PrepareActions(model,plan).WorkItems.Single();
        Assert.AreEqual(DataStatus.Ready,work.Data);
        options["MemberFingerprint"]=ModelValues.Fingerprint(new object[]{work.Member.Snapshot});
        var result=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,options,ntc?new StandardNTC2018Steel():new StandardEN1993p11())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied,result.Outcome,string.Join(";",result.Diagnostics.Select(d=>d.Message??d.Code)));
        Assert.AreEqual(4000d,result.Details.Trace.Single(t=>t.Key=="MemberStability.Ncr1.EffectiveLength").Value);
        Assert.AreEqual(5000d,result.Details.Trace.Single(t=>t.Key=="MemberStability.Ncr2.EffectiveLength").Value);
        Assert.AreEqual(750d,result.Details.Trace.Single(t=>t.Key=="Member.Geometry.SpanStart").Value);
        Assert.AreEqual(2250d,result.Details.Trace.Single(t=>t.Key=="Member.Geometry.SpanEnd").Value);
    }

    [DataTestMethod] [DataRow("internal release")] [DataRow("section rotation")] [DataRow("hidden section")]
    public void DiscontinuitiesCannotBeHiddenByAnUnchangedSectionAtExportedCuts(string problem)
    {
        var model=SteelReferenceFrame.Create(true,true,m=> {
            var beam=m.BeamElements[101];
            if(problem=="internal release") {
                var release=new BeamReleasesAttribute{CoordinateSystem=beam.Assignments.SectionAxes};
                release.J[3]=new BeamDofConnection(BeamConnectionKind.Released);beam.Attributes.Add(release);
            }
            if(problem=="section rotation") {
                beam.Assignments.SectionAxes=Axes.Beam(beam.StartPoint,beam.EndPoint,new Vector3d(0,1,0));
                beam.Assignments.SectionGeometryAxes=beam.Assignments.SectionAxes;
            }
            if(problem=="hidden section") {
                var original=beam.BeamProperty;var other=new SteelSection(new SectionH(250,10,140,16,140,16,"hidden"),SteelMaterialEN1993Data.S355);
                beam.Assignments.Sections.Clear();
                beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=0,End=.3,Property=original});
                beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=.3,End=.4,Property=other});
                beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=.4,End=1,Property=original});
            }
        });
        var config=SteelReferenceFrame.Configure(model,false);var report=new Service().Verify(model,EngineCatalog.BuiltIn().Compile(config));
        var result=report.Jobs[0].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,result.Outcome);Assert.IsNull(result.Utilization);
        Assert.IsTrue(result.Diagnostics.Any(d=>d.Code=="SteelMember.NotSupported"));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void EveryInternalInterfaceRequiresBothElementFaces(bool missingAtStart)
    {
        var model=SteelReferenceFrame.Create(true,true);
        // Change the case identity to remove one interface face from this design state.
        int id=missingAtStart?101:100;double station=1; // both reference elements end at the common node
        var sample=ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[id],SteelReferenceFrame.Plan("C1","ULS-A").Results[0])
            .Single(s=>s.ParametricDistance==station);sample.State.ConcomitantStateId="other";
        var config=SteelReferenceFrame.Configure(model,false);
        var r=new Service().Verify(model,EngineCatalog.BuiltIn().Compile(config)).Jobs[0].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Code=="SteelMember.InvalidOrMissingInput"));
    }
}
