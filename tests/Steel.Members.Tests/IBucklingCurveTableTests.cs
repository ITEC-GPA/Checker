using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Probe = Steel.Members.Tests.PublishedBucklingBenchmarks.BucklingProbe;

namespace Steel.Members.Tests;

[TestClass]
public class IBucklingCurveTableTests
{
    // EN 1993-1-1:2005 Table 6.2, p58. Each row is a normative table entry,
    // not output copied from the implementation. y/z are geometric axes.
    public static IEnumerable<object[]> TableCases
    {
        get
        {
            foreach (double fy in new[] {235d,275,355,420,460})
            foreach (var row in new[]
            {
                (H:600d,B:300d,T:39.999,Cy:"a",Cz:"b",Sy:"a0",Sz:"a0"),
                (H:600d,B:300d,T:40d,Cy:"a",Cz:"b",Sy:"a0",Sz:"a0"),
                (H:600d,B:300d,T:40.001,Cy:"b",Cz:"c",Sy:"a",Sz:"a"),
                (H:600d,B:300d,T:100d,Cy:"b",Cz:"c",Sy:"a",Sz:"a"),
                (H:360d,B:300d,T:40d,Cy:"b",Cz:"c",Sy:"a",Sz:"a"),
                (H:360.001,B:300d,T:40d,Cy:"a",Cz:"b",Sy:"a0",Sz:"a0"),
                (H:360d,B:300d,T:100d,Cy:"b",Cz:"c",Sy:"a",Sz:"a"),
                (H:360d,B:300d,T:100.001,Cy:"d",Cz:"d",Sy:"c",Sz:"c")
            })
            foreach (int axis in new[]{1,2})
                yield return new object[] {fy,row.H,row.B,row.T,axis,
                    fy==460 ? (axis==1?row.Sy:row.Sz) : (axis==1?row.Cy:row.Cz)};
        }
    }
    [DataTestMethod,DynamicData(nameof(TableCases))]
    public void RolledTableRowsAndInclusiveBoundaries(double fy,double h,double b,double tf,int axis,string curve)
    {
        var s=Section(h,b,tf,10,fy);
        Assert.AreEqual(curve,new Probe(s,5000,5000).Curve(s,axis));
    }

    [DataTestMethod]
    [DataRow(600d,300d,20d,50d,1,"a")]
    [DataRow(600d,300d,20d,50d,2,"b")]
    [DataRow(360d,300d,50d,110d,1,"b")]
    [DataRow(360d,300d,50d,110d,2,"c")]
    public void WebThicknessDoesNotSelectTheFlangeThicknessRow(
        double h,double b,double tf,double tw,int axis,string curve)
    {
        var s=Section(h,b,tf,tw,355);
        Assert.AreEqual(curve,new Probe(s,5000,5000).Curve(s,axis));
    }

    [DataTestMethod]
    [DataRow(235d,1,"c")] [DataRow(235d,2,"b")]
    [DataRow(355d,1,"c")] [DataRow(355d,2,"b")]
    [DataRow(460d,1,"a")] [DataRow(460d,2,"a")]
    public void VeryWideSectionMapsGeometricAxesToPrincipalAxes(double fy,int axis,string curve)
    {
        var s=Section(200,600,20,10,fy);
        Assert.IsTrue(s.Jyy>s.Jxx,"Fixture must have vertical principal axis 1");
        Assert.AreEqual(curve,new Probe(s,5000,5000).Curve(s,axis));
    }

    [DataTestMethod]
    [DataRow(355d,1)] [DataRow(355d,2)] [DataRow(460d,1)] [DataRow(460d,2)]
    public void RolledGeometryOutsideTableIsNotSilentlyAssignedACurve(double fy,int axis)
    {
        var s=Section(700,300,101,10,fy);
        Assert.ThrowsException<NotSupportedException>(()=>new Probe(s,5000,5000).Curve(s,axis));
    }

    internal static SteelSection Section(double h,double b,double tf,double tw,double fy) =>
        new(new SectionH(h,tw,b,tf,b,tf,"Table 6.2"),
            new SteelMaterial("Table nominal fy",210000,fy,Math.Max(550,fy+50)),
            GPC.Model.Sections.Section.SectionTypes.Rolled,
            GPC.Model.Sections.Section.FormedTypes.HotFinished);
}
