using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BoltAuditRegressionTests
{
    private static EN1993BoltChecker Calculate(double n, double v,
        EN1993BoltChecker.ShearConnectionsCategoryType category = EN1993BoltChecker.ShearConnectionsCategoryType.A,
        BoltStresses.CombCaseType state = BoltStresses.CombCaseType.SLU)
    {
        var plate = new RectangularPlateWithBolts(300, 250, SteelMaterialEN1993Data.S235, 10,
            new[] { 200.0 }, new[] { 150.0 }, 16, BoltMaterialEN1993Data.Class10_9, new Point2d(50, 50));
        var system = new CoordinateSystem(plate.BoltGrid.CalculateBarycenter(), Vector3d.XAxis, Vector3d.YAxis);
        var force = new ResultBeamForces(n, v, 0, 0, 0, 0, system);
        var checker = new EN1993BoltChecker(plate,
            new List<BoltStresses> { new(new LoadCase("audit", LoadCase.LoadCaseTypes.SelfWeight), force, state) },
            new StandardEN1993p11(), new() { ShearConnectionsCategory = category });
        checker.PerformCheck(); return checker;
    }

    [DataTestMethod]
    [DataRow(.4)] [DataRow(4.0)] [DataRow(4.000001)]
    public void PositiveTensionIsConservedAcrossOneNewtonBoundary(double n)
    {
        var r = Calculate(n, 0).BoltResultsEN1993;
        Assert.AreEqual(n, r.Sum(x => x.BeamForces.N), 1e-12, "equilibrium of positive axial force");
        Assert.IsTrue(r.All(x => x.TensionIsActive), "positive demand must be evaluated");
        foreach (var b in r) Assert.AreEqual(n / 4 / b.TensionResistance, b.TensionRatio, 1e-12);
    }

    [DataTestMethod]
    [DataRow(.4)] [DataRow(4.0)]
    public void SmallShearKeepsItsActualUtilization(double v)
    {
        var r = Calculate(0, v).BoltResultsEN1993;
        Assert.AreEqual(v, r.Sum(x => x.BeamForces.V1), 1e-12);
        Assert.IsTrue(r.All(x => x.ShearIsActive));
        foreach (var b in r) Assert.AreEqual(v / 4 / b.ShearResistance, b.ShearRatio, 1e-12);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ExhaustedPreloadCannotGiveNegativeResistanceOrFavourableSlip(bool service)
    {
        // FpC=.7*fub*As; the formula's remaining contact force becomes negative.
        // All four M16 bolts carry 200 kN tension and 1 kN shear each.
        var r = Calculate(800000, 4000, service ? EN1993BoltChecker.ShearConnectionsCategoryType.B
            : EN1993BoltChecker.ShearConnectionsCategoryType.C,
            service ? BoltStresses.CombCaseType.SLS : BoltStresses.CombCaseType.SLU).BoltResultsEN1993;
        foreach (var b in r)
        {
            Assert.AreEqual(0, service ? b.SlipSerResistance : b.SlipResistance, "no residual clamping force");
            Assert.IsTrue(double.IsPositiveInfinity(service ? b.SlipSerRatio : b.SlipRatio), "nonzero shear with zero resistance fails");
        }
    }

    [TestMethod]
    public void ZeroActionDoesNotActivateChecksOrInventDemand()
    {
        foreach (var b in Calculate(0, 0).BoltResultsEN1993)
        {
            Assert.AreEqual(0, b.BeamForces.N); Assert.AreEqual(0, b.SollShear);
            Assert.IsFalse(b.TensionIsActive); Assert.IsFalse(b.ShearIsActive);
            Assert.AreEqual(0, b.CalcMaxRatio());
        }
    }

    [TestMethod]
    public void RectangularBoltGroupMatchesIndependentEccentricEquilibrium()
    {
        var grid = new RectangularBoltGrid(new[] { 200.0 }, new[] { 150.0 }, 16, BoltMaterialEN1993Data.Class8_8, new Point2d(-100, -75));
        // Fx=12000 at y=40 => Mz=-480000; applied Mz=1080000 => total 600000 Nmm.
        var force = new ResultBeamForces(0, 12000, -8000, 1080000, 0, 0,
            new CoordinateSystem(new Point3d(0, 40, 0), Vector3d.XAxis, Vector3d.YAxis));
        var r = grid.CalculateShearForcesElastic(force);
        foreach (var b in r)
        {
            Assert.AreEqual(3000 - 600000 * b.Key.Position.Y / 62500, b.Value.V1, 1e-9);
            Assert.AreEqual(-2000 + 600000 * b.Key.Position.X / 62500, b.Value.V2, 1e-9);
        }
        Assert.AreEqual(600000, r.Sum(b => b.Key.Position.X * b.Value.V2 - b.Key.Position.Y * b.Value.V1), 1e-7);
    }
}
