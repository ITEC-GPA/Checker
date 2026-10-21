using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ClassifiedSectionTreatmentTests
{
    [TestMethod]
    public void JoiningClassificationAndTreatmentPreservesMachineNeighborsOfClass3Limit()
    {
        foreach (double fy in new[] { 235d, 275, 355, 420, 460 })
        {
            double limit = Ec3PlateClassification.Internal(100, 1, fy, .5, -1).Nominal.Class3Limit!.Value;
            foreach (double ratio in new[] { Math.BitDecrement(limit), limit, Math.BitIncrement(limit) })
            {
                var classified = Ec3PlateClassification.Internal(ratio, 1, fy, .5, -1).Nominal;
                var treatment = Ec3ClassifiedSectionTreatment.Evaluate(1, new ClassifiedCompressionPart("web", classified, fy));
                Assert.AreEqual(classified.NominalClass, treatment.NominalClass);
                Assert.AreEqual(classified.NominalClass, treatment.SectionResistanceClass);
            }
        }
    }

    [TestMethod]
    public void PlasticClassIsNotRejectedByTheSeparateElasticClass3Limit()
    {
        // Table5.2, free tip under pure bending: limits1/2=18/20,
        // limit3=21*sqrt(.85)=19.361... . A c/t19.5 part is class2.
        var part = Ec3PlateClassification.Outstand(19.5, 1, 235, .5, true, -1, 1).Nominal;
        Assert.AreEqual(2, part.NominalClass);
        Assert.IsTrue(part.WidthThicknessRatio > part.Class3Limit);
        var result = Ec3ClassifiedSectionTreatment.Evaluate(1, new ClassifiedCompressionPart("free", part, 100));
        Assert.AreEqual(2, result.SectionResistanceClass); Assert.AreEqual(2, result.MemberBucklingClass);
        Assert.IsFalse(result.UsesClause5529);
    }

    [TestMethod]
    public void LowStressSectionTreatmentNeverRelaxesMemberBucklingClass()
    {
        // fy235,gamma1, c/t50, uniform class3 limit42. At sigma=235/4,
        // the increased limit is84: nominal4, section3, member4.
        var web = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 500, 10, 235);
        var result = Ec3ClassifiedSectionTreatment.Evaluate(1, new ClassifiedCompressionPart("web", web, 58.75));
        Assert.AreEqual(4, result.NominalClass); Assert.AreEqual(3, result.SectionResistanceClass);
        Assert.AreEqual(4, result.MemberBucklingClass); Assert.IsTrue(result.UsesClause5529);
        Assert.AreEqual(84d, result.Parts[0].IncreasedClass3Limit, 1e-12);
    }

    [TestMethod]
    public void TreatmentUsesGradientLimitAndEveryCompressedPart()
    {
        var web = Ec3PlateClassification.Internal(1500, 10, 235, .5, -1).Nominal;
        var flange = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.OutstandFlat, 160, 10, 235);
        var result = Ec3ClassifiedSectionTreatment.Evaluate(1, new ClassifiedCompressionPart("web", web, 58.75),
            new ClassifiedCompressionPart("flange", flange, 235));
        Assert.AreEqual(248d, result.Parts[0].IncreasedClass3Limit, 1e-12);
        Assert.AreEqual(4, result.SectionResistanceClass); Assert.IsFalse(result.UsesClause5529);
    }

    [TestMethod]
    public void GammaM0EntersStressExemptionButNotNominalClass()
    {
        var part = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 600, 10, 235);
        var p = new ClassifiedCompressionPart("web", part, 100);
        Assert.AreEqual(3, Ec3ClassifiedSectionTreatment.Evaluate(1, p).SectionResistanceClass);
        Assert.AreEqual(4, Ec3ClassifiedSectionTreatment.Evaluate(1.25, p).SectionResistanceClass);
        Assert.AreEqual(4, p.Classification.NominalClass);
    }

    [TestMethod]
    public void AmbiguousOrUnsupportedPartSetsAreRejected()
    {
        var p = new ClassifiedCompressionPart("p", Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 300, 10, 235), 50);
        var other = new ClassifiedCompressionPart("other", Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 300, 10, 355), 50);
        Assert.ThrowsException<ArgumentException>(() => Ec3ClassifiedSectionTreatment.Evaluate(1));
        Assert.ThrowsException<ArgumentException>(() => Ec3ClassifiedSectionTreatment.Evaluate(1, p, p));
        Assert.ThrowsException<NotSupportedException>(() => Ec3ClassifiedSectionTreatment.Evaluate(1, p, other));
        Assert.ThrowsException<NotSupportedException>(() => new ClassifiedCompressionPart("tube", Ec3CompressionPartClassification.Evaluate(CompressionPartKind.CircularHollow, 100, 1, 235), 50));
        Assert.ThrowsException<ArgumentException>(() => new ClassifiedCompressionPart("bad", Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, null, 1, 235), 50));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ClassifiedCompressionPart("bad", p.Classification, double.NaN));
    }
}
