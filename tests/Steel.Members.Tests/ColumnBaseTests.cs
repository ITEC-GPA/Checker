using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Standard column base (ColumnBase.Verify). Expected values are hand calculations written out in
/// the comments (EN1993-1-8:2005 6.2.2, 6.2.5, 6.2.6.11-12, Table6.2, Table6.6 outer row, Table6.7;
/// EN1992-1-1:2004 6.7), never values read back from the engine.</summary>
[TestClass]
public class ColumnBaseTests
{
    // HEB240 (h240 b240 tw10 tf17 r21), S275 with EN1993-1-1 Table3.1 values for t<=40 mm: fy=275, fu=430.
    // Wpl,y=1053 cm3 (catalogue). Mc,Rd=Wpl fy/gammaM0, class 1, V2<0.5Vpl (Av=3324 mm2, Vpl=527.7 kN).
    private static ConnectionRolledISection Heb240() => new(240, 240, 10, 17, 21, 2 * 240 * 17 + (240 - 34) * 10 + 4 * (1 - Math.PI / 4) * 21 * 21, 275, 430);

    private sealed record Spec
    {
        public bool Ntc { get; init; }
        public ColumnBaseDetail Detail { get; init; } = ColumnBaseDetail.UnstiffenedExposedPlate;
        public ColumnBaseDesignSituation Situation { get; init; } = ColumnBaseDesignSituation.OrdinaryStatic;
        public ConnectionRolledISection? Column { get; init; }
        public double PlateWidth { get; init; } = 400;
        public double PlateLength { get; init; } = 440;
        public double PlateThickness { get; init; } = 30;
        public double PlateFy { get; init; } = 275;
        public double PlateFu { get; init; } = 430;
        public double Diameter { get; init; } = 24;
        public double TensileArea { get; init; } = 353;
        public double Hole { get; init; } = 26;
        public double Fyb { get; init; } = 300;
        public double Fub { get; init; } = 500;
        public bool ThreadsEn1090 { get; init; } = true;
        public double Washer { get; init; } = 4;
        public double Nut { get; init; } = 21.5;
        // ISO4032 M24: s=36, e=39.55, dm=(36+39.55)/2 rounded down to 37.7.
        public double Dm { get; init; } = 37.7;
        public ColumnBaseAnchorage Anchorage { get; init; } = ColumnBaseAnchorage.WasherPlate;
        public string? AnchorSource { get; init; } = "EN ISO 898-1 class 5.6, rolled threads to EN1090-2";
        public string? HoleServiceability { get; init; }
        public ColumnBaseAnchorLayout Layout { get; init; } = ColumnBaseAnchorLayout.OutsideFlanges;
        public double Gauge { get; init; } = 160;
        public double Row { get; init; } = 170;
        public string? PatternSource { get; init; } = "Base plate drawing BP-01";
        public double Fck { get; init; } = 25;
        public double AlphaCC { get; init; } = 1.0;
        public double GammaC { get; init; } = 1.5;
        public double? Ac1 { get; init; }
        public double Grout { get; init; } = 30;
        public double GroutFck { get; init; } = 35;
        public ColumnBaseGrout GroutType { get; init; } = ColumnBaseGrout.SandCementMortar;
        public double? Friction { get; init; }
        public string? FrictionSource { get; init; }
        public string? Ac1Source { get; init; } = "Block 1000x1000x800: concentric 800x880 (factor 2) within edges and depth, EN1992-1-1 6.7(3); uniform load, no high shear";
        public double? ConcreteTension { get; init; } = 110e3;
        public double? ConcreteShear { get; init; } = 60e3;
        public string? ConcreteSource { get; init; } = "EN1992-4 calculation AN-01 (cone, pull-out, splitting, edge, pry-out) for this group";
        public double FlangeLeg { get; init; } = 12;
        public double WebLeg { get; init; } = 8;
        public string? Consumable { get; init; } = "G 42 4 M21 3Si1, matching S275";
        public string? Eccentricity { get; init; } = "Fillets all round, symmetric about both axes";
        public double? Mc { get; init; }
        public double? McShear { get; init; }
        public string? McSource { get; init; } = "HEB240 S275 class 1, Wpl fy/gammaM0, V2<0.5Vpl";
        public double N { get; init; } = -600e3;
        public double V1 { get; init; }
        public double V2 { get; init; } = 150e3;
        public double T { get; init; }
        public double M1 { get; init; } = 120e6;
        public double M2 { get; init; }
        public string? Combination { get; init; } = "ULS STR combination 6.10, case 12";

        public ColumnBaseInput Build()
        {
            var code = Ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1.0, 1.25, 1.25, "EN1993-1-8 recommended factors");
            var column = Column ?? Heb240();
            double mc = Mc ?? 1053e3 * 275 / code.Plates.GammaM0;
            return new ColumnBaseInput(code, Detail, Situation, column, 430, new ConnectionPlate(PlateWidth, PlateThickness, PlateFy, PlateFu), PlateLength,
                new ColumnBaseAnchor(Diameter, TensileArea, Hole, Fyb, Fub, ThreadsEn1090, Washer, Nut, Dm, Anchorage, AnchorSource!, HoleServiceability),
                new ColumnBaseAnchorPattern(Layout, Gauge, Row, PatternSource!),
                new ColumnBaseFoundation(Fck, AlphaCC, GammaC, Ac1 ?? 4 * PlateWidth * PlateLength, Grout, GroutFck, GroutType, Ac1Source!, Friction, FrictionSource),
                new ColumnBaseAnchorConcreteResistance(ConcreteTension, ConcreteShear, ConcreteSource!),
                new ColumnBaseWelds(FlangeLeg, WebLeg, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize,
                    FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275, Consumable!, Eccentricity!),
                new ColumnBaseColumnMomentResistance(code, column, mc, McShear ?? Math.Abs(V2), McSource!),
                new SteelActions(N, V1, V2, T, M1, M2), Combination!);
        }
    }

    /// <summary>Example 1, EC: N=-600 kN, M1=120 kNm, V2=150 kN; four M24 5.6 outside the flanges.</summary>
    private static Spec Example1() => new();

    /// <summary>Example 2, NTC2018: N=+50 kN, M1=25 kNm, V2=20 kN; 15 mm plate, four M20 8.8, prying develops.</summary>
    private static Spec Example2() => new()
    {
        Ntc = true, PlateThickness = 15, Diameter = 20, TensileArea = 245, Hole = 22, Fyb = 640, Fub = 800, Washer = 3, Nut = 18,
        // ISO4032 M20: s=30, e=32.95, dm=31.475 rounded down to 31.4.
        Dm = 31.4, AnchorSource = "EN ISO 898-1 class 8.8 rods, rolled threads", HoleServiceability = "SLE settlement check SLE-03 for 2 mm clearance",
        Row = 180, AlphaCC = .85, GammaC = 1.5, ConcreteTension = 90e3, ConcreteShear = 50e3, N = 50e3, M1 = 25e6, V2 = 20e3
    };

    /// <summary>Example 3, EC: nominally pinned base, two M20 4.6 between the flanges on the major axis, N=-500 kN, V1=30, V2=40 kN.</summary>
    private static Spec Example3() => new()
    {
        PlateWidth = 300, PlateLength = 300, PlateThickness = 20, Diameter = 20, TensileArea = 245, Hole = 22, Fyb = 240, Fub = 400, Washer = 3, Nut = 18, Dm = 31.4,
        AnchorSource = "EN ISO 898-1 class 4.6", Layout = ColumnBaseAnchorLayout.BetweenFlanges, Gauge = 100, Row = 0, Ac1 = 300 * 300, Grout = 25,
        Ac1Source = "No spreading assumed: Ac1=Ac0", ConcreteTension = null, ConcreteShear = null, ConcreteSource = null,
        FlangeLeg = 6, WebLeg = 6, N = -500e3, V1 = 30e3, V2 = 40e3, M1 = 0, McShear = 40e3
    };

    private static ConnectionCheck Check(ColumnBaseResult r, string id) => r.Checks.Single(c => c.Id == id);
    private static double Trace(ColumnBaseResult r, string id) => r.Trace.Single(v => v.Name == id).Value;
    private static void Close(double expected, double actual, string what) => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), what);

    [TestMethod]
    public void Example1CompressionAndMomentMatchesTheHandCalculation()
    {
        // Concrete (EC, alpha_cc=1, gamma_c=1.5): fcd=25/1.5=16.6667 MPa. Footprint 400x440=176000 mm2, Ac1=704000:
        // sqrt(Ac1/Ac0)=2<=3, FRdu=176000*16.6667*2=5866666.7 N (EN1992-1-1 (6.63)); fjd=2/3*FRdu/Ac0=22.2222 MPa (6.6),
        // grout 30 mm <= 0.2*400 and 35 >= 0.2*25 MPa. c=30*sqrt(275/(3*22.2222*1.0))=60.93029 mm (6.5).
        // Flange T-stub: outwards min(c,(440-240)/2=100)=60.930, inwards min(c,(240-34)/2=103)=60.930, along X min(120+c,200)=180.930:
        // y from 103-60.930=42.070 to 120+60.930=180.930, area 361.861*138.861=50248.17 mm2; the two holes d0=26 at y=170 lie inside
        // and are deducted: 2*pi*13^2=1061.86 -> 49186.31 mm2, centroid (50248.17*111.5-1061.86*170)/49186.31=110.23707 mm = zC.
        // Fc,pl,Rd=22.2222*49186.31=1093029.1 N; Fc,fc,Rd=289.575e6/(240-17)=1298542.6 N; FC=1093029.1 N.
        // Tension T-stub (Table6.6 outer row): mx=50-0.8*12=40.4, e=(400-160)/2=120, ex=220-170=50, w=160.
        // Circular min(2pi mx=253.84, pi mx+w=286.92, pi mx+2e=366.92)=253.84; non-circular min(4mx+1.25ex=224.10,
        // e+2mx+0.625ex=232.05, bp/2=200, w/2+2mx+0.625ex=192.05)=192.05 -> leff1=leff2=192.05; n=min(ex,1.25mx)=50.
        // Mpl,1=0.25*192.05*30^2*275/1.0=11883093.75 Nmm; plate 2Mpl,1/m=588271.97 N.
        // Anchor M24 5.6: Ft,Rd=0.9*500*353/1.25=127080 N; Bp,Rd=0.6*pi*37.7*30*430/1.25=733368.4 N; declared concrete 110000 N
        // -> 110000 N per anchor, sum Ft=220000 N. Lb=8*24+30+30+4+21.5/2=266.75 mm; Lb*=8.8*40.4^3*353*1/(192.05*30^3)=39.502 mm:
        // Lb>Lb*, no prying: FT=min(588271.97, 220000)=220000 N. zT=170 mm.
        // Equilibrium, side A (+Y) in tension, side B in compression, z=170+110.23707=280.23707:
        // TA=(N zC+M)/z=(-600000*110.23707+120e6)/z=192186.41 N; CB=(M-N zT)/z=(120e6+600000*170)/z=792186.41 N.
        // u=max(192186.41/220000=0.873575, 792186.41/1093029.1=0.724762); Mj,Rd=M/u=137366635 Nmm, the Table6.7 row-1 value
        // min(FT z/(zC/e+1), -FC z/(zT/e-1)) with e=M/N=-200 mm.
        var r = ColumnBase.Verify(Example1().Build());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete && r.CanQualifyWholeJoint);
        Assert.AreEqual(ColumnBaseEquilibriumState.SideATensionSideBCompression, r.State);
        Close(22.222222222222222, r.BearingStrength!.Value, "fjd"); Close(60.9302880347697, r.AdditionalBearingWidth!.Value, "c");
        Close(49186.30973295829, Trace(r, "Compression.FlangeTStub.EffectiveArea"), "Aeff"); Assert.AreEqual(2, Trace(r, "Compression.FlangeTStub.DeductedHoles"));
        Close(110.23707314338716, r.CompressionLeverArm!.Value, "zC"); Close(170, r.TensionLeverArm!.Value, "zT");
        Close(1093029.105176851, r.SideCompressionResistance!.Value, "FC"); Close(1298542.600896861, Trace(r, "Compression.FcFcRd"), "Fc,fc");
        Close(11883093.75, Trace(r, "Tension.TStub.Mpl1"), "Mpl1"); Close(39.50239225695469, Trace(r, "Tension.Anchor.PryingLengthLimit"), "Lb*");
        Close(266.75, Trace(r, "Tension.Anchor.AnchorElongationLength"), "Lb"); Assert.IsFalse(r.PryingDevelops!.Value);
        Close(220000, r.SideTensionResistance!.Value, "FT");
        Assert.IsFalse(r.Checks.Any(c => c.Id.EndsWith(".Mode2")), "No Mode2 without prying");
        Close(588271.9678217822, Check(r, "SideA.TStub.PlateWithoutPrying").Resistance, "plate");
        Close(192186.40670861787, Check(r, "SideA.TStub.Mode3").Demand, "TA"); Close(220000, Check(r, "SideA.TStub.Mode3").Resistance, "Mode3");
        Close(792186.4067086178, Check(r, "SideB.ConcreteAndPlate").Demand, "CB");
        Close(137366635.0920759, r.MomentResistance!.Value, "Mj,Rd"); Close(-686833.1754603796, r.AxialResistance!.Value, "Nj,Rd");
        // Shear: Ff=0.2*600000=120000 N < 150000 N, so the two compression-side anchors are credited (normal 2 mm holes, fyb=300).
        // F1=0.5*500*353/1.25=70600; alpha_bc=0.44-0.0003*300=0.35, F2=0.35*500*353/1.25=49420 N (6.2); bearing X: alpha_d=min(120/78,
        // 160/78-0.25)=1.538, alpha_b=1, k1=min(2.5, 2.8*50/26-1.7, 1.4*340/26-1.7)=2.5 -> 619200 N; bearing Y: alpha_d=min(50/78,
        // 340/78-0.25)=0.6410, k1=2.5 -> 396923 N; concrete 60000 N. Fvb=49420 N; Fv,Rd=120000+2*49420=218840 N (6.3).
        Close(49420, Trace(r, "Shear.Anchor.FvbRd"), "Fvb"); Close(396923.07692307694, Trace(r, "Shear.Anchor.PlateBearingY"), "Fb,y");
        Assert.AreEqual(2, Trace(r, "Shear.CreditedAnchors")); Close(218840, r.ShearResistance!.Value, "Fv,Rd");
        Close(150000 / 218840.0, Check(r, "Shear.FrictionAndAnchors").Utilization, "shear");
        // Welds: throats 12/sqrt2 (flanges) and 8/sqrt2 (web); runs 2x240 outer, 4x94 inner, 2x164 web: A=9118.849 mm2,
        // Ix=2*240*a_f*120^2+4*94*a_f*103^2+2*a_w*164^3/12=96656594 mm4. Outer -Y fillet: sz=-600000/A-120e6*120/Ix=-214.779 MPa,
        // V2 gives 150000/A=16.449 MPa towards the member face: sigma=(16.449-214.779)/sqrt2=-140.240, tau_perp=(-16.449-214.779)/sqrt2=-163.503,
        // sqrt(sigma^2+3tau^2)=316.017 <= 430/(0.85*1.25)=404.706 MPa: 0.780857. At N/u, M/u (6.2.3(4)) the same point gives 0.889281.
        Close(0.7808570922658625, Check(r, "Welds.ActualActions").Demand, "welds");
        var atResistance = Check(r, "Welds.NotLimitingJointMomentResistance");
        Assert.AreEqual(ConnectionCheckRole.Detailing, atResistance.Role); Close(0.8892809963335758, atResistance.Demand, "welds at Mj,Rd");
        Close(192186.40670861787 / 220000, r.Utilization!.Value, "governing");
    }

    [TestMethod]
    public void Example2TensionAndMomentWithPryingMatchesTheHandCalculation()
    {
        // NTC2018: gammaM0=1.05, gammaM2=1.25; fcd=0.85*25/1.5=14.1667 MPa (4.1.2.1.1.1), fjd=2/3*2*14.1667=18.8889 MPa.
        // c=15*sqrt(275/(3*18.8889*1.05))=32.24773 mm. Flange T-stub y 70.752..152.248, x +/-152.248: 24814.996 mm2; the holes
        // at y=180 (r=11) do not reach it; centroid 120-17/2=111.5 mm. Fc,pl,Rd=468727.70 N; Fc,fc,Rd=(1053e3*275/1.05)/223=1236707.2 N.
        // T-stub: mx=60-9.6=50.4, e=120, ex=220-180=40: circular min(316.67, 318.34, 398.34)=316.67; non-circular
        // min(251.6, 245.8, 200, 205.8)=200 -> leff1=leff2=200; n=min(40, 63)=40; Mpl=0.25*200*15^2*275/1.05=2946428.57 Nmm.
        // M20 8.8: Ft,Rd=0.9*800*245/1.25=141120 N, Bp,Rd=0.6*pi*31.4*15*430/1.25=305408.0 N, concrete 90000 N -> sum Ft=180000 N.
        // Lb=8*20+30+15+3+18/2=217 mm <= Lb*=8.8*50.4^3*245/(200*15^3)=408.918 mm: prying develops.
        // EN1993-1-8 6.2.6.11(2): the plate keeps 2Mpl,1/m=116921.77 N; the anchors with prying: Mode2=(2*2946428.57+40*180000)/90.4=144832.49 N,
        // Mode3=180000 N. FT=116921.77 N. (Table6.2 read literally with prying would give Mode1=4Mpl/m=233843.5 N and FT=Mode2.)
        // State A tension / B compression, z=180+111.5=291.5: TA=(50000*111.5+25e6)/291.5=104888.51 N, CB=(25e6-50000*180)/291.5=54888.51 N.
        // u=104888.51/116921.77=0.897083; Mj,Rd=25e6/u=27868107.6 Nmm = Table6.7 row 1 with e=500 mm.
        var r = ColumnBase.Verify(Example2().Build());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Assert.AreEqual(ColumnBaseEquilibriumState.SideATensionSideBCompression, r.State);
        Close(18.888888888888886, r.BearingStrength!.Value, "fjd"); Close(32.247728081006805, r.AdditionalBearingWidth!.Value, "c");
        Close(111.5, r.CompressionLeverArm!.Value, "zC"); Assert.AreEqual(0, Trace(r, "Compression.FlangeTStub.DeductedHoles"));
        Close(468727.70409569243, r.SideCompressionResistance!.Value, "FC");
        Assert.IsTrue(r.PryingDevelops!.Value); Close(217, Trace(r, "Tension.Anchor.AnchorElongationLength"), "Lb");
        Close(408.91834367999996, Trace(r, "Tension.Anchor.PryingLengthLimit"), "Lb*");
        Close(305408.04486313957, Trace(r, "Tension.Anchor.PlatePunchingResistance"), "Bp");
        Close(116921.76870748299, Check(r, "SideA.TStub.PlateWithoutPrying").Resistance, "plate");
        Close(144832.4905183312, Check(r, "SideA.TStub.Mode2").Resistance, "Mode2"); Close(180000, Check(r, "SideA.TStub.Mode3").Resistance, "Mode3");
        Close(104888.5077186964, Check(r, "SideA.TStub.Mode2").Demand, "TA"); Close(54888.5077186964, Check(r, "SideB.ConcreteAndPlate").Demand, "CB");
        Close(27868107.586452402, r.MomentResistance!.Value, "Mj,Rd");
        // Shear with N>0: Ff=0; the two compression-side anchors carry V2=20 kN (NTC hole clearance with its SLE evidence).
        // F2=(0.44-0.0003*640)*800*245/1.25=38886.4 N; NTC bearing (no p2 term) X 258000, Y 2.5*0.60606*430*20*15/1.25=156363.6 N;
        // concrete 50000; F1=78400. Fv,Rd=2*38886.4=77772.8 N.
        Close(0, Trace(r, "Shear.FfRd"), "Ff"); Close(156363.63636363635, Trace(r, "Shear.Anchor.PlateBearingY"), "Fb,y NTC");
        Close(77772.80000000002, r.ShearResistance!.Value, "Fv,Rd");
        // Welds: outer +Y fillet sz=50000/A+25e6*120/Ix=36.521 MPa: sqrt(24.273^2+3*27.375^2)=53.267 MPa -> 0.131619.
        Close(0.1316189033006049, Check(r, "Welds.ActualActions").Demand, "welds");
        Close(104888.5077186964 / 116921.76870748299, r.Utilization!.Value, "governing");
    }

    [TestMethod]
    public void Example3PinnedBaseUsesTheThreeNonOverlappingTStubs()
    {
        // EC, Ac1=Ac0: fjd=2/3*16.6667=11.1111 MPa; c=20*sqrt(275/(3*11.1111))=57.44563 mm.
        // Flange T-stubs: outwards min(c,30)=30, inwards 57.446, along X min(120+c,150)=150: y 45.554..150, 300*104.446=31333.69 mm2 each.
        // Web T-stub between their inner edges: |y|<=45.554, x within min(5+c,150)=62.446: 124.891*91.109=11378.69 mm2, minus the two
        // holes d0=22 at (+/-50,0): 10618.42 mm2. Nj,Rd=11.1111*(2*31333.69+10618.42)=814286.62 N (6.2.8.2); u=500000/814286.62=0.614034.
        // Shear: Ff=0.2*500000=100000 N >= sqrt(30^2+40^2)=50 kN; anchors not declared in shear: friction only, u=0.5.
        // Welds 6/6 (a=4.2426): A=5023.287 mm2, uniform sz=-99.536 MPa, sx=5.972, sy=7.963 MPa; outer -Y fillet sigma=(7.963-99.536)/sqrt2,
        // tau_perp=(-7.963-99.536)/sqrt2, tau_par=5.972: 147.085 MPa -> 0.363437.
        var r = ColumnBase.Verify(Example3().Build());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Assert.AreEqual(ColumnBaseEquilibriumState.AxialCompressionOnly, r.State);
        Close(57.44562646538029, r.AdditionalBearingWidth!.Value, "c");
        Close(10618.420152260342, Trace(r, "Compression.WebTStub.EffectiveArea"), "web T-stub");
        Close(814286.6225720946, Check(r, "Axial.ThreeTStubs").Resistance, "Nj,Rd"); Close(-814286.6225720946, r.AxialResistance!.Value, "Nj signed");
        Assert.AreEqual(0, Trace(r, "Shear.CreditedAnchors")); Close(0.5, Check(r, "Shear.FrictionAndAnchors").Utilization, "friction");
        Close(0.3634369468023925, Check(r, "Welds.ActualActions").Demand, "welds");
        Assert.IsFalse(r.Checks.Any(c => c.Id.StartsWith("Welds.NotLimiting")), "6.2.3(4) concerns the moment resistance only");
        Close(0.6140344028011235, r.Utilization!.Value, "governing");
    }

    [TestMethod]
    public void MomentResistanceReproducesEveryRowOfTable67()
    {
        // Example 1 geometry: FT=220000, FC=1093029.1, zT=170, zC=110.23707. Table6.7 (MEd>0 tensions the left side A, NEd>0 tension):
        // row1 LT/RC z=zT+zC: N>0,e>zT or N<=0,e<=-zC: min(FT z/(zC/e+1), -FC z/(zT/e-1)); row2 LT/RT z=2zT: N>0, -zT<e<zT:
        // min(FT z/(zT/e+1), FT z/(zT/e-1)); row3 LC/RT z=zC+zT: N>0,e<=-zT or N<=0,e>zC: min(-FC z/(zT/e+1), FT z/(zC/e-1));
        // row4 LC/RC z=2zC: N<=0, -zC<e<zC: min(-FC z/(zC/e+1), -FC z/(zC/e-1)); "min" by magnitude.
        const double ft = 220000, zt = 170; double fc = 1093029.105176851, zc = 110.23707314338716;
        foreach (double n in new[] { 200e3, -200e3, -900e3 })
            foreach (double e in new[] { -2000, -400, -150, -100, -40, 40, 100, 150, 400, 2000 })
            {
                double mExpected;
                if (n > 0 && e > zt || n <= 0 && e <= -zc) mExpected = Smaller(ft * (zt + zc) / (zc / e + 1), -fc * (zt + zc) / (zt / e - 1));
                else if (n > 0 && e > -zt && e < zt) mExpected = Smaller(ft * 2 * zt / (zt / e + 1), ft * 2 * zt / (zt / e - 1));
                else if (n > 0 && e <= -zt || n <= 0 && e > zc) mExpected = Smaller(-fc * (zc + zt) / (zt / e + 1), ft * (zc + zt) / (zc / e - 1));
                else mExpected = Smaller(-fc * 2 * zc / (zc / e + 1), -fc * 2 * zc / (zc / e - 1));
                var r = ColumnBase.Verify((Example1() with { N = n, M1 = n * e, V2 = 0 }).Build());
                Assert.AreEqual(0, r.MissingRequirements.Count, string.Join(";", r.MissingRequirements));
                Assert.AreEqual(mExpected, r.MomentResistance!.Value, 1e-9 * Math.Abs(mExpected), $"N={n} e={e}");
                Assert.AreEqual(n * e > 0 ? 1 : -1, Math.Sign(r.MomentResistance.Value));
            }
        static double Smaller(double a, double b) => Math.Abs(a) <= Math.Abs(b) ? a : b;
    }

    [TestMethod]
    public void PureTensionLoadsBothRowsAndCreditsNoAnchorInShear()
    {
        // N=+200 kN, M1=0: TA=TB=100000 N <= FT=220000 (u=0.4545). V=0, Ff=0, no anchor free of tension.
        var r = ColumnBase.Verify((Example1() with { N = 200e3, M1 = 0, V2 = 0 }).Build());
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Assert.AreEqual(ColumnBaseEquilibriumState.BothSidesInTension, r.State);
        Close(100000, Check(r, "SideA.TStub.Mode3").Demand, "TA"); Close(100000, Check(r, "SideB.TStub.Mode3").Demand, "TB");
        Assert.AreEqual(0, Trace(r, "Shear.CreditedAnchors")); Close(100000 / 220000.0, r.Utilization!.Value, "u");
        // The same tension with a shear: no friction and no tension-free anchor; interaction is outside the method.
        var sheared = ColumnBase.Verify((Example1() with { N = 200e3, M1 = 0, V2 = 10e3 }).Build());
        Assert.AreEqual(ConnectionStatus.NotSupported, sheared.Status);
        CollectionAssert.Contains(sheared.MissingRequirements.ToArray(), "SHEAR_EXCEEDS_RESISTANCE_WITHOUT_TENSIONED_ANCHORS:TENSION_SHEAR_INTERACTION_NOT_IMPLEMENTED");
        Assert.IsNull(sheared.Utilization); Assert.IsNull(sheared.MomentResistance);
    }

    [TestMethod]
    public void SmallMomentUnderHighCompressionKeepsBothFlangesCompressed()
    {
        // N=-1000 kN, M1=20 kNm: e=-20 mm, |e|<zC. Row 4, z=2zC=220.47415: CA=-(N zC+M)/z=409284.55, CB=(M-N zC)/z=590715.45 N;
        // FC=1093029.1 -> u=590715.45/1093029.1=0.540439. All four anchors are free of tension: Fv,Rd=200000+4*49420=397680 N.
        var r = ColumnBase.Verify((Example1() with { N = -1000e3, M1 = 20e6 }).Build());
        Assert.AreEqual(ColumnBaseEquilibriumState.BothSidesInCompression, r.State, string.Join(";", r.MissingRequirements));
        double z = 2 * 110.23707314338716, ca = -(-1000e3 * 110.23707314338716 + 20e6) / z, cb = (20e6 + 1000e3 * 110.23707314338716) / z;
        Close(ca, Check(r, "SideA.ConcreteAndPlate").Demand, "CA"); Close(cb, Check(r, "SideB.ConcreteAndPlate").Demand, "CB");
        Assert.AreEqual(4, Trace(r, "Shear.CreditedAnchors")); Close(397680, r.ShearResistance!.Value, "Fv");
        Assert.IsFalse(r.Checks.Any(c => c.Id.Contains("TStub")));
    }

    [TestMethod]
    public void OverloadIsAVisibleFailure()
    {
        // M1=200 kNm: TA=(-66142243.9+200e6)/280.237=477666 N > FT=220000 N.
        var r = ColumnBase.Verify((Example1() with { M1 = 200e6 }).Build());
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.HasFailedComponent);
        Assert.IsFalse(Check(r, "SideA.TStub.Mode3").Passed); Assert.IsTrue(r.Utilization > 1);
    }

    [TestMethod]
    public void UndersizedWeldsThatWouldLimitMjRdFailTheDetailingRule()
    {
        // Flange leg 10 mm: at the actions 0.919448 (passes); at N/u, M1/u 1.047238 > 1, contrary to EN1993-1-8 6.2.3(4).
        var r = ColumnBase.Verify((Example1() with { FlangeLeg = 10 }).Build());
        Close(0.9194483311237192, Check(r, "Welds.ActualActions").Demand, "actual"); Assert.IsTrue(Check(r, "Welds.ActualActions").Passed);
        var rule = Check(r, "Welds.NotLimitingJointMomentResistance");
        Close(1.0472378275226526, rule.Demand, "at resistance"); Assert.IsFalse(rule.Passed); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
    }

    [TestMethod]
    public void ShearBeyondFrictionNeedsAdmissibleAnchors()
    {
        // Oversized 30 mm holes (6.2.2(5)): only friction, 120 kN < 150 kN: a missing requirement (shear key out of scope), not a pass.
        var oversized = ColumnBase.Verify((Example1() with { Hole = 30 }).Build());
        Assert.AreEqual(ConnectionStatus.NotSupported, oversized.Status);
        Assert.IsTrue(oversized.MissingRequirements.Any(m => m.StartsWith("SHEAR_EXCEEDS_FRICTION:") && m.Contains("OVERSIZED_HOLES")));
        // Concrete shear resistance not declared: same outcome, never an assumed value.
        var undeclared = ColumnBase.Verify((Example1() with { ConcreteShear = null }).Build());
        Assert.IsTrue(undeclared.MissingRequirements.Any(m => m.Contains("ANCHOR_CONCRETE_SHEAR_RESISTANCE_NOT_DECLARED")));
        // fyb=700 > 640 (6.2.2(7), 3.3(1)).
        var strong = ColumnBase.Verify((Example1() with { Fyb = 700, Fub = 800 }).Build());
        Assert.IsTrue(strong.MissingRequirements.Any(m => m.Contains("ANCHOR_FYB_OUTSIDE_235_TO_640_MPA")));
        // All four anchors credited and still insufficient: a failure. N=-1000, M1=20 kNm, V2=450 kN > 397680 N.
        var failed = ColumnBase.Verify((Example1() with { N = -1000e3, M1 = 20e6, V2 = 450e3 }).Build());
        Assert.AreEqual(ConnectionStatus.Failed, failed.Status); Assert.IsFalse(Check(failed, "Shear.FrictionAndAnchors").Passed);
        // Friction alone sufficient although anchors are not admissible: complete.
        var friction = ColumnBase.Verify((Example1() with { Hole = 30, V2 = 100e3 }).Build());
        Assert.AreEqual(ConnectionStatus.Passed, friction.Status, string.Join(";", friction.MissingRequirements));
        Assert.AreEqual(0, Trace(friction, "Shear.CreditedAnchors"));
    }

    [DataTestMethod]
    [DataRow("stiffened", "STIFFENED_BASE_PLATE_OUT_OF_SCOPE")]
    [DataRow("embedded", "EMBEDDED_COLUMN_BASE_OUT_OF_SCOPE")]
    [DataRow("hollow", "HOLLOW_SECTION_COLUMN_OUT_OF_SCOPE")]
    [DataRow("seismic", "SEISMIC_CAPACITY_DESIGN_OUT_OF_SCOPE")]
    [DataRow("fatigue", "FATIGUE_OUT_OF_SCOPE")]
    [DataRow("minor", "MINOR_AXIS_OR_BIAXIAL_BENDING_OUT_OF_SCOPE")]
    [DataRow("biaxial", "MINOR_AXIS_OR_BIAXIAL_BENDING_OUT_OF_SCOPE")]
    [DataRow("torsion", "TORSION_OUT_OF_SCOPE")]
    [DataRow("combination", "ACTION_COMBINATION_SOURCE_REQUIRED")]
    [DataRow("pattern", "ANCHOR_PATTERN_DETAIL_SOURCE_REQUIRED")]
    [DataRow("material", "ANCHOR_MATERIAL_SOURCE_REQUIRED")]
    [DataRow("ac1source", "CONCENTRATED_RESISTANCE_AC1_SOURCE_REQUIRED")]
    [DataRow("mcsource", "COLUMN_MOMENT_RESISTANCE_SOURCE_REQUIRED")]
    [DataRow("mcshear", "COLUMN_MOMENT_RESISTANCE_MUST_USE_SAME_CODE_COLUMN_AND_CONCOMITANT_V2")]
    [DataRow("s460", "CARBON_STEEL_UP_TO_S460_ONLY")]
    [DataRow("grade", "YIELD_STRENGTH_EXCEEDS_DECLARED_NOMINAL_GRADE")]
    [DataRow("deep", "COLUMN_DEEPER_THAN_600MM_REQUIRES_WEB_CONTRIBUTION_LIMIT")]
    [DataRow("cover", "PLATE_MUST_COVER_THE_COLUMN_FOOTPRINT")]
    [DataRow("fyb900", "ANCHOR_YIELD_STRENGTH_ABOVE_900_MPA")]
    [DataRow("diameter", "ANCHOR_DIAMETER_OUTSIDE_12_TO_64_MM")]
    [DataRow("hook", "HOOKED_ANCHOR_ABOVE_FYB_300_MPA")]
    [DataRow("edge", "ANCHOR_EDGE_DISTANCE_BELOW_1.2D0")]
    [DataRow("spacing", "ANCHOR_SPACING_BELOW_2.4D0")]
    [DataRow("flangeweld", "ANCHOR_HOLE_INTERSECTS_FLANGE_WELD")]
    [DataRow("webweld", "ANCHOR_HOLE_INTERSECTS_WEB_WELD")]
    [DataRow("innerweld", "ANCHOR_HOLE_INTERSECTS_INNER_FLANGE_WELD")]
    [DataRow("groutstrength", "GROUT_OUTSIDE_BETA_J_TWO_THIRDS_CONDITIONS")]
    [DataRow("groutthick", "GROUT_OUTSIDE_BETA_J_TWO_THIRDS_CONDITIONS")]
    [DataRow("grout50", "GROUT_OUTSIDE_BETA_J_TWO_THIRDS_CONDITIONS")]
    [DataRow("ntcconcrete", "NTC_CONCRETE_FACTORS_FIXED_BY_4.1.2.1.1.1")]
    [DataRow("ec2alpha", "EC2_CONCRETE_FACTORS_OUTSIDE_3.1.6")]
    [DataRow("ac1", "DESIGN_DISTRIBUTION_AREA_BELOW_PLATE_FOOTPRINT")]
    [DataRow("friction", "TESTED_GROUT_FRICTION_COEFFICIENT_AND_SOURCE_REQUIRED")]
    [DataRow("concretetension", "ANCHOR_CONCRETE_TENSION_RESISTANCE_REQUIRED")]
    [DataRow("concretesource", "ANCHOR_CONCRETE_RESISTANCE_SOURCE_REQUIRED")]
    [DataRow("between", "ANCHOR_TENSION_BETWEEN_FLANGES_NOT_IMPLEMENTED")]
    [DataRow("betweentension", "ANCHOR_TENSION_BETWEEN_FLANGES_NOT_IMPLEMENTED")]
    [DataRow("consumable", "Welds:CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED")]
    [DataRow("eccentricity", "Welds:LOCAL_ECCENTRICITY_MODEL_REQUIRED")]
    public void OutOfFieldOrUndeclaredInputIsAMissingRequirementNeverAPass(string key, string expected)
    {
        var deep = new ConnectionRolledISection(650, 300, 13, 25, 27, 2 * 300 * 25 + (650 - 50) * 13 + 4 * (1 - Math.PI / 4) * 27 * 27, 275, 430);
        var s = key switch
        {
            "stiffened" => Example1() with { Detail = ColumnBaseDetail.StiffenedPlate },
            "embedded" => Example1() with { Detail = ColumnBaseDetail.EmbeddedColumn },
            "hollow" => Example1() with { Detail = ColumnBaseDetail.HollowSectionColumn },
            "seismic" => Example1() with { Situation = ColumnBaseDesignSituation.SeismicCapacityDesign },
            "fatigue" => Example1() with { Situation = ColumnBaseDesignSituation.Fatigue },
            "minor" => Example1() with { M1 = 0, M2 = 10e6 },
            "biaxial" => Example1() with { M2 = 1e3 },
            "torsion" => Example1() with { T = 1e3 },
            "combination" => Example1() with { Combination = " " },
            "pattern" => Example1() with { PatternSource = "" },
            "material" => Example1() with { AnchorSource = null },
            "ac1source" => Example1() with { Ac1Source = null },
            "mcsource" => Example1() with { McSource = " " },
            "mcshear" => Example1() with { McShear = 0 },
            "s460" => Example1() with { PlateFy = 500, PlateFu = 550 },
            "grade" => Example1() with { PlateFy = 355 },
            "deep" => Example1() with { Column = deep, PlateLength = 900, PlateWidth = 400, Row = 380 },
            "cover" => Example1() with { PlateWidth = 230, Gauge = 100 },
            "fyb900" => Example1() with { Fyb = 940, Fub = 1040 },
            "diameter" => Example1() with { Diameter = 10, TensileArea = 58, Hole = 11, Dm = 17 },
            "hook" => Example1() with { Anchorage = ColumnBaseAnchorage.Hook, Fyb = 355, Fub = 510 },
            "edge" => Example1() with { Row = 195 },
            "spacing" => Example1() with { Gauge = 60 },
            "flangeweld" => Example1() with { Row = 140 },
            "webweld" => Example3() with { Gauge = 40 },
            "innerweld" => Example3() with { Row = 90 },
            "groutstrength" => Example1() with { GroutFck = 4 },
            "groutthick" => Example1() with { Grout = 90 },
            "grout50" => Example1() with { Grout = 60, GroutFck = 20 },
            "ntcconcrete" => Example2() with { AlphaCC = 1.0 },
            "ec2alpha" => Example1() with { AlphaCC = 0.7 },
            "ac1" => Example1() with { Ac1 = 100000 },
            "friction" => Example1() with { GroutType = ColumnBaseGrout.OtherTested },
            "concretetension" => Example1() with { ConcreteTension = null },
            "concretesource" => Example1() with { ConcreteSource = null },
            "between" => Example3() with { M1 = 60e6, McShear = 40e3 },
            "betweentension" => Example3() with { N = 50e3 },
            "consumable" => Example1() with { Consumable = null },
            "eccentricity" => Example1() with { Eccentricity = "" },
            _ => throw new ArgumentException(key)
        };
        var r = ColumnBase.Verify(s.Build());
        CollectionAssert.Contains(r.MissingRequirements.ToArray(), expected, string.Join(";", r.MissingRequirements));
        Assert.IsFalse(r.IsStaticResistanceComplete); Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Checks.All(c => !c.Passed), "Only failed checks may be retained beside missing requirements");
        Assert.IsNull(r.MomentResistance); Assert.IsNull(r.ShearResistance);
        Assert.AreNotEqual(ConnectionStatus.Passed, r.Status);
    }

    [TestMethod]
    public void InvalidInputThrowsInsteadOfProducingAResult()
    {
        static ColumnBaseAnchor Anchor(double d = 24, double As = 353, double d0 = 26, double fyb = 300, double fub = 500, double nut = 21.5, double dm = 37.7, double washer = 4)
            => new(d, As, d0, fyb, fub, true, washer, nut, dm, ColumnBaseAnchorage.WasherPlate, "src");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Anchor(d: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Anchor(As: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Anchor(washer: -1));
        Assert.ThrowsException<ArgumentException>(() => Anchor(fyb: 600, fub: 500));
        Assert.ThrowsException<ArgumentException>(() => Anchor(d0: 24));
        Assert.ThrowsException<ArgumentException>(() => Anchor(As: 500));
        Assert.ThrowsException<ArgumentException>(() => Anchor(dm: 26));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchor(24, 353, 26, 300, 500, true, 4, 21.5, 37.7, (ColumnBaseAnchorage)9, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchorPattern(ColumnBaseAnchorLayout.OutsideFlanges, 160, 0, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchorPattern(ColumnBaseAnchorLayout.BetweenFlanges, 0, 0, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchorPattern((ColumnBaseAnchorLayout)5, 100, 0, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseFoundation(25, 1, 1.5, 1e5, 0, 30, ColumnBaseGrout.SandCementMortar, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseFoundation(25, 1, 1.5, 1e5, 30, 30, ColumnBaseGrout.OtherTested, "s", -0.3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseFoundation(25, 1, 1.5, 1e5, 30, 30, (ColumnBaseGrout)3, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchorConcreteResistance(0, null, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseAnchorConcreteResistance(null, double.PositiveInfinity, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseWelds(0, 8, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275, "c", "e"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseWelds(12, 8, (FilletWeldEndCondition)7, FilletWeldEndCondition.FullSize, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275, "c", "e"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseWelds(12, 8, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, (FilletWeldSteelGrade)300, FilletWeldSteelGrade.S275, "c", "e"));
        var code = ConnectionDesignCode.Ntc2018();
        Assert.ThrowsException<ArgumentNullException>(() => new ColumnBaseColumnMomentResistance(null!, Heb240(), 1e8, 0, "s"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseColumnMomentResistance(code, Heb240(), 0, 0, "s"));
        var good = Example1().Build();
        Assert.ThrowsException<ArgumentNullException>(() => ColumnBase.Verify(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new ColumnBaseInput(good.Code, good.Detail, good.Situation, good.Column, 430, good.Plate, 440,
            null!, good.Pattern, good.Foundation, good.AnchorConcrete, good.Welds, good.ColumnMomentResistance, good.Actions, "c"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseInput(good.Code, good.Detail, good.Situation, good.Column, 430, good.Plate, 0,
            good.Anchor, good.Pattern, good.Foundation, good.AnchorConcrete, good.Welds, good.ColumnMomentResistance, good.Actions, "c"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseInput(good.Code, (ColumnBaseDetail)8, good.Situation, good.Column, 430, good.Plate, 440,
            good.Anchor, good.Pattern, good.Foundation, good.AnchorConcrete, good.Welds, good.ColumnMomentResistance, good.Actions, "c"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnBaseInput(good.Code, good.Detail, (ColumnBaseDesignSituation)8, good.Column, 430, good.Plate, 440,
            good.Anchor, good.Pattern, good.Foundation, good.AnchorConcrete, good.Welds, good.ColumnMomentResistance, good.Actions, "c"));
    }

    [TestMethod]
    public void ReusedComponentsGiveTheSameValuesAsTheirOwnRoutes()
    {
        var input = Example1().Build(); var r = ColumnBase.Verify(input);
        // Table6.6 outer-row geometry of the end-plate components, used unchanged for the anchor row.
        var g = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new TStubExtendedEndPlateGeometry(400, 160, 50, 12, 50, "Base plate drawing BP-01"));
        foreach (var v in g.Trace) Assert.AreEqual(v.Value, Trace(r, "Tension.TStub." + v.Name), 0, v.Name);
        // Elastic weld group of the module, same segments and actions.
        Assert.IsNotNull(r.Welds); Assert.AreEqual(16, r.Welds!.Points.Count);
        Assert.AreEqual(r.Welds.Utilization!.Value, Check(r, "Welds.ActualActions").Demand, 0);
        // Bolt tension formula shared with every bolted component.
        Assert.AreEqual(BoltResistance.Tension(500, 353, 1.25), Trace(r, "Tension.Anchor.SteelTensionResistance"), 0);
        // Cut threads not to EN1090: x0.85 (EN1993-1-8 3.6.1(3)): 0.85*127080=108018 N, still below concrete? No: 108018<110000, governs.
        var cut = ColumnBase.Verify((Example1() with { ThreadsEn1090 = false }).Build());
        Close(108018, Trace(cut, "Tension.Anchor.SteelTensionResistance"), "0.85 Ft"); Close(2 * 108018, cut.SideTensionResistance!.Value, "FT cut threads");
    }

    [TestMethod]
    public void ReportAndSummaryProjectTheResultWithoutChangingIt()
    {
        var r = ColumnBase.Verify(Example1().Build());
        var report = StandardConnectionCaseReport.Create(r);
        Assert.AreEqual(ColumnBase.MethodId, report.MethodId); Assert.AreEqual(r.Status, report.Status); Assert.IsTrue(report.IsComplete);
        Assert.AreEqual(r.Checks.Count, report.Checks.Count); Assert.IsFalse(report.CanQualifyWholeMember);
        Assert.AreEqual(r.MomentResistance!.Value, report.Results.Single(v => v.Name == "Joint.MomentResistanceAtActualEccentricity").Value, 0);
        Assert.IsTrue(report.Sources.Contains("COLUMN_BASE_USAGE.txt")); StringAssert.StartsWith(report.Code, "EC3:2005");
        var summary = SteelAssessmentSummary.From(report);
        Assert.AreEqual(EngineeringOutcome.Satisfied, summary.Outcome); Assert.IsTrue(summary.IsComplete);
        var refused = StandardConnectionCaseReport.Create(ColumnBase.Verify((Example1() with { M2 = 5e6 }).Build()));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, SteelAssessmentSummary.From(refused).Outcome);
        Assert.ThrowsException<ArgumentNullException>(() => StandardConnectionCaseReport.Create((ColumnBaseResult)null!));
        var ntc = StandardConnectionCaseReport.Create(ColumnBase.Verify(Example2().Build())); StringAssert.StartsWith(ntc.Code, "NTC2018");
    }

    [TestMethod]
    public void CatalogListsTheStandardColumnBaseWithItsOpenMechanisms()
    {
        var c = ConnectionCapabilityCatalog.Get("ColumnBases");
        Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification, c.Level);
        Assert.AreEqual("ColumnBase.Verify", c.RecommendedEntryPoint); CollectionAssert.Contains(c.EntryPoints.ToArray(), "StandardConnectionCaseReport.Create");
        Assert.AreEqual("COLUMN_BASE_USAGE.txt", c.Documentation);
        foreach (string open in new[] { "Minor-axis", "between the flanges", "Stiffened or embedded", "hollow-section", "seismic", "fatigue" })
            Assert.IsTrue(c.MissingMechanisms.Any(m => m.IndexOf(open, StringComparison.OrdinalIgnoreCase) >= 0), open);
        Assert.IsTrue(c.ExternalRequirements.Any(x => x.Contains("concrete failure modes of the anchors")));
        Assert.AreEqual(ConnectionCapabilityLevel.NotImplemented, ConnectionCapabilityCatalog.Get("TubularJoints").Level);
    }
}
