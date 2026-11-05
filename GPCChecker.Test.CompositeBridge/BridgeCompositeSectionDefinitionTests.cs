using System.Globalization;
using System.Reflection;
using System.Text;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Core;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.CompositeBridge;

/// <summary>The composite section of a bridge girder (lane L11, correzioni-P2B.md §1bis): contract, validation, archive, fingerprint, and the
/// section input of the H bridge engine equal bit for bit to the one of the BridGHe adapter on the sections of the examples 50 and 99.</summary>
[TestClass]
public class BridgeCompositeSectionDefinitionTests
{
    /// <summary>The section of a girder as Super Element Check builds it (constant section: three bars per row of the slab, the diameter
    /// of a third of the area of the row; BridGHe CompositeSectionFactory.Create = ReinforcedConcreteSection.CreateBridgeSection with
    /// vertical offset 0).</summary>
    private static ReinforcedConcreteSection Girder(string concreteName, string steelName, double slabWidth, double slabHeight, double topArea, double topCover,
        double bottomArea, double bottomCover, double d, double tw, double topWidth, double topThickness, double bottomWidth, double bottomThickness, string name)
    {
        var concrete = new ConcreteMaterialEN1992(concreteName, 32, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterialEN1992(steelName, 210000, 355, 510, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        var rebar = new SteelMaterialEN1992("B450C", 200000, 450, 540, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        int numberRebars = 3;
        double rebarAreaTop = topArea / numberRebars;
        double rebarDiameterTop = Math.Sqrt(rebarAreaTop * 4 / Math.PI);
        RebarSectionCircular? top = rebarDiameterTop != 0 ? new RebarSectionCircular("", rebarDiameterTop, rebar) : null;
        double rebarDiameterBottom = Math.Sqrt(bottomArea / numberRebars * 4 / Math.PI);
        RebarSectionCircular? bottom = rebarDiameterBottom != 0 ? new RebarSectionCircular("", rebarDiameterBottom, rebar) : null;
        var h = new SectionH(d, tw, topWidth, topThickness, bottomWidth, bottomThickness, "1");
        return ReinforcedConcreteSection.CreateBridgeSection(slabWidth, slabHeight, concrete, top, slabWidth / numberRebars, topCover, bottom, slabWidth / numberRebars,
            h, steel, bottomCover, 0, name);
    }

    /// <summary>Example 50, girder G1-C1 (beam 1 of super element G1): I 2500 with flanges 600 × 30, web 16; slab 3000 × 300; two rows of
    /// 3500 mm² at 50 mm from the faces.</summary>
    private static ReinforcedConcreteSection Example50() => Girder("Calcestruzzo soletta", "Acciaio carpenteria", 3000, 300, 3500, 50, 3500, 50, 2500, 16, 600, 30, 600, 30, "G1-C1");
    /// <summary>Example 99 (REV004), girder of G1: I 1800, top flange 600 × 30, bottom flange 800 × 40, web 15; slab 2500 × 300; two rows at
    /// 50 mm from the faces (the area 1600π mm² gives the diameter of the native run bit for bit).</summary>
    private static ReinforcedConcreteSection Example99() => Girder("6", "2", 2500, 300, 1600 * Math.PI, 50, 1600 * Math.PI, 50, 1800, 15, 600, 30, 800, 40, "Composite");

    /// <summary>
    /// Reference: BridGHe.Common.Checks.BridgeCheckAdapter.SectionInput(rebars) = Input(true, rebars, true), copied verbatim from
    /// gpc-f6-claude/P2B/BridGHe src/BridGHe.Common/Checks/BridgeCheckAdapter.cs (commit e2759922, blob 034e5ce6), lines 39-86, with the
    /// fields of the adapter as parameters (section, reference, options). The same comparison against the compiled adapter of BridGHe is
    /// in correzioni-L11.md.
    /// </summary>
    private static HBridgeInput AdapterSectionInput(ReinforcedConcreteSection section, CoordinateSystem reference, BridgeAnalysisOptions options, bool rebars)
    {
        bool concrete = true, buckling = true; _ = concrete;
        if (section.SteelSections.Count != 1 || !(section.SteelSections[0].Section.SectionShape is SectionH h))
            throw new NotSupportedException("L'adattatore BridgeChecks supporta una singola sezione ad H con soletta rettangolare.");
        var positioned = section.SteelSections[0];
        if (Math.Abs(positioned.Rotation) > 1e-10 || Math.Abs(section.Jxy) > 1e-10 * (section.Jxx + section.Jyy))
            throw new NotSupportedException("Sezione ruotata o non simmetrica: adattamento del verificatore da completare.");
        var rows = section.Rebars.GroupBy(b => Math.Round(b.Position.Y, 8)).OrderByDescending(g => g.Key).ToArray();
        if (rows.Length > 2) throw new NotSupportedException("Sono supportate al massimo due file di armatura.");
        BridgeRebarRow Row(int index)
        {
            if (!rebars || index >= rows.Length) return new BridgeRebarRow();
            var bars = rows[index].ToArray();
            if (bars.Any(b => Math.Abs(b.RebarSection.Diameter - bars[0].RebarSection.Diameter) > 1e-8))
                throw new NotSupportedException("Diametri diversi nella stessa fila non supportati dall'adattatore.");
            return new BridgeRebarRow
            {
                Enabled = true,
                Diameter = bars[0].RebarSection.Diameter,
                Pitch = section.Width / bars.Length,
                AxisDistance = index == 0 ? section.Height - bars[0].Position.Y : bars[0].Position.Y
            };
        }
        var steel = positioned.Section.SteelMaterial;
        var rebar = section.Rebars.FirstOrDefault()?.RebarSection.RebarMaterial ?? steel;
        return new HBridgeInput
        {
            Materials = new BridgeMaterialSet((ConcreteMaterialEN1992)section.ConcreteMaterial, steel, rebar),
            Geometry = new HSectionDimensions
            {
                SlabWidth = section.Width,
                SlabHeight = section.Height,
                WebHeight = h.HeightWeb,
                WebThickness = h.ThicknessWeb,
                TopWidth = h.LenghtTopFlange,
                TopThickness = h.ThicknessTopFlange,
                BottomWidth = h.LenghtBottomFlange,
                BottomThickness = h.ThicknessBottomFlange
            },
            TopRebars = Row(0),
            BottomRebars = Row(1),
            Options = options with
            {
                Class4 = buckling && options.Class4,
                CommonLoadY = reference.Origin.Y
            }
        };
    }

    /// <summary>The reference system of Super Element Check (SuperElementCheckComponent.GetCoordinateSystem).</summary>
    private static CoordinateSystem SuperElementReference(ReinforcedConcreteSection section) => new(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
    /// <summary>The options of Super Element Check (EN 1994-2, rare SLS) with its three buckling inputs.</summary>
    private static BridgeAnalysisOptions SuperElementOptions(bool web, bool top, bool bottom) => new()
    { Standard = BridgeStandard.Eurocode4, LimitState = BridgeLimitState.Rare, WebBuckling = web, TopFlangeBuckling = top, BottomFlangeBuckling = bottom };

    /// <summary>Every value of a record of the engine input, the doubles as their bits, the other objects by identity: equal texts are
    /// equal inputs bit for bit.</summary>
    private static string Dump(object? value, bool identity = true)
    {
        var text = new StringBuilder();
        void Walk(object? v, string p)
        {
            switch (v)
            {
                case null: text.Append(p).Append("=null\n"); return;
                case double d: text.Append(p).Append('=').Append(BitConverter.DoubleToInt64Bits(d).ToString("X16")).Append('\n'); return;
                case string or bool or int or Enum: text.Append(p).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n'); return;
                case Material m: text.Append(p).Append('=').Append(identity ? "material#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m) : ModelValues.Fingerprint(new object[] { m })).Append(' ').Append(m.Name).Append('\n'); return;
                case System.Collections.IEnumerable items:
                    int i = 0; foreach (var item in items) Walk(item, p + "[" + i++ + "]"); text.Append(p).Append(".Count=").Append(i).Append('\n'); return;
            }
            foreach (var property in v.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetIndexParameters().Length == 0).OrderBy(x => x.Name, StringComparer.Ordinal))
                Walk(property.GetValue(v), p + "." + property.Name);
        }
        Walk(value, "");
        return text.ToString();
    }

    private static void Same(HBridgeInput expected, HBridgeInput actual, string label)
    {
        Assert.AreEqual(Dump(expected), Dump(actual), label);
        Assert.AreEqual(expected, actual, label);
        Assert.AreEqual(ModelValues.Fingerprint(new object[] { expected }), ModelValues.Fingerprint(new object[] { actual }), label);
    }

    private static string Document(byte[] xml)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return System.Text.RegularExpressions.Regex.Replace(Encoding.UTF8.GetString(xml), "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            m => "#" + (seen.TryGetValue(m.Value, out int i) ? i : seen[m.Value] = seen.Count));
    }

    private static BridgeCompositeSectionDefinition Definition(ReinforcedConcreteSection section) => new("Composite", section, 18.41, 6.18, 16.9, 0, true, true);

    [TestMethod]
    public void SectionInputIsTheInputOfTheBridGHeAdapterOnTheSectionsOfTheExamples()
    {
        foreach (var (label, section, captured) in new[] { ("50 G1-C1", Example50(), SuperElementOptions(true, true, true)), ("99", Example99(), SuperElementOptions(false, false, false)) })
        {
            var definition = Definition(section);
            var reference = SuperElementReference(section);
            var variants = new[] { null, captured, SuperElementOptions(true, false, true), captured with { Class4 = false }, captured with { LimitState = BridgeLimitState.Ultimate, Standard = BridgeStandard.Ntc2018 } };
            foreach (bool rebars in new[] { true, false })
                foreach (var options in variants)
                {
                    // null: the default options of the adapter (new BridgeAnalysisOptions { Standard = Eurocode4, LimitState = Rare })
                    var adapterOptions = options ?? new BridgeAnalysisOptions { Standard = BridgeStandard.Eurocode4, LimitState = BridgeLimitState.Rare };
                    var expected = AdapterSectionInput(section, reference, adapterOptions, rebars);
                    var actual = options == null ? definition.SectionInput(rebars) : definition.SectionInput(rebars, options);
                    Same(expected, actual, label + " rebars=" + rebars + " options=" + (options == null ? "default" : Dump(options)));
                    Assert.AreSame(section.ConcreteMaterial, actual.Materials.Concrete); Assert.AreSame(section.SteelSections[0].Section.SteelMaterial, actual.Materials.Steel);
                    Assert.AreEqual(0, actual.Phases.Length);
                }
            // The options given are not changed.
            Assert.AreEqual(0, captured.CommonLoadY);
        }
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
    private static long Captured(string capturedText) => BitConverter.DoubleToInt64Bits(double.Parse(capturedText, CultureInfo.InvariantCulture));

    /// <summary>The values of the input that the native runs of BridGHe wrote for these sections (Super Element Check, first row of each
    /// capture: gpc-f6-claude/P2B/results/s12-native-50/gpc/example50-rev003-native.inputs.jsonl.gz "G1 / Beam 1 / s=0 mm" and
    /// s12-native-99/gpc/modular-flusso-acciaio-misto_REV004.inputs.jsonl.gz "G1 / Beam 1 / s=0 mm"), in the round-trip text of the capture.</summary>
    [TestMethod]
    public void SectionInputGivesTheValuesOfTheNativeRuns()
    {
        void Check(HBridgeInput input, string[] geometry, string[] top, string[] bottom, bool buckling)
        {
            var g = input.Geometry;
            CollectionAssert.AreEqual(geometry.Select(Captured).ToArray(), new[] { g.SlabWidth, g.SlabHeight, g.WebHeight, g.WebThickness, g.TopWidth, g.TopThickness, g.BottomWidth, g.BottomThickness }.Select(Bits).ToArray());
            foreach (var (row, values) in new[] { (input.TopRebars, top), (input.BottomRebars, bottom) })
            {
                Assert.IsTrue(row.Enabled);
                CollectionAssert.AreEqual(values.Select(Captured).ToArray(), new[] { row.Diameter, row.Pitch, row.AxisDistance }.Select(Bits).ToArray());
            }
            var o = input.Options;
            Assert.AreEqual(Captured("150"), Bits(o.CommonLoadY));
            Assert.IsTrue(o.Class4); Assert.AreEqual(BridgeStandard.Eurocode4, o.Standard); Assert.AreEqual(BridgeLimitState.Rare, o.LimitState);
            Assert.AreEqual(buckling, o.WebBuckling); Assert.AreEqual(buckling, o.TopFlangeBuckling); Assert.AreEqual(buckling, o.BottomFlangeBuckling);
            Assert.AreEqual(Captured("1.05"), Bits(o.GammaM0)); Assert.AreEqual(Captured("1.5"), Bits(o.GammaC)); Assert.AreEqual(Captured("1.15"), Bits(o.GammaS));
            Assert.AreEqual(Captured("0.85"), Bits(o.AlphaCC)); Assert.AreEqual(Captured("1.1"), Bits(o.GammaM1)); Assert.AreEqual(Captured("1.25"), Bits(o.GammaM2)); Assert.AreEqual(Captured("1.2"), Bits(o.ShearEta));
            Assert.IsTrue(o.AcceleratedIteration);
        }
        Check(Definition(Example50()).SectionInput(true, SuperElementOptions(true, true, true)),
            new[] { "3000", "300", "2440", "16", "600", "30", "600", "30" }, new[] { "38.541485901874054", "1000", "50" }, new[] { "38.541485901874054", "1000", "50" }, true);
        Check(Definition(Example99()).SectionInput(true, SuperElementOptions(false, false, false)),
            new[] { "2500", "300", "1730", "15", "600", "30", "800", "40" }, new[] { "46.188021535170066", "833.3333333333334", "50" }, new[] { "46.188021535170066", "833.3333333333334", "50" }, false);
        // The default options are those of the run of the 50 (the three buckling inputs at their default, true).
        var section50 = Example50();
        Assert.AreEqual(Dump(Definition(section50).SectionInput(true, SuperElementOptions(true, true, true))), Dump(Definition(section50).SectionInput(true)));
    }

    [TestMethod]
    public void DefinitionRoundTripsWithItsFingerprint()
    {
        foreach (var section in new[] { Example50(), Example99() })
        {
            var definition = new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.9, -0.0, false, true);
            string fingerprint = definition.Fingerprint();
            byte[] saved = definition.Save();
            foreach (var other in new[] { BridgeCompositeSectionDefinition.Read(saved), definition.Copy() })
            {
                Assert.AreNotSame(definition.Section, other.Section);
                Assert.AreEqual("G1-C1 composite", other.Name);
                Assert.AreEqual(Bits(18.41), Bits(other.NLongTerm)); Assert.AreEqual(Bits(6.18), Bits(other.NShortTerm)); Assert.AreEqual(Bits(16.9), Bits(other.NShrinkage));
                Assert.AreEqual(Bits(-0.0), Bits(other.VerticalOffsetMm));
                Assert.IsFalse(other.ConsiderConcrete); Assert.IsTrue(other.ConsiderRebar);
                Assert.AreEqual(fingerprint, other.Fingerprint());
                Assert.AreEqual(ModelValues.Fingerprint(new object[] { section }), ModelValues.Fingerprint(new object[] { other.Section }));
                Assert.AreSame(other.Section.SteelSections[0].Section.SteelMaterial, other.SectionInput(true).Materials.Steel, "the copy reads its own materials");
                // The same input, with the materials of the copy.
                foreach (bool rebars in new[] { true, false })
                    Assert.AreEqual(Dump(definition.SectionInput(rebars), false), Dump(other.SectionInput(rebars), false), "same values");
                // The same document (the GUIDs of the geometry points are generated again on reading: numbered by first appearance).
                Assert.AreEqual(Document(saved), Document(other.Save()));
            }
            // Every field enters the fingerprint.
            foreach (var changed in new[] {
                new BridgeCompositeSectionDefinition("other", section, 18.41, 6.18, 16.9, 0, false, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.42, 6.18, 16.9, 0, false, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.19, 16.9, 0, false, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.8, 0, false, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.9, 5, false, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.9, 0, true, true),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.9, 0, false, false),
                new BridgeCompositeSectionDefinition("G1-C1 composite", section.Width == 3000 ? Example99() : Example50(), 18.41, 6.18, 16.9, 0, false, true) })
                Assert.AreNotEqual(fingerprint, changed.Fingerprint());
            Assert.AreEqual(fingerprint, new BridgeCompositeSectionDefinition("G1-C1 composite", section, 18.41, 6.18, 16.9, 0, false, true).Fingerprint(), "-0 and 0 are one offset");
        }
    }

    /// <summary>
    /// Reference: BridGHe.Common.Models.CompositeBeamPropertyModel.GetReinforcedConcreteSection (the section of the homogenised analysis
    /// properties of Composite Beam Property and Bridge State Override), copied verbatim from gpc-f6-claude/P2B/BridGHe
    /// src/BridGHe.Common/Models/CompositeBeamPropertyModel.cs (commit e33822f), lines 203-241, with the fields of the property as
    /// parameters (the steel H B1/B2/D/T1/T2/T3, the slab, the rows, the material values, the name) and CompositeSectionFactory.Create
    /// (vertical offset 0) inlined.
    /// </summary>
    private static ReinforcedConcreteSection ReferenceAnalysisSection(double bfw, double tfw, double h, double bft, double tft, double wt,
        double hh, double b, double topRebarsAreaTot, double topRebarsCover, double bottomRebarArea, double bottomCover,
        string concreteName, double fck, string steelName, double steelE, double steelFy, double steelFu, double rebarE, double rebarFy, double rebarFu,
        double? rebarUltimateStrain, string sectionHName, string name)
    {
        SectionH sectionH = new SectionH(h, wt, tfw, tft, bfw, bft, sectionHName);
        ConcreteMaterialEN1992 concreteMaterialEN1992 = new ConcreteMaterialEN1992(concreteName, fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        SteelMaterialEN1992 steelMaterialEN1992 = new SteelMaterialEN1992(steelName, steelE, steelFy, steelFu, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        SteelMaterialEN1992 rebarMaterial = new SteelMaterialEN1992("B450C", rebarE, rebarFy, rebarFu, rebarUltimateStrain ?? 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        double rebarDiameterTop = Math.Sqrt(topRebarsAreaTot * 4 / Math.PI);
        RebarSectionCircular? rebarTop = rebarDiameterTop != 0 ? new RebarSectionCircular("", rebarDiameterTop, rebarMaterial) : null;
        double rebarDiameterBottom = Math.Sqrt(bottomRebarArea * 4 / Math.PI);
        RebarSectionCircular? rebarBottom = rebarDiameterBottom != 0 ? new RebarSectionCircular("", rebarDiameterBottom, rebarMaterial) : null;
        return ReinforcedConcreteSection.CreateBridgeSection(b, hh, concreteMaterialEN1992, rebarTop, b / 1.0, topRebarsCover, rebarBottom, b / 1.0, sectionH, steelMaterialEN1992,
            bottomCover, 0, name);
    }

    /// <summary>The girders of the examples and variants of the rows: (label, slab width, slab height, rows, steel d, tw, top width,
    /// top thickness, bottom width, bottom thickness, concrete, steel names, name).</summary>
    private static IEnumerable<(string Label, double W, double H, BridgeCompositeRebarRows Rows, double D, double Tw, double Bt, double Tt, double Bb, double Tb, string Concrete, string Steel, string Name)> AnalysisCases()
    {
        yield return ("50 G1-C1", 3000, 300, new BridgeCompositeRebarRows(3500, 50, 3500, 50, 3), 2500, 16, 600, 30, 600, 30, "Calcestruzzo soletta", "Acciaio carpenteria", "G1-C1");
        yield return ("99", 2500, 300, new BridgeCompositeRebarRows(1600 * Math.PI, 50, 1600 * Math.PI, 50, 3), 1800, 15, 600, 30, 800, 40, "6", "2", "Composite");
        yield return ("top row only", 3000, 300, new BridgeCompositeRebarRows(3500, 45, 0, 50, 3), 2500, 16, 600, 30, 600, 30, "C", "S", "top");
        yield return ("bottom row only", 3000, 250, new BridgeCompositeRebarRows(0, 50, 2711.3, 62.5, 3), 2500, 16, 600, 30, 700, 35, "C", "S", "bottom");
        yield return ("bottom cover 0 (the top one)", 2800, 280, new BridgeCompositeRebarRows(4021.24, 47.5, 1884.96, 0, 3), 2200, 14, 500, 25, 650, 40, "C", "S", "cover0");
        yield return ("different covers, two bars per row", 3100, 320, new BridgeCompositeRebarRows(3141.59, 40, 2513.27, 70, 2), 2400, 18, 620, 32, 760, 44, "C", "S", "two");
        yield return ("no bars", 3000, 300, new BridgeCompositeRebarRows(0, 50, 0, 50, 3), 2500, 16, 600, 30, 600, 30, "C", "S", "bare");
    }

    /// <summary>The section of the checks of a case: <paramref name="bars"/> bars per row of a third (bars = 3) of the area of the row, as
    /// Super Element Check builds it (<see cref="Girder"/> with the bars of the rows).</summary>
    private static ReinforcedConcreteSection ChecksSection((string Label, double W, double H, BridgeCompositeRebarRows Rows, double D, double Tw, double Bt, double Tt, double Bb, double Tb, string Concrete, string Steel, string Name) c)
    {
        var concrete = new ConcreteMaterialEN1992(c.Concrete, 32, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterialEN1992(c.Steel, 210000, 355, 510, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        var rebar = new SteelMaterialEN1992("B450C", 200000, 450, 540, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        int n = c.Rows.BarsPerRow;
        double top = Math.Sqrt(c.Rows.TopAreaMm2 / n * 4 / Math.PI), bottom = Math.Sqrt(c.Rows.BottomAreaMm2 / n * 4 / Math.PI);
        return ReinforcedConcreteSection.CreateBridgeSection(c.W, c.H, concrete, top != 0 ? new RebarSectionCircular("", top, rebar) : null, c.W / n, c.Rows.TopCoverMm,
            bottom != 0 ? new RebarSectionCircular("", bottom, rebar) : null, c.W / n, new SectionH(c.D, c.Tw, c.Bt, c.Tt, c.Bb, c.Tb, "1"), steel, c.Rows.BottomCoverMm, 0, c.Name);
    }

    /// <summary>Every bar (position and diameter bits, material values) and the homogenised properties at several creep coefficients with
    /// and without the bars and the steel, as bits: equal texts are the same section for the analysis properties.</summary>
    private static string AnalysisDump(Func<ReinforcedConcreteSection> create)
    {
        var text = new StringBuilder();
        var section = create();
        foreach (var bar in section.Rebars)
            text.Append("bar ").Append(Bits(bar.Position.X).ToString("X16")).Append(' ').Append(Bits(bar.Position.Y).ToString("X16")).Append(' ')
                .Append(Bits(bar.RebarSection.Diameter).ToString("X16")).Append(' ').Append(Bits(bar.RebarSection.RebarMaterial.ElasticModulusTension).ToString("X16")).Append('\n');
        text.Append("width ").Append(Bits(section.Width).ToString("X16")).Append(" height ").Append(Bits(section.Height).ToString("X16")).Append(" bars ").Append(section.Rebars.Count()).Append('\n');
        foreach (double phi in new[] { 0.0, 1.0, 18.41 * 33345.7645 / 210000 - 1, 6.18 * 33345.7645 / 210000 - 1, -0.25 })
            foreach (var (rebars, steel) in new[] { (true, true), (false, true), (true, false) })
            {
                var s = create();
                if (!rebars) s.ClearRebars();
                if (!steel) s.SteelSections.Clear();
                var p = s.GetHomogeneizedMechanicalProperties(phi);
                text.Append("phi ").Append(Bits(phi).ToString("X16")).Append(rebars ? " rebars" : "").Append(steel ? " steel" : "").Append(':');
                foreach (double v in new[] { p.areaH, p.SxH, p.SyH, p.centroidH.X, p.centroidH.Y, p.JxxH, p.JyyH, p.JxyH, p.JpH, p.J11H, p.J22H, p.angleX })
                    text.Append(' ').Append(Bits(v).ToString("X16"));
                text.Append('\n');
            }
        return text.ToString();
    }

    [TestMethod]
    public void AnalysisSectionIsTheSectionOfTheHomogenisedPropertiesOfBridGHe()
    {
        foreach (var c in AnalysisCases())
        {
            var checks = ChecksSection(c);
            var definition = new BridgeCompositeSectionDefinition(c.Name, checks, c.Rows, 18.41, 6.18, 16.9, 0, true, true);
            Func<ReinforcedConcreteSection> reference = () => ReferenceAnalysisSection(c.Bb, c.Bt, c.D, c.Tb, c.Tt, c.Tw, c.H, c.W, c.Rows.TopAreaMm2, c.Rows.TopCoverMm,
                c.Rows.BottomAreaMm2, c.Rows.BottomCoverMm, c.Concrete, 32, c.Steel, 210000, 355, 510, 200000, 450, 540, null, "1", c.Name);
            Assert.AreEqual(AnalysisDump(reference), AnalysisDump(definition.AnalysisSection), c.Label);
            // The same section as a value (materials, shapes, bars, name): the fingerprint of GPCModel.
            Assert.AreEqual(ModelValues.Fingerprint(new object[] { reference() }), ModelValues.Fingerprint(new object[] { definition.AnalysisSection() }), c.Label);
            // One bar per row of non-zero area, at the middle of the slab; a new section at every call, with the materials and the steel of the definition.
            var analysis = definition.AnalysisSection();
            int rows = (c.Rows.TopAreaMm2 > 0 ? 1 : 0) + (c.Rows.BottomAreaMm2 > 0 ? 1 : 0);
            Assert.AreEqual(rows, analysis.Rebars.Count(), c.Label);
            Assert.IsTrue(analysis.Rebars.All(b => b.Position.X == c.W / 2), c.Label);
            Assert.AreNotSame(analysis, definition.AnalysisSection());
            Assert.AreNotSame(definition.Section, analysis);
            Assert.AreSame(checks.ConcreteMaterial, analysis.ConcreteMaterial);
            Assert.AreSame(checks.SteelSections[0].Section.SteelMaterial, analysis.SteelSections[0].Section.SteelMaterial);
            Assert.AreSame(checks.SteelSections[0].Section.SectionShape, analysis.SteelSections[0].Section.SectionShape);
            // The section of the checks is not changed.
            Assert.AreEqual(rows * c.Rows.BarsPerRow, definition.Section.Rebars.Count(), c.Label);
            // The section input of the checks is the one of the same definition without the rows.
            Assert.AreEqual(Dump(new BridgeCompositeSectionDefinition(c.Name, checks, 18.41, 6.18, 16.9, 0, true, true).SectionInput(true)), Dump(definition.SectionInput(true)), c.Label);
        }
        // The ultimate strain of the bars of the analysis section of BridGHe (ReinforcementUltimateStrain ?? 0.1) does not enter the
        // homogenised properties: a rebar material with another ultimate strain gives the same values.
        var g = AnalysisCases().First();
        Func<ReinforcedConcreteSection> other = () => ReferenceAnalysisSection(g.Bb, g.Bt, g.D, g.Tb, g.Tt, g.Tw, g.H, g.W, g.Rows.TopAreaMm2, g.Rows.TopCoverMm,
            g.Rows.BottomAreaMm2, g.Rows.BottomCoverMm, g.Concrete, 32, g.Steel, 210000, 355, 510, 200000, 450, 540, 0.0675, "1", g.Name);
        Assert.AreEqual(AnalysisDump(other), AnalysisDump(new BridgeCompositeSectionDefinition(g.Name, ChecksSection(g), g.Rows, 18.41, 6.18, 16.9, 0, true, true).AnalysisSection));
        // Without the rows there is no analysis section.
        Assert.ThrowsException<InvalidOperationException>(() => Definition(Example50()).AnalysisSection());
    }

    [TestMethod]
    public void RebarRowsRoundTripAndEnterTheFingerprintOnlyWhenGiven()
    {
        // A definition without the rows keeps the fingerprint of the Checker before the rows (d018ff28, measured with that build) and its document.
        Assert.AreEqual("72E292C0F336A3074BD6CB1D156EDCA111C92B7F43B03F874017FF029F37ACE1", Definition(Example50()).Fingerprint());
        Assert.AreEqual("FDFA7A8AFEC347E0D50C079473F7C0FD4D2D816FFB82C42689F8A1215686A56E", Definition(Example99()).Fingerprint());
        Assert.AreEqual("53E8924DC8B85C2CEE4B3E2936CFE72C72F130432F4060A4628307155846C0C8",
            new BridgeCompositeSectionDefinition("G1-C1 composite", Example50(), 18.41, 6.18, 16.9, 0, false, true).Fingerprint());
        StringAssert.DoesNotMatch(Encoding.UTF8.GetString(Definition(Example50()).Save()), new System.Text.RegularExpressions.Regex("RebarRows"));
        foreach (var c in AnalysisCases())
        {
            var section = ChecksSection(c);
            var definition = new BridgeCompositeSectionDefinition(c.Name, section, c.Rows, 18.41, 6.18, 16.9, 0, true, true);
            string fingerprint = definition.Fingerprint();
            Assert.AreNotEqual(new BridgeCompositeSectionDefinition(c.Name, section, 18.41, 6.18, 16.9, 0, true, true).Fingerprint(), fingerprint, c.Label);
            byte[] saved = definition.Save();
            foreach (var other in new[] { BridgeCompositeSectionDefinition.Read(saved), definition.Copy() })
            {
                var rows = other.RebarRows!;
                Assert.AreNotSame(definition.RebarRows, rows);
                CollectionAssert.AreEqual(new[] { c.Rows.TopAreaMm2, c.Rows.TopCoverMm, c.Rows.BottomAreaMm2, c.Rows.BottomCoverMm }.Select(Bits).ToArray(),
                    new[] { rows.TopAreaMm2, rows.TopCoverMm, rows.BottomAreaMm2, rows.BottomCoverMm }.Select(Bits).ToArray(), c.Label);
                Assert.AreEqual(c.Rows.BarsPerRow, rows.BarsPerRow);
                Assert.AreEqual(fingerprint, other.Fingerprint(), c.Label);
                Assert.AreEqual(AnalysisDump(definition.AnalysisSection), AnalysisDump(other.AnalysisSection), c.Label);
                Assert.AreEqual(Document(saved), Document(other.Save()));
            }
            // Every value of the rows enters the fingerprint (a cover alone changes no bar of the section of the checks).
            var r = c.Rows;
            foreach (var changed in new[] { new BridgeCompositeRebarRows(r.TopAreaMm2, r.TopCoverMm + 1, r.BottomAreaMm2, r.BottomCoverMm, r.BarsPerRow),
                new BridgeCompositeRebarRows(r.TopAreaMm2, r.TopCoverMm, r.BottomAreaMm2, r.BottomCoverMm + 1, r.BarsPerRow) })
                Assert.AreNotEqual(fingerprint, new BridgeCompositeSectionDefinition(c.Name, section, changed, 18.41, 6.18, 16.9, 0, true, true).Fingerprint(), c.Label);
        }
        var g = AnalysisCases().First();
        var more = (g.Label, g.W, g.H, Rows: new BridgeCompositeRebarRows(3600, 50, 3500, 50, 3), g.D, g.Tw, g.Bt, g.Tt, g.Bb, g.Tb, g.Concrete, g.Steel, g.Name);
        Assert.AreNotEqual(new BridgeCompositeSectionDefinition(g.Name, ChecksSection(g), g.Rows, 18.41, 6.18, 16.9, 0, true, true).Fingerprint(),
            new BridgeCompositeSectionDefinition(g.Name, ChecksSection(more), more.Rows, 18.41, 6.18, 16.9, 0, true, true).Fingerprint());
        // The rows travel inside the bridge data of a model.
        var data = new BridgeModelData(); var beam = Guid.NewGuid();
        data.SetCompositeSection(beam, new BridgeBeamCompositeSection(new BridgeCompositeSectionDefinition(g.Name, ChecksSection(g), g.Rows, 18.41, 6.18, 16.9, 0, true, true)));
        var read = BridgeModelData.Read(data.Save());
        Assert.AreEqual(data.Fingerprint(), read.Fingerprint());
        Assert.AreEqual(AnalysisDump(data.CompositeSection(beam)!.Start.AnalysisSection), AnalysisDump(read.CompositeSection(beam)!.Start.AnalysisSection));
    }

    /// <summary>The steel of the analysis properties as BridGHe gives it (GpcMaterials.Elastic of the steel GH_Material: E, ν, density, α and,
    /// with an explicit G or damping, the analysis data).</summary>
    private static Material AnalysisSteel(double e = 210000, double density = 7.85e-9) =>
        new("Acciaio carpenteria", e, 0.3, density, 1.2e-5) { AnalysisData = new MaterialAnalysisData { BeamShearModulus = 80769.23, ViscousDampingDensity = 0.01 } };

    [TestMethod]
    public void AnalysisSteelRoundTripsAndEntersTheFingerprintOnlyWhenGiven()
    {
        // A definition with the rows and without the steel keeps the fingerprint of 28d8b967 (measured with that build on fresh sections).
        var rows50 = new BridgeCompositeRebarRows(3500, 50, 3500, 50, 3); var rows99 = new BridgeCompositeRebarRows(1600 * Math.PI, 50, 1600 * Math.PI, 50, 3);
        Assert.AreEqual("E8D8CA2CF09D75F1AAF1B1EDD6D78DBBDF3313AA3DA0B454EB98824E13D9DE65", new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, 18.41, 6.18, 16.9, 0, true, true).Fingerprint());
        Assert.AreEqual("9F9A0D30C74C973698625CCCD10B9605D61CD837FE5EEC9C3ACC8F1A825BE77E", new BridgeCompositeSectionDefinition("Composite", Example99(), rows99, 18.41, 6.18, 16.9, 0, true, true).Fingerprint());
        StringAssert.DoesNotMatch(Encoding.UTF8.GetString(new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, 18.41, 6.18, 16.9, 0, true, true).Save()),
            new System.Text.RegularExpressions.Regex("AnalysisSteel"));
        foreach (var (section, rows) in new[] { (Example50(), rows50), (Example99(), rows99) })
            foreach (Material steel in new Material[] { AnalysisSteel(), new SteelMaterialEN1992("S355", 210000, 355, 510, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural) })
            {
                var definition = new BridgeCompositeSectionDefinition("Composite", section, rows, steel, 18.41, 6.18, 16.9, 0, true, true);
                Assert.AreSame(steel, definition.AnalysisSteel);
                string fingerprint = definition.Fingerprint();
                Assert.AreNotEqual(new BridgeCompositeSectionDefinition("Composite", section, rows, 18.41, 6.18, 16.9, 0, true, true).Fingerprint(), fingerprint);
                byte[] saved = definition.Save();
                foreach (var other in new[] { BridgeCompositeSectionDefinition.Read(saved), definition.Copy() })
                {
                    Assert.AreNotSame(steel, other.AnalysisSteel);
                    Assert.AreEqual(steel.GetType(), other.AnalysisSteel!.GetType());
                    Assert.AreEqual(ModelValues.Fingerprint(new object[] { steel }), ModelValues.Fingerprint(new object[] { other.AnalysisSteel }));
                    Assert.AreEqual(fingerprint, other.Fingerprint());
                    Assert.AreEqual(Document(saved), Document(other.Save()));
                    Assert.AreEqual(AnalysisDump(definition.AnalysisSection), AnalysisDump(other.AnalysisSection), "the analysis section does not depend on the steel");
                }
                // The steel enters the fingerprint (a density other than that of the steel of the section).
                Assert.AreNotEqual(fingerprint, new BridgeCompositeSectionDefinition("Composite", section, rows, AnalysisSteel(density: 7.9e-9), 18.41, 6.18, 16.9, 0, true, true).Fingerprint());
                // The rows travel with it inside the bridge data of a model.
                var data = new BridgeModelData(); var beam = Guid.NewGuid();
                data.SetCompositeSection(beam, new BridgeBeamCompositeSection(definition));
                var read = BridgeModelData.Read(data.Save());
                Assert.AreEqual(data.Fingerprint(), read.Fingerprint());
                Assert.AreEqual(ModelValues.Fingerprint(new object[] { steel }), ModelValues.Fingerprint(new object[] { read.CompositeSection(beam)!.Start.AnalysisSteel! }));
            }
        // One input: an analysis steel of another E is refused with a clear message; a missing steel too.
        var mismatch = Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, AnalysisSteel(205000), 18.41, 6.18, 16.9, 0, true, true));
        StringAssert.StartsWith(mismatch.Message, "BridgeCompositeAnalysisSteelModulusMismatch: the elastic modulus of the analysis steel 'Acciaio carpenteria' (205000 MPa) differs from that of the steel of the section 'Acciaio carpenteria' (210000 MPa)");
        Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, null!, 18.41, 6.18, 16.9, 0, true, true));
        // A difference in the last bits of E (below 1e-12) is the same E.
        new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, AnalysisSteel(210000 * (1 + 4e-16)), 18.41, 6.18, 16.9, 0, true, true);
        // Archived steels are checked when read: the E of the analysis steel changed in the document.
        string xml = Encoding.UTF8.GetString(new BridgeCompositeSectionDefinition("Composite", Example50(), rows50, AnalysisSteel(), 18.41, 6.18, 16.9, 0, true, true).Save());
        int at = xml.IndexOf("AnalysisSteel", StringComparison.Ordinal); Assert.IsTrue(at > 0);
        var modulus = new System.Text.RegularExpressions.Regex("(ElasticModulusTension[^<>]*>)210000<");
        var match = modulus.Match(xml, at); Assert.IsTrue(match.Success);
        string tampered = xml.Substring(0, match.Index) + match.Groups[1].Value + "205000<" + xml.Substring(match.Index + match.Length);
        Assert.ThrowsException<ArgumentException>(() => BridgeCompositeSectionDefinition.Read(Encoding.UTF8.GetBytes(tampered)));
    }

    [TestMethod]
    public void InvalidRebarRowsAreRejected()
    {
        foreach (double area in new[] { -1, -1e-300, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeRebarRows(area, 50, 0, 50, 3), "top area " + area);
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeRebarRows(0, 50, area, 50, 3), "bottom area " + area);
        }
        foreach (double cover in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeRebarRows(3500, cover, 3500, 50, 3), "top cover " + cover);
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeRebarRows(3500, 50, 3500, cover, 3), "bottom cover " + cover);
        }
        foreach (int bars in new[] { 0, -1, int.MinValue })
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeRebarRows(3500, 50, 3500, 50, bars), "bars " + bars);
        var section = Example50();
        Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, null!, 18, 6, 16, 0, true, true));
        // Rows that do not describe the bars of the section: another area, another number of bars, a row the section has not, rows without bars.
        foreach (var rows in new[] { new BridgeCompositeRebarRows(3600, 50, 3500, 50, 3), new BridgeCompositeRebarRows(3500, 50, 3500, 50, 2),
            new BridgeCompositeRebarRows(3500, 50, 0, 50, 3), new BridgeCompositeRebarRows(0, 50, 0, 50, 3) })
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, rows, 18, 6, 16, 0, true, true),
                $"{rows.TopAreaMm2} {rows.BottomAreaMm2} {rows.BarsPerRow}");
        // Archived rows are checked when read.
        string xml = Encoding.UTF8.GetString(new BridgeCompositeSectionDefinition("C", section, new BridgeCompositeRebarRows(3500, 50, 3500, 50, 3), 18, 6, 16, 0, true, true).Save());
        var barsElement = new System.Text.RegularExpressions.Regex("(BarsPerRow[^<>]*>)3<");
        Assert.AreEqual(1, barsElement.Matches(xml).Count);
        Assert.ThrowsException<ArgumentException>(() => BridgeCompositeSectionDefinition.Read(Encoding.UTF8.GetBytes(barsElement.Replace(xml, "${1}0<"))));
        Assert.ThrowsException<ArgumentException>(() => BridgeCompositeSectionDefinition.Read(Encoding.UTF8.GetBytes(barsElement.Replace(xml, "${1}2<"))));
    }

    [TestMethod]
    public void InvalidDefinitionsAreRejected()
    {
        var section = Example50();
        foreach (var name in new[] { null, "", " " })
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition(name!, section, 18, 6, 16, 0, true, true));
        Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", null!, 18, 6, 16, 0, true, true));
        foreach (double n in new[] { 0, -0.0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, n, 6, 16, 0, true, true), "n∞ " + n);
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, 18, n, 16, 0, true, true), "n0 " + n);
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, 18, 6, n, 0, true, true), "nsh " + n);
        }
        foreach (double offset in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.ThrowsException<ArgumentException>(() => new BridgeCompositeSectionDefinition("C", section, 18, 6, 16, offset, true, true));
        // A section the H bridge engine does not check: a plate instead of the H.
        var concrete = new ConcreteMaterialEN1992("C", 32, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterialEN1992("S", 210000, 355, 510, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        var box = ReinforcedConcreteSection.CreateBridgeSection(3000, 300, concrete, null, 0, 50, null, 0, new SectionRectangular(1000, 600, "plate"), steel, 50, 0, "box");
        Assert.ThrowsException<NotSupportedException>(() => new BridgeCompositeSectionDefinition("C", box, 18, 6, 16, 0, true, true));
        // Archived values are checked when read.
        string xml = Encoding.UTF8.GetString(new BridgeCompositeSectionDefinition("C", section, 18.5, 6, 16, 0, true, true).Save());
        StringAssert.Contains(xml, ">18.5<");
        Assert.ThrowsException<ArgumentException>(() => BridgeCompositeSectionDefinition.Read(Encoding.UTF8.GetBytes(xml.Replace(">18.5<", ">-18.5<"))));
    }
}
