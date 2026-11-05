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
/// The bridge vocabulary that lane L12 moved from GPCModel to CompositeBridge: the member metadata (CompositeBridge.Layout, used by
/// BridGHe) and the composite section definition. The tests they had in GPCModel (BridgeMetadataIsExplicitDetachedPersistedAndFingerprint-
/// Sensitive and the per-part metadata of review B4, the composite part of ElasticExportContractsTest), with their own Save/Read/Copy in
/// place of ModelValues.CopyValue and ModelValueArchive, which keep the closed vocabulary of GPCModel. The F5 inputs of the bridge
/// generator were removed (coordinator decision of 5/11: unused, branch-only).
/// </summary>
[TestClass]
public class BridgeLayoutContractsTests
{
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
        Assert.AreEqual(fingerprint, ModelValues.Fingerprint(new object[] { BridgeModelData.Read(With(p1, metadata).Save()).Member(p1)! }));
        // Invalid metadata in a document are refused when read.
        string xml = Encoding.UTF8.GetString(With(p1, metadata).Save());
        StringAssert.Contains(xml, ">girder-1<");
        StringAssert.Contains(Assert.ThrowsException<SerializationException>(() => BridgeModelData.Read(Encoding.UTF8.GetBytes(xml.Replace(">girder-1<", "> <")))).Message,
            "InvalidBridgeMemberMetadata");
    }

    private static BridgeModelData With(Guid beam, BridgeMemberMetadata metadata) { var data = new BridgeModelData(); data.SetMember(beam, metadata); return data; }

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
        // Another root, DTDs, and GPCModel's value archives, which do not know the bridge vocabulary any more.
        Assert.ThrowsException<SerializationException>(() => CompositeSectionDefinition.Read(new BridgeModelData().Save()), "another root");
        Assert.ThrowsException<SerializationException>(() => CompositeSectionDefinition.Read(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY p 'blocked'>]><CompositeSectionDefinition>&p;</CompositeSectionDefinition>")));
        using var stream = new MemoryStream();
        Assert.ThrowsException<SerializationException>(() => GPC.Model.Persistence.ModelValueArchive.Save(definition, stream));
        Assert.IsFalse(ModelValues.DataContracts.Any(t => t.Namespace != null && t.Namespace.StartsWith("GPC.Checkers", StringComparison.Ordinal)));
    }

    [TestMethod] public void OnlyTheMemberVocabularyIsInLayout()
    {
        // The F5 inputs of the bridge generator are gone (coordinator decision of 5/11); BridGHe uses the member metadata and family.
        var layout = typeof(BridgeMemberMetadata).Assembly.GetTypes().Where(t => t.Namespace == "GPC.Checkers.CompositeBridge.Layout" && t.IsPublic).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(new[] { "BridgeMemberFamily", "BridgeMemberMetadata" }, layout);
    }
}
