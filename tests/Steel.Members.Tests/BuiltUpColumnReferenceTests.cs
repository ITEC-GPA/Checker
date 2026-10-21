using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BuiltUpColumnReferenceTests
{
    [TestMethod]
    public void PublishedCticmSciLacedColumnAppendixA()
    {
        // SBE_SS6 Appendix A pp6-24..6-28. Two HE220A: Ach6430 mm2,
        // h0=800, L=10000, a=1250, 2 N-lacing planes, Ad1552/Av1227.
        var c=Ec3BuiltUpColumn.Laced(210000,10000,8,800,6430,1552,LacingSystem.WithPosts,1227);
        // Source rounds d to1480mm and Ieff to205800cm4. Propagate only those
        // known input roundings, rather than tuning tolerance against output.
        double d=Math.Sqrt(800d*800+1250d*1250);
        double shear(double diagonal)=>2*210000d*1552*1250*800*800/(Math.Pow(diagonal,3)+1552d/1227*Math.Pow(800,3));
        double roundedSv=shear(1480),exactSv=shear(d);
        double exactI=2057600000d, roundedNcr=Math.PI*Math.PI*210000*2058000000/1e8;
        double exactNcr=Math.PI*Math.PI*210000*exactI/1e8;
        Assert.AreEqual(2058000000,c.EffectiveInertia,500000d);
        Assert.AreEqual(134100000,c.ShearStiffness,50000+Math.Abs(exactSv-roundedSv));
        Assert.AreEqual(42650000,c.EulerForce,5000+Math.Abs(exactNcr-roundedNcr));
        var r=c.Analyze(900000,450e6,BuiltUpLoadDiagram.UniformTransverseLoad);
        Assert.AreEqual(BuiltUpAnalysisStatus.Evaluated,r.Status);
        Assert.AreEqual(481.4e6,r.Moment!.Value,50000d);
        Assert.AreEqual(1052000,r.ChordCompression!.Value,500d);
        // Guide computes V from M=482kNm. The explicit propagation accounts for
        // its intermediate rounding; output is compared also to exact equations.
        double factor=(4-(4-Math.PI)*18000000/(18000000+450e6))/10000;
        double exactMoment=468e6/(1-900000/exactNcr-900000/exactSv);
        Assert.AreEqual(191200,r.Shear!.Value,50+factor*Math.Abs(exactMoment-482e6));
        double exactV=factor*exactMoment;
        Assert.AreEqual(exactV,r.Shear.Value,1e-7);
        double roundDiag=191200d*1480/1600,exactDiag=exactV*d/1600;
        Assert.AreEqual(176860,r.DiagonalForce!.Value,10+Math.Abs(exactDiag-roundDiag));
        Assert.AreEqual(r.Shear.Value,r.PostForce!.Value,1e-9);
        // Published design chord capacities are independent inputs: plane .477,
        // out-of-plane .595. Actual chord buckling lengths differ (1125/5000).
        Assert.AreEqual(.595,Ec3BuiltUpAxialChecks.ChordUtilization(r,2203000,1767000),.0005);
        Assert.IsTrue(r.OtherChordForce<0,"The opposite chord is in tension; do not omit its tensile check.");
        CollectionAssert.Contains(r.RequiredChecks.ToArray(),"ChordTensionIfPresent");
    }

    [TestMethod]
    public void PublishedSciNsc2021LacedColumnCapacity()
    {
        // NSC Technical Digest2021 pp24-25, same section and lacing, N=4167kN,
        // no external moment: M=95.66kNm, Nch=2203kN. Published rounded values.
        var c=Ec3BuiltUpColumn.Laced(210000,10000,8,800,6430,1552,LacingSystem.WithPosts,1227);
        var r=c.Analyze(4167000);
        double d=Math.Sqrt(800d*800+1250d*1250);
        double exactSv=2*210000d*1552*1250*800*800/(Math.Pow(d,3)+1552d/1227*Math.Pow(800,3));
        double exactNcr=Math.PI*Math.PI*210000*2057600000d/1e8;
        double exactM=4167000*20d/(1-4167000/exactNcr-4167000/exactSv);
        double publishedInputM=4167000*20d/(1-4167d/42650-4167d/134100);
        // NSC uses the rounded Ncr/Sv inherited from SBE_SS6. Propagate that
        // independently calculated input difference; retain the printed target.
        Assert.AreEqual(95.66e6,r.Moment!.Value,5000+Math.Abs(exactM-publishedInputM));
        Assert.AreEqual(2203000,r.ChordCompression!.Value,500d);
        Assert.AreEqual(Math.PI*r.Moment.Value/10000,r.Shear!.Value,1e-8);
    }

    public static IEnumerable<object[]> LacingCases =>
        from system in new[]{LacingSystem.ZigZag,LacingSystem.ZigZagWithPosts,LacingSystem.WithPosts}
        from h in new[]{200d,800d,1600d}
        from panels in new[]{3,6,12}
        select new object[]{system,h,panels};

    [DataTestMethod,DynamicData(nameof(LacingCases))]
    public void LacingStiffnessAgainstUnitLoadStrainEnergy(LacingSystem system,double h,int panels)
    {
        // Independent unit-shear energy per module, ESDEP Lecture7.6 section3.1.
        // Work V^2*a/(2Sv) = sum(Ni^2*li/(2EAi)), two planes.
        double e=210000,a=6000d/panels,ad=800,av=600;
        double run=system==LacingSystem.ZigZag?a/2:a,d=Math.Sqrt(h*h+run*run);
        double nd=d/(2*h);
        int count=system==LacingSystem.ZigZag?4:2;
        double twiceEnergy=count*nd*nd*d/(e*ad);
        if(system==LacingSystem.WithPosts)twiceEnergy+=2*.5*.5*h/(e*av);
        var c=Ec3BuiltUpColumn.Laced(e,6000,panels,h,3000,ad,system,system==LacingSystem.WithPosts?av:0);
        Assert.AreEqual(a/twiceEnergy,c.ShearStiffness,c.ShearStiffness*1e-12);
        Assert.AreEqual(.5*3000*h*h,c.EffectiveInertia,1e-8);
        double n=0.2*c.CriticalForce;
        var r=c.Analyze(n);
        Assert.AreEqual(.8,r.AmplificationDenominator,1e-12);
        Assert.AreEqual(n/2+r.Moment!.Value/h,r.ChordCompression!.Value,1e-8);
        Assert.AreEqual(r.Shear!.Value*d/(2*h),r.DiagonalForce!.Value,1e-8);
    }

    public static IEnumerable<object[]> BattenCases =>
        from length in new[]{5000d,15000d,30000d,60000d}
        from ib in new[]{1e4,1e7,1e12}
        select new object[]{length,ib};

    [DataTestMethod,DynamicData(nameof(BattenCases))]
    public void BattenStiffnessAgainstFrameStrainEnergyAndTable68(double length,double ib)
    {
        double e=210000,h=400,ach=2400,ich=2026250,a=length/10;
        var c=Ec3BuiltUpColumn.Battened(e,length,10,h,ach,ich,ib);
        // Unit shear: two chords Mmax=a/4; two battens Mmax=a/4.
        // Integral M^2/EI ds gives compliance a^3/(24EIc)+a^2*h/(24EIb).
        double flexibility=a*a*a/(24*e*ich)+a*a*h/(24*e*ib);
        double expected=Math.Min(a/flexibility,2*Math.PI*Math.PI*e*ich/(a*a));
        Assert.AreEqual(expected,c.ShearStiffness,expected*1e-12);
        double i0=Math.Sqrt((.5*h*h*ach+2*ich)/(2*ach)),lambda=length/i0;
        double mu=Math.Clamp((150-lambda)/75,0,1);
        Assert.AreEqual(mu,c.Efficiency,1e-12);
        Assert.AreEqual(.5*h*h*ach+2*mu*ich,c.EffectiveInertia,1e-7);
        var r=c.Analyze(.2*c.CriticalForce);
        double v=r.Shear!.Value;
        Assert.AreEqual(v*a/4,r.LocalChordMoment!.Value,1e-7);
        Assert.AreEqual(v*a/4,r.Batten.Moment,1e-7);
        Assert.AreEqual(v*a/(2*h),r.Batten.Shear,1e-9);
        Assert.ThrowsException<NotSupportedException>(()=>Ec3BuiltUpAxialChecks.ChordUtilization(r,1e9,1e9));
    }

    [DataTestMethod]
    [DataRow(75d,1d)] [DataRow(150d,0d)] [DataRow(112.5,.5)]
    public void EfficiencyTableBoundaries(double slenderness,double expected)
    {
        // Ach=2,Ic=1,h=2 -> i0=sqrt(6/4).
        var c=Ec3BuiltUpColumn.Battened(210000,slenderness*Math.Sqrt(1.5),3,2,2,1,1);
        Assert.AreEqual(expected,c.Efficiency,1e-14);
    }

    [DataTestMethod]
    [DataRow(.999)] [DataRow(1d)] [DataRow(1.001)] [DataRow(10d)]
    public void CriticalPointDoesNotProducePostcriticalResistance(double fraction)
    {
        var c=Ec3BuiltUpColumn.Battened(210000,6000,6,400,2400,2026250,1e7);
        var r=c.Analyze(fraction*c.CriticalForce);
        Assert.AreEqual(fraction<1?BuiltUpAnalysisStatus.Evaluated:BuiltUpAnalysisStatus.Unstable,r.Status);
        if(fraction>=1){Assert.IsNull(r.Moment);Assert.IsNull(r.ChordCompression);Assert.IsNull(r.Batten);}
    }

    [TestMethod]
    public void MomentSignAndZeroLoad()
    {
        var c=Ec3BuiltUpColumn.Laced(210000,6000,6,400,2400,800,LacingSystem.WithPosts,600);
        var p=c.Analyze(100000,1e7);var n=c.Analyze(100000,-1e7);
        Assert.AreEqual(p.Moment,n.Moment);Assert.AreEqual(p.Shear,n.Shear);
        Assert.AreEqual(0d,c.Analyze(0).Moment);
        Assert.AreEqual(0d,c.Analyze(0).ChordCompression);
        Assert.AreEqual(Math.PI*1e7/6000,c.Analyze(0,1e7).Shear!.Value,1e-8);
    }

    [DataTestMethod]
    [DataRow(149.999,true)] [DataRow(150d,true)] [DataRow(150.001,false)]
    public void CloselySpacedLimit(double pitch,bool expected)
        =>Assert.AreEqual(expected,Ec3BuiltUpColumn.MayIgnoreShearDeformation(pitch,10));

    [DataTestMethod]
    [DataRow("C")] [DataRow("L")]
    public void LacedCAndLChordsUseQualifiedComponentCompression(string family)
    {
        Section shape=family=="C"?new SectionC(300,10,100,15,100,15,""):new SectionL(100,20,150,20,"");
        var s=new SteelSection(shape,new SteelMaterial("S235",210000,235,360));
        var c=Ec3BuiltUpColumn.Laced(210000,6000,6,400,s.Area,800,LacingSystem.WithPosts,600);
        // Local chord segment and global out-of-plane check have explicit lengths;
        // each component check includes both principal and torsional modes conservatively.
        var local=Ec3OpenSectionCompression.Evaluate(s,1000,235,1,"test EC3 gammaM1");
        var global=Ec3OpenSectionCompression.Evaluate(s,6000,235,1,"test EC3 gammaM1");
        var r=c.Analyze(10000);
        Assert.AreEqual(r.ChordCompression!.Value/Math.Min(local.NbRd,global.NbRd),Ec3BuiltUpAxialChecks.ChordUtilization(r,local.NbRd,global.NbRd),1e-12);
        Assert.IsTrue(local.NbRd>global.NbRd);
    }

    [DataTestMethod]
    [DataRow("C-back",10d)] [DataRow("C-toe",20d)] [DataRow("L-v",0d)] [DataRow("L-h",30d)]
    public void ModelPairSpacingIsCentroidDistanceNotAssemblyGap(string layout,double gap)
    {
        bool c=layout.StartsWith("C"),horizontal=layout=="L-h";
        var l=new SectionL(100,10,150,10,"");var ch=new SectionC(200,8,80,12,80,12,"");
        var shape=c?SectionBuiltUp.DoubleChannel(ch,gap,layout=="C-back",""):
            SectionBuiltUp.DoubleAngle(l,gap,horizontal?SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack:SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack,"");
        var snapshot=ModelBuiltUpGeometry.Create(shape);
        // Independent rectangular integrals: L centroid 23.75/48.75;
        // C area3328, centroid=(2*960*40+1408*4)/3328.
        double centroidC=(2*960d*40+1408*4)/3328;
        double distance=c?(layout=="C-back"?gap+2*centroidC:gap+2*(80-centroidC)):
            gap+2*(horizontal?48.75:23.75);
        Assert.AreEqual(distance,snapshot.CentroidSpacing,1e-10);
        Assert.AreEqual(c?3328:2400,snapshot.Area,1e-10);
        Assert.AreEqual(c,snapshot.ChordAxesArePrincipal);
        var member=Ec3BuiltUpColumn.Laced(210000,6000,6,snapshot.CentroidSpacing,snapshot.Area,800,LacingSystem.ZigZag);
        Assert.AreEqual(.5*snapshot.Area*distance*distance,member.EffectiveInertia,1e-7);
    }

    [TestMethod]
    public void ModelMapperRejectsOverlappingAndUnpairedSections()
    {
        var l=new SectionL(100,10,150,10,"");
        Assert.ThrowsException<NotSupportedException>(()=>ModelBuiltUpGeometry.Create(new SectionBuiltUp(new[]{
            new SectionBuiltUp.Part(l,50,0),new SectionBuiltUp.Part(l,100,0,true)},"overlap")));
        Assert.ThrowsException<NotSupportedException>(()=>ModelBuiltUpGeometry.Create(SectionBuiltUp.Cruciform(l,10,"")));
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidGeometryRejected(double bad)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.Laced(210000,6000,6,bad,2400,800,LacingSystem.ZigZag));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.Battened(210000,6000,6,400,2400,bad,1e7));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.MayIgnoreShearDeformation(100,bad));
    }

    [TestMethod]
    public void UnsupportedSystemsAndNonphysicalActionsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.Laced(210000,6000,2,400,2400,800,LacingSystem.ZigZag));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.Laced(210000,6000,6,400,2400,800,(LacingSystem)99));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3BuiltUpColumn.Laced(210000,6000,6,400,2400,800,LacingSystem.WithPosts));
        var c=Ec3BuiltUpColumn.Laced(210000,6000,6,400,2400,800,LacingSystem.ZigZag);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>c.Analyze(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>c.Analyze(1,double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>c.Analyze(1,0,(BuiltUpLoadDiagram)99));
    }

    [TestMethod]
    public void PublishedLacingComponentResistanceChecks()
    {
        var c=Ec3BuiltUpColumn.Laced(210000,10000,8,800,6430,1552,LacingSystem.WithPosts,1227);
        var r=c.Analyze(900000,450e6,BuiltUpLoadDiagram.UniformTransverseLoad);
        // SBE_SS6 Appendix A pp6-26..6-31: chord out-of-plane1767kN,
        // diagonal Nb285.9kN and Nt551kN; post Nb310kN.
        // Chord/post tensile capacities here are analytical gross-section inputs,
        // explicitly for unperforated components, not a joint verification.
        var checks=BuiltUpComponentChecks.Laced(r,
            new AxialComponentResistances(1767000,6430*355,"SBE_SS6 p6-27; gross unperforated Nt=Af_y/gammaM0"),
            new AxialComponentResistances(285900,551000,"SBE_SS6 pp6-29/6-31; effective area independently established there"),
            new AxialComponentResistances(310000,1227*355,"SBE_SS6 p6-30; gross unperforated Nt=Af_y/gammaM0"));
        Assert.AreEqual(6,checks.Count);
        Assert.AreEqual(.595,checks.Single(x=>x.Component=="Chord"&&x.Mechanism=="Compression").Utilization,.0005);
        Assert.AreEqual(.62,checks.Single(x=>x.Component=="Diagonal"&&x.Mechanism=="Compression").Utilization,.005);
        Assert.AreEqual(.32,checks.Single(x=>x.Component=="Diagonal"&&x.Mechanism=="Tension").Utilization,.005);
        Assert.AreEqual(.62,checks.Single(x=>x.Component=="Post"&&x.Mechanism=="Compression").Utilization,.005);
        Assert.IsTrue(checks.Single(x=>x.Component=="Chord"&&x.Mechanism=="Tension").Demand>0);
        Assert.IsTrue(checks.All(x=>x.Passed));
        Assert.ThrowsException<ArgumentNullException>(()=>BuiltUpComponentChecks.Laced(r,
            new AxialComponentResistances(1e6,1e6,"test"),new AxialComponentResistances(1e6,1e6,"test")));
    }

    [TestMethod]
    public void BattenComponentChecksRetainTheRequiredChordInteraction()
    {
        var c=Ec3BuiltUpColumn.Battened(210000,6000,6,400,2400,2026250,1e7);
        var r=c.Analyze(.2*c.CriticalForce);
        var checks=BuiltUpComponentChecks.Batten(r,r.Batten.Moment*.9,r.Batten.Shear*2,"explicit design capacities for this M/V state");
        Assert.IsFalse(checks[0].Passed);Assert.IsTrue(checks[1].Passed);
        CollectionAssert.Contains(r.RequiredChecks.ToArray(),"ChordInPlaneNMVAndBuckling");
        Assert.ThrowsException<InvalidOperationException>(()=>BuiltUpComponentChecks.Batten(c.Analyze(c.CriticalForce),1,1,"test"));
    }
}
