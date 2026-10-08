using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ConcreteTests
{
    /// <summary>
    /// Contract of the points of the failure domain at an assigned axial force of GPCChecker.Concrete 0.0.17.0 (ANTHEA F2.1, divergence S-1):
    /// regression snapshot of <see cref="SectionSolver.CalculateDomainPoint(ResultBeamForces[])"/>, captured from the code of Checker develop
    /// 4f54139a before the tolerance on N that depends on the concrete diagram was added. The search is configured as ANTHEA's resistance at an
    /// assigned axial force (SectionMomentResistance of X.Calculations): N constant, local axes (centroid, -X, -Y), iterative strategy with the
    /// fallbacks of the solver, non linear model, 32 angular subdivisions, no tensile concrete, one force per call.
    /// Sections: the five sections in closed form of the F2.1 bench of ANTHEA (R1 300 × 500 3Ø20 + 2Ø16 C30/37, R2 400 × 600 4 + 4Ø20 C35/45,
    /// R3 300 × 500 3Ø20 C25/30, C1 polygon of 72 sides D 600 8Ø20 C30/37, C2 polygon of 144 sides D 800 12Ø16 C40/50, bars of attesi.json),
    /// NTC 2018 (αcc 0.85, γc 1.5, γs 1.15), B450C elastic perfectly plastic (εuk 7.5 %). For every section the four diagrams of ANTHEA
    /// (parabola-rectangle, bilinear, stress block, non linear), the plastic (ULS) and elastic (SLV) domains, 6 or 7 axial forces from tension
    /// to 0.8 NRd,c (R1 also beyond NRd,c; C2 also the N of S-1, -1500 kN) and the directions Mx+, Mx-, My+, My-.
    /// For every point: the path of the solver (iterative, closest point, bisection, intersection, read from the log), the point (NRd, MxRd,
    /// MyRd, failure field, immersion, id and curvatures of the strain plane) or the type of the exception, |NRd - N| and the verdict of the rule
    /// on N used by ANTHEA up to 0.0.17.0: accepted if |NRd - N| ≤ max(1000 N; 1e-6 |N|) (in N and, as ANTHEA compares, in kN).
    /// Doubles are written with the round-trip format (G17 where "R" does not give the same bits back), so the comparison is bit for bit.
    /// Generation runs with the invariant culture and UI culture; x64 test platform as the rest of the suite. The values hold for the runtime of
    /// this suite (.NET Framework 4.7.2): with the stress block the path of the search is chaotic and another runtime can give another point for
    /// the same input (C2, N = -1500 kN, Mx+: NRd = -1513.15 kN here, -1502.05 kN with the same section on .NET 8, the runtime of ANTHEA).
    /// It is a regression snapshot, not an independent expectation: never regenerate it to make the test pass. On a difference the generated text is
    /// written next to the test assembly (domain-point-contract.actual.json) and the first differing line is reported.
    /// </summary>
    [TestClass]
    public class DomainPointContractTests
    {
        internal const string FileName = "domain-point-contract.json";

        [TestMethod]
        public void DomainPointContractOf0017IsUnchanged()
        {
            int count;
            string actual = Generate(out count);
            int expectedCount = Sections.Sum(s => s.AxialKn.Length) * Diagrams.Length * States.Length * Directions.Length;
            Assert.AreEqual(expectedCount, count, "points");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FileName);
            string actualPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "domain-point-contract.actual.json");
            if (!File.Exists(path))
            {
                File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
                Assert.Fail("Missing " + path + "; generated text written to " + actualPath);
            }
            // The working tree may hold CRLF (core.autocrlf): the contract is compared with LF line ends.
            string expected = File.ReadAllText(path, new UTF8Encoding(false)).Replace("\r\n", "\n");
            if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            var e = expected.Split('\n'); var a = actual.Split('\n');
            int line = 0;
            while (line < e.Length && line < a.Length && string.Equals(e[line], a[line], StringComparison.Ordinal)) line++;
            Assert.Fail("Domain point contract 0.0.17.0 changed at line " + (line + 1) + "\nexpected: " + (line < e.Length ? e[line] : "<end>")
                + "\nactual:   " + (line < a.Length ? a[line] : "<end>") + "\nGenerated text: " + actualPath);
        }

        // ---------------------------------------------------------------- bench

        /// <summary>A section of the F2.1 bench of ANTHEA (contour centred in the origin, bars x, y, Ø in mm) with the axial forces of the contract</summary>
        internal sealed class BenchSection
        {
            public string Name;
            public double Width;
            public double Height;
            public double Diameter;
            public int Sides;
            public double Fck;
            public double[][] Bars;
            public double[] AxialKn;
        }

        // Bars as in attesi.json of the bench (genera_attesi.py); axial forces in kN, compression negative, chosen from tension to about 0.8 NRd,c.
        internal static readonly BenchSection[] Sections =
        {
            new BenchSection
            {
                Name = "R1", Width = 300, Height = 500, Fck = 30,
                Bars = new[] { new double[] { -100, -200, 20 }, new double[] { 0, -200, 20 }, new double[] { 100, -200, 20 }, new double[] { -100, 205, 16 }, new double[] { 100, 205, 16 } },
                AxialKn = new double[] { 250, 0, -150, -500, -1200, -2400, -5000 },
            },
            new BenchSection
            {
                Name = "R2", Width = 400, Height = 600, Fck = 35,
                Bars = new[]
                {
                    new double[] { -150, -250, 20 }, new double[] { -50, -250, 20 }, new double[] { 50, -250, 20 }, new double[] { 150, -250, 20 },
                    new double[] { -150, 250, 20 }, new double[] { -50, 250, 20 }, new double[] { 50, 250, 20 }, new double[] { 150, 250, 20 },
                },
                AxialKn = new double[] { 490, 0, -300, -1000, -2800, -4500 },
            },
            new BenchSection
            {
                Name = "R3", Width = 300, Height = 500, Fck = 25,
                Bars = new[] { new double[] { -100, -200, 20 }, new double[] { 0, -200, 20 }, new double[] { 100, -200, 20 } },
                AxialKn = new double[] { 180, 0, -120, -500, -1200, -2000 },
            },
            new BenchSection
            {
                Name = "C1", Diameter = 600, Sides = 72, Fck = 30,
                Bars = new[]
                {
                    new[] { 221.73108780270883, 91.84402376762155, 20 }, new[] { 91.84402376762156, 221.73108780270883, 20 },
                    new[] { -91.84402376762154, 221.73108780270883, 20 }, new[] { -221.73108780270883, 91.84402376762158, 20 },
                    new[] { -221.73108780270886, -91.84402376762152, 20 }, new[] { -91.84402376762148, -221.73108780270886, 20 },
                    new[] { 91.84402376762161, -221.7310878027088, 20 }, new[] { 221.73108780270886, -91.8440237676215, 20 },
                },
                AxialKn = new double[] { 490, 0, -300, -800, -2900, -4600 },
            },
            new BenchSection
            {
                Name = "C2", Diameter = 800, Sides = 144, Fck = 40,
                Bars = new[]
                {
                    new[] { 318.75552267539257, 85.41028488383185, 16 }, new[] { 233.3452377915607, 233.3452377915607, 16 },
                    new[] { 85.41028488383185, 318.75552267539257, 16 }, new[] { -85.41028488383188, 318.75552267539257, 16 },
                    new[] { -233.34523779156066, 233.3452377915607, 16 }, new[] { -318.7555226753925, 85.41028488383193, 16 },
                    new[] { -318.75552267539257, -85.41028488383186, 16 }, new[] { -233.34523779156075, -233.34523779156066, 16 },
                    new[] { -85.4102848838318, -318.75552267539257, 16 }, new[] { 85.4102848838317, -318.75552267539257, 16 },
                    new[] { 233.34523779156063, -233.34523779156075, 16 }, new[] { 318.75552267539257, -85.41028488383182, 16 },
                },
                AxialKn = new double[] { 470, 0, -600, -1500, -3700, -6100, -9800 },
            },
        };

        /// <summary>The diagrams of ANTHEA (ConcreteMaterials.ConcreteDiagrams), in its order</summary>
        internal static readonly ConcreteMaterial.CompressionStressStrainDiagrams[] Diagrams =
        {
            ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear,
            ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock, ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear,
        };

        /// <summary>ULS (plastic domain) and SLV (elastic domain), as the states SLU and SLV of ANTHEA</summary>
        internal static readonly SectionSolver.FailureDomainTypes[] States = { SectionSolver.FailureDomainTypes.Plastic, SectionSolver.FailureDomainTypes.Elastic };

        /// <summary>The four directions of SectionMomentResistance (moments of 1 kNm)</summary>
        internal static readonly (string Label, double Mx, double My)[] Directions = { ("Mx+", 1, 0), ("Mx-", -1, 0), ("My+", 0, 1), ("My-", 0, -1) };

        /// <summary>The section as CheckerSection.PrepareModel of ANTHEA builds it (contour vertices and bars in the same order and arithmetic)</summary>
        internal static ReinforcedConcreteSection BuildSection(BenchSection s, ConcreteMaterial.CompressionStressStrainDiagrams diagram)
        {
            var concrete = new ConcreteMaterialEN1992(s.Name, s.Fck, diagram);
            var steel = new SteelMaterial("B450C", 200000, 450, 450, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var outline = new List<Point2d>();
            if (s.Sides > 0)
            {
                double radius = s.Diameter / 2;
                for (int i = 0; i < s.Sides; i++)
                    outline.Add(new Point2d(radius * Math.Cos(2 * Math.PI * i / s.Sides), radius * Math.Sin(2 * Math.PI * i / s.Sides)));
            }
            else
            {
                outline.Add(new Point2d(-s.Width / 2, -s.Height / 2));
                outline.Add(new Point2d(s.Width / 2, -s.Height / 2));
                outline.Add(new Point2d(s.Width / 2, s.Height / 2));
                outline.Add(new Point2d(-s.Width / 2, s.Height / 2));
            }
            var section = new ReinforcedConcreteSection(new Shape2d(new Polygon2d(outline), new Polygon2d[0]), concrete);
            foreach (var bar in s.Bars)
                section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(bar[2], steel), new Point2d(bar[0], bar[1])) });
            return section;
        }

        /// <summary>The local axes of the section, as ANTHEA (centroid, -X, -Y)</summary>
        internal static CoordinateSystem LocalAxes(ReinforcedConcreteSection section) =>
            new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));

        /// <summary>The checker as CheckerSection of ANTHEA for the resistance at an assigned axial force (NTC 2018, N constant, iterative strategy)</summary>
        internal static SectionCheckerModelCode2010 BuildChecker(ReinforcedConcreteSection section, SectionSolver.FailureDomainTypes state)
        {
            var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(LocalAxes(section), SectionSolver.FailureAnalysisTypes.ConstantN, state,
                SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        /// <summary>The force of a direction at the axial force (kN), in the local axes, as CheckerSection.Force of ANTHEA</summary>
        internal static ResultBeamForces Force(CoordinateSystem local, double axialKn, double mx, double my) =>
            new ResultBeamForces(axialKn * 1000, 0, 0, 0, mx * 1e6, my * 1e6, new CoordinateSystem(local), 1).ToCoordinateSystemWithEccentricity(local);

        /// <summary>The rule on N of ANTHEA up to GPCChecker.Concrete 0.0.17.0, in N: max(1000 N; 1e-6 |N|)</summary>
        internal static double ToleranceOf0017(double axialForce) => Math.Max(1000, 1e-6 * Math.Abs(axialForce));

        // ---------------------------------------------------------------- generation

        private static string Generate(out int count)
        {
            var thread = Thread.CurrentThread;
            var culture = thread.CurrentCulture; var uiCulture = thread.CurrentUICulture;
            try
            {
                thread.CurrentCulture = CultureInfo.InvariantCulture; thread.CurrentUICulture = CultureInfo.InvariantCulture;
                var items = new List<string>();
                foreach (var s in Sections)
                    foreach (var diagram in Diagrams)
                    {
                        var section = BuildSection(s, diagram);
                        foreach (var state in States)
                        {
                            var checker = BuildChecker(section, state);
                            var solver = checker.SectionSolver;
                            var local = LocalAxes(section);
                            foreach (double axialKn in s.AxialKn)
                                foreach (var direction in Directions)
                                    items.Add(Point(s.Name, diagram, state, solver, local, axialKn, direction));
                        }
                    }
                count = items.Count;
                var text = new StringBuilder();
                text.Append("{\n");
                text.Append("  \"contract\": ").Append(S("Domain points at an assigned axial force of GPCChecker.Concrete 0.0.17.0 (ANTHEA F2.1, S-1): SectionSolver.CalculateDomainPoint as SectionMomentResistance of ANTHEA, and the rule on N of 0.0.17.0")).Append(",\n");
                text.Append("  \"source\": ").Append(S("Checker develop 4f54139a before the tolerance on N of S-1; Model 5ad56681; regression snapshot, not an independent expectation")).Append(",\n");
                text.Append("  \"units\": ").Append(S("N, Nmm; compression negative; tolerance of 0.0.17.0 = max(1000; 1e-6 |N|); acceptedKn = the same rule compared in kN as ANTHEA")).Append(",\n");
                text.Append("  \"points\": [\n").Append(string.Join(",\n", items)).Append("\n  ]\n");
                text.Append("}\n");
                return text.ToString();
            }
            finally { thread.CurrentCulture = culture; thread.CurrentUICulture = uiCulture; }
        }

        private static string Point(string name, ConcreteMaterial.CompressionStressStrainDiagrams diagram, SectionSolver.FailureDomainTypes state, SectionSolver solver,
            CoordinateSystem local, double axialKn, (string Label, double Mx, double My) direction)
        {
            double axial = axialKn * 1000;
            var fields = new List<string>
            {
                F("section", S(name)), F("diagram", S(diagram.ToString())), F("state", S(state.ToString())), F("N", N(axial)), F("direction", S(direction.Label)),
            };
            int before = solver.GetLog().Count;
            FailureDomain.FailureDomainPoint point = null;
            Exception failure = null;
            try { point = solver.CalculateDomainPoint(new[] { Force(local, axialKn, direction.Mx, direction.My) })[0]; }
            catch (Exception e) { failure = e; }
            var log = solver.GetLog().Skip(before).ToList();
            fields.Add(F("path", S(PathOf(log))));
            fields.Add(F("logLines", log.Count.ToString(CultureInfo.InvariantCulture)));
            if (failure != null)
            {
                fields.Add(F("exception", S(failure.GetType().FullName)));
                if (failure.InnerException != null) fields.Add(F("inner", S(failure.InnerException.GetType().FullName)));
                fields.Add(F("accepted", "false"));
                fields.Add(F("acceptedKn", "false"));
            }
            else if (point == null)
            {
                fields.Add(F("point", "null"));
                fields.Add(F("accepted", "false"));
                fields.Add(F("acceptedKn", "false"));
            }
            else
            {
                fields.Add(F("NRd", N(point.NRd)));
                fields.Add(F("MxRd", N(point.MxRd)));
                fields.Add(F("MyRd", N(point.MyRd)));
                fields.Add(F("failureIndex", S(point.FailureIndex.ToString())));
                fields.Add(F("immersione", N(point.Immersione)));
                if (point.StrainPlane != null)
                {
                    fields.Add(F("planeId", point.StrainPlane.Id.ToString(CultureInfo.InvariantCulture)));
                    fields.Add(F("chiX", N(point.StrainPlane.ChiX)));
                    fields.Add(F("chiY", N(point.StrainPlane.ChiY)));
                    fields.Add(F("strain", N(point.StrainPlane.StrainReferencePoint)));
                }
                double deltaN = Math.Abs(point.NRd - axial);
                bool finite = !double.IsNaN(point.NRd + point.MxRd + point.MyRd) && !double.IsInfinity(point.NRd + point.MxRd + point.MyRd);
                double tolerance = ToleranceOf0017(axial);
                bool accepted = finite && deltaN <= tolerance;
                // As SectionMomentResistance of ANTHEA: kN, rejected if |N - axial| > max(1; 1e-6 |axial|)
                bool acceptedKn = finite && !(Math.Abs(point.NRd / 1000 - axialKn) > Math.Max(1, Math.Abs(axialKn) * 1e-6));
                fields.Add(F("deltaN", N(deltaN)));
                fields.Add(F("tolerance", N(tolerance)));
                fields.Add(F("accepted", accepted ? "true" : "false"));
                fields.Add(F("acceptedKn", acceptedKn ? "true" : "false"));
            }
            return "    {" + string.Join(", ", fields) + "}";
        }

        /// <summary>The path of the solver from the lines added to its log by the search</summary>
        private static string PathOf(List<string> log)
        {
            if (log.Contains("Iterative and bisection strategies failed: intersection strategy used")) return "intersection";
            if (log.Contains("Iterative strategy failed: bisection strategy used")) return "bisection";
            if (log.Contains("Fail to calculate point on domain")) return "closest";
            return log.Count == 0 ? "iterative" : "iterative+log";
        }

        // ---------------------------------------------------------------- JSON text

        private static string F(string key, string json) => S(key) + ": " + json;

        private static string S(string value)
        {
            if (value == null) return "null";
            var b = new StringBuilder("\"");
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(ch);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        /// <summary>Round-trip text of a double: "R", G17 where "R" does not give the same bits back, -0 kept, non-finite values as strings.</summary>
        internal static string N(double v)
        {
            if (double.IsNaN(v)) return "\"NaN\"";
            if (double.IsPositiveInfinity(v)) return "\"Infinity\"";
            if (double.IsNegativeInfinity(v)) return "\"-Infinity\"";
            if (v == 0) return BitConverter.DoubleToInt64Bits(v) == 0 ? "0" : "-0";
            string r = v.ToString("R", CultureInfo.InvariantCulture);
            if (BitConverter.DoubleToInt64Bits(double.Parse(r, CultureInfo.InvariantCulture)) != BitConverter.DoubleToInt64Bits(v)) r = v.ToString("G17", CultureInfo.InvariantCulture);
            return r;
        }
    }
}
