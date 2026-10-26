using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class StandardConnectionReportTests
{
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void EndPlateExampleRetainsSignedCaseUnitsAndGlobalMismatch(bool ntc,bool flush)
    {
        var p=EndPlateStandardCaseData.Parent(ntc,flush,shear:-300000);
        var connection=EndPlateInitialCaseTestData.Connection(p.MomentComponents);
        var initial=EndPlateInitialStiffnessFromMoment.Calculate(EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(p,
            Enumerable.Range(0,p.MomentComponents.EffectiveRows.Count).Select(i=>new EndPlateBoltElongationDetail(i,70.5,"Actual 25+20.5+4+4+17mm grip"))));
        var local=EndPlateLocalCaseAssessment.Assess(p,connection,initial);var plane=EndPlateInPlaneChecks.Verify(connection.Bolts);
        var column=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,axial:-400000));
        var raw=EndPlateStandardJoint.Verify(local,plane,column);
        var result=EndPlateStandardJoint.Verify(local,plane,column,raw.SecantStiffness*1.2);
        var report=StandardConnectionCaseReport.Create(result);
        Assert.AreEqual(ConnectionStatus.Passed,report.Status,string.Join(";",report.Diagnostics));Assert.IsTrue(report.IsComplete);
        Assert.IsFalse(report.CanQualifyWholeMember);Assert.AreEqual(-300000,report.Inputs.Single(x=>x.Name=="V2").Value);
        Assert.AreEqual("Nmm/rad",report.Results.Single(x=>x.Name=="Joint.SecantStiffness").Unit);
        Assert.AreEqual(0,report.Results.Single(x=>x.Name=="Global.SpringMatches").Value);
        Assert.IsTrue(report.ExternalConditions.Any(x=>x.Contains("Reanalyse")));
        Assert.AreEqual(ntc?1.05:1,report.Inputs.Single(x=>x.Name=="GammaM0").Value);
        Assert.IsFalse(report.Results.Any(x=>x.Name.Contains("RotationCapacity")));
        Export("endplate-"+(ntc?"ntc":"ec")+"-"+(flush?"flush":"extended"),report);
    }
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void PackedExamplesDoNotClaimMemberStability(bool ntc,bool welded)
    {
        var m=PackedWeldedCaseData.Model(ntc:ntc,holes:!welded);var terminal=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,ntc));
        var p=welded?SteelPackedPairAssessment.FromWeldedPacking(SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m,ntc)),terminal,"Forks")
            :SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(m,ntc:ntc),terminal,"Forks");
        var report=StandardConnectionCaseReport.Create(p);
        Assert.AreEqual(ConnectionStatus.Passed,report.Status);Assert.IsTrue(report.IsComplete);Assert.IsFalse(report.CanQualifyWholeMember);
        Assert.AreEqual(report.Checks.Count,report.Checks.Select(x=>x.Id).Distinct().Count());
        Assert.IsTrue(report.Results.Single(x=>x.Name=="Terminal.ForcePerChord").Value>500);
        Assert.IsTrue(report.ExternalConditions.Any(x=>x.Contains("T/FT")));
        Export("packed-"+(ntc?"ntc":"ec")+"-"+(welded?"welded":"bolted"),report);
    }
    [TestMethod]
    public void FailingTerminalRemainsAFailingConnectionReport()
    {
        var m=PackedWeldedCaseData.Model();var p=SteelPackedPairAssessment.FromWeldedPacking(SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m)),
            SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,free:5000)),"Forks");
        var report=StandardConnectionCaseReport.Create(p);Assert.AreEqual(ConnectionStatus.Failed,report.Status);
        Assert.IsTrue(report.Checks.Any(x=>!x.Passed));
    }
    [TestMethod]
    public void UnqualifiedScalarTerminalCannotBePromotedByReporting()
    {
        var m=PackedWeldedCaseData.Model(holes:true);var p=SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(m),1000,10000,"External","Forks");
        Assert.ThrowsException<ArgumentException>(()=>StandardConnectionCaseReport.Create(p));
    }
    [DataTestMethod][DataRow("StandardMomentEndPlate")][DataRow("StandardBoltedPacking")][DataRow("StandardWeldedPacking")][DataRow("StandardWeldedPackedTerminal")]
    public void CatalogSeparatesTheNewStandardRoutesFromLegacyComponents(string id)
    {
        var c=ConnectionCapabilityCatalog.Get(id);Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification,c.Level);
        Assert.IsTrue(c.ExternalRequirements.Count>0&&c.MissingMechanisms.Count>0&&c.EntryPoints.Count>0);
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,ConnectionCapabilityCatalog.Get("MomentEndPlate").Level);
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,ConnectionCapabilityCatalog.Get("PackedPairConnections").Level);
    }
    private static void Export(string name,StandardConnectionCaseReport report)
    {
        var options=new JsonSerializerOptions{WriteIndented=true};options.Converters.Add(new JsonStringEnumConverter());
        var json=JsonSerializer.Serialize(report,options);using var doc=JsonDocument.Parse(json);
        Assert.AreEqual(report.MethodId,doc.RootElement.GetProperty("MethodId").GetString());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<SectionResistanceValue>)report.Results).Clear());
        string? folder=Environment.GetEnvironmentVariable("GPC_STANDARD_CONNECTION_REPORT_DIR");
        if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,name+".json"),json);}
    }
}
