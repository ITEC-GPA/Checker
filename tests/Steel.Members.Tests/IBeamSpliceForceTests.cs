using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceForceTests
{
    internal static ConnectionRolledISection Section()=>new(453.4,189.9,8.5,12.7,10.2,8550,275,410);
    internal static IBeamSpliceForceInput Input(bool service=false,PlanarConnectionActions? actions=null,
        SpliceBoltLayout? web=null,SpliceBoltLayout? flange=null,double iy=294000000)
        =>new(Section(),iy,actions??(service?new( -100000,-100000,0,0,0,-133000000):new(-150000,-150000,0,0,0,-200000000)),
            web??new(2,3,85,120,70),flange??new(3,2,80,120,60));
    internal static IBeamSpliceBoltGroup Group(IBeamSpliceForceResult r,SpliceSide side,IBeamSplicePart part)
        =>r.BoltGroups.Single(g=>g.Side==side&&g.Part==part);
    private static void Equal(double expected,double actual,double relative=1e-11)
        =>Assert.AreEqual(expected,actual,Math.Max(Math.Abs(expected),1)*relative);
    private static void Unsupported(IBeamSpliceForceResult r,string reason)
    {
        Assert.IsFalse(r.IsSupported);Assert.IsNull(r.Resultants);Assert.AreEqual(0,r.BoltGroups.Count);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Contains(reason)),string.Join(";",r.Diagnostics));
    }
    private static double Quantity(IBeamSpliceForceResult r,string key)
    {
        string part=key.StartsWith("uls")||key.StartsWith("sls")?key.Substring(3):key;
        var w=Group(r,SpliceSide.Left,IBeamSplicePart.Web).Distribution;
        return part switch {
            "Top_N"=>r.Resultants.TopFlangeAxial,"Bottom_N"=>r.Resultants.BottomFlangeAxial,
            "WebAxial_N"=>r.Resultants.WebAxial,"WebMoment_Nmm"=>r.Resultants.WebMoment,
            "LeftMoment_Nmm"=>w.ActionsAtCentroid.Mz,
            "RightMoment_Nmm"=>Group(r,SpliceSide.Right,IBeamSplicePart.Web).Distribution.ActionsAtCentroid.Mz,
            "MaximumBolt_N"=>r.BoltGroups.Max(g=>g.Distribution.MaximumShear!.Value),
            "AdditionalMoment_Nmm"=>Math.Abs(w.ActionsAtCentroid.Mz-r.Resultants.WebMoment),
            "webArea_mm2"=>r.Trace.Single(t=>t.Name=="Web.Area").Value,
            "webIy_mm4"=>r.Trace.Single(t=>t.Name=="Web.StrongAxisSecondMoment").Value,
            "webAxialFraction"=>r.Trace.Single(t=>t.Name=="Web.AxialFraction").Value,
            "webMomentFraction"=>r.Trace.Single(t=>t.Name=="Web.MomentFraction").Value,
            "boltJ_mm2"=>w.Points.Sum(p=>Math.Pow(p.X-w.Centroid.X,2)+Math.Pow(p.Y-w.Centroid.Y,2)),
            _=>throw new ArgumentException(key)
        };
    }
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ibeam-splice-independent.json")));
    public static IEnumerable<object[]> ExactQuantities()
    {
        using var fixture=Fixture();
        foreach(var p in fixture.RootElement.GetProperty("independent").EnumerateObject()) yield return new object[]{p.Name,p.Value.GetDouble()};
    }
    [DataTestMethod][DynamicData(nameof(ExactQuantities),DynamicDataSourceType.Method)]
    public void IndependentUnroundedStepOneAndBoltGroupArithmetic(string key,double expected)
    {
        var r=IBeamSpliceForces.Calculate(Input(key.StartsWith("sls")));
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));Equal(expected,Quantity(r,key));
    }
    public static IEnumerable<object[]> PublishedQuantities()
    {
        using var fixture=Fixture();
        foreach(var p in fixture.RootElement.GetProperty("published").EnumerateObject())
            if(!p.Name.Contains("Printed"))yield return new object[]{p.Name,p.Value.GetDouble()};
    }
    [DataTestMethod][DynamicData(nameof(PublishedQuantities),DynamicDataSourceType.Method)]
    public void SourceRoundedNumbersAreComparedSeparately(string key,double expected)
    {
        var r=IBeamSpliceForces.Calculate(Input(key.StartsWith("sls")));
        // 1kN accommodates documented source rounding and its 0.275kNm transport typo.
        Assert.AreEqual(expected,Quantity(r,key),key.EndsWith("Nmm")?100000:1000,key);
    }
    [TestMethod]
    public void PrintedEccentricMomentIsNotUsedAsAnExactExpectedValue()
    {
        using var fixture=Fixture();
        Equal(16600000,fixture.RootElement.GetProperty("published").GetProperty("ulsAdditionalMomentPrinted_Nmm").GetDouble());
        Equal(16875000,Quantity(IBeamSpliceForces.Calculate(Input()),"ulsAdditionalMoment_Nmm"));
    }
    public static IEnumerable<object[]> SignedLoads()
    {
        foreach(double n in new[]{-170000.0,0,170000})foreach(double v in new[]{-80000.0,0,80000})
            foreach(double m in new[]{-133000000.0,0,133000000})yield return new object[]{n,v,m};
    }
    [DataTestMethod][DynamicData(nameof(SignedLoads),DynamicDataSourceType.Method)]
    public void AllSignCombinationsBalanceAllSixActionsOnEachMember(double n,double v,double m)
    {
        var r=IBeamSpliceForces.Calculate(Input(actions:new(n,v,0,0,0,m)));
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));
        foreach(var side in new[]{SpliceSide.Left,SpliceSide.Right})
        {
            var sums=new double[6];var scales=new double[6];
            foreach(var g in r.BoltGroups.Where(g=>g.Side==side))foreach(var f in g.Distribution.Forces)
            {
                bool web=g.Part==IBeamSplicePart.Web;
                double x=f.Position.X,y=web?f.Position.Y:(g.Part==IBeamSplicePart.TopFlange?1:-1)*r.Resultants.FlangeLeverArm/2;
                double z=web?0:f.Position.Y,fx=f.Fx,fy=web?f.Fy:-f.Fz,fz=web?f.Fz:f.Fy;
                double[] contribution={fx,fy,fz,y*fz-z*fy,z*fx-x*fz,x*fy-y*fx};
                for(int k=0;k<6;k++){sums[k]+=contribution[k];scales[k]+=Math.Abs(contribution[k]);}
            }
            double sign=side==SpliceSide.Left?1:-1;double[] target={sign*n,sign*v,0,0,0,sign*m};
            for(int k=0;k<6;k++)Assert.AreEqual(target[k],sums[k],Math.Max(scales[k],1)*1e-11,$"{side} action{k}");
        }
    }
    [TestMethod]
    public void OppositeWebGroupsDoNotReceiveTheSameUnsignedEccentricMoment()
    {
        var r=IBeamSpliceForces.Calculate(Input());
        var left=Group(r,SpliceSide.Left,IBeamSplicePart.Web).Distribution;
        var right=Group(r,SpliceSide.Right,IBeamSplicePart.Web).Distribution;
        Assert.IsTrue(left.MaximumShear>right.MaximumShear);
        Equal(-33750000,left.ActionsAtCentroid.Mz+right.ActionsAtCentroid.Mz);
    }
    [TestMethod]
    public void EquivalentActionPolesGiveIdenticalBoltForces()
    {
        var baseline=IBeamSpliceForces.Calculate(Input());
        // M_at_origin = M_at_pole + x*Fy-y*Fx.
        var shifted=IBeamSpliceForces.Calculate(Input(actions:new(-150000,-150000,0,0,0,-200000000-123*(-150000)+47*(-150000),123,47)));
        for(int i=0;i<6;i++)for(int j=0;j<baseline.BoltGroups[i].Distribution.Forces.Count;j++)
        {
            Equal(baseline.BoltGroups[i].Distribution.Forces[j].Fx,shifted.BoltGroups[i].Distribution.Forces[j].Fx);
            Equal(baseline.BoltGroups[i].Distribution.Forces[j].Fy,shifted.BoltGroups[i].Distribution.Forces[j].Fy);
        }
    }
    [DataTestMethod][DataRow(1e-90)][DataRow(1e-12)][DataRow(1e80)]
    public void MechanicsHasNoAbsoluteSmallLoadCutoff(double scale)
    {
        var r=IBeamSpliceForces.Calculate(Input(actions:new(-150000*scale,-150000*scale,0,0,0,-200000000*scale)));
        Assert.IsTrue(r.IsSupported,string.Join(";",r.Diagnostics));
        Assert.AreEqual(121695.04530109138,Group(r,SpliceSide.Left,IBeamSplicePart.Web).Distribution.MaximumShear!.Value/scale,1e-6);
    }
    [TestMethod]
    public void WebKeepsItsOwnMomentEvenIfFlangeBoltsCouldCarryMore()
    {
        var r=IBeamSpliceForces.Calculate(Input(flange:new(20,2,30,120,60)));
        Equal(-37779103.854875283,r.Resultants.WebMoment);
    }
    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void UnsupportedActionComponentsAreNotDiscardedEvenWhenTiny(int component)
    {
        double q=1e-50;var a=component switch {
            0=>new PlanarConnectionActions(-150000,-150000,q,0,0,-200000000),
            1=>new(-150000,-150000,0,q,0,-200000000),
            2=>new(-150000,-150000,0,0,q,-200000000),
            _=>new(-150000,-150000,0,0,0,-200000000,0,0,q)};
        Unsupported(IBeamSpliceForces.Calculate(Input(actions:a)),"IN_PLANE_N_V_M_ONLY");
    }
    [DataTestMethod][DataRow(29400000.0)][DataRow(2940000000.0)]
    public void WrongAxisOrWrongInertiaUnitsAreRejected(double iy)
        =>Unsupported(IBeamSpliceForces.Calculate(Input(iy:iy)),"STRONG_AXIS_INERTIA_INCONSISTENT_WITH_SECTION");
    [TestMethod]
    public void BoltCentresOutsideActualSteelAreRejected()
    {
        Unsupported(IBeamSpliceForces.Calculate(Input(web:new(2,3,85,210,70))),"WEB_BOLT_CENTRES_INTERSECT_ROOTS");
        Unsupported(IBeamSpliceForces.Calculate(Input(flange:new(3,2,80,200,60))),"FLANGE_BOLT_CENTRES_OUTSIDE_FLANGE");
    }
    [TestMethod]
    public void SingularWebGridCannotSilentlyLoseMoment()
        =>Unsupported(IBeamSpliceForces.Calculate(Input(web:new(1,1,85,120,70))),"single point bolt");
    [TestMethod]
    public void OverflowReturnsNoUsableDistribution()
    {
        var r=IBeamSpliceForces.Calculate(Input(actions:new(1e308,1e308,0,0,0,0,100,0)));
        Unsupported(r,"NUMERIC_RANGE");Assert.AreEqual(IBeamSpliceForceStatus.NumericalFailure,r.Status);
    }
    [TestMethod]
    public void NonPhysicalInputsThrowAtConstruction()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new SpliceBoltLayout(0,2,80,120,60));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new SpliceBoltLayout(3,2,0,120,60));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new SpliceBoltLayout(3,2,80,120,double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(iy:double.PositiveInfinity));
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSpliceForces.Calculate(null!));
    }
    [TestMethod]
    public void ZeroLoadsAreBalancedButAreNeverAWholeJointVerification()
    {
        var r=IBeamSpliceForces.Calculate(Input(actions:new(0,0,0,0,0,0)));
        Assert.IsTrue(r.IsSupported);Assert.AreEqual(6,r.BoltGroups.Count);
        Assert.IsTrue(r.BoltGroups.All(g=>g.Distribution.Forces.All(f=>f.Shear==0&&f.Fz==0)));
        StringAssert.Contains(r.Scope,"No bolt/plate/member resistance");
    }
}
