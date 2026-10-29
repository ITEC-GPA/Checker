using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

/// <summary>Section transitions of bolted welded-I double covers. Expected values come from an
/// independent exact-fraction free-body calculation in the global frame (bottom outer face, pole at
/// the left centroid): elastic shares of each section, cover forces from Fo+Fi=N, -(Yo Fo+Yi Fi)=-Yf N+M,
/// transfers T=N_own-N_delivered, couples dM=M_own-M_delivered-+T t/2. No call to the library distributor.</summary>
[TestClass]
public class BridgeSpliceTransitionTests
{
    // Left: 400x30 / 360x16 / 400x30, H=420, yc=210. Right: same depth and widths, other thicknesses.
    internal static WeldedIBeamSpliceSection Section(double top=30,double web=16,double bottom=30,double topToe=8)
        =>new(new(400,top,355,510),new(420-top-bottom,web,355,510),new(400,bottom,355,510),topToe,8);
    internal static WeldedIBeamSpliceForceInput Detail(double top=40,double web=16,double bottom=30,PlanarConnectionActions? actions=null,
        SpliceSide side=SpliceSide.Left,WeldedSpliceSectionTransition? transition=null,bool pack=true)
    {
        var d=BridgeSpliceUnequalWidthTests.Detail(360,actions??new(2e5,-1e5,0,0,0,50e6));
        // The packing fills every cover plane on the thinner member.
        double ti=pack?top-30:0,bi=pack?bottom-30:0,w=pack?(web-16)/2:0;
        var left=new WeldedSplicePacking(0,Math.Max(0,ti),0,Math.Max(0,bi),Math.Max(0,w),"Fitted full-footprint packs; grip and washers checked");
        var right=new WeldedSplicePacking(0,Math.Max(0,-ti),0,Math.Max(0,-bi),Math.Max(0,-w),"Fitted full-footprint packs; grip and washers checked");
        return new(Section(),d.Top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions,left,
            transition??new WeldedSpliceSectionTransition(Section(top,web,bottom),right),side);
    }
    internal static SteelSectionInput Model(WeldedIBeamSpliceSection s)
        =>new(new SteelSection(new SectionH(s.Height,s.Web.Thickness,s.TopFlange.Width,s.TopFlange.Thickness,s.BottomFlange.Width,s.BottomFlange.Thickness,"Transition parent",0),
            new SteelMaterial("S355 test",210000,355,510),GPC.Model.Sections.Section.SectionTypes.Welded,GPC.Model.Sections.Section.FormedTypes.HotFinished),true);
    static IEnumerable<WeldedSpliceFrictionInterface> Interfaces(WeldedSplicePacking p,WeldedSpliceCover c,int extra=0)
        =>Enumerable.Range(0,p.Layers(c).Count+1+extra).Select(j=>new WeldedSpliceFrictionInterface(j==0?.4:.35,j==0?"Cover surface":"Packing surface"));
    internal static WeldedIBeamSpliceSlipInput SlipTemplate(bool ntc,WeldedIBeamSpliceForceInput d,int rightExtra=0)
    {
        var q=WeldedIBeamSpliceSlipTests.Input(ntc,BoltSlipLimitState.Ultimate,d);
        return new(q.Policy,q.Bolts,BoltSlipLimitState.Ultimate,Enum.GetValues<WeldedSpliceCover>().Select(c=>
            new WeldedSpliceFrictionSurface(c,Interfaces(d.Packing,c),Interfaces(d.Transition!.RightPacking,c,rightExtra))),
            q.TighteningControl,q.AssemblyQualificationSource,q.ActionCombinationSource);
    }
    internal static SpliceActionCase[] Actions(double scale=1)=>new[]{
        new SpliceActionCase("ULS+",new(2e5*scale,-1e5*scale,0,0,0,50e6*scale),"Combination +"),
        new SpliceActionCase("ULS-",new(-2e5*scale,1e5*scale,0,0,0,-50e6*scale),"Combination -")};
    internal static WeldedIBeamSpliceStandardInput Create(bool ntc=false,SpliceBoltCategory category=SpliceBoltCategory.A,WeldedIBeamSpliceForceInput? detail=null,
        SpliceFlangeFaceModel model=SpliceFlangeFaceModel.PackedSectionTransition,SplicePlateStabilityMethod stability=SplicePlateStabilityMethod.CompactCovers,
        SpliceActionCase[]? actions=null,int rightExtra=0)
    {
        var d=detail??Detail();
        var template=BridgeSpliceApplicationTests.Bridge(WeldedIBeamSpliceStandardTests.Input(ntc,d));
        return BridgeSpliceApplication.CreateBolted(template,Model(d.Section),Model(d.Transition!.Right),actions??Actions(),WeldedIBeamSpliceStandardTests.Welds(),
            "Global analysis with the actual section change and centroid offset",category,stability,BridgeSpliceApplicationTests.Prepare,
            category==SpliceBoltCategory.A?null:SlipTemplate(ntc,d,rightExtra),
            category==SpliceBoltCategory.B?new[]{new SpliceActionCase("SLS",new(1e5,-5e4,0,0,0,25e6),"SLS combination")}:null,
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,model);
    }
    static WeldedIBeamSpliceTransitionResult Transfer(WeldedIBeamSpliceForceInput d,bool ntc=false,double throat=5)
    {
        var detail=WeldedIBeamSpliceStandardTests.Input(ntc,d);var f=WeldedIBeamSpliceForces.Calculate(d);
        var parents=new WeldedIBeamSpliceParentInput(detail,WeldedIBeamSpliceStandardTests.Section(detail),WeldedIBeamSpliceStandardTests.Section(detail),
            WeldedIBeamSpliceStandardTests.Welds(throat),"Global analysis");
        return WeldedIBeamSpliceTransfer.Verify(parents,f,WeldedIBeamSplicePlates.Verify(detail.Plates),SplicePlateStabilityMethod.CompactCovers);
    }
    static double V(IEnumerable<SectionResistanceValue> trace,string name)=>trace.Single(t=>t.Name==name).Value;
    static ConnectionCheck C(IEnumerable<ConnectionCheck> checks,string id)=>checks.Single(c=>c.Id==id);
    static void Close(double expected,double actual,double relative=1e-9)=>Assert.AreEqual(expected,actual,Math.Max(1e-9,Math.Abs(expected)*relative));
    static string All(WeldedIBeamSpliceStandardResult r)=>string.Join(";",r.MissingRequirements);

    [TestMethod]
    public void ThickerFlangeOnTheRightTransfersTheMeasuredDifferenceInsideTheOtherMember()
    {
        // Pure N=1e6 at the LEFT centroid (210): the right centroid is 230, so MR=+20e6 about its own centroid.
        var left=WeldedIBeamSpliceForces.Calculate(Detail(actions:new(1e6,0,0,0,0,0)));
        Assert.IsTrue(left.IsBalanced,string.Join(";",left.Diagnostics));
        Close(2e7,V(left.Trace,"Right.Own.MzAtCentroid"));Close(0,V(left.Trace,"Left.Own.MzAtCentroid"));
        // Left distribution through the covers: Fo, Fi from the common cover planes (Yo=440, Yi=370 for the top flange).
        Close(235215.05376344087,V(left.Trace,"Left.Top.OuterForce"));Close(168010.75268817204,V(left.Trace,"Left.Top.BothInnerForce"));
        var t=Transfer(Detail(actions:new(1e6,0,0,0,0,0))).Transfer;Assert.IsTrue(t.IsComplete,string.Join(";",t.Diagnostics));
        Close(-2016129.0322580645,V(t.Trace,"Transition.Right.Top.DeliveredLocalMoment"));
        Close(22631.58313634663,V(t.Trace,"Transition.Right.Top.TransferFromWeb"));
        Close(1659.4637186313603,V(t.Trace,"Transition.Right.Bottom.TransferFromWeb"));
        Close(-(22631.58313634663+1659.4637186313603),V(t.Trace,"Transition.Right.Web.AxialChange"));
        Close(1602974.3001997725,V(t.Trace,"Transition.Right.Top.JunctionCouple"));
        Close(41546.28590530312,V(t.Trace,"Transition.Right.Bottom.JunctionCouple"));
        Close(5,V(t.Trace,"Transition.Right.Web.DeliveredEccentricity"));
        Close(-967741.9354838709,V(t.Trace,"Transition.Right.Web.DeliveredLocalMoment"));
        // T/L and 4dM/L^2 over the 265mm lap (70+140+60-10/2); double 5mm fillets, directional method.
        Close(85.4022005145156,V(t.Trace,"Transition.Right.Top.LongitudinalJunctionFlow"));
        Close(91.30505091917536,V(t.Trace,"Transition.Right.Top.VerticalJunctionLoad"));
        Close(19.635129794547066,C(t.Checks,"Right.Top.TransferFillets.DirectionalEquivalent").Demand);
        Close(6.45624206615319,C(t.Checks,"Right.Top.TransferFillets.NormalTension").Demand);
        Close(510/(.9*1.25),C(t.Checks,"Right.Top.TransferFillets.DirectionalEquivalent").Resistance);
        Assert.IsFalse(t.Checks.Any(c=>c.Id.StartsWith("Left.")),"The distributing member transfers nothing.");
        // The right distribution moves the whole transfer into the left member, with opposite sign.
        var r=Transfer(Detail(actions:new(1e6,0,0,0,0,0),side:SpliceSide.Right)).Transfer;Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        var rf=WeldedIBeamSpliceForces.Calculate(Detail(actions:new(1e6,0,0,0,0,0),side:SpliceSide.Right));
        Close(212270.74594950242,V(rf.Trace,"Left.Top.OuterForce"));Close(213586.6436384571,V(rf.Trace,"Left.Top.BothInnerForce"));
        Close(-22631.58313634663,V(r.Trace,"Transition.Left.Top.TransferFromWeb"));
        Close(2168763.878608438,V(r.Trace,"Transition.Left.Top.DeliveredLocalMoment"));
        Close(-1829290.1315632388,V(r.Trace,"Transition.Left.Top.JunctionCouple"));
        Close(1904145.0777202072,V(r.Trace,"Transition.Left.Web.DeliveredLocalMoment"));
        Close(20.879223927160467,C(r.Checks,"Left.Top.TransferFillets.DirectionalEquivalent").Demand);
        Assert.IsFalse(r.Checks.Any(c=>c.Id.StartsWith("Right.")));
    }

    public static IEnumerable<object[]> NmvCases()
    {
        // top, web, bottom, sign, distribution, redistributing junction, T_top, T_bottom, couple top, couple bottom, top fillet, bottom fillet
        yield return new object[]{40d,16d,30d,1d,SpliceSide.Left,-1503.981713289487,-114.49285353874849,-105196.79044659855,-2828.554603999363,44.58079462465061,41.42098178249048};
        yield return new object[]{40d,16d,30d,1d,SpliceSide.Right,1503.981713289487,114.49285353874849,120236.60757949341,2828.554603999363,42.494822622662234,41.57561426602835};
        yield return new object[]{30d,24d,30d,1d,SpliceSide.Left,-3417.932674545208,-10813.566376688188,49846.75286776601,-163625.73290073493,42.45568725062335,47.30597050290455};
        yield return new object[]{30d,24d,30d,1d,SpliceSide.Right,3417.932674545208,10813.566376688188,-49846.75286776601,163625.73290073493,43.73659297043828,48.58644763332977};
        yield return new object[]{40d,24d,30d,1d,SpliceSide.Left,-5679.278512592468,-11119.658977945503,-25326.614818370745,-169439.88287106095,46.59703276329389,47.15013232331412};
        yield return new object[]{40d,24d,30d,1d,SpliceSide.Right,5679.278512592468,11119.658977945503,82119.39994429542,169439.88287106095,45.217613490620586,48.787725475183066};
        // Sign reversal: every transfer and couple changes sign, the fillet stresses do not.
        yield return new object[]{40d,24d,30d,-1d,SpliceSide.Left,5679.278512592468,11119.658977945503,25326.614818370745,169439.88287106095,46.59703276329389,47.15013232331412};
        yield return new object[]{40d,24d,30d,-1d,SpliceSide.Right,-5679.278512592468,-11119.658977945503,-82119.39994429542,-169439.88287106095,45.217613490620586,48.787725475183066};
    }
    [DataTestMethod,DynamicData(nameof(NmvCases),DynamicDataSourceType.Method)]
    public void ConcomitantNmvTransfersMatchTheIndependentFreeBodies(double top,double web,double bottom,double sign,SpliceSide side,
        double tTop,double tBottom,double cTop,double cBottom,double weldTop,double weldBottom)
    {
        foreach(bool ntc in new[]{false,true})
        {
            var d=Detail(top,web,bottom,new(2e5*sign,-1e5*sign,0,0,0,50e6*sign),side);
            var result=Transfer(d,ntc);var t=result.Transfer;Assert.IsTrue(t.IsComplete,string.Join(";",t.Diagnostics));
            string m=(side==SpliceSide.Left?"Right":"Left")+".";
            Assert.AreEqual(side,result.CoverDistribution);
            Close(tTop,V(t.Trace,"Transition."+m+"Top.TransferFromWeb"));Close(tBottom,V(t.Trace,"Transition."+m+"Bottom.TransferFromWeb"));
            Close(cTop,V(t.Trace,"Transition."+m+"Top.JunctionCouple"));Close(cBottom,V(t.Trace,"Transition."+m+"Bottom.JunctionCouple"));
            Close(weldTop,C(t.Checks,m+"Top.TransferFillets.DirectionalEquivalent").Demand);
            Close(weldBottom,C(t.Checks,m+"Bottom.TransferFillets.DirectionalEquivalent").Demand);
            // The two web free-body residuals close by global equilibrium, not by an imposed adjustment.
            Assert.IsTrue(Math.Abs(V(t.Trace,"Transition."+m+"Web.Residual.Fx"))<1e-6);
            Assert.IsTrue(Math.Abs(V(t.Trace,"Transition."+m+"Web.Residual.Mz"))<1e-2);
            // NTC: same weld formula (beta_w=0.9, gammaM2=1.25), NTC gammaM0=1.05 on the web junction.
            Close(ntc?355/1.05:355,C(t.Checks,m+"Top.WebJunction.Equivalent").Resistance);
        }
    }

    [TestMethod]
    public void EveryContinuousCoverBalancesWhereTwoIndependentElasticDistributionsWouldNot()
    {
        foreach(var side in new[]{SpliceSide.Left,SpliceSide.Right})
        {
            var d=Detail(40,24,30,side:side);var f=WeldedIBeamSpliceForces.Calculate(d);Assert.IsTrue(f.IsBalanced);
            var t=Transfer(d).Transfer;
            foreach(var cover in Enum.GetValues<WeldedSpliceCover>())
            {
                double scale=f.CoverGroups.Where(g=>g.Cover==cover).Sum(g=>g.Distribution.Forces.Sum(b=>Math.Abs(b.Fx)+Math.Abs(b.Fy)));
                foreach(string c in new[]{"Fx","Fy"})Assert.IsTrue(Math.Abs(V(t.Trace,"Transition."+cover+".ContinuousCoverResidual."+c))<=1e-12*scale);
                // Left bolts and right bolts of one cover carry the same force, mirrored.
                Close(f.CoverGroups.Single(g=>g.Cover==cover&&g.Side==SpliceSide.Left).Distribution.Forces.Sum(b=>b.Fx),
                    -f.CoverGroups.Single(g=>g.Cover==cover&&g.Side==SpliceSide.Right).Distribution.Forces.Sum(b=>b.Fx),1e-12);
            }
            foreach(string c in new[]{"Fx","Fy","Mz"})foreach(var s in new[]{"Left","Right"})
                Assert.IsTrue(Math.Abs(V(f.Trace,s+".Residual."+c))<1e-3);
        }
        // Independent elastic shares (oracle): top flange -39157.25687541024 (left) and -44836.53538800271 (right).
        // Loading the left bolts with one and the right bolts with the other would leave 5679.28 N on every
        // top-flange cover pair: the library carries exactly this T through the junction of the other member.
        var a=WeldedIBeamSpliceForces.Calculate(Detail(40,24,30));
        Close(-39157.25687541024,V(a.Trace,"Left.Own.Top.Axial"));Close(-44836.53538800271,V(a.Trace,"Right.Own.Top.Axial"));
        Close(-44836.53538800271+39157.25687541024,V(Transfer(Detail(40,24,30)).Transfer.Trace,"Transition.Right.Top.TransferFromWeb"));
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PackingFactorAndGripArePerMember(bool ntc)
    {
        var r=WeldedIBeamSpliceBolts.Verify(WeldedIBeamSpliceStandardTests.Input(ntc,Detail(40,24,30)).Plates.Bolts).ComponentAssessment;
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        double plane=.6*800*353/1.25;
        // EN1993-1-8 3.6.1(12) Eq3.3: left top flange packed 10mm>d/3: beta_p=9*24/(8*24+3*10); the right bolts are unpacked.
        foreach(var face in new[]{"TopOuter","TopInnerNegative","TopInnerPositive"})
        {
            Close(plane*216/222,C(r.Checks,"Left."+face+".Bolt0.ShearPlane").Resistance,1e-12);
            Close(plane,C(r.Checks,"Right."+face+".Bolt0.ShearPlane").Resistance,1e-12);
            // Grip: 30+10+20+20 on the left, 40+0+20+20 on the right.
            Assert.AreEqual(80,V(r.Trace,"Left."+face+".GripThickness"));Assert.AreEqual(80,V(r.Trace,"Right."+face+".GripThickness"));
        }
        // Web packing 4mm<=d/3: no reduction. Grip 16+2*(25+4) = 24+2*25.
        Close(plane,C(r.Checks,"Left.WebNegative.Bolt0.ShearPlane").Resistance,1e-12);
        Assert.AreEqual(74,V(r.Trace,"Left.WebPositive.GripThickness"));Assert.AreEqual(74,V(r.Trace,"Right.WebPositive.GripThickness"));
        // Parent bearing uses each member's own flange: Fb proportional to t, same edges and pitches.
        Close(40d/30,V(r.Trace,"Right.TopFlange.Parent.Bolt1.BearingX")/V(r.Trace,"Left.TopFlange.Parent.Bolt1.BearingX"),1e-12);
    }

    public static IEnumerable<object[]> Workflows()
    {
        foreach(bool ntc in new[]{false,true})foreach(var cat in new[]{SpliceBoltCategory.A,SpliceBoltCategory.B,SpliceBoltCategory.C})
        foreach(var g in new[]{(40d,16d,30d),(30d,24d,30d),(40d,24d,30d),(40d,16d,40d)})yield return new object[]{ntc,cat,g.Item1,g.Item2,g.Item3};
    }
    [DataTestMethod,DynamicData(nameof(Workflows),DynamicDataSourceType.Method)]
    public void CompleteWorkflowChecksBothDistributionsWithEveryIdenticalRouteComponent(bool ntc,SpliceBoltCategory cat,double top,double web,double bottom)
    {
        var p=Create(ntc,cat,Detail(top,web,bottom));var r=WeldedIBeamSpliceStandard.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,All(r));Assert.IsTrue(r.IsStaticResistanceComplete);
        CollectionAssert.AreEqual(new[]{"ULS+.LeftDistribution","ULS+.RightDistribution","ULS-.LeftDistribution","ULS-.RightDistribution"},p.Cases.Select(c=>c.Id).ToArray());
        Assert.AreEqual(cat==SpliceBoltCategory.A?0:cat==SpliceBoltCategory.B?2:4,r.SlipCases.Count);
        foreach(var c in r.Cases)
        {
            Assert.IsNotNull(c.Transition);
            Assert.AreEqual(c.Input.Id.Contains("Left")?SpliceSide.Left:SpliceSide.Right,c.Transition!.CoverDistribution);
            Assert.AreEqual(ConnectionStatus.Passed,c.Transition.Transfer.Status);Assert.AreEqual(ConnectionStatus.Passed,c.Transition.Domain.Status);
        }
        var components=r.CheckSources.Select(s=>s.Component).Distinct().ToArray();
        foreach(var x in new[]{"Bolts","Detailing","TopOuter.GrossSections","WebPositive.Compression","CombinedNetSections","DirectBlockTearing",
            "LongitudinalWelds","Left.Section","Right.Section","SectionTransfer"})
            Assert.IsTrue(components.Contains(x),x);
        if(cat!=SpliceBoltCategory.A)Assert.IsTrue(components.Contains("Slip"));
        if(cat==SpliceBoltCategory.C)
            foreach(var id in new[]{"Left.Top.Parent.NetAxialPlastic","Right.Top.Parent.NetAxialPlastic","Right.Web.Parent.NetAxialPlastic"})
                Assert.IsTrue(r.CheckSources.Any(s=>s.Component=="CategoryC.NetAxial"&&s.ComponentCheck!.Id==id),id);
        var net=r.Cases[0].AdditionalPlates.NetSections.Checks.Select(x=>x.Id).ToArray();
        CollectionAssert.IsSubsetOf(new[]{"Left.Parent.NetElasticNVM","Right.Parent.NetElasticNVM"},net);
        var welds=r.Cases[0].Parents.LongitudinalWelds.Checks.Select(x=>x.Id).ToArray();
        Assert.IsTrue(welds.Any(x=>x.StartsWith("Left.Top."))&&welds.Any(x=>x.StartsWith("Right.Bottom.")));
    }

    [TestMethod]
    public void EachMemberSectionIsEvaluatedAboutItsOwnCentroid()
    {
        var p=Create(detail:Detail(40,24,30));var c=p.Cases[0].Parents;
        Assert.AreEqual(-50e6,c.LeftSection.Plan.Input.Actions.M1);
        // Right centroid 228.0769...: MR=50e6-(228.0769-210)*(-2e5)... = 50e6+(210-228.0769)*(-2e5)*(-1).
        Close(-53615384.615384616,c.RightSection.Plan.Input.Actions.M1,1e-12);
        Assert.AreSame(p.Cases[0].Parents.RightSection,p.Cases[1].Parents.RightSection);
        var r=WeldedIBeamSpliceStandard.Verify(p);Assert.IsTrue(r.Cases.All(x=>x.Parents.IsBound),All(r));
    }

    [TestMethod]
    public void OneCoverDistributionAloneNeverQualifiesTheJoint()
    {
        foreach(var cat in new[]{SpliceBoltCategory.A,SpliceBoltCategory.C})
        {
            var p=Create(category:cat);
            var only=new WeldedIBeamSpliceStandardInput(p.Cases.Where(c=>c.Id.EndsWith("LeftDistribution")),cat,
                p.Envelope.SlipCases.Where(s=>s.Id.Contains("LeftDistribution")),p.BlockMethod,p.FaceModel,p.PlateStability);
            var r=WeldedIBeamSpliceStandard.Verify(only);
            Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
            Assert.IsTrue(r.MissingRequirements.Contains("ULS+.LeftDistribution:TRANSITION_OPPOSITE_COVER_DISTRIBUTION_CASE_REQUIRED"),All(r));
            if(cat==SpliceBoltCategory.C)Assert.IsTrue(r.MissingRequirements.Contains("ULS+.LeftDistribution.slip:TRANSITION_OPPOSITE_COVER_DISTRIBUTION_CASE_REQUIRED"));
        }
        // A category C slip state must carry the distribution of its linked ULS case.
        var q=Create(category:SpliceBoltCategory.C);
        var swapped=q.Envelope.SlipCases.Select(s=>new WeldedSpliceSlipCase(s.Id,s.Input,
            s.UltimateCaseId!.EndsWith("LeftDistribution")?s.UltimateCaseId.Replace("Left","Right"):s.UltimateCaseId.Replace("Right","Left")));
        var w=WeldedIBeamSpliceStandard.Verify(new WeldedIBeamSpliceStandardInput(q.Cases,SpliceBoltCategory.C,swapped,q.BlockMethod,q.FaceModel));
        Assert.IsTrue(w.MissingRequirements.Any(m=>m.EndsWith("LINKED_ULTIMATE_COVER_DISTRIBUTION_MISMATCH")),All(w));
    }

    [TestMethod]
    public void ManualCasesBindTheRightSectionToItsOwnGeometry()
    {
        var p=Create();
        var cases=p.Cases.Select(c=>new WeldedSpliceStandardCase(c.Id,new WeldedIBeamSpliceParentInput(c.Parents.Detail,c.Parents.LeftSection,
            c.Parents.LeftSection,c.Parents.Welds,c.Parents.ContinuityAndSecondOrderAnalysisSource)));
        var r=WeldedIBeamSpliceStandard.Verify(new WeldedIBeamSpliceStandardInput(cases,p.Envelope.Category,null,p.BlockMethod,p.FaceModel));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Contains("ULS+.LeftDistribution:Right.Section.GEOMETRY_MISMATCH"),All(r));
        Assert.IsTrue(r.MissingRequirements.Contains("ULS+.LeftDistribution:Right.Section.ACTION_OR_AXIS_SIGN_MISMATCH"));
    }

    public static IEnumerable<object[]> Refusals()
    {
        var pack=new WeldedSplicePacking(0,0,0,0,0,null);
        yield return new object[]{"depth",new WeldedSpliceSectionTransition(new(new(400,40,355,510),new(360,16,355,510),new(400,30,355,510),8,8),pack),"TRANSITION_OVERALL_DEPTH_CHANGE_NOT_QUALIFIED"};
        yield return new object[]{"width",new WeldedSpliceSectionTransition(new(new(420,30,355,510),new(360,16,355,510),new(400,30,355,510),8,8),pack),"TRANSITION_FLANGE_WIDTH_CHANGE_NOT_QUALIFIED"};
        yield return new object[]{"vertical offset",new WeldedSpliceSectionTransition(Section(),pack,outerPlaneOffset:5),"TRANSITION_OUTER_FLANGE_PLANE_OFFSET_NOT_QUALIFIED"};
        yield return new object[]{"out-of-plane web",new WeldedSpliceSectionTransition(Section(),pack,webPlaneOffset:2),"TRANSITION_OUT_OF_PLANE_WEB_OFFSET_NOT_QUALIFIED"};
        yield return new object[]{"unpacked flange step",new WeldedSpliceSectionTransition(Section(40),pack),"TRANSITION_TOP_INNER_COVER_PLANE_MISMATCH"};
        yield return new object[]{"unpacked web step",new WeldedSpliceSectionTransition(Section(30,24),pack),"TRANSITION_WEB_COVER_PLANE_MISMATCH"};
        yield return new object[]{"unequal outer packs",new WeldedSpliceSectionTransition(Section(),new WeldedSplicePacking(5,0,0,0,0,"Pack")),"TRANSITION_TOP_OUTER_COVER_PLANE_MISMATCH"};
        yield return new object[]{"right seams",new WeldedSpliceSectionTransition(Section(30,16,30,75),pack),"Right.WEB_COVER_INTERSECTS_LONGITUDINAL_SEAMS"};
    }
    [DataTestMethod,DynamicData(nameof(Refusals),DynamicDataSourceType.Method)]
    public void OtherTransitionsRemainRefusedWithPreciseCodes(string name,WeldedSpliceSectionTransition transition,string code)
    {
        var d=Detail(transition:transition,pack:false);
        var f=WeldedIBeamSpliceForces.Calculate(d);
        Assert.IsFalse(f.IsBalanced,name);Assert.IsTrue(f.Diagnostics.Contains(code),name+": "+string.Join(";",f.Diagnostics));
        var r=WeldedIBeamSpliceStandard.Verify(Create(detail:d));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status,name);
        Assert.IsTrue(r.MissingRequirements.Any(m=>m.EndsWith(":"+code)),name);
    }

    [TestMethod]
    public void ModelAndMethodOptionsOutsideTheTransitionScopeAreRefused()
    {
        var wrong=WeldedIBeamSpliceStandard.Verify(Create(model:SpliceFlangeFaceModel.PackedIndependentFaceDimensions));
        Assert.AreEqual(ConnectionStatus.NotSupported,wrong.Status);
        Assert.IsTrue(wrong.MissingRequirements.Any(m=>m.EndsWith("Model:EXPLICIT_SECTION_TRANSITION_MODEL_REQUIRED")));
        var effective=WeldedIBeamSpliceStandard.Verify(Create(stability:SplicePlateStabilityMethod.EffectiveCompression));
        Assert.AreEqual(ConnectionStatus.NotSupported,effective.Status);
        Assert.IsTrue(effective.MissingRequirements.Any(m=>m.EndsWith("TransitionDomain:TRANSITION_EFFECTIVE_COMPRESSION_NOT_QUALIFIED")),All(effective));
        // The shear-buckling method (claude/conn-blocchi-classe4) is an effective-compression method too.
        var buckling=WeldedIBeamSpliceStandard.Verify(Create(stability:SplicePlateStabilityMethod.EffectiveCompressionWithShearBuckling));
        Assert.AreEqual(ConnectionStatus.NotSupported,buckling.Status);
        Assert.IsTrue(buckling.MissingRequirements.Any(m=>m.EndsWith("TransitionDomain:TRANSITION_EFFECTIVE_COMPRESSION_NOT_QUALIFIED")),All(buckling));
        var identical=WeldedIBeamSpliceStandardTests.Workflow(false,SpliceBoltCategory.A);
        var noTransition=WeldedIBeamSpliceStandard.Verify(new WeldedIBeamSpliceStandardInput(identical.Cases,SpliceBoltCategory.A,null,identical.BlockMethod,SpliceFlangeFaceModel.PackedSectionTransition));
        Assert.AreEqual(ConnectionStatus.NotSupported,noTransition.Status);
        Assert.IsTrue(noTransition.MissingRequirements.Any(m=>m.EndsWith("Model:SECTION_TRANSITION_DETAIL_REQUIRED")));
        // Out-of-plane actions are still rejected, never zeroed.
        Assert.IsTrue(WeldedIBeamSpliceForces.Calculate(Detail(actions:new(1e5,0,1,0,0,0))).Diagnostics.Contains("IN_PLANE_N_V_M_ONLY"));
        Assert.ThrowsException<NotSupportedException>(()=>Create(actions:new[]{new SpliceActionCase("U",new(1e5,0,0,1,0,0),"Torsion")}));
        Assert.ThrowsException<ArgumentNullException>(()=>new WeldedIBeamSpliceForceInput(Section(),Detail().Top,Detail().Bottom,Detail().EachWebCover,
            Detail().WebBolts,60,10,new(0,0,0,0,0,0),null,null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedSpliceSectionTransition(Section(),null!,double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Detail(side:(SpliceSide)7));
    }

    [TestMethod]
    public void HighCoverShearLeavesTheTwoDistributionEnvelopeUnqualified()
    {
        // |V|>0.5 min(Vpl,Vnet) at a web-cover cut: the M-N-V interaction is no longer convex in the mixture.
        var d=Detail(actions:new(0,-1.4e6,0,0,0,0));var t=Transfer(d);
        var check=t.Domain.Checks.First(c=>c.Id=="WebPositive.CoverShearInConvexEnvelopeDomain");
        Assert.IsFalse(check.Passed);Assert.AreEqual(ConnectionCheckRole.Applicability,check.Role);
        var r=WeldedIBeamSpliceStandard.Verify(Create(actions:new[]{new SpliceActionCase("V",new(0,-1.4e6,0,0,0,0),"High shear")}));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Contains("V.LeftDistribution:TransitionDomain:SUFFICIENT_DOMAIN_NOT_ESTABLISHED"),All(r));
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void TransferWeldUsesTheLongJointFactorAndCanFail(bool ntc)
    {
        // 1.5mm throat: L=265>150a=225, betaLw1=1.2-0.2*265/225 (EN1993-1-8 4.11(2)).
        var t=Transfer(Detail(actions:new(1e6,0,0,0,0,0)),ntc,1.5).Transfer;
        Close(1.2-.2*265/225,V(t.Trace,"Transition.Right.Top.BetaLw1"),1e-12);
        Close(510/(.9*1.25)*(1.2-.2*265/225),C(t.Checks,"Right.Top.TransferFillets.DirectionalEquivalent").Resistance,1e-12);
        // An undersized seam is a known failure of the transfer, not an applicability gap.
        var weak=Transfer(Detail(actions:new(1e7,0,0,0,0,0)),ntc,1.5).Transfer;
        Assert.AreEqual(ConnectionStatus.Failed,weak.Status);
        Assert.IsTrue(weak.Checks.Any(c=>!c.Passed&&c.Id.Contains("TransferFillets")));
    }

    [TestMethod]
    public void ZeroTransitionReproducesTheIdenticalRouteExactly()
    {
        var plain=WeldedIBeamSpliceForces.Calculate(BridgeSpliceUnequalWidthTests.Detail(360) is var u
            ?new WeldedIBeamSpliceForceInput(Section(),u.Top,u.Bottom,u.EachWebCover,u.WebBolts,u.WebCoverEnd,u.ButtGap,u.Actions):null!);
        foreach(var side in new[]{SpliceSide.Left,SpliceSide.Right})
        {
            var d=Detail(30,16,30,side:side);var f=WeldedIBeamSpliceForces.Calculate(d);
            for(int i=0;i<plain.CoverGroups.Count;i++)
                CollectionAssert.AreEqual(plain.CoverGroups[i].Distribution.Forces.Select(b=>b.Fx).ToArray(),f.CoverGroups[i].Distribution.Forces.Select(b=>b.Fx).ToArray());
            var t=Transfer(d).Transfer;string m=(side==SpliceSide.Left?"Right":"Left")+".";
            Assert.AreEqual(0,V(t.Trace,"Transition."+m+"Top.TransferFromWeb"));Assert.AreEqual(0,V(t.Trace,"Transition."+m+"Bottom.TransferFromWeb"));
        }
        var identical=WeldedIBeamSpliceBolts.Verify(WeldedIBeamSpliceStandardTests.Input(false,new WeldedIBeamSpliceForceInput(Section(),Detail().Top,Detail().Bottom,
            Detail().EachWebCover,Detail().WebBolts,60,10,new(2e5,-1e5,0,0,0,50e6))).Plates.Bolts).ComponentAssessment;
        var zero=WeldedIBeamSpliceBolts.Verify(WeldedIBeamSpliceStandardTests.Input(false,Detail(30,16,30)).Plates.Bolts).ComponentAssessment;
        CollectionAssert.AreEqual(identical.Checks.Select(c=>c.Id+"="+c.Demand+"/"+c.Resistance).ToArray(),zero.Checks.Select(c=>c.Id+"="+c.Demand+"/"+c.Resistance).ToArray());
    }

    [TestMethod]
    public void SlipUsesThePerMemberSerialInterfaces()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Create(category:SpliceBoltCategory.C));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,All(r));
        var slip=r.SlipCases.First().Assessment.Checks.Select(c=>c.Id).ToArray();
        // Left inner strips have a 10mm pack: two serial interfaces; the right ones bear directly on the 40mm flange.
        Assert.IsTrue(slip.Contains("Left.TopInnerNegative.Bolt0.CoverToPacking.Slip.Ultimate"));
        Assert.IsTrue(slip.Contains("Left.TopInnerNegative.Bolt0.PackingToParent.Slip.Ultimate"));
        Assert.IsTrue(slip.Contains("Right.TopInnerNegative.Bolt0.Slip.Ultimate"));
        Assert.IsFalse(slip.Any(x=>x.StartsWith("Right.TopInnerNegative.Bolt0.PackingToParent")));
        var wrong=WeldedIBeamSpliceStandard.Verify(Create(category:SpliceBoltCategory.C,rightExtra:1));
        Assert.AreEqual(ConnectionStatus.NotSupported,wrong.Status);
        Assert.IsTrue(wrong.MissingRequirements.Any(m=>m.EndsWith("PACKING_FRICTION_INTERFACES_MISMATCH")));
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReportCarriesBothSectionsBothDistributionsAndTheTransfer(bool ntc)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Create(ntc,SpliceBoltCategory.C,Detail(40,24,30)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,All(r));
        var report=BridgeSpliceReport.FromBolted(r);var text=report.ToText();
        foreach(var x in new[]{"Right.Parent.Top.Thickness","Transition.CoverDistribution","coverDistribution=Right;redistributingMember=Left",
            "Transition.Left.Top.TransferFromWeb","TransitionDomain","SectionTransfer","TopInnerNegative:Right:mu=","PackedSectionTransition"})
            StringAssert.Contains(text,x);
        Assert.IsTrue(report.Rows.Any(x=>x.Component=="SectionTransfer"&&x.Role==SpliceReportRole.Resistance));
        Assert.IsTrue(report.Rows.Any(x=>x.Component=="TransitionDomain"&&x.Role==SpliceReportRole.Applicability));
        // Overload at low shear: a known failure inside the qualified envelope, not an applicability gap.
        var overload=WeldedIBeamSpliceStandard.Verify(Create(ntc,SpliceBoltCategory.A,Detail(40,24,30),
            actions:new[]{new SpliceActionCase("O",new(8e6,-1e5,0,0,0,2e9),"Overload")}));
        Assert.AreEqual(ConnectionStatus.Failed,overload.Status,All(overload));
    }
}
