using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class LtbCoefficientRoutingTests
{
    public static IEnumerable<object[]> EndMoments
    {
        get
        {
            foreach (double k in new[] { 1d, .5 })
                foreach (double psi in new[] { -1d, -.75, -.5, -.25, 0, .25, .5, .75, 1 })
                    foreach (double psif in new[] { -1d, 1 })
                        yield return new object[] { k, psi, psif };
        }
    }

    [DataTestMethod, DynamicData(nameof(EndMoments))]
    public void PrescribedEndMomentsDoNotDependOnTheInPlaneSupportLabel(double k, double psi, double psif)
    {
        // Routing regression, not independent qualification of every table entry.
        var p = new Probe();
        var expected = p.Coefficients(Opt.SupportConditions.EndsRestrained, Opt.LoadConditions.NotDirectlyLoaded, k, psi, psif);
        var actual = p.Coefficients(Opt.SupportConditions.HingesAtEnds, Opt.LoadConditions.NotDirectlyLoaded, k, psi, psif);
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsTrue(actual[0] > 0);
    }

    [DataTestMethod]
    [DataRow(-1d)] [DataRow(0d)] [DataRow(1d)]
    public void UniformMomentHasUnitC1AndC3AndNoLoadHeightTerm(double psif)
    {
        // Exact uniform-moment eigenproblem with fork ends, k=kw=1:
        // the coefficients reduce to C1=C3=1, C2=0.
        var p = new Probe();
        CollectionAssert.AreEqual(new[] { 1d, 0d, 1d },
            p.Coefficients(Opt.SupportConditions.HingesAtEnds, Opt.LoadConditions.NotDirectlyLoaded, 1, 1, psif));
    }

    [DataTestMethod]
    [DataRow(.7, 1d)] [DataRow(1d, -1.01)] [DataRow(1d, 1.01)] [DataRow(1d, double.NaN)]
    public void UnsupportedEndMomentInputsCannotReturnZeroCoefficients(double k, double psi)
    {
        Assert.ThrowsException<Exception>(() => new Probe().Coefficients(
            Opt.SupportConditions.HingesAtEnds, Opt.LoadConditions.NotDirectlyLoaded, k, psi, 1));
    }

    [DataTestMethod]
    [DataRow(1d)] [DataRow(.5)]
    public void UnknownLoadCannotLeaveAllCoefficientsZero(double k)
    {
        Assert.ThrowsException<NotSupportedException>(() => new Probe().Coefficients(
            Opt.SupportConditions.HingesAtEnds, (Opt.LoadConditions)999, k, 1, 1));
    }

    private sealed class Probe : EN1993p11Checker
    {
        public Probe() : base(new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
            new SteelSection(new SectionT(400, 200, 40, 50, ""), new SteelMaterial("S355", 210000, 355, 510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11()) { }
        public double[] Coefficients(Opt.SupportConditions support, Opt.LoadConditions load, double k, double psi, double psif)
        {
            GetC1C2C3ForMcr(support, load, k, psi, psif, out double c1, out double c2, out double c3);
            return new[] { c1, c2, c3 };
        }
    }
}
