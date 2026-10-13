using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Design.Bridges;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass,DoNotParallelize]
public sealed class BridgeConceptTests
{
    static JsonArray Cases()
    {
        byte[] bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/bridge-f4.json.gz"));
        Assert.AreEqual("54461E42ABC4DB18D1A9BCB5CCB237D99B174CD24AAB9CF549DF7F71044AF742",Convert.ToHexString(SHA256.HashData(bytes)));
        using var zip=new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress);
        return JsonNode.Parse(zip)!["cases"]!.AsArray();
    }
    static string Property(string key)=>string.Concat(key.Split('_').Select(v=>char.ToUpperInvariant(v[0])+v[1..]));
    internal static BridgeModel Input(JsonNode source)
    {
        T Read<T>(string group)
        {
            var values=new JsonObject();foreach(var pair in source[group]!.AsObject())values[Property(pair.Key)]=pair.Value?.DeepClone();
            return values.Deserialize<T>()!;
        }
        return new(Read<BridgeInput>("input"),Read<BridgeRates>("rates"),Read<BridgeAssumptions>("assumptions"));
    }
    static JsonNode LegacyData(JsonNode native,JsonNode template)
    {
        var result=template.DeepClone();
        foreach(string group in new[]{"input","rates","assumptions"})
            foreach(string key in result[group]!.AsObject().Select(p=>p.Key).ToArray())
                result[group]![key]=native[Property(group)]![Property(key)]!.DeepClone();
        return result;
    }
    static void RestoreArchiveShape(JsonNode? value,JsonNode template)
    {
        if(value is JsonArray array){foreach(var child in array)RestoreArchiveShape(child,template);return;}
        if(value is not JsonObject obj)return;
        foreach(string key in obj.Select(p=>p.Key).ToArray())
            if(key=="Data")obj[key]=LegacyData(obj[key]!,template);else RestoreArchiveShape(obj[key],template);
    }
    internal static int Compare(JsonNode? expected,JsonNode? actual,string path)
    {
        if(expected is null || actual is null){Assert.AreEqual(expected is null,actual is null,path);return 0;}
        if(expected is JsonObject eo)
        {
            Assert.IsInstanceOfType<JsonObject>(actual,path);var ao=actual.AsObject();
            CollectionAssert.AreEquivalent(eo.Select(p=>p.Key).ToArray(),ao.Select(p=>p.Key).ToArray(),path);
            return eo.Sum(p=>Compare(p.Value,ao[p.Key],path+"/"+p.Key));
        }
        if(expected is JsonArray ea)
        {Assert.IsInstanceOfType<JsonArray>(actual,path);var aa=actual.AsArray();Assert.AreEqual(ea.Count,aa.Count,path);return ea.Select((v,i)=>Compare(v,aa[i],path+"/"+i)).Sum();}
        if(expected.AsValue().TryGetValue<double>(out double e)&&actual.AsValue().TryGetValue<double>(out double a))
        {Assert.IsTrue(double.IsFinite(a),path);Assert.AreEqual(e,a,1e-10*Math.Max(1,Math.Abs(e)),path);return 1;}
        Assert.AreEqual(expected.ToJsonString(),actual.ToJsonString(),path);return 0;
    }
    [TestMethod]
    public void ThirtyOneFrozenModelsRetainQuantitiesReactionsAndTechnicalGeometry()
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        var cases=Cases();Assert.AreEqual(31,cases.Count);int numbers=0;
        foreach(var row in cases)
        {
            var data=Input(row!["input"]!);var copy=data with {};var r=BridgeConcept.Calculate(data);
            var actual=new JsonObject{["result"]=JsonSerializer.SerializeToNode(r),["technical"]=JsonSerializer.SerializeToNode(BridgeConcept.TechnicalSchedule(data,r)),
                ["supports"]=JsonSerializer.SerializeToNode(BridgeConcept.SupportSchedule(data,r))};
            int removed=NormalizeLegacyStations(row["calculation"]);
            Assert.AreEqual(0,NormalizeLegacyStations(actual),"Native diagram still contains duplicates.");
            Console.WriteLine($"{row["id"]}: {removed} redundant legacy stations");
            numbers+=Compare(row["calculation"],actual,row["id"]!.GetValue<string>());Assert.AreEqual(copy,data);
        }
        Console.WriteLine($"Frozen bridge calculations: {cases.Count} cases, {numbers} numbers.");
    }
    [TestMethod]
    public void SeventeenFrozenSearchesRetainProposalsTrialOrderParetoAndRejections()
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");int count=0,numbers=0;
        foreach(var row in Cases().Where(r=>r!["options"] is not null))
        {
            var data=Input(row!["input"]!);var options=row["options"]!.Deserialize<BridgeConcept.OptimizationOptions>()!;
            var actual=JsonSerializer.SerializeToNode(BridgeConcept.Optimize(data,options));RestoreArchiveShape(actual,row["input"]!);
            int removed=NormalizeLegacyStations(row["optimization"]);
            Assert.AreEqual(0,NormalizeLegacyStations(actual),"Native search diagram still contains duplicates.");
            Console.WriteLine($"{row["id"]}: {removed} redundant legacy stations");
            numbers+=Compare(row["optimization"],actual,row["id"]!.GetValue<string>());count++;
        }
        Assert.AreEqual(17,count);Console.WriteLine($"Frozen bridge searches: {count} grids, {numbers} numbers.");
    }
    // Classified representation fix only. Each removed point must have the same span and all physical values
    // within the original numerical comparison tolerance. The SHA-protected source bytes are never changed.
    static int NormalizeLegacyStations(JsonNode? node)
    {
        if(node is JsonArray list)return list.Sum(NormalizeLegacyStations);
        if(node is not JsonObject obj)return 0;
        int removed=0;
        foreach(var pair in obj.ToArray())
        {
            if(pair.Key!="Stations"||pair.Value is not JsonArray stations){removed+=NormalizeLegacyStations(pair.Value);continue;}
            for(int i=stations.Count-1;i>0;i--)
            {
                var a=stations[i-1]!;var b=stations[i]!;
                double x=a["X"]!.GetValue<double>(),y=b["X"]!.GetValue<double>();
                if(a["Span"]!.GetValue<int>()!=b["Span"]!.GetValue<int>()||Math.Abs(x-y)>1e-12*Math.Max(1,Math.Abs(x)))continue;
                foreach(string key in new[]{"Moment","Shear","DeflectionMm"})Compare(a[key],b[key],"redundant station/"+key);
                stations.RemoveAt(i);removed++;
            }
        }
        return removed;
    }
    [TestMethod]
    public void RoundoffCannotAddDuplicateStationAtAnalyticalMaximum()
    {
        foreach(double q in new[]{1d,1.0000000000000002,5.73,181.8})
        {
            var result=BridgeConcept.Analyze([36,36],q,1e6,true);
            Assert.AreEqual(82,result.Stations.Length);
            foreach(var span in result.Stations.GroupBy(s=>s.Span))Assert.AreEqual(41,span.Select(s=>s.X).Distinct().Count());
        }
    }
    [TestMethod]
    public void SimplySupportedUniformLoadMatchesIndependentStatics()
    {
        var result=BridgeConcept.Analyze([10],2,1e6,false);
        Assert.AreEqual(10d,result.Reactions[0],1e-12);Assert.AreEqual(10d,result.Reactions[1],1e-12);
        var mid=result.Stations.Single(p=>p.X==5);Assert.AreEqual(25d,mid.Moment,1e-12);Assert.AreEqual(0d,mid.Shear,1e-12);
        Assert.AreEqual(.2604166666666667,mid.DeflectionMm,1e-12);
    }
    [TestMethod]
    public void ValidationAndCancellationAreExplicit()
    {
        var data=Input(Cases()[0]!["input"]!);
        var result=BridgeConcept.Calculate(data);
        Assert.IsFalse(result.Spans is double[]);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<double>)result.Spans)[0]=1);
        Assert.ThrowsException<ArgumentException>(()=>BridgeConcept.Calculate(data with {Input=data.Input with {Length=double.NaN}}));
        Assert.ThrowsException<ArgumentException>(()=>BridgeConcept.Calculate(data with {Rates=data.Rates with {Rebar=-1}}));
        Assert.ThrowsException<ArgumentException>(()=>BridgeConcept.Calculate(data with {Input=data.Input with {Spans=2.5}}));
        Assert.ThrowsException<ArgumentException>(()=>BridgeConcept.Calculate(data with {Input=data.Input with {Soil="unknown"}}));
        Assert.ThrowsException<OperationCanceledException>(()=>BridgeConcept.Optimize(data,new(),new CancellationToken(true)));
        using var stop=new CancellationTokenSource();int notifications=0;
        Assert.ThrowsException<OperationCanceledException>(()=>BridgeConcept.Optimize(data,new(){KeepFamily=true},stop.Token,
            new ProgressSink(p=>{notifications++;stop.Cancel();})));
        Assert.IsTrue(notifications>0);
    }
    sealed class ProgressSink(Action<BridgeConcept.OptimizationProgress> action):IProgress<BridgeConcept.OptimizationProgress>
    {public void Report(BridgeConcept.OptimizationProgress value)=>action(value);}
}
