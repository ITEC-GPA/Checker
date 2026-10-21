using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class AngleChannelQualificationTests
{
    // EC3:2005/AC:2009 Table 5.2 sheet 3, p44. Whole-angle dimensions.
    [DataTestMethod]
    [DataRow(115d, 115d, 10d, 3)] [DataRow(115.001, 115.001, 10d, 4)]
    [DataRow(150d, 80d, 10d, 3)] [DataRow(150.001, 79d, 10d, 4)]
    [DataRow(120d, 120d, 10d, 4)] [DataRow(80d, 150d, 10d, 3)]
    [DataRow(80d, 80d, 10d, 3)]
    public void AngleWholeSectionLimits(double h, double b, double t, int expected)
        => Assert.AreEqual(expected, Ec3AngleCompression.Classify(h, b, t, 235));

    public static IEnumerable<object[]> Grades =>
        from fy in new[] {235d, 275d, 355d, 420d, 460d}
        from ratio in new[] {11.49, 11.51}
        select new object[] {fy, ratio};

    [DataTestMethod, DynamicData(nameof(Grades))]
    public void GradeDependenceUsesExactEpsilon(double fy, double ratio)
    {
        double h = ratio * Math.Sqrt(235 / fy) * 10;
        Assert.AreEqual(ratio < 11.5 ? 3 : 4, Ec3AngleCompression.Classify(h, h, 10, fy));
    }

    [TestMethod]
    public void RadiusNeverReducesWholeAngleDimensions()
    {
        Assert.AreEqual(4, Ec3AngleCompression.Classify(120, 120, 10, 235, 12));
        Assert.AreEqual(3, Ec3AngleCompression.Classify(150, 80, 10, 235, 12));
    }

    [DataTestMethod]
    [DataRow(90d,9d)] [DataRow(80d,8d)]
    public void PublishedSbeSs6AnglesAreClass4(double leg,double t)
    {
        // CTICM/SCI SBE_SS6, Appendix A pp6-29/6-30: S355 angles
        // L90x90x9 and L80x80x8 are class4 despite full effective area
        // in that separately evaluated example. No Ag=Aeff shortcut here.
        Assert.AreEqual(4,Ec3AngleCompression.Classify(leg,leg,t,355));
        var s=new SteelSection(new SectionL(leg,t,leg,t,""),new SteelMaterial("S355",210000,355,510));
        Assert.AreEqual(4,new ReferenceProbe(s).Class(-1000,0));
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidAngleDimensionsRejected(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec3AngleCompression.Classify(value, 100, 10, 235));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec3AngleCompression.Classify(100, 100, value, 235));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec3AngleCompression.Classify(100, 100, 10, value));
    }

    public static IEnumerable<object[]> Members =>
        from family in new[] {"L", "UnequalL", "C"}
        from length in new[] {500d, 3000d, 12000d}
        from gamma in new[] {1d, 1.05}
        select new object[] {family, length, gamma};

    [DataTestMethod, DynamicData(nameof(Members))]
    public void CompleteCompressionAgainstIndependentGeometricAxisEigenproblem(string family, double length, double gamma)
    {
        // Geometry oracle: rectangular area integrals, independent of section properties.
        var rectangles = family == "C"
            ? new[] {(0d,0d,100d,15d), (0d,15d,10d,270d), (0d,285d,100d,15d)}
            : new[] {(0d,0d,100d,20d), (0d,20d,20d,family == "L" ? 80d : 130d)};
        double area = rectangles.Sum(r => r.Item3*r.Item4);
        double cx = rectangles.Sum(r => r.Item3*r.Item4*(r.Item1+r.Item3/2))/area;
        double cy = rectangles.Sum(r => r.Item3*r.Item4*(r.Item2+r.Item4/2))/area;
        double ix = rectangles.Sum(r => r.Item3*Math.Pow(r.Item4,3)/12+r.Item3*r.Item4*Math.Pow(r.Item2+r.Item4/2-cy,2));
        double iy = rectangles.Sum(r => r.Item4*Math.Pow(r.Item3,3)/12+r.Item3*r.Item4*Math.Pow(r.Item1+r.Item3/2-cx,2));
        double ixy = rectangles.Sum(r => r.Item3*r.Item4*(r.Item1+r.Item3/2-cx)*(r.Item2+r.Item4/2-cy));
        Section shape = family == "C" ? new SectionC(300,10,100,15,100,15,"") : new SectionL(100,20,family == "L" ? 100 : 150,20,"");
        var section = new SteelSection(shape, new SteelMaterial("S235",210000,235,360));
        var actual = Ec3OpenSectionCompression.Evaluate(section,length,235,gamma,"explicit test parameters");
        // Torsion/shear-centre inputs are independently qualified by CISC tests in
        // TeeTorsionReferenceTests; this oracle independently assembles the coupling.
        double dx=section.ShearCenter.X-cx,dy=section.ShearCenter.Y-cy;
        double i0=Math.Sqrt((ix+iy)/area+dx*dx+dy*dy), k=Math.PI*Math.PI*210000/(length*length);
        double nt=(210000/(2*(1+section.SteelMaterial.Ni))*section.Jt+k*section.Jw)/(i0*i0);
        var stiffness=DenseMatrix.OfArray(new[,]{{k*ix,-k*ixy,0},{-k*ixy,k*iy,0},{0,0,nt}});
        var g=DenseMatrix.OfArray(new[,]{{1d,0,dx/i0},{0,1d,dy/i0},{dx/i0,dy/i0,1d}});
        var eig=stiffness.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);
        var inv=eig.EigenVectors*DenseMatrix.OfDiagonalArray(eig.EigenValues.Select(v=>1/Math.Sqrt(v.Real)).ToArray())*eig.EigenVectors.Transpose();
        double ncr=1/(inv*g*inv).Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Max(v=>v.Real);
        double lambda=Math.Sqrt(area*235/ncr),alpha=family=="C"?.49:.34;
        double phi=(1+alpha*(lambda-.2)+lambda*lambda)/2;
        double chi=lambda<=.2?1:Math.Min(1,1/(phi+Math.Sqrt(phi*phi-lambda*lambda)));
        Assert.AreEqual(ncr,actual.Ncr,ncr*1e-10);
        Assert.AreEqual(chi*area*235/gamma,actual.NbRd,actual.NbRd*1e-10);
        Assert.AreEqual(alpha,actual.Alpha);
    }

    [TestMethod]
    public void UnqualifiedInputsNeverReturnResistance()
    {
        var material=new SteelMaterial("S235",210000,235,360);
        Assert.ThrowsException<NotSupportedException>(()=>Ec3OpenSectionCompression.Evaluate(new SteelSection(new SectionL(120,10,120,10,""),material),3000,235,1,"test"));
        Assert.ThrowsException<NotSupportedException>(()=>Ec3OpenSectionCompression.Evaluate(new SteelSection(new SectionL(100,10,100,12,""),material),3000,235,1,"test"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3OpenSectionCompression.Evaluate(new SteelSection(new SectionC(300,10,100,15,100,15,""),material),0,235,1,"test"));
    }
}
