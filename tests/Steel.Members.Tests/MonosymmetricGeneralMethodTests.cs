using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Curve = GPC.Checkers.Steel.Members.Ec3GeneralMethodCurve;

namespace Steel.Members.Tests;

[TestClass]
public class MonosymmetricGeneralMethodTests
{
    [DataTestMethod]
    [DataRow(Curve.A0, .7253442178684112)]
    [DataRow(Curve.A, .6656030592846071)]
    [DataRow(Curve.B, .5970231915935529)]
    [DataRow(Curve.C, .5399390272238410)]
    [DataRow(Curve.D, .4670914028401907)]
    public void GeneralMethodMatchesNormativeCurvesAtUnitSlenderness(Curve curve, double expected)
    {
        // EC3:2005 eqs6.49/6.56,6.63/6.64; independent 40-digit Decimal
        // evaluation at lambda=1, alphaUlt,k=alphaCr,op=2, gammaM1=1.1.
        var lt=curve==Curve.A0?Curve.A:curve;
        double expectedOp=curve==Curve.A0?.6656030592846071:expected;
        var r=Ec3GeneralMethod.EvaluateInPlane(2,2,curve,lt,1.1,"EC3 recommended values", "independent scalar example");
        Assert.AreEqual(1,r.Slenderness,1e-14);
        Assert.AreEqual(expected,r.Chi,1e-14);
        Assert.AreEqual(expectedOp,r.ChiOp,1e-14);
        Assert.AreEqual(1.1/(2*expectedOp),r.Utilization,1e-14);
        Assert.AreEqual(r.Utilization<=1,r.IsSatisfied);
    }

    [DataTestMethod]
    [DataRow(.0001)] [DataRow(.04)] [DataRow(.25)] [DataRow(1d)] [DataRow(4d)]
    public void UsesMinimumCurveAndPreservesProportionalLoadScaling(double squaredLambda)
    {
        var r=Ec3GeneralMethod.EvaluateInPlane(3,3/squaredLambda,Curve.B,Curve.D,1,"test","scalar");
        var scaled=Ec3GeneralMethod.EvaluateInPlane(1.5,1.5/squaredLambda,Curve.B,Curve.D,1,"test","double load");
        Assert.AreEqual(Math.Sqrt(squaredLambda),r.Slenderness,1e-14);
        Assert.AreEqual(r.ChiLT,r.ChiOp,1e-14);
        Assert.AreEqual(2*r.Utilization,scaled.Utilization,1e-14);
        if(squaredLambda<=.04) Assert.AreEqual(1,r.ChiOp);
    }

    public static IEnumerable<object[]> Cases =>
        from family in new[] { "H", "T" }
        from scale in new[] { .1,1d,10d }
        from lengthRatio in new[] { 2.5,10d,30d }
        from action in new[] { "N", "M", "NM" }
        from sign in new[] { -1d,1d }
        select new object[] { family,scale,lengthRatio,action,sign };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void UniformMemberMatchesIndependentEnergyAndFiniteDifferenceSolutions(string family,double scale,double lengthRatio,string action,double sign)
    {
        var s=Section(family,scale); double l=s.SectionShape.Height*lengthRatio;
        double p=action=="M"?0:100000*scale*scale, m=action=="N"?0:sign*80e6*scale*scale*scale;
        var r=Check(s,l,-p,m);
        double expected=EnergyCriticalFactor(s,l,p,m,1);
        Assert.AreEqual(expected,r.GeneralMethod.AlphaCrOp,expected*2e-10);
        Assert.IsTrue(EnergyCriticalFactor(s,l,p,m,2)>=expected);
        Assert.IsTrue(EnergyCriticalFactor(s,l,p,m,3)>=expected);
        // Independently solve EI*v''+alpha*P*v=-alpha*M-alpha*P*e0*sin(pi*x/L)
        // on two finite-difference meshes. No secant/cosine formula in this oracle.
        double coarse=FiniteDifferenceYieldRatio(s,l,p,Math.Abs(m),l/200,r.GeneralMethod.AlphaUltK,128);
        double fine=FiniteDifferenceYieldRatio(s,l,p,Math.Abs(m),l/200,r.GeneralMethod.AlphaUltK,256);
        Assert.AreEqual(1,fine,2e-5);
        if(Math.Abs(coarse-1)>1e-8) Assert.IsTrue(Math.Abs(fine-1)<.3*Math.Abs(coarse-1),$"mesh convergence {coarse:R}, {fine:R}");
        Assert.AreEqual(l/200,r.InitialBow,1e-12*l);
        Assert.AreEqual(Math.Max(r.SectionUtilization,r.GeneralMethod.Utilization),r.Utilization);
        Assert.IsTrue(double.IsFinite(r.Utilization));
    }

    [DataTestMethod]
    [DataRow("H")] [DataRow("T")]
    public void PureCompressionMatchesPublishedMonosymmetricEquation16(string family)
    {
        // Snijder/Stroetmann/Balaz (2025), CE/Papers8(6),p618,eq16;
        // doi10.1002/cepa.70116. Equal sinusoidal modes -> coupling integral=1.
        var s=Section(family,1); var r=Check(s,4000,-100000,0);
        double a=s.Area, i02=(s.J11+s.J22)/a+Math.Pow(s.ShearCenter.Y-s.Centroid.Y,2);
        double nt=r.TorsionalStiffness/i02, nz=r.NcrOutOfPlane;
        double beta=1-Math.Pow(s.ShearCenter.Y-s.Centroid.Y,2)/i02;
        double expected=2*nz*nt/(nz+nt+Math.Sqrt(Math.Pow(nz+nt,2)-4*beta*nz*nt));
        Assert.AreEqual(expected,100000*r.GeneralMethod.AlphaCrOp,expected*1e-10);
    }

    [DataTestMethod]
    [DataRow(Curve.A0,350d)] [DataRow(Curve.A,300d)] [DataRow(Curve.B,250d)] [DataRow(Curve.C,200d)] [DataRow(Curve.D,150d)]
    public void ElasticImperfectionsFollowTable51(Curve curve,double divisor)
    {
        var s=Section("T",1);
        var r=Ec3MonosymmetricMember.EvaluateUniformFork(s,4000,-100000,80e6,curve,Curve.C,Curve.D,1,1,"Table5.1 elastic");
        Assert.AreEqual(4000/divisor,r.InitialBow,1e-12);
    }

    [DataTestMethod]
    [DataRow("H")] [DataRow("T")]
    public void LoadScalingAndPartialFactorsRemainSeparate(string family)
    {
        var s=Section(family,1); var r=Check(s,4000,-100000,80e6); var doubled=Check(s,4000,-200000,160e6);
        Assert.AreEqual(r.GeneralMethod.AlphaUltK/2,doubled.GeneralMethod.AlphaUltK,1e-12);
        Assert.AreEqual(r.GeneralMethod.AlphaCrOp/2,doubled.GeneralMethod.AlphaCrOp,1e-12);
        Assert.AreEqual(r.Utilization*2,doubled.Utilization,1e-12);
        var factors=Ec3MonosymmetricMember.EvaluateUniformFork(s,4000,-100000,80e6,Curve.C,Curve.C,Curve.D,1.2,1.1,"explicit factors");
        Assert.AreEqual(1.2*r.SectionUtilization,factors.SectionUtilization,1e-12);
        Assert.AreEqual(1.1*r.GeneralMethod.Utilization,factors.GeneralMethod.Utilization,1e-12);
        Assert.AreEqual(r.GeneralMethod.AlphaUltK,factors.GeneralMethod.AlphaUltK);
    }

    [TestMethod]
    public void MirroringHAndReversingMomentPreservesMemberResistance()
    {
        var s=Section("H",1); var mirror=new SteelSection(new SectionH(300,25,150,25,300,25,""),s.SteelMaterial);
        var a=Check(s,4000,-100000,80e6); var b=Check(mirror,4000,-100000,-80e6);
        Assert.AreEqual(a.GeneralMethod.AlphaCrOp,b.GeneralMethod.AlphaCrOp,1e-10);
        Assert.AreEqual(a.Utilization,b.Utilization,1e-10);
        Assert.IsTrue(Math.Abs(a.GeneralMethod.AlphaCrOp-Check(s,4000,-100000,-80e6).GeneralMethod.AlphaCrOp)>.01);
    }

    [TestMethod]
    public void SecondOrderImperfectionReducesInPlaneResistanceAndCanFail()
    {
        var s=Section("T",1); var r=Check(s,12000,-500000,100e6);
        double firstOrder=355/(500000/s.Area+100e6/s.Wel1);
        Assert.IsTrue(r.GeneralMethod.AlphaUltK<firstOrder);
        Assert.IsFalse(r.IsSatisfied);
        Assert.IsTrue(Check(s,12000,-500000,0).GeneralMethod.AlphaUltK<355*s.Area/500000);
    }

    [TestMethod]
    public void InvalidInputsAndUnqualifiedSectionsFailExplicitly()
    {
        var s=Section("T",1);
        Assert.ThrowsException<NotSupportedException>(()=>Check(s,4000,1,80e6));
        Assert.ThrowsException<ArgumentException>(()=>Check(s,4000,0,0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Check(s,0,-1000,0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Check(s,4000,double.NaN,0));
        Assert.ThrowsException<NotSupportedException>(()=>Check(new SteelSection(new SectionT(400,200,10,10,""),s.SteelMaterial),4000,-1000,0));
        Assert.ThrowsException<NotSupportedException>(()=>Check(new SteelSection(new SectionT(400,200,40,50,"",10),s.SteelMaterial),4000,-1000,0));
        Assert.ThrowsException<NotSupportedException>(()=>Check(new SteelSection(new SectionT(160,500,8,40,""),s.SteelMaterial),4000,-1000,0));
        Assert.ThrowsException<ArgumentException>(()=>Ec3GeneralMethod.EvaluateInPlane(2,2,Curve.C,Curve.D,1,"","source"));
        Assert.ThrowsException<ArgumentException>(()=>Ec3GeneralMethod.EvaluateInPlane(2,2,Curve.C,Curve.D,1,"set",""));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3GeneralMethod.EvaluateInPlane(0,2,Curve.C,Curve.D,1,"set","source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3GeneralMethod.EvaluateInPlane(2,double.PositiveInfinity,Curve.C,Curve.D,1,"set","source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3GeneralMethod.EvaluateInPlane(2,2,(Curve)99,Curve.D,1,"set","source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3GeneralMethod.EvaluateInPlane(2,2,Curve.A0,Curve.A0,1,"set","source"));
    }

    private static SteelSection Section(string family,double s) => new(family=="H"
        ?new SectionH(300*s,25*s,300*s,25*s,150*s,25*s,"")
        :new SectionT(400*s,200*s,40*s,50*s,""),new SteelMaterial("S355",210000,355,510));
    private static MonosymmetricMemberResistance Check(SteelSection s,double l,double n,double m) =>
        Ec3MonosymmetricMember.EvaluateUniformFork(s,l,n,m,Curve.C,Curve.C,Curve.D,1,1,"EC3 explicit c/c/d test parameters");

    private static double EnergyCriticalFactor(SteelSection s,double l,double p,double m,int mode)
    {
        // Independent geometric-stiffness integration: axial stresses multiply
        // squared lateral slopes about the shear centre. 3x3 Gauss is exact.
        double[,] kg=new double[2,2]; var shape=s.SectionShape;
        (double b,double y0,double y1)[] rects=shape is SectionH h
            ?new[] { (h.LenghtBottomFlange,0d,h.ThicknessBottomFlange),(h.ThicknessWeb,h.ThicknessBottomFlange,h.Height-h.ThicknessTopFlange),(h.LenghtTopFlange,h.Height-h.ThicknessTopFlange,h.Height) }
            :new[] { (((SectionT)shape).ThicknessWeb,0d,((SectionT)shape).HeightWeb),(((SectionT)shape).LenghtFlange,((SectionT)shape).HeightWeb,shape.Height) };
        double[] nodes={-Math.Sqrt(3d/5),0,Math.Sqrt(3d/5)}, weights={5d/9,8d/9,5d/9};
        foreach(var r in rects) for(int i=0;i<3;i++) for(int j=0;j<3;j++)
        {
            double x=nodes[i]*r.b/2,y=(r.y0+r.y1)/2+nodes[j]*(r.y1-r.y0)/2;
            double da=weights[i]*weights[j]*r.b*(r.y1-r.y0)/4;
            double sigma=-p/s.Area+m*(y-s.Centroid.Y)/s.J11, ys=y-s.ShearCenter.Y;
            kg[0,0]-=sigma*da; kg[0,1]+=sigma*ys*da; kg[1,1]-=sigma*(x*x+ys*ys)*da;
        }
        double k1=Math.Pow(mode*Math.PI/l,2)*210000*s.J22;
        double k2=210000/2.6*s.Jt+Math.Pow(mode*Math.PI/l,2)*210000*s.Jw;
        var matrix=Matrix<double>.Build.DenseOfArray(new[,] {{kg[0,0]/k1,kg[0,1]/Math.Sqrt(k1*k2)},{kg[0,1]/Math.Sqrt(k1*k2),kg[1,1]/k2}});
        double eigen=matrix.Evd(Symmetricity.Symmetric).EigenValues.Max(v=>v.Real);
        return 1/eigen;
    }

    private static double FiniteDifferenceYieldRatio(SteelSection s,double l,double p,double m,double bow,double alpha,int elements)
    {
        int count=elements-1; double dx=l/elements, ei=210000*s.J11, off=-ei/(dx*dx);
        var diag=Enumerable.Repeat(2*ei/(dx*dx)-alpha*p,count).ToArray(); var rhs=new double[count];
        for(int i=0;i<count;i++) rhs[i]=alpha*m+alpha*p*bow*Math.Sin(Math.PI*(i+1)/elements);
        // Tridiagonal elimination for -EI*v''-alpha*P*v=alpha*M+alpha*P*e0.
        for(int i=1;i<count;i++) { double f=off/diag[i-1]; diag[i]-=f*off; rhs[i]-=f*rhs[i-1]; }
        var v=new double[count]; v[count-1]=rhs[count-1]/diag[count-1];
        for(int i=count-2;i>=0;i--) v[i]=(rhs[i]-off*v[i+1])/diag[i];
        double maximum=0;
        for(int i=0;i<count;i++) maximum=Math.Max(maximum,alpha*m+alpha*p*(v[i]+bow*Math.Sin(Math.PI*(i+1)/elements)));
        return (alpha*p/s.Area+maximum/s.Wel1)/355;
    }
}
