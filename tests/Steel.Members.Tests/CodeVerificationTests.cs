using System.Text.Json;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Members;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SC = GPC.Checkers.Steel.Checkers.EN1993p11Checker.SectionClass;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class CodeVerificationTests
{
    // New independent fixture: analytic integrals + clipping about the principal plastic PNA.
    // Source generator is committed alongside this fixture; no GPC output supplies an expected value.
    public record Oracle(string Name, double Area, double I1, double I2, double Wpl1, double Wpl2, double Wel1, double Wel2);
    private static Oracle[] Oracles => JsonSerializer.Deserialize<Oracle[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "section-oracles.json")))!;
    public static IEnumerable<object[]> BendingCases =>
        from o in Oracles from code in new[] { "EC3", "NTC2018" }
        from cl in new[] { 1, 2, 3 } from axis in new[] { 1, 2 }
        select new object[] { o.Name, code, cl, axis };
    public static IEnumerable<object[]> CompressionCases =>
        from o in Oracles from cl in new[] { 1, 2, 3 } select new object[] { o.Name, cl };
    public static IEnumerable<object[]> ShearCases =>
        from shape in new[] { "H", "C", "T", "RHS", "CHS" }
        from code in new[] { "EC3", "NTC2018" } from axis in new[] { 1, 2 }
        select new object[] { shape, code, axis };
    public static IEnumerable<object[]> BucklingCases =>
        from alpha in new[] { .13, .21, .34, .49, .76 }
        from lambda in new[] { .1, .2, .5, 1.0, 2.0 } select new object[] { alpha, lambda };
    public static IEnumerable<object[]> PlateCases =>
        from family in new[] { "inner", "outer", "CHS" } from boundary in new[] { 1, 2, 3 }
        from offset in new[] { -1e-7, 1e-7 } select new object[] { family, boundary, offset };

    [DataTestMethod, DynamicData(nameof(BendingCases))]
    public void BendingAgainstIndependentSectionIntegrals(string name, string code, int cl, int axis)
    {
        var o = Oracles.Single(x => x.Name == name);
        var p = new Probe(name, code);
        double expectedW = axis == 1 ? (cl <= 2 ? o.Wpl1 : o.Wel1) : (cl <= 2 ? o.Wpl2 : o.Wel2);
        double actual = code == "EC3" ? p.Moment(cl, axis)
            : Ntc2018SectionResistance.Bending(axis == 1 ? p.Section.Wpl1 : p.Section.Wpl2,
                axis == 1 ? p.Section.Wel1 : p.Section.Wel2, 235, cl);
        Near(expectedW * 235 / (code == "EC3" ? 1 : 1.05), actual, 2e-6);
    }

    [DataTestMethod, DynamicData(nameof(CompressionCases))]
    public void NtcCompressionAgainstIndependentArea(string name, int cl)
    {
        double expected = Oracles.Single(o => o.Name == name).Area * 235 / 1.05;
        Near(expected, Ntc2018SectionResistance.Compression(new Probe(name).Section.Area, 235, cl));
    }

    [DataTestMethod, DynamicData(nameof(ShearCases))]
    public void ShearAreaAndPartialFactor(string name, string code, int axis)
    {
        var p = new Probe(name, code);
        double area = Oracles.Single(o => o.Name == name).Area;
        double av = name switch
        {
            "H" => axis == 1 ? 8000 : 2800,
            "C" => axis == 1 ? 3000 : 2850,
            "T" => axis == 1 ? 3000 : code == "NTC2018" ? .9 * 1850 : 1925,
            "RHS" => area * (axis == 1 ? 200 : 300) / 500,
            "CHS" => 2 * area / Math.PI,
            _ => throw new InvalidOperationException()
        };
        double expected = av * 235 / (Math.Sqrt(3) * (code == "EC3" ? 1 : 1.05));
        Near(expected, p.Shear(axis));
        if (code == "NTC2018") Near(expected, Ntc2018SectionResistance.Shear(av, 235));
    }

    [DataTestMethod, DynamicData(nameof(BucklingCases))]
    public void BucklingCurvesAndEulerLimit(double alpha, double lambda)
    {
        // A=1000 mm2; E=210000 MPa; I=1e6 mm4. L chosen from the prescribed lambda.
        double length = lambda * Math.PI * Math.Sqrt(210000 * 1e6 / (1000 * 235));
        double ncr = 235000 / (lambda * lambda);
        double phi = (1 + alpha * (lambda - .2) + lambda * lambda) / 2;
        double chi = lambda <= .2 ? 1 : 1 / (phi + Math.Sqrt(phi * phi - lambda * lambda));
        var actual = Ntc2018SectionResistance.FlexuralBuckling(1000, 1e6, length, 210000, 235, alpha, 1);
        Near(ncr, actual.CriticalForce); Near(lambda, actual.NormalizedSlenderness);
        Near(chi, actual.Reduction); Near(chi * 235000 / 1.05, actual.Resistance);
        Assert.IsTrue(actual.Resistance <= 235000 / 1.05 * (1 + 1e-12));
        Assert.IsTrue(actual.Resistance <= ncr / 1.05 * (1 + 1e-12));
        var ec = new Probe("H");
        Near(chi, ec.Chi(alpha, lambda));
    }

    [DataTestMethod, DynamicData(nameof(PlateCases))]
    public void ClassificationBothSidesOfEveryBoundary(string family, int boundary, double offset)
    {
        double[] limits = family == "inner" ? new[] { 33d, 38d, 42d } :
            family == "outer" ? new[] { 9d, 10d, 14d } : new[] { 50d, 70d, 90d };
        var p = new Probe("H");
        Assert.AreEqual(offset < 0 ? boundary : boundary + 1, p.Classify(family, limits[boundary - 1] + offset));
    }

    [DataTestMethod]
    [DataRow("H", 1)] [DataRow("H", 2)] [DataRow("RHS", 1)] [DataRow("RHS", 2)]
    [DataRow("CHS", 1)] [DataRow("CHS", 2)]
    public void HighShearSignDoesNotChangeMomentCapacity(string name, int axis)
    {
        var p = new Probe(name);
        double v = .75 * p.Shear(axis == 1 ? 2 : 1);
        double positive = p.MomentWithShear(axis, v);
        double negative = p.MomentWithShear(axis, -v);
        Near(positive, negative);
        Assert.IsTrue(positive > 0 && positive < p.Moment(1, axis));
        Near(p.Moment(1, axis), p.MomentWithShear(axis, .5 * p.Shear(axis == 1 ? 2 : 1)));
    }

    [DataTestMethod]
    [DataRow("H", .1)] [DataRow("H", .5)] [DataRow("RHS", .1)] [DataRow("RHS", .5)]
    public void SectionNMUsesPlasticAxialResistanceAndIsSignSymmetric(string shape, double n)
    {
        var p = new Probe(shape); var o = Oracles.Single(x => x.Name == shape);
        double m1 = o.Wpl1 * 235, m2 = o.Wpl2 * 235;
        double a = Math.Min((o.Area - (shape == "H" ? 8000 : 4000)) / o.Area, .5);
        double expected1 = Math.Min(m1, m1 * (1 - n) / (1 - .5 * a));
        double expected2 = shape == "H" ? n <= a ? m2 : m2 * (1 - Math.Pow((n - a) / (1 - a), 2)) :
            Math.Min(m2, m2 * (1 - n) / (1 - .5 * Math.Min((o.Area - 6000) / o.Area, .5)));
        foreach (double sign in new[] { -1d, 1d })
        {
            // Deliberately unequal fictitious member capacities must have no effect.
            var actual = p.NM(sign * n * o.Area * 235, .2, .4);
            Near(expected1, actual.Item1); Near(expected2, actual.Item2);
        }
    }

    [DataTestMethod]
    [DataRow("H")] [DataRow("C")] [DataRow("T")] [DataRow("L")]
    [DataRow("RHS")] [DataRow("CHS")] [DataRow("Rectangular")] [DataRow("Circular")]
    public void NtcElasticInteractionAndNetSection(string shape)
    {
        var o = Oracles.Single(x => x.Name == shape);
        var s = new Probe(shape).Section;
        foreach (double sign in new[] { -1d, 1d })
            Near(.6, Ntc2018SectionResistance.ElasticNormalInteraction(sign * .1 * o.Area * 235 / 1.05,
                sign * .2 * o.Wel1 * 235 / 1.05, -.3 * o.Wel2 * 235 / 1.05,
                s.Area, s.Wel1, s.Wel2, 235, 3), 2e-6);
        Near(o.Area * 235 / 1.05, Ntc2018SectionResistance.Tension(s.Area, 235, 360));
        Near(.9 * .6 * o.Area * 360 / 1.25, Ntc2018SectionResistance.Tension(s.Area, 235, 360, .6 * o.Area));
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidPositiveInputsAreRejected(double bad)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ntc2018SectionResistance.Compression(bad, 235, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ntc2018SectionResistance.Shear(1000, bad));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ntc2018SectionResistance.FlexuralBuckling(1000, 1e6, bad, 210000, 235, .21, 1));
    }

    [TestMethod]
    public void Class4CannotSilentlyUseGrossProperties()
    {
        var p = new Probe("H");
        Assert.ThrowsException<NotSupportedException>(() => p.Moment(4, 1));
        Assert.ThrowsException<NotSupportedException>(() => p.Moment(4, 2));
        Assert.ThrowsException<NotSupportedException>(() => Ntc2018SectionResistance.Compression(1000, 235, 4));
        Assert.ThrowsException<NotSupportedException>(() => Ntc2018SectionResistance.Bending(1e6, 8e5, 235, 4));
        Assert.ThrowsException<NotSupportedException>(() => Ntc2018SectionResistance.FlexuralBuckling(1000, 1e6, 3000, 210000, 235, .21, 4));
    }

    [TestMethod]
    public void ClassificationUsesClearFlangeOutstand()
    {
        // b=190, tw=20, tf=10 gives c/t=8.5, class 1; b/2tf=9.5 erroneously gives class 2.
        var shape = new SteelSection(new SectionH(200, 20, 190, 10, 190, 10, ""), SteelMaterialEN1993Data.S235);
        var p = new Probe(shape);
        Assert.AreEqual(1, p.CompressionClass());
    }

    [TestMethod]
    public void ZeroNormalForceHasDefinedInteractionLimit()
    {
        var p = new Probe("H");
        Near(0, p.EpsilonInteraction(0, 0));
        Assert.IsTrue(double.IsPositiveInfinity(p.EpsilonInteraction(0, 1e6)));
        Near(0, p.EpsilonInteraction(-1e5, 0));
    }

    [DataTestMethod]
    [DataRow("H", 0)] [DataRow("H", 1)] [DataRow("H", 2)]
    [DataRow("RHS", 0)] [DataRow("RHS", 1)] [DataRow("RHS", 2)]
    public void BulkPureLoadCasesDoNotHideNonfiniteInteractions(string shape, int load)
    {
        var p = new Probe(shape);
        p.AddLoad(load == 0 ? new ResultBeamForces(-10000, 0, 0, 0, 0, 0, CoordinateSystem.Global) :
            load == 1 ? new ResultBeamForces(0, 1000, 1000, 0, 0, 0, CoordinateSystem.Global) :
            new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global));
        p.PerformCheck();
        Assert.AreEqual(0, p.GetErrorLog().Count, string.Join("; ", p.GetErrorLog()));
        Assert.IsTrue(double.IsFinite(p.EN1993p11BeamStationResults[0].WorkingRatio));
    }

    [DataTestMethod]
    [DataRow("roundedRHS")] [DataRow("taperedH")] [DataRow("box")]
    public void UnqualifiedSpecialShapesDoNotInheritAnOrdinaryProfileCheck(string name)
    {
        GPC.Model.Sections.Section shape = name switch
        {
            "roundedRHS" => new SectionRHSRoundedCorners(300, 200, 10, 20, 10, ""),
            "taperedH" => new SectionHTaperFlange(300, 10, 200, 20, .1, 10, 5, 50, ""),
            _ => new SectionSteelBox(300, 10, 100, 20, 300, 20, 200, 200, "")
        };
        var p = new Probe(new SteelSection(shape, SteelMaterialEN1993Data.S235));
        p.AddLoad(new ResultBeamForces(-1000, 0, 0, 0, 0, 0, CoordinateSystem.Global));
        p.PerformCheck();
        Assert.AreEqual(1, p.GetErrorLog().Count);
        StringAssert.Contains(p.GetErrorLog()[0], shape.GetType().Name);
        Assert.IsTrue(double.IsNaN(p.EN1993p11BeamStationResults[0].WorkingRatio));
    }

    [DataTestMethod]
    [DataRow(-1d)] [DataRow(1d)]
    public void TorsionNeedsAnExplicitQualifiedMethod(double sign)
    {
        var p = new Probe("H");
        p.AddLoad(new ResultBeamForces(0, 0, 0, sign, 0, 0, CoordinateSystem.Global));
        p.PerformCheck();
        Assert.AreEqual(1, p.GetErrorLog().Count);
        StringAssert.Contains(p.GetErrorLog()[0], "Torsion");
        Assert.IsTrue(double.IsNaN(p.EN1993p11BeamStationResults[0].WorkingRatio));
    }

    private static void Near(double expected, double actual, double relative = 1e-10)
    {
        Assert.IsTrue(double.IsFinite(actual), $"Nonfinite result: {actual}");
        Assert.AreEqual(expected, actual, relative * Math.Max(1, Math.Abs(expected)));
    }

    private sealed class Probe : EN1993p11Checker
    {
        public SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        public Probe(string name, string code = "EC3") : this(new SteelSection(Shape(name),
            SteelMaterialEN1993Data.S235, GPC.Model.Sections.Section.SectionTypes.Rolled,
            GPC.Model.Sections.Section.FormedTypes.HotFinished), code) { }
        public Probe(SteelSection section, string code = "EC3") : base(
            new BeamElement(Point3d.Origin, new Point3d(3000, 0, 0), section),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.EndsRestrained,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, .5, 1),
            code == "NTC2018" ? new StandardNTC2018Steel() : new StandardEN1993p11()) { }
        public double Moment(int cl, int axis) => axis == 1 ? CalculateMcRd1((SC)cl, Section) : CalculateMcRd2((SC)cl, Section);
        public double Shear(int axis) => axis == 1 ? CalculateShear1Capacity(Forces(), Section) : CalculateShear2Capacity(Forces(), Section);
        public double MomentWithShear(int axis, double v) => axis == 1 ?
            CalculateBendingMoment1Capacity(Forces(v2: v), SC.Class1, Section) :
            CalculateBendingMoment2Capacity(Forces(v1: v), SC.Class1, Section);
        public double Chi(double alpha, double lambda) => GetChi(GetPhiForBuckling(alpha, lambda), lambda);
        public int Classify(string family, double ratio) => (int)(family == "inner" ? GetClassCompressedInnerPlate(ratio, 1) :
            family == "outer" ? GetClassCompressedOuterPlate(ratio, 1) : GetClassCHSBending(ratio, 1));
        public int CompressionClass() => (int)CalculateSectionClassDueToCompression(Forces(n: -1000), Section);
        public double EpsilonInteraction(double n, double m) => CalculateEpsilonyForInteraction(SC.Class1, Forces(n: n, m1: m), Section);
        public void AddLoad(ResultBeamForces forces) => BeamElement.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("General check", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        public (double, double) NM(double n, double factor1, double factor2)
        {
            double resistance = Section.Area * Fy / GammaM0;
            CalculateMNRd1(SC.Class1, Section, n, factor1 * resistance, factor2 * resistance, resistance,
                Moment(1, 1), Moment(1, 2), out double m1, out double m2);
            return (m1, m2);
        }
        private static ResultBeamForces Forces(double n = 0, double v1 = 0, double v2 = 0, double m1 = 0)
            => new(n, v1, v2, 0, m1, 0, CoordinateSystem.Global);
        private static GPC.Model.Sections.Section Shape(string name) => name switch
        {
            "H" => new SectionH(300, 10, 200, 20, 200, 20, ""),
            "C" => new SectionC(300, 10, 100, 15, 100, 15, ""),
            "T" => new SectionT(200, 200, 10, 15, ""),
            "L" => new SectionL(100, 12, 150, 10, ""),
            "RHS" => new SectionRHS(300, 200, 10, 10, 10, 10, ""),
            "CHS" => new SectionCHS(200, 10, ""),
            "Rectangular" => new SectionRectangular(200, 100, ""),
            "Circular" => new SectionCircular(100, ""),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }
}
