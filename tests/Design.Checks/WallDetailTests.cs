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
    // ANTHEA 1.16.0 (registro R-MURI-*): the frozen F4 capture predates the corrections of the details. Every value is still compared;
    // the ones changed by a declared correction are compared with the old value transformed by that correction, written here:
    // - spacing (1000-2c-Ø)/(n-1) of an isolated metre -> 1000/n of the continuous wall: interferro, passo e interferro fra coppie
    //   giuntate move by the difference, the mandrel by ab = min(c+Ø/2; passo/2);
    // - foot on the bottom mesh (+ Ø transverse + Ø longitudinal): the height for the bend loses the two diameters;
    // - foot direction, tail, anchorage provided, shapes, lengths and steel per metre: new geometry, checked by WallTavolaTests;
    // - marks are positions 1, 2, …; foundation bars may be one continuous bar (same diameter, number and stock).
    internal static double OldSpacing(double cover,double dia,int count)=>(1000-2*cover-dia)/(count-1);
    [TestMethod]
    public void FrozenDetailsChangeOnlyByTheDeclaredCorrections()
    {
        int barCount=0,checkCount=0,exact=0,moved=0,geometry=0;
        foreach(var row in Fixture().OfType<JsonObject>())
        {
            string id=S(row,"id");var expected=row["details"]!;var input=Input(row);var actual=WallReinforcementDetailing.Calculate(input);
            var bars=input.Reinforcement.Zones;double cover=input.Cover;var m=input.Material;
            WallFaceBars Of(WallRebarZone z,WallRebarFace f)=>f is WallRebarFace.Upstream or WallRebarFace.Bottom?bars[z].FirstFace:bars[z].OppositeFace;
            double Delta(WallRebarZone z,WallRebarFace f){var x=Of(z,f);return 1000.0/x.Count-OldSpacing(cover,x.Diameter,x.Count);}
            double Mandrel(WallRebarZone z,WallRebarFace f,double spacing)
            {
                var x=Of(z,f);double ab=Math.Min(cover+x.Diameter/2,spacing/2);
                return Math.Max((x.Diameter<=16?4:7)*x.Diameter,Math.PI*x.Diameter*x.Diameter/4*m.Fyd/m.Fcd*(1/ab+1/(2*x.Diameter)));
            }
            var foundation=new[]{WallRebarZone.Toe,WallRebarZone.Heel}.Where(z=>z==WallRebarZone.Toe?input.Geometry.Toe>0:input.Geometry.Heel>0).ToArray();
            double mesh=foundation.Select(z=>bars[z].FirstFace.Diameter).DefaultIfEmpty(0).Max()+foundation.Select(z=>bars[z].SecondaryDiameter).DefaultIfEmpty(0).Max();
            foreach(var e in expected["Bars"]!.AsArray())
            {
                var zone=Enum.GetValues(typeof(WallRebarZone)).Cast<WallRebarZone>().Single(z=>Zone(z)==S(e!,"Zone"));
                var face=Enum.GetValues(typeof(WallRebarFace)).Cast<WallRebarFace>().Single(f=>Face(f)==S(e!,"Face"));
                var a=actual.Bars.SingleOrDefault(b=>b.Zone==zone && b.Face==face)??actual.Bars.Single(b=>b.Continuous && b.Face==face);barCount++;
                foreach(string p in new[]{"Diameter","Count","Area","RequiredAnchor","RequiredLap","Lap","Fbd"})
                    Same(D(e!,p),Convert.ToDouble(typeof(WallBarDetail).GetProperty(p)!.GetValue(a),CultureInfo.InvariantCulture),id+"/"+p);
                Same(OldSpacing(cover,a.Diameter,a.Count),D(e!,"Spacing"),id);Same(1000.0/a.Count,a.Spacing,id);
                if(zone==WallRebarZone.Stem && D(e!,"Mandrel")>0 && bars[zone].BendDiameter==0)
                {
                    Same(Math.Ceiling(Mandrel(zone,face,OldSpacing(cover,a.Diameter,a.Count))/10)*10,D(e!,"Mandrel"),id);
                    Same(Math.Ceiling(Mandrel(zone,face,a.Spacing)/10)*10,a.Mandrel,id);
                }
                else if(zone is WallRebarZone.Stem or WallRebarZone.StemUpper) Same(D(e!,"Mandrel"),a.Mandrel,id);
                if(zone is WallRebarZone.Toe or WallRebarZone.Heel) Same(D(e!,"Anchor"),a.Anchor,id);
            }
            Assert.AreEqual(expected["Checks"]!.AsArray().Count,actual.Checks.Count,id);
            for(int i=0;i<actual.Checks.Count;i++)
            {
                var e=expected["Checks"]![i]!;var a=actual.Checks[i];checkCount++;
                Assert.AreEqual(S(e,"Unit")=="barre"?"bars":S(e,"Unit"),a.Unit,id);
                if(a.Zone.HasValue) StringAssert.StartsWith(S(e,"Name"),Zone(a.Zone.Value)+" · "+Face(a.Face!.Value)+" · ");
                double demand=D(e,"Demand"),capacity=D(e,"Resistance");
                switch(a.Rule)
                {
                    case WallDetailRule.ClearSpacing: case WallDetailRule.LapPairClearSpacing: capacity+=Delta(a.Zone!.Value,a.Face!.Value);moved++;break;
                    case WallDetailRule.MainSpacing: demand+=Delta(a.Zone!.Value,a.Face!.Value);moved++;break;
                    case WallDetailRule.Mandrel:
                        if(bars[a.Zone!.Value].BendDiameter==0){var x=Of(a.Zone.Value,a.Face!.Value);demand=Mandrel(a.Zone.Value,a.Face.Value,1000.0/x.Count);capacity=Math.Ceiling(demand/10)*10;}
                        else demand=Mandrel(a.Zone.Value,a.Face!.Value,1000.0/Of(a.Zone.Value,a.Face.Value).Count);
                        moved++;break;
                    case WallDetailRule.BendHeight: capacity-=mesh;demand=a.Demand;moved++;break;
                    case WallDetailRule.TailExtent: case WallDetailRule.FootAnchorage: demand=a.Demand;capacity=a.Capacity;geometry++;break;
                    default: exact++;break;
                }
                Same(demand,a.Demand,id+"/"+a.Rule);Same(capacity,a.Capacity,id+"/"+a.Rule);
            }
        }
        Console.WriteLine($"Details: {barCount} frozen bars, {checkCount} checks: {exact} unchanged, {moved} moved by the spacing, mandrel and mesh corrections, {geometry} new foot geometry");
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
