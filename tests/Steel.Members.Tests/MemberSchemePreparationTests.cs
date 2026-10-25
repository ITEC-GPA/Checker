using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class MemberSchemePreparationTests
{
    internal static SteelSectionInput Section(bool slim = false)
        => new(new SteelSection(new SectionH(slim ? 1000 : 600, slim ? 5 : 12, 300, 20, 300, 20, "Welded H"),
            new SteelMaterial("S355", 210000, 355, 510), GPC.Model.Sections.Section.SectionTypes.Welded,
            GPC.Model.Sections.Section.FormedTypes.HotFinished), true);

    [DataTestMethod]
    [DataRow(SteelIdealFlexuralBoundary.PinnedPinned, 1d)]
    [DataRow(SteelIdealFlexuralBoundary.FixedFixed, 4d)]
    [DataRow(SteelIdealFlexuralBoundary.Cantilever, .25)]
    public void IdealFlexuralModesMatchIndependentRectangleIntegrals(SteelIdealFlexuralBoundary boundary, double multiplier)
    {
        var r = SteelMemberSchemePreparation.FlexuralModes(Section(), new(5000, boundary, "Declared axis1"), new(4000, boundary, "Declared axis2"));
        // Independently integrate two300x20 flanges and the560x12 web.
        double i1 = 2 * (300 * Math.Pow(20, 3) / 12 + 300 * 20 * 290 * 290) + 12 * Math.Pow(560, 3) / 12;
        double i2 = 2 * 20 * Math.Pow(300, 3) / 12 + 560 * Math.Pow(12, 3) / 12;
        Assert.AreEqual(210000 * i1 * Math.Pow(Math.PI / 5000, 2) * multiplier, r[0].Value, r[0].Value * 1e-12);
        Assert.AreEqual(210000 * i2 * Math.Pow(Math.PI / 4000, 2) * multiplier, r[1].Value, r[1].Value * 1e-12);
        Assert.IsTrue(r.All(x => x.Quantity != SteelStabilityQuantity.NcrT && x.Quantity != SteelStabilityQuantity.NcrFT));
        Assert.IsTrue(r.All(x => x.Source.Contains("torsional modes not assessed")));
    }

    [TestMethod]
    public void FixedPinnedEigenvalueSatisfiesIndependentBoundaryDeterminantRoot()
    {
        // Characteristic equation sin(q)-q*cos(q)=0 from the four ideal boundary
        // conditions; bisection independently recovers the first nonzero root.
        double lo = Math.PI + .1, hi = 1.5 * Math.PI - .01;
        for (int j = 0; j < 70; j++) { double q = (lo + hi) / 2; if (Math.Sin(q) - q * Math.Cos(q) > 0) lo = q; else hi = q; }
        var span = new SteelIdealFlexuralSpan(5000, SteelIdealFlexuralBoundary.FixedPinned, "Ideal no-sway fixed/pinned isolated column");
        Assert.AreEqual(Math.PI / ((lo + hi) / 2), span.EffectiveLengthFactor, 2e-14);
        Assert.AreEqual(3495.778298214073, span.EffectiveLength, 1e-8);
    }

    [DataTestMethod]
    [DataRow(1d, 1d, 1d)] [DataRow(.75, 1.14, .9)] [DataRow(.5, 1.31, .8)]
    [DataRow(.25, 1.52, .7)] [DataRow(0d, 1.77, .6)] [DataRow(-.25, 2.05, .5)]
    [DataRow(-.5, 2.33, .4)] [DataRow(-.75, 2.57, .4)] [DataRow(-1d, 2.55, .4)]
    public void PublishedSn003bTableAndIndependentNormativeCmStayDistinct(double psi, double c1, double cm)
    {
        foreach (double sign in new[] { -1d, 1d }) foreach (bool reverse in new[] { false, true })
        {
            double a = sign * 1e8, b = psi * a;
            var d = SteelForkMomentDiagram.EndMoments(reverse ? b : a, reverse ? a : b, "SN003b Table3.1, end moments; common section-face signs");
            Assert.AreEqual(c1, d.C1, 1e-14); Assert.AreEqual(cm, d.EquivalentMomentFactor, 1e-14);
            Assert.IsFalse(d.UsesConservativeUniformC1); Assert.AreEqual(0, d.C2);
            Assert.AreEqual((a + b) / 2, d.MomentAt(.5), 1e-7);
        }
    }

    [TestMethod]
    public void OffTableRatioExplicitlyFallsBackWithoutInventedInterpolation()
    {
        var d = SteelForkMomentDiagram.EndMoments(1e8, .63e8, "Linear physical diagram");
        Assert.IsTrue(d.UsesConservativeUniformC1); Assert.AreEqual(1d, d.C1);
        Assert.AreEqual(.852, d.EquivalentMomentFactor, 1e-14);
        StringAssert.Contains(SteelMemberSchemePreparation.ForkBending(Section(), new(6000), d, "Forks free warping").ActualDiagram.Source, "off-table");
    }

    [DataTestMethod] [DataRow(false, -300d)] [DataRow(false, 0d)] [DataRow(false, 300d)]
    [DataRow(true, -300d)] [DataRow(true, 0d)] [DataRow(true, 300d)]
    public void TransverseLoadUsesPublishedCoefficientsAndSatisfiesIndependentQuadratic(bool point, double height)
    {
        var d = point ? SteelForkMomentDiagram.CentralPointLoad(1e8, height, "Central point force, ends pinned")
            : SteelForkMomentDiagram.UniformTransverseLoad(1e8, height, "Uniform load, ends pinned");
        var s = Section(); var model = s.CreateSectionCopy();
        var r = SteelMemberSchemePreparation.ForkBending(s, new(6000), d, "Forks/free warping over whole span");
        Assert.AreEqual(point ? 1.348 : 1.127, d.C1); Assert.AreEqual(point ? .630 : .454, d.C2);
        Assert.AreEqual(point ? .9 : .95, d.EquivalentMomentFactor);
        Assert.AreEqual(point ? .5e8 : .75e8, d.MomentAt(.25), 1e-7);
        // Different oracle: solve the positive root of x²+2*z*C2*x-B=0
        // by bracket bisection, rather than evaluating the production square root.
        double q = Math.Pow(Math.PI / 6000, 2) * 210000 * model.J22;
        double b = model.Jw / model.J22 + 210000 / (2 * (1 + s.PoissonRatio)) * model.Jt / q;
        double lo = 0, hi = Math.Sqrt(b) + 2 * Math.Abs(d.C2 * height) + 1;
        for (int j = 0; j < 100; j++) { double x = (lo + hi) / 2; if (x * x + 2 * d.C2 * height * x - b > 0) hi = x; else lo = x; }
        double expected = d.C1 * q * (lo + hi) / 2;
        Assert.AreEqual(expected, r.ActualDiagram.Value, expected * 2e-12);
        if (height > 0) Assert.IsTrue(r.ActualDiagram.Value < d.C1 * r.UniformMoment.Value);
        if (height < 0) Assert.IsTrue(r.ActualDiagram.Value > d.C1 * r.UniformMoment.Value);
        if (height == 0) Assert.AreSame(r.UniformMoment, r.ForCode(SectionResistanceStandard.Ntc2018));
        else Assert.ThrowsException<NotSupportedException>(() => r.ForCode(SectionResistanceStandard.Ntc2018));
        Assert.AreSame(r.ActualDiagram, r.ForCode(SectionResistanceStandard.Eurocode3_2005));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void GrossUniformReferenceMatchesExistingEngineIncludingSlenderSections(bool slim)
    {
        var s = Section(slim); var member = new SteelMemberInput(6000); var d = SteelForkMomentDiagram.EndMoments(1e6, 1e6, "Uniform moment");
        var prepared = SteelMemberSchemePreparation.ForkBending(s, member, d, "Fork");
        var input = new SteelVerificationInput(s, new(0, 0, 0, 0, 1e6, 0), SteelDesignContext.Ntc2018(), member);
        double old = SteelAutomaticStability.UniformFork(input, true, "Fork").Single().Value;
        Assert.AreEqual(old, prepared.UniformMoment.Value, old * 2e-12);
        Assert.AreEqual(s.CreateSectionCopy().J22, prepared.Trace.Single(x => x.Name == "IminorGross").Value);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BoundAnalysisRetainsActualMaximumAndCorrectNormativeBasis(bool ntc)
    {
        var s = Section(); var member = new SteelMemberInput(6000); var d = SteelForkMomentDiagram.EndMoments(-1e6, 0, "Actual linear diagram");
        var r = SteelMemberSchemePreparation.ForkBending(s, member, d, "Fork");
        var code = ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "Test"), 1);
        var input = new SteelVerificationInput(s, new(0, 0, 0, 0, -1e6, 0), code, member);
        var a = r.CreateBendingAnalysis(input, "Test");
        Assert.AreEqual(ntc ? SteelCriticalMomentBasis.UniformMoment : SteelCriticalMomentBasis.ActualMomentDiagram, a.Prismatic.CriticalMomentBasis);
        Assert.AreEqual(ntc ? r.UniformMoment.Value : r.ActualDiagram.Value, a.Values.Single().Value);
        Assert.AreSame(input, a.Input);
        Assert.ThrowsException<ArgumentException>(() => r.CreateBendingAnalysis(new(s, input.Actions, code, new SteelMemberInput(6000)), "Test"));
        Assert.ThrowsException<ArgumentException>(() => r.CreateBendingAnalysis(new(s, new(0, 0, 0, 0, -2e6, 0), code, member), "Test"));
    }

    [DataTestMethod] [DataRow("length")] [DataRow("warping")] [DataRow("rotation")] [DataRow("translation")] [DataRow("height")] [DataRow("source")]
    public void ContradictoryRestraintsAndUnsupportedLoadGeometryAreRejected(string problem)
    {
        var s = Section(); var member = new SteelMemberInput(6000, bucklingLength2: problem == "length" ? 3000 : null,
            endRestraints: new SteelEndRestraints(translation2: problem == "translation" ? SteelRestraintState.Free : SteelRestraintState.Unknown,
                rotation3: problem == "rotation" ? SteelRestraintState.Free : SteelRestraintState.Unknown,
                warping: problem == "warping" ? SteelRestraintState.Fixed : SteelRestraintState.Unknown));
        var d = SteelForkMomentDiagram.UniformTransverseLoad(1e8, problem == "height" ? 301 : 0, "Actual UDL");
        if (problem == "height") Assert.ThrowsException<NotSupportedException>(() => SteelMemberSchemePreparation.ForkBending(s, member, d, "Fork"));
        else Assert.ThrowsException<ArgumentException>(() => SteelMemberSchemePreparation.ForkBending(s, member, d, problem == "source" ? "" : "Fork"));
    }

    [TestMethod]
    public void InvalidAndInactiveDiagramsDoNotCreateCriticalValues()
    {
        Assert.ThrowsException<ArgumentException>(() => SteelForkMomentDiagram.EndMoments(0, 0, "inactive"));
        Assert.ThrowsException<ArgumentException>(() => SteelForkMomentDiagram.CentralPointLoad(0, 0, "inactive"));
        Assert.ThrowsException<ArgumentException>(() => SteelForkMomentDiagram.EndMoments(1, 1, ""));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SteelForkMomentDiagram.EndMoments(double.NaN, 1, "bad"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelIdealFlexuralSpan(-1, SteelIdealFlexuralBoundary.PinnedPinned, "bad"));
    }
}
