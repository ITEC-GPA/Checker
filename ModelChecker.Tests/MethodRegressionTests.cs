using System.Runtime.Serialization;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Results.Locations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

[TestClass]
public class MethodRegressionTests
{
    private static VerificationConfiguration Configuration() => new() {
        Jobs = new[] { new ConfiguredCheckJob { Name = "test", Plan = new BeamCheckPlanRequest() } }
    };

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(1.0)]
    public void BeamCutToLocation_PreservesEndpointDomainAndSide(double parameter)
    {
        var location = new BeamCut { ElementId = 42, Parameter = parameter, Domain = BeamStationDomain.OffsetToOffset, Side = SectionSide.Left }.ToLocation();
        Assert.AreEqual(42, location.BeamId); Assert.AreEqual(parameter, location.Station);
        Assert.AreEqual("OffsetToOffset", location.StationDomain); Assert.AreEqual(SectionSide.Left, location.Side);
    }
    [DataTestMethod]
    [DataRow(-0.01)] [DataRow(1.01)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void BeamCutToLocation_RejectsInvalidStation(double parameter)
    { Assert.ThrowsException<ArgumentException>(() => new BeamCut { Parameter = parameter }.ToLocation()); }

    [TestMethod]
    public void ConfigurationCopy_DetachesNestedPlanAndPreservesFingerprint()
    {
        var source = Configuration(); source.Jobs[0].Plan.Settings = "original";
        var copy = ConfigurationArchive.Copy(source);
        Assert.AreEqual(ConfigurationArchive.Fingerprint(source), ConfigurationArchive.Fingerprint(copy));
        copy.Jobs[0].Plan.Settings = "changed";
        Assert.AreEqual("original", source.Jobs[0].Plan.Settings);
        Assert.AreNotEqual(ConfigurationArchive.Fingerprint(source), ConfigurationArchive.Fingerprint(copy));
    }
    [TestMethod]
    public void ConfigurationSave_InvalidSchemaLeavesDestinationUntouched()
    {
        var source = Configuration(); source.Schema = 999;
        using var stream = new MemoryStream(); stream.WriteByte(42);
        Assert.ThrowsException<SerializationException>(() => ConfigurationArchive.Save(source, stream));
        CollectionAssert.AreEqual(new byte[] { 42 }, stream.ToArray());
    }
    [TestMethod]
    public void CalculationRegister_DuplicateIdCannotReplaceExistingFactory()
    {
        var first = new CalculationFactoryTest.Alternative(); var catalog = new ConcreteCalculationCatalog(); catalog.Register(first);
        Assert.ThrowsException<ArgumentException>(() => catalog.Register(new CalculationFactoryTest.Alternative()));
        var resolved = catalog.Resolve(new ConcreteVerificationOptions { CalculationEngineId = first.Id, CalculationEngineVersion = first.Version, CalculationEngineConfiguration = first.Configuration });
        Assert.AreEqual(first.Id, resolved.Id); Assert.AreEqual(first.Version, resolved.Version);
    }
    [TestMethod]
    public void CalculationResolve_RejectsChangedIdentityAfterRegistration()
    {
        var factory = new CalculationFactoryTest.Alternative(); var catalog = new ConcreteCalculationCatalog(); catalog.Register(factory);
        var options = new ConcreteVerificationOptions { CalculationEngineId = factory.Id, CalculationEngineVersion = factory.Version, CalculationEngineConfiguration = factory.Configuration };
        factory.RuntimeVersion = "changed";
        var error = Assert.ThrowsException<InvalidOperationException>(() => catalog.Resolve(options));
        StringAssert.Contains(error.Message, "RegisteredNumericalEngineChanged"); Assert.AreEqual(0, factory.Sessions);
    }
    [TestMethod]
    public void EngineRegister_DuplicateKindLeavesOriginalDescriptor()
    {
        var catalog = new EngineCatalog();
        var descriptor = new EngineCapability("test", 1, "first", "fixture only", (_, _) => throw new NotSupportedException(), (_, _, _) => false);
        catalog.Register(descriptor);
        Assert.ThrowsException<ArgumentException>(() => catalog.Register(new EngineCapability("test", 2, "second", "fixture only", (_, _) => throw new NotSupportedException(), (_, _, _) => false)));
        Assert.AreSame(descriptor, catalog.Capabilities.Single());
    }
}
