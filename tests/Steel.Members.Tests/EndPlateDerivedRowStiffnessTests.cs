using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.EndPlateInitialCaseTestData;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateDerivedRowStiffnessTests
{
    static EndPlateBoltElongationDetail[] Hardware(bool cjp=false)
        =>Enumerable.Range(0,cjp?2:3).Select(i=>new EndPlateBoltElongationDetail(i,70.5,
            "Actual grip25+20.5+washers4+4+half(head15+nut19)=70.5mm")).ToArray();

    [DataTestMethod]
    [DataRow(false,false,0d,237302576169.69534)] [DataRow(true,false,0d,237302576169.69534)]
    [DataRow(false,false,1d,90338344812.44681)] [DataRow(true,false,1d,90338344812.44681)]
    [DataRow(false,true,0d,152350333980.8783)] [DataRow(true,true,0d,152350333980.8783)]
    [DataRow(false,true,1d,69580519870.4866)] [DataRow(true,true,1d,69580519870.4866)]
    public void GeometryDerivedRowsMatchIndependentNumericalOracleWithoutManualLengths(bool ntc,bool cjp,double beta,double expected)
    {
        // Same independent spring calculation already recorded in derive_end_plate_initial_case.py.
        // Alpha7.5 is the qualified C1 graphical input; hardware is an explicit test detail.
        var parent=Parent(ntc,cjp,beta:beta);
        var input=EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(parent,Hardware(cjp));
        var r=EndPlateInitialStiffnessFromMoment.Calculate(input);
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));Assert.IsTrue(input.LengthsDerivedFromGeometry);
        Assert.AreEqual(expected,r.InitialStiffness!.Value,expected*1e-10);Assert.AreSame(parent,r.Parent);
        var oracle=Details(cjp);
        for(int i=0;i<oracle.Length;i++) {
            Assert.AreEqual(oracle[i].ColumnLength.Minimum,input.Rows[i].ColumnLength.Minimum,1e-9);
            Assert.AreEqual(oracle[i].EndPlateLength.Minimum,input.Rows[i].EndPlateLength.Minimum,1e-9);
            Assert.AreEqual(oracle[i].ColumnLength.RelevantRowLengths.Count,input.Rows[i].ColumnLength.RelevantRowLengths.Count);
        }
        Assert.IsFalse(r.CanQualifyWholeJoint);Assert.IsTrue(r.PendingChecks.Any(s=>s.Contains("net and block")));
        Assert.IsTrue(r.PendingChecks.Any(s=>s.Contains("derived for EACH row")));
    }

    [TestMethod]
    public void UnequalPitchColumnUsesRowContributionsNotGroupAverage()
    {
        var g=TStubEffectiveGeometry.UnstiffenedColumnFlange(new(258.8,12.8,12.7,250,100,new[]{0d,100,190},1000,1000,"Table6.4 geometry"));
        Assert.IsTrue(g.IsSupported);Assert.AreEqual(7,g.RowGroupContributions.Count);
        var triple=g.RowGroupContributions.Where(c=>c.FirstRow==0&&c.RowCount==3).OrderBy(c=>c.RowIndex).ToArray();
        Assert.AreEqual(166.505,triple[0].NonCircularLength,1e-10);
        Assert.AreEqual(95d,triple[1].NonCircularLength,1e-10);
        Assert.AreEqual(161.505,triple[2].NonCircularLength,1e-10);
        Assert.AreEqual(95d,g.StiffnessLength(1).Minimum);
        Assert.AreNotEqual(triple.Sum(x=>x.NonCircularLength)/3,g.StiffnessLength(1).Minimum);
        // Group strength still sums circular and non-circular terms BEFORE the minimum.
        foreach(var group in g.AdjacentGroups) {
            var terms=g.RowGroupContributions.Where(c=>c.FirstRow==group.FirstRow&&c.RowCount==group.RowCount);
            Assert.AreEqual(Math.Min(terms.Sum(c=>c.CircularLength),terms.Sum(c=>c.NonCircularLength)),group.EffectiveLengths.Mode1,1e-10);
            Assert.AreEqual(terms.Sum(c=>c.NonCircularLength),group.EffectiveLengths.Mode2,1e-10);
        }
    }

    [TestMethod]
    public void InternalFirstRowKeepsFlangeBoundaryContributionAndExtensionStaysSeparate()
    {
        var parent=Parent();var p=TStubInternalEndPlateLengths.Calculate(parent.Input.InternalGeometry);
        Assert.AreEqual(210.15,p.StiffnessLength(0).Minimum,1e-10);
        Assert.AreEqual(168.975,p.StiffnessLength(1).Minimum,1e-10);
        var outer=TStubEffectiveGeometry.ExtendedEndPlateOuterRow(parent.Input.ExtensionGeometry);
        Assert.AreEqual(0,outer.AdjacentGroups.Count);Assert.AreEqual(0,outer.RowGroupContributions.Count);
        Assert.AreEqual(125d,outer.StiffnessLength(0).Minimum);
        Assert.AreEqual(1,outer.StiffnessLength(0).RelevantRowLengths.Count);
    }

    [DataTestMethod] [DataRow("missing")] [DataRow("extra")] [DataRow("duplicate")] [DataRow("reverse")]
    public void HardwareMustFollowEachActualParentRowExactlyOnce(string issue)
    {
        var rows=Hardware().ToList();if(issue=="missing")rows.RemoveAt(0);
        if(issue=="extra")rows.Add(new(3,70.5,"extra"));if(issue=="duplicate")rows[1]=rows[0];if(issue=="reverse")rows.Reverse();
        Assert.ThrowsException<ArgumentException>(()=>EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(Parent(),rows));
    }

    [TestMethod]
    public void KnownParentFailureRemainsFailureAndExportsNoStiffness()
    {
        var parent=Parent(moment:700e6);var p=EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(parent,Hardware());
        var r=EndPlateInitialStiffnessFromMoment.Calculate(p);
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsNull(r.InitialStiffness);Assert.AreSame(parent,r.Parent);
    }

    [TestMethod]
    public void UnsupportedFreeEndCannotExportDerivedLengths()
    {
        var g=TStubEffectiveGeometry.UnstiffenedColumnFlange(new(258.8,12.8,12.7,250,100,new[]{0d,100},20,1000,"near free end"));
        Assert.IsFalse(g.IsSupported);Assert.AreEqual(0,g.RowGroupContributions.Count);
        Assert.ThrowsException<InvalidOperationException>(()=>g.StiffnessLength(0));
    }

    [TestMethod]
    public void HardwareTooShortStillFailsAfterEffectiveLengthAutomation()
    {
        var rows=Hardware();rows[1]=new(1,45.5,"Only the two plates: insufficient hardware length");
        var r=EndPlateInitialStiffnessFromMoment.Calculate(EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(Parent(),rows));
        Assert.IsFalse(r.IsSupported);Assert.IsNull(r.InitialStiffness);
    }
}
