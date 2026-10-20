using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

// SCI P363, March 2015 reprint; UK NA, not an Italian national annex.
// SCI_SECTION_BENCHMARKS.txt gives pages, row identities, axes and limitations.
[TestClass]
public class SciSectionReferenceTests
{
    private static SteelSection Geometry(string family) => new(family switch
    {
        "C" => new SectionC(300, 9, 100, 16.5, 100, 16.5, "PFC300x100x46", 15),
        "T" => new SectionT(85.1, 157.4, 11, 15.7, "UKT152x76x26", 7.6),
        "L" => new SectionL(100, 10, 100, 10, "UKA100x100x10", 12, 6),
        "CHS" => new SectionCHS(60.3, 4, "CHS60.3x4"),
        "RHSRounded" => new SectionRHSRoundedCorners(50, 30, 4, 6, 4, "HF RHS50x30x4"),
        _ => throw new ArgumentException(family)
    }, new SteelMaterial("Source strength explicitly assigned", 210000, family == "C" ? 265 : 355, 510),
        Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);

    [DataTestMethod]
    [DataRow("C", "A", 5800d, 5d)]
    [DataRow("C", "I1", 8230e4, 50000d)]
    [DataRow("C", "I2", 568e4, 5000d)]
    [DataRow("C", "Wpl1", 641e3, 500d)]
    [DataRow("C", "Wpl2", 148e3, 500d)]
    [DataRow("C", "Wel1", 549e3, 500d)]
    [DataRow("C", "Wel2", 81.7e3, 50d)]
    // Wide T: Model principal axis 1 = SCI z-z, axis 2 = SCI y-y.
    [DataRow("T", "A", 3260d, 5d)]
    [DataRow("T", "I1", 511e4, 5000d)]
    [DataRow("T", "I2", 141e4, 5000d)]
    [DataRow("T", "Wpl1", 99.5e3, 50d)]
    [DataRow("T", "Wpl2", 41.4e3, 50d)]
    [DataRow("T", "Wel1", 64.9e3, 50d)]
    [DataRow("T", "Wel2", 21e3, 50d)]
    // Equal angle: Model 1=u-u, 2=v-v, not the parallel-to-leg axes.
    [DataRow("L", "A", 1920d, 5d)]
    [DataRow("L", "I1", 280e4, 5000d)]
    [DataRow("L", "I2", 73e4, 500d)]
    [DataRow("CHS", "A", 707d, .5)]
    [DataRow("CHS", "I1", 28.2e4, 500d)]
    [DataRow("CHS", "I2", 28.2e4, 500d)]
    [DataRow("CHS", "Wpl1", 12.7e3, 50d)]
    [DataRow("CHS", "Wel1", 9.34e3, 5d)]
    [DataRow("CHS", "Jt", 56.3e4, 500d)]
    [DataRow("RHSRounded", "A", 559d, .5)]
    [DataRow("RHSRounded", "I1", 16.5e4, 500d)]
    [DataRow("RHSRounded", "I2", 7.08e4, 50d)]
    [DataRow("RHSRounded", "Wpl1", 8.59e3, 5d)]
    [DataRow("RHSRounded", "Wpl2", 5.88e3, 5d)]
    [DataRow("RHSRounded", "Wel1", 6.60e3, 5d)]
    [DataRow("RHSRounded", "Wel2", 4.72e3, 5d)]
    public void ModelGeometryAgainstPublishedSectionTable(string family, string property, double value, double digit)
    {
        var s = Geometry(family);
        double actual = property switch
        {
            "A" => s.Area, "I1" => s.J11, "I2" => s.J22, "Jt" => s.Jt,
            "Wpl1" => s.Wpl1, "Wpl2" => s.Wpl2, "Wel1" => s.Wel1, "Wel2" => s.Wel2,
            _ => throw new ArgumentException(property)
        };
        Assert.AreEqual(value, actual, digit, family + " " + property);
    }

    public static IEnumerable<object[]> ChsBucklingCases
    {
        get
        {
            double[] lengths = { 1, 1.5, 2, 2.5, 3, 3.5, 4, 5, 6, 7, 8, 9, 10 };
            double[] nb = { 218, 170, 117, 80.4, 57.9, 43.4, 33.7, 22, 15.4, 11.4, 8.80, 6.99, 5.68 };
            for (int i = 0; i < lengths.Length; ++i)
                foreach (int axis in new[] { 1, 2 })
                    yield return new object[] { lengths[i] * 1000, nb[i] * 1000,
                        i <= 2 ? 500d : i <= 9 ? 50d : 5d, axis };
        }
    }
    [DataTestMethod, DynamicData(nameof(ChsBucklingCases))]
    public void ChsPublishedBucklingAtThirteenLengths(double length, double published, double digit, int axis)
    {
        var s = Geometry("CHS");
        var p = new ReferenceProbe(s, length, 1);
        Assert.AreEqual("a", p.Curve(axis), "hot finished S355, EC3 table6.2");
        // Exact circular-annulus input, independent of table rounding and Model.
        double a = Math.PI / 4 * (60.3 * 60.3 - 52.3 * 52.3);
        double inertia = Math.PI / 64 * (Math.Pow(60.3, 4) - Math.Pow(52.3, 4));
        double lambda = length / Math.PI * Math.Sqrt(a * 355 / (210000 * inertia));
        double exact = Reduction(lambda, .21) * a * 355;
        Close(exact, p.Nb(axis));
        // SCI notes6.2 use L/(93.9*epsilon*i). B-14 prints A=7.07cm2,i=2.00cm.
        // Their rounded radius reproduces the table, whereas the exact annulus has
        // i=19.955231mm. Propagate that identified input difference, not a fitted %.
        double sourceLambda = length / (93.9 * Math.Sqrt(235d / 355) * 20);
        double sourceCalculation = Reduction(sourceLambda, .21) * 707 * 355;
        Assert.AreEqual(published, sourceCalculation, digit, "SCI D-14 rounded-input arithmetic");
        Assert.AreEqual(published, p.Nb(axis), digit + Math.Abs(sourceCalculation - exact), "SCI D-14");
    }

    public static IEnumerable<object[]> ChannelBucklingCases
    {
        get
        {
            double[] lengths = { 1, 1.5, 2, 2.5, 3, 3.5, 4, 5, 6, 7, 8, 9, 10 };
            double[] ny = { 2000, 2000, 1980, 1930, 1870, 1820, 1760, 1640, 1510, 1380, 1240, 1110, 986 };
            double[] nz = { 1780, 1550, 1290, 1050, 834, 668, 542, 373, 270, 204, 160, 128, 105 };
            for (int i = 0; i < lengths.Length; ++i)
                foreach (int axis in new[] { 1, 2 })
                    yield return new object[] { lengths[i] * 1000, (axis == 1 ? ny[i] : nz[i]) * 1000, axis };
        }
    }
    [DataTestMethod, DynamicData(nameof(ChannelBucklingCases))]
    public void ChannelPublishedFlexuralBucklingAtThirteenLengths(double length, double published, int axis)
    {
        // P363 B-43 input A=58.0cm2,i_y=11.9cm,i_z=3.13cm.
        // S355 with tf=16.5mm has source fy=345MPa. This does not qualify T/TF.
        var section = new ReferenceSection(new SectionC(300, 9, 100, 16.5, 100, 16.5, "PFC", 15), 345)
        { Area = 5800, J11 = 5800 * 119d * 119, J22 = 5800 * 31.3 * 31.3 };
        var p = new ReferenceProbe(section, length, 1);
        Assert.AreEqual("c", p.Curve(axis));
        double radius = axis == 1 ? 119 : 31.3;
        double lambda = length / (Math.PI * Math.Sqrt(210000d / 345) * radius);
        double expected = Reduction(lambda, .49) * 5800 * 345;
        Close(expected, p.Nb(axis));
        double sourceLambda = length / (93.9 * Math.Sqrt(235d / 345) * radius);
        double sourceValue = Reduction(sourceLambda, .49) * 5800 * 345;
        double digit = published >= 1e6 ? 5000 : 500; // 3 significant figures, notes3.1
        Assert.AreEqual(published, sourceValue, digit, "SCI D-53, rounded inputs");
        Assert.AreEqual(published, p.Nb(axis), digit + Math.Abs(sourceValue - expected), "SCI D-53");
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)]
    public void ChsPublishedMomentShearAndClassification(int axis)
    {
        var p = new ReferenceProbe(Geometry("CHS"), gamma: 1);
        Assert.AreEqual(1, p.Class(0, 1e6), "SCI D-103");
        Assert.AreEqual(4.51e6, p.Moment(axis), 5000, "SCI D-103");
        Assert.AreEqual(92.3e3, p.Shear(axis), 50, "SCI D-103");
        Assert.AreEqual(251e3, p.Axial(), 500, "SCI D-14 torsional plateau/section squash");
    }

    [DataTestMethod]
    [DataRow(1, 170e6, 500000d)]
    [DataRow(2, 39.2e6, 50000d)]
    public void ChannelPublishedSectionBending(int axis, double capacity, double digit)
    {
        // tf=16.5 mm -> source S275 uses fy=265 MPa, not 275.
        var p = new ReferenceProbe(Geometry("C"), gamma: 1);
        Assert.AreEqual(1, p.Class(0, 1e6), "SCI C-130");
        // The resistance table evaluates the rounded Wpl=641/148 cm3 (B-43).
        // The geometry test above independently bounds Model to those printed moduli.
        double wp = axis == 1 ? 641000 : 148000;
        Assert.AreEqual(capacity, wp * 265, digit, "SCI C-130 rounded-input arithmetic");
        Assert.AreEqual(capacity, p.Moment(axis), digit + 500 * 265, "SCI C-130");
    }

    [DataTestMethod]
    [DataRow(1, 3.05e6)] [DataRow(2, 2.09e6)]
    public void RoundedRhsPublishedSectionBendingWithAssignedClass(int axis, double capacity)
    {
        // Source D-112 gives class1. The generic RHSRounded bulk path remains unsupported.
        Assert.AreEqual(capacity, new ReferenceProbe(Geometry("RHSRounded"), gamma: 1).Moment(axis), 5000);
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)]
    public void WideTeeElasticAndPlasticResistanceUseCorrectPrincipalAxis(int axis)
    {
        var p = new ReferenceProbe(Geometry("T"), gamma: 1);
        double wp = axis == 1 ? 99500 : 41400, we = axis == 1 ? 64900 : 21000;
        // EC3 6.2.5 applied to independently published moduli, NOT a published Mc.
        Assert.AreEqual(wp * 355, p.Moment(axis), 50 * 355, "SCI B-57 -> EC3 6.2.5 plastic");
        Assert.AreEqual(we * 355, p.Moment(axis, 3), 50 * 355, "SCI B-57 -> EC3 6.2.5 elastic");
    }
}
