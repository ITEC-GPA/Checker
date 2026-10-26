using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubBothFlangesTests
{
    // m=35,e=50; clear depth300; top/bottom physical boundary, not a free edge.
    private static TStubInternalEndPlateGeometry Geometry(double[]? rows=null,double lowerAlpha=6.5)
        => new TStubInternalEndPlateGeometry(200,220,100,10,12.5,12,12,300,rows??new[]{50d,150,250},
            new(7.5,"Qualified upper chart reading"),"Both flange analytical geometry").WithOppositeFlange(new(lowerAlpha,"Qualified lower chart reading"));

    [TestMethod]
    public void IndividualRowsAndEachAdjacentGroupUseIndependentSciTableArithmetic()
    {
        var r=TStubInternalEndPlateLengths.Calculate(Geometry());
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));
        CollectionAssert.AreEqual(new[]{262.5,202.5,227.5},r.IndividualRows.Select(x=>x.Mode2).ToArray());
        var expected=new[]{362.5,327.5,487.5};
        CollectionAssert.AreEqual(expected,r.AdjacentGroups.Select(x=>x.EffectiveLengths.Mode2).ToArray());
        Assert.AreEqual(35*Math.PI+100,r.RowGroupContributions.First().CircularLength,1e-10);
        // Upper:262.5-101.25+50; middle:100; lower:227.5-101.25+50.
        var whole=r.RowGroupContributions.Where(x=>x.RowCount==3).ToArray();
        CollectionAssert.AreEqual(new[]{211.25,100,176.25},whole.Select(x=>x.NonCircularLength).ToArray());
        Assert.AreEqual(3,r.AdjacentGroups.Count);
    }

    [TestMethod]
    public void RowAndGroupLengthsReverseWithPhysicalFlangesAndTheirOwnAlpha()
    {
        var p=Geometry(new[]{50d,165,250});
        var reversed=new TStubInternalEndPlateGeometry(200,220,100,10,12.5,12,12,300,new[]{50d,135,250},
            p.LastRowAlpha,"Reversed same physical detail").WithOppositeFlange(p.FirstRowAlpha);
        var a=TStubInternalEndPlateLengths.Calculate(p);var b=TStubInternalEndPlateLengths.Calculate(reversed);
        Assert.IsTrue(a.IsSupported&&b.IsSupported);
        CollectionAssert.AreEqual(a.IndividualRows.Select(x=>x.Mode2).Reverse().ToArray(),b.IndividualRows.Select(x=>x.Mode2).ToArray());
        foreach(var group in a.AdjacentGroups)
        {
            var other=b.AdjacentGroups.Single(x=>x.FirstRow==3-group.FirstRow-group.RowCount&&x.RowCount==group.RowCount);
            Assert.AreEqual(group.EffectiveLengths.Mode1,other.EffectiveLengths.Mode1,1e-9);
            Assert.AreEqual(group.EffectiveLengths.Mode2,other.EffectiveLengths.Mode2,1e-9);
        }
    }

    [TestMethod]
    public void SingleRowBetweenTwoFlangesUsesTheDistinctSciPatternWithoutInventedGroups()
    {
        var p=new TStubInternalEndPlateGeometry(200,220,100,10,12.5,12,12,150,new[]{75d},
            new(7.5,"Upper"),"SCI2.2d input").WithOppositeFlange(new(6.5,"Lower"));
        var r=TStubInternalEndPlateLengths.Calculate(p);
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));
        Assert.AreEqual(287.5,r.IndividualRows.Single().Mode2);Assert.AreEqual(2*Math.PI*35,r.IndividualRows.Single().Mode1,1e-9);
        Assert.AreEqual(0,r.AdjacentGroups.Count);
    }

    [TestMethod]
    public void ExplicitNewBoundaryDoesNotRemoveTheOldMissingDataRejection()
    {
        var p=Geometry();
        var legacy=new TStubInternalEndPlateGeometry(p.PlateWidth,p.OpposingFlangeWidth,p.BoltGauge,p.BeamWebThickness,p.WebWeldLeg,
            p.TensionFlangeWeldLeg,p.CompressionFlangeWeldLeg,p.BeamClearDepth,p.OrderedInternalRows,p.FirstRowAlpha,p.DetailSource);
        Assert.IsNull(legacy.LastRowAlpha);
        Assert.IsTrue(TStubInternalEndPlateLengths.Calculate(legacy).Diagnostics.Contains("CompressionFlangeTooCloseForUnrestrictedEndRowMechanism"));
    }

    [TestMethod]
    public void UnqualifiedOppositeAlphaAndMultipleRowsInOneBoundaryZoneRemainUnsupported()
    {
        Assert.IsFalse(TStubInternalEndPlateLengths.Calculate(Geometry(lowerAlpha:5)).IsSupported);
        Assert.IsFalse(TStubInternalEndPlateLengths.Calculate(Geometry(new[]{50d,90,250})).IsSupported);
        var p=Geometry().WithOppositeFlange(new(6.5," "));
        Assert.IsTrue(TStubInternalEndPlateLengths.Calculate(p).Diagnostics.Contains("OppositeFlangeAlphaDerivationRequired"));
    }

    [TestMethod]
    public void SciApproximationMatchesExistingIndependentC1ValueWithoutOverwritingChartReading()
    {
        var alpha=TStubSciAlpha.Calculate(38.55,75,34.8,"SCI C1 physical dimensions");
        Assert.AreEqual(7.343053807634066,alpha.Value,1e-11);
        Assert.AreEqual(7.5,new TStubChartAlpha(7.5,"C1 graphical reading").Value);
    }

    [DataTestMethod][DataRow(5d,1.4)][DataRow(6d,1.3)][DataRow(7.5,1.2)]
    public void SciStraightBranchHasAnIndependentClosedFormInverse(double alpha,double lambda2)
    {
        double lambda1=1.25/(alpha-2.75);
        var r=TStubSciAlpha.Calculate(lambda1,1-lambda1,lambda2,"Straight branch");
        Assert.AreEqual(alpha,r.Value,1e-10);
    }

    [TestMethod]
    public void SciApproximationNeverClipsOutsideItsDomain()
    {
        Assert.ThrowsException<NotSupportedException>(()=>TStubSciAlpha.Calculate(1,100,1,"outside"));
        Assert.ThrowsException<NotSupportedException>(()=>TStubSciAlpha.Calculate(35,50,200,"outside"));
        Assert.ThrowsException<ArgumentException>(()=>TStubSciAlpha.Calculate(35,50,50," "));
    }
}
