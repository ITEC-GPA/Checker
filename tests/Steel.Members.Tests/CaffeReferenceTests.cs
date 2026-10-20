using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

// Caffe, Acciaio, first edition 2012 / reprint April 2013.
// Numerical facts visually checked in the user-supplied scan.
// USER_REFERENCE_BENCHMARKS.txt records printed/PDF pages and source contradictions.
[TestClass]
public class CaffeReferenceTests
{
    internal static ReferenceSection Ipe300() => new(new SectionH(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15))
    {
        Area = 5380, J11 = 8.356e7, J22 = 6.038e6, Jt = 2.012e5, Jw = 1.259e11,
        Wpl1 = 628400, Wpl2 = 125200, Wel1 = 557100, Wel2 = 80500
    };
    internal static ReferenceSection Hea600() => new(new SectionH(590, 13, 300, 25, 300, 25, "HEA600", 27))
    {
        Area = 22650, J11 = 1.412e9, J22 = 1.127e8, Jt = 3.978e6, Jw = 8.978e12,
        Wpl1 = 5350000, Wpl2 = 1156000, Wel1 = 4787000, Wel2 = 751400
    };
    internal static ReferenceSection Ipe500() => new(new SectionH(500, 10.2, 200, 16, 200, 16, "IPE500", 21))
    {
        // Use the section-resistance table p236, NOT the conflicting A=11550 on p241.
        Area = 11600, J11 = 4.820e8, J22 = 2.142e7, Jt = 8.929e5, Jw = 1.249e12,
        Wpl1 = 2194000, Wpl2 = 335900, Wel1 = 1930000, Wel2 = 214200
    };
    private static ReferenceSection Upn300() => new(new SectionC(300, 10, 100, 16, 100, 16, "UPN input", 16))
    {
        Area = 5880, J11 = 80.3e6, J22 = 4.95e6, Jt = 374000, Jw = 69.1e9,
        PublishedRadius1 = 117, PublishedRadius2 = 29, Offset = new Point2d(-54.1, 0)
    };

    [DataTestMethod]
    [DataRow("IPE300", 6000d, 1.127, .454, 150d, 78.62e6, 5000d)]
    [DataRow("HEA600 segment 1", 4000d, 1.879, 0d, 0d, 8747e6, 500000d)]
    [DataRow("HEA600 segment 2", 4000d, 1.141, 0d, 0d, 5311e6, 500000d)]
    [DataRow("IPE500 constant moment", 4000d, 1d, 0d, 0d, 805.6e6, 50000d)]
    [DataRow("IPE500 end moments and UDL", 4000d, 2.578, 1.554, 250d, 690.3e6, 50000d)]
    public void PublishedCriticalMoment(string id, double length, double c1, double c2,
        double zg, double published, double lastDigit)
    {
        var s = id.StartsWith("HEA") ? Hea600() : id.StartsWith("IPE500") ? Ipe500() : Ipe300();
        double expected = Mcr(length, s.J22, s.Jt, s.Jw, c1, c2, zg);
        double sourceG = Mcr(length, s.J22, s.Jt, s.Jw, c1, c2, zg, 80770);
        // p242 substitutes Ncr=2774.7 kN after rounding, unlike the exact eigenvalue.
        double intermediateBound = 0;
        if (id == "IPE500 end moments and UDL")
        {
            double sourceN = 2774.7e3;
            double sourceValue = c1 * sourceN * (Math.Sqrt(s.Jw / s.J22 +
                80770 * s.Jt / sourceN + Math.Pow(c2 * zg, 2)) - c2 * zg);
            intermediateBound = Math.Abs(sourceValue - sourceG);
        }
        Assert.AreEqual(published, sourceG, lastDigit + intermediateBound, id + " printed arithmetic");
        double actual = new ReferenceProbe(s, length).Mcr(length, c1, c2, zg != 0);
        Close(expected, actual, id + " independent eigenvalue");
        Assert.AreEqual(published, actual, lastDigit + intermediateBound + Math.Abs(sourceG - expected));
    }

    [TestMethod]
    public void Ipe300AutomaticLoadHeightAndModifiedResistance()
    {
        var s = Ipe300();
        var p = new ReferenceProbe(s);
        double mcr = Mcr(6000, s.J22, s.Jt, s.Jw, 1.127, .454, 150);
        Close(mcr, p.AutomaticUdlMcr(6000));
        double lambda = Math.Sqrt(628400 * 275 / mcr);
        double chi = Reduction(lambda, .34, .75, .4);
        double f = Math.Min(1, 1 - .5 * (1 - .94) * (1 - 2 * Math.Pow(lambda - .8, 2)));
        double modified = Math.Min(Math.Min(1, 1 / (lambda * lambda)), chi / f);
        Close(modified * 628400 * 275 / 1.05, p.AutomaticUdlMb());
        // p223's 71.43 kNm uses printed chi=.434, despite declaring chi_mod=.435.
        // Preserve the discrepancy as source evidence, not a passing capacity oracle.
        Assert.AreEqual(71.43e6, .434 * 628400 * 275 / 1.05, 5000);
        Assert.IsTrue(Math.Abs(p.AutomaticUdlMb() - 71.43e6) > 5000);
    }

    [DataTestMethod]
    [DataRow("IPE300 p223", 1.483, .34, .94, 1.509, .434, .998, .435)]
    [DataRow("HEA600 p227", .526, .34, .923, .625, .949, .967, .981)]
    public void PublishedModifiedReduction(string id, double lambda, double alpha, double kc,
        double phi, double chi, double f, double modified)
    {
        var a = new ReferenceProbe(Ipe300()).Reduction(lambda, alpha, .75, .4, kc);
        Assert.AreEqual(phi, a.Phi, .0005, id);
        // Three-digit Phi in the source propagates to chi; no blanket % tolerance.
        double low = Raw(phi + .0005, lambda), high = Raw(phi - .0005, lambda);
        Assert.IsTrue(a.Chi >= Math.Min(chi - .0005, low) && a.Chi <= Math.Max(chi + .0005, high), id);
        Assert.AreEqual(f, a.F, .0005, id);
        Assert.IsTrue(a.Modified >= Math.Min(modified - .0005, low / (f + .0005)) &&
            a.Modified <= Math.Max(modified + .0005, high / (f - .0005)), id);
        Close(Reduction(lambda, alpha, .75, .4), a.Chi, id);
    }
    private static double Raw(double phi, double lambda) =>
        Math.Min(1 / (lambda * lambda), 1 / (phi + Math.Sqrt(phi * phi - .75 * lambda * lambda)));

    [DataTestMethod]
    [DataRow("UPN300 y", 6000d, 80.3e6, 4623.00e3, 5d)]
    [DataRow("UPN300 z", 2000d, 4.95e6, 2564.86e3, 5d)]
    [DataRow("IPE500 y", 4000d, 4.820e8, 62437.6e3, 50d)]
    [DataRow("IPE500 z", 4000d, 2.142e7, 2774.7e3, 50d)]
    public void PublishedEulerForces(string id, double length, double inertia, double force, double digit)
    {
        double expected = Math.PI * Math.PI * 210000 * inertia / (length * length);
        double actual = new ReferenceProbe(Upn300()).Euler(length, inertia);
        Close(expected, actual, id);
        // UPN300 Iy is printed as 8030 cm4: include half its last tabulated digit.
        double inputBound = id == "UPN300 y" ? Math.PI * Math.PI * 210000 * 5000 / (length * length) : 0;
        Assert.AreEqual(force, actual, digit + inputBound, id);
    }

    [TestMethod]
    public void Upn300TorsionalAndGoverningFlexuralTorsionalMode()
    {
        var s = Upn300();
        var p = new ReferenceProbe(s, 6000, weakLength: 2000, torsionalLength: 2000);
        double i0 = 117 * 117 + 29 * 29 + 54.1 * 54.1;
        double expected = (210000 / 2.6 * 374000 + Math.PI * Math.PI * 210000 * 69.1e9 / 4e6) / i0;
        Close(expected, p.Torsion());
        Assert.AreEqual(3781e3, p.Torsion(), 500 + Math.Abs(80770 - 210000 / 2.6) * 374000 / i0);
        double ny = Math.PI * Math.PI * 210000 * 80.3e6 / 36e6;
        double nz = Math.PI * Math.PI * 210000 * 4.95e6 / 4e6;
        // Root of (Ny-N)(NT-N)-e^2/i0^2*N^2=0, solved by bisection.
        double lo = 0, hi = Math.Min(ny, expected);
        for (int i = 0; i < 100; ++i)
        {
            double n = (lo + hi) / 2;
            if ((ny - n) * (expected - n) - 54.1 * 54.1 / i0 * n * n > 0) lo = n;
            else hi = n;
        }
        double coupled = (lo + hi) / 2;
        Assert.AreEqual(2931e3, coupled, 1000); // printed rounding of NT propagated
        Close(nz, p.FlexuralTorsion(ny, nz, expected), "uncoupled weak axis governs");
        // Component isolation: suppress only the uncoupled mode. NOT a second published member.
        Close(coupled, p.FlexuralTorsion(ny, 1e20, expected), "coupled root");
        Assert.AreEqual("c", p.Curve(2));
        double lambda = Math.Sqrt(5880 * 275 / nz);
        Close(Reduction(lambda, .49) * 5880 * 275 / 1.05, p.Nb(2));
        // The displayed .665 gives 1024.1 kN, not the printed 1025 kN.
        // Keep this editorial inconsistency visible; do not force the final resistance.
        Assert.AreEqual(1024100d, .665 * 5880 * 275 / 1.05, 1e-7);
        Assert.IsTrue(Math.Abs(1025e3 - .665 * 5880 * 275 / 1.05) > 500);
        Assert.AreEqual(.665, p.Nb(2) * 1.05 / (5880 * 275), .0015);
    }

    [DataTestMethod]
    [DataRow("N", 3038e3, 500d)]
    [DataRow("M1", 574.6e6, 50000d)]
    [DataRow("M2", 87.97e6, 5000d)]
    public void Ipe500PublishedSectionResistances(string action, double expected, double digit)
    {
        var p = new ReferenceProbe(Ipe500());
        double value = action == "N" ? p.Axial() : p.Moment(action == "M1" ? 1 : 2);
        Assert.AreEqual(expected, value, digit, "A5.5 p238");
    }

    [DataTestMethod]
    [DataRow(1, 6826d, 1032e3, 500d)]
    [DataRow(2, 5987d, 905.3e3, 50d)]
    public void Ipe500PublishedShearResistance(int axis, double av, double capacity, double digit)
    {
        // Source tabulated Av is tested separately from the Model/EC3 area formula.
        double actual = GPC.Checkers.Steel.Members.Ntc2018SectionResistance.Shear(av, 275);
        Assert.AreEqual(capacity, actual, digit, "A5.5 p238; shared resistance expression, supplied Av");
    }

    [DataTestMethod]
    [DataRow(0d, 1e6)] [DataRow(-1000d, 0d)] [DataRow(-1000d, 1e6)]
    public void Heb280PublishedClassification(double n, double m)
    {
        var s = new SteelSection(new SectionH(280, 10.5, 280, 18, 280, 18, "HEB280", 24),
            new SteelMaterial("S275", 210000, 275, 430));
        Assert.AreEqual(1, new ReferenceProbe(s).Class(n, m), "A3.1 pp114-115");
    }

    [DataTestMethod]
    [DataRow("Area", 9056d, .00001)]
    [DataRow("CentroidX", 27.99, .005)]
    [DataRow("I1", 20084e4, 5000d)]
    [DataRow("I2", 921e4, 5000d)]
    [DataRow("Wel1", 1004e3, 500d)]
    [DataRow("Wel2", 112.3e3, 50d)]
    [DataRow("Wpl1", 1220e3, 500d)]
    [DataRow("Wpl2", 202.2e3, 50d)]
    public void WeldedChannelPublishedGeometry(string quantity, double expected, double digit)
    {
        var s = new SectionC(400, 14, 110, 18, 110, 18, "Caffe A1.1");
        double actual = quantity switch
        {
            "Area" => s.Area, "CentroidX" => s.Centroid.X, "I1" => s.J11, "I2" => s.J22,
            "Wel1" => s.Wel1, "Wel2" => s.Wel2, "Wpl1" => s.Wpl1, "Wpl2" => s.Wpl2,
            _ => throw new ArgumentException(quantity)
        };
        Assert.AreEqual(expected, actual, digit, "A1.1 pp49-51 " + quantity);
    }

    [DataTestMethod]
    [DataRow(1, 3960d)] [DataRow(2, 5096d)]
    public void WeldedChannelPublishedShearAreas(int axis, double area)
    {
        var s = new SteelSection(new SectionC(400, 14, 110, 18, 110, 18, "Caffe A1.1"),
            new SteelMaterial("S275", 210000, 275, 430), Section.SectionTypes.Welded);
        Close(area * 275 / (Math.Sqrt(3) * 1.05), new ReferenceProbe(s).Shear(axis), "A1.1 p49");
    }
}
