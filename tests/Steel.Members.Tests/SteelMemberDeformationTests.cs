using GPC.Checkers.Steel.Serviceability;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Member deformations of EN 1993-1-1:2005 7.2 / EN 1990 A1.4 and NTC 2018 4.2.4.2 on exported points. Expected values are
/// closed forms written in the comments (Euler-Bernoulli beam, E = 210000 MPa), not library outputs.</summary>
[TestClass]
public class SteelMemberDeformationTests
{
    private const double E = 210000, I = 8.356e7; // IPE 300 major axis
    // Simply supported, uniform q: w(x) = q x (L^3 - 2 L x^2 + x^3) / (24 EI); w(L/2) = 5 q L^4 / (384 EI).
    private static double SimplySupported(double q, double l, double x) => q * x * (l * l * l - 2 * l * x * x + x * x * x) / (24 * E * I);
    private static SteelDisplacementPoint[] Points(Func<double, double> value, double l, int segments)
        => Enumerable.Range(0, segments + 1).Select(k => new SteelDisplacementPoint(l * k / segments, value(l * k / segments), "N" + k)).ToArray();

    [TestMethod]
    public void SimplySupportedUniformLoadDeflectsFiveOver384()
    {
        // q = 10 N/mm, L = 6000 mm: 5 x 10 x 6000^4 / (384 x 210000 x 8.356e7) = 6.48e16 / 6.7382784e15 = 9.61670 mm; L/250 = 24 mm, u = 0.40070.
        double l = 6000, expected = 5 * 10 * Math.Pow(l, 4) / (384 * E * I);
        Assert.AreEqual(9.61670, expected, 5e-6);
        var profile = SteelDeflectionProfile.Create(Points(x => -SimplySupported(10, l, x), l, 8), 0, l, SteelDeflectionScheme.SupportedEnds);
        Assert.AreEqual(-expected, profile.Governing.Value, 1e-12); Assert.AreEqual(3000, profile.Governing.Distance); Assert.AreEqual("N4", profile.Governing.Label);
        var check = SteelMemberDeformation.MaximumDeflection(profile, 0, new SteelDeformationLimit(250, l, "project agreement L/250"), "EN 1993-1-1 7.2.1");
        Assert.AreEqual(expected, check.Demand, 1e-12); Assert.AreEqual(24, check.Capacity, 1e-12);
        Assert.AreEqual(expected / 24, check.Utilization, 1e-12); Assert.IsTrue(check.Passed);
        Assert.AreEqual(3000d, check.Location); Assert.AreEqual("project agreement L/250", check.Source);
    }

    [TestMethod]
    public void TheChordRemovesSupportSettlementsAndTheSpanCanBePartial()
    {
        // Settlements -4 mm at x = 1000 and -10 mm at x = 7000 (span 6000 between them): a rigid-body line, removed by the chord.
        double l = 6000, expected = 5 * 10 * Math.Pow(l, 4) / (384 * E * I);
        double Settled(double x) => -4 + (-10 + 4) * (x - 1000) / l - SimplySupported(10, l, x - 1000);
        var points = Points(x => Settled(x + 1000), l, 8).Select(p => new SteelDisplacementPoint(p.Distance + 1000, p.Value, p.Label));
        var profile = SteelDeflectionProfile.Create(points, 1000, 7000, SteelDeflectionScheme.SupportedEnds);
        Assert.AreEqual(-expected, profile.Governing.Value, 1e-11); Assert.AreEqual(4000, profile.Governing.Distance);
        Assert.AreEqual(-4, profile.ReferenceAtStart, 1e-12); Assert.AreEqual(-10, profile.ReferenceAtEnd, 1e-12);
        Assert.AreEqual(0, profile.Relative[0].Value, 1e-12); Assert.AreEqual(0, profile.Relative[8].Value, 1e-12);
    }

    [TestMethod]
    public void CantileverTipIsRelativeToTheSupportTranslation()
    {
        // Cantilever q = 5 N/mm, Lc = 2000: tip q L^4 / (8 EI) = 5 x 1.6e13 / (8 x 210000 x 8.356e7) = 0.56988 mm; support settles 3 mm.
        double l = 2000, tip = 5 * Math.Pow(l, 4) / (8 * E * I);
        Assert.AreEqual(.56988, tip, 5e-6);
        // w(x) = q x^2 (6 L^2 - 4 L x + x^2) / (24 EI)
        double W(double x) => 5 * x * x * (6 * l * l - 4 * l * x + x * x) / (24 * E * I);
        var atStart = SteelDeflectionProfile.Create(Points(x => -3 - W(x), l, 4), 0, l, SteelDeflectionScheme.CantileverAtStart);
        Assert.AreEqual(-tip, atStart.Governing.Value, 1e-12); Assert.AreEqual(l, atStart.Governing.Distance);
        // Reference length declared as twice the cantilever length: limit 2 x 2000 / 250 = 16 mm.
        var check = SteelMemberDeformation.MaximumDeflection(atStart, 0, new SteelDeformationLimit(250, 2 * l, "declared 2 Lc"), "NTC 4.2.4.2.1");
        Assert.AreEqual(16, check.Capacity, 1e-12); Assert.AreEqual(tip / 16, check.Utilization, 1e-12);
        // The same cantilever described from its free end.
        var atEnd = SteelDeflectionProfile.Create(Points(x => -3 - W(l - x), l, 4), 0, l, SteelDeflectionScheme.CantileverAtEnd);
        Assert.AreEqual(-tip, atEnd.Governing.Value, 1e-12); Assert.AreEqual(0, atEnd.Governing.Distance);
    }

    [TestMethod]
    public void PrecamberAndVariableDeflectionFollowEn1990A143()
    {
        // Total q = 10 (G 6 + Q 4); precamber 5 mm upward: w_max = |-9.61670 + 5| = 4.61670; w3 = 4/10 x 9.61670 = 3.84668 mm, L/300 = 20.
        double l = 6000, total = 5 * 10 * Math.Pow(l, 4) / (384 * E * I);
        var all = SteelDeflectionProfile.Create(Points(x => -SimplySupported(10, l, x), l, 8), 0, l, SteelDeflectionScheme.SupportedEnds);
        var permanent = SteelDeflectionProfile.Create(Points(x => -SimplySupported(6, l, x), l, 8), 0, l, SteelDeflectionScheme.SupportedEnds);
        var max = SteelMemberDeformation.MaximumDeflection(all, 5, new SteelDeformationLimit(250, l, "s"), "c");
        Assert.AreEqual(total - 5, max.Demand, 1e-12);
        var variable = SteelMemberDeformation.VariableDeflection(all.Minus(permanent), new SteelDeformationLimit(300, l, "s2"), "c");
        Assert.AreEqual(.4 * total, variable.Demand, 1e-12); Assert.AreEqual(20, variable.Capacity, 1e-12); Assert.AreEqual(3000d, variable.Location);
        // An over-cambered member: the residual hog counts as a deflection.
        Assert.AreEqual(20 - total, SteelMemberDeformation.MaximumDeflection(all, 20, new SteelDeformationLimit(250, l, "s"), "c").Demand, 1e-12);
    }

    [TestMethod]
    public void DriftIsTheRelativeDisplacementOfTheSpanEnds()
    {
        // Column H = 3500 fixed at the base, P = 10 kN at the top: Delta = P H^3 / (3 EI) = 4.2875e14 / 5.26428e13 = 8.14451 mm;
        // the base translates 2 mm. h/300 = 11.66667 mm, u = 0.69810.
        double h = 3500, delta = 1e4 * Math.Pow(h, 3) / (3 * E * I);
        Assert.AreEqual(8.14451, delta, 5e-6);
        var one = SteelMemberDeformation.RelativeDisplacement(2, 2 + delta, null, null, new SteelDeformationLimit(300, h, "declared h/300"), "EC3 7.2.2");
        Assert.AreEqual(delta, one.Demand, 1e-12); Assert.AreEqual(h / 300, one.Capacity, 1e-12); Assert.IsNull(one.Location);
        // Both transverse components: magnitude (3, 4) -> 5.
        Assert.AreEqual(5, SteelMemberDeformation.RelativeDisplacement(1, 4, -1, 3, new SteelDeformationLimit(300, h, "s"), "c").Demand, 1e-12);
    }

    [TestMethod]
    public void NaturalFrequencyOfTheSimplySupportedMemberWithUniformMass()
    {
        // f = (pi/2) sqrt(EI / (m L^4)) and delta = 5 m g L^4 / (384 EI) give f = (pi/2) sqrt(5 g / 384) / sqrt(delta):
        // with g = 9806.65 mm/s^2 the constant is 17.75005, the 17.75 of the formula.
        Assert.AreEqual(17.75, Math.PI / 2 * Math.Sqrt(5 * 9806.65 / 384), 1e-4);
        // Mass 4 N/mm (as weight), L = 6000: delta = 0.4 x 9.61670 = 3.84668 mm, f = 17.75 / 1.961296 = 9.05014 Hz.
        double l = 6000, q = 4, delta = 5 * q * Math.Pow(l, 4) / (384 * E * I);
        double f = SteelMemberDeformation.NaturalFrequency(delta, SteelMemberDeformation.SimplySupportedFrequencyCoefficient);
        Assert.AreEqual(9.05014, f, 5e-6);
        double exact = Math.PI / 2 * Math.Sqrt(E * I * 9806.65 / (q * Math.Pow(l, 4)));
        Assert.AreEqual(exact, f, 1e-5 * exact);
        var check = SteelMemberDeformation.Frequency(f, 3, 3000, "N4", "EC3 7.2.3", "declared 3 Hz");
        Assert.AreEqual(3, check.Demand); Assert.AreEqual(f, check.Capacity); Assert.AreEqual(3 / f, check.Utilization, 1e-15); Assert.IsTrue(check.Passed);
        Assert.IsFalse(SteelMemberDeformation.Frequency(f, 10, 3000, "N4", "c", "s").Passed);
    }

    [TestMethod]
    public void IncompleteOrInconsistentPointsAreRefused()
    {
        double l = 6000;
        var ends = new[] { new SteelDisplacementPoint(0, 0, "A"), new SteelDisplacementPoint(l, 0, "B") };
        // A supported span with only its end points would give zero by construction.
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => SteelDeflectionProfile.Create(ends, 0, l, SteelDeflectionScheme.SupportedEnds)).Message, "InteriorDisplacementPointRequired");
        Assert.AreEqual(0, SteelDeflectionProfile.Create(ends, 0, l, SteelDeflectionScheme.CantileverAtStart).Governing.Value);
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => SteelDeflectionProfile.Create(ends.Take(1).Append(new SteelDisplacementPoint(3000, -1, "M")), 0, l,
            SteelDeflectionScheme.SupportedEnds)).Message, "DisplacementPointsRequiredAtBothSpanEnds");
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => SteelDeflectionProfile.Create(ends.Reverse(), 0, l,
            SteelDeflectionScheme.SupportedEnds)).Message, "DisplacementPointsMustIncreaseAlongTheMember");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelDisplacementPoint(0, double.NaN, "A"));
        Assert.ThrowsException<ArgumentException>(() => new SteelDeformationLimit(250, l, " "));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelDeformationLimit(0, l, "s"));
        Assert.ThrowsException<ArgumentException>(() => SteelMemberDeformation.NaturalFrequency(0, 17.75));
        var three = Points(x => -x * (l - x) * 1e-6, l, 2);
        var other = SteelDeflectionProfile.Create(Points(x => 0, l, 4), 0, l, SteelDeflectionScheme.SupportedEnds);
        Assert.ThrowsException<ArgumentException>(() => SteelDeflectionProfile.Create(three, 0, l, SteelDeflectionScheme.SupportedEnds).Minus(other));
    }
}
