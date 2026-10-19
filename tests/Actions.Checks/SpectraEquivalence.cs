using GPC.Design.Actions;
using System.Text.Json;
internal static class SpectraEquivalence
{
 internal static void Verify(string path)
 {
int checks=0,failures=0;double worst=0;
void Equal(double actual,double expected,string name) {
    checks++;double delta=Math.Abs(actual-expected);worst=Math.Max(worst,delta);
    if(!double.IsFinite(actual)||delta>1e-10*Math.Max(1,Math.Abs(expected))) { failures++;if(failures<30)Console.WriteLine($"FAIL {name}: {actual:R} != {expected:R}"); }
}
using var doc=JsonDocument.Parse(File.ReadAllText(path));
foreach(var c in doc.RootElement.EnumerateArray()) {
    double D(string key)=>c.GetProperty(key).GetDouble();
    string name=c.GetProperty("name").GetString()+"/"+D("method");
    SiteHazard site;
    try {site=NtcSpectra.Site(D("longitude"),D("latitude"),(SiteInterpolation)(int)D("method"));}
    catch(ArgumentException) when(D("status")==0) {checks++;continue;}
    catch(Exception ex) {failures++;Console.WriteLine($"FAIL {name}: {ex}");continue;}
    Equal(site.GridType,D("kind"),name+"/grid");
    int j=0;foreach(var p in c.GetProperty("periods").EnumerateArray()) {
        var h=site.Periods[j++].Parameters;
        Equal(h.AgG,p[1].GetDouble(),name+"/ag");Equal(h.F0,p[2].GetDouble(),name+"/F0");Equal(h.TcStarSeconds,p[3].GetDouble(),name+"/Tc");
    }
    var states=NtcSpectra.Calculate(site,new(D("nominal"),D("use"),"ABCDE"[(int)D("soil")-1].ToString(),(int)D("topography"),D("height"),D("damping"),D("mode")==2,D("q0"),D("regular")==1,D("qv")));
    int i=0;foreach(var state in c.GetProperty("states").EnumerateArray()) {
        var s=states[i++];Equal(s.ReturnYears,state.GetProperty("tr").GetDouble(),name+"/tr");
        foreach(var (key,points) in new[]{("horizontal",s.Horizontal),("vertical",s.Vertical),("reference",s.Reference)}) {
            int r=0; foreach(var row in state.GetProperty(key).EnumerateArray()) {
                Equal(points[r].PeriodSeconds,row[0].GetDouble(),$"{name}/{s.State}/{key}/{r}/T");
                Equal(points[r].AccelerationG,row[1].GetDouble(),$"{name}/{s.State}/{key}/{r}/Sa");r++;
            }
        }
    }
}
Console.WriteLine($"Checks={checks}; failures={failures}; maximum absolute difference={worst:R}; tolerance=1e-10*max(1,|expected|)");
if(failures!=0)throw new InvalidOperationException($"{failures} confronti Spettri falliti.");

 }
}
