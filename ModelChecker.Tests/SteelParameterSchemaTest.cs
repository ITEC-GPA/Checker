using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

[TestClass]
public class SteelParameterSchemaTest
{
    private static Dictionary<string,string> Compression() => new() {
        ["AnalysisMode"]="Compression",["Unperforated"]="true",["MemberFingerprint"]="prepared-snapshot",
        ["LoadPatternReference"]="constant concentric compression",["ParameterSet"]="EC3 declared coefficients",
        ["CompressionCurve1"]="B",["CompressionCurve2"]="C",["TorsionalCurve"]="B",
        ["TorsionalExclusionSource"]="independently restrained",["FlexuralTorsionalExclusionSource"]="independent assessment" };

    [TestMethod]
    public void CatalogExposesImmutableSchemasWithoutChangingArchiveVocabulary()
    {
        var catalog=EngineCatalog.BuiltIn();
        Assert.AreSame(SteelParameterSchemas.Member,catalog.Capabilities.Single(c=>c.Kind==SteelMemberPipelineChecker.EngineId).ParameterSchema);
        Assert.AreSame(SteelParameterSchemas.Section,catalog.Capabilities.Single(c=>c.Kind==SteelPipelineMaterialChecker.EngineId).ParameterSchema);
        Assert.IsNull(catalog.Capabilities.Single(c=>c.Kind=="Concrete.Section").ParameterSchema);
        var s=SteelParameterSchemas.Member;
        Assert.AreEqual("mm",s.Parameters.Single(p=>p.Key=="Packed.Pitch").Unit);
        CollectionAssert.AreEquivalent(new[]{"Nmm","KNm"},s.Parameters.Single(p=>p.Key=="Mcr.Unit").Choices.ToArray());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<string>)s.Parameters.Single(p=>p.Key=="Mcr.Unit").Choices)[0]="N");
        Assert.AreEqual(0,s.Validate(Compression()).Count); // effective lengths are in the model context, not in Parameters
        Assert.IsTrue(s.ExternalRequirements.Any(r=>r.Contains("effective lengths")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExistingCompleteBeamColumnExamplePassesConfigurationPreflight(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();var config=SteelBeamColumnFrame.Configure(model,ntc);
        foreach(var engine in config.Engines) {
            var schema=EngineCatalog.BuiltIn().Capabilities.Single(c=>c.Kind==engine.Kind).ParameterSchema;
            var issues=schema.Validate(engine.Parameters);
            Assert.AreEqual(0,issues.Count,string.Join(";",issues.Select(i=>i.Code+":"+i.Key)));
        }
        using var stream=new MemoryStream();ConfigurationArchive.Save(config,stream);stream.Position=0;
        var restored=ConfigurationArchive.Load(stream);
        Assert.AreEqual(ConfigurationArchive.Fingerprint(config),ConfigurationArchive.Fingerprint(restored));
    }

    [DataTestMethod]
    [DataRow("Ncr1.Value","NaN")] [DataRow("Ncr1.Value","Infinity")] [DataRow("Ncr1.Value","-1")]
    [DataRow("Ncr1.Unit","Nmm")] [DataRow("CompressionCurve1","0")] [DataRow("AnalysisMode","compression")]
    [DataRow("Unperforated","yes")] [DataRow("MemberClass","5")] [DataRow("LateralTorsionalCurve","A0")]
    public void InvalidTypedValuesAreReportedBeforeRunningTheModel(string key,string value)
    {
        var values=Compression();values[key]=value;
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(values).Any(i=>i.Key==key&&i.Code=="InvalidParameterValue"));
    }
    [TestMethod]
    public void CriticalValuesNeedUnitsAndSourcesAndCannotAlsoBeExcluded()
    {
        var values=Compression();values["NcrT.Value"]="200000";
        var issues=SteelParameterSchemas.Member.Validate(values);
        Assert.IsTrue(issues.Any(i=>i.Code=="NcrTTriplet"));
        Assert.IsTrue(issues.Any(i=>i.Code=="ConflictingCompressionModeT"));
        values["NcrT.Unit"]="N";values["NcrT.Source"]="elastic eigenvalue";values.Remove("TorsionalExclusionSource");
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(values).Count);
    }
    [TestMethod]
    public void PartialPackingAndParametersFromAnotherRouteAreVisible()
    {
        var values=Compression();values["Packed.Pitch"]="200";values["Cm1"]="0.8";
        var issues=SteelParameterSchemas.Member.Validate(values);
        Assert.IsTrue(issues.Any(i=>i.Code=="PackedAssessmentComplete"));
        Assert.IsTrue(issues.Any(i=>i.Key=="Cm1"&&i.Code=="ParameterOnAnotherMode"));
    }
    [TestMethod]
    public void SectionAssessmentRequiresBindingAndPerforatedDeclaration()
    {
        var values=new Dictionary<string,string>{{"NetArea","1000"}};
        var issues=SteelParameterSchemas.Section.Validate(values);
        Assert.IsTrue(issues.Any(i=>i.Code=="AssessmentFingerprintRequired"));
        Assert.IsTrue(issues.Any(i=>i.Code=="NetAreaDeclarationRequired"));
        values["SectionFingerprint"]="property";values["Unperforated"]="true";
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(values).Any(i=>i.Code=="NetAreaRequiresPerforated"));
        values["Unperforated"]="false";
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(values).Count);
        _=new SteelPipelineMaterialChecker(new StandardEN1993p11(),"2005",parameters:values);
    }
    [TestMethod]
    public void RuntimeVocabularyAndMetadataStayAlignedAndInputIsNotMutated()
    {
        var values=Compression();var original=new Dictionary<string,string>(values);
        _=new SteelMemberPipelineChecker(new StandardEN1993p11(),"2005",parameters:values);
        _=SteelParameterSchemas.Member.Validate(values);
        CollectionAssert.AreEquivalent(original.ToArray(),values.ToArray());
        values["UndeclaredParameter"]="123";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(values).Any(i=>i.Code=="UnknownParameter"));
        Assert.ThrowsException<ArgumentException>(()=>new SteelMemberPipelineChecker(new StandardEN1993p11(),"2005",parameters:values));
    }
}
