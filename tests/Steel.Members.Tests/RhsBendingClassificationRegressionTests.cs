using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class RhsBendingClassificationRegressionTests
{
    // Table 5.2, internal plates in pure bending: 72/83/124 epsilon.
    // All these webs are class 4 in uniform compression (>42 epsilon).
    public static IEnumerable<object[]> Cases =>
        from material in new[] { "S235", "S355" }
        from rotated in new[] { false, true }
        from bendingClass in new[] { 1, 2, 3, 4 }
        from axis in new[] { 1, 2 }
        from sign in new[] { -1d, 1d }
        select new object[] { material, rotated, bendingClass, axis, sign };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void SymmetricRhsUsesTheLoadedPrincipalAxis(
        string material, bool rotated, int bendingClass, int axis, double sign)
    {
        var probe = MakeProbe(material, rotated, bendingClass);
        var forces = Force(axis, sign);
        int expected = axis == 1 ? bendingClass : 4;
        Assert.AreEqual(expected, probe.BendingClass(forces));
        Assert.AreEqual(expected, probe.CombinedClass(forces));
    }

    public static IEnumerable<object[]> BulkCases =>
        from rotated in new[] { false, true }
        from bendingClass in new[] { 1, 2, 3 }
        from sign in new[] { -1d, 1d }
        select new object[] { rotated, bendingClass, sign };

    [DataTestMethod, DynamicData(nameof(BulkCases))]
    public void BulkPureBendingUsesTheCorrespondingPlasticOrElasticCapacity(
        bool rotated, int bendingClass, double sign)
    {
        var probe = MakeProbe("S235", rotated, bendingClass);
        probe.AddLoad(Force(1, sign));
        probe.PerformCheck();
        Assert.AreEqual(0, probe.GetErrorLog().Count, string.Join("; ", probe.GetErrorLog()));
        var result = probe.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(bendingClass, (int)result.BendingCompressionClass);
        // Independent closed-form integral of a hollow rectangle, N-mm-MPa.
        double h = Height(bendingClass, 235), b = 150, t = 10;
        double modulus = bendingClass <= 2
            ? (b * h * h - (b - 2 * t) * (h - 2 * t) * (h - 2 * t)) / 4
            : (b * Math.Pow(h, 3) - (b - 2 * t) * Math.Pow(h - 2 * t, 3)) / (6 * h);
        double expected = modulus * 235;
        Assert.AreEqual(expected, result.BendingMoment1Capacity, expected * 1e-10);
        Assert.IsTrue(double.IsFinite(result.WorkingRatio));
    }

    [TestMethod]
    public void UniaxialBendingShortcutIsNotAppliedToCombinedLoading()
    {
        var probe = MakeProbe("S235", false, 1);
        // This regression preserves the current conservative fallback; it does not qualify it.
        Assert.AreEqual(4, probe.CombinedClass(new ResultBeamForces(-1000, 0, 0, 0, 1e6, 0, CoordinateSystem.Global)));
        Assert.AreEqual(4, probe.CombinedClass(new ResultBeamForces(0, 0, 0, 0, 1e6, 1e6, CoordinateSystem.Global)));
    }

    private static double Height(int bendingClass, double fy)
        => 20 + 10 * Math.Sqrt(235 / fy) * (bendingClass switch { 1 => 50, 2 => 78, 3 => 100, _ => 130 });
    private static ResultBeamForces Force(int axis, double sign)
        => new(0, 0, 0, 0, axis == 1 ? sign * 1e6 : 0, axis == 2 ? sign * 1e6 : 0, CoordinateSystem.Global);
    private static Probe MakeProbe(string material, bool rotated, int bendingClass)
    {
        SteelMaterial steel = material == "S235" ? SteelMaterialEN1993Data.S235 : SteelMaterialEN1993Data.S355;
        double h = Height(bendingClass, steel.Fyk);
        return new Probe(new SteelSection(new SectionRHS(rotated ? 150 : h, rotated ? h : 150,
            10, 10, 10, 10, ""), steel));
    }

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        public Probe(SteelSection section) : base(
            new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0), section),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11()) { }
        public int BendingClass(ResultBeamForces forces) => (int)CalculateSectionClassDueToBending(forces, Section);
        public int CombinedClass(ResultBeamForces forces) => (int)CalculateSectionClassDueToCombinedBendingAndCompression(forces, Section);
        public void AddLoad(ResultBeamForces forces) => BeamElement.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("RHS pure bending", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
    }
}
