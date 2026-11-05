using System.Runtime.Serialization;
using System.Text;
using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.Layout;
using GPC.Geometry;
using GPC.Model.Core;
using GPC.Model.Materials;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.CompositeBridge;

/// <summary>
/// The bridge vocabulary that lane L12 moved from GPCModel to CompositeBridge.Layout (the inputs of the bridge generator of F5, the member
/// metadata) and CompositeBridge (the composite section definition): the tests they had in GPCModel (BridgeDefinitionContractsTest, the B4
/// tests of ReviewFixesL1Test, BridgeMetadataIsExplicitDetachedPersistedAndFingerprintSensitive, the composite part of
/// ElasticExportContractsTest), with their own Save/Read/Copy in place of ModelValues.CopyValue and ModelValueArchive, which keep the
/// closed vocabulary of GPCModel.
/// </summary>
[TestClass]
public class BridgeLayoutContractsTests
{
    private static BridgeLayoutGeometry GeometrySample()
    {
        var start = new Point3d(0, 0, 0); var end = new Point3d(30000, 0, 0);
        return new BridgeLayoutGeometry { Id = Guid.NewGuid(), Nodes = new[] { start, end }, Stations = new[] { 0d, 30000 },
            Frames = new[] { new CoordinateSystem(start, new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)), new CoordinateSystem(end, new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)) },
            Members = new[] { new BridgeAxisMember { Guid = Guid.NewGuid(), Id = "G1/S1/B1", Family = BridgeMemberFamily.Girder, Group = 1, Segment = 1, Span = 1, NodeFrom = 1, NodeTo = 2, StationFrom = 0, StationTo = 30000 } },
            Supports = new[] { new BridgeSupportPoint { Support = 1, Girder = 1, Station = 0, Point = start }, new BridgeSupportPoint { Support = 2, Girder = 1, Station = 30000, Point = end } },
            Joints = new[] { new BridgeJointPoint { Joint = 1, Girder = 1, Station = 15000, Point = new Point3d(15000, 0, 0) } },
            Segments = new[] { new BridgeSegmentRange { Girder = 1, Segment = 1, Start = 0, End = 30000 } }, DeckThickness = 250,
            LeftEdge = new[] { new Point3d(0, 1000, 0), new Point3d(30000, 1000, 10) }, RightEdge = new[] { new Point3d(0, -1000, 0), new Point3d(30000, -1000, 0) },
            Deck = new BridgeDeckMesh { Vertices = new[] { new Point3d(0, -1000, 0), new Point3d(30000, -1000, 0), new Point3d(30000, 1000, 10), new Point3d(0, 1000, 0) }, Faces = new[] { new[] { 0, 1, 2, 3 } } } };
    }

    [TestMethod] public void SampledGeometryCopyAndArchiveKeepStableIdentityAndWarpedDeck()
    {
        var geometry = GeometrySample(); string fp = geometry.Fingerprint(); var copy = geometry.Copy();
        Assert.AreEqual(fp, copy.Fingerprint()); Assert.AreEqual(30000, copy.Axis(copy.Members[0].Guid).Length, 1e-8);
        Assert.AreEqual("Girder:1:1", copy.Members[0].PhysicalId); Assert.AreEqual(10, copy.Deck.Vertices[2].Z);
        Assert.AreNotSame(geometry.Nodes[0], copy.Nodes[0]); Assert.AreSame(copy.Nodes[0], copy.Supports[0].Point);
        copy.Deck.Vertices[2].Z = 20; Assert.AreEqual(10, geometry.Deck.Vertices[2].Z); Assert.AreNotEqual(fp, copy.Fingerprint());
        var restored = BridgeLayoutGeometry.Read(geometry.Save());
        Assert.AreEqual(fp, restored.Fingerprint()); Assert.AreSame(restored.Nodes[0], restored.Supports[0].Point);
    }

    [TestMethod] public void SampledGeometryRejectsDanglingIndicesDuplicateIdentityAndFoldedDeck()
    {
        var geometry = GeometrySample(); geometry.Members[0].NodeFrom = 0; Assert.ThrowsException<ArgumentException>(() => geometry.Validate());
        geometry = GeometrySample(); geometry.Members = new[] { geometry.Members[0], geometry.Members[0] }; Assert.ThrowsException<ArgumentException>(() => geometry.Validate());
        geometry = GeometrySample(); geometry.Deck.Faces[0] = new[] { 0, 2, 1, 3 }; Assert.ThrowsException<ArgumentException>(() => geometry.Validate());
        geometry = GeometrySample(); geometry.Supports[0].Station = -1; Assert.ThrowsException<ArgumentException>(() => geometry.Validate());
        string xml = Encoding.UTF8.GetString(GeometrySample().Save());
        Assert.IsTrue(xml.Contains("<schema>1</schema>"));
        Assert.ThrowsException<SerializationException>(() => BridgeLayoutGeometry.Read(Encoding.UTF8.GetBytes(xml.Replace("<schema>1</schema>", "<schema>2</schema>"))));
    }

    [TestMethod] public void StandaloneArchiveRejectsWrongRootDtdsAndOversizedDocuments()
    {
        var bytes = GeometrySample().Save();
        Assert.ThrowsException<SerializationException>(() => BridgeDefinition.Read(bytes), "another root");
        Assert.ThrowsException<SerializationException>(() => BridgeLayoutGeometry.Read(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY p 'blocked'>]><BridgeLayoutGeometry>&p;</BridgeLayoutGeometry>")));
        Assert.ThrowsException<ArgumentNullException>(() => BridgeLayoutGeometry.Read(null!));
        // An invalid geometry is not saved.
        var invalid = GeometrySample(); invalid.Members[0].NodeFrom = 0; Assert.ThrowsException<ArgumentException>(() => invalid.Save());
        // GPCModel's value archives do not know the bridge vocabulary any more.
        using var stream = new MemoryStream();
        Assert.ThrowsException<SerializationException>(() => GPC.Model.Persistence.ModelValueArchive.Save(GeometrySample(), stream));
        Assert.IsFalse(ModelValues.DataContracts.Any(t => t.Namespace != null && t.Namespace.StartsWith("GPC.Checkers", StringComparison.Ordinal)));
    }

    [TestMethod] public void SectionRulesKeepIndependentPropertiesSelectorsAndDimensionalScales()
    {
        var rule = new BridgeSectionRule { Family = BridgeMemberFamily.Girder, Group = 2, Start = 1000, End = 3000,
            Property = new GPC.Model.ElementProperties.ElasticBeamProperty("test", new Material("steel", 210000, .3, 0, 0), new SectionRectangular(100, 200)),
            ScaleFrom = new Vector2d(1, 2), ScaleTo = new Vector2d(3, 4) };
        rule.Validate(); var copy = rule.Copy(); Assert.AreEqual(ModelValues.Fingerprint(new object[] { rule }), ModelValues.Fingerprint(new object[] { copy }));
        Assert.AreEqual(ModelValues.Fingerprint(new object[] { rule }), ModelValues.Fingerprint(new object[] { BridgeSectionRule.Read(rule.Save()) }));
        Assert.AreNotSame(rule.Property, copy.Property); Assert.AreEqual(2, copy.Scale(2000).X); Assert.AreEqual(3, copy.Scale(2000).Y);
        Assert.AreEqual(1, copy.Scale(0).X); Assert.AreEqual(3, copy.Scale(4000).X);
        var member = new BridgeMemberMetadata("girder", rule.Family, 2, 3, 1, 1000, 3000, new Vector3d(0, 0, 1));
        Assert.IsTrue(copy.Selects(member)); copy.Segment = 4; Assert.IsFalse(copy.Selects(member));
        copy.ScaleFrom.X = 2; Assert.AreEqual(1, rule.ScaleFrom.X); copy.Family = BridgeMemberFamily.Post;
        Assert.ThrowsException<ArgumentException>(() => copy.Validate()); rule.End = rule.Start; Assert.ThrowsException<ArgumentException>(() => rule.Validate());
        var fallback = new BridgeFamilyProperty { Property = copy.Property }; fallback.Validate(); Assert.IsNull(fallback.Copy().Family);
        Assert.IsNull(BridgeFamilyProperty.Read(fallback.Save()).Family);
    }

    private static BridgeDefinition Sample(bool curved = false)
    {
        Curve3d alignment = curved ? new ArcCurve3d(new Point3d(0, 0, 0), new Vector3d(0, 0, 1), new Vector3d(1, 0, 0), 30000, 1)
            : new LineCurve3d(new Point3d(0, 0, 0), new Point3d(30000, 0, 0));
        return new BridgeDefinition { Id = Guid.NewGuid(), Alignment = alignment, Supports = new BridgeSupportLayout { Stations = new[] { 0d, 15000, 30000 } },
            Girders = new BridgeGirderLayout { Offsets = new[] { -3000d, 3000 } }, Fabrication = BridgeFabricationPlan.Create(30000, 2, new BridgeSegmentSettings()) };
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void IndependentDefinitionCopyAndXmlKeepGeometryAndFingerprint(bool curved)
    {
        var definition = Sample(curved); string fp = definition.Fingerprint(); var copy = definition.Copy();
        Assert.AreEqual(fp, copy.Fingerprint()); Assert.AreNotSame(definition.Alignment, copy.Alignment);
        for (int i = 0; i <= 10; i++) Assert.AreEqual(0, GPC.Model.Core.Coordinates.Axes.Length(definition.Alignment.PointAtLength(i * 3000) - copy.Alignment.PointAtLength(i * 3000)), 1e-8);
        Assert.AreEqual(fp, BridgeDefinition.Read(definition.Save()).Fingerprint());
        copy.Deck.Slopes[0] = .02; Assert.AreNotEqual(fp, copy.Fingerprint()); Assert.AreEqual(fp, definition.Fingerprint());
        copy.Alignment.Move(100, 0, 0); Assert.AreEqual(curved ? 30000 : 0, definition.Alignment.StartPoint.X, 1e-8);
    }

    [TestMethod] public void FabricationPrecedesMeshAndExplicitJointsTakePrecedence()
    {
        var settings = new BridgeSegmentSettings { MaximumLength = 12000 }; settings.Joints.Add(1, new[] { 8000d });
        var plan = BridgeFabricationPlan.Create(30000, 2, settings); plan.Validate();
        CollectionAssert.AreEqual(new[] { 0d, 8000 }, plan.Ranges.Where(r => r.Girder == 1).Select(r => r.Start).ToArray());
        CollectionAssert.AreEqual(new[] { 8000d, 30000 }, plan.Ranges.Where(r => r.Girder == 1).Select(r => r.End).ToArray());
        CollectionAssert.AreEqual(new[] { 0d, 10000, 20000 }, plan.Ranges.Where(r => r.Girder == 2).Select(r => r.Start).ToArray());
        settings.Joints[1][0] = 9000; Assert.AreEqual(8000, plan.Settings.Joints[1][0]);
        plan.Settings.Joints[1][0] = 10000; StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => plan.Validate()).Message, "StaleBridgeFabricationRanges");
    }

    [TestMethod] public void CommonAndLocalJointsProduceDeterministicOrderedSegments()
    {
        var a = new BridgeSegmentSettings(); a.Joints.Add(2, new[] { 20000d, 10000 }); a.Joints.Add(0, new[] { 10000d });
        var b = new BridgeSegmentSettings(); b.Joints.Add(0, new[] { 10000d }); b.Joints.Add(2, new[] { 20000d, 10000 });
        var pa = BridgeFabricationPlan.Create(30000, 2, a); var pb = BridgeFabricationPlan.Create(30000, 2, b);
        Assert.AreEqual(ModelValues.Fingerprint(new object[] { pa }), ModelValues.Fingerprint(new object[] { pb }));
        Assert.AreEqual(5, pa.Ranges.Length); Assert.IsTrue(pa.Ranges.All(r => r.End > r.Start));
    }

    [TestMethod] public void InvalidLayoutAndUnmatchedFabricationAreRejected()
    {
        var definition = Sample(); definition.Supports.Stations = new[] { 0d, 0, 30000 }; Assert.ThrowsException<ArgumentException>(() => definition.Validate());
        definition = Sample(); definition.Deck.Slopes = new[] { .01, .02 }; definition.Deck.SlopeStations = new[] { 1000d, 30000 }; Assert.ThrowsException<ArgumentException>(() => definition.Validate());
        definition = Sample(); definition.Fabrication = BridgeFabricationPlan.Create(20000, 2, new BridgeSegmentSettings()); Assert.ThrowsException<ArgumentException>(() => definition.Validate());
        definition = Sample(); definition.Supports.SkewRadians[0] = Math.PI / 2; Assert.ThrowsException<ArgumentException>(() => definition.Validate());
        definition = Sample(); definition.Diaphragms.Truss = true; Assert.ThrowsException<ArgumentException>(() => definition.Validate());
    }

    [TestMethod] public void FutureSchemaAndUnboundedSubdivisionAreRejected()
    {
        string xml = Encoding.UTF8.GetString(Sample().Save());
        Assert.IsTrue(xml.Contains("<schema>1</schema>"));
        Assert.ThrowsException<SerializationException>(() => BridgeDefinition.Read(Encoding.UTF8.GetBytes(xml.Replace("<schema>1</schema>", "<schema>2</schema>"))));
        Assert.ThrowsException<ArgumentException>(() => BridgeFabricationPlan.Create(30000, 2, new BridgeSegmentSettings { MaximumLength = .001 }));
    }

    // Review B4 (GPCModel until lane L12): a smooth host alignment (Rhino NURBS) as an exact chainage table that keeps the host length.
    [TestMethod] public void AlignmentTableKeepsTheHostLengthAndIsSmooth()
    {
        double radius = 100000, length = 30000.123456789; int count = 7;
        var stations = Enumerable.Range(0, count).Select(i => i == count - 1 ? length : length * i / (count - 1)).ToArray(); // the host length, exactly
        var table = new BridgeAlignmentTable { Stations = stations,
            Points = stations.Select(s => new Point3d(radius * Math.Sin(s / radius), radius * (1 - Math.Cos(s / radius)), 0)).ToArray(),
            Tangents = stations.Select(s => new Vector3d(Math.Cos(s / radius), Math.Sin(s / radius), 0)).ToArray() };
        var definition = new BridgeDefinition { Id = Guid.NewGuid(), AlignmentTable = table,
            Supports = new BridgeSupportLayout { Stations = new[] { 0d, length } },
            Girders = new BridgeGirderLayout { Offsets = new[] { -3000d, 3000 } },
            Fabrication = BridgeFabricationPlan.Create(length, 2, new BridgeSegmentSettings()) };
        definition.Validate(); Assert.AreEqual(length, definition.AlignmentLength);
        double s = length * .37; var p = table.PointAt(s);
        Assert.AreEqual(radius * Math.Sin(s / radius), p.X, 1e-3); Assert.AreEqual(radius * (1 - Math.Cos(s / radius)), p.Y, 1e-3);
        Assert.AreEqual(1, table.TangentAt(stations[3]).X / Math.Cos(stations[3] / radius), 1e-12);
        var copy = definition.Copy(); Assert.AreEqual(definition.Fingerprint(), copy.Fingerprint());
        var moved = copy.AlignmentTable.Points[3]; copy.AlignmentTable.Points[3] = new Point3d(moved.X, moved.Y, moved.Z + .001); Assert.AreNotEqual(definition.Fingerprint(), copy.Fingerprint());
        copy.AlignmentTable.Stations[3] = copy.AlignmentTable.Stations[2]; Assert.AreEqual("AlignmentStationsMustIncrease", Assert.ThrowsException<ArgumentException>(() => copy.Validate()).Message);
        definition.Alignment = new LineCurve3d(new Point3d(0, 0, 0), new Point3d(length, 0, 0));
        Assert.AreEqual("InvalidBridgeAlignmentIdentity", Assert.ThrowsException<ArgumentException>(() => definition.Validate()).Message);
        // Without the table, the historical text and fingerprint (the member is omitted when null).
        var plain = Sample(); StringAssert.DoesNotMatch(Encoding.UTF8.GetString(plain.Save()), new System.Text.RegularExpressions.Regex("AlignmentTable"));
    }

    // MigrationFoundationsTest (GPCModel until lane L12): the member metadata are explicit, detached, persisted and fingerprint sensitive;
    // review B4: one metadata per FE part ("Id/P1", "Id/P2"), now per beam GUID in the bridge data.
    [TestMethod] public void MemberMetadataAreDetachedPersistedAndFingerprintSensitivePerFePart()
    {
        var direction = new Vector3d(0, 0, 1);
        var metadata = new BridgeMemberMetadata("girder-1", BridgeMemberFamily.Girder, 2, 3, 1, 12000, 18000, direction, "alignment-1");
        direction.Z = 8; metadata.SectionUp.Z = 9;
        Assert.AreEqual(1.0, metadata.SectionUp.Z, "the direction given and the direction read are copies");
        string fingerprint = ModelValues.Fingerprint(new object[] { metadata });
        Assert.AreNotEqual(fingerprint, ModelValues.Fingerprint(new object[] { new BridgeMemberMetadata("girder-1", BridgeMemberFamily.Girder, 2, 3, 1, 12000, 18000.5, new Vector3d(0, 0, 1), "alignment-1") }));
        Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid();
        var data = new BridgeModelData();
        data.SetMember(p1, new BridgeMemberMetadata("G1", BridgeMemberFamily.Girder, 1, 2, 1, 0, 5000, new Vector3d(0, 0, 1), "G1-S2/P1"));
        data.SetMember(p2, new BridgeMemberMetadata("G1", BridgeMemberFamily.Girder, 1, 2, 1, 5000, 10000, new Vector3d(0, 0, 1), "G1-S2/P2"));
        var read = BridgeModelData.Read(data.Save());
        Assert.AreEqual("G1-S2/P2", read.Member(p2)!.GeometryId); Assert.AreEqual(5000, read.Member(p2)!.StationFromMm); Assert.AreEqual(10000, read.Member(p2)!.StationToMm);
        Assert.AreEqual(data.Fingerprint(), read.Fingerprint());
        Assert.AreEqual(fingerprint, ModelValues.Fingerprint(new object[] { BridgeModelData.Read(BridgeModelDataWith(p1, metadata).Save()).Member(p1)! }));
    }

    private static BridgeModelData BridgeModelDataWith(Guid beam, BridgeMemberMetadata metadata) { var data = new BridgeModelData(); data.SetMember(beam, metadata); return data; }

    // ElasticExportContractsTest (GPCModel until lane L12): the composite section definition keeps the physical section and its ratios.
    [TestMethod] public void CompositeDefinitionRetainsThePhysicalSectionAndArchivesIndependently()
    {
        var concrete = new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterialEN1993("S355", 210000, 355, 510);
        var section = GPC.Model.Sections.Concrete.ReinforcedConcreteSection.CreateBridgeSection(1000, 200, concrete, null, 0, 40, null, 0, new SectionRectangular(300, 200), steel);
        double n0 = steel.ElasticModulusTension / concrete.ElasticModulusCompression;
        var definition = new CompositeSectionDefinition { PhysicalSection = section, LongTermRatio = 2 * n0, ShortTermRatio = n0, IncludeReinforcement = false };
        definition.Validate(); string fingerprint = definition.Fingerprint();
        var copy = CompositeSectionDefinition.Read(definition.Save());
        Assert.AreEqual(fingerprint, copy.Fingerprint()); Assert.AreEqual(2 * n0, copy.EffectiveShrinkageRatio); Assert.AreEqual(definition.Id, copy.Id);
        copy.ShrinkageRatio = 3 * n0; Assert.AreNotEqual(fingerprint, copy.Fingerprint()); Assert.IsNull(definition.ShrinkageRatio);
        Assert.AreNotSame(definition.PhysicalSection, definition.Copy().PhysicalSection); Assert.AreEqual(fingerprint, definition.Copy().Fingerprint());
        var full = GPC.Model.Analysis.CompositeSectionProperties.Calculate(section, 0, true, false, true);
        Assert.AreEqual(200000 + n0 * 60000, full.areaH, 1e-6);
        Assert.AreEqual(fingerprint, definition.Fingerprint());
        definition.ShortTermRatio = double.NaN; Assert.ThrowsException<ArgumentException>(() => definition.Validate());
        string xml = Encoding.UTF8.GetString(copy.Save());
        Assert.ThrowsException<SerializationException>(() => CompositeSectionDefinition.Read(Encoding.UTF8.GetBytes(xml.Replace("<schema>1</schema>", "<schema>2</schema>"))));
    }
}
