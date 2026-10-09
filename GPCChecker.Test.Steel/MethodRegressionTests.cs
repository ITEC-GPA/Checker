using System;
using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteelOptions = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace SteelTests
{
    [TestClass]
    public class MethodRegressionTests
    {
        // Expose protected section primitives without invoking bulk legacy checks.
        private sealed class Probe : EN1993p11Checker
        {
            public SteelSection Section { get; }
            public Probe() : this(new SteelSection(new SectionCHS(100, 10), SteelMaterialEN1993Data.S235)) { }
            private Probe(SteelSection section) : base(new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0), section),
                new SteelOptions(SteelOptions.LoadConditions.Constant, SteelOptions.SupportConditions.HingesAtEnds,
                    SteelOptions.LateralSupportConditions.HingesAtEnds, SteelOptions.LateralWarpingConditions.HingesAtEnds, null, null,
                    unbracedLengthFactorAxialBuck1: .5, effectiveLengthFactorAxialBuck1: .8,
                    unbracedLengthFactorAxialBuck2: .4, effectiveLengthFactorAxialBuck2: 1.2,
                    UnbracedLengthFactorLatTorsBuck: .6, effectiveLengthFactorLatTorsBuck: .7,
                    unbracedLengthFactorCriticalMoment1: .3, effectiveLengthFactorCriticalMoment1: .9,
                    unbracedLengthFactorCriticalMoment2: .2, effectiveLengthFactorCriticalMoment2: 1.1),
                new StandardEN1993p11 { GammaM0 = 1.25 }) { Section = section; }
            public double Moment1(SectionClass sectionClass) => CalculateMcRd1(sectionClass, Section);
            public double Moment2(SectionClass sectionClass) => CalculateMcRd2(sectionClass, Section);
            public double Shear1() => CalculateVcRd1(Section);
            public double Shear2() => CalculateVcRd2(Section);
        }
        [TestMethod]
        public void GetLengthAxialBuckling1_AppliesBothFactors()
        { Assert.AreEqual(400.0, new Probe().GetLengthAxialBuckling1(), 1e-10); }
        [TestMethod]
        public void GetLengthAxialBuckling2_AppliesItsOwnFactors()
        { Assert.AreEqual(480.0, new Probe().GetLengthAxialBuckling2(), 1e-10); }
        [TestMethod]
        public void GetEffectiveLengthAxialBuckling1_UsesOnlyEffectiveFactor()
        { Assert.AreEqual(800.0, new Probe().GetEffectiveLengthAxialBuckling1(), 1e-10); }
        [TestMethod]
        public void GetEffectiveLengthAxialBuckling2_UsesOnlyEffectiveFactor()
        { Assert.AreEqual(1200.0, new Probe().GetEffectiveLengthAxialBuckling2(), 1e-10); }
        [TestMethod]
        public void GetLengthLatTorsBuckling_AppliesLateralFactors()
        { Assert.AreEqual(420.0, new Probe().GetLengthLatTorsBuckling(), 1e-10); }
        [TestMethod]
        public void GetLengthCriticalMoment1_AppliesItsOwnFactors()
        { Assert.AreEqual(270.0, new Probe().GetLengthCriticalMoment1(), 1e-10); }
        [TestMethod]
        public void GetLengthCriticalMoment2_AppliesItsOwnFactors()
        { Assert.AreEqual(220.0, new Probe().GetLengthCriticalMoment2(), 1e-10); }
        [TestMethod]
        public void CalculateMcRd1_SelectsPlasticOrElasticModulusAndAppliesGamma()
        {
            var probe = new Probe();
            // Annulus: Wpl=4/3*(50^3-40^3); Wel=pi/4*(50^4-40^4)/50.
            Assert.AreEqual(81333.33333333333 * 235 / 1.25, probe.Moment1(EN1993p11Checker.SectionClass.Class1), 1e-6);
            Assert.AreEqual(Math.PI * 18450 * 235 / 1.25, probe.Moment1(EN1993p11Checker.SectionClass.Class3), 1e-6);
        }
        [TestMethod]
        public void CalculateMcRd2_SelectsPlasticOrElasticModulusAndAppliesGamma()
        {
            var probe = new Probe();
            Assert.AreEqual(81333.33333333333 * 235 / 1.25, probe.Moment2(EN1993p11Checker.SectionClass.Class2), 1e-6);
            Assert.AreEqual(Math.PI * 18450 * 235 / 1.25, probe.Moment2(EN1993p11Checker.SectionClass.Class3), 1e-6);
        }
        [TestMethod]
        public void CalculateVcRd1_UsesAnnulusShearAreaAndGamma()
        { Assert.AreEqual(1800 * 235 / Math.Sqrt(3) / 1.25, new Probe().Shear1(), 1e-8); }
        [TestMethod]
        public void CalculateVcRd2_UsesAnnulusShearAreaAndGamma()
        { Assert.AreEqual(1800 * 235 / Math.Sqrt(3) / 1.25, new Probe().Shear2(), 1e-8); }
    }
}
