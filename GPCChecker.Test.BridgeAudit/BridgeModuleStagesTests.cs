using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X.Core;

namespace BridgeAudit;

/// <summary>Tests of the shipped bridge adapter. All audit code lives in Checker; production code is referenced unchanged.</summary>
[TestClass, TestCategory("BridgeModuleStages"), DoNotParallelize]
public class BridgeModuleStagesTests
{
    public TestContext TestContext { get; set; } = null!;
    static JsonObject Input(params JsonObject[] phases)
    {
        var d = BridgeSection.Defaults(); d["classe4"] = false;
        d["fasi"] = new JsonArray(phases.Cast<JsonNode>().ToArray()); return d;
    }
    static JsonObject Phase(string kind, double n = 0, double m = 1000, double phi = 0, double psi = 1) => BridgeSection.Phase(kind, kind, n, m, phi, psi);
    static void Near(double actual, double expected, string label, double rel = 1e-8) => Assert.AreEqual(expected, actual, 1e-7 + rel * Math.Max(1, Math.Abs(expected)), label);
    static (double A, double Y, double I) Properties(IEnumerable<(double A, double Y, double I)> components)
    {
        var p = components.ToArray(); double a = p.Sum(x => x.A), y = p.Sum(x => x.A * x.Y) / a;
        return (a, y, p.Sum(x => x.I + x.A * Math.Pow(x.Y - y, 2)));
    }
    static List<(double A, double Y, double I)> Steel(BridgeGeometry g, bool real = false) => real ?
        [ (g.TopWidth*g.TopThickness, -g.TopThickness/2, g.TopWidth*Math.Pow(g.TopThickness,3)/12),
          (g.WebThickness*g.WebHeight, -g.TopThickness-g.WebHeight/2, g.WebThickness*Math.Pow(g.WebHeight,3)/12),
          (g.Bottom1Width*g.Bottom1Thickness, -g.TopThickness-g.WebHeight-g.Bottom1Thickness/2, g.Bottom1Width*Math.Pow(g.Bottom1Thickness,3)/12),
          (g.Bottom2Width*g.Bottom2Thickness, -g.Height+g.Bottom2Thickness/2, g.Bottom2Width*Math.Pow(g.Bottom2Thickness,3)/12) ] :
        [ (g.TopWidth*g.TopThickness, -g.TopThickness/2, g.TopWidth*Math.Pow(g.TopThickness,3)/12),
          (g.WebThickness*g.WebHeight, -g.TopThickness-g.WebHeight/2, g.WebThickness*Math.Pow(g.WebHeight,3)/12),
          (g.BottomEquivalentWidth*g.BottomEquivalentThickness, -g.Height+g.BottomEquivalentThickness/2, g.BottomEquivalentWidth*Math.Pow(g.BottomEquivalentThickness,3)/12) ];

    [DataTestMethod]
    [DataRow(0d,1500d)] [DataRow(-400d,1500d)] [DataRow(300d,-1500d)] [DataRow(-500d,0d)] [DataRow(500d,0d)] [DataRow(0d,0d)]
    public void G1_GrossSteelMatchesIndependentRectangles(double n, double m)
    {
        var result = BridgeSection.Calculate(Input(Phase("Solo acciaio", n, m))); var g = result.Geometry;
        var p = Properties(Steel(g)); var c = result.Stages.Single().Contributions.Single();
        Near(c.Area,p.A,"area"); Near(c.Centroid,p.Y,"centroid"); Near(c.Inertia,p.I,"inertia");
        foreach (var point in result.Stages[0].Points)
        {
            if (point.Material == "Acciaio") Near(point.Stress,n*1000/p.A-(m*1e6+n*1000*p.Y)/p.I*(point.Y-p.Y),"steel stress");
            else { Near(point.Stress,0,"inactive material"); Assert.IsFalse(point.Active); Assert.IsNull(point.Utilization); }
        }
    }
    [DataTestMethod]
    [DataRow(0d,1d)] [DataRow(1d,1.1d)] [DataRow(2d,1.1d)] [DataRow(3d,.55d)] [DataRow(5d,2d)]
    public void G2_PhiAndNProduceSameField(double phi, double psi)
    {
        var phase = Phase("Composta", -200, 2000, phi, psi); var d=Input(phase);
        var h=BridgeSection.Homogenization(d,phase); var a=BridgeSection.Calculate(d);
        Near(h.N, h.N0*(1+psi*phi), "modular ratio");
        phase["modo"]="Da n"; phase["n"]=h.N; phase["phi"]=12345;
        var b=BridgeSection.Calculate(d);
        Near(b.Stages[0].Contributions[0].Phi,phi,"inverse phi");
        for(int i=0;i<a.Stages[0].Points.Count;i++) Near(b.Stages[0].Points[i].Stress,a.Stages[0].Points[i].Stress,"same field",1e-5);
    }
    [DataTestMethod]
    [DataRow(false,0d)] [DataRow(false,2.2d)] [DataRow(true,0d)] [DataRow(true,2.2d)]
    public void Q_AllThreeCumulativeSituationsHaveConsistentContributions(bool effective, double phi)
    {
        var d=Input(Phase("Solo acciaio",-100,1500),Phase("Composta",-200,2000,phi),Phase("Composta",50,-500)); d["classe4"]=effective;
        var result=BridgeSection.Calculate(d);
        Assert.AreEqual(3,result.Stages.Count);
        for(int i=0;i<3;i++)
        {
            var stage=result.Stages[i]; Assert.AreEqual(i+1,stage.Contributions.Count);
            Assert.IsTrue(stage.Residual<1e-7); Assert.IsTrue(stage.Contributions.All(c=>c.EquilibriumResidual<=1e-5));
            foreach(var point in stage.Points) Near(point.Stress,point.Contributions.Sum(),"sum of stress increments");
            var prefix=(JsonObject)d.DeepClone(); prefix["fasi"]=new JsonArray(d.Array("fasi").Take(i+1).Select(p=>p!.DeepClone()).ToArray());
            var standalone=BridgeSection.Calculate(prefix).Stages.Last();
            for(int j=0;j<stage.Points.Count;j++) Near(stage.Points[j].Stress,standalone.Points[j].Stress,"prefix invariance");
        }
    }
    [DataTestMethod]
    [DataRow(false,false)] [DataRow(true,false)] [DataRow(false,true)] [DataRow(true,true)]
    public void CrackedSlab_IndependentSteelAndRebarSection(bool top, bool bottom)
    {
        var d=Input(Phase("Soletta esclusa",-150,-1200)); d["rebars_top"]=top; d["rebars_bottom"]=bottom;
        var r=BridgeSection.Calculate(d); var g=r.Geometry; var parts=Steel(g); double ratio=r.Materials.Es/r.Materials.Ea;
        parts.AddRange(g.Bars.Select(b=>(b.Area*ratio,b.Y,Math.PI*Math.Pow(b.Diameter,4)/64*ratio)));
        var p=Properties(parts); var c=r.Stages[0].Contributions[0];
        Near(c.Area,p.A,"area"); Near(c.Centroid,p.Y,"centroid"); Near(c.Inertia,p.I,"inertia");
        foreach(var point in r.Stages[0].Points)
        {
            double sigma=-150000/p.A-(-1200e6-150000*p.Y)/p.I*(point.Y-p.Y);
            Near(point.Stress,point.Material=="CLS"?0:sigma*(point.Material=="Armatura"?ratio:1),"cracked stress");
            if(point.Material=="CLS") Assert.IsFalse(point.Active);
        }
    }
    [DataTestMethod]
    [DataRow("Solo acciaio")] [DataRow("Composta")] [DataRow("Soletta esclusa")]
    public void ReferenceMomentTransportPreservesStress(string kind)
    {
        var d=Input(Phase(kind,-200,2000,2.2));var a=BridgeSection.Calculate(d);
        d["y_ref"]=500; d.Array("fasi")[0]!["Mx"]=1900;var b=BridgeSection.Calculate(d);
        for(int i=0;i<a.Stages[0].Points.Count;i++) Near(b.Stages[0].Points[i].Stress,a.Stages[0].Points[i].Stress,"transport",1e-5);
    }
    [DataTestMethod]
    [DataRow("Solo acciaio")] [DataRow("Composta")] [DataRow("Soletta esclusa")]
    public void NoActionsProducesZeroStressWithClass4Enabled(string kind)
    {
        var d=Input(Phase(kind,0,0));d["classe4"]=true;var r=BridgeSection.Calculate(d);
        foreach(var p in r.Stages[0].Points) Near(p.Stress,0,"zero stress");
        Near(r.Stages[0].EffectiveSteel.Area,r.Geometry.SteelArea,"gross effective area");
    }
    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SplittingSameCompositeIncrementPreservesFinalResult(bool effective)
    {
        var d=Input(Phase("Solo acciaio",0,1500),Phase("Composta",-200,2000,2.2));d["classe4"]=effective;
        var a=BridgeSection.Calculate(d).Stages.Last();
        d["fasi"]=new JsonArray(Phase("Solo acciaio",0,1500),Phase("Composta",-80,800,2.2),Phase("Composta",-120,1200,2.2));
        var b=BridgeSection.Calculate(d).Stages.Last();
        for(int i=0;i<a.Points.Count;i++) Near(b.Points[i].Stress,a.Points[i].Stress,"split increment",1e-5);
    }
    [TestMethod]
    public void DisabledPhaseCannotAffectResultOrRequireValidActions()
    {
        var d=Input(Phase("Composta",0,2000,2.2)); var a=BridgeSection.Calculate(d);
        var ignored=Phase("Solo acciaio");ignored["attiva"]=false;ignored["N"]="bad";d.Array("fasi").Add(ignored);
        var b=BridgeSection.Calculate(d);Assert.AreEqual(1,b.Stages.Count);
        for(int i=0;i<a.Stages[0].Points.Count;i++) Near(a.Stages[0].Points[i].Stress,b.Stages[0].Points[i].Stress,"disabled phase");
    }
    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void TwoEqualWidthBottomPlatesAreExactlyEquivalent(bool effective)
    {
        var d=Input(Phase("Solo acciaio",0,-1000),Phase("Composta",0,2000,2.2));d["classe4"]=effective;
        d["plate2"]=true;d["b_bottom2"]=700;d["t_bottom2"]=20;var a=BridgeSection.Calculate(d);
        d["plate2"]=false;d["t_bottom"]=50;var b=BridgeSection.Calculate(d);
        for(int i=0;i<a.Stages.Last().Points.Count;i++) Near(a.Stages.Last().Points[i].Stress,b.Stages.Last().Points[i].Stress,"equal-width plates");
    }
    [DataTestMethod,TestCategory("ApproximationCharacterization")]
    [DataRow(500d,20d)] [DataRow(200d,60d)]
    public void UnequalWidthBottomPlatesQuantifyApproximation(double width2,double thickness2)
    {
        var d=Input(Phase("Solo acciaio"));d["plate2"]=true;d["b_bottom2"]=width2;d["t_bottom2"]=thickness2;
        var r=BridgeSection.Calculate(d);var real=Properties(Steel(r.Geometry,true));var eq=Properties(Steel(r.Geometry));
        Near(real.A,eq.A,"preserved area");Assert.IsTrue(Math.Abs(real.Y-eq.Y)>1e-3);
        double realSigma=-1e9/real.I*(-r.Geometry.Height-real.Y), eqSigma=r.Stages[0].Contributions[0].SteelStress(-r.Geometry.Height);
        TestContext.WriteLine("Real y={0:R}; equivalent y={1:R}; real I={2:R}; equivalent I={3:R}; bottom stress real={4:R}; equivalent={5:R}; error={6:P5}",real.Y,eq.Y,real.I,eq.I,realSigma,eqSigma,eqSigma/realSigma-1);
        Assert.IsTrue(r.Stages[0].Warnings.Any(w=>w.Contains("Due piastre")));
    }
    [DataTestMethod]
    [DataRow(1d,4d)] [DataRow(0d,7.809523809523809d)] [DataRow(-.5d,13.4d)] [DataRow(-1d,23.88d)] [DataRow(-2d,53.82d)] [DataRow(-3d,95.68d)]
    public void Class4_TableCoefficientsAndStressReversal(double psi,double expectedK)
    {
        var a=BridgeSection.InternalPlate(1800,10,-100,-100*psi,355);var b=BridgeSection.InternalPlate(1800,10,-100*psi,-100,355);
        Near(a.KSigma,expectedK,"k sigma");Near(a.Rho,b.Rho,"rho reversal");Near(a.EffectiveAtStart,b.EffectiveAtEnd,"effective strip reversal");
        Assert.IsTrue(a.Rho>0&&a.Rho<=1);Assert.IsTrue(a.EffectiveAtStart+a.EffectiveAtEnd<=1800+1e-7);
        Near(a.CompressedWidth,psi<0?1800/(1-psi):1800,"compressed width");
    }
    [TestMethod]
    public void Class4_JrcWorkedExample492x8()
    {
        // EUR 22898 EN, chapter 17, web subpanel 2. Published rounded values, not generated by the code under test.
        var p=BridgeSection.InternalPlate(492,8,-100,-40.6,235);
        Assert.AreEqual(5.632,p.KSigma,.001);Assert.AreEqual(.912,p.Lambda,.001);Assert.AreEqual(.871,p.Rho,.002);
    }
    [DataTestMethod]
    [DataRow(1d)] [DataRow(0d)] [DataRow(-1d)] [DataRow(-3d)]
    public void Class4_NoReductionBelowSlendernessThreshold(double psi)
    {
        var p=BridgeSection.InternalPlate(100,20,-100,-100*psi,355);Near(p.Rho,1,"stocky panel");Near(p.EffectiveAtStart+p.EffectiveAtEnd,100,"full panel");
    }
    [DataTestMethod]
    [DataRow(0d,0d)] [DataRow(20d,100d)] [DataRow(100d,20d)]
    public void Class4_TensionOnlyWebIsNotReduced(double start,double end)
    {
        var p=BridgeSection.InternalPlate(2000,6,start,end,355);Near(p.Rho,1,"tension-only rho");Near(p.EffectiveAtStart+p.EffectiveAtEnd,2000,"tension-only width");
    }
    [DataTestMethod]
    [DataRow(-100d,.5749038524302d)] [DataRow(100d,1d)]
    public void Class4_SlenderOutstandsCompressionAndTension(double stress,double expectedRho)
    {
        // b/t=28.4, fy=235: lambda=1/sqrt(0.43), rho=sqrt(0.43)-0.188*0.43.
        var d=Input(Phase("Solo acciaio"));d["b_top"]=582;d["b_bottom"]=582;d["t_top"]=10;d["t_bottom"]=10;
        var g=BridgeSection.Geometry(d);var e=BridgeSection.EffectiveWidths(g,_=>stress,235);
        Near(e.Top.Rho,expectedRho,"top outstand rho");Near(e.Bottom.Rho,expectedRho,"bottom outstand rho");
        Near(e.TopWidth,14+568*expectedRho,"effective flange width");Near(e.BottomWidth,e.TopWidth,"symmetry");
    }
    [TestMethod]
    public void Class4_SlenderFlangesConvergeUnderCentricCompression()
    {
        var d=Input(Phase("Solo acciaio",-500,0));d["classe4"]=true;d["b_top"]=582;d["b_bottom"]=582;d["t_top"]=10;d["t_bottom"]=10;d["y_ref"]=-910;
        var r=BridgeSection.Calculate(d);var s=r.Stages.Single();var c=s.Contributions.Single();
        Assert.IsTrue(s.Effective.Top.Rho<1&&s.Effective.Bottom.Rho<1&&s.Effective.Web.Rho<1);
        Near(s.Effective.TopWidth,s.Effective.BottomWidth,"symmetric effective flanges");Near(c.Centroid,-910,"symmetric centroid");
        Near(c.StressSlope,0,"centric compression curvature");Near(c.SteelStress(0)*c.Area,-500000,"integrated compression");
        Assert.IsTrue(s.Iterations<=120&&s.Residual<1e-7);
    }
    [DataTestMethod]
    [DataRow(-500d,0d)] [DataRow(0d,2000d)] [DataRow(0d,-2000d)] [DataRow(-500d,2000d)]
    public void Class4_IterationConvergesAndSatisfiesIndependentEquilibrium(double n,double m)
    {
        var d=Input(Phase("Solo acciaio",n,m));d["classe4"]=true;d["t_web"]=8;
        var r=BridgeSection.Calculate(d);var s=r.Stages[0];var g=r.Geometry;var e=s.Effective;
        var p=Properties(new[] {
            (e.TopWidth*g.TopThickness,-g.TopThickness/2,e.TopWidth*Math.Pow(g.TopThickness,3)/12),
            (g.WebThickness*e.WebTop,-g.TopThickness-e.WebTop/2,g.WebThickness*Math.Pow(e.WebTop,3)/12),
            (g.WebThickness*e.WebBottom,-g.TopThickness-g.WebHeight+e.WebBottom/2,g.WebThickness*Math.Pow(e.WebBottom,3)/12),
            (e.BottomWidth*g.BottomEquivalentThickness,-g.Height+g.BottomEquivalentThickness/2,e.BottomWidth*Math.Pow(g.BottomEquivalentThickness,3)/12) });
        var c=s.Contributions[0];Near(c.Area,p.A,"effective area");Near(c.Centroid,p.Y,"effective centroid");Near(c.Inertia,p.I,"effective inertia");
        Near(c.SteelStress(p.Y)*p.A,n*1000,"integrated N");Near(-c.StressSlope*p.I-n*1000*p.Y,m*1e6,"integrated M");
        Assert.IsTrue(s.Iterations<=120&&s.Residual<1e-7);Assert.IsTrue(s.EffectiveSteel.Area<=g.SteelArea+1e-5);
    }
    [DataTestMethod]
    [DataRow("SLU",.5666666666666667d)] [DataRow("SLE rara",.6d)] [DataRow("SLE quasi permanente",.45d)]
    public void StressResults_ConcreteLimitsAndTensionHasNoPassingRatio(string state,double factor)
    {
        var d=Input(Phase("Composta",0,-1000));d["stato"]=state;var r=BridgeSection.Calculate(d);
        foreach(var p in r.Stages[0].Points.Where(p=>p.Material=="CLS")) {Near(p.Limit,factor*r.Materials.Fck,"concrete limit");if(p.Stress>0)Assert.IsNull(p.Utilization);}
        Assert.IsTrue(r.Stages[0].Warnings.Any(w=>w.Contains("CLS teso")));
    }
    [DataTestMethod]
    [DataRow("t_web")] [DataRow("gamma_c")] [DataRow("b_cls")] [DataRow("pitch_top")] [DataRow("cover_bottom")]
    public void Validation_ZeroPositiveRequiredInputIsRejected(string key)
    {
        var d=Input(Phase("Composta"));d[key]=0;Assert.ThrowsException<ArgumentException>(()=>BridgeSection.Calculate(d));
    }
    [TestMethod]
    public void Validation_PhaseOrderAndEmptyActiveSetAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>BridgeSection.Calculate(Input(Phase("Composta"),Phase("Solo acciaio"))));
        var d=Input(Phase("Solo acciaio"));d.Array("fasi")[0]!["attiva"]=false;
        Assert.ThrowsException<ArgumentException>(()=>BridgeSection.Calculate(d));
    }
    [TestMethod]
    public void Validation_NLessThanInstantaneousRatioIsRejected()
    {
        var p=Phase("Composta");var d=Input(p);p["modo"]="Da n";p["n"]=1;Assert.ThrowsException<ArgumentException>(()=>BridgeSection.Calculate(d));
    }
    [TestMethod,TestCategory("ApproximationCharacterization")]
    public void Class4_PriorSteelContributionIsRecomputedOnEachCommonEffectiveSection()
    {
        var d=BridgeSection.Defaults();d["t_web"]=8;var r=BridgeSection.Calculate(d);
        var initial=r.Stages[0].Contributions[0];var final=r.Stages.Last().Contributions[0];
        TestContext.WriteLine("G1 top steel initial={0:R}, final={1:R}; G1 bottom initial={2:R}, final={3:R}",initial.SteelStress(0),final.SteelStress(0),initial.SteelStress(-r.Geometry.Height),final.SteelStress(-r.Geometry.Height));
        Assert.IsTrue(Math.Abs(initial.SteelStress(0)-final.SteelStress(0))>1e-3,"This test records the common-effective-section approximation, not construction-history conservation.");
    }
    [TestMethod]
    public void ZeroCompositeActionShouldReportSameIntegrationInertiaAsNonzeroAction()
    {
        // Metadata contract: section integration inertia cannot depend on whether M equals zero. Fixed in CompositeBridge 1.0.1.0
        // (before, the unloaded phase reported the Model inertia, larger by the own inertia of the flanges and of the bars).
        var zero=BridgeSection.Calculate(Input(Phase("Composta",0,0,2.2)));var a=zero.Stages[0].Contributions[0];
        var b=BridgeSection.Calculate(Input(Phase("Composta",0,1000,2.2))).Stages[0].Contributions[0];
        var g=zero.Geometry;
        double flangeOwnInertia=(g.TopWidth*Math.Pow(g.TopThickness,3)+g.BottomEquivalentWidth*Math.Pow(g.BottomEquivalentThickness,3))/12;
        double rebarOwnInertia=g.Bars.Sum(bar=>Math.PI*Math.Pow(bar.Diameter,4)/64)*(zero.Materials.Es/zero.Materials.Ea-1/b.HomogenizationN);
        Near(a.Inertia,b.Inertia,"geometric inertia is already load-independent");
        Near(a.Inertia-a.SolverInertia,flangeOwnInertia+rebarOwnInertia,"integration inertia without the own inertia of flanges and bars");
        TestContext.WriteLine("I reported for zero load={0:R}; for nonzero load={1:R}; difference={2:R}",a.SolverInertia,b.SolverInertia,a.SolverInertia-b.SolverInertia);
        Near(a.SolverInertia,b.SolverInertia,"integration inertia independent of load");
    }
}
