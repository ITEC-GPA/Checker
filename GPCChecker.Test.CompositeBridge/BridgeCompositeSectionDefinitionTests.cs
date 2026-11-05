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
using GPC.Model.Structure.Bridges;
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

    [TestMethod]
    public void TheSectionBehaviorOfModelHasTheNamesAndOrderOfTheActionBehavior()
    {
        CollectionAssert.AreEqual(Enum.GetNames(typeof(BridgeActionBehavior)), Enum.GetNames(typeof(BridgeSectionBehavior)));
        CollectionAssert.AreEqual(Enum.GetValues(typeof(BridgeActionBehavior)).Cast<int>().ToArray(), Enum.GetValues(typeof(BridgeSectionBehavior)).Cast<int>().ToArray());
    }
}
