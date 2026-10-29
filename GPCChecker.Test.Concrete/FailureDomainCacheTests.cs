// Raw domain points on purpose: these tests compare the points of the cached and of the computed domains (architecture review R2).
#pragma warning disable CS0618
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConcreteTests
{
    /// <summary>
    /// Cache of the failure domains (29/10/2026): the domain of a solver is computed once for the same section content, standards and options of
    /// the domain, also by another solver on an equal section, with the same numbers as a new computation; the intersection strategy makes its
    /// mesh on the first point, not in the constructor
    /// </summary>
    [TestClass]
    public class FailureDomainCacheTests : ConcreteTestBase
    {
        private ReinforcedConcreteSection NewSection() =>
            GetRectangularSection4Rebars(300, 500, 20, 50, ConcreteMaterialEN1992Data.C30_37, SteelMaterialEN1992Data.B450C);

        private static SectionCheckerModelCode2010 NewChecker(IConcreteSection section, CoordinateSystem axes = null, int teta = 64,
            SectionSolver.FailureAnalysisTypes criterion = SectionSolver.FailureAnalysisTypes.ConstantN,
            SectionSolver.FailureDomainTypes type = SectionSolver.FailureDomainTypes.Plastic, bool tensile = false,
            SectionSolver.DomainPointStrategyTypes strategy = SectionSolver.DomainPointStrategyTypes.Iterative, StandardModelCode2010 standard = null,
            bool cache = true)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes ?? CoordinateSystem.Global, criterion, type,
                SectionSolver.StressAnalysisTypes.Linear, 0, 0, tensile, teta) { DomainPointStrategy = strategy };
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard ?? new StandardNTC2018Concrete(), tensile);
            checker.SectionSolver.DomainCacheEnabled = cache;
            return checker;
        }

        private static ResultBeamForces[] Forces(CoordinateSystem axes)
        {
            var forces = new List<ResultBeamForces>();
            int id = 1;
            foreach (double n in new[] { -1200e3, -300e3, 100e3 })
                foreach (double angle in new[] { 0.3, 1.9, 4.0 })
                    forces.Add(new ResultBeamForces(n, 0, 0, 0, 60e6 * Math.Cos(angle), 30e6 * Math.Sin(angle), axes, id++));
            return forces.ToArray();
        }

        #region Bit-for-bit comparison

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static void AssertSamePoint(FailureDomain.FailureDomainPoint expected, FailureDomain.FailureDomainPoint actual, string where)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected == null, actual == null, where + ": null point");
                return;
            }
            Assert.AreEqual(Bits(expected.NRd), Bits(actual.NRd), where + ": NRd");
            Assert.AreEqual(Bits(expected.MxRd), Bits(actual.MxRd), where + ": MxRd");
            Assert.AreEqual(Bits(expected.MyRd), Bits(actual.MyRd), where + ": MyRd");
            Assert.AreEqual(expected.FailureIndex, actual.FailureIndex, where + ": field");
            Assert.AreEqual(Bits(expected.Immersione), Bits(actual.Immersione), where + ": immersion");
            Assert.AreEqual(Bits(expected.WorkingRatio), Bits(actual.WorkingRatio), where + ": working ratio");
            Assert.AreEqual(expected.InterpolatedStrainPlane, actual.InterpolatedStrainPlane, where + ": interpolated plane");
            Assert.AreEqual(expected.StrainPlane == null, actual.StrainPlane == null, where + ": null plane");
            if (expected.StrainPlane == null)
                return;
            Assert.AreEqual(Bits(expected.StrainPlane.ChiX), Bits(actual.StrainPlane.ChiX), where + ": χx");
            Assert.AreEqual(Bits(expected.StrainPlane.ChiY), Bits(actual.StrainPlane.ChiY), where + ": χy");
            Assert.AreEqual(Bits(expected.StrainPlane.StrainReferencePoint), Bits(actual.StrainPlane.StrainReferencePoint), where + ": ε");
            Assert.AreEqual(Bits(expected.StrainPlane.ReferencePoint.X), Bits(actual.StrainPlane.ReferencePoint.X), where + ": x of the plane");
            Assert.AreEqual(Bits(expected.StrainPlane.ReferencePoint.Y), Bits(actual.StrainPlane.ReferencePoint.Y), where + ": y of the plane");
            Assert.AreEqual(expected.StrainPlane.Id, actual.StrainPlane.Id, where + ": id of the plane");
        }

        private static void AssertSameDomain(FailureDomain expected, FailureDomain actual, string where)
        {
            Assert.IsNotNull(expected, where);
            Assert.IsNotNull(actual, where);
            Assert.AreNotSame(expected, actual, where + ": a result shares its domain");
            Assert.AreEqual(expected.FailureDomainAnalysisTypes, actual.FailureDomainAnalysisTypes, where + ": type");
            Assert.AreEqual(expected.AxialForceSubdivision, actual.AxialForceSubdivision, where + ": subdivisions");
            Assert.AreEqual(expected.ForceLinearInterpolation, actual.ForceLinearInterpolation, where + ": interpolation");
            Assert.AreEqual(expected.DomainPoints.Length, actual.DomainPoints.Length, where + ": meridians");
            for (int i = 0; i < expected.DomainPoints.Length; i++)
            {
                Assert.AreEqual(expected.DomainPoints[i].Length, actual.DomainPoints[i].Length, where + ": points of meridian " + i);
                for (int j = 0; j < expected.DomainPoints[i].Length; j++)
                {
                    Assert.AreNotSame(expected.DomainPoints[i][j], actual.DomainPoints[i][j], where + ": a result shares its points");
                    AssertSamePoint(expected.DomainPoints[i][j], actual.DomainPoints[i][j], where + " [" + i + "][" + j + "]");
                }
            }
        }

        private static void AssertSameDomain2d(FailureDomain2d expected, FailureDomain2d actual, string where)
        {
            Assert.AreEqual(expected.DomainType, actual.DomainType, where + ": type");
            Assert.AreEqual(Bits(expected.MomentAxis), Bits(actual.MomentAxis), where + ": axis");
            Assert.AreEqual(expected.DomainPoints.Length, actual.DomainPoints.Length, where + ": points");
            for (int i = 0; i < expected.DomainPoints.Length; i++)
                AssertSamePoint(expected.DomainPoints[i], actual.DomainPoints[i], where + " [" + i + "]");
        }

        #endregion

        [TestMethod]
        public void IntersectionCheckerIsCreatedWithoutComputingTheDomain()
        {
            FailureDomainCache.Clear();
            var section = NewSection();
            var checker = NewChecker(section, strategy: SectionSolver.DomainPointStrategyTypes.Intersection);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            Assert.AreEqual(0, checker.SectionSolver.DomainComputations, "constructor and SetDomainPointStrategy compute no domain");
            Assert.AreEqual(0, FailureDomainCache.MeshBuilds);

            var forces = Forces(CoordinateSystem.Global);
            var points = forces.Select(f => checker.CalculateFailureDomainPoint(f)).ToArray();
            Assert.AreEqual(1, checker.SectionSolver.DomainComputations, "the first point computes the domain");
            Assert.AreEqual(1, FailureDomainCache.MeshBuilds, "and its mesh");

            // switching strategies (the tests and ANTHEA do it) makes neither the domain nor the mesh again
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            var again = forces.Select(f => checker.CalculateFailureDomainPoint(f)).ToArray();
            Assert.AreEqual(1, checker.SectionSolver.DomainComputations);
            Assert.AreEqual(1, FailureDomainCache.MeshBuilds);

            // the same points as the strategy that computes its own domain
            var reference = NewChecker(NewSection(), strategy: SectionSolver.DomainPointStrategyTypes.Intersection, cache: false);
            for (int i = 0; i < forces.Length; i++)
            {
                var expected = reference.CalculateFailureDomainPoint(forces[i]);
                Assert.IsNotNull(expected);
                AssertSamePoint(expected, points[i], "first " + i);
                AssertSamePoint(expected, again[i], "again " + i);
            }
        }

        [TestMethod]
        public void SecondDomainOfTheSameSolverIsTheCachedOne()
        {
            FailureDomainCache.Clear();
            var checker = NewChecker(NewSection());
            var first = checker.GetFailureDomainResult();
            var second = checker.GetFailureDomainResult();
            Assert.AreEqual(1, checker.SectionSolver.DomainComputations);
            Assert.AreNotSame(first, second);
            AssertSameDomain(first.Domain, second.Domain, "second");
            // as before, the criterion of a result is the one of the options of its solver
            second.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantMxMy;
            Assert.AreEqual(SectionSolver.FailureAnalysisTypes.ConstantMxMy, checker.SectionCheckerOptions.FailureAnalysisType);
            second.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantN;

            // what a caller changes on its result stays on its result
            first.Domain.ForceLinearInterpolation = true;
            first.Domain.AxialForceSubdivision = 7;
            first.FailureDomainAnalysisType = SectionSolver.FailureDomainTypes.Elastic;
            first.Domain.DomainPoints[3][5].WorkingRatio = 0.5;
            first.Domain.DomainPoints[3][5].StrainPlane.ReferencePoint.X = 1e9;
            first.Domain.DomainPoints[4][6] = null;
            var third = checker.GetFailureDomainResult();
            Assert.AreEqual(1, checker.SectionSolver.DomainComputations);
            AssertSameDomain(second.Domain, third.Domain, "third");
            var reference = NewChecker(NewSection(), cache: false).GetFailureDomainResult();
            AssertSameDomain(reference.Domain, third.Domain, "computed");
        }

        [TestMethod]
        public void NewCheckerOnTheSameSectionFindsTheSharedDomain()
        {
            FailureDomainCache.Clear();
            var section = NewSection();
            var first = NewChecker(section).GetFailureDomainResult();

            // a detached copy: another criterion, strategy and stress analysis options on the same section object
            var copy = NewChecker(section, criterion: SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
                strategy: SectionSolver.DomainPointStrategyTypes.Intersection);
            copy.SectionCheckerOptions.StressAnalysisConcreteTension = ConcreteMaterial.TensionResponseDiagrams.TensionStiffening;
            copy.SectionCheckerOptions.PsiCoefficientRebar = 2;
            var copied = copy.GetFailureDomainResult();
            Assert.AreEqual(0, copy.SectionSolver.DomainComputations, "the copy finds the domain of the first checker");
            AssertSameDomain(first.Domain, copied.Domain, "copy");
            Assert.AreSame(copy.SectionSolver, GetSolver(copied), "the result is bound to the solver of the caller");

            // an equal section built again (other objects)
            var equal = NewChecker(NewSection());
            AssertSameDomain(first.Domain, equal.GetFailureDomainResult().Domain, "equal section");
            Assert.AreEqual(0, equal.SectionSolver.DomainComputations);

            // the intersection strategy of the copy uses the shared domain too
            copy.CalculateFailureDomainPoint(Forces(CoordinateSystem.Global)[0]);
            Assert.AreEqual(0, copy.SectionSolver.DomainComputations);
            Assert.IsTrue(FailureDomainCache.Hits >= 2, "hits " + FailureDomainCache.Hits);
        }

        private static SectionSolver GetSolver(FailureDomainResult result) =>
            (SectionSolver)typeof(FailureDomainResult).GetField("_sectionSolver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(result);

        [TestMethod]
        public void ChangedInputsComputeTheDomainAgain()
        {
            FailureDomainCache.Clear();
            var section = NewSection();
            var standard = new StandardNTC2018Concrete();
            var checker = NewChecker(section, standard: standard);
            int computations = 0;
            void AssertComputedAgain(string change)
            {
                var cached = checker.GetFailureDomainResult();
                Assert.AreEqual(++computations, checker.SectionSolver.DomainComputations, change + ": computed again");
                checker.SectionSolver.DomainCacheEnabled = false;
                var computed = checker.GetFailureDomainResult();
                checker.SectionSolver.DomainCacheEnabled = true;
                computations++;
                AssertSameDomain(computed.Domain, cached.Domain, change);
                Assert.AreEqual(computations, checker.SectionSolver.DomainComputations);
                checker.GetFailureDomainResult();
                Assert.AreEqual(computations, checker.SectionSolver.DomainComputations, change + ": then cached");
            }
            AssertComputedAgain("first");
            checker.SectionSolver.TetaDiscretization = 32;
            AssertComputedAgain("meridians");
            checker.SectionSolver.ConsiderTensileConcrete = true;
            AssertComputedAgain("tensile concrete");
            checker.SectionSolver.ConsiderTensileConcrete = false;
            checker.SectionCheckerOptions.FailureDomainType = SectionSolver.FailureDomainTypes.Elastic;
            AssertComputedAgain("elastic");
            checker.SectionCheckerOptions.FailureDomainType = SectionSolver.FailureDomainTypes.Plastic;
            checker.SectionCheckerOptions.ForceReferenceCoordinateSystem = new CoordinateSystem(new Point3d(100, 200, 0), Vector3d.XAxis, Vector3d.YAxis);
            AssertComputedAgain("origin of the force axes");
            section.AddRebar(new ReinforcedConcreteRebar(new RebarSectionCircular(16, SteelMaterialEN1992Data.B450C), new Point3d(150, 450, 0)));
            AssertComputedAgain("bar added");
            section.ConcreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            AssertComputedAgain("concrete");
            standard.GammaC = 1.6;
            AssertComputedAgain("standard");
        }

        [TestMethod]
        public void SectionWithoutBarsHasNoDomainPointWithIntersection()
        {
            FailureDomainCache.Clear();
            var section = new ReinforcedConcreteSection(GetRectangularShape(300, 500), ConcreteMaterialEN1992Data.C30_37);
            // before: NullReferenceException in the constructor of the checker (DomainPointStrategyIntersection.CalculateDomainMesh)
            var checker = NewChecker(section, strategy: SectionSolver.DomainPointStrategyTypes.Intersection);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            Assert.IsNull(checker.SectionSolver.GetFailureDomainResult(), "plain concrete without bars has no domain");
            var force = new ResultBeamForces(-500e3, 0, 0, 0, 10e6, 0, CoordinateSystem.Global, 1);
            Assert.IsNull(checker.CalculateFailureDomainPoint(force), "no domain, no point");
            var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(force));
            Assert.AreEqual(CalculationStatus.NotConverged, response.Diagnostics.Status);
        }

        [TestMethod]
        public void CachedDomainsAreTheComputedOnesBitForBit()
        {
            var cases = new List<Func<bool, SectionCheckerModelCode2010>>
            {
                cache => NewChecker(NewSection(), cache: cache),
                cache => NewChecker(NewSection(), teta: 32, type: SectionSolver.FailureDomainTypes.Elastic, cache: cache),
                cache => { var s = NewSection(); return NewChecker(s, GetLocalCoordinateSystem(s), 48, tensile: true, cache: cache); },
                cache =>
                {
                    var rebar = new RebarSectionCircular("", 16.0, SteelMaterialEN1992Data.B450C);
                    var composite = new ReinforcedConcreteSection(1000.0, 300.0, ConcreteMaterialEN1992Data.C25_30, rebar, 200.0, 50.0, rebar, 200.0,
                        new SectionH(300.0, 7.1, 150.0, 10.7, 150.0, 10.7, "IPE300 r=0"), SteelMaterialEN1993Data.S275, 50.0);
                    var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(composite),
                        SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.Linear, 0, 0, false, 24);
                    var c = new SectionCheckerModelCode2010(new SectionCheckerAttribute(composite, null, null), options, new StandardNTC2018Concrete(), false, -1, new StandardEN1993p11());
                    c.SectionSolver.DomainCacheEnabled = cache;
                    return c;
                },
            };
            for (int k = 0; k < cases.Count; k++)
            {
                FailureDomainCache.Clear();
                var computed = cases[k](false);
                var cached = cases[k](true);
                string where = "case " + k;
                var axes = computed.SectionCheckerOptions.ForceReferenceCoordinateSystem;
                // twice: the second time from the cache of the solver
                for (int pass = 0; pass < 2; pass++)
                {
                    AssertSameDomain(computed.GetFailureDomainResult().Domain, cached.GetFailureDomainResult().Domain, where + " 3D");
                    AssertSameDomain(computed.SectionSolver.GetElasticFailureDomainResult().Domain, cached.SectionSolver.GetElasticFailureDomainResult().Domain, where + " elastic");
                    AssertSameDomain2d(computed.SectionSolver.GetPlasticFailureDomainResult2d(0.7).Domain, cached.SectionSolver.GetPlasticFailureDomainResult2d(0.7).Domain, where + " 2D");
                    AssertSameDomain2d(computed.GetFailureDomainResult2d().Domain, cached.GetFailureDomainResult2d().Domain, where + " 2D checker");
                    AssertSameDomain2d(computed.GetFailureDomainResult().CalculateDomainConstantAxialForce(new ResultBeamForces(-400e3, 0, 0, 0, 0, 0, axes)).Domain,
                        cached.GetFailureDomainResult().CalculateDomainConstantAxialForce(new ResultBeamForces(-400e3, 0, 0, 0, 0, 0, axes)).Domain, where + " constant N");
                    var range = computed.SectionSolver.CalculateAxialForceRange().Value;
                    var cachedRange = cached.SectionSolver.CalculateAxialForceRange().Value;
                    AssertSamePoint(range.Compression, cachedRange.Compression, where + " NRd,c");
                    AssertSamePoint(range.Tension, cachedRange.Tension, where + " NRd,t");
                    // intersection: one strategy for all the forces (the uncached solver makes its domain and mesh for each new strategy)
                    var forces = Forces(axes);
                    computed.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
                    cached.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
                    foreach (var force in forces)
                        AssertSamePoint(computed.CalculateFailureDomainPoint(force), cached.CalculateFailureDomainPoint(force), where + " intersection " + force.Id);
                    computed.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
                    cached.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
                    foreach (var force in forces.Where(f => pass == 0 && f.Id % 3 == 1))
                    {
                        var a = new LegacySectionCalculation(computed).SolveResistance(new SectionAnalysisInput(force));
                        var b = new LegacySectionCalculation(cached).SolveResistance(new SectionAnalysisInput(force));
                        Assert.AreEqual(a.Diagnostics.Status, b.Diagnostics.Status, where + " status " + force.Id);
                        Assert.AreEqual(a.Utilization.HasValue ? Bits(a.Utilization.Value) : 0, b.Utilization.HasValue ? Bits(b.Utilization.Value) : 0, where + " utilization " + force.Id);
                    }
                }
                Assert.IsTrue(cached.SectionSolver.DomainComputations < computed.SectionSolver.DomainComputations, where + ": the cache computed less");
            }
        }

        [TestMethod]
        public void IntersectionStrategyKeepsTheStateOfItsCreation()
        {
            // The strategy of the options is created by the solver constructor, before the checker assigns the 32 meridians of the options: its
            // domain has the 64 meridians of the solver default, as when it was computed in the constructor (kept bit for bit).
            FailureDomainCache.Clear();
            var forces = Forces(CoordinateSystem.Global);
            var fromOptions = NewChecker(NewSection(), teta: 32, strategy: SectionSolver.DomainPointStrategyTypes.Intersection);
            var with64 = NewChecker(NewSection(), teta: 64);
            with64.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            var with32 = NewChecker(NewSection(), teta: 32);
            with32.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            bool differs = false;
            foreach (var force in forces)
            {
                var p = fromOptions.CalculateFailureDomainPoint(force);
                AssertSamePoint(with64.CalculateFailureDomainPoint(force), p, "64 meridians " + force.Id);
                var q = with32.CalculateFailureDomainPoint(force);
                differs |= Bits(q.NRd) != Bits(p.NRd) || Bits(q.MxRd) != Bits(p.MxRd) || Bits(q.MyRd) != Bits(p.MyRd);
            }
            Assert.IsTrue(differs, "the 32 meridians give other points");
            Assert.AreEqual(32, fromOptions.SectionSolver.TetaDiscretization);
            Assert.AreEqual(32, fromOptions.GetFailureDomainResult().Domain.DomainPoints.Length, "the domain of the results follows the options");
        }

        [TestMethod]
        public void AciIntersectionOfTheOptionsKeepsTheTiedCoefficients()
        {
            // SectionSolverACI318 assigns the spiral after the base constructor, which created the strategy of the options: its domain was
            // computed without the spiral and still is (kept bit for bit); the domain of the results has the spiral.
            FailureDomainCache.Clear();
            SectionCheckerACI318 Aci(bool withSpiral, SectionSolver.DomainPointStrategyTypes strategy)
            {
                var section = GetRectangularSection4Rebars(300, 500, 20, 50, ConcreteMaterialACI318Data.Fc3000, SteelMaterialACI318Data.Grade60);
                var options = new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN,
                    SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64) { DomainPointStrategy = strategy };
                return new SectionCheckerACI318(new SectionCheckerAttribute(section, null, null), options, new StandardACI318p08(), withSpiral, false);
            }
            var spiralOptions = Aci(true, SectionSolver.DomainPointStrategyTypes.Intersection);
            var tied = Aci(false, SectionSolver.DomainPointStrategyTypes.Intersection);
            var spiral = Aci(true, SectionSolver.DomainPointStrategyTypes.Iterative);
            spiral.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            var axes = spiral.SectionCheckerOptions.ForceReferenceCoordinateSystem;
            bool differs = false;
            foreach (var force in Forces(axes))
            {
                var p = spiralOptions.CalculateFailureDomainPoint(force);
                AssertSamePoint(tied.CalculateFailureDomainPoint(force), p, "tied " + force.Id);
                var q = spiral.CalculateFailureDomainPoint(force);
                differs |= Bits(q.NRd) != Bits(p.NRd) || Bits(q.MxRd) != Bits(p.MxRd) || Bits(q.MyRd) != Bits(p.MyRd);
            }
            Assert.IsTrue(differs, "the spiral changes the domain");
            var a = spiral.GetFailureDomainResult().Domain.DomainPoints.SelectMany(m => m).Select(p => Bits(p.NRd)).ToArray();
            var b = tied.GetFailureDomainResult().Domain.DomainPoints.SelectMany(m => m).Select(p => Bits(p.NRd)).ToArray();
            Assert.IsFalse(a.SequenceEqual(b), "the spiral is in the key");
        }

        [TestMethod]
        public void ParallelCheckersShareTheDomain()
        {
            FailureDomainCache.Clear();
            var section = NewSection();
            var reference = NewChecker(NewSection(), cache: false);
            var expected = reference.GetFailureDomainResult().Domain;
            var forces = Forces(CoordinateSystem.Global);
            reference.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Intersection);
            var expectedPoints = forces.Select(f => reference.CalculateFailureDomainPoint(f)).ToArray();

            var results = new FailureDomain[24];
            var points = new FailureDomain.FailureDomainPoint[24][];
            var computations = new int[24];
            Parallel.For(0, 24, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var checker = NewChecker(i % 3 == 0 ? NewSection() : section, strategy: SectionSolver.DomainPointStrategyTypes.Intersection);
                // an option that the domain does not read
                if (i % 2 == 1)
                    checker.SectionCheckerOptions.StressAnalysisConcreteTension = ConcreteMaterial.TensionResponseDiagrams.TensionChord;
                results[i] = checker.GetFailureDomainResult().Domain;
                // the parallel searches of one checker share its strategy and its mesh
                points[i] = checker.SectionSolver.CalculateDomainPoints(forces);
                computations[i] = checker.SectionSolver.DomainComputations;
            });
            for (int i = 0; i < 24; i++)
            {
                AssertSameDomain(expected, results[i], "checker " + i);
                for (int j = 0; j < forces.Length; j++)
                    AssertSamePoint(expectedPoints[j], points[i][j], "checker " + i + " point " + j);
            }
            Assert.IsTrue(computations.Sum() < 24, "the checkers share the domain: " + computations.Sum() + " computations");
            Assert.IsTrue(FailureDomainCache.MeshBuilds <= computations.Sum() + 1, "one mesh per shared domain");
        }
    }
}
