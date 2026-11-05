using System.Runtime.Serialization;
using System.Text;
using CompositeBridgeTests;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.Layout;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Models;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.CompositeBridge;

/// <summary>
/// The bridge model by composition (lane L12, correzioni-L12.md): a generic GPC model and the bridge data outside it, keyed by element GUID.
/// Ported from the tests of the bridge attributes of lane L11 (round trips, validation, copies, the writer and the fingerprints untouched),
/// plus the package, the copies with a new identity and D3 on a bridge model.
/// </summary>
[TestClass]
public class BridgeModelTests
{
    private static Guid Id(int n) => Guid.Parse("00000000-0000-0000-0000-0000000012" + n.ToString("00"));

    private static ReinforcedConcreteSection Girder(double slabWidth, double d, string name)
    {
        var concrete = new ConcreteMaterialEN1992("C32", 32, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterialEN1992("S355", 210000, 355, 510, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        var rebar = new SteelMaterialEN1992("B450C", 200000, 450, 540, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
        var bar = new RebarSectionCircular("", Math.Sqrt(3500.0 / 3 * 4 / Math.PI), rebar);
        return ReinforcedConcreteSection.CreateBridgeSection(slabWidth, 300, concrete, bar, slabWidth / 3, 50, bar, slabWidth / 3,
            new SectionH(d, 16, 600, 30, 600, 30, "1"), steel, 50, 0, name);
    }

    /// <summary>Four nodes, three beams numbered 1-3 and a triangle; a rigid connection on node 1 (it stays in GPCModel).</summary>
    private static Model Bare()
    {
        var model = new Model(Id(1), "Bridge");
        var a = new NodeElement(Id(2), new Point3d(0, 0, 0), null, "1", 1);
        var b = new NodeElement(Id(3), new Point3d(6000, 0, 0), null, "2", 2);
        var c = new NodeElement(Id(4), new Point3d(12000, 0, 0), null, "3", 3);
        var d = new NodeElement(Id(5), new Point3d(0, 3000, 0), null, "4", 4);
        foreach (var node in new[] { a, b, c, d }) model.NodesElements.Add(node);
        BeamElement Beam(int id, int number, NodeElement i, NodeElement j)
        {
            var beam = new BeamElement(Id(id), i.Position, j.Position, null, null, number.ToString(), number);
            beam.ConnectNodes(i, j); beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; return beam;
        }
        // Added out of order: the queries follow the numbering of the model, not the order of addition.
        model.BeamElements.Add(Beam(8, 3, a, d)); model.BeamElements.Add(Beam(6, 1, a, b)); model.BeamElements.Add(Beam(7, 2, b, c));
        var shell = new AreaElement(Id(9), null, null, null, "1", 1); shell.ConnectNodes(a, b, d); model.AreaElements.Add(shell);
        a.Attributes.Add(new RigidConnectionAttribute(new Point3d(0, 0, -1250.5)));
        return model;
    }

    private static BeamElement Beam(Model model, int number) => model.BeamElements.Values.Single(b => b.Id == number);
    private static BridgeMemberMetadata Part(int n, double from, double to) => new("G1", BridgeMemberFamily.Girder, 1, 2, 1, from, to, new Vector3d(0, 0.1, 1), "G1-S2/P" + n);

    /// <summary>Every datum: physical member G1 of two parts (beams 1 and 2), super element G1 (beams 1, 2), a local state on beam 1, a constant
    /// composite section shared by beams 1 and 2, a variable one on beam 3, which is a diaphragm.</summary>
    private static BridgeModel Sample() => Sample(Bare());

    private static BridgeModel Sample(Model model)
    {
        var data = new BridgeModelData();
        Guid b1 = Beam(model, 1).Guid, b2 = Beam(model, 2).Guid, b3 = Beam(model, 3).Guid;
        var constant = new BridgeCompositeSectionDefinition("G1", Girder(3000, 2500, "G1"), 18.375, 6.125, 16.5, 0, true, true);
        data.Set(new BridgeBeamData(b1, Part(1, 0, 6000), "G1", new BridgeLocalSectionState(BridgeActionBehavior.LongTerm, true, 18.375, "M01_NINF",
            BridgeWorkflowDefinition.PropertyFingerprint(Beam(model, 1))), new BridgeBeamCompositeSection(constant)));
        data.SetMember(b2, Part(2, 6000, 12000)); data.SetSuperElement(b2, "G1"); data.SetCompositeSection(b2, new BridgeBeamCompositeSection(constant));
        data.SetMember(b3, new BridgeMemberMetadata("D1", BridgeMemberFamily.SolidDiaphragm, 1, 1, 0, 0, 0, new Vector3d(0, 0, 1)));
        data.SetCompositeSection(b3, new BridgeBeamCompositeSection(new BridgeCompositeSectionDefinition("D1a", Girder(2500, 1800, "D1a"), 18, 6, 16, 10, true, false),
            new BridgeCompositeSectionDefinition("D1b", Girder(2500, 2000, "D1b"), 18, 6, 16, 10, true, false)));
        return new BridgeModel(model, data);
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

    private static void SameData(BridgeModel bridge)
    {
        var model = bridge.Model; var data = bridge.Data;
        Assert.AreEqual(3, data.Count);
        var first = data.Beam(Beam(model, 1).Guid)!;
        Assert.AreEqual("G1", first.Member!.PhysicalId); Assert.AreEqual(BridgeMemberFamily.Girder, first.Member.Family); Assert.AreEqual(1, first.Member.Group);
        Assert.AreEqual(2, first.Member.Segment); Assert.AreEqual(1, first.Member.Span); Assert.AreEqual(0, first.Member.StationFromMm); Assert.AreEqual(6000, first.Member.StationToMm);
        Assert.AreEqual(Bits(0.1), Bits(first.Member.SectionUp.Y)); Assert.AreEqual("G1-S2/P1", first.Member.GeometryId);
        Assert.AreEqual("G1", first.SuperElement);
        var state = first.SectionState!;
        Assert.AreEqual(BridgeActionBehavior.LongTerm, state.Behavior); Assert.IsTrue(state.ConcreteExcluded); Assert.AreEqual(Bits(18.375), Bits(state.ModularRatio));
        Assert.AreEqual("M01_NINF", state.ClassificationCase); Assert.AreEqual(BridgeWorkflowDefinition.PropertyFingerprint(Beam(model, 1)), state.PropertyFingerprint);
        var second = data.Beam(Beam(model, 2).Guid)!;
        Assert.AreEqual("G1-S2/P2", second.Member!.GeometryId); Assert.AreEqual(6000, second.Member.StationFromMm); Assert.IsNull(second.SectionState);
        // The constant section of beams 1 and 2 is one definition, the variable one of beam 3 two.
        Assert.IsTrue(first.CompositeSection!.IsConstant); Assert.AreSame(first.CompositeSection.Start, second.CompositeSection!.Start);
        var third = data.Beam(Beam(model, 3).Guid)!;
        Assert.IsFalse(third.CompositeSection!.IsConstant); Assert.AreEqual("D1b", third.CompositeSection.End.Name); Assert.AreEqual(Bits(10), Bits(third.CompositeSection.End.VerticalOffsetMm));
        Assert.IsNull(third.SuperElement); Assert.AreEqual(BridgeMemberFamily.SolidDiaphragm, third.Member!.Family);
        Assert.AreEqual(-1250.5, model.NodesElements.Values.Single(n => n.Id == 1).Attributes.Values.OfType<RigidConnectionAttribute>().Single().TargetMm.Z);
    }

    private static byte[] Package(BridgeModel bridge, ModelArchiveFormat format) { using var s = new MemoryStream(); bridge.Save(s, format); return s.ToArray(); }
    private static BridgeModel Unpack(byte[] bytes) { using var s = new MemoryStream(bytes, false); return BridgeModel.Load(s); }
    private static byte[] ModelBytes(Model model, ModelArchiveFormat format) { using var s = new MemoryStream(); ModelArchive.Save(model, s, format); return s.ToArray(); }

    [TestMethod]
    public void TheBridgeModelRoundTripsInThePackageWithBothFormsOfTheModel()
    {
        var bridge = Sample(); SameData(bridge);
        Assert.AreEqual(0, bridge.Validate().Count, string.Join("; ", bridge.Validate().Select(d => d.Code)));
        string analysis = bridge.Model.AnalysisFingerprint(), data = bridge.Data.Fingerprint();
        foreach (var format in new[] { ModelArchiveFormat.Compact, ModelArchiveFormat.Xml })
        {
            var package = Package(bridge, format);
            Assert.AreEqual("GPCBRG", Encoding.ASCII.GetString(package, 0, 6)); Assert.AreEqual(1, package[6]);
            // The model part is the archive of ModelArchive, byte for byte.
            var model = ModelBytes(bridge.Model, format);
            CollectionAssert.AreEqual(model, package.Skip(package.Length - model.Length).ToArray(), format.ToString());
            var read = Unpack(package);
            SameData(read);
            Assert.AreEqual(analysis, read.Model.AnalysisFingerprint(), format.ToString());
            Assert.AreEqual(data, read.Data.Fingerprint(), format.ToString());
            Assert.AreEqual(bridge.Model.Guid, read.Model.Guid);
            Assert.AreEqual(0, read.Validate().Count);
            // Saved again: the same model and data (GPC.Geometry values carry their own GUID, which a reading renews, so the bytes differ).
            var again = Unpack(Package(read, format));
            Assert.AreEqual(analysis, again.Model.AnalysisFingerprint()); Assert.AreEqual(data, again.Data.Fingerprint()); SameData(again);
            // A non-seekable source reads the same.
            using var pipe = new NonSeekable(package);
            Assert.AreEqual(data, BridgeModel.Load(pipe).Data.Fingerprint());
        }
        // The data alone, as the other contracts of CompositeBridge.
        var alone = BridgeModelData.Read(bridge.Data.Save());
        Assert.AreEqual(data, alone.Fingerprint()); SameData(new BridgeModel(bridge.Model, alone));
        // The fingerprint does not depend on the order in which the beams were given.
        var reversed = new BridgeModelData();
        foreach (var record in bridge.Data.Beams.Reverse()) reversed.Set(record);
        Assert.AreEqual(data, reversed.Fingerprint());
        CollectionAssert.AreEqual(bridge.Data.Save(), reversed.Save());
        // Each datum enters the fingerprint.
        var changed = bridge.Data.Copy(); changed.SetSuperElement(Beam(bridge.Model, 2).Guid, "G2"); Assert.AreNotEqual(data, changed.Fingerprint());
        changed = bridge.Data.Copy(); changed.SetSectionState(Beam(bridge.Model, 1).Guid, null); Assert.AreNotEqual(data, changed.Fingerprint());
        changed = bridge.Data.Copy(); changed.SetMember(Beam(bridge.Model, 3).Guid, null); Assert.AreNotEqual(data, changed.Fingerprint());
        changed = bridge.Data.Copy(); changed.Remove(Beam(bridge.Model, 2).Guid); Assert.AreNotEqual(data, changed.Fingerprint());
        Assert.AreEqual(data, bridge.Data.Fingerprint(), "the source is unchanged");
    }

    [TestMethod]
    public void TheModelOfABridgeModelIsTheGenericModel()
    {
        // GPCModel has no bridge vocabulary any more (lane L12).
        var gpc = typeof(Model).Assembly;
        Assert.IsFalse(gpc.GetTypes().Any(t => t.Namespace == "GPC.Model.Structure.Bridges"));
        Assert.IsNull(gpc.GetType("GPC.Model.Structure.Members.BridgeMemberMetadata"));
        Assert.IsNull(gpc.GetType("GPC.Model.Analysis.CompositeSectionDefinition"));
        Assert.IsNull(typeof(GPC.Model.Structure.Members.PhysicalMemberDefinition).GetProperty("BridgeMetadata"));
        Assert.AreEqual(typeof(BridgeActionBehavior), typeof(BridgeLocalSectionState).GetProperty(nameof(BridgeLocalSectionState.Behavior))!.PropertyType);

        // The bridge data do not touch the model: the same analysis and binding fingerprints, the same archives (what the Straus7 writer
        // and the analysis receive), the same attributes, the same export preflight.
        var model = Bare();
        string analysis = model.AnalysisFingerprint(), binding = model.SolverBindingFingerprint();
        var xml = ModelBytes(model, ModelArchiveFormat.Xml); var compact = ModelBytes(model, ModelArchiveFormat.Compact);
        var attributes = model.AllElements.Sum(e => e.Attributes.Count);
        var bridge = Sample(model);
        Assert.AreSame(model, bridge.Model, "composition: the model itself, not a copy or a derived class");
        Assert.AreEqual(typeof(Model), bridge.Model.GetType());
        Assert.AreEqual(analysis, Bare().AnalysisFingerprint(), "an identical model without bridge data has the same analysis fingerprint");
        Assert.AreEqual(analysis, bridge.Model.AnalysisFingerprint());
        Assert.AreEqual(binding, bridge.Model.SolverBindingFingerprint());
        CollectionAssert.AreEqual(xml, ModelBytes(bridge.Model, ModelArchiveFormat.Xml));
        CollectionAssert.AreEqual(compact, ModelBytes(bridge.Model, ModelArchiveFormat.Compact));
        Assert.AreEqual(attributes, bridge.Model.AllElements.Sum(e => e.Attributes.Count));
        bridge.Model.RequireExportable();
        Assert.AreEqual(0, bridge.Model.PhysicalMembers.Count, "no physical member is created for the bridge members");
        // A recipe of the model stays valid when the bridge data change: they are not part of the model revision.
        var recipe = Recipe(bridge.Model); string revision = bridge.Model.AnalysisFingerprint();
        bridge.Data.Workflow = recipe; Assert.AreEqual(0, bridge.Validate().Count);
        bridge.Data.SetSuperElement(Beam(bridge.Model, 3).Guid, "D"); bridge.Data.SetSectionState(Beam(bridge.Model, 2).Guid,
            new BridgeLocalSectionState(BridgeActionBehavior.ShortTerm, false, 6, null, BridgeWorkflowDefinition.PropertyFingerprint(Beam(bridge.Model, 2))));
        recipe.Validate(bridge.Model); Assert.AreEqual(revision, bridge.Model.AnalysisFingerprint());
    }

    /// <summary>A recipe of one steel action on a load case of the model.</summary>
    private static BridgeWorkflowDefinition Recipe(Model model)
    {
        var load = model.LoadCases.Values.FirstOrDefault(c => c.Name == "dead");
        if (load == null) { load = new LoadCaseBase("dead"); model.AddLoadCase(load); }
        var action = new BridgeWorkflowActionDefinition { Key = "steel-dead", Behavior = BridgeActionBehavior.Steel, ForceCaseGuid = load.Guid, SourceCaseGuid = Guid.NewGuid(),
            SourceModelGuid = Guid.NewGuid(), SourceAnalysisFingerprint = "source-revision", BeamGuids = new[] { Beam(model, 1).Guid } };
        return new BridgeWorkflowDefinition { ModelGuid = model.Guid, AnalysisFingerprint = model.AnalysisFingerprint(),
            Phases = new[] { new BridgeWorkflowPhaseDefinition { Id = Guid.NewGuid(), Order = 1, Name = "Steel", State = BridgeWorkflowSectionState.Steel, Day = 0, Actions = new[] { action } } } };
    }

    private static string[] Codes(BridgeModel bridge, DiagnosticSeverity severity) => bridge.Validate().Where(d => d.Severity == severity).Select(d => d.Code).ToArray();

    [TestMethod]
    public void TheDataAreValidatedAgainstTheModelWithTheRulesOfTheDomainChecks()
    {
        var bridge = Sample();
        CollectionAssert.AreEqual(Array.Empty<string>(), Codes(bridge, DiagnosticSeverity.Error)); bridge.RequireValid();
        // A dangling key: data of a beam that is not in the model (removed after the data were given).
        var dangling = Sample(); dangling.Model.BeamElements.Remove(3);
        CollectionAssert.AreEqual(new[] { "UnknownBridgeElement" }, Codes(dangling, DiagnosticSeverity.Error));
        StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => dangling.RequireValid()).Message, "UnknownBridgeElement");
        var unknown = Sample(); unknown.Data.SetSuperElement(Guid.NewGuid(), "G1");
        CollectionAssert.AreEqual(new[] { "UnknownBridgeElement" }, Codes(unknown, DiagnosticSeverity.Error));
        // Beam data on a node or on a plate.
        foreach (var place in new Func<Model, Guid>[] { m => m.NodesElements.Values.First().Guid, m => m.AreaElements.Values.Single().Guid })
            foreach (var give in new Action<BridgeModelData, Guid>[] { (d, g) => d.SetMember(g, Part(1, 0, 1)), (d, g) => d.SetSuperElement(g, "G1"),
                (d, g) => d.SetSectionState(g, new BridgeLocalSectionState(BridgeActionBehavior.ShortTerm, false, 6, null, "F")),
                (d, g) => d.SetCompositeSection(g, d.CompositeSection(d.Beams.First().BeamGuid)) })
            {
                var wrong = Sample(); give(wrong.Data, place(wrong.Model));
                CollectionAssert.AreEqual(new[] { "BridgeDataOnWrongElement" }, Codes(wrong, DiagnosticSeverity.Error));
                StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => wrong.RequireValid()).Message, "BridgeDataOnWrongElement");
            }
        // A section state computed for another property: a warning (the checks refuse it), not an error.
        var stale = Sample(); Beam(stale.Model, 1).BeamProperty = new GPC.Model.ElementProperties.ElasticBeamProperty("other", new Material("steel", 210000, .3, 0, 0), new SectionRectangular(100, 200));
        CollectionAssert.AreEqual(new[] { "StaleBridgeSectionState" }, Codes(stale, DiagnosticSeverity.Warning));
        CollectionAssert.AreEqual(Array.Empty<string>(), Codes(stale, DiagnosticSeverity.Error)); stale.RequireValid();
        // A recipe of another model or revision.
        var recipe = Sample(); recipe.Data.Workflow = Recipe(recipe.Model); Assert.AreEqual(0, recipe.Validate().Count);
        Beam(recipe.Model, 3).BeamProperty = new GPC.Model.ElementProperties.ElasticBeamProperty("other", new Material("steel", 210000, .3, 0, 0), new SectionRectangular(100, 200));
        CollectionAssert.AreEqual(new[] { "InvalidBridgeWorkflow" }, Codes(recipe, DiagnosticSeverity.Error));
        StringAssert.Contains(recipe.Validate().Single().Message, "StaleBridgeWorkflowModel");
        // One record per beam, one datum per kind: setting again replaces; an empty record removes the beam.
        var data = Sample().Data; var b1 = data.Beams.First().BeamGuid; int count = data.Count;
        data.SetSuperElement(b1, "G9"); Assert.AreEqual("G9", data.SuperElement(b1)); Assert.AreEqual(count, data.Count);
        data.Set(new BridgeBeamData(b1)); Assert.IsNull(data.Beam(b1)); Assert.AreEqual(count - 1, data.Count);
    }

    [TestMethod]
    public void InvalidValuesAndDocumentsAreRejected()
    {
        foreach (var name in new[] { "", " ", " G1", "G1 ", "\tG1" })
            Assert.ThrowsException<ArgumentException>(() => new BridgeBeamData(Id(6), superElement: name), name);
        Assert.ThrowsException<ArgumentException>(() => new BridgeBeamData(Guid.Empty, superElement: "G1"));
        Assert.ThrowsException<ArgumentException>(() => new BridgeLocalSectionState(BridgeActionBehavior.Steel, false, 6, "C", "F"));
        Assert.ThrowsException<ArgumentException>(() => new BridgeLocalSectionState((BridgeActionBehavior)7, false, 6, "C", "F"));
        foreach (double n in new[] { 0, -0.0, -6, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.ThrowsException<ArgumentException>(() => new BridgeLocalSectionState(BridgeActionBehavior.ShortTerm, false, n, "C", "F"), n.ToString());
        Assert.ThrowsException<ArgumentException>(() => new BridgeLocalSectionState(BridgeActionBehavior.ShortTerm, false, 6, " ", "F"));
        foreach (var fingerprint in new[] { null, "", " " })
            Assert.ThrowsException<ArgumentException>(() => new BridgeLocalSectionState(BridgeActionBehavior.Shrinkage, false, 6, "C", fingerprint!));
        Assert.IsNull(new BridgeLocalSectionState(BridgeActionBehavior.Shrinkage, false, 6, null, "F").ClassificationCase, "no classification case");
        Assert.ThrowsException<ArgumentNullException>(() => new BridgeBeamCompositeSection(null!));
        Assert.ThrowsException<ArgumentException>(() => new BridgeMemberMetadata(" ", BridgeMemberFamily.Girder, 1, 1, 1, 0, 1, new Vector3d(0, 0, 1)), "the metadata validate themselves, as before");
        Assert.ThrowsException<ArgumentException>(() => new BridgeMemberMetadata("G", BridgeMemberFamily.Girder, 1, 1, 1, 0, 1, new Vector3d(0, 0, 0)));
        Assert.ThrowsException<ArgumentException>(() => new BridgeMemberMetadata("G", BridgeMemberFamily.Girder, 1, 1, 1, double.NaN, 1, new Vector3d(0, 0, 1)));
        Assert.ThrowsException<ArgumentNullException>(() => new BridgeModel(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new BridgeModel(Bare(), null!));

        // Archived data are checked when read.
        var bridge = Sample(); string xml = Encoding.UTF8.GetString(bridge.Data.Save());
        Assert.IsTrue(xml.Contains(">18.375<") && xml.Contains(">LongTerm<") && xml.Contains(">G1<"), xml);
        void Refused(string text, string code)
        {
            var error = Assert.ThrowsException<SerializationException>(() => BridgeModelData.Read(Encoding.UTF8.GetBytes(text)));
            StringAssert.Contains(error.ToString(), code);
        }
        Refused(xml.Replace(">18.375<", ">-18.375<"), "InvalidBridgeSectionModularRatio");
        Refused(xml.Replace(">LongTerm<", ">Steel<"), "InvalidBridgeSectionBehavior");
        Refused(xml.Replace(">G1<", ">G1 <"), "InvalidSuperElementName");
        Refused(xml.Replace("<schema>1</schema>", "<schema>2</schema>"), "UnsupportedBridgeModelDataSchema");
        // The same beam twice: the duplicates of lane L11 cannot be written, and are refused when read.
        var b1 = bridge.Model.BeamElements.Values.Single(b => b.Id == 1).Guid; var b2 = bridge.Model.BeamElements.Values.Single(b => b.Id == 2).Guid;
        Assert.IsTrue(xml.Contains(b2.ToString("D")));
        Refused(xml.Replace(b2.ToString("D"), b1.ToString("D")), "DuplicateBridgeBeamData");
        Refused(xml.Replace(b2.ToString("D"), Guid.Empty.ToString("D")), "EmptyBridgeBeamGuid");
        var dtd = Assert.ThrowsException<SerializationException>(() => BridgeModelData.Read(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY p 'blocked'>]><BridgeModelData>&p;</BridgeModelData>")));
        Assert.IsInstanceOfType(dtd.InnerException, typeof(System.Xml.XmlException), "DTDs are prohibited");
        // A composite section that does not hold any more (here: a negative ratio) is refused too.
        Refused(xml.Replace(">16.5<", ">-16.5<"), "InvalidBridgeCompositeSection");

        // The package: header, version, truncation.
        var package = Package(bridge, ModelArchiveFormat.Compact);
        void Broken(byte[] bytes, string code) => StringAssert.Contains(Assert.ThrowsException<SerializationException>(() => Unpack(bytes)).Message, code);
        var wrong = (byte[])package.Clone(); wrong[0] = (byte)'X'; Broken(wrong, "NotABridgeModelPackage");
        var future = (byte[])package.Clone(); future[6] = 2; Broken(future, "UnsupportedBridgeModelPackageVersion");
        Broken(package.Take(5).ToArray(), "TruncatedBridgeModelPackage");
        Broken(package.Take(40).ToArray(), "TruncatedBridgeModelPackage");
        var huge = (byte[])package.Clone(); huge[10] = 0x7F; Broken(huge, "InvalidBridgeModelDataLength");
        Assert.ThrowsException<SerializationException>(() => Unpack(ModelBytes(bridge.Model, ModelArchiveFormat.Compact)), "a model archive is not a package");
    }

    [TestMethod]
    public void CopiesKeepTheKeysAndShareOrCopyTheCompositeSections()
    {
        var bridge = Sample(); bridge.Data.Workflow = Recipe(bridge.Model);
        string analysis = bridge.Model.AnalysisFingerprint(), data = bridge.Data.Fingerprint();
        var first = bridge.Data.Beams.First(b => b.SectionState != null);
        foreach (var options in new[] { ModelCopyOptions.None, ModelCopyOptions.ShareProperties, ModelCopyOptions.ShareProperties | ModelCopyOptions.ShareCoordinateSystems })
        {
            var copy = bridge.Copy(options);
            Assert.AreNotSame(bridge.Model, copy.Model); Assert.AreNotSame(bridge.Data, copy.Data);
            Assert.AreEqual(analysis, copy.Model.AnalysisFingerprint(), options.ToString()); Assert.AreEqual(data, copy.Data.Fingerprint(), options.ToString());
            Assert.AreEqual(bridge.Model.Guid, copy.Model.Guid); SameData(copy);
            Assert.AreEqual(0, copy.Validate().Count, "the recipe holds for the plain copy");
            // Immutable records are shared, the recipe is copied, the sections follow ShareProperties (shared among the beams in both cases).
            var same = copy.Data.Beam(first.BeamGuid)!;
            Assert.AreSame(first.Member, same.Member); Assert.AreSame(first.SectionState, same.SectionState);
            Assert.AreNotSame(bridge.Data.Workflow, copy.Data.Workflow);
            if (options.SharesProperties) Assert.AreSame(first.CompositeSection!.Start, same.CompositeSection!.Start);
            else
            {
                Assert.AreNotSame(first.CompositeSection!.Start, same.CompositeSection!.Start);
                Assert.AreNotSame(first.CompositeSection.Start.Section, same.CompositeSection.Start.Section);
                Assert.AreEqual(first.CompositeSection.Start.Fingerprint(), same.CompositeSection.Start.Fingerprint());
            }
            // The copy is independent: editing it leaves the source as it was.
            copy.Data.SetSuperElement(first.BeamGuid, "G7"); copy.Data.Workflow!.Phases[0].Name = "changed";
            Assert.AreEqual(data, bridge.Data.Fingerprint()); Assert.AreEqual("G1", bridge.Data.SuperElement(first.BeamGuid));
        }
        // Under a new model identity: the same elements and data; the recipe still names the source model, as its analysis state.
        var identity = Guid.Parse("4d2f7a6e-0c1b-4e5f-9a8b-123456789abc");
        var renamed = bridge.Copy(ModelCopyOptions.ShareProperties, identity);
        Assert.AreEqual(identity, renamed.Model.Guid);
        CollectionAssert.AreEqual(bridge.Model.AllElements.Select(e => e.Guid).ToArray(), renamed.Model.AllElements.Select(e => e.Guid).ToArray());
        Assert.AreEqual(data, renamed.Data.Fingerprint()); SameData(renamed);
        Assert.AreEqual(bridge.Model.Guid, renamed.Data.Workflow!.ModelGuid);
        CollectionAssert.AreEqual(new[] { "InvalidBridgeWorkflow" }, Codes(renamed, DiagnosticSeverity.Error));
        renamed.Data.Workflow = null; renamed.RequireValid();
        Assert.AreEqual(bridge.Model.Guid, bridge.Data.Workflow!.ModelGuid); Assert.AreEqual(0, bridge.Validate().Count, "the source is unchanged");
        Assert.ThrowsException<ArgumentException>(() => bridge.Copy(ModelCopyOptions.None, Guid.Empty));
    }

    [TestMethod]
    public void SuperElementsAndPhysicalMembersFollowTheNumberingOfTheModel()
    {
        var bridge = Sample();
        CollectionAssert.AreEqual(new[] { "G1" }, bridge.SuperElementNames().ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, bridge.SuperElement("G1").Select(b => b.Id).ToArray(), "the order of the former elements, not of the addition");
        Assert.AreEqual(0, bridge.SuperElement("G2").Count);
        CollectionAssert.AreEqual(new[] { "G1", "D1" }, bridge.PhysicalMemberIds().ToArray());
        var parts = bridge.PhysicalMember("G1");
        CollectionAssert.AreEqual(new[] { 1, 2 }, parts.Select(p => p.Beam.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "G1-S2/P1", "G1-S2/P2" }, parts.Select(p => p.Metadata.GeometryId).ToArray());
        // Girder stations of group 1 (as BridgeDeflectionCamber reads them).
        CollectionAssert.AreEqual(new[] { 0d, 6000, 6000, 12000 }, bridge.Data.Beams.Select(b => b.Member).Where(m => m != null && m.Family == BridgeMemberFamily.Girder && m.Group == 1)
            .SelectMany(m => new[] { m!.StationFromMm, m.StationToMm }).OrderBy(s => s).ToArray());
    }

    /// <summary>A beam with three analysed load cases and a recipe of three phases (the sample of <see cref="BridgeWorkflowExecutionTests"/>).</summary>
    private static (Model Model, BridgeWorkflowDefinition Workflow, BridgeWorkflowEvaluation Evaluation) Analysed()
    {
        var beam = new BeamElement(new Point3d(0, 0, 0), new Point3d(1000, 0, 0), null);
        beam.ConnectNodes(new NodeElement(beam.StartPoint), new NodeElement(beam.EndPoint));
        beam.Assignments.Formulation = BeamFormulation.StraightTwoNode;
        var model = ModelAssembly.Assemble(new[] { beam }, Guid.NewGuid()); beam = model.BeamElements.Values.Single();
        var loads = new[] { new LoadCaseBase("G1"), new LoadCaseBase("G2"), new LoadCaseBase("Q") }; foreach (var load in loads) model.AddLoadCase(load);
        string fp = model.AnalysisFingerprint(); var axes = new CoordinateSystem(new Point3d(500, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1));
        model.Datasets.Add("analysis", new AnalysisDataset { Id = "analysis", InputFingerprint = fp, NormalizedUnits = "N,mm,rad", Semantics = AnalysisSemantics.LinearStatic });
        double[] moments = { 1500, 2000, 3000 };
        for (int i = 0; i < loads.Length; i++)
        {
            var sample = new StationResultBeamForces(loads[i], new ResultBeamForces(0, 0, 0, 0, moments[i] * 1e6, 0, axes), .5)
            { Side = SectionSide.Left, Body = ActionBody.PositiveSectionFace, State = new ResultState { DatasetId = "analysis", InputFingerprint = fp,
                Semantics = AnalysisSemantics.LinearStatic, IsCombined = false, IsCumulative = true, ConcomitantStateId = loads[i].Name,
                Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray() } };
            beam.Results.Add(new BeamResult(new[] { sample }));
        }
        var workflow = new BridgeWorkflowDefinition { ModelGuid = model.Guid, AnalysisFingerprint = fp,
            Phases = loads.Select((load, i) => new BridgeWorkflowPhaseDefinition { Id = Guid.NewGuid(), Order = i, Name = load.Name,
                State = i == 0 ? BridgeWorkflowSectionState.Steel : BridgeWorkflowSectionState.Composite,
                Actions = new[] { new BridgeWorkflowActionDefinition { Key = load.Name, ForceCaseGuid = load.Guid, SourceCaseGuid = load.Guid,
                    SourceModelGuid = model.Guid, SourceAnalysisFingerprint = fp, Behavior = i == 0 ? BridgeActionBehavior.Steel : i == 1 ? BridgeActionBehavior.LongTerm : BridgeActionBehavior.ShortTerm } } }).ToArray() };
        var evaluation = new BridgeWorkflowEvaluation { BeamGuid = beam.Guid, PhysicalPropertyFingerprint = BridgeWorkflowDefinition.PropertyFingerprint(beam),
            SectionInput = CompositeBridgeApiTests.Input() with { Phases = Array.Empty<BridgePhase>() }, Station = .5, Side = SectionSide.Left, SectionAxes = axes, LongTermPhi = 2,
            Factors = loads.ToDictionary(l => l.Guid, l => 1d), Results = loads.ToDictionary(l => l.Guid, l => new ResultSelection { Dataset = "analysis", Case = l.Name }) };
        return (model, workflow, evaluation);
    }

    [TestMethod]
    public void D3RunsOnTheModelAndTheRecipeOfABridgeModel()
    {
        var (model, workflow, evaluation) = Analysed();
        var data = new BridgeModelData { Workflow = workflow };
        data.SetSuperElement(model.BeamElements.Values.Single().Guid, "G1");
        data.SetSectionState(model.BeamElements.Values.Single().Guid, new BridgeLocalSectionState(BridgeActionBehavior.LongTerm, false, 18, null,
            BridgeWorkflowDefinition.PropertyFingerprint(model.BeamElements.Values.Single())));
        var bridge = new BridgeModel(model, data);
        Assert.AreEqual(0, bridge.Validate().Count, string.Join("; ", bridge.Validate().Select(d => d.Message)));
        var expected = BridgeWorkflowExecutor.Calculate(workflow, model, new[] { model }, evaluation);
        var actual = BridgeWorkflowExecutor.Calculate(bridge, new[] { model }, evaluation);
        var planned = BridgeWorkflowExecutor.Prepare(bridge, new[] { model }).Calculate(evaluation);
        foreach (var result in new[] { actual, planned })
        {
            Assert.AreEqual(expected.Stages.Count, result.Stages.Count);
            for (int i = 0; i < result.Stages.Count; i++)
            {
                Assert.AreEqual(expected.Stages[i].Points.Count, result.Stages[i].Points.Count);
                for (int j = 0; j < result.Stages[i].Points.Count; j++) Assert.AreEqual(Bits(expected.Stages[i].Points[j].Stress), Bits(result.Stages[i].Points[j].Stress));
            }
        }
        // The recipe of a package: saved and read with the data, still valid for the model read with it.
        using (var stream = new MemoryStream())
        {
            bridge.Save(stream); stream.Position = 0; var read = BridgeModel.Load(stream);
            Assert.AreEqual(0, read.Validate().Count);
            var again = BridgeWorkflowExecutor.Calculate(read, new[] { read.Model }, evaluation);
            Assert.AreEqual(Bits(expected.Stages.Last().Points.Last().Stress), Bits(again.Stages.Last().Points.Last().Stress));
        }
        // Without a recipe.
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => BridgeWorkflowExecutor.Prepare(new BridgeModel(model), new[] { model })).Message, "MissingBridgeWorkflow");
        Assert.ThrowsException<ArgumentNullException>(() => BridgeWorkflowExecutor.Prepare((BridgeModel)null!, new[] { model }));
    }

    /// <summary>A read-only stream that cannot seek (a pipe).</summary>
    private sealed class NonSeekable : Stream
    {
        private readonly MemoryStream inner;
        public NonSeekable(byte[] bytes) { inner = new MemoryStream(bytes, false); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 7));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
