using System;
using System.Linq;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Options = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace SteelTests
{
    [TestClass]
    public class EN1993ResultIntegrityTests
    {
        // Units: N, mm, Nmm, MPa. N is positive in tension.
        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public void GroupingDoesNotChangeStationResults(int split)
        {
            var stations = new[] { Station("A", 100000), Station("B", 200000), Station("C", 300000) };
            var combined = Run(Rhs(), new BeamResult(stations));
            var separated = Run(Rhs(), new BeamResult(stations.Take(split).ToArray()), new BeamResult(stations.Skip(split).ToArray()));
            Assert.AreEqual(0, combined.GetErrorLog().Count);
            Assert.AreEqual(0, separated.GetErrorLog().Count);
            Assert.AreEqual(stations.Length, separated.EN1993p11BeamStationResults.Length);
            for (int i = 0; i < stations.Length; i++)
            {
                var expected = combined.EN1993p11BeamStationResults[i];
                var actual = separated.EN1993p11BeamStationResults[i];
                Assert.AreSame(stations[i], actual.StationResultBeamForces);
                Near(expected.AxialTensionCapacity, actual.AxialTensionCapacity);
                Near(stations[i].ResultBeamForces.N / actual.AxialTensionCapacity, actual.AxialTensionWorkingRatio);
                Near(expected.CrossSectionInteraction, actual.CrossSectionInteraction);
                Near(expected.WorkingRatio, actual.WorkingRatio);
                Near(expected.ChiAxialBuckling1, actual.ChiAxialBuckling1);
                Near(expected.ChiAxialBuckling2, actual.ChiAxialBuckling2);
                Near(expected.Kxx, actual.Kxx);
                Assert.AreEqual(expected.AxialCompressionClass, actual.AxialCompressionClass);
            }
        }

        [DataTestMethod]
        [DataRow(false, -1, 1)]
        [DataRow(false, 1, -1)]
        [DataRow(false, -1, -1)]
        [DataRow(true, -1, 1)]
        [DataRow(true, 1, -1)]
        [DataRow(true, -1, -1)]
        public void SymmetricSectionInteractionDoesNotDependOnMomentSigns(bool useH, int sign1, int sign2)
        {
            var section = useH ? H() : Rhs();
            double n = useH ? -2000000 : 100000;
            double m = useH ? 10000000 : 100000000;
            var reference = Run(section, new BeamResult(new[] { Station("Positive", n, m, m) }));
            var signed = Run(section, new BeamResult(new[] { Station("Signed", n, sign1 * m, sign2 * m) }));
            Assert.AreEqual(0, reference.GetErrorLog().Count);
            Assert.AreEqual(0, signed.GetErrorLog().Count);
            var expected = reference.EN1993p11BeamStationResults[0];
            var actual = signed.EN1993p11BeamStationResults[0];
            Assert.IsTrue(expected.CrossSectionInteraction > 0);
            Near(expected.CrossSectionInteraction, actual.CrossSectionInteraction);
            Assert.IsFalse(double.IsNaN(actual.WorkingRatio));
        }

        [TestMethod]
        public void FailedStationKeepsItsIdentityAndDoesNotOverwriteFollowingStation()
        {
            var good = Station("Before", 100000);
            var bad = Station("InvalidTorsion", 100000, torque: 1e12);
            var next = Station("After", 200000);
            var checker = Run(Rhs(), new BeamResult(new[] { good, bad, next }));
            var results = checker.EN1993p11BeamStationResults;
            Assert.AreEqual(3, results.Length);
            Assert.AreEqual(1, checker.GetErrorLog().Count);
            StringAssert.Contains(checker.GetErrorLog()[0], "InvalidTorsion");
            Assert.AreSame(bad, results[1].StationResultBeamForces);
            Assert.IsTrue(double.IsNaN(results[1].WorkingRatio), "A failed calculation must not look satisfied.");
            Assert.AreSame(next, results[2].StationResultBeamForces);
            Near(next.ResultBeamForces.N / results[2].AxialTensionCapacity, results[2].AxialTensionWorkingRatio);
            Assert.IsTrue(results[0].WorkingRatio > 0);
            Assert.IsTrue(results[2].WorkingRatio > 0);
        }

        [DataTestMethod]
        [DataRow("N")]
        [DataRow("V1")]
        [DataRow("V2")]
        [DataRow("T")]
        [DataRow("M1")]
        [DataRow("M2")]
        public void NonfiniteActionsAreReportedWithoutAValidUtilization(string component)
        {
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var station = Station("Nonfinite-" + component, 100000);
                typeof(ResultBeamForces).GetProperty(component).SetValue(station.ResultBeamForces, invalid);
                var checker = Run(Rhs(), new BeamResult(new[] { station }));
                Assert.AreEqual(1, checker.GetErrorLog().Count, component + " = " + invalid);
                Assert.AreSame(station, checker.EN1993p11BeamStationResults[0].StationResultBeamForces);
                Assert.IsTrue(double.IsNaN(checker.EN1993p11BeamStationResults[0].WorkingRatio));
                Assert.IsTrue(double.IsNaN(checker.EN1993p11BeamStationResults[0].AxialTensionCapacity));
            }
        }

        [TestMethod]
        public void RepeatedCheckClearsPreviousErrorsAndResults()
        {
            var station = Station("Corrected", 100000, torque: 1e12);
            var checker = Run(Rhs(), new BeamResult(new[] { station }));
            Assert.AreEqual(1, checker.GetErrorLog().Count);
            station.ResultBeamForces.T = 0;
            checker.PerformCheck();
            Assert.AreEqual(0, checker.GetErrorLog().Count);
            Assert.IsTrue(checker.EN1993p11BeamStationResults[0].WorkingRatio > 0);
            Assert.IsFalse(double.IsNaN(checker.EN1993p11BeamStationResults[0].WorkingRatio));
        }

        [TestMethod]
        public void AggregateDoesNotHideANonfiniteInteraction()
        {
            var result = new ResultProbe();
            Assert.IsTrue(double.IsNaN(result.WorkingRatio));
        }

        private sealed class ResultProbe : EN1993p11BeamStationResult
        {
            public ResultProbe() : base(new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0), Rhs()),
                Station("Probe", 100000), new LoadCase("Probe", GPC.Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight),
                CreateOptions(), new StandardEN1993p11())
            {
                _axialTensionWorkingRatio = .5;
                _axialCompressionWorkingRatio = _axialBuckling1WorkingRatio = _axialBuckling2WorkingRatio = .5;
                _shear1WorkingRatio = _shear2WorkingRatio = .5;
                _bendingMoment1WorkingRatio = _bendingMoment2WorkingRatio = _lateraTorsionalBucklingWorkingRatio = .5;
                _bucklingInteraction1Axis = _bucklingInteraction2Axis = _flexuralTorsionalInteraction = .5;
                _crossSectionInteraction = double.NaN;
            }
        }

        private static EN1993p11Checker Run(SteelSection section, params BeamResult[] groups)
        {
            var beam = new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0), section);
            beam.Results.AddRange(groups);
            var checker = new EN1993p11Checker(beam, CreateOptions(), new StandardEN1993p11());
            checker.PerformCheck();
            return checker;
        }

        private static Options CreateOptions() => new Options(Options.LoadConditions.NotDirectlyLoaded,
            Options.SupportConditions.EndsRestrained, Options.LateralSupportConditions.HingesAtEnds,
            Options.LateralWarpingConditions.HingesAtEnds, .5, 1);

        private static SteelSection Rhs() => new SteelSection(new SectionRHS(400, 200, 8, 8, 15, 15, ""), SteelMaterialEN1993Data.S355);

        private static SteelSection H() => new SteelSection(new SectionH(300, 12, 300, 20, 300, 20, "", 15),
            SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);

        private static StationResultBeamForces Station(string name, double n, double m1 = 100000000,
            double m2 = 100000000, double torque = 0)
            => new StationResultBeamForces(new LoadCase(name, LoadCase.LoadCaseTypes.SelfWeight),
                new ResultBeamForces(n, 50000, 50000, torque, m1, m2, CoordinateSystem.Global), 0);

        private static void Near(double expected, double actual)
        {
            Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual));
            // Relative 1e-12 with unit floor: the grouping/sign transformations must preserve the same arithmetic.
            Assert.AreEqual(expected, actual, 1e-12 * Math.Max(1, Math.Abs(expected)));
        }
    }
}
