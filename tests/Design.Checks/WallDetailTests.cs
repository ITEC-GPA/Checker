using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Durability;
using GPC.Design.Walls;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public class WallDetailTests
{
    internal static JsonArray Fixture()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/details-f4.json"));
        Assert.AreEqual("E31A18D8D65CB9BA45566B9EA20AA6C643122C20BB5CE2A591A387917007A899",RuntimeCompatibility.Sha256Hex(bytes));
        return JsonNode.Parse(bytes)!.AsArray();
    }
    internal static double D(JsonNode n,string key) => double.Parse(n[key]!.ToString(),CultureInfo.InvariantCulture);
    internal static bool B(JsonNode n,string key) => n[key]!.GetValue<bool>();
    internal static string S(JsonNode n,string key) => n[key]!.GetValue<string>();
    internal static string Zone(WallRebarZone z) => z switch {WallRebarZone.Stem=>"stem",WallRebarZone.StemUpper=>"stem_upper",WallRebarZone.Toe=>"toe",WallRebarZone.Heel=>"heel",_=>throw new ArgumentException()};
    internal static string Face(WallRebarFace f) => f switch {WallRebarFace.Upstream=>"monte",WallRebarFace.Downstream=>"valle",WallRebarFace.Bottom=>"inferiore",WallRebarFace.Top=>"superiore",_=>throw new ArgumentException()};
    internal static WallDetailInput Input(JsonNode row)
    {
        var d=row["input"]!;var g=d["geometry"]!;var m=d["materials"]!;var r=d["reinforcement"]!;var o=d["detailing"]!;var mat=row["material"]!;
        var zones=new Dictionary<WallRebarZone,WallZoneBars>();
        foreach(var zone in ((WallRebarZone[])Enum.GetValues(typeof(WallRebarZone))))
        {
            var a=r[Zone(zone)]!;bool symmetric=B(a,"symmetric");
            zones[zone]=new(new(D(a,"diameter"),(int)D(a,"count")),new(D(a,symmetric?"diameter":"opposite_diameter"),(int)D(a,symmetric?"count":"opposite_count")),
                D(a,"secondary_diameter"),D(a,"secondary_spacing"),D(a,"anchor_length"),D(a,"lap_length"),D(a,"bend_diameter"));
        }
        string abrasion=S(o,"cover_abrasion"),ground=S(o,"cover_ground");
        var cover=new CoverInput(new[]{S(m,"exposure")},D(m,"fck"),(int)D(o,"life"),B(o,"cover_high_strength"),B(o,"cover_slab"),B(o,"cover_ec_quality"),16,
            D(o,"aggregate"),D(o,"cover_deviation"),B(o,"cover_rough"),abrasion.StartsWith("XM3")?15:abrasion.StartsWith("XM2")?10:abrasion.StartsWith("XM1")?5:0,
            ground=="Direttamente su terra"?75:ground=="Terreno preparato"?40:0,S(o,"cover_element")=="Piastra / soletta / parete",B(o,"cover_quality"),ExposureClasses.Uni11104MinimumStrength(new[]{S(m,"exposure")}));
        return new(new(D(g,"height"),D(g,"slab"),D(g,"toe"),D(g,"stem_base"),D(g,"stem_top"),D(g,"heel")),new(B(r,"two_zones"),D(r,"lower_height"),zones),
            new(D(mat,"fcd"),D(mat,"fyd"),D(mat,"fctk05"),D(mat,"gammaC")),D(m,"cover"),
            new(D(o,"aggregate"),D(o,"lap_clear"),D(o,"lap_percent"),B(o,"good_bond"),D(o,"tie_diameter"),D(o,"tie_spacing"),
                S(o,"cover_method")=="EC2 2004"?DurabilityProfile.EN1992p11:DurabilityProfile.Ntc2018,cover));
    }
    internal static void Same(double expected,double actual,string label="") => Assert.AreEqual(expected,actual,1e-9+1e-9*Math.Max(Math.Abs(expected),Math.Abs(actual)),label);
    internal static void Same(double? expected,double? actual,string label="")
    {Assert.AreEqual(expected.HasValue,actual.HasValue,label);if(expected.HasValue) Same(expected.Value,actual!.Value,label);}
    [TestMethod]
    public void FrozenDetailsAllBarsChecksShapesAndWeightsRemainEquivalent()
    {
        int barCount=0,checkCount=0,pointCount=0;
        foreach(var row in Fixture().OfType<JsonObject>())
        {
            string id=S(row,"id");var expected=row["details"]!;var actual=WallReinforcementDetailing.Calculate(Input(row));
            Same(D(expected,"SteelKg"),actual.SteelKg,id);
            Assert.AreEqual(expected["Bars"]!.AsArray().Count,actual.Bars.Count,id);
            for(int i=0;i<actual.Bars.Count;i++)
            {
                var e=expected["Bars"]![i]!;var a=actual.Bars[i];barCount++;
                Assert.AreEqual(S(e,"Mark"),a.Mark,id); Assert.AreEqual(S(e,"Zone"),Zone(a.Zone),id);Assert.AreEqual(S(e,"Face"),Face(a.Face),id);
                foreach(string p in new[]{"Diameter","Count","Spacing","Area","Length","RequiredAnchor","Anchor","RequiredLap","Lap","Mandrel","Fbd"})
                    Same(D(e,p),Convert.ToDouble(typeof(WallBarDetail).GetProperty(p)!.GetValue(a),CultureInfo.InvariantCulture),id+"/"+p);
                Assert.AreEqual(e["Path"]!.AsArray().Count,a.Path.Count,id);
                for(int j=0;j<a.Path.Count;j++){Same(D(e["Path"]![j]!,"X"),a.Path[j].X,id);Same(D(e["Path"]![j]!,"Y"),a.Path[j].Y,id);pointCount++;}
            }
            Assert.AreEqual(expected["Checks"]!.AsArray().Count,actual.Checks.Count,id);
            for(int i=0;i<actual.Checks.Count;i++)
            {
                var e=expected["Checks"]![i]!;var a=actual.Checks[i];checkCount++;
                Same(D(e,"Demand"),a.Demand,id);Same(D(e,"Resistance"),a.Capacity,id);Same(e["Ratio"]?.GetValue<double>(),a.Ratio,id);
                Assert.AreEqual(S(e,"Unit")=="barre"?"bars":S(e,"Unit"),a.Unit,id);
                var outcome=S(e,"Status").StartsWith("Soddisfatta")?EngineeringOutcome.Satisfied:S(e,"Status").StartsWith("Non soddisfatta")?EngineeringOutcome.NotSatisfied:EngineeringOutcome.NotEvaluated;
                Assert.AreEqual(outcome,a.Outcome,id);
                if(a.Zone.HasValue) StringAssert.StartsWith(S(e,"Name"),Zone(a.Zone.Value)+" · "+Face(a.Face!.Value)+" · ");
            }
        }
        Console.WriteLine($"Details: {barCount} bars, {pointCount} path points, {checkCount} checks and 56 weights equivalent");
    }
    [TestMethod]
    public void DetailInputSnapshotsLayoutAndCancellationReturnsNoPartialResult()
    {
        var p=Input(Fixture()[0]!);var source=p.Reinforcement.Zones.ToDictionary(x=>x.Key,x=>x.Value);
        var layout=new WallRebarLayout(false,1,source);source.Clear();Assert.AreEqual(4,layout.Zones.Count);
        using var c=new CancellationTokenSource();c.Cancel();Assert.ThrowsException<OperationCanceledException>(()=>WallReinforcementDetailing.Calculate(p,c.Token));
    }
    [TestMethod]
    public void DetailInvalidInputsAreRefusedAndMissingSpaceNeverPasses()
    {
        var p=Input(Fixture()[0]!);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>WallReinforcementDetailing.Calculate(p with {Geometry=p.Geometry with {Height=double.NaN}}));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>WallReinforcementDetailing.Calculate(p with {Options=p.Options with {LapPercent=0}}));
        Assert.ThrowsException<ArgumentException>(()=>WallReinforcementDetailing.Calculate(p with {Reinforcement=new(true,0,p.Reinforcement.Zones)}));
        Assert.ThrowsException<ArgumentException>(()=>WallReinforcementDetailing.Calculate(p with {Material=p.Material with {Fcd=double.PositiveInfinity}}));
        Assert.ThrowsException<ArgumentException>(()=>WallReinforcementDetailing.Calculate(p with {Reinforcement=new(false,1,new Dictionary<WallRebarZone,WallZoneBars>())}));
        var a=new WallDetailCheck(WallDetailRule.ClearSpacing,WallRebarZone.Stem,WallRebarFace.Upstream,20,-10,"mm");
        Assert.IsNull(a.Ratio);Assert.AreEqual(EngineeringOutcome.NotEvaluated,a.Outcome);
    }
    [TestMethod]
    public void ActualBarCoverReusesCompleteDurabilitySpecification()
    {
        var a=new CoverInput(new[]{"XC4","XD3"},35,100,true,true,true,12,32,10,true,5,75,true,true,30);
        var b=a.WithBarDiameter(40);Assert.AreEqual(12,a.BarDiameter);Assert.AreEqual(40,b.BarDiameter);
        foreach(var profile in new[]{DurabilityProfile.Ntc2018,DurabilityProfile.EN1992p11})
        {
            var expected=CoverRequirements.Calculate(profile,new CoverInput(new[]{"XC4","XD3"},35,100,true,true,true,40,32,10,true,5,75,true,true,30));
            Same(expected.Nominal,CoverRequirements.Calculate(profile,b).Nominal);
        }
    }
}
