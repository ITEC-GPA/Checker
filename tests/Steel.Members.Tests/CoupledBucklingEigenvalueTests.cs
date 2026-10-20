using GPC.Geometry;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Materials;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class CoupledBucklingEigenvalueTests
{
    // Snijder/Balaz/Stroetmann, ce/papers8(2025),pp613-619,eq17,p618.
    // Independent generalized eigenproblem, common fork ends/free warping,
    // shared sine mode (coupling integrals alpha_yw=alpha_zw=1).
    public static IEnumerable<object[]> Cases =>
        from scale in new[] { 1e-8, 1d, 1e8 }
        from forces in new[] { new[] { 1d, 2d, 3d }, new[] { 3d, 1d, 2d }, new[] { 2d, 3d, 1d }, new[] { 1d, 1d, 1d } }
        from offset in new[] { new[] { 10d, 20d }, new[] { -10d, 20d }, new[] { 10d, 0d }, new[] { 0d, 20d } }
        select new object[] { scale, forces[0], forces[1], forces[2], offset[0], offset[1] };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void LowestRootMatchesSymmetricEigenproblem(double scale, double n1, double n2, double nt, double x, double y)
    {
        var s = new ReferenceSection(new SectionC(300, 10, 100, 16, 100, 16, ""))
        { PublishedRadius1 = 100, PublishedRadius2 = 30, Offset = new Point2d(x, y) };
        double i0 = Math.Sqrt(100 * 100 + 30 * 30 + x * x + y * y);
        n1 *= scale; n2 *= scale; nt *= scale;
        // K^-1/2 G K^-1/2, where K=diag(N1,N2,Nt), Gii=1.
        var a = DenseMatrix.OfArray(new[,] {
            { 1/n1, 0, x/i0/Math.Sqrt(n1)/Math.Sqrt(nt) },
            { 0, 1/n2, y/i0/Math.Sqrt(n2)/Math.Sqrt(nt) },
            { x/i0/Math.Sqrt(n1)/Math.Sqrt(nt), y/i0/Math.Sqrt(n2)/Math.Sqrt(nt), 1/nt } });
        double expected = 1 / a.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Max(v => v.Real);
        double actual = new ReferenceProbe(s).FlexuralTorsion(n1, n2, nt);
        Assert.IsTrue(double.IsFinite(actual) && actual > 0);
        Assert.AreEqual(expected, actual, expected * 1e-11);
    }

    [DataTestMethod]
    [DataRow(500d)] [DataRow(3000d)] [DataRow(12000d)]
    public void EqualAngleMatchesEigenproblemInGeometricAxes(double length)
    {
        var shape = new SectionL(100, 20, 100, 20, "");
        var s = new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510));
        var p = new ReferenceProbe(s, length);
        double k = Math.PI * Math.PI * 210000 / (length * length);
        // Independently integrated L=100x100x20: A=3600, centroid=(290/9,290/9).
        double ix = 28280000d / 9, ixy = -16000000d / 9;
        double delta = 10 - 290d / 9;
        double i0 = Math.Sqrt(2 * ix / 3600 + 2 * delta * delta);
        double nt = p.Torsion(); // torsional properties qualified separately by CISC.
        var stiffness = DenseMatrix.OfArray(new[,] { { k*ix, -k*ixy, 0 }, { -k*ixy, k*ix, 0 }, { 0, 0, nt } });
        var g = DenseMatrix.OfArray(new[,] { { 1d, 0, delta/i0 }, { 0, 1d, delta/i0 }, { delta/i0, delta/i0, 1d } });
        var eig = stiffness.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);
        var inv = eig.EigenVectors * DenseMatrix.OfDiagonalArray(eig.EigenValues.Select(v => 1/Math.Sqrt(v.Real)).ToArray()) * eig.EigenVectors.Transpose();
        var normalized = inv * g * inv;
        double expected = 1 / normalized.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Max(v => v.Real);
        double actual = p.FlexuralTorsion(k * s.J11, k * s.J22, nt);
        Assert.AreEqual(expected, actual, expected * 1e-10);
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidCriticalForceIsRejected(double n)
    {
        var s = new ReferenceSection(new SectionC(300, 10, 100, 16, 100, 16, ""))
        { PublishedRadius1 = 100, PublishedRadius2 = 30, Offset = new Point2d(10, 20) };
        var p = new ReferenceProbe(s);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => p.FlexuralTorsion(n, 2, 3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => p.FlexuralTorsion(1, n, 3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => p.FlexuralTorsion(1, 2, n));
    }
}
