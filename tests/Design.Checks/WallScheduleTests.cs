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
    // ANTHEA 1.16.0 (registro R-MURI-*): the 30 frozen F4 schedules counted the main bars at the spacing of an isolated metre and listed
    // the longitudinal bars and the ties as preliminary. Each frozen quantity is explained by the old rule restated here, and the new
    // one is restated independently: bars at both ends of the panel at no more than 1000/n; longitudinal bars over the net panel,
    // one piece up to the stock bar; ties at the calculated or assigned length. Shapes and lengths: WallTavolaTests.
    [TestMethod]
    public void ThirtyFrozenSchedulesChangeOnlyByTheDeclaredCorrections()
    {
        int schedules=0,main=0,secondary=0,ties=0;
        foreach(var row in Fixture().OfType<JsonObject>().Where(r=>r["schedule"] is not null))
        {
            string id=S(row,"id");var e=row["schedule"]!;var input=Input(row);var o=Options(row);var a=WallBarScheduling.Calculate(input,o);schedules++;
            Same(D(e,"PanelLength"),a.PanelLength);Same(D(e,"EndCover"),a.EndCover);Same(D(e,"StockLength"),a.StockLength);
            double net=o.PanelLength-2*o.EndCover/1000;
            foreach(var x in e["Bars"]!.AsArray())
            {
                string kind=S(x!,"Kind");double dia=D(x!,"Diameter");
                if(kind=="Principale")
                {
                    main++;var zone=Enum.GetValues(typeof(WallRebarZone)).Cast<WallRebarZone>().Single(z=>Zone(z)==S(x!,"Zone"));
                    var face=Enum.GetValues(typeof(WallRebarFace)).Cast<WallRebarFace>().Single(f=>Face(f)==S(x!,"Face"));
                    var arm=input.Reinforcement.Zones[zone];int n=face is WallRebarFace.Upstream or WallRebarFace.Bottom?arm.FirstFace.Count:arm.OppositeFace.Count;
                    double span=o.PanelLength*1000-2*o.EndCover-dia;
                    Assert.AreEqual((int)Math.Ceiling(span/OldSpacing(input.Cover,dia,n)-1e-10)+1,(int)D(x!,"Quantity"),id);
                    var y=a.Bars.SingleOrDefault(b=>b.Kind==WallBarKind.Main && b.Zone==zone && b.Face==face)??a.Bars.Single(b=>b.Kind==WallBarKind.Main && b.Note==WallBarNote.Continuous && b.Face==face);
                    Same(dia,y.Diameter,id);Same(1000.0/n,y.Spacing,id);Assert.AreEqual((int)Math.Ceiling(span*n/1000-1e-10)+1,y.Quantity,id);
                }
                else if(kind=="Secondaria")
                {
                    secondary++;Same(net,D(x!,"CutLength"),id);
                    var y=a.Bars.Where(b=>b.Kind==WallBarKind.Secondary && b.Diameter==dia).ToArray();Assert.IsTrue(y.Length>0,id);
                    foreach(var b in y) Same(net<=o.StockLength?Math.Ceiling(net*100-1e-9)/100:b.CutLength!.Value,b.CutLength!.Value,id);
                }
                else
                {
                    ties++;var y=a.Bars.Single(b=>b.Kind==WallBarKind.Tie);
                    Assert.AreEqual((int)D(x!,"Quantity"),y.Quantity,id);Same(dia,y.Diameter,id);
                    if(o.TieCutLength>0) Same(D(x!,"CutLength"),y.CutLength,id); else Assert.IsNull(x!["CutLength"]?.GetValue<double>(),id);
                    Assert.IsNotNull(y.CutLength,id);
                }
            }
            Assert.IsTrue(a.CompleteQuantities,id);
            foreach(var w in a.Warnings.Where(w=>w.Code==WallScheduleWarningCode.LongerThanStock))
                Assert.IsTrue(a.Bars.Single(b=>b.Mark==w.Mark).CutLength>a.StockLength);
        }
        Assert.AreEqual(30,schedules);Console.WriteLine($"Schedules: {schedules} schedules, {main} main, {secondary} longitudinal and {ties} tie positions explained");
    }
    [TestMethod]
    public void AssignedTieLengthOverridesAndStockExcessDoesNotSplitMainBars()
    {
        var row=Fixture().OfType<JsonObject>().First(r=>S(r,"id")=="schedules/panel-00.json");var input=Input(row);var opt=Options(row);
        var calculated=WallBarScheduling.Calculate(input,opt with {StockLength=1.5,TieCutLength=0});
        var c=calculated.Bars.Single(b=>b.Kind==WallBarKind.Tie);Assert.IsNotNull(c.CutLength);Assert.IsTrue(calculated.CompleteQuantities);
        Assert.IsTrue(calculated.Warnings.Any(w=>w.Code==WallScheduleWarningCode.LongerThanStock));
        var assigned=WallBarScheduling.Calculate(input,opt with {StockLength=1.5,TieCutLength=900});
        Same(.9,assigned.Bars.Single(b=>b.Kind==WallBarKind.Tie).CutLength!.Value);
        CollectionAssert.AreEqual(calculated.Bars.Select(b=>b.Mark).ToArray(),assigned.Bars.Select(b=>b.Mark).ToArray());
        var t=assigned.Bars.Single(b=>b.Kind==WallBarKind.Tie);
        Same(t.Weight!.Value-c.Weight!.Value,assigned.KnownWeight-calculated.KnownWeight);
    }
    [TestMethod]
    public void LongitudinalBoundaryIsOwnedOnceAndPanelEndsKeepTheSpacing()
    {
        var row=Fixture().OfType<JsonObject>().First(r=>S(r,"id")=="schedules/panel-00.json");var p=Input(row);var a=WallBarScheduling.Calculate(p,Options(row));
        var lower=a.Bars.Single(b=>b.Zone==WallRebarZone.Stem && b.Kind==WallBarKind.Secondary && b.Face==WallRebarFace.Upstream);
        var upper=a.Bars.Single(b=>b.Zone==WallRebarZone.StemUpper && b.Kind==WallBarKind.Secondary && b.Face==WallRebarFace.Upstream);
        double split=p.Geometry.Slab+p.Reinforcement.LowerHeight;
        Assert.IsTrue(lower.SectionPoints.All(q=>q.Y<split));Same(split,upper.SectionPoints[0].Y);
        foreach(var b in a.Bars.Where(b=>b.Kind==WallBarKind.Main))
            Assert.IsTrue((a.PanelLength*1000-2*a.EndCover-b.Diameter)/(b.Quantity-1)<=b.Spacing+1e-9);
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
