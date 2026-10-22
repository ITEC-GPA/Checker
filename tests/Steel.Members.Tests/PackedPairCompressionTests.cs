using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class PackedPairCompressionTests
{
    internal static SectionBuiltUp Pair(string kind,double gap=10) => kind=="C"
        ?SectionBuiltUp.DoubleChannel(new SectionC(200,10,75,12,75,12,"C"),gap,true,"2C")
        :SectionBuiltUp.DoubleAngle(new SectionL(100,12,150,12,"L"),gap,
            kind=="L-long"?SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack:SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack,"2L");
    internal static SteelVerificationInput Input(string kind,bool ntc=false,double gap=10)
        =>new(new SteelSectionInput(new SteelSection(Pair(kind,gap),new SteelMaterial("S235",210000,235,360)),true),
            new SteelActions(-100000,0,0,0,0,0),ntc?SteelDesignContext.Ntc2018():new SteelDesignContext(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),
            new SteelMemberInput(3000,3000,3000));
    internal static SteelPackedPairAssessment Packing(SteelVerificationInput i,double pitch=100,double gap=10,double cr=200000,double connection=20000)
        =>new(i,pitch,gap,gap,60000,cr,0,200000,10000,connection,
            "backs connected at ends and every pitch, packed gussets", "independent second-order local force envelope, same state",
            "component stability and complete connection/plate design, same code");
    private static SteelMemberAnalysis Analysis(SteelPackedPairAssessment p,bool modes=true,int? cl=null)
        =>SteelMemberAnalysis.PackedCompression(p,"centroidal constant N","explicit curves C",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:modes?"independent T assessment":null,flexuralTorsionalExclusionSource:modes?"independent FT assessment":null,
            memberClass:cl,classificationSource:cl.HasValue?"external nominal component class":null);
    private static SteelCheckExecution Run(SteelMemberAnalysis a) =>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();

    // Independent rectangle integration, including product of inertia for the unequal angle.
    private static (double A,double X,double Y,double Ix,double Iy,double Ixy,double Rmin) Chord(string kind)
    {
        var rectangles=kind=="C"?new[]{(0d,0d,75d,12d),(0d,12d,10d,176d),(0d,188d,75d,12d)}
            :new[]{(0d,0d,100d,12d),(0d,12d,12d,138d)};
        double a=rectangles.Sum(r=>r.Item3*r.Item4);
        double x=rectangles.Sum(r=>r.Item3*r.Item4*(r.Item1+r.Item3/2))/a;
        double y=rectangles.Sum(r=>r.Item3*r.Item4*(r.Item2+r.Item4/2))/a;
        double ix=0,iy=0,xy=0;
        foreach(var (rx,ry,b,h) in rectangles) {
            double dx=rx+b/2-x,dy=ry+h/2-y;
            ix+=b*h*h*h/12+b*h*dy*dy;iy+=h*b*b*b/12+b*h*dx*dx;xy+=b*h*dx*dy;
        }
        double jmin=(ix+iy)/2-Math.Sqrt(Math.Pow((ix-iy)/2,2)+xy*xy);
        return (a,x,y,ix,iy,xy,Math.Sqrt(jmin/a));
    }
    [DataTestMethod]
    [DataRow("C",false)] [DataRow("C",true)] [DataRow("L-long",false)] [DataRow("L-long",true)]
    [DataRow("L-short",false)] [DataRow("L-short",true)]
    public void EulerAndDesignResistanceUseWholePairAxesAndExcludePackingArea(string kind,bool ntc)
    {
        var i=Input(kind,ntc);var r=Run(Analysis(Packing(i)));
        Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(SteelExecutionStatus.Passed,r.Status);
        var c=Chord(kind);double ix=2*c.Ix,iy=2*c.Iy;
        if(kind=="L-short")ix+=2*c.A*Math.Pow(c.Y+5,2);else iy+=2*c.A*Math.Pow(c.X+5,2);
        double n1=Math.PI*Math.PI*210000*Math.Max(ix,iy)/9e6,n2=Math.PI*Math.PI*210000*Math.Min(ix,iy)/9e6;
        double Value(string n)=>r.Trace.Single(t=>t.Name==n).Value;
        Assert.AreEqual(2*c.A,Value("Area"),1e-8);
        Assert.AreEqual(n1,Value("Ncr1"),n1*1e-10);Assert.AreEqual(n2,Value("Ncr2"),n2*1e-10);
        Assert.AreEqual(15*c.Rmin,Value("PackedPair.PitchLimit"),1e-8);
        double lambda=Math.Sqrt(2*c.A*235/n2),phi=.5*(1+.49*(lambda-.2)+lambda*lambda);
        double chi=Math.Min(1,1/(phi+Math.Sqrt(phi*phi-lambda*lambda)));
        double global=100000/(chi*2*c.A*235/(ntc?1.05:1));
        Assert.AreEqual(Math.Max(.5,global),r.Utilization!.Value,1e-10);
    }
    [DataTestMethod] [DataRow("pitch")] [DataRow("gap")] [DataRow("wide")] [DataRow("class4")] [DataRow("modes")]
    public void UnqualifiedDetailingAndMissingModesNeverPass(string reason)
    {
        var i=Input("L-long",gap:reason=="wide"?40:10);
        var r=Run(Analysis(Packing(i,pitch:reason=="pitch"?1000:100,gap:reason=="gap"?12:reason=="wide"?40:10),reason!="modes",reason=="class4"?4:null));
        Assert.IsFalse(r.IsEvaluated);Assert.AreNotEqual(SteelExecutionStatus.Passed,r.Status);
    }
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void ComponentOrConnectionFailureControlsDespiteGlobalPass(bool component)
    {
        var i=Input("C");var r=Run(Analysis(Packing(i,cr:component?30000:200000,connection:component?20000:5000)));
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);Assert.AreEqual(2d,r.Utilization!.Value);
    }
    [TestMethod]
    public void PlainCompressionCannotBypassPackingQualification()
    {
        var i=Input("C");var a=SteelMemberAnalysis.Compression(i,"N","C",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"T",flexuralTorsionalExclusionSource:"FT");
        Assert.AreEqual("Member.PackingAssessmentRequired",Run(a).DiagnosticCode);
    }
}
