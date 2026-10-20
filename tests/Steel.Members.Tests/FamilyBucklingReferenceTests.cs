using System.Text.Json;
using GPC.Checkers.Steel.Members;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

// Normative/analytical references, NOT published numerical worked examples.
// EC3:2005 6.3.1.1-3, tables6.1/6.2; NTC2018 4.2.4.1.3.1, table4.2.VIII.
// Independent geometry fixture already has its rectangle/clipping generator.
[TestClass]
public class FamilyBucklingReferenceTests
{
    public static IEnumerable<object[]> Cases =>
        from family in new[] { "H", "C", "T", "L", "RHS", "CHS", "Rectangular", "Circular" }
        from axis in new[] { 1, 2 }
        from code in family is "Rectangular" or "Circular" ? new[] { "NTC2018" } : new[] { "EC3", "NTC2018" }
        select new object[] { family, axis, code };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void EachSupportedFamilyAndAxisAgainstIndependentEulerAndReduction(string family, int axis, string code)
    {
        var data = JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "section-oracles.json")))!;
        var o = data.Single(x => x.Name == family);
        double area = o.Area, i1 = o.I1, i2 = o.I2;
        // Compact alternatives avoid treating the old slender T fixture as class<=3 in N.
        // T: rectangles 100x10 at y=90..100 and 10x90 at x=45..55.
        // L: rectangles 100x20 at y=0..20 and 20x80 at x=0..20,y=20..100.
        // Direct area moments + centroid shift + eigenvalues of the inertia tensor.
        if (family == "T") { area = 1900; i1 = 1800043.859649123; i2 = 840833.3333333333; }
        if (family == "L") { area = 3600; i1 = 4920000; i2 = 1364444.4444444452; }
        Section shape = family switch
        {
            "H" => new SectionH(300, 10, 200, 20, 200, 20, "analytic"),
            "C" => new SectionC(300, 10, 100, 15, 100, 15, "analytic"),
            "T" => new SectionT(100, 100, 10, 10, "compact analytic"),
            "L" => new SectionL(100, 20, 100, 20, "compact analytic"),
            "RHS" => new SectionRHS(300, 200, 10, 10, 10, 10, "analytic"),
            "CHS" => new SectionCHS(200, 10, "analytic"),
            "Rectangular" => new SectionRectangular(200, 100, "analytic"),
            "Circular" => new SectionCircular(100, "analytic"),
            _ => throw new ArgumentException(family)
        };
        var section = new SteelSection(shape, new SteelMaterial("S235", 210000, 235, 360),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
        Close(area, section.Area);
        Close(axis == 1 ? i1 : i2, axis == 1 ? section.J11 : section.J22);
        string curve = family switch
        {
            "H" => axis == 1 ? "a" : "b",
            "C" or "T" or "Rectangular" or "Circular" => "c",
            "L" => "b", "RHS" or "CHS" => "a", _ => throw new ArgumentException(family)
        };
        double alpha = curve == "a" ? .21 : curve == "b" ? .34 : .49;
        double inertia = axis == 1 ? i1 : i2;
        // Different restraint lengths ensure the two axes are not accidentally aliased.
        double length = axis == 1 ? 6000 : 3500;
        double ncr = Math.PI * Math.PI * 210000 * inertia / (length * length);
        double lambda = Math.Sqrt(area * 235 / ncr);
        double chi = Reduction(lambda, alpha);
        if (code == "EC3")
        {
            var probe = new ReferenceProbe(section, 6000, 1, weakLength: 3500);
            Assert.AreEqual(curve, probe.Curve(axis), "automatic family/axis selector");
            Close(chi * area * 235, probe.Nb(axis, 3));
        }
        else
        {
            // Explicit alpha and conservative class3: primitive scope, no automatic
            // classification or torsional/TF safety claim for these members.
            var result = Ntc2018SectionResistance.FlexuralBuckling(
                section.Area, axis == 1 ? section.J11 : section.J22, length, 210000, 235, alpha, 3);
            Close(ncr, result.CriticalForce);
            Close(lambda, result.NormalizedSlenderness);
            Close(chi, result.Reduction);
            Close(chi * area * 235 / 1.05, result.Resistance);
        }
    }
}
