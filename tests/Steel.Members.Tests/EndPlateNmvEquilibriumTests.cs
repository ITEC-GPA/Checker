using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateNmvEquilibriumTests
{
    static EndPlateNmvSpringRow[] Rows(bool asymmetric = false) => new[] {
        new EndPlateNmvSpringRow("upper", 80, 10000), new EndPlateNmvSpringRow("lower", -80, asymmetric ? 20000 : 10000) };
    static EndPlateNmvContactBand[] Bands() => new[] {
        new EndPlateNmvContactBand("upper", 100, 120, 30000), new EndPlateNmvContactBand("lower", -120, -100, 30000) };

    [TestMethod]
    public void PureTensionHasNoContactAndTheIndependentTwoSpringSolution()
    {
        var r = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), 2000, 0);
        Assert.IsTrue(r.IsConverged); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.AreEqual(.1, r.AxialOpening, 1e-10); Assert.AreEqual(0, r.Rotation, 1e-12);
        Assert.IsTrue(r.Contacts.All(c => c.Compression == 0 && c.ClosedLength == 0 && c.ResultantZ == null));
        Assert.IsTrue(r.Rows.All(row => Math.Abs(row.Tension - 1000) < 1e-7));
        Assert.AreEqual(20000, r.Tangent.Axial, 1e-8);
        Assert.AreEqual(128e6, r.Tangent.Rotation, 1e-5);
        Assert.AreEqual(0, r.Tangent.Coupling, 1e-8);
    }

    [TestMethod]
    public void PureCompressionUsesBothPhysicalBandsAndTheirActualCentroids()
    {
        var r = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), -6000, 0);
        Assert.AreEqual(-.1, r.AxialOpening, 1e-10); Assert.AreEqual(0, r.Rotation, 1e-12);
        Assert.IsTrue(r.Rows.All(row => row.Tension == 0));
        Assert.AreEqual(3000, r.Contacts[0].Compression, 1e-7);
        Assert.AreEqual(20, r.Contacts[0].ClosedLength, 1e-10);
        Assert.AreEqual(110, r.Contacts[0].ResultantZ!.Value, 1e-10);
        Assert.AreEqual(150, r.Contacts[0].MaximumCompressionPerLength, 1e-8);
        Assert.AreEqual(60000 * (110 * 110 + 20d * 20 / 12), r.Tangent.Rotation, 1e-5);
    }

    [DataTestMethod]
    [DataRow(.2, .001)][DataRow(-.2, .001)][DataRow(0, .001)]
    [DataRow(.11, .001)][DataRow(-.11, .001)][DataRow(.08, .001)]
    [DataRow(.2, -.001)][DataRow(-.2, -.001)][DataRow(0, -.001)]
    [DataRow(.11, -.001)][DataRow(-.11, -.001)]
    public void RecoversPrescribedMotionFromIndependentCompositeMidpointQuadrature(double u, double theta)
    {
        // Independent numerical integration, not the production clipped polynomial.
        var rows = Rows(true); var bands = Bands();
        double n = 0, m = 0;
        foreach (var row in rows) { double f = row.Stiffness * Math.Max(0, u + theta * row.Z); n += f; m += f * row.Z; }
        foreach (var band in bands)
        {
            const int intervals = 40000;
            double dz = (band.UpperZ - band.LowerZ) / intervals;
            for (int i = 0; i < intervals; i++)
            {
                double z = band.LowerZ + (i + .5) * dz;
                double f = Math.Min(0, u + theta * z) * band.Stiffness / intervals;
                n += f; m += f * z;
            }
        }
        var r = EndPlateNmvEquilibrium.Solve(rows, bands, n, m);
        Assert.IsTrue(r.IsConverged); Assert.AreEqual(u, r.AxialOpening, 2e-9); Assert.AreEqual(theta, r.Rotation, 2e-11);
        Assert.AreEqual(n * r.AxialOpening + m * r.Rotation, 2 * r.ElasticEnergy, 1e-6);
        Assert.IsTrue(r.Tangent.IsPositiveDefinite);
    }

    [TestMethod]
    public void PartialContactHasTriangularPressureAndShiftedResultant()
    {
        // u=.11 mm, theta=.001 rad: only -120..-110 mm is compressed.
        // Triangle: peak15 N/mm, force75 N, centroid -116.6666667mm.
        double n = 1900 + 300 - 75;
        double m = 1900 * 80 - 300 * 80 + 75 * (120 - 10d / 3);
        var r = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), n, m);
        var c = r.Contacts[1];
        Assert.AreEqual(.11, r.AxialOpening, 1e-10); Assert.AreEqual(.001, r.Rotation, 1e-12);
        Assert.AreEqual(75, c.Compression, 1e-7); Assert.AreEqual(10, c.ClosedLength, 1e-7);
        Assert.AreEqual(-120 + 10d / 3, c.ResultantZ!.Value, 1e-7);
        Assert.AreEqual(15, c.MaximumCompressionPerLength, 1e-7);
    }

    [TestMethod]
    public void TangentInverseMatchesIndependentLoadPerturbationsWithCoupling()
    {
        var rows = Rows(true); var bands = Bands();
        var r = EndPlateNmvEquilibrium.Solve(rows, bands, 5000, 250000);
        var k = r.Tangent; double determinant = k.Axial * k.Rotation - k.Coupling * k.Coupling;
        Assert.IsFalse(r.IsAtContactSwitch); Assert.AreNotEqual(0, k.Coupling);
        const double dn = 1, dm = 100;
        var np = EndPlateNmvEquilibrium.Solve(rows, bands, 5000 + dn, 250000);
        var nm = EndPlateNmvEquilibrium.Solve(rows, bands, 5000 - dn, 250000);
        var mp = EndPlateNmvEquilibrium.Solve(rows, bands, 5000, 250000 + dm);
        var mm = EndPlateNmvEquilibrium.Solve(rows, bands, 5000, 250000 - dm);
        Assert.AreEqual(k.Rotation / determinant, (np.AxialOpening - nm.AxialOpening) / (2 * dn), 1e-9);
        Assert.AreEqual(-k.Coupling / determinant, (mp.AxialOpening - mm.AxialOpening) / (2 * dm), 1e-11);
        Assert.AreEqual(-k.Coupling / determinant, (np.Rotation - nm.Rotation) / (2 * dn), 1e-11);
        Assert.AreEqual(k.Axial / determinant, (mp.Rotation - mm.Rotation) / (2 * dm), 1e-13);
        Assert.AreEqual(1 / k.RotationAtConstantAxialForce!.Value, k.Axial / determinant, 1e-18);
    }

    [DataTestMethod][DataRow(-5000.0)][DataRow(0.0)][DataRow(5000.0)]
    public void ReversalAndProportionalScalingRespectSymmetryAndEnergy(double n)
    {
        var a = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), n, 400000);
        var b = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), n, -400000);
        var c = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), 3 * n, 1200000);
        Assert.AreEqual(a.AxialOpening, b.AxialOpening, 1e-9);
        Assert.AreEqual(a.Rotation, -b.Rotation, 1e-11);
        Assert.AreEqual(a.Rows[0].Tension, b.Rows[1].Tension, 1e-5);
        Assert.AreEqual(a.Contacts[0].Compression, b.Contacts[1].Compression, 1e-5);
        Assert.AreEqual(3 * a.AxialOpening, c.AxialOpening, 1e-9);
        Assert.AreEqual(3 * a.Rotation, c.Rotation, 1e-11);
        Assert.AreEqual(9 * a.ElasticEnergy, c.ElasticEnergy, 1e-5);
    }

    [TestMethod]
    public void ZeroLoadDoesNotInventAUniqueInitialContactTangent()
    {
        var r = EndPlateNmvEquilibrium.Solve(Rows(), Bands(), 0, 0);
        Assert.IsTrue(r.IsConverged); Assert.IsTrue(r.IsAtContactSwitch);
        Assert.AreEqual(0, r.ElasticEnergy); Assert.IsFalse(r.Tangent.IsPositiveDefinite);
        Assert.IsNull(r.Tangent.RotationAtConstantAxialForce);
    }

    [TestMethod]
    public void InvalidComponentsCannotEnterTheEquilibrium()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EndPlateNmvSpringRow("r", 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EndPlateNmvContactBand("c", 1, 1, 100));
        Assert.ThrowsException<ArgumentException>(() => EndPlateNmvEquilibrium.Solve(new[] { Rows()[0], Rows()[0] }, Bands(), 0, 0));
        Assert.ThrowsException<ArgumentException>(() => EndPlateNmvEquilibrium.Solve(Rows(), new[] { Bands()[0], new EndPlateNmvContactBand("overlap", 110, 130, 1000) }, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => EndPlateNmvEquilibrium.Solve(Rows(), Bands(), double.NaN, 0));
    }
}
