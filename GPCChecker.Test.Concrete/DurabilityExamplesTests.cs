using GPC.Checkers.Concrete.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>Five worked examples of the materials and durability phase (report M), each step computed by hand.</summary>
    [TestClass]
    public class DurabilityExamplesTests
    {
        /// <summary>
        /// 1. NTC 2018, beam of a car park ramp, XC4, C30/37, 50 years, links Ø8, Δcdev 10 mm. Environment aggressive (Tab. 4.1.III); Cmin = UNI 11104
        /// XC4 = C32/40, C0 = C40/50; table "altri elementi" 30 + 5 (fck &lt; C0) = 35; fck 30 &lt; Cmin 32 → +5 = 40 mm; cmin,b = 8 mm; cnom = 50 mm.
        /// The UNI 11104 class is C32/40: C30/37 is not enough.
        /// </summary>
        [TestMethod]
        public void Example1_NtcAggressiveBeam()
        {
            var r = CoverRequirements.Calculate(DurabilityProfile.Ntc2018, new CoverInput(new[] { "XC4" }, 30, 50, false, false, false, 8, 20, 10,
                pertinentCmin: ExposureClasses.Uni11104MinimumStrength(new[] { "XC4" })));
            Assert.AreEqual(1, r.NtcEnvironment); Assert.AreEqual(32, r.NtcCmin); Assert.AreEqual(40, r.NtcC0); Assert.AreEqual(35, r.NtcTable);
            Assert.AreEqual(5, r.NtcLowStrengthExtra); Assert.AreEqual(0, r.NtcLifeExtra); Assert.AreEqual(0, r.NtcQualityReduction);
            Assert.AreEqual(40, r.Durability); Assert.AreEqual(8, r.Bond); Assert.AreEqual(40, r.Minimum); Assert.AreEqual(50, r.Nominal);
            var strength = ExposureClasses.MinimumStrength(DurabilityProfile.Ntc2018, new[] { "XC4" });
            Assert.AreEqual(32, strength.Fck); Assert.IsTrue(30 < strength.Fck);
            var mix = ExposureClasses.Uni11104Mix(new[] { "XC4" }); Assert.AreEqual(.50, mix.Item1); Assert.AreEqual(340, mix.Item2);
        }

        /// <summary>
        /// 2. EN 1992-1-1, bridge parapet XD1 + XF2, C40/50, 100 years, strength reduction, Ø20 bars, dg 32 mm, Δcdev 10 mm. XD1: S4 + 2 (100 years)
        /// − 1 (fck 40 ≥ C40/50) = S5 → Table 4.4N 40 mm; XF2 has no cover column. cmin,b = 20 (dg = 32, no addition); cmin = 40; cnom = 50 mm.
        /// Table E.1N: max(C30/37; C25/30) = C30/37 ≤ C40/50. EN 206 F.1: w/c ≤ 0.55, air ≥ 4%.
        /// </summary>
        [TestMethod]
        public void Example2_EurocodeParapetWithStructuralClass()
        {
            var r = CoverRequirements.Calculate(DurabilityProfile.EN1992p11, new CoverInput(new[] { "XD1", "XF2" }, 40, 100, true, false, false, 20, 32, 10));
            Assert.AreEqual(1, r.Lines.Count); Assert.AreEqual("XD1", r.Lines[0].Exposure); Assert.AreEqual(5, r.Lines[0].StructuralClass);
            Assert.AreEqual(40, r.Durability); Assert.AreEqual(20, r.Bond); Assert.AreEqual(40, r.Minimum); Assert.AreEqual(50, r.Nominal);
            Assert.AreEqual(30, ExposureClasses.MinimumStrength(DurabilityProfile.EN1992p11, new[] { "XD1", "XF2" }).Fck);
            Assert.AreEqual(.55, ExposureClasses.Get("XF2").En206MaxWaterCement); Assert.AreEqual(4, ExposureClasses.Get("XF2").En206MinAir);
            // Without the strength reduction: S6 → 45 mm, cnom 55 mm.
            Assert.AreEqual(55, CoverRequirements.Calculate(DurabilityProfile.EN1992p11, new CoverInput(new[] { "XD1", "XF2" }, 40, 100, false, false, false, 20, 32, 10)).Nominal);
        }

        /// <summary>
        /// 3. UNI EN 1992-1-1 (DM 31/07/2012), footing XC2 + XA2, C30/37, Ø25 bars, dg 40 mm, Δcdev 10 mm. XC2: S4 → 25 mm; cmin,b = 25 + 5 (dg &gt; 32)
        /// = 30 governs; cmin = 30; cmin + Δcdev = 40 = prepared ground 40 mm; directly against soil 75 mm. Prospetto E.1N (DM): max(C25/30; C30/37) = C30/37.
        /// </summary>
        [TestMethod]
        public void Example3_ItalianAnnexFootingAgainstGround()
        {
            CoverInput Footing(int ground) => new CoverInput(new[] { "XC2", "XA2" }, 30, 50, false, false, false, 25, 40, 10, ground: ground);
            var prepared = CoverRequirements.Calculate(DurabilityProfile.UniEN1992p11, Footing(40));
            Assert.AreEqual(25, prepared.Durability); Assert.AreEqual(30, prepared.Bond); Assert.AreEqual(30, prepared.Minimum); Assert.AreEqual(40, prepared.Nominal);
            Assert.AreEqual(75, CoverRequirements.Calculate(DurabilityProfile.UniEN1992p11, Footing(75)).Nominal);
            Assert.AreEqual(40, CoverRequirements.Calculate(DurabilityProfile.UniEN1992p11, Footing(0)).Nominal, "formwork: cmin + Δcdev");
            Assert.AreEqual(30, ExposureClasses.MinimumStrength(DurabilityProfile.UniEN1992p11, new[] { "XC2", "XA2" }).Fck);
            StringAssert.Contains(prepared.Reference, "DM 31/07/2012");
        }

        /// <summary>
        /// 4. DS/EN 1992-1-1 (DK NA), quay wall XS3 with abrasion XM1, C40/50, Ø16, Δcdev 5 mm. Tabel 4.4N NA: XS3 → 40 mm (no structural classes);
        /// cmin = max(10; 16; 40) + 5 (k1) = 45; cnom = 50 mm. Tabel E.1(2): extra aggressive, fck ≥ 40 MPa.
        /// </summary>
        [TestMethod]
        public void Example4_DanishMarineWithAbrasion()
        {
            var r = CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, new CoverInput(new[] { "XS3" }, 40, 50, true, true, true, 16, 20, 5, abrasion: 5));
            Assert.IsNull(r.Lines.Single().StructuralClass); Assert.AreEqual(40, r.Durability); Assert.AreEqual(45, r.Minimum); Assert.AreEqual(50, r.Nominal);
            Assert.AreEqual(40, ExposureClasses.MinimumStrength(DurabilityProfile.DsEN1992p11, new[] { "XS3" }).Fck);
            StringAssert.Contains(r.Reference, "DK NA");
        }

        /// <summary>
        /// 5. NTC 2018, slab (plate) XC3, C32/40, 100 years, quality control of the covers, Ø12, Δcdev 5 mm. Ordinary; Cmin = UNI 11104 XC3 = 30,
        /// C0 = 35; plate table 15 + 5 (fck &lt; C0) = 20; +10 (100 years) = 30; fck 32 ≥ Cmin; −5 (quality) = 25 mm; cmin = 25; cnom = 30 mm.
        /// </summary>
        [TestMethod]
        public void Example5_NtcPlateWithLongLifeAndQualityControl()
        {
            var r = CoverRequirements.Calculate(DurabilityProfile.Ntc2018, new CoverInput(new[] { "XC3" }, 32, 100, false, false, false, 12, 20, 5, plateElement: true,
                ntcQualityReduction: true, pertinentCmin: 30));
            Assert.AreEqual(0, r.NtcEnvironment); Assert.AreEqual(20, r.NtcTable); Assert.AreEqual(10, r.NtcLifeExtra); Assert.AreEqual(0, r.NtcLowStrengthExtra);
            Assert.AreEqual(5, r.NtcQualityReduction); Assert.AreEqual(25, r.Durability); Assert.AreEqual(25, r.Minimum); Assert.AreEqual(30, r.Nominal);
            // The same slab designed with EN: S4 + 2 − 1 (slab geometry) − 1 (quality) = S4 → 25 mm, cnom 30 mm.
            var en = CoverRequirements.Calculate(DurabilityProfile.EN1992p11, new CoverInput(new[] { "XC3" }, 32, 100, false, true, true, 12, 20, 5));
            Assert.AreEqual(4, en.Lines.Single().StructuralClass); Assert.AreEqual(30, en.Nominal);
        }
    }
}
