using System;
using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    // Units: N, mm, MPa. Independent oracles and source limitations:
    // tests/Validation/ConcreteBridge/SOURCES.txt (JRC-EC2, NTC-2018, A-TRUSS).
    [TestClass]
    public class CriticalBranchReferenceTests
    {
        private static Standard Code(bool ntc) => ntc ? (Standard)new StandardNTC2018Concrete() : new StandardEN1992p11();
        private static void Close(double expected, double actual, string label)
            => Assert.AreEqual(expected, actual, 1e-8 * Math.Max(1, Math.Abs(expected)), label);

        [TestMethod]
        public void Jrc2014BeamSupportWithoutStirrups()
        {
            // JRC89037, 3.2.1.4, printed pp.59-60: T beam, web 250, d372, Asl565, C25.
            // Already reduced VEd is an input; this test does not validate load reduction near supports.
            var r = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(),
                0, 115520, 0, 250 * 400, 250, 372, 565, 25, 25.0 / 1.5, 435, 1.5, 200000, 0, 175));
            Close(47902.36530502512, r.VRd, "independently evaluated VRdc");
            Assert.AreEqual(47.90, r.VRd / 1000, .005, "published rounding, kN");
            Assert.AreEqual(ShearVerdict.NotSatisfied, r.Verdict);
        }

        [TestMethod]
        public void Jrc2014BeamSupportWithNominalTwoLegSixMillimetreStirrups()
        {
            // JRC prints 339 mm2/m for 2phi6/175; nominal geometry gives 323.14.
            // Retain the actual bar geometry, not the inconsistent printed area (see SOURCES.txt).
            double asw = 2 * Math.PI * 6 * 6 / 4;
            var r = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(),
                0, 115520, 0, 100000, 250, 372, 565, 25, 25.0 / 1.5, 435, 1.5, 200000, asw, 175, cotTheta: 2.5));
            Close(259758.6206896552, r.VRcd, "JRC concrete strut");
            Assert.AreEqual(259.8, r.VRcd / 1000, .05, "published rounding, kN");
            Close(.9 * 372 * asw / 175 * 435 * 2.5, r.VRsd, "nominal link equilibrium");
            Assert.AreEqual(ShearVerdict.Satisfied, r.Verdict);
        }

        [DataTestMethod]
        [DataRow(true, 90.0, .7)] [DataRow(true, 90.0, 1.5)] [DataRow(true, 90.0, 3.0)]
        [DataRow(false, 90.0, .7)] [DataRow(false, 90.0, 1.5)] [DataRow(false, 90.0, 3.0)]
        [DataRow(true, 60.0, .7)] [DataRow(true, 60.0, 1.5)] [DataRow(true, 60.0, 3.0)]
        [DataRow(false, 60.0, .7)] [DataRow(false, 60.0, 1.5)] [DataRow(false, 60.0, 3.0)]
        public void TrussOptimumMatchesClosedFormAtBothBoundsAndInterior(bool ntc, double alphaDegrees, double intersection)
        {
            // Equality of the steel and concrete branches gives c^2+1 = bw nu fcd/(q fyd sin alpha).
            // For c>=1 and alpha<=90, the concrete branch decreases and the steel branch increases.
            double alpha = alphaDegrees * Math.PI / 180, nu = ntc ? .5 : .528;
            double q = 300 * nu * 20 / (435 * Math.Sin(alpha) * (1 + intersection * intersection));
            double c = Math.Max(1, Math.Min(2.5, intersection)), z = .9 * 460;
            double steel = z * q * 435 * (c + 1 / Math.Tan(alpha)) * Math.Sin(alpha);
            double concrete = z * 300 * nu * 20 * (c + 1 / Math.Tan(alpha)) / (1 + c * c);
            var r = SectionShearCalculator.Calculate(new SectionShearInput(Code(ntc), 0, 100000, 0,
                150000, 300, 460, 1256, 30, 20, 435, 1.5, 200000, q * 150, 150, alphaDegrees));
            Close(c, r.CotTheta, "closed-form optimum");
            Close(steel, r.VRsd, "steel"); Close(concrete, r.VRcd, "concrete");
            Close(Math.Min(steel, concrete), r.VRd, "governing branch");
        }

        [DataTestMethod]
        [DataRow(-.1, 1.0)] [DataRow(0.0, 1.0)]
        [DataRow(.249999, 1.249999)] [DataRow(.25, 1.25)] [DataRow(.250001, 1.25)]
        [DataRow(.499999, 1.25)] [DataRow(.5, 1.25)] [DataRow(.500001, 1.2499975)]
        [DataRow(.9, .25)] [DataRow(.9999, .00025)]
        public void NtcCompressionFactorHasThePrescribedPiecewiseBranches(double compressionFraction, double alphaC)
        {
            // NTC2018 4.1.2.3.5.2, eq.4.1.29; N is negative in compression.
            var r = SectionShearCalculator.Calculate(new SectionShearInput(Code(true), -compressionFraction * 20 * 150000,
                10000, 0, 150000, 300, 460, 1256, 30, 20, 435, 1.5, 200000, 200, 150, cotTheta: 1.5));
            Close(.9 * 460 * 300 * .5 * 20 * alphaC * 1.5 / 3.25, r.VRcd, "piecewise alphaC");
        }

        [TestMethod]
        public void NtcCompressionAtFcdDoesNotReturnAFavourableCheck()
        {
            // Outside the open upper interval in 4.1.29: API guard, not a design capacity at sigma=fcd.
            var r = SectionShearCalculator.Calculate(new SectionShearInput(Code(true), -20 * 150000,
                10000, 0, 150000, 300, 460, 1256, 30, 20, 435, 1.5, 200000, 200, 150, cotTheta: 1.5));
            Assert.AreEqual(0, r.VRd); Assert.IsNull(r.Ratio);
            Assert.AreEqual(ShearVerdict.NotSatisfied, r.Verdict);
        }

        [DataTestMethod]
        [DataRow(true, .999)] [DataRow(true, 1.001)] [DataRow(true, -1.001)]
        [DataRow(false, .999)] [DataRow(false, 1.001)] [DataRow(false, -1.001)]
        public void ShearVerdictCrossesAnIndependentlyCalculatedResistance(bool ntc, double demandFactor)
        {
            // Links govern for both standards at assigned cot=1.5.
            double resistance = .9 * 460 * 100 / 150 * 435 * 1.5;
            var r = SectionShearCalculator.Calculate(new SectionShearInput(Code(ntc), 0, demandFactor * resistance, 0,
                150000, 300, 460, 1256, 30, 20, 435, 1.5, 200000, 100, 150, cotTheta: 1.5));
            Close(resistance, r.VRd, "resistance"); Close(Math.Abs(demandFactor), r.Ratio.Value, "utilization");
            Assert.AreEqual(Math.Abs(demandFactor) < 1 ? ShearVerdict.Satisfied : ShearVerdict.NotSatisfied, r.Verdict);
        }

        [DataTestMethod]
        [DataRow(true, 0)] [DataRow(true, 1)] [DataRow(true, 2)] [DataRow(true, 3)]
        [DataRow(false, 0)] [DataRow(false, 1)] [DataRow(false, 2)] [DataRow(false, 3)]
        public void TorsionFromOutlineToResistanceMatchesPeripheralTruss(bool ntc, int shape)
        {
            var g = Geometry(ntc, shape, out double area, out double perimeter, out double thickness);
            Close(area, g.EnclosedArea, "Ak"); Close(perimeter, g.Perimeter, "uk"); Close(thickness, g.Thickness, "tef");
            double nu = ntc ? .5 : .528;
            double tc = 2 * area * thickness * nu * 20 * 1.5 / 3.25;
            double ts = 2 * area * 50 / 150 * 435 * 1.5;
            double tl = 2 * area * 800 / perimeter * 435 / 1.5;
            var r = SectionTorsionCalculator.Calculate(Torsion(ntc, g, -1e7), null, null);
            Close(tc, r.TRcd, "strut"); Close(ts, r.TRsd, "links"); Close(tl, r.TRld, "longitudinal steel");
            Close(Math.Min(tc, Math.Min(ts, tl)), r.TRd, "minimum resistance");
            Close(1e7 / Math.Min(tc, Math.Min(ts, tl)), r.Ratio.Value, "signed torque");
        }

        [DataTestMethod]
        [DataRow(true, 0)] [DataRow(true, 1)] [DataRow(true, 2)] [DataRow(true, 3)]
        [DataRow(false, 0)] [DataRow(false, 1)] [DataRow(false, 2)] [DataRow(false, 3)]
        public void TorsionAndBothShearsUseOneTrussWithIndependentInteraction(bool ntc, int shape)
        {
            var g = Geometry(ntc, shape, out double area, out double perimeter, out double thickness);
            double nu = ntc ? .5 : .528, c = 1.5;
            double tc = 2 * area * thickness * nu * 20 * c / (1 + c * c);
            double ts = 2 * area * 50 / 150 * 435 * c, tl = 2 * area * 800 / perimeter * 435 / c;
            double v1s = .9 * 250 * 100 / 150 * 435 * c, v2s = .9 * 450 * 100 / 150 * 435 * c;
            double v1c = .9 * 250 * 500 * nu * 20 * c / (1 + c * c);
            double v2c = .9 * 450 * 300 * nu * 20 * c / (1 + c * c);
            var axis1 = new SectionShearInput(Code(ntc), 0, 40000, 0, 150000, 500, 250, 0, 30, 20, 435, 1.5, 200000, 100, 150);
            var axis2 = new SectionShearInput(Code(ntc), 0, -90000, 0, 150000, 300, 450, 0, 30, 20, 435, 1.5, 200000, 100, 150);
            var r = SectionTorsionCalculator.Calculate(Torsion(ntc, g, 1e7), axis1, axis2);
            Close(c, r.Axis1Shear.CotTheta, "common cot 1"); Close(c, r.Axis2Shear.CotTheta, "common cot 2");
            Close(v1s, r.Axis1Shear.VRsd, "links 1"); Close(v2s, r.Axis2Shear.VRsd, "links 2");
            Close(v1c, r.Axis1Shear.VRcd, "strut 1"); Close(v2c, r.Axis2Shear.VRcd, "strut 2");
            double concrete = 1e7 / tc + 40000 / v1c + 90000 / v2c;
            double links = 1e7 / ts + Math.Max(40000 / v1s, 90000 / v2s);
            double overall = Math.Max(1e7 / Math.Min(tc, Math.Min(ts, tl)), Math.Max(concrete, links));
            Close(concrete, r.ConcreteInteraction.Value, "concrete interaction");
            Close(links, r.LinkInteraction.Value, "link interaction"); Close(overall, r.Ratio.Value, "overall");
            Assert.AreEqual(overall <= 1 ? TorsionVerdict.Satisfied : TorsionVerdict.NotSatisfied, r.Verdict);
        }

        private static SectionTorsionInput Torsion(bool ntc, TorsionGeometry g, double t)
            => new SectionTorsionInput(Code(ntc), t, g, 30, 20, 1.5, 435, 435, 50, 150, 800, 1.5);

        private static TorsionGeometry Geometry(bool ntc, int shape, out double ak, out double uk, out double tef)
        {
            // Independent centre-line geometry. Hollow sections intentionally distinguish actual wall
            // (NTC) from equivalent thickness limited by wall (base EC2 API); no imported baseline.
            var rule = ntc ? TorsionThicknessRule.WallThickness : TorsionThicknessRule.EquivalentLimitedByWall;
            double width = shape == 0 ? 300 : 1200, height = shape == 0 ? 500 : 1000;
            double diameter = shape == 2 ? 600 : 1000;
            tef = shape == 0 ? 93.75 : shape == 1 ? (ntc ? 300 : 1200000.0 / 4400)
                : shape == 2 ? 150 : (ntc ? 300 : 250);
            if (shape < 2)
            {
                ak = (width - tef) * (height - tef); uk = 2 * (width + height - 2 * tef);
                return shape == 0 ? TorsionGeometry.Rectangle(300, 500, 150000, 40)
                    : TorsionGeometry.Rectangle(1200, 1000, 960000, 60, 600, 400, rule);
            }
            ak = Math.PI * (diameter - tef) * (diameter - tef) / 4; uk = Math.PI * (diameter - tef);
            return shape == 2 ? TorsionGeometry.Circle(600, Math.PI * 600 * 600 / 4, 40)
                : TorsionGeometry.Circle(1000, Math.PI * (1000 * 1000 - 400 * 400) / 4, 40, 400, rule);
        }
    }
}
