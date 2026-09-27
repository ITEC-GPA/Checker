using GPC.Checkers.CompositeBridge.Viviani;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests.Viviani;

/// <summary>
/// The clean rewrite of the method of prof. Viviani gives exactly the results of the original program: every output (and colour) of
/// <see cref="VivianiMethod"/> is compared, bit for bit, with <see cref="VivianiOriginal"/>, the mechanical extraction of the decompiled
/// calculation, on random data and on data chosen to run every branch
/// </summary>
[TestClass]
public class VivianiEquivalenceTests
{
    /// <summary>Runs the original calculation on the data</summary>
    internal static VivianiOriginal Original(VivianiInput d)
    {
        var o = new VivianiOriginal
        {
            inTextBox2 = d.SlabHeight, inTextBox9 = d.SlabWidth, inTextBox18 = d.RebarCover, inTextBox24 = d.RebarArea,
            inTextBox8 = d.TopFlangeThickness, inTextBox7 = d.TopFlangeWidth, inTextBox4 = d.WebHeight, inTextBox1 = d.WebThickness,
            inTextBox3 = d.BottomFlangeThickness, inTextBox17 = d.BottomFlangeWidth, inTextBox5 = d.ShortTermModularRatio, inTextBox6 = d.LongTermModularRatio,
            inTextBox16 = d.RebarFyd, inTextBox32 = d.SteelOnly.N, inTextBox27 = d.LongTerm.N, inTextBox11 = d.ShortTerm.N,
            inTextBox33 = d.SteelOnly.T, inTextBox28 = d.LongTerm.T, inTextBox30 = d.ShortTerm.T,
            inTextBox34 = d.SteelOnly.M, inTextBox29 = d.LongTerm.M, inTextBox31 = d.ShortTerm.M,
            inTextBox45 = d.Fcd, inTextBox46 = d.TopFlangeFyd, inTextBox47 = d.WebFyd, inTextBox48 = d.BottomFlangeFyd, inTextBox62 = d.Iterations,
            inTextBox54 = d.GammaG1, inTextBox55 = d.GammaG2, inTextBox56 = d.GammaQ, inCheckBox1 = d.FactoredElasticStresses
        };
        o.Button1_Click();
        return o;
    }

    private static VivianiColor Color(string name) => string.IsNullOrEmpty(name) ? VivianiColor.Black : Enum.Parse<VivianiColor>(name);

    /// <summary>Every output of the program and the state the rewrite exposes</summary>
    internal static void AssertSame(VivianiOriginal o, VivianiResult r, string label)
    {
        void Same(double expected, double actual, string name)
        {
            if (!expected.Equals(actual))
                Assert.Fail($"{label}: {name} originale {expected:R}, riscritto {actual:R}");
        }
        Same(o.outTextBox40, r.SteelSection.Area, "ainf"); Same(o.outTextBox44, r.SteelSection.Centroid, "yginf"); Same(o.outTextBox38, r.SteelSection.Inertia, "jxinf");
        Same(o.outTextBox36, r.LongTermSection.Area, "aninf"); Same(o.outTextBox42, r.LongTermSection.Centroid, "ygninf"); Same(o.outTextBox39, r.LongTermSection.Inertia, "jxninf");
        Same(o.outTextBox37, r.ShortTermSection.Area, "an0"); Same(o.outTextBox43, r.ShortTermSection.Centroid, "ygn0"); Same(o.outTextBox41, r.ShortTermSection.Inertia, "jxn0");
        Same(o.outTextBox22, r.Sigma1, "sig1"); Same(o.outTextBox35, r.Sigma2, "sig2"); Same(o.outTextBox19, r.Sigma3, "sig3"); Same(o.outTextBox23, r.Sigma4, "sig4");
        Same(o.outTextBox12, r.Sigma5, "sig5"); Same(o.outTextBox15, r.Sigma6, "sig6");
        Same(o.outTextBox14, r.SigmaId3, "sigid3"); Same(o.outTextBox21, r.SigmaId4, "sigid4"); Same(o.outTextBox25, r.SigmaId5, "sigid5"); Same(o.outTextBox26, r.SigmaId6, "sigid6");
        Same(o.outTextBox10, r.Tau, "tau4"); Same(o.tau5, r.Tau, "tau5"); Same(o.outTextBox20, r.ShearFlow, "taub");
        Same(o.outTextBox57, r.MEd, "mtot"); Same(o.outTextBox58, r.TEd, "ttot"); Same(o.outTextBox59, r.MRd, "mpl"); Same(o.outTextBox13, r.Utilization, "nibeta");
        Same(o.outTextBox60, r.ElasticNeutralAxis, "yel"); Same(o.outTextBox61, r.PlasticNeutralAxis, "ypl");
        Same(o.outTextBox49, r.ElasticLimit, "cte"); Same(o.outTextBox50, r.WebSlenderness, "ctr"); Same(o.outTextBox52, r.PlasticLimit, "ctp");
        Same(o.outTextBox51, r.ElasticClass, "cle"); Same(o.outTextBox53, r.PlasticClass, "clp"); Same(o.outTextBox1, r.ShownWebThickness, "taw");
        Same(o.be1, r.EffectiveWebTop, "be1"); Same(o.be2, r.EffectiveWebBottom, "be2"); Same(o.iter, r.IterationsPerformed, "iter"); Same(o.fyda, r.WebFydForBending, "fyda");
        Assert.AreEqual(o.par1 > 1E-05 | o.hs <= 0.0, r.CrackedSlab, label + ": sezione fessurata");
        Assert.AreEqual(Color(o.colorTextBox1), r.WebThicknessColor, label + ": colore Tw");
        Assert.AreEqual(Color(o.colorTextBox13), r.UtilizationColor, label + ": colore η");
        Assert.AreEqual(Color(o.colorTextBox24), r.RebarAreaColor, label + ": colore Af");
        Assert.AreEqual(Color(o.colorTextBox47), r.WebFydColor, label + ": colore fyd anima");
        Assert.AreEqual(Color(o.colorTextBox51), r.ElasticClassColor, label + ": colore classe elastica");
        Assert.AreEqual(Color(o.colorTextBox53), r.PlasticClassColor, label + ": colore classe plastica");
        Assert.AreEqual(o.colorTextBox1 == "Red", r.ShearExceedsWebResistance, label);
    }

    /// <summary>Random data in the ranges of the bridge girders, with some sections without slab, slender webs, high shear and negative moments</summary>
    internal static VivianiInput Random(Random random)
    {
        double U(double a, double b) => a + (b - a) * random.NextDouble();
        bool slab = random.NextDouble() > 0.1;
        double hs = slab ? U(15, 35) : 0;
        double sign = random.NextDouble() < 0.3 ? -1 : 1;
        double scale = U(0.2, 3.0);
        return new VivianiInput
        {
            SlabHeight = hs, SlabWidth = slab ? U(100, 450) : 0, RebarCover = slab ? U(3, Math.Max(3.5, hs - 3)) : 0, RebarArea = slab ? U(0, 80) : 0,
            TopFlangeThickness = U(1.2, 5), TopFlangeWidth = U(25, 90), WebHeight = U(50, 280), WebThickness = U(0.8, 2.6),
            BottomFlangeThickness = U(1.5, 7), BottomFlangeWidth = U(35, 130),
            ShortTermModularRatio = U(5.5, 7), LongTermModularRatio = U(14, 22), RebarFyd = 391.3, Fcd = U(11, 25),
            TopFlangeFyd = U(200, 420), WebFyd = U(200, 420), BottomFlangeFyd = U(200, 420),
            SteelOnly = new(U(-300, 300), U(0, 800) * scale, sign * U(0, 3000) * scale),
            LongTerm = new(U(-300, 300), U(0, 500) * scale, sign * U(0, 2500) * scale),
            ShortTerm = new(U(-300, 300), U(0, 1500) * scale, (random.NextDouble() < 0.15 ? -sign : sign) * U(0, 6000) * scale),
            GammaG1 = U(1, 1.35), GammaG2 = U(1, 1.5), GammaQ = U(1, 1.5), FactoredElasticStresses = random.NextDouble() < 0.5,
            Iterations = random.Next(0, 7)
        };
    }

    [TestMethod]
    public void RandomDataGiveTheSameResults()
    {
        var random = new Random(20260926);
        var branches = new HashSet<string>();
        for (int i = 0; i < 5000; i++)
        {
            VivianiInput d = Random(random);
            VivianiOriginal o = Original(d);
            VivianiResult r = VivianiMethod.Calculate(d);
            AssertSame(o, r, $"caso {i}");
            branches.Add($"cle={o.cle} clp={o.clp} fessurata={o.par1 > 1E-05 | o.hs <= 0.0} ridotta={o.iterp} taglio={o.colorTextBox1}");
        }
        // the random data run many combinations of classes and branches
        Assert.IsTrue(branches.Count >= 20, string.Join("\n", branches));
    }

    /// <summary>Data chosen to run the single branches: neutral axis in every part, both signs of the moment, slender webs, high shear</summary>
    internal static IEnumerable<(string Name, VivianiInput Input)> BranchCases()
    {
        var girder = new VivianiInput
        {
            SlabHeight = 25, SlabWidth = 300, RebarCover = 4, RebarArea = 25, TopFlangeThickness = 3, TopFlangeWidth = 50, WebHeight = 180, WebThickness = 1.4,
            BottomFlangeThickness = 4, BottomFlangeWidth = 70, SteelOnly = new(0, 300, 1500), LongTerm = new(0, 200, 2000), ShortTerm = new(0, 600, 3500)
        };
        yield return ("trave tipo", girder);
        yield return ("asse plastico in soletta", girder with { SlabWidth = 600, SlabHeight = 30 });
        yield return ("asse plastico all'armatura", girder with { SlabWidth = 90, RebarArea = 60, RebarCover = 6, SlabHeight = 20 });
        yield return ("asse plastico in piattabanda superiore", girder with { SlabWidth = 80, SlabHeight = 15, RebarArea = 5 });
        yield return ("asse plastico in anima", girder with { SlabWidth = 40, SlabHeight = 15, RebarArea = 0, TopFlangeWidth = 30, TopFlangeThickness = 1.5 });
        yield return ("asse plastico in piattabanda inferiore", girder with { SlabWidth = 100, SlabHeight = 10, Fcd = 5, RebarArea = 0, TopFlangeWidth = 30,
            TopFlangeThickness = 1.5, WebHeight = 60, WebThickness = 2, BottomFlangeWidth = 200, BottomFlangeThickness = 8 });
        yield return ("momento negativo", girder with { SteelOnly = new(0, 300, -1500), LongTerm = new(0, 200, -2000), ShortTerm = new(0, 600, -3500) });
        yield return ("momento negativo, asse in piattabanda inferiore", girder with { BottomFlangeWidth = 120, BottomFlangeThickness = 7, RebarArea = 5,
            SteelOnly = new(0, 100, -500), LongTerm = new(0, 0, -500), ShortTerm = new(0, 0, -1000) });
        yield return ("momento negativo, armatura forte", girder with { RebarArea = 400, TopFlangeWidth = 30, BottomFlangeWidth = 30, WebHeight = 60,
            WebThickness = 1.5, SteelOnly = new(0, 0, -100), LongTerm = new(0, 0, -100), ShortTerm = new(0, 0, -100) });
        yield return ("anima snella classe 4", girder with { WebThickness = 1.0, WebHeight = 250 });
        yield return ("taglio elevato", girder with { ShortTerm = new(0, 3000, 3500) });
        yield return ("taglio oltre Vpl", girder with { ShortTerm = new(0, 9000, 3500) });
        yield return ("solo acciaio", girder with { SlabHeight = 0, SlabWidth = 0, RebarArea = 0, RebarCover = 0 });
        yield return ("soletta tesa", girder with { LongTerm = new(1500, 0, -200), ShortTerm = new(0, 0, -800) });
        yield return ("tensioni con γ", girder with { GammaG1 = 1.35, GammaG2 = 1.5, GammaQ = 1.35, FactoredElasticStresses = true });
        yield return ("nessuna iterazione", girder with { WebThickness = 1.0, WebHeight = 250, Iterations = 0 });
        yield return ("anima tesa", girder with { SteelOnly = new(3000, 0, 0), LongTerm = new(0, 0, 0), ShortTerm = new(0, 0, 0) });
        yield return ("valori del programma", new VivianiInput { SteelOnly = new(0, 0, 1.0) });
    }

    [TestMethod]
    public void EveryBranchGivesTheSameResults()
    {
        foreach (var (name, input) in BranchCases())
            AssertSame(Original(input), VivianiMethod.Calculate(input), name);
    }

    [TestMethod]
    public void TheInputIsNotChanged()
    {
        // the program writes Tw = 0 in its field when the shear exceeds the resistance; the rewrite reports it without changing the data
        var d = BranchCases().Single(c => c.Name == "taglio oltre Vpl").Input;
        VivianiResult r = VivianiMethod.Calculate(d);
        Assert.IsTrue(r.ShearExceedsWebResistance);
        Assert.AreEqual(0, r.ShownWebThickness);
        Assert.AreEqual(1.4, d.WebThickness);
        Assert.AreEqual(1.0, r.Utilization);
        Assert.AreEqual(0, r.MRd);
    }
}
