using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.Viviani;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests.Viviani;

/// <summary>
/// Comparison of the method of prof. Viviani with the calculations of the library: the plastic moment with the exact integration of rectangular
/// blocks, the elastic stresses with the linear calculation of HBridgeSection. The differences found are described in NOTE-metodo-Viviani.md
/// </summary>
[TestClass]
public class VivianiComparisonTests
{
    /// <summary>Where the plastic neutral axis of the method is, from its level</summary>
    private static string PlasticBranch(VivianiInput d, VivianiResult r)
    {
        double y = r.PlasticNeutralAxis, tpi = d.BottomFlangeThickness, top = tpi + d.WebHeight, steel = top + d.TopFlangeThickness;
        string zone = y <= tpi ? "piattabanda inferiore" : y < top ? "anima" : y <= steel ? "piattabanda superiore" : "soletta";
        return (r.MEd >= 0 ? "M+ " : "M- ") + zone;
    }

    [TestMethod]
    public void PlasticMomentIsExactExceptTwoBranches()
    {
        // on random data the plastic moment of the method is the exact one of the rectangular blocks, apart from two branches of the program:
        // M+ with the neutral axis in the bottom flange (the moment is not divided by 1000: 1000 times the exact one) and M- with all the steel
        // compressed (the force of the reinforcement is (Af fyd + steel) / 2 instead of the one in equilibrium with the steel)
        var random = new Random(27092026);
        var counts = new Dictionary<string, int>();
        int bottomFlange = 0, allCompressed = 0, exact = 0;
        for (int i = 0; i < 20000; i++)
        {
            VivianiInput d = VivianiEquivalenceTests.Random(random);
            if (i % 3 == 0)
                d = d with { SlabWidth = d.SlabWidth * 0.2, TopFlangeWidth = d.TopFlangeWidth * 0.5, Fcd = d.Fcd * 0.3 };
            if (i % 7 == 0)
                d = d with { RebarArea = d.RebarArea * 8, SlabHeight = Math.Max(d.SlabHeight, 10), SlabWidth = Math.Max(d.SlabWidth, 50) };
            VivianiResult r = VivianiMethod.Calculate(d);
            if (r.MRd == 0 || double.IsNaN(r.MRd))
                continue;
            string branch = PlasticBranch(d, r);
            counts[branch] = counts.TryGetValue(branch, out int c) ? c + 1 : 1;
            double library = VivianiComparisons.PlasticMoment(d, r);
            if (branch == "M+ piattabanda inferiore")
            {
                Assert.AreEqual(1000 * library, r.MRd, 1e-6 * Math.Abs(r.MRd), $"caso {i}: {branch}");
                bottomFlange++;
            }
            else if (branch == "M- piattabanda superiore" && Math.Abs(r.PlasticNeutralAxis - (d.BottomFlangeThickness + d.WebHeight + d.TopFlangeThickness)) < 1e-12)
            {
                // neutral axis at the top of the steel: the moment of the method is larger than the exact one
                Assert.IsTrue(Math.Abs(r.MRd) > Math.Abs(library), $"caso {i}: {branch}");
                allCompressed++;
            }
            else
            {
                Assert.AreEqual(library, r.MRd, 1e-6 * Math.Abs(library) + 1e-6, $"caso {i}: {branch}");
                exact++;
            }
        }
        Console.WriteLine(string.Join("\n", counts.OrderBy(k => k.Key).Select(k => $"{k.Key}: {k.Value}")));
        Console.WriteLine($"esatti {exact}, piattabanda inferiore {bottomFlange}, acciaio tutto compresso {allCompressed}");
        Assert.IsTrue(exact > 5000 && bottomFlange > 0, $"esatti {exact}, piattabanda inferiore {bottomFlange}");
    }

    [TestMethod]
    public void PlasticMomentInTheBottomFlangeIsThousandTimesLarger()
    {
        // the branch case of the history: M Rd of the program 1000 times the exact one, so η = M Ed / M Rd is 1000 times too small
        var d = VivianiEquivalenceTests.BranchCases().Single(c => c.Name == "asse plastico in piattabanda inferiore").Input;
        VivianiResult r = VivianiMethod.Calculate(d);
        Assert.IsTrue(r.PlasticNeutralAxis <= d.BottomFlangeThickness);
        double exact = VivianiComparisons.PlasticMoment(d, r);
        Assert.AreEqual(1000, r.MRd / exact, 1e-6);
        Assert.IsTrue(r.Utilization < 1e-2);
    }

    /// <summary>Girder of a road bridge in the units of the library (mm, kN, kNm)</summary>
    internal static HBridgeInput Girder(SteelMaterial? rebar = null, double phiLong = 2, bool class4 = false) => new()
    {
        Materials = new(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, rebar ?? SteelMaterialEN1992Data.B450C),
        Geometry = new() { SlabWidth = 3500, SlabHeight = 250, WebHeight = 1700, WebThickness = 16, TopWidth = 600, TopThickness = 30, BottomWidth = 800, BottomThickness = 40 },
        TopRebars = new() { Enabled = true, Diameter = 20, Pitch = 100, AxisDistance = 50 },
        Options = new() { Class4 = class4, AlphaCC = 0.85, GammaC = 1.5, GammaM0 = 1.05, GammaS = 1.15 },
        Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 5300, ShearKN = 620 },
            new() { Name = "G2", Kind = BridgePhaseKind.Composite, MomentKNm = 1650, ShearKN = 180, Phi = phiLong, PsiL = 1.1 },
            new() { Name = "Q", Kind = BridgePhaseKind.Composite, MomentKNm = 7800, ShearKN = 950 }]
    };

    private static void Print(VivianiComparison c)
    {
        Console.WriteLine("punto                          Viviani    libreria   differenza");
        foreach (var p in c.Points)
            Console.WriteLine($"{p.Point} {p.Name,-28} {p.Viviani,9:F3} {p.Library,9:F3} {p.Difference,9:F3}");
        Console.WriteLine($"M Rd Viviani {c.Viviani.MRd:F1} kNm, integrazione esatta {c.LibraryPlasticMoment:F1} kNm; classe elastica {c.Viviani.ElasticClass}, fessurata {c.Viviani.CrackedSlab}");
        foreach (string n in c.Notes) Console.WriteLine("nota: " + n);
    }

    [TestMethod]
    public void SameHypothesesGiveTheSameStresses()
    {
        // reinforcement with the modulus of the structural steel and without it: the two calculations are the same transformed section
        var rebar = new SteelMaterial("barre con Ea", 210000, 450, 540, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        VivianiComparison c = VivianiComparisons.Compare(Girder(rebar) with { TopRebars = new() });
        Print(c);
        Assert.IsFalse(c.Viviani.CrackedSlab);
        // the library integrates the plates on their middle line (Checker): without their own inertia, 1e-5 of the inertia of the section
        double scale = c.Points.Max(p => Math.Abs(p.Library));
        foreach (var p in c.Points.Where(p => p.Point != 2))
            Assert.AreEqual(p.Library, p.Viviani, 1e-4 * scale, p.Name);
    }

    [TestMethod]
    public void ReinforcementDifferencesAreSmall()
    {
        // with the reinforcement: the method adds the bars to the gross slab and uses Ea for them; the library removes the concrete of the bars
        // and uses Es. The differences are small (below 0.5% of the stresses of the steel)
        VivianiComparison c = VivianiComparisons.Compare(Girder());
        Print(c);
        double scale = c.Points.Where(p => p.Point >= 3).Max(p => Math.Abs(p.Library));
        double esOverEa = 200000.0 / 210000.0;
        foreach (var p in c.Points)
        {
            // the stress of the bars of the library is Es ε, the one of the method Ea ε
            double viviani = p.Point == 2 ? p.Viviani * esOverEa : p.Viviani;
            Assert.AreEqual(p.Library, viviani, 5e-3 * scale, p.Name);
        }
        Assert.IsTrue(c.Notes.Any(n => n.Contains("Es/Ea")));
    }

    [TestMethod]
    public void NegativeMomentCracksTheSlabInTheMethod()
    {
        // hogging: the method cracks the slab automatically; the library needs the phases with the excluded slab
        var data = Girder() with
        {
            Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = -6200, ShearKN = 1400 },
                new() { Name = "G2", Kind = BridgePhaseKind.Composite, MomentKNm = -1900, ShearKN = 400, Phi = 2, PsiL = 1.1 },
                new() { Name = "Q", Kind = BridgePhaseKind.Composite, MomentKNm = -5600, ShearKN = 1900 }]
        };
        VivianiComparison c = VivianiComparisons.Compare(data);
        Print(c);
        Assert.IsTrue(c.Viviani.CrackedSlab);
        Assert.AreEqual(0, c.Viviani.Sigma1);
        Assert.IsTrue(c.Points.Single(p => p.Point == 1).Library > 0, "the library keeps the uncracked slab of the composite phases");
        Assert.IsTrue(c.Notes.Any(n => n.Contains("fessurata")));
        // the cracked section of the method equals the library with excluded slab for G2 and Q, apart from Es / Ea
        var excluded = data with { Phases = data.Phases.Select(p => p.Kind == BridgePhaseKind.Composite ? p with { Kind = BridgePhaseKind.ConcreteExcluded } : p).ToArray() };
        HBridgeAnalysisResult library = HBridgeSection.Calculate(excluded);
        double bottom = library.Stages.Last().Contributions.Sum(x => x.Stress("Acciaio", -library.Geometry.Height));
        Assert.AreEqual(bottom, c.Viviani.Sigma6, 0.01 * Math.Abs(bottom));
    }

    [TestMethod]
    public void EffectiveWebOfTheMethod()
    {
        // slender web in class 4: the method reduces only the web with 4 iterations and ε from fyd; compare with the library
        var data = Girder(class4: true) with { Geometry = Girder().Geometry with { WebThickness = 12, WebHeight = 2200 } };
        VivianiComparison c = VivianiComparisons.Compare(data);
        Print(c);
        Assert.AreEqual(4, c.Viviani.ElasticClass);
        foreach (var p in c.Points.Where(p => p.Point >= 3))
            Assert.AreEqual(p.Library, p.Viviani, 0.05 * Math.Abs(p.Library), p.Name);
    }
}
