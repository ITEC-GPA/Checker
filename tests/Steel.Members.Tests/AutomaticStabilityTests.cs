using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class AutomaticStabilityTests
{
    private static SteelSectionInput Section(Section shape,double fy=355,bool welded=false,bool cold=false)
        => new(new SteelSection(shape,new SteelMaterial("nominal",210000,fy,550),
            welded?GPC.Model.Sections.Section.SectionTypes.Welded:GPC.Model.Sections.Section.SectionTypes.Rolled,
            cold?GPC.Model.Sections.Section.FormedTypes.ColdFormed:GPC.Model.Sections.Section.FormedTypes.HotFinished),true);
    // Direct transcription of the independently inspected EN1993-1-1:2005 Table6.2
    // p58 / NTC2018 Table4.2.VIII p102. Axis expectations are geometric here.
    [DataTestMethod]
    [DataRow(600d,300d,40d,355d,false,SteelBucklingCurve.A,SteelBucklingCurve.B)]
    [DataRow(600d,300d,40.001,355d,false,SteelBucklingCurve.B,SteelBucklingCurve.C)]
    [DataRow(600d,300d,100d,355d,false,SteelBucklingCurve.B,SteelBucklingCurve.C)]
    [DataRow(360d,300d,40d,355d,false,SteelBucklingCurve.B,SteelBucklingCurve.C)]
    [DataRow(360.001,300d,40d,355d,false,SteelBucklingCurve.A,SteelBucklingCurve.B)]
    [DataRow(360d,300d,100.001,355d,false,SteelBucklingCurve.D,SteelBucklingCurve.D)]
    [DataRow(600d,300d,40d,460d,false,SteelBucklingCurve.A0,SteelBucklingCurve.A0)]
    [DataRow(600d,300d,40.001,460d,false,SteelBucklingCurve.A,SteelBucklingCurve.A)]
    [DataRow(360d,300d,100.001,460d,false,SteelBucklingCurve.C,SteelBucklingCurve.C)]
    [DataRow(600d,300d,40d,355d,true,SteelBucklingCurve.B,SteelBucklingCurve.C)]
    [DataRow(600d,300d,40.001,460d,true,SteelBucklingCurve.C,SteelBucklingCurve.D)]
    [DataRow(100d,500d,20d,355d,false,SteelBucklingCurve.B,SteelBucklingCurve.C)]
    public void CurvesUseThicknessRatioGradeFabricationAndPrincipalAxes(double h,double b,double tf,double fy,bool welded,SteelBucklingCurve y,SteelBucklingCurve z)
    {
        var input=Section(new SectionH(h,12,b,tf,b,tf,"table"),fy,welded);
        bool majorIsY=input.CreateSectionCopy().Jxx>=input.CreateSectionCopy().Jyy;
        foreach(var code in new[]{SectionResistanceStandard.Eurocode3_2005,SectionResistanceStandard.Ntc2018}) {
            var r=SteelAutomaticStability.CompressionCurves(input,code);
            Assert.AreEqual(majorIsY?y:z,r.First);Assert.AreEqual(majorIsY?z:y,r.Second);
            Assert.AreEqual((SteelBucklingCurve)Math.Max((int)y,(int)z),r.Torsional);
            StringAssert.Contains(r.Source,code==SectionResistanceStandard.Ntc2018?"4.2.VIII":"6.2");
        }
    }
    [DataTestMethod]
    [DataRow("RHS",false,355d,SteelBucklingCurve.A)] [DataRow("CHS",false,460d,SteelBucklingCurve.A0)]
    [DataRow("RHS",true,355d,SteelBucklingCurve.C)] [DataRow("CHS",true,460d,SteelBucklingCurve.C)]
    [DataRow("C",false,235d,SteelBucklingCurve.C)] [DataRow("T",false,355d,SteelBucklingCurve.C)]
    [DataRow("L",false,355d,SteelBucklingCurve.B)] [DataRow("Rectangular",false,355d,SteelBucklingCurve.C)]
    [DataRow("Circular",false,355d,SteelBucklingCurve.C)]
    public void FamilyRows(string family,bool cold,double fy,SteelBucklingCurve expected)
    {
        var r=SteelAutomaticStability.CompressionCurves(Section(CommonSectionCoverageTests.Shape(family),fy,cold:cold),SectionResistanceStandard.Eurocode3_2005);
        Assert.AreEqual(expected,r.First);Assert.AreEqual(expected,r.Second);
    }
    [DataTestMethod] [DataRow("H")] [DataRow("C")] [DataRow("T")] [DataRow("L")]
    public void CoupledCompressionMatchesIndependentSymmetricMatrixEigenproblem(string family)
    {
        // Same sine-mode physical model, independent dense eigensolver instead of
        // the production determinant/bisection. Section properties are separately
        // benchmarked in the Model and legacy regression suites.
        var s=Section(CommonSectionCoverageTests.Shape(family));var shape=s.CreateSectionCopy();
        double l=3200,e=s.ElasticModulus,g=e/(2*(1+s.PoissonRatio)),c=Math.Cos(shape.AngleX1),sn=Math.Sin(shape.AngleX1);
        double dx=shape.ShearCenter.X-shape.Centroid.X,dy=shape.ShearCenter.Y-shape.Centroid.Y;
        double x=c*dx+sn*dy,y=-sn*dx+c*dy,r2=(shape.J11+shape.J22)/shape.Area+x*x+y*y;
        double n1=e*shape.J11*Math.Pow(Math.PI/l,2),n2=e*shape.J22*Math.Pow(Math.PI/l,2),nt=(g*shape.Jt+e*shape.Jw*Math.Pow(Math.PI/l,2))/r2;
        var b=DenseMatrix.OfArray(new[,]{{1/n1,0d,x/Math.Sqrt(r2*n1*nt)},{0d,1/n2,y/Math.Sqrt(r2*n2*nt)},{x/Math.Sqrt(r2*n1*nt),y/Math.Sqrt(r2*n2*nt),1/nt}});
        double expected=1/b.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Max(v=>v.Real);
        foreach(var ntc in new[]{false,true}) {
            var baseInput=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc,-10000);
            var input=new SteelVerificationInput(s,baseInput.Actions,baseInput.Code,new SteelMemberInput(l,l,l));
            var r=SteelAutomaticStability.UniformFork(input,false,"declared two forks and free warping").ToDictionary(v=>v.Quantity);
            Assert.AreEqual(expected,r[SteelStabilityQuantity.NcrFT].Value,expected*2e-12);
            Assert.AreEqual(n1,r[SteelStabilityQuantity.Ncr1].Value,n1*2e-12);
            Assert.AreEqual(n2,r[SteelStabilityQuantity.Ncr2].Value,n2*2e-12);
            Assert.AreEqual(nt,r[SteelStabilityQuantity.NcrT].Value,nt*2e-12);
        }
    }
    [TestMethod]
    public void UniformMomentMatchesRayleighTwoDegreeOfFreedomCriticalEquation()
    {
        var s=Section(CommonSectionCoverageTests.Shape("H"));var shape=s.CreateSectionCopy();
        double l=5000,k=Math.PI/l,g=210000/(2*(1+s.PoissonRatio));
        double m=Math.Sqrt(210000*shape.J22*k*k*(g*shape.Jt+210000*shape.Jw*k*k));
        var input=new SteelVerificationInput(s,new SteelActions(0,0,0,0,-1e7,0),SteelDesignContext.Ntc2018(),new SteelMemberInput(l));
        Assert.AreEqual(m,SteelAutomaticStability.UniformFork(input,true,"uniform moment, forks").Single().Value,m*2e-12);
    }
    [TestMethod]
    public void AutomaticPreparationRejectsUnqualifiedGradeGeometryActionsAndLengths()
    {
        Assert.ThrowsException<NotSupportedException>(()=>SteelAutomaticStability.CompressionCurves(Section(CommonSectionCoverageTests.Shape("H"),450),SectionResistanceStandard.Ntc2018));
        Assert.ThrowsException<NotSupportedException>(()=>SteelAutomaticStability.CompressionCurves(Section(new SectionH(600,12,300,101,300,101,"outside table")),SectionResistanceStandard.Ntc2018));
        var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("H"),false,-10000);
        Assert.ThrowsException<ArgumentException>(()=>SteelAutomaticStability.UniformFork(new(b.Section,b.Actions,b.Code,new SteelMemberInput(3000,6000,3000)),false,"forks"));
        Assert.ThrowsException<NotSupportedException>(()=>SteelAutomaticStability.UniformFork(new(b.Section,new SteelActions(-10000,0,0,0,1,0),b.Code,new SteelMemberInput(3000)),false,"forks"));
        Assert.ThrowsException<ArgumentException>(()=>SteelAutomaticStability.UniformFork(new(b.Section,b.Actions,b.Code,new SteelMemberInput(3000)),false,""));
    }
}
