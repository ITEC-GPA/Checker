using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class ChannelClassificationRegressionTests
{
    // EN 1993-1-1:2005, Table 5.2 sheet 2: S235, c/t limits 9, 10, 14.
    // The web is deliberately compact, so only the flange outstand governs.
    public static IEnumerable<object[]> OutstandCases =>
        from mode in new[] { "compression", "bending", "combined" }
        from radius in new[] { 0d, 10d }
        from boundary in new[] { 1, 2, 3 }
        from offset in new[] { -1e-6, 1e-6 }
        select new object[] { mode, radius, boundary, offset };

    [DataTestMethod, DynamicData(nameof(OutstandCases))]
    public void EveryChannelClassificationPathUsesTheClearOutstand(
        string mode, double radius, int boundary, double offset)
    {
        double limit = boundary switch { 1 => 9, 2 => 10, _ => 14 };
        double width = 20 + radius + (limit + offset) * 10;
        var shape = new SectionC(300, 20, width, 10, width, 10, "", radius);
        Assert.AreEqual(offset < 0 ? boundary : boundary + 1, new Probe(shape).Classify(mode),
            $"c/t={limit + offset}, r={radius}, mode={mode}");
    }

    [DataTestMethod]
    [DataRow("compression")] [DataRow("bending")] [DataRow("combined")]
    public void ChannelWebClearWidthExcludesBothRootFillets(string mode)
    {
        // h=370, tf=10, r=15, tw=10 => c/t=32, class 1 in uniform compression.
        // Taking the gross web height gives 35 and the incorrect class 2.
        var shape = new SectionC(370, 10, 80, 10, 80, 10, "", 15);
        Assert.AreEqual(1, new Probe(shape).Classify(mode));
    }

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        public Probe(SectionC shape) : base(
            new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
                new SteelSection(shape, SteelMaterialEN1993Data.S235)),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11()) { }

        public int Classify(string mode)
        {
            var forces = new ResultBeamForces(mode == "bending" ? 0 : -1000, 0, 0, 0,
                mode == "compression" ? 0 : 1e6, 0, CoordinateSystem.Global);
            return (int)(mode switch
            {
                "compression" => CalculateSectionClassDueToCompression(forces, Section),
                "bending" => CalculateSectionClassDueToBending(forces, Section),
                "combined" => CalculateSectionClassDueToCombinedBendingAndCompression(forces, Section),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            });
        }
    }
}
