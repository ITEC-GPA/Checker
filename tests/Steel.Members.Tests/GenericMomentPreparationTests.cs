using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class GenericMomentPreparationTests
{
    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void GenericBoundDoesNotExceedIndependentSpectralBifurcation(int pattern)
    {
        double[] ordinates = pattern switch {
            0 => new[] { 1d, 1, 1, 1, 1 }, 1 => new[] { 1d, .5, 0, -.5, -1 },
            2 => new[] { 0d, .75, 1, .75, 0 }, 3 => new[] { .2, -.8, 1d, -.3, .5 },
            _ => new[] { 0d, 0, 1d, 0, 0 } };
        const double length = 6000; var s = MemberSchemePreparationTests.Section(); var model = s.CreateSectionCopy();
        var rows = ordinates.Select((m, i) => new SteelMomentStation(i * length / 4, m * 1e8)).ToArray();
        var diagram = SteelForkMomentDiagram.GenericEnvelope(length, rows, 0, "Same-case samples include every extremum; through shear centre");
        var result = SteelMemberSchemePreparation.ForkBending(s, new(length), diagram, "Fork, no intermediate restraint, free warping");
        // Independent numerical oracle: 12 sine terms for lateral deflection and
        // twist, energy coupling integral M(x)*theta*v'', evaluated by trapezoids.
        // Largest singular value of Kb^-1/2 B Kt^-1/2 gives reciprocal Mcr.
        // Interpolation here defines ONLY this oracle's synthetic test function;
        // production does not infer an actual FEM diagram between exported cuts.
        const int terms = 12, panels = 1600; double g = 210000 / (2 * (1 + s.PoissonRatio));
        var coupling = DenseMatrix.Create(terms, terms, 0);
        for (int i = 1; i <= terms; i++) for (int j = 1; j <= terms; j++)
        {
            double integral = 0;
            for (int k = 0; k <= panels; k++)
            {
                double x = (double)k / panels, p = x * 4; int left = Math.Min(3, (int)p); double a = p - left;
                double m = ordinates[left] * (1 - a) + ordinates[left + 1] * a;
                integral += (k == 0 || k == panels ? .5 : 1) * m * Math.Sin(i * Math.PI * x) * Math.Sin(j * Math.PI * x);
            }
            double ki = i * Math.PI / length, kj = j * Math.PI / length;
            double kb = length / 2 * 210000 * model.J22 * Math.Pow(ki, 4);
            double kt = length / 2 * (g * model.Jt * kj * kj + 210000 * model.Jw * Math.Pow(kj, 4));
            coupling[i - 1, j - 1] = -ki * ki * integral * length / panels / Math.Sqrt(kb * kt);
        }
        double spectral = 1 / coupling.Svd(false).S[0];
        Assert.IsTrue(result.UniformMoment.Value <= spectral * (1 + 2e-10), $"bound={result.UniformMoment.Value}; spectral={spectral}");
        if (pattern == 0) Assert.AreEqual(spectral, result.UniformMoment.Value, spectral * 2e-10);
        Assert.AreEqual(1d, diagram.C1); Assert.AreEqual(1d, diagram.EquivalentMomentFactor);
        Assert.AreEqual(rows.Length, diagram.Stations.Count); Assert.AreSame(rows[2], diagram.Stations[2]);
        Assert.AreEqual(rows.OrderByDescending(x => Math.Abs(x.Moment)).First().Moment, diagram.MaximumMoment);
        StringAssert.Contains(result.ActualDiagram.Source, "no actual-diagram eigenvalue claimed");
        Assert.ThrowsException<NotSupportedException>(() => diagram.MomentAt(.3));
    }

    [TestMethod]
    public void GenericDiagramCannotBeReusedOnADifferentMemberLength()
    {
        var diagram = SteelForkMomentDiagram.GenericEnvelope(2000,
            new[] { new SteelMomentStation(0, 1e8), new(2000, -1e8) }, 0, "All extrema");
        Assert.ThrowsException<ArgumentException>(() => SteelMemberSchemePreparation.ForkBending(
            MemberSchemePreparationTests.Section(), new SteelMemberInput(6000), diagram, "Full-span fork"));
    }

    [TestMethod]
    public void TwoFacesAndSignedInteriorMaximumArePreservedWithoutCancellation()
    {
        var d = SteelForkMomentDiagram.GenericEnvelope(2000, new[] { new SteelMomentStation(0, 0), new(800, -3e8), new(800, 2e8), new(2000, 1e8) }, 0, "Applied couple at800; both faces included");
        Assert.AreEqual(-3e8, d.MaximumMoment); Assert.AreEqual(4, d.Stations.Count);
        Assert.AreEqual(-3e8, d.Stations[1].Moment); Assert.AreEqual(2e8, d.Stations[2].Moment);
        var list = (IList<SteelMomentStation>)d.Stations; Assert.ThrowsException<NotSupportedException>(() => list.Clear());
    }

    [DataTestMethod] [DataRow("start")] [DataRow("end")] [DataRow("order")] [DataRow("source")] [DataRow("height")]
    public void IncompleteCoverageOrUnsupportedLoadingIsRejected(string problem)
    {
        var rows = new[] { new SteelMomentStation(problem == "start" ? 1 : 0, 1e6), new(problem == "order" ? 2001 : 1000, -2e6), new(problem == "end" ? 1999 : 2000, 1e6) };
        if (problem == "height") Assert.ThrowsException<NotSupportedException>(() => SteelForkMomentDiagram.GenericEnvelope(2000, rows, 1, "All extrema"));
        else Assert.ThrowsException<ArgumentException>(() => SteelForkMomentDiagram.GenericEnvelope(2000, rows, 0, problem == "source" ? "" : "All extrema"));
    }
}
