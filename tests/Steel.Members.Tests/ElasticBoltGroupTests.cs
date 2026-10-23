using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ElasticBoltGroupTests
{
    private static ConnectionPoint2D[] Rectangle() => new[]
    { new ConnectionPoint2D(-100, -50), new ConnectionPoint2D(100, -50), new ConnectionPoint2D(100, 50), new ConnectionPoint2D(-100, 50) };

    private static void Balanced(ElasticBoltGroupResult r)
    {
        Assert.AreEqual(ElasticBoltGroupStatus.Balanced, r.Status, string.Join("; ", r.Diagnostics));
        Assert.IsTrue(r.IsSupported); Assert.AreEqual(r.Points.Count, r.Forces.Count);
        Assert.IsNotNull(r.EquilibriumResidual); Assert.IsTrue(r.Forces.All(f => f.Fz >= 0));
    }

    [TestMethod]
    public void RectangularGroupMatchesSixComponentHandCalculation()
    {
        // Sum x^2=40000, sum y^2=10000, J=50000 mm2. T=5000+20*x+40*y.
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(12000, -8000, 20000, 400000, -800000, 600000));
        Balanced(r); Assert.AreEqual(2, r.GeometryRank);
        double[] fx = { 3600, 3600, 2400, 2400 }, fy = { -3200, -800, -800, -3200 }, fz = { 1000, 5000, 9000, 5000 };
        for (int i = 0; i < 4; i++)
        { Assert.AreEqual(fx[i], r.Forces[i].Fx, 1e-9); Assert.AreEqual(fy[i], r.Forces[i].Fy, 1e-9); Assert.AreEqual(fz[i], r.Forces[i].Fz, 1e-9); }
        Assert.AreEqual(9000, r.MaximumTension!.Value, 1e-9);
        Assert.AreEqual(Math.Sqrt(3600 * 3600 + 3200 * 3200), r.MaximumShear!.Value, 1e-9);
        Assert.AreEqual(1000, r.MinimumTrialFz!.Value, 1e-9);
        CheckEquilibrium(r, 12000, -8000, 20000, 400000, -800000, 600000);
    }

    [TestMethod]
    public void UnsymmetricTriangleUsesFullProductOfInertia()
    {
        // Centroid=(40,20); Ix=2400, Iy=4200, Ixy=-600 mm2.
        // Independently choose T(x,y)=100+2*x+3*y at original coordinates and sum actions manually.
        var p = new[] { new ConnectionPoint2D(0, 0), new ConnectionPoint2D(90, 0), new ConnectionPoint2D(30, 60) };
        var r = ElasticBoltGroup.Calculate(p, new(90, -60, 720, 6000, -6600, 6600, 40, 20));
        Balanced(r); double[] normal = { 100, 280, 340 }, fx = { 50, 50, -10 }, fy = { -60, 30, -30 };
        for (int i = 0; i < 3; i++)
        { Assert.AreEqual(normal[i], r.Forces[i].Fz, 1e-9); Assert.AreEqual(fx[i], r.Forces[i].Fx, 1e-9); Assert.AreEqual(fy[i], r.Forces[i].Fy, 1e-9); }
        CheckEquilibrium(r, 90, -60, 720, 6000, -6600, 6600);
    }

    [TestMethod]
    public void ApplicationHeightAndBothPlanarOffsetsAreIncluded()
    {
        // r=(30,-20,15); r x F=(-230000,-285000,80000) Nmm.
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(1000, 2000, 10000, 40000, -30000, 20000, 30, -20, 15));
        Balanced(r);
        Assert.AreEqual(-190000, r.ActionsAtCentroid.Mx); Assert.AreEqual(-315000, r.ActionsAtCentroid.My);
        Assert.AreEqual(100000, r.ActionsAtCentroid.Mz);
        CheckEquilibrium(r, 1000, 2000, 10000, -190000, -315000, 100000);
        Assert.AreEqual(2662.5, r.Forces[0].Fz, 1e-9);
    }

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(0.13)] [DataRow(0.7)] [DataRow(1.5707963267948966)] [DataRow(2.4)]
    public void RotatingGeometryAndActionsRotatesForces(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        var basis = ElasticBoltGroup.Calculate(Rectangle(), new(12000, -8000, 20000, 400000, -800000, 600000));
        var p = Rectangle().Select(v => new ConnectionPoint2D(c * v.X - s * v.Y, s * v.X + c * v.Y)).ToArray();
        var r = ElasticBoltGroup.Calculate(p, new(c * 12000 + s * 8000, s * 12000 - c * 8000, 20000,
            c * 400000 + s * 800000, s * 400000 - c * 800000, 600000));
        Balanced(r);
        for (int i = 0; i < p.Length; i++)
        {
            Assert.AreEqual(c * basis.Forces[i].Fx - s * basis.Forces[i].Fy, r.Forces[i].Fx, 1e-8);
            Assert.AreEqual(s * basis.Forces[i].Fx + c * basis.Forces[i].Fy, r.Forces[i].Fy, 1e-8);
            Assert.AreEqual(basis.Forces[i].Fz, r.Forces[i].Fz, 1e-8);
        }
    }

    [DataTestMethod]
    [DataRow(25.0, -70.0)] [DataRow(1000000000.0, -2000000000.0)]
    public void TranslatingGeometryAndApplicationPointPreservesDistribution(double dx, double dy)
    {
        var basis = ElasticBoltGroup.Calculate(Rectangle(), new(1000, 2000, 10000, 40000, -30000, 20000, 30, -20, 15));
        var r = ElasticBoltGroup.Calculate(Rectangle().Select(p => new ConnectionPoint2D(p.X + dx, p.Y + dy)),
            new(1000, 2000, 10000, 40000, -30000, 20000, 30 + dx, -20 + dy, 15));
        Balanced(r); Assert.AreEqual(dx, r.Centroid.X); Assert.AreEqual(dy, r.Centroid.Y);
        for (int i = 0; i < 4; i++)
        { Assert.AreEqual(basis.Forces[i].Fx, r.Forces[i].Fx, 1e-9); Assert.AreEqual(basis.Forces[i].Fy, r.Forces[i].Fy, 1e-9); Assert.AreEqual(basis.Forces[i].Fz, r.Forces[i].Fz, 1e-9); }
    }

    [TestMethod]
    public void ReversingInPlaneActionsReversesShearWithoutChangingTension()
    {
        var a = ElasticBoltGroup.Calculate(Rectangle(), new(12000, -8000, 20000, 400000, -800000, 600000));
        var b = ElasticBoltGroup.Calculate(Rectangle(), new(-12000, 8000, 20000, 400000, -800000, -600000));
        Balanced(a); Balanced(b);
        for (int i = 0; i < 4; i++)
        { Assert.AreEqual(-a.Forces[i].Fx, b.Forces[i].Fx, 1e-9); Assert.AreEqual(-a.Forces[i].Fy, b.Forces[i].Fy, 1e-9); Assert.AreEqual(a.Forces[i].Fz, b.Forces[i].Fz, 1e-9); }
    }

    [TestMethod]
    public void ObliqueCollinearGroupMatchesManualSolution()
    {
        // Along line (0.6,0.8), distances -100,0,100; T=1000+2*s.
        var p = new[] { new ConnectionPoint2D(-60, -80), new ConnectionPoint2D(0, 0), new ConnectionPoint2D(60, 80) };
        var r = ElasticBoltGroup.Calculate(p, new(900, 600, 3000, 32000, -24000, 20000));
        Balanced(r); Assert.AreEqual(1, r.GeometryRank);
        double[] fx = { 380, 300, 220 }, fy = { 140, 200, 260 }, fz = { 800, 1000, 1200 };
        for (int i = 0; i < 3; i++)
        { Assert.AreEqual(fx[i], r.Forces[i].Fx, 1e-9); Assert.AreEqual(fy[i], r.Forces[i].Fy, 1e-9); Assert.AreEqual(fz[i], r.Forces[i].Fz, 1e-9); }
        CheckEquilibrium(r, 900, 600, 3000, 32000, -24000, 20000);
    }

    [DataTestMethod]
    [DataRow(1.0)] [DataRow(1e-12)] [DataRow(1e-200)]
    public void HorizontalGroupDoesNotHideTinyUnsupportedMomentBesideLargeSupportedMoment(double unsupportedMx)
    {
        var p = new[] { new ConnectionPoint2D(-100, 0), new ConnectionPoint2D(100, 0) };
        var r = ElasticBoltGroup.Calculate(p, new(0, 0, 10000, unsupportedMx, 100000, 0));
        Assert.AreEqual(ElasticBoltGroupStatus.UnsupportedGeometry, r.Status); Assert.IsFalse(r.IsSupported); Assert.AreEqual(0, r.Forces.Count);
    }

    [TestMethod]
    public void ObliqueLineRejectsMomentAboutItsOwnAxis()
    {
        var p = new[] { new ConnectionPoint2D(-60, -80), new ConnectionPoint2D(60, 80) };
        var r = ElasticBoltGroup.Calculate(p, new(0, 0, 1000, 600, 800, 0));
        Assert.AreEqual(ElasticBoltGroupStatus.UnsupportedGeometry, r.Status);
    }

    [TestMethod]
    public void VerticalLineCarriesItsCompatibleBendingAndTorsion()
    {
        // sum y^2=5000 mm2: T=1000/2+(10000/5000)*y=500 +/- 100 N.
        // Initial new-test draft had 300/700 N, which would reconstruct 20000 Nmm; corrected by equilibrium.
        var p = new[] { new ConnectionPoint2D(0, -50), new ConnectionPoint2D(0, 50) };
        var r = ElasticBoltGroup.Calculate(p, new(100, 200, 1000, 10000, 0, 5000));
        Balanced(r); Assert.AreEqual(400, r.Forces[0].Fz, 1e-9); Assert.AreEqual(600, r.Forces[1].Fz, 1e-9);
        Assert.AreEqual(100, r.Forces[0].Fx, 1e-9); Assert.AreEqual(0, r.Forces[1].Fx, 1e-9);
        CheckEquilibrium(r, 100, 200, 1000, 10000, 0, 5000);
    }

    [TestMethod]
    public void SingletonCarriesForceAppliedAtItsOwnPosition()
    {
        var r = ElasticBoltGroup.Calculate(new[] { new ConnectionPoint2D(10, 20) }, new(3, 4, 5, 0, 0, 0, 10, 20));
        Balanced(r); Assert.AreEqual(0, r.GeometryRank); Assert.AreEqual(3, r.Forces[0].Fx); Assert.AreEqual(4, r.Forces[0].Fy);
        Assert.AreEqual(5, r.Forces[0].Fz); Assert.AreEqual(5, r.MaximumShear!.Value);
    }

    [TestMethod]
    public void SingletonAcceptsEccentricForceOnlyWhenFreeMomentCancelsTransport()
    {
        // r=(2,-3,2), F=(3,4,5); r x F=(-23,-4,17).
        var r = ElasticBoltGroup.Calculate(new[] { new ConnectionPoint2D(10, 20) }, new(3, 4, 5, 23, 4, -17, 12, 17, 2));
        Balanced(r); Assert.AreEqual(3, r.Forces[0].Fx); Assert.AreEqual(5, r.Forces[0].Fz);
    }

    [DataTestMethod]
    [DataRow(0, 1.0)] [DataRow(1, 1.0)] [DataRow(2, 1.0)]
    [DataRow(0, 1e-200)] [DataRow(1, 1e-200)] [DataRow(2, 1e-200)]
    public void SingletonRejectsEveryNonzeroMomentIncludingTinyValues(int component, double moment)
    {
        var r = ElasticBoltGroup.Calculate(new[] { new ConnectionPoint2D(0, 0) },
            new(0, 0, 1000, component == 0 ? moment : 0, component == 1 ? moment : 0, component == 2 ? moment : 0));
        Assert.AreEqual(ElasticBoltGroupStatus.UnsupportedGeometry, r.Status); Assert.AreEqual(0, r.Forces.Count);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)]
    public void ZeroActionsAreBalancedForEveryRank(int rank)
    {
        var p = rank == 0 ? new[] { new ConnectionPoint2D(0, 0) } : rank == 1
            ? new[] { new ConnectionPoint2D(-50, 0), new ConnectionPoint2D(50, 0) } : Rectangle();
        var r = ElasticBoltGroup.Calculate(p, new(0, 0, 0, 0, 0, 0)); Balanced(r);
        Assert.AreEqual(rank, r.GeometryRank); Assert.AreEqual(0, r.MaximumShear!.Value); Assert.AreEqual(0, r.MaximumTension!.Value);
    }

    [DataTestMethod]
    [DataRow(-1.0)] [DataRow(-1e-200)]
    public void CompressionRequiresContactWithoutReturningCompressedBolts(double compression)
    {
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(0, 0, compression, 0, 0, 0));
        Assert.AreEqual(ElasticBoltGroupStatus.RequiresContact, r.Status); Assert.AreEqual(0, r.Forces.Count);
        Assert.AreEqual(compression / 4, r.MinimumTrialFz!.Value); Assert.IsNull(r.EquilibriumResidual);
        Assert.IsNull(r.MaximumTension); Assert.IsNull(r.MaximumShear);
    }

    [TestMethod]
    public void PartialContactIsNotSilentlyClipped()
    {
        // T=250+10*y gives -250 N on the lower row and +750 N on the upper row.
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(0, 0, 1000, 100000, 0, 0));
        Assert.AreEqual(ElasticBoltGroupStatus.RequiresContact, r.Status); Assert.AreEqual(-250, r.MinimumTrialFz!.Value, 1e-10);
        Assert.AreEqual(0, r.Forces.Count); Assert.IsFalse(r.IsSupported);
    }

    [TestMethod]
    public void ExactZeroTensionAtNeutralRowIsPermitted()
    {
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(0, 0, 2000, 100000, 0, 0));
        Balanced(r); Assert.AreEqual(0, r.MinimumTrialFz!.Value, 1e-10); CheckEquilibrium(r, 0, 0, 2000, 100000, 0, 0);
    }

    [DataTestMethod]
    [DataRow(1e-12)] [DataRow(1e-200)] [DataRow(1e-300)]
    public void TinyPureTorsionRemainsAnActualDemand(double moment)
    {
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(0, 0, 0, 0, 0, moment));
        Balanced(r); Assert.IsTrue(r.MaximumShear > 0);
        double torque = r.Forces.Sum(f => f.Position.X * f.Fy - f.Position.Y * f.Fx);
        Assert.AreEqual(1, torque / moment, 1e-12);
    }

    [TestMethod]
    public void NearCollinearPlanarGeometryDoesNotProduceAnIllConditionedSolution()
    {
        var p = new[] { new ConnectionPoint2D(-100, 0), new ConnectionPoint2D(0, 1e-5), new ConnectionPoint2D(100, 0) };
        var r = ElasticBoltGroup.Calculate(p, new(0, 0, 1000, 0, 0, 0));
        Assert.AreEqual(ElasticBoltGroupStatus.UnsupportedGeometry, r.Status); Assert.AreEqual(2, r.GeometryRank);
        StringAssert.Contains(r.Diagnostics.Single(), "inertia ratio");
    }

    [TestMethod]
    public void InputSnapshotAndResultCollectionsCannotBeMutated()
    {
        var p = Rectangle().ToList(); var r = ElasticBoltGroup.Calculate(p, new(0, 0, 1000, 0, 0, 0));
        p.Clear(); Balanced(r); Assert.AreEqual(4, r.Points.Count);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionPoint2D>)r.Points)[0] = new(0, 0));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ElasticBoltForce>)r.Forces).Clear());
    }

    [TestMethod]
    public void DuplicatePointsCannotArtificiallyMultiplyBoltCapacity()
    {
        var p = new[] { new ConnectionPoint2D(10, 20), new ConnectionPoint2D(10, 20) };
        Assert.ThrowsException<ArgumentException>(() => ElasticBoltGroup.Calculate(p, new(0, 0, 1000, 0, 0, 0)));
    }

    [TestMethod]
    public void NullAndEmptyInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ElasticBoltGroup.Calculate(null!, new(0, 0, 0, 0, 0, 0)));
        Assert.ThrowsException<ArgumentNullException>(() => ElasticBoltGroup.Calculate(Rectangle(), null!));
        Assert.ThrowsException<ArgumentException>(() => ElasticBoltGroup.Calculate(Array.Empty<ConnectionPoint2D>(), new(0, 0, 0, 0, 0, 0)));
    }

    [DataTestMethod]
    [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(double.NegativeInfinity)]
    public void NonFiniteCoordinatesAndActionsAreRejected(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionPoint2D(value, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConnectionPoint2D(0, value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PlanarConnectionActions(value, 0, 0, 0, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PlanarConnectionActions(0, 0, 0, 0, 0, value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PlanarConnectionActions(0, 0, 0, 0, 0, 0, applicationZ: value));
    }

    [TestMethod]
    public void NumericOverflowIsReportedInsteadOfReturningInfiniteForces()
    {
        var r = ElasticBoltGroup.Calculate(Rectangle(), new(1e308, 1e308, 1e308, 0, 0, 0, 1e308, 1e308, 1e308));
        Assert.AreEqual(ElasticBoltGroupStatus.NumericalFailure, r.Status); Assert.AreEqual(0, r.Forces.Count);
    }

    [TestMethod]
    public void PermutingInputChangesOnlyOutputOrder()
    {
        var p = Rectangle(); var actions = new PlanarConnectionActions(12000, -8000, 20000, 400000, -800000, 600000);
        var a = ElasticBoltGroup.Calculate(p, actions); var b = ElasticBoltGroup.Calculate(p.Reverse(), actions); Balanced(b);
        for (int i = 0; i < 4; i++)
        { Assert.AreEqual(a.Forces[i].Fx, b.Forces[3 - i].Fx, 1e-9); Assert.AreEqual(a.Forces[i].Fy, b.Forces[3 - i].Fy, 1e-9); Assert.AreEqual(a.Forces[i].Fz, b.Forces[3 - i].Fz, 1e-9); }
    }

    private static void CheckEquilibrium(ElasticBoltGroupResult r, double fx, double fy, double fz, double mx, double my, double mz)
    {
        // Reconstruct directly from ORIGINAL coordinates, independently of the solver's normalized system.
        double dx(ElasticBoltForce f) => f.Position.X - r.Centroid.X;
        double dy(ElasticBoltForce f) => f.Position.Y - r.Centroid.Y;
        Assert.AreEqual(fx, r.Forces.Sum(f => f.Fx), 1e-8); Assert.AreEqual(fy, r.Forces.Sum(f => f.Fy), 1e-8);
        Assert.AreEqual(fz, r.Forces.Sum(f => f.Fz), 1e-8); Assert.AreEqual(mx, r.Forces.Sum(f => dy(f) * f.Fz), 1e-7);
        Assert.AreEqual(my, r.Forces.Sum(f => -dx(f) * f.Fz), 1e-7);
        Assert.AreEqual(mz, r.Forces.Sum(f => dx(f) * f.Fy - dy(f) * f.Fx), 1e-7);
    }
}
