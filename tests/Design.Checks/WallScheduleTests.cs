using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Design.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Design.Checks.WallDetailTests;

namespace Design.Checks;

[TestClass]
public class WallScheduleTests
{
    static WallScheduleOptions Options(JsonNode row)
    {
        var d=row["input"]!;var o=d["bar_schedule"]!;
        return new(D(o,"panel_length"),D(o,"end_cover"),D(o,"stock_length"),(int)D(o,"ties_per_m"),D(o,"tie_cut_length"),B(d["detailing"]!,"enabled"));
    }
    [TestMethod]
    public void ThirtyFrozenSchedulesPreserveAllQuantitiesLegsAndShapes()
    {
        int schedules=0,bars=0,legs=0,points=0;
        foreach(var row in Fixture().OfType<JsonObject>().Where(r=>r["schedule"] is not null))
        {
            string id=S(row,"id");var e=row["schedule"]!;var a=WallBarScheduling.Calculate(Input(row),Options(row));schedules++;
            Same(D(e,"PanelLength"),a.PanelLength);Same(D(e,"EndCover"),a.EndCover);Same(D(e,"StockLength"),a.StockLength);
            Same(D(e,"KnownWeight"),a.KnownWeight,id);Assert.AreEqual(B(e,"CompleteQuantities"),a.CompleteQuantities,id);
            Assert.AreEqual(e["Bars"]!.AsArray().Count,a.Bars.Count,id);
            for(int i=0;i<a.Bars.Count;i++)
            {
                var x=e["Bars"]![i]!;var y=a.Bars[i];bars++;
                Assert.AreEqual(S(x,"Mark"),y.Mark);Assert.AreEqual(S(x,"Zone"),Zone(y.Zone));Assert.AreEqual(S(x,"Face"),y.Face.HasValue?Face(y.Face.Value):"fra facce");
                Assert.AreEqual(S(x,"Kind"),y.Kind switch {WallBarKind.Main=>"Principale",WallBarKind.Secondary=>"Secondaria",_=>"Collegamento"});
                Assert.AreEqual((int)D(x,"Quantity"),y.Quantity,id);
                foreach(string p in new[]{"Diameter","Spacing","Mandrel","Lap"})
                    Same(D(x,p),Convert.ToDouble(typeof(WallScheduledBar).GetProperty(p)!.GetValue(y),CultureInfo.InvariantCulture),id+"/"+p);
                Same(x["CutLength"]?.GetValue<double>(),y.CutLength,id);Same(x["TotalLength"]?.GetValue<double>(),y.TotalLength,id);Same(x["Weight"]?.GetValue<double>(),y.Weight,id);
                Assert.AreEqual(x["Legs"]!.AsArray().Count,y.Legs.Count);
                for(int j=0;j<y.Legs.Count;j++){legs++;Same(D(x["Legs"]![j]!,"Length"),y.Legs[j].Length,id);Assert.AreEqual(S(x["Legs"]![j]!,"Name"),y.Legs[j].Name=="Arc90"?"arco 90°":y.Legs[j].Name);}
                Assert.AreEqual(x["Shape"]!.AsArray().Count,y.Shape.Count);
                for(int j=0;j<y.Shape.Count;j++){points++;Same(D(x["Shape"]![j]!,"X"),y.Shape[j].X,id);Same(D(x["Shape"]![j]!,"Y"),y.Shape[j].Y,id);}
                if(y.Note==WallBarNote.None) Assert.AreEqual("",S(x,"Note"));
                if(y.Note==WallBarNote.LongitudinalEndsToDetail) StringAssert.EndsWith(S(x,"Note"),"Zona "+y.ZoneStart!.Value.ToString("0.###",CultureInfo.InvariantCulture)+"–"+y.ZoneEnd!.Value.ToString("0.###",CultureInfo.InvariantCulture)+" m.");
            }
            Assert.AreEqual(e["Warnings"]!.AsArray().Count,a.Warnings.Count,id);
            Assert.AreEqual(!a.CompleteQuantities,a.Warnings.Any(w=>w.Code==WallScheduleWarningCode.MissingTieDevelopment));
            foreach(var w in a.Warnings.Where(w=>w.Code==WallScheduleWarningCode.LongerThanStock))
                Assert.IsTrue(a.Bars.Single(b=>b.Mark==w.Mark).CutLength>a.StockLength);
        }
        Assert.AreEqual(30,schedules);Console.WriteLine($"Schedules: {schedules} schedules, {bars} bars, {legs} legs, {points} points equivalent; quantities exact");
    }
    [TestMethod]
    public void UnknownTieLengthIsDistinctFromZeroAndStockExcessDoesNotSplitBars()
    {
        var row=Fixture().OfType<JsonObject>().First(r=>S(r,"id")=="schedules/panel-00.json");var input=Input(row);var opt=Options(row);
        var missing=WallBarScheduling.Calculate(input,opt with {StockLength=1,TieCutLength=0});
        var c=missing.Bars.Single(b=>b.Kind==WallBarKind.Tie);Assert.IsNull(c.CutLength);Assert.IsNull(c.Weight);Assert.IsFalse(missing.CompleteQuantities);
        Assert.IsTrue(missing.Warnings.Any(w=>w.Code==WallScheduleWarningCode.LongerThanStock));
        var complete=WallBarScheduling.Calculate(input,opt with {StockLength=1,TieCutLength=900});
        Assert.IsTrue(complete.CompleteQuantities);Assert.IsTrue(complete.KnownWeight>missing.KnownWeight);
        CollectionAssert.AreEqual(missing.Bars.Select(b=>b.Mark).ToArray(),complete.Bars.Select(b=>b.Mark).ToArray());
        Same(complete.Bars.Single(b=>b.Kind==WallBarKind.Tie).Weight!.Value,complete.KnownWeight-missing.KnownWeight);
    }
    [TestMethod]
    public void SecondaryBoundaryIsOwnedOnceAndPanelEndsPreserveCounts()
    {
        var row=Fixture().OfType<JsonObject>().First(r=>S(r,"id")=="schedules/panel-00.json");var p=Input(row);var a=WallBarScheduling.Calculate(p,Options(row));
        var lower=a.Bars.Single(b=>b.Zone==WallRebarZone.Stem && b.Kind==WallBarKind.Secondary && b.Face==WallRebarFace.Upstream);
        var upper=a.Bars.Single(b=>b.Zone==WallRebarZone.StemUpper && b.Kind==WallBarKind.Secondary && b.Face==WallRebarFace.Upstream);
        Same(lower.ZoneEnd,upper.ZoneStart);
        double lastLower=lower.ZoneStart!.Value+(lower.Quantity-1)*lower.Spacing/1000;
        Assert.IsTrue(lastLower<upper.ZoneStart!.Value);
        foreach(var b in a.Bars.Where(b=>b.Kind==WallBarKind.Main))
            Same(a.PanelLength*1000-2*a.EndCover-b.Diameter,(b.Quantity-1)*b.Spacing);
    }
    [TestMethod]
    public void InvalidPanelsAndCancellationAreAtomic()
    {
        var row=Fixture()[0]!;var p=Input(row);var o=Options(row);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>WallBarScheduling.Calculate(p,o with {PanelLength=0}));
        Assert.ThrowsException<ArgumentException>(()=>WallBarScheduling.Calculate(p,o with {PanelLength=.2,EndCover=150}));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsException<OperationCanceledException>(()=>WallBarScheduling.Calculate(p,o,cancel.Token));
    }
}
