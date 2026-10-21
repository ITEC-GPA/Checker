using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

// EN1993-1-1:2005 +AC2009 Table6.2, printed p58 / PDF p60.
// https://unaribas.com/wp-content/uploads/2021/04/eurocode-3.pdf
// Normative table tests, not published numerical member examples. N, mm, MPa.
[TestClass]
public class WeldedBucklingCurveTests
{
    public static IEnumerable<object[]> BoxCases =>
        from fy in new[] { 235d, 460d }
        from axis in new[] { 1, 2 }
        from row in new[] {
            (B:200d,H:250d,Tf:10d,Tw:10d,A:5d,Curve:"b"),
            (B:200d,H:250d,Tf:10d,Tw:10d,A:5.001,Curve:"c"),
            (B:299.999,H:299.999,Tf:10d,Tw:10d,A:6d,Curve:"c"),
            (B:300d,H:250d,Tf:10d,Tw:10d,A:6d,Curve:"b"),
            (B:200d,H:300d,Tf:10d,Tw:10d,A:6d,Curve:"b"),
            (B:350d,H:250d,Tf:10d,Tw:10d,A:6d,Curve:"b"),
            (B:200d,H:250d,Tf:10d,Tw:10d,A:0d,Curve:"c"),
            (B:350d,H:250d,Tf:10d,Tw:10d,A:0d,Curve:"b"),
            (B:200d,H:250d,Tf:20d,Tw:10d,A:8d,Curve:"b")
        }
        select new object[] {fy,axis,row.B,row.H,row.Tf,row.Tw,row.A,row.Curve};

    [DataTestMethod, DynamicData(nameof(BoxCases))]
    public void WeldedBoxTableAndStrictBoundaries(double fy,int axis,double b,double h,double tf,double tw,double a,string expected)
    {
        // Exact SectionRHS + Welded: Model documents R as weld throat; zero is
        // unspecified, never evidence that the weld is thin. Both axes use same curve.
        var s = Steel(new SectionRHS(h,b,tf,tf,tw,tw,"welded box",a),fy);
        Assert.AreEqual(expected,new ReferenceProbe(s).Curve(axis));
    }

    public static IEnumerable<object[]> ICases =>
        from fy in new[] {235d,355d,460d}
        from axis in new[]{1,2}
        from row in new[] {
            (H:600d,B:300d,Tf:39.999,Tw:10d,Y:"b",Z:"c"),
            (H:600d,B:300d,Tf:40d,Tw:10d,Y:"b",Z:"c"),
            (H:600d,B:300d,Tf:40.001,Tw:10d,Y:"c",Z:"d"),
            (H:600d,B:300d,Tf:20d,Tw:50d,Y:"b",Z:"c"),
            (H:200d,B:600d,Tf:20d,Tw:10d,Y:"c",Z:"b"),
            (H:250d,B:600d,Tf:50d,Tw:10d,Y:"d",Z:"c")
        }
        select new object[]{fy,axis,row.H,row.B,row.Tf,row.Tw,axis==1?row.Y:row.Z};

    [DataTestMethod, DynamicData(nameof(ICases))]
    public void WeldedIUsesFlangeThicknessAndCorrectPrincipalAxis(double fy,int axis,double h,double b,double tf,double tw,string expected)
    {
        var s = Steel(new SectionH(h,tw,b,tf,b,tf,"welded I"),fy);
        Assert.AreEqual(expected,new ReferenceProbe(s).Curve(axis));
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)]
    public void ThickWeldCorrectionChangesReductionButNotElasticCriticalForce(int axis)
    {
        // Independent rectangle integration of a 250x200 box, t=10; L=4000mm.
        double area = 250d*200-230d*180;
        double inertia = axis==1 ? (200d*Math.Pow(250,3)-180d*Math.Pow(230,3))/12
            : (250d*Math.Pow(200,3)-230d*Math.Pow(180,3))/12;
        double ncr = Math.PI*Math.PI*210000*inertia/16000000;
        double lambda = Math.Sqrt(area*235/ncr);
        var thin = new ReferenceProbe(Steel(new SectionRHS(250,200,10,10,10,10,"thin weld",5),235),4000,1);
        var thick = new ReferenceProbe(Steel(new SectionRHS(250,200,10,10,10,10,"thick weld",6),235),4000,1);
        Close(ncr,thin.Euler(4000,inertia)); Close(ncr,thick.Euler(4000,inertia));
        Close(area*235*Reduction(lambda,.34),thin.Nb(axis,3));
        Close(area*235*Reduction(lambda,.49),thick.Nb(axis,3));
        Assert.IsTrue(thick.Nb(axis,3)<thin.Nb(axis,3));
    }

    [TestMethod]
    public void UnequalWallsAndDerivedRadiusDoNotReceiveAnUnqualifiedFavourableCurve()
    {
        var unequal = Steel(new SectionRHS(250,200,10,12,10,10,"unequal",4),235);
        var rounded = Steel(new SectionRHSRoundedCorners(250,200,10,20,10,"radius is not throat"),235);
        foreach(var s in new[]{unequal,rounded}) foreach(int axis in new[]{1,2})
            Assert.AreEqual("c",new ReferenceProbe(s).Curve(axis));
    }

    [DataTestMethod]
    [DataRow(235d, Section.FormedTypes.HotFinished, "a")]
    [DataRow(460d, Section.FormedTypes.HotFinished, "a0")]
    [DataRow(235d, Section.FormedTypes.ColdFormed, "c")]
    public void HollowProductCurvesRemainDistinctFromWeldedBox(double fy,Section.FormedTypes formation,string expected)
    {
        var s = new SteelSection(new SectionRHS(250,200,10,10,10,10,"hollow",6),
            new SteelMaterial("nominal",210000,fy,550),Section.SectionTypes.Rolled,formation);
        foreach(int axis in new[]{1,2}) Assert.AreEqual(expected,new ReferenceProbe(s).Curve(axis));
    }

    private static SteelSection Steel(Section shape,double fy) => new(shape,
        new SteelMaterial("nominal",210000,fy,550),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished);
}
