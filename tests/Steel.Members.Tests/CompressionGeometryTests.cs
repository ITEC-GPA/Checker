using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Data.Steel;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class CompressionGeometryTests
{
    private static Section Shape(string family) => family switch {
        "H" => new SectionH(400, 10, 200, 15, 160, 20, "H", 8),
        "T" => new SectionT(200, 160, 10, 15, "T", 6),
        "C" => new SectionC(300, 8, 100, 12, 80, 14, "C", 7),
        "RHS" => new SectionRHS(300, 200, 10, 12, 8, 9, "RHS"),
        _ => new SectionCHS(168.3, 6.3, "CHS")
    };

    [DataTestMethod]
    [DataRow("H", "TopFlange", 87d, 15d, CompressionPartKind.OutstandFlat)]
    [DataRow("H", "BottomFlange", 67d, 20d, CompressionPartKind.OutstandFlat)]
    [DataRow("H", "Web", 349d, 10d, CompressionPartKind.InternalFlat)]
    [DataRow("T", "Flange", 69d, 15d, CompressionPartKind.OutstandFlat)]
    [DataRow("T", "Stem", 185d, 10d, CompressionPartKind.OutstandFlat)]
    [DataRow("C", "TopFlange", 85d, 12d, CompressionPartKind.OutstandFlat)]
    [DataRow("C", "BottomFlange", 65d, 14d, CompressionPartKind.OutstandFlat)]
    [DataRow("C", "Web", 260d, 8d, CompressionPartKind.InternalFlat)]
    [DataRow("RHS", "TopWall", 183d, 10d, CompressionPartKind.InternalFlat)]
    [DataRow("RHS", "BottomWall", 183d, 12d, CompressionPartKind.InternalFlat)]
    [DataRow("RHS", "LeftWall", 278d, 8d, CompressionPartKind.InternalFlat)]
    [DataRow("RHS", "RightWall", 278d, 9d, CompressionPartKind.InternalFlat)]
    [DataRow("CHS", "Tube", 168.3, 6.3, CompressionPartKind.CircularHollow)]
    public void ClearWidthsAndThicknessesMatchIndependentDimensionSubtractions(string family, string part, double c, double t, CompressionPartKind kind)
    {
        var geometry = ModelCompressionGeometry.Create(new SteelSection(Shape(family), SteelMaterialEN1993Data.S355));
        Assert.AreEqual(family, geometry.Family);
        var actual = geometry.Parts.Single(p => p.Id == part);
        Assert.AreEqual(c, actual.WidthOrDiameter, 1e-12); Assert.AreEqual(t, actual.Thickness, 1e-12);
        Assert.AreEqual(kind, actual.Kind);
    }

    [DataTestMethod]
    [DataRow("H", 235d, 2, "Web")] [DataRow("H", 355d, 4, "Web")]
    [DataRow("T", 235d, 4, "Stem")] [DataRow("T", 355d, 4, "Stem")]
    [DataRow("C", 235d, 1, "Web")] [DataRow("C", 355d, 3, "Web")]
    [DataRow("RHS", 235d, 2, "LeftWall")] [DataRow("RHS", 355d, 4, "LeftWall")]
    [DataRow("CHS", 235d, 1, "Tube")] [DataRow("CHS", 460d, 2, "Tube")]
    public void WholeSectionReportsTheWorstPartAndItsEvidence(string family, double fy, int expected, string governing)
    {
        // Ratios from the dimension subtractions above, compared with Table5.2.
        var section = new SteelSection(Shape(family), SteelMaterialEN1993Data.S355);
        var result = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), fy);
        Assert.IsTrue(result.IsEvaluated); Assert.AreEqual(expected, result.NominalClass);
        Assert.IsTrue(result.GoverningParts.Contains(governing));
        Assert.IsTrue(result.Parts.All(p => p.Classification.YieldStrength == fy));
        Assert.IsTrue(result.Parts.All(p => p.Classification.NominalClass <= result.NominalClass));
    }

    [DataTestMethod]
    [DataRow("H")] [DataRow("T")] [DataRow("C")] [DataRow("RHS")] [DataRow("CHS")]
    public void UniformCompressionAgreesWithLegacyForRepresentativeFamilies(string family)
    {
        foreach (double fy in new[] { 235d, 275d, 355d, 420d, 460d })
        {
            var actual = new SteelSection(Shape(family), new GPC.Model.Materials.SteelMaterial("test", 210000, fy, 2 * fy));
            var probe = new ReferenceProbe(actual);
            var result = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(actual), fy);
            Assert.AreEqual(probe.Class(-1, 0), result.NominalClass);
        }
    }

    [TestMethod]
    public void GeometryAndResultsAreSnapshots()
    {
        var shape = (SectionH)Shape("H"); var section = new SteelSection(shape, SteelMaterialEN1993Data.S355);
        var geometry = ModelCompressionGeometry.Create(section);
        var result = Ec3UniformCompressionClassification.Evaluate(geometry, 235);
        shape.LenghtTopFlange = 1000;
        Assert.AreEqual(87d, geometry.Parts[0].WidthOrDiameter);
        Assert.AreEqual(2, result.NominalClass);
        Assert.AreNotEqual(geometry.Parts[0].WidthOrDiameter, ModelCompressionGeometry.Create(section).Parts[0].WidthOrDiameter);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<CompressionPartGeometry>)geometry.Parts).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<NamedCompressionPartClassification>)result.Parts).Clear());
    }

    [TestMethod]
    public void OneInvalidPartPreventsAWholeSectionClass()
    {
        var geometry = new CompressionSectionGeometry("explicit parts", new[] {
            new CompressionPartGeometry("valid", CompressionPartKind.InternalFlat, 100, 10),
            new CompressionPartGeometry("bad", CompressionPartKind.InternalFlat, -1, 10)
        });
        var result = Ec3UniformCompressionClassification.Evaluate(geometry, 235);
        Assert.AreEqual(SectionClassificationStatus.InvalidInput, result.Status); Assert.IsNull(result.NominalClass);
        Assert.AreEqual(0, result.GoverningParts.Count); Assert.AreEqual(2, result.Parts.Count);
        Assert.AreEqual("bad.WidthOrDiameter.NotPositiveFinite", result.DiagnosticCode);
        Assert.AreEqual(SectionClassificationStatus.MissingInput, Ec3UniformCompressionClassification.Evaluate(null!, 235).Status);
        Assert.AreEqual(SectionClassificationStatus.MissingInput, Ec3UniformCompressionClassification.Evaluate(geometry, null).Status);
    }

    [TestMethod]
    public void EmptyOrAmbiguousPartSetsCannotMasqueradeAsASection()
    {
        var part = new CompressionPartGeometry("p", CompressionPartKind.InternalFlat, 100, 10);
        Assert.ThrowsException<ArgumentException>(() => new CompressionSectionGeometry("x", Array.Empty<CompressionPartGeometry>()));
        Assert.ThrowsException<ArgumentException>(() => new CompressionSectionGeometry("x", new[] { part, part }));
        Assert.ThrowsException<ArgumentException>(() => new CompressionSectionGeometry("x", new CompressionPartGeometry[] { null! }));
        var source = new List<CompressionPartGeometry> { part }; var geometry = new CompressionSectionGeometry("x", source);
        source.Clear(); Assert.AreEqual(1, geometry.Parts.Count);
    }

    [TestMethod]
    public void AmbiguousBaseRhsRadiusAndAdditionalAngleRequirementsAreNotSilentlyMapped()
    {
        var rhs = new SteelSection(new SectionRHS(300, 200, 10, 10, 10, 10, "rounded", 15), SteelMaterialEN1993Data.S355);
        var angle = new SteelSection(new SectionL(100, 10, 100, 10, "angle"), SteelMaterialEN1993Data.S355);
        Assert.ThrowsException<NotSupportedException>(() => ModelCompressionGeometry.Create(rhs));
        Assert.ThrowsException<NotSupportedException>(() => ModelCompressionGeometry.Create(angle));
        Assert.ThrowsException<ArgumentNullException>(() => ModelCompressionGeometry.Create(null!));
    }
}
