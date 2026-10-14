using System.Security.Cryptography;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Masonry;
using GPC.Design.Walls;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;
[TestClass]
public sealed class GravityWallTests
{
    static double D(JsonNode? o, string key) => o?[key]?.GetValue<double>() ?? 0;
    static string S(JsonNode? o, string key) => o?[key]?.GetValue<string>() ?? "";
    static WallMember Member(string text) => text switch { "Fusto" => WallMember.Stem, "Valle" => WallMember.Toe, "Monte" => WallMember.Heel, _ => throw new ArgumentException(text) };

    [TestMethod]
    public void SixFrozenGravityWallsRemainEquivalent()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gravity-f4.json"));
        Assert.AreEqual("2DA105542D1914E469192404D436DCCE10816AEC2A54F241D413076A63E11633", Convert.ToHexString(SHA256.HashData(bytes)));
        var fixture = JsonNode.Parse(bytes)!;
        var rows = fixture["rows"]!.AsArray(); Assert.AreEqual(6, rows.Count);
        int checks = 0, curves = 0;
        foreach (var row in rows)
        {
            var input = row!["input"]!; var p = input["gravity_design"]!; var m = row["material"]!; var g = input["geometry"]!;
            double creep = D(input["materials"], "creep");
            GravityWallMaterial material = S(p, "type") switch {
                "Muratura" => GravityWallMaterial.From(new MasonryWallMaterial(D(p,"fk"), D(p,"gamma_m"), D(p,"confidence"), D(p,"elastic_modulus"), creep, D(p,"fvk0"), D(p,"fvk_limit"))),
                "Calcestruzzo non armato" => GravityWallMaterial.From(new PlainConcreteWallMaterial(D(m,"compression"), D(m,"fctk05"), D(m,"gammaC"), D(m,"ecm"), creep)),
                _ => GravityWallMaterial.Assigned(D(m,"compression"),D(m,"tension"),D(m,"modulus"),D(m,"shear0")) };
            Same(D(m,"compression"),material.Compression); Same(D(m,"tension"),material.Tension); Same(D(m,"modulus"),material.EffectiveModulus);
            var cases = row["cases"]!.AsArray().Select(c => new GravityCombination(S(c,"id"), S(c,"state").StartsWith("SLE",StringComparison.Ordinal), c!["contactValid"]!.GetValue<bool>(),
                c["sections"]!.AsArray().Select(f => new GravitySection(Member(S(f,"Name")),D(f,"Position")*1000,D(f,"Thickness")*1000,D(f,"N"),D(f,"V"),D(f,"M")*1000)).ToArray())).ToArray();
            double h = D(g,"height")*1000;
            var result = GravityWallAnalysis.Calculate(material,h,D(g,"stem_top")*1000,2*h,D(p,"eccentricity"),cases);
            var expected = row["checks"]!.AsArray().ToArray(); var actual = result.SelectMany(r => r.Checks).ToArray();
            Assert.AreEqual(expected.Length,actual.Length,S(row,"id"));
            for (int j=0;j<actual.Length;j++)
            {
                var e=expected[j]!; var a=actual[j]; double scale=a.Unit=="Nmm/mm" ? 1000 : 1;
                Assert.AreEqual(S(e,"Combination"),a.Combination);
                Same(D(e,"Demand"),a.Demand/scale);
                Assert.AreEqual(e["Resistance"] is not null,a.Capacity.HasValue);
                if(a.Capacity.HasValue) Same(D(e,"Resistance"),a.Capacity.Value/scale);
                Assert.AreEqual(e["Ratio"] is not null,a.Ratio.HasValue);
                if(a.Ratio.HasValue) Same(D(e,"Ratio"),a.Ratio.Value);
                if(e["Ratio"] is not null) Assert.AreEqual(D(e,"Ratio")<=1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied,a.Outcome);
                checks++;
            }
            for(int j=0;j<result.Count;j++)
            {
                var expectedCurves=row["cases"]![j]!["curvatures"]!.AsArray(); var actualCurves=result[j].Curvatures;
                Assert.AreEqual(expectedCurves.Count,actualCurves.Count);
                for(int k=0;k<actualCurves.Count;k++) { Same(D(expectedCurves[k],"Y"),actualCurves[k].Height/1000); Same(D(expectedCurves[k],"Curvature"),actualCurves[k].Curvature*1000); curves++; }
            }
        }
        Assert.IsTrue(checks>1000 && curves>0); Console.WriteLine($"Gravity: {checks} checks and {curves} curvatures equivalent");
    }
    [TestMethod]
    public void AssignedTensileAxialActionKeepsHistoricalAbsoluteDemand()
    {
        var mat=GravityWallMaterial.Assigned(2,.1,3000,.05);
        var c=new GravityCombination("T",false,true,new[]{new GravitySection(WallMember.Toe,1000,500,-100,0,0)});
        var checks=GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c})[0].Checks;
        Same(.2,checks[0].Demand); Same(.1,checks[0].Ratio!.Value);
        Same(.2,checks[1].Demand); Same(2,checks[1].Ratio!.Value);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied,checks[1].Outcome);
    }
    [TestMethod]
    public void AssignedStrengthsNeedNoInventedElasticModulus()
    {
        var noStiffness=GravityWallMaterial.Assigned(2,.1,.05);
        Assert.IsFalse(noStiffness.HasStiffness);Assert.AreEqual(0,noStiffness.EffectiveModulus);
        var c=new GravityCombination("A",false,true,new[]{new GravitySection(WallMember.Stem,1000,500,10,2,100)});
        var a=GravityWallAnalysis.Calculate(noStiffness,3000,500,6000,0,new[]{c,c with {Id="SLS",Serviceability=true}});
        var b=GravityWallAnalysis.Calculate(GravityWallMaterial.Assigned(2,.1,3000,.05),3000,500,6000,0,new[]{c});
        CollectionAssert.AreEqual(b[0].Checks.ToArray(),a[0].Checks.ToArray());
        Assert.AreEqual(0,a[1].Checks.Count);Assert.AreEqual(0,a[1].Curvatures.Count);
        Assert.IsFalse(a[0].Checks.Any(x=>x.Method==GravityCheckMethod.ElasticStability));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>GravityWallMaterial.Assigned(2,.1,0,.05));
    }
    [TestMethod]
    public void NegativeNoTensionCapacityIsUnavailableAndNeverPasses()
    {
        var c=new GravityCombination("T",false,true,new[]{new GravitySection(WallMember.Stem,1000,500,-100,0,0)});
        var check=GravityWallAnalysis.Calculate(GravityWallMaterial.Assigned(2,.1,.05),3000,500,6000,0,new[]{c})[0].Checks.Single(x=>x.Method==GravityCheckMethod.NoTension);
        Assert.IsTrue(check.Capacity<0);Assert.IsNull(check.Ratio);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,check.Outcome);
    }
    static void Same(double expected,double actual) => Assert.AreEqual(expected,actual,1e-9+1e-9*Math.Max(Math.Abs(expected),Math.Abs(actual)));

    [TestMethod]
    public void IndependentAssignedStressAndMasonryVectors()
    {
        var material=GravityWallMaterial.Assigned(2,.1,3000,.05);
        var actions=new[]{new GravityCombination("ULS",false,true,new[]{new GravitySection(WallMember.Stem,1000,1000,.5,.1,100)})};
        var r=GravityWallAnalysis.Calculate(material,3000,1000,6000,0,actions)[0];
        Same(.0011,r.Checks[0].Demand); Same(500.0/6,r.Checks[1].Capacity!.Value); Same(.00015,r.Checks[2].Demand);
        var masonry=new MasonryWallMaterial(5,2.5,1,3000,2,.2,1);
        Same(1.7,masonry.Compression); Same(1000,masonry.EffectiveModulus); Same(.24,masonry.ShearStress(1)); Same(.4,masonry.ShearStress(100));
    }
    [TestMethod]
    public void CrackedOrUnstableSectionsNeverReceiveAResistance()
    {
        var mat=GravityWallMaterial.From(new MasonryWallMaterial(5,2.5,1,3000,2,.2,1));
        foreach(var n in new[]{0d,10000d})
        {
            var c=new GravityCombination("ULS",false,true,new[]{new GravitySection(WallMember.Stem,1000,500,n,1,10000)});
            var r=GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c})[0];
            foreach(var check in r.Checks.Where(x=>x.Method is GravityCheckMethod.CompressedBlock or GravityCheckMethod.MasonryJointShear))
            { Assert.IsNull(check.Capacity); Assert.AreEqual(EngineeringOutcome.NotEvaluated,check.Outcome); }
        }
    }
    [TestMethod]
    public void InvalidInputsAndCancellationCannotReturnCompletedPartialResults()
    {
        var mat=GravityWallMaterial.Assigned(2,.1,3000,.05);
        var c=new GravityCombination("A",false,false,new[]{new GravitySection(WallMember.Toe,1,500,1,2,3)});
        var result=GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c})[0];
        Assert.AreEqual(GravityCheckMethod.Contact,result.Checks.Single().Method); Assert.IsNull(result.Checks.Single().Capacity);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>GravityWallAnalysis.Calculate(mat,3000,500,5999,0,new[]{c}));
        Assert.ThrowsException<ArgumentException>(()=>GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c,c}));
        Assert.ThrowsException<ArgumentException>(()=>GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c with {Sections=new[]{c.Sections[0] with { N=double.NaN }}}}));
        Assert.ThrowsException<OperationCanceledException>(()=>GravityWallAnalysis.Calculate(mat,3000,500,6000,0,new[]{c},new CancellationToken(true)));
    }
}
