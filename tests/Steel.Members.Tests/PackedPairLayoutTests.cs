using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class PackedPairLayoutTests
{
    static SteelPackedPairAssessment Assessment(SteelPackingLayout layout, double connectionResistance = 20000)
        => SteelPackedPairAssessment.FromLayout(layout, 10, 10, 60000, 0, 10000, connectionResistance,
            "Complete independent local force envelope for this same input, including imperfections",
            "Independently established full station strength including chord walls and end load transfer",
            "Axial chord panels, centroidal loading, forks and free warping; no local moments");
    static SteelCheckExecution Run(SteelPackedPairAssessment p)
        => SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(SteelMemberAnalysis.PackedCompression(
            p, "Same constant compression case", "Explicit curvesC", SteelBucklingCurve.C, SteelBucklingCurve.C, SteelBucklingCurve.C,
            torsionalExclusionSource: "Independent torsional assessment", flexuralTorsionalExclusionSource: "Independent FT assessment"))).Checks.Single();

    [DataTestMethod] [DataRow("C",false)] [DataRow("C",true)] [DataRow("L-long",false)] [DataRow("L-long",true)]
    [DataRow("L-short",false)] [DataRow("L-short",true)]
    public void ActualStationsControlChordSpanAndAreRetainedInGlobalResult(string kind, bool ntc)
    {
        var input = PackedPairCompressionTests.Input(kind,ntc);
        var stations = Enumerable.Range(0,31).Select(i=>100d*i).ToArray(); stations[29]=2875;
        var layout = new SteelPackingLayout(input,stations,"Actual end gussets and all intermediate packing centres");
        stations[29]=2900; // Defensive copy.
        var assessment = Assessment(layout); var result=Run(assessment);
        Assert.AreEqual(125d,layout.MaximumPitch); Assert.AreEqual(29,layout.GoverningPanelIndex);
        Assert.AreEqual(125d,assessment.ChordCalculation.Analysis.Input.Member.Length);
        Assert.AreSame(input,assessment.Input); Assert.AreSame(layout,assessment.Layout);
        Assert.AreEqual(SteelExecutionStatus.Passed,result.Status,result.Message);
        Assert.AreEqual(2875d,result.Trace.Single(v=>v.Name=="PackedPair.ConnectionStation29").Value);
        Assert.AreEqual(125d,result.Trace.Single(v=>v.Name=="PackedPair.PanelLength29").Value);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<double>)layout.Stations)[0]=2);
    }

    [DataTestMethod] [DataRow("end")] [DataRow("start")] [DataRow("duplicate")] [DataRow("order")]
    [DataRow("nan")] [DataRow("source")] [DataRow("outside")]
    public void MissingEndConnectionsAndInvalidStationListsAreRejected(string problem)
    {
        var x = new[] {0d,100,200,3000};
        if(problem=="end")x[3]=2999; if(problem=="start")x[0]=1;
        if(problem=="duplicate")x[1]=0; if(problem=="order")x[2]=99;
        if(problem=="nan")x[1]=double.NaN; if(problem=="outside")x[2]=3001;
        Assert.ThrowsException<ArgumentException>(() => {
            try {new SteelPackingLayout(PackedPairCompressionTests.Input("C"),x,problem=="source"?"":"Details");}
            catch(ArgumentOutOfRangeException e){throw new ArgumentException(e.Message);}
        });
    }

    [TestMethod]
    public void OmittedInteriorPackingLeavesALongPanelAndCannotPass()
    {
        var layout = new SteelPackingLayout(PackedPairCompressionTests.Input("C"),new[]{0d,100,200,3000},"Actual stations");
        var result=Run(Assessment(layout));
        Assert.IsFalse(result.IsEvaluated); Assert.AreNotEqual(SteelExecutionStatus.Passed,result.Status);
    }

    [TestMethod]
    public void ConnectionFailureCannotBeHiddenByValidPitchOrGlobalMember()
    {
        var layout=new SteelPackingLayout(PackedPairCompressionTests.Input("C"),Enumerable.Range(0,31).Select(i=>100d*i),"All centres");
        var result=Run(Assessment(layout,5000));
        Assert.AreEqual(SteelExecutionStatus.Failed,result.Status); Assert.AreEqual(2d,result.Utilization!.Value);
    }
}
