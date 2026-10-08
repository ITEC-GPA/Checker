using GPC.Checkers.Concrete.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>Boundaries, combinations and invalid data of the durability rules (exposure classes, covers of NTC, EN/UNI and DS).</summary>
    [TestClass]
    public class DurabilityEdgeCaseTests
    {
        private static CoverInput Input(string[] exposures, double fck = 30, int life = 50, bool reduction = false, bool slab = false, bool quality = false, double diameter = 16,
            double aggregate = 20, double deviation = 10, bool rough = false, int abrasion = 0, int ground = 0, bool plate = false, bool ntcQuality = false, double? cmin = null)
            => new CoverInput(exposures, fck, life, reduction, slab, quality, diameter, aggregate, deviation, rough, abrasion, ground, plate, ntcQuality, cmin);
        private static CoverResult En(CoverInput p) => CoverRequirements.Calculate(DurabilityProfile.EN1992p11, p);
        private static CoverResult Ntc(CoverInput p) => CoverRequirements.Calculate(DurabilityProfile.Ntc2018, p);
        private static CoverResult Ds(CoverInput p) => CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, p);
        private static readonly string[] Xc3 = { "XC3" };

        [TestMethod]
        public void StrengthReductionAppliesFromTheThresholdOfEachExposure()
        {
            // Table 4.3N: the class is reduced when fck reaches C30/37 (X0, XC1), C35/45 (XC2, XC3), C40/50 (XC4, XD1, XD2, XS1), C45/55 (others).
            foreach (var (code, threshold) in new[] { ("XC1", 30.0), ("XC3", 35.0), ("XC4", 40.0), ("XD2", 40.0), ("XS1", 40.0), ("XD3", 45.0), ("XS2", 45.0), ("XS3", 45.0) })
            {
                var e = ExposureClasses.Get(code);
                Assert.AreEqual(4, CoverRequirements.StructuralClass(e, Input(new[] { code }, fck: threshold - .5, reduction: true)), code + " just below the threshold");
                Assert.AreEqual(3, CoverRequirements.StructuralClass(e, Input(new[] { code }, fck: threshold, reduction: true)), code + " at the threshold");
                Assert.AreEqual(4, CoverRequirements.StructuralClass(e, Input(new[] { code }, fck: threshold + 10)), code + " without the reduction flag");
            }
        }

        [TestMethod]
        public void StructuralClassStaysBetweenS1AndS6()
        {
            var xc3 = ExposureClasses.Get("XC3");
            Assert.AreEqual(1, CoverRequirements.StructuralClass(xc3, Input(Xc3, fck: 50, reduction: true, slab: true, quality: true)), "4 − 3 = S1");
            Assert.AreEqual(3, CoverRequirements.StructuralClass(xc3, Input(Xc3, fck: 50, life: 100, reduction: true, slab: true, quality: true)), "4 + 2 − 3 = S3");
            Assert.AreEqual(6, CoverRequirements.StructuralClass(xc3, Input(Xc3, life: 100)), "4 + 2 = S6");
            // Table 4.4N corners: S1/X0 = 10 mm, S6/XD3 = 55 mm.
            Assert.AreEqual(10, En(Input(new[] { "X0" }, fck: 50, reduction: true, slab: true, quality: true, diameter: 8)).Durability);
            Assert.AreEqual(55, En(Input(new[] { "XD3" }, life: 100)).Durability);
        }

        [TestMethod]
        public void TheMostSevereExposureOfACombinationGoverns()
        {
            var r = En(Input(new[] { "XC4", "XS3", "XF4" }, fck: 35));
            Assert.AreEqual(2, r.Lines.Count, "XF4 has no corrosion column"); Assert.AreEqual(45, r.Durability, "XS3, S4");
            Assert.AreEqual(45, r.Lines.Single(l => l.Exposure == "XS3").Durability); Assert.AreEqual(30, r.Lines.Single(l => l.Exposure == "XC4").Durability);
            Assert.AreEqual(2, Ntc(Input(new[] { "XC3", "XF4" })).NtcEnvironment, "XF4 is very aggressive for NTC");
            Assert.AreEqual(40, Ds(Input(new[] { "XC3", "XD3" })).Durability);
        }

        [TestMethod]
        public void BondAggregateSurfaceAbrasionAndGroundAreApplied()
        {
            Assert.AreEqual(32, En(Input(Xc3, diameter: 32, aggregate: 32)).Bond, "dg = 32: no addition");
            Assert.AreEqual(37, En(Input(Xc3, diameter: 32, aggregate: 32.5)).Bond, "dg > 32: +5 mm");
            Assert.AreEqual(37, En(Input(Xc3, diameter: 32, aggregate: 40)).Minimum, "cmin,b governs over cmin,dur = 25");
            Assert.AreEqual(25 + 5 + 15, En(Input(Xc3, rough: true, abrasion: 15)).Minimum, "rough surface + k3");
            Assert.AreEqual(75, En(Input(Xc3, ground: 75)).Nominal, "cast directly against soil");
            Assert.AreEqual(40, En(Input(Xc3, ground: 40, deviation: 5)).Nominal, "prepared ground governs over 25 + 5");
            Assert.AreEqual(45, En(Input(Xc3, ground: 40, deviation: 20)).Nominal, "cmin + Δcdev governs over the ground value");
            Assert.AreEqual(10, En(Input(new[] { "X0" }, diameter: 6, aggregate: 10, deviation: 0)).Minimum, "absolute minimum 10 mm");
        }

        [TestMethod]
        public void NtcTableBoundariesOfCminAndC0()
        {
            // Ordinary environment: Cmin = 25, C0 = 35 MPa (defaults); below C0 +5 mm, below Cmin another +5 mm.
            Assert.AreEqual(20, Ntc(Input(Xc3, fck: 35)).NtcTable, "fck = C0: base value");
            Assert.AreEqual(25, Ntc(Input(Xc3, fck: 34.9)).NtcTable, "just below C0");
            Assert.AreEqual(0, Ntc(Input(Xc3, fck: 25)).NtcLowStrengthExtra, "fck = Cmin: no low-strength addition");
            Assert.AreEqual(5, Ntc(Input(Xc3, fck: 24.9)).NtcLowStrengthExtra, "below Cmin");
            Assert.AreEqual(30, Ntc(Input(Xc3, fck: 24.9)).Durability, "20 + 5 + 5");
            // Aggressive and very aggressive environments; plates −5 mm; quality control −5 mm; 100 years +10 mm.
            Assert.AreEqual(30 + 5, Ntc(Input(new[] { "XD1" }, fck: 30)).Durability); Assert.AreEqual(40, Ntc(Input(new[] { "XD3" }, fck: 45)).Durability);
            Assert.AreEqual(35, Ntc(Input(new[] { "XD3" }, fck: 45, plate: true)).Durability);
            Assert.AreEqual(20, Ntc(Input(Xc3, fck: 30, ntcQuality: true)).Durability, "25 − 5");
            Assert.AreEqual(35, Ntc(Input(Xc3, fck: 30, life: 100)).Durability, "25 + 10");
            // A pertinent Cmin (UNI 11104) replaces the default; it must lie between 12 MPa and C0.
            Assert.AreEqual(5, Ntc(Input(Xc3, fck: 28, cmin: 30)).NtcLowStrengthExtra);
            Assert.ThrowsException<ArgumentException>(() => Ntc(Input(Xc3, cmin: 11)));
            Assert.ThrowsException<ArgumentException>(() => Ntc(Input(Xc3, cmin: 36)), "Cmin above C0 = 35");
        }

        [TestMethod]
        public void DanishTableIgnoresStructuralClassModifiers()
        {
            var plain = Ds(Input(Xc3)); var modified = Ds(Input(Xc3, fck: 50, reduction: true, slab: true, quality: true));
            Assert.AreEqual(plain.Durability, modified.Durability, "DK NA 4.4.1.2(5): structural classes are not used");
            Assert.IsNull(plain.Lines.Single().StructuralClass);
            Assert.AreEqual(25, Ds(Input(Xc3, deviation: 5)).Nominal, "Δcdev = 5 mm accepted");
            Assert.ThrowsException<ArgumentException>(() => Ds(Input(Xc3, deviation: 4.99)));
            Assert.ThrowsException<ArgumentException>(() => Ds(Input(new[] { "XF3" })), "XF alone does not define cmin,dur");
        }

        [TestMethod]
        public void InvalidDataAreRejected()
        {
            foreach (var p in new[]
            {
                Input(Xc3, fck: 11.9), Input(Xc3, fck: 90.1), Input(Xc3, fck: double.NaN), Input(Xc3, life: 75), Input(Xc3, diameter: 0), Input(Xc3, diameter: -8),
                Input(Xc3, aggregate: 0), Input(Xc3, deviation: -1), Input(Xc3, deviation: double.PositiveInfinity), Input(Xc3, abrasion: 7), Input(Xc3, ground: 50),
                Input(new string[0]), Input(new[] { "X0", "XC1" }), Input(new[] { "XC9" }), Input(new[] { "xc3" })
            })
            {
                Assert.ThrowsException<ArgumentException>(() => En(p), string.Join("+", p.Exposures) + " " + p.Fck + " " + p.DesignLife);
                Assert.ThrowsException<ArgumentException>(() => Ntc(p), string.Join("+", p.Exposures) + " NTC");
            }
            Assert.ThrowsException<ArgumentException>(() => En(Input(new[] { "XA2" })), "XA alone does not define cmin,dur in EC2");
            Assert.AreEqual(35, Ntc(Input(new[] { "XA2" })).Durability, "NTC: XA2 is aggressive");
            Assert.ThrowsException<ArgumentNullException>(() => CoverRequirements.Calculate(DurabilityProfile.EN1992p11, null));
            Assert.ThrowsException<ArgumentNullException>(() => new CoverInput(null, 30, 50, false, false, false, 16, 20, 10));
        }

        [TestMethod]
        public void MinimumStrengthDependsOnTheProfile()
        {
            StrengthRequirement S(DurabilityProfile p, params string[] codes) => ExposureClasses.MinimumStrength(p, codes);
            // XC3: EN Table E.1N C25/30, DM 2012 C25/30, DK NA 30 MPa, UNI 11104:2016 and 2025 C30/37.
            Assert.AreEqual(25, S(DurabilityProfile.EN1992p11, "XC3").Fck); Assert.AreEqual(25, S(DurabilityProfile.UniEN1992p11, "XC3").Fck);
            Assert.AreEqual(30, S(DurabilityProfile.DsEN1992p11, "XC3").Fck); Assert.AreEqual(30, S(DurabilityProfile.Ntc2018, "XC3").Fck);
            Assert.AreEqual(30, S(DurabilityProfile.CnrDT200, "XC3").Fck);
            // Italian annex differences: XC1 C25/30 (EN C20/25), XF2 C30/37 (EN C25/30).
            Assert.AreEqual(20, S(DurabilityProfile.EN1992p11, "XC1").Fck); Assert.AreEqual(25, S(DurabilityProfile.UniEN1992p11, "XC1").Fck);
            Assert.AreEqual(25, S(DurabilityProfile.EN1992p11, "XF2").Fck); Assert.AreEqual(30, S(DurabilityProfile.UniEN1992p11, "XF2").Fck);
            // DK NA groups: passive 12, moderate 30, aggressive 35, extra aggressive 40.
            Assert.AreEqual(12, S(DurabilityProfile.DsEN1992p11, "XC1").Fck); Assert.AreEqual(35, S(DurabilityProfile.DsEN1992p11, "XS2").Fck);
            Assert.AreEqual(40, S(DurabilityProfile.DsEN1992p11, "XC1", "XF4").Fck); Assert.AreEqual(35, S(DurabilityProfile.DsEN1992p11, "XC4", "XF2").Fck);
            // XF4 is not in Table E.1N: listed as undefined, the other classes govern; alone, no value.
            var withXf4 = S(DurabilityProfile.EN1992p11, "XC4", "XF4");
            Assert.AreEqual(30, withXf4.Fck); CollectionAssert.AreEqual(new[] { "XF4" }, withXf4.Undefined.ToArray());
            Assert.IsNull(S(DurabilityProfile.UniEN1992p11, "XF4").Fck); Assert.AreEqual(0, S(DurabilityProfile.DsEN1992p11, "XF4").Undefined.Count);
            Assert.AreEqual(35, S(DurabilityProfile.EN1992p11, "XS3", "XA1").Fck);
            StringAssert.Contains(S(DurabilityProfile.EN1992p11, "XC1").Reference, "informative");
            StringAssert.Contains(S(DurabilityProfile.DsEN1992p11, "XC1").Reference, "E.1(2)");
            // Every class has a value in the tables except XF4 in Annex E; X0 is always C12/15.
            foreach (var e in ExposureClasses.All)
            {
                Assert.AreEqual(e.Code == "XF4", e.En1992IndicativeStrength == null, e.Code); Assert.AreEqual(e.Code == "XF4", e.UniIndicativeStrength == null, e.Code);
                Assert.IsTrue(e.DkMinimumStrength >= 12 && e.DkMinimumStrength <= 40, e.Code);
            }
            foreach (DurabilityProfile p in Enum.GetValues(typeof(DurabilityProfile))) Assert.AreEqual(12, S(p, "X0").Fck, p.ToString());
            Assert.ThrowsException<ArgumentException>(() => S(DurabilityProfile.EN1992p11, "X0", "XC1"));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => S((DurabilityProfile)99, "XC1"));
        }

        [TestMethod]
        public void MinimumStrengthMixAndAirLimits()
        {
            Assert.AreEqual(12, ExposureClasses.Uni11104MinimumStrength(new[] { "X0" }));
            var x0 = ExposureClasses.Uni11104Mix(new[] { "X0" }); Assert.IsNull(x0.Item1); Assert.IsNull(x0.Item2);
            var combined = ExposureClasses.Uni11104Mix(new[] { "XC2", "XF1" }); Assert.AreEqual(.50, combined.Item1); Assert.AreEqual(320, combined.Item2);
            Assert.AreEqual(35, ExposureClasses.Uni11104MinimumStrength(new[] { "XC1", "XA3" }));
            // Air (UNI 11104) for XF2-XF4 only: 5% for 12 ≤ dmax ≤ 16, 4% above 20 mm, no value between 16 and 20 or below 12.
            Assert.IsNull(ExposureClasses.Uni11104Air(new[] { "XF1" }, 32));
            Assert.AreEqual(5, ExposureClasses.Uni11104Air(new[] { "XF2" }, 12)); Assert.AreEqual(5, ExposureClasses.Uni11104Air(new[] { "XF3" }, 16));
            Assert.IsNull(ExposureClasses.Uni11104Air(new[] { "XF4" }, 18)); Assert.IsNull(ExposureClasses.Uni11104Air(new[] { "XF4" }, 20));
            Assert.AreEqual(4, ExposureClasses.Uni11104Air(new[] { "XF4" }, 20.5)); Assert.IsNull(ExposureClasses.Uni11104Air(new[] { "XF2" }, 11.9));
            Assert.ThrowsException<ArgumentException>(() => ExposureClasses.Uni11104Air(new[] { "XF2" }, 0));
            Assert.ThrowsException<ArgumentException>(() => ExposureClasses.Uni11104MinimumStrength(new[] { "X0", "XC1" }));
            Assert.AreEqual(18, ExposureClasses.All.Count); Assert.AreEqual(18, ExposureClasses.All.Select(e => e.Code).Distinct().Count());
            // Groups of NTC Tab. 4.1.III.
            CollectionAssert.AreEquivalent(new[] { "X0", "XC1", "XC2", "XC3", "XF1" }, ExposureClasses.All.Where(e => e.NtcEnvironment == 0).Select(e => e.Code).ToArray());
            CollectionAssert.AreEquivalent(new[] { "XD2", "XD3", "XS2", "XS3", "XA3", "XF4" }, ExposureClasses.All.Where(e => e.NtcEnvironment == 2).Select(e => e.Code).ToArray());
        }
    }
}
