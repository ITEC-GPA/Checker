using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Data.Steel;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ShearAreaExtractionTests
{
    public static IEnumerable<object[]> Cases =>
        from family in new[] { "H", "C", "T", "RHS", "CHS" }
        from welded in new[] { false, true }
        from standard in new[] { SectionResistanceStandard.Eurocode3_2005, SectionResistanceStandard.Ntc2018 }
        select new object[] { family, welded, standard };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void ExtractedAreasMatchIndependentRectangleAndRingCalculations(string family, bool welded, SectionResistanceStandard standard)
    {
        Section shape = family switch {
            "H" => new SectionH(300, 10, 200, 20, 200, 20, "H"),
            "C" => new SectionC(300, 10, 100, 15, 100, 15, "C"),
            "T" => new SectionT(200, 200, 10, 15, "T"),
            "RHS" => new SectionRHS(300, 200, 10, 10, 10, 10, "RHS"),
            _ => new SectionCHS(200, 10, "CHS")
        };
        var section = new SteelSection(shape, SteelMaterialEN1993Data.S235,
            welded ? Section.SectionTypes.Welded : Section.SectionTypes.Rolled);
        // EC3 6.2.6(3), eta=1 as in the facade; NTC2018 4.2.4.1.2.4.
        // H Aw2600,Af8000; C Aw2700,Af3000; T Aw1850,Af3000;
        // RHS A9600; CHS A1900*pi. No production property is used as expected input.
        double expected1 = family switch { "H" => 8000, "C" or "T" => 3000,
            "RHS" => welded ? 4000 : 3840, _ => 3800 };
        double expected2 = family switch { "H" => welded ? 2600 : 2800,
            "C" => welded ? 2700 : 2850, "T" => standard == SectionResistanceStandard.Ntc2018 ? 1665 : 1925,
            "RHS" => welded ? 5600 : 5760, _ => 3800 };
        Assert.AreEqual(expected1, ModelShearAreas.GetArea1(section), 1e-9);
        Assert.AreEqual(expected2, ModelShearAreas.GetArea2(section, standard), 1e-9);
        // Resistance factors belong to the resistance service, not the area mapper.
        var code = standard == SectionResistanceStandard.Ntc2018 ? SectionResistanceCode.Ntc2018() : SectionResistanceCode.Eurocode3(1, 1.25, "test");
        var result = SectionResistanceVerifier.Shear(code, 1000, expected2, 235);
        Assert.AreEqual(expected2 * 235 / (Math.Sqrt(3) * code.GammaM0), result.Resistance!.Value, 1e-8);
    }

    [TestMethod]
    public void RolledTeeRootAreaKeepsTheDistinctNationalPolicies()
    {
        var section = new SteelSection(new SectionT(200, 200, 10, 15, "T", 10), SteelMaterialEN1993Data.S235, Section.SectionTypes.Rolled);
        // Two r10 squares minus quarter-circles add the root area to Aw1850.
        double roots = 200 * (1 - Math.PI / 4);
        Assert.AreEqual(2075 + roots, ModelShearAreas.GetArea2(section, SectionResistanceStandard.Eurocode3_2005), 1e-9);
        Assert.AreEqual(.9 * (1850 + roots), ModelShearAreas.GetArea2(section, SectionResistanceStandard.Ntc2018), 1e-9);
    }

    [TestMethod]
    public void UnknownStandardAndMissingGeometryDoNotChooseDefaults()
    {
        var section = new SteelSection(new SectionT(200, 200, 10, 15, "T"), SteelMaterialEN1993Data.S235);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ModelShearAreas.GetArea2(section, (SectionResistanceStandard)99));
        Assert.ThrowsException<ArgumentNullException>(() => ModelShearAreas.GetArea1(null!));
        Assert.ThrowsException<ArgumentNullException>(() => ModelShearAreas.GetArea2(null!, SectionResistanceStandard.Ntc2018));
    }
}
