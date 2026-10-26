using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateIndependentJointTests
{
    public TestContext TestContext { get; set; } = null!;
    public static IEnumerable<object[]> Cases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","end-plate-reference.json")));
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            foreach(bool ntc in new[]{false,true})yield return new object[]{c.GetProperty("name").GetString()!,ntc};
    }

    [DataTestMethod][DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void WholePhysicalJointMatchesIndependentForwardQuadrature(string caseName,bool ntc)
    {
        var g=EndPlateBothFlangesCaseData.Geometry(ntc,flush:true,shear:20000);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","end-plate-reference.json")));
        var root=doc.RootElement;
        var c=root.GetProperty("cases").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==caseName);
        for(int i=0;i<4;i++) Near(root.GetProperty("rowStiffness")[i].GetDouble(),g.Rows[i].Stiffness,"k"+i);
        Near(root.GetProperty("contactStiffness").GetDouble(),g.Contacts[0].Stiffness,"kc");
        var r=EndPlateNmv.Verify(EndPlateBothFlangesCaseData.Input(g,D("n"),D("moment")));
        Assert.IsNotNull(r.Equilibrium,EndPlateNmvTests.Details(r));
        Assert.IsTrue(r.Equilibrium.IsConverged);
        Near(D("u"),r.Equilibrium.AxialOpening,"opening",1e-9);
        Near(D("theta"),r.Equilibrium.Rotation,"rotation",1e-11);
        Near(D("energy"),r.Equilibrium.ElasticEnergy,"energy");
        for(int i=0;i<4;i++)Near(c.GetProperty("rowForces")[i].GetDouble(),r.Equilibrium.Rows[i].Tension,"row force"+i);
        for(int i=0;i<2;i++)
        {
            var expected=c.GetProperty("contacts")[i];var actual=r.Equilibrium.Contacts[i];
            Near(expected.GetProperty("compression").GetDouble(),actual.Compression,"compression");
            Near(expected.GetProperty("moment").GetDouble(),actual.SignedMoment,"contact moment");
            Near(expected.GetProperty("closed").GetDouble(),actual.ClosedLength,"closed length");
            Near(expected.GetProperty("peak").GetDouble(),actual.MaximumCompressionPerLength,"peak");
        }
        var k=r.Equilibrium.Tangent;
        Near(c.GetProperty("tangent")[0].GetDouble(),k.Axial,"KNN");
        Near(c.GetProperty("tangent")[1].GetDouble(),k.Coupling,"KNM",1e-4);
        Near(c.GetProperty("tangent")[2].GetDouble(),k.Rotation,"KMM");
        Near(D("panel"),r.Trace.Single(x=>x.Name=="Column.PanelShearEnvelope").Value,"panel");
        Near(D("columnNetArea"),r.Trace.Single(x=>x.Name=="Column.NetArea").Value,"Anet");
        Near(D("columnCentroid"),r.Trace.Single(x=>x.Name=="Column.NetCentroidShift").Value,"centroid");
        Near(D("columnInertia"),r.Trace.Single(x=>x.Name=="Column.NetInertia").Value,"Inet");
        double pv=D("panel"),netA=D("columnNetArea"),netZ=D("columnCentroid"),netW=D("columnModulus");
        double translated=D("moment")-20000*133.35;
        for(int i=0;i<2;i++)
        {
            double columnN=i==0?-10000:-30000;
            double bound=Math.Abs(columnN)/netA+(Math.Abs(translated/2)+pv*533.1+Math.Abs(columnN*netZ))/netW;
            double elastic=bound+Math.Sqrt(3)*1.5*pv/(12.8*200.3);
            Near(elastic,r.Checks.Single(x=>x.Id==$"Column.Cut{i}.ElasticNet").Demand,"net column stress");
        }
        // All independent demands pass through the complete physical bolt/weld/
        // in-plane/group/contact/column assessment, not just the spring kernel.
        Assert.IsTrue(r.Checks.Any(x=>x.Id.StartsWith("Bolts.")));
        Assert.IsTrue(r.Checks.Any(x=>x.Id.StartsWith("Weld.")));
        Assert.IsTrue(r.Checks.Any(x=>x.Id.Contains("Block")));
        string name=c.GetProperty("name").GetString()!;
        TestContext.WriteLine($"{name}, NTC={ntc}: {r.Status}; max={r.Checks.Max(x=>x.Utilization):G10}; components={r.ComponentUtilization:G10}");
        if(name=="beyond_limit")
        {
            Assert.AreEqual(ConnectionStatus.Failed,r.Status);
            Assert.IsTrue(r.Checks.Any(x=>x.Id.StartsWith("Column.Cut")&&!x.Passed));
            Assert.IsFalse(EndPlateNmvStiffness.Evaluate(r).HasQualifiedLocalTangent);
        }
        else if(name=="near_limit")
        {
            // Below every variable component bound, yet outside the additional
            // conservative pure-N/pure-M interaction. The detail must still fail.
            Assert.IsTrue(r.ComponentUtilization<1);
            Assert.AreEqual(ConnectionStatus.Failed,r.Status);
            Assert.IsFalse(r.Checks.Single(x=>x.Id=="Joint.ConservativeNMInteraction").Passed);
        }
        else
        {
            Assert.AreEqual(ConnectionStatus.Passed,r.Status,EndPlateNmvTests.Details(r));
            if(name=="high_utilization")Assert.IsTrue(r.Checks.Max(x=>x.Utilization)>.7);
        }
        double D(string key)=>c.GetProperty(key).GetDouble();
    }
    private static void Near(double expected,double actual,string name,double absolute=1e-5)
        => Assert.AreEqual(expected,actual,Math.Max(absolute,Math.Abs(expected)*2e-8),name);
}
