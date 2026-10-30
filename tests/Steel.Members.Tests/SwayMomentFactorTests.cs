using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>EN 1993-1-1:2005 Annex B Table B.3 note: Cm=0.9 in the plane of a sway buckling mode. NTC: Circolare 2019
/// C4.2.4.1.3.3.2, text after Table C4.2.V, alpha_my or alpha_mz = 0.9 (checked on the GU text, 30/10/2026).</summary>
[TestClass]
public class SwayMomentFactorTests
{
    [DataTestMethod]
    [DataRow(SteelIdealFlexuralBoundary.PinnedPinned, false)] [DataRow(SteelIdealFlexuralBoundary.FixedPinned, false)]
    [DataRow(SteelIdealFlexuralBoundary.FixedFixed, false)] [DataRow(SteelIdealFlexuralBoundary.Cantilever, true)]
    public void OnlyTheCantileverIdealizationCarriesTheSwayFactor(SteelIdealFlexuralBoundary boundary, bool sway)
    {
        var span = new SteelIdealFlexuralSpan(3000, boundary, "Declared isolated column");
        Assert.AreEqual(sway, span.IsSwayMode);
        if (sway) Assert.AreEqual(.9, SteelSwayMomentFactor.For(span));
        else StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(() => SteelSwayMomentFactor.For(span)).Message, "nonsway");
        Assert.ThrowsException<ArgumentNullException>(() => SteelSwayMomentFactor.For(null!));
    }

    [TestMethod]
    public void CantileverNcrAndSwayFactorDescribeTheSamePlane()
    {
        // Ideal cantilever L=3000: Leff=2L, Ncr1=pi^2 E I1/(2L)^2 with the independent rectangle integral of the 600x300 welded H.
        var span = new SteelIdealFlexuralSpan(3000, SteelIdealFlexuralBoundary.Cantilever, "Free top, fixed base, plane 1");
        var r = SteelMemberSchemePreparation.FlexuralModes(MemberSchemePreparationTests.Section(), span,
            new SteelIdealFlexuralSpan(3000, SteelIdealFlexuralBoundary.PinnedPinned, "Braced plane 2"));
        double i1 = 2 * (300 * Math.Pow(20, 3) / 12 + 300 * 20 * 290 * 290) + 12 * Math.Pow(560, 3) / 12;
        Assert.AreEqual(Math.PI * Math.PI * 210000 * i1 / (6000 * 6000), r[0].Value, r[0].Value * 1e-12);
        Assert.AreEqual(SteelSwayMomentFactor.Value, SteelSwayMomentFactor.For(span));
    }

    [TestMethod]
    public void ReferencesNameTheClauseOfEachCode()
    {
        StringAssert.Contains(SteelSwayMomentFactor.Reference(SectionResistanceStandard.Eurocode3_2005), "TableB.3 note");
        string ntc = SteelSwayMomentFactor.Reference(SectionResistanceStandard.Ntc2018);
        StringAssert.Contains(ntc, "TableC4.2.VI"); StringAssert.Contains(ntc, "C4.2.4.1.3.3.2"); StringAssert.Contains(ntc, "text after TableC4.2.V");
        Assert.IsFalse(ntc.Contains("GPC assumption")); // verified on the Circolare text, no longer an assumption
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SwayFactorEntersAnnexBTablesB1AndC42IVByHandSubstitution(bool ntc)
    {
        // n1=.2, n2=.4, m1=.3, m2=.1, lambda1=.6, lambda2=1.2, class1, torsion restrained; Cm1=0.9 (sway plane 1),
        // Cm2=CmLT=.6 (linear psi=0). kyy=.9(1+.4*.2)=.972, kzz=.6(1+1.4*.4)=.936 (NTC envelope .6(1+.6*.2)=.672 smaller),
        // kyz=.6*.936=.5616, kzy=.6*.972=.5832; u1=.2+.972*.3+.5616*.1=.54776, u2=.4+.5832*.3+.936*.1=.66856.
        var r = BeamColumnInteraction.Evaluate(.2, .4, .3, .1, .6, 1.2, 1, false, SteelSwayMomentFactor.Value, .6, .6, ntc);
        Assert.AreEqual(.972, r.K11, 1e-12); Assert.AreEqual(.936, r.K22, 1e-12);
        Assert.AreEqual(.5616, r.K12, 1e-12); Assert.AreEqual(.5832, r.K21, 1e-12);
        Assert.AreEqual(.54776, r.Utilization1, 1e-12); Assert.AreEqual(.66856, r.Utilization2, 1e-12);
    }
}
