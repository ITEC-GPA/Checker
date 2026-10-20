using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;
using SC = GPC.Checkers.Steel.Checkers.EN1993p11Checker.SectionClass;

namespace Steel.Members.Tests;

// Published numerical data, NOT snapshots of GPC results. N, mm, MPa.
// See BUCKLING_BENCHMARKS.txt for sources, page numbers, scope and rounding policy.
[TestClass]
public class PublishedBucklingBenchmarks
{
    public record Column(string Id, double H, double B, double Tw, double Tf, double R,
        double Fy, double A, double Iy, double Iz, double Ly, double Lz,
        int Class, string Cy, string Cz, double? LambdaY, double? LambdaZ,
        double? PhiY, double? PhiZ, double? ChiY, double? ChiZ,
        double? NbY = null, double? NbZ = null, bool Hollow = false);

    public static readonly Column[] Columns =
    {
        // SCI P364, Ex.9, pp.97-102; radii in the section table converted to I=A*i^2.
        new("SCI364-9",355.6,368.6,10.4,17.5,15.2,345,16400,
            16400*156d*156,16400*94.3*94.3,6000,6000,3,"b","c",
            null,.82,null,.99,null,.65,5010000,3678000),
        // SCI P364 Ex.10 pp.104-107: intermediate restraint only about z-z.
        new("SCI364-10",307.9,305.3,9.9,15.4,15.2,275,12300,
            12300*134d*134,12300*76.9*76.9,6000,3000,2,"b","c",
            .52,.45,.69,.66,.87,.88,2943000),
        // SCI P364 Ex.11 pp.112,116-117 and Ex.13 pp.143-145 share these axial terms.
        new("SCI364-11-and-13",203.2,203.6,7.2,11,10.2,275,5870,
            5870*88.2*88.2,5870*51.3*51.3,5000,5000,1,"b","c",
            .66,1.13,.80,1.37,.80,.47,1291000,759000),
        // Ex.12 pp.126,131: Class 3 belongs to the original N+M state, not pure N.
        new("SCI364-12",453.4,189.9,8.5,12.7,10.2,355,8550,
            8550*185d*185,8550*41.2*41.2,6000,3000,3,"a","b",
            .43,.96,.62,1.09,.94,.62),
        // CIC 2015, Ex.II-3 D26-D28 and Ex.II-4 D30-D33 share these axial terms.
        new("CIC-II-3-and-II-4",254.1,254.6,8.6,14.2,12.7,355,9310,
            114e6,39.1e6,9000,6300,2,"b","c",1.06,1.27,1.21,1.57,.56,.40,
            1850800,1322000),
        // CIC II-5 D37-D39 uses Iz=1548 cm4 in the substitution, 1550 in its table.
        new("CIC-II-5",203.2,203.6,7.2,11,10.2,275,5870,
            45.7e6,15.48e6,5000,5000,1,"b","c",null,1.12,null,1.35,null,.48,
            null,774000),
        // SCI P374 Ex.2 pp.18-21: hot-finished SHS, not cold-formed.
        new("SCI374-2",200,200,6.3,6.3,0,355,4840,
            4840*78.9*78.9,4840*78.9*78.9,6000,6000,2,"a","a",
            1,1,1.08,1.08,.67,.67,1151000,1151000,true),
        // SCI P374 Ex.5 pp.37,40. Only the axial terms of the beam-column check.
        new("SCI374-5",150,150,6.3,6.3,0,355,3580,
            3580*58.5*58.5,3580*58.5*58.5,3500,3500,1,"a","a",
            .79,.79,.87,.87,.81,.81,null,null,true),
        // SCI P374 Ex.6 pp.46,50-51: chord restrained at different spacings.
        new("SCI374-6",150,150,5,5,0,355,2870,
            1000e4,1000e4,3600,1800,2,"a","a",
            .80,.40,.88,.60,.80,.95,null,null,true),
        // SCI P374 Ex.7 pp.57,59. Same section, different restraint spacing.
        new("SCI374-7",150,150,5,5,0,355,2870,
            1000e4,1000e4,3500,3500,2,"a","a",
            .78,.78,.87,.87,.80,.80,815000,815000,true)
    };

    public static IEnumerable<object[]> FlexuralCases =>
        from c in Columns from axis in new[] { 1, 2 } select new object[] { c.Id, axis };

    [DataTestMethod, DynamicData(nameof(FlexuralCases))]
    public void PublishedFlexuralBuckling(string id, int axis)
    {
        var c = Columns.Single(x => x.Id == id);
        var s = TabulatedSection.For(c);
        var p = new BucklingProbe(s, c.Ly, c.Lz);
        var a = p.Flexural(s, c.Class, axis);
        Assert.AreEqual(axis == 1 ? c.Cy : c.Cz, a.Curve, id);
        // Each book rounds lambda/Phi/chi during evaluation. Propagate the printed
        // lambda and Phi half-units rather than using a blanket percentage tolerance.
        double? lambda = axis == 1 ? c.LambdaY : c.LambdaZ;
        double? phi = axis == 1 ? c.PhiY : c.PhiZ;
        double? chi = axis == 1 ? c.ChiY : c.ChiZ;
        if (lambda.HasValue) Assert.AreEqual(lambda.Value, a.Lambda, .01, id + " lambda");
        if (phi.HasValue)
        {
            double lambdaRound = lambda!.Value;
            // Include the .005 printed rounding and propagation of lambda's last digit.
            double phiBound = .005 + (lambdaRound + .5 * a.Alpha) * .01 + .00005;
            Assert.AreEqual(phi.Value, a.Phi, phiBound, id + " Phi");
        }
        double chiLow=0, chiHigh=1;
        if (phi.HasValue && lambda.HasValue)
        {
            // Independent envelope: propagate printed digits, never actual SUT outputs.
            chiLow = Math.Min(chi!.Value-.005,Reduction(phi.Value+.005,lambda.Value-.01));
            chiHigh = Math.Max(chi.Value+.005,Reduction(phi.Value-.005,lambda.Value+.01));
        }
        if (chi.HasValue) Assert.IsTrue(a.Chi>=chiLow && a.Chi<=chiHigh,
            $"{id} chi {a.Chi:R} outside published rounding envelope [{chiLow:R},{chiHigh:R}]");
        double? nb = axis == 1 ? c.NbY : c.NbZ;
        if (nb.HasValue)
        {
            // Propagate the independently bounded chi to the resistance.
            // For the Blue Book value only (Ex.9 y), allow 0.3% table-property rounding.
            if (chi.HasValue)
                Assert.IsTrue(a.Nb>=chiLow*c.A*c.Fy-1000 && a.Nb<=chiHigh*c.A*c.Fy+1000,
                    id+" Nb,Rd outside propagated rounding envelope");
            else Assert.AreEqual(nb.Value,a.Nb,nb.Value*.003,id+" Blue Book Nb,Rd");
            if (chi.HasValue) Assert.AreEqual(nb.Value,chi.Value*c.A*c.Fy,1000,id+" source units");
        }
        Assert.IsTrue(a.Chi is > 0 and <= 1);
        Assert.IsTrue(a.Nb <= c.A*c.Fy);
    }

    // A separate tight check prevents the rounded publication comparisons from
    // concealing a defect in the reduction formula.
    [DataTestMethod, DynamicData(nameof(FlexuralCases))]
    public void PublishedInputsAgainstIndependentCharacteristicEquation(string id, int axis)
    {
        var c = Columns.Single(x => x.Id == id);
        var s = TabulatedSection.For(c);
        var p = new BucklingProbe(s, c.Ly, c.Lz);
        var a = p.Flexural(s, c.Class, axis);
        string expectedCurve = axis == 1 ? c.Cy : c.Cz;
        double alpha = expectedCurve switch { "a" => .21, "b" => .34, "c" => .49, _ => throw new Exception() };
        double inertia = axis == 1 ? c.Iy : c.Iz, length = axis == 1 ? c.Ly : c.Lz;
        double ncr = Math.PI*Math.PI*210000*inertia/(length*length);
        double lambda = Math.Sqrt(c.A*c.Fy/ncr);
        // Solve lambda^2*chi^2 - (1+alpha*(lambda-.2)+lambda^2)*chi + 1 = 0
        // by bisection on the physical interval, independently of the production radical.
        double lo=0, hi=Math.Min(1,1/(lambda*lambda));
        for (int i=0;i<100;i++)
        {
            double x=(lo+hi)/2;
            double residual=lambda*lambda*x*x-(1+alpha*(lambda-.2)+lambda*lambda)*x+1;
            if (residual>0) lo=x; else hi=x;
        }
        Assert.AreEqual(ncr,a.Ncr,ncr*1e-12,id+" Euler");
        Assert.AreEqual(lambda,a.Lambda,1e-12,id+" lambda");
        Assert.AreEqual((lo+hi)/2,a.Chi,1e-12,id+" chi");
        Assert.AreEqual((lo+hi)/2*c.A*c.Fy,a.Nb,c.A*c.Fy*1e-12,id+" Nb");
    }

    // Reduction stage only: explicitly assigned NA coefficients and published lambda.
    // This does NOT qualify Mcr generation or the complete N-M interaction.
    [DataTestMethod]
    [DataRow("SCI364-3 p27", 1.07, .49, .75, .4, .61, 1.09, .60, .83, .72)]
    [DataRow("SCI364-4 p38", .62, .49, .75, .4, .92, .70, .87, .96, double.NaN)]
    [DataRow("SCI364-5 p47-48 class3", .92, .34, .75, .4, .87, .91, .74, .94, .79)]
    [DataRow("SCI364-7 p71-72 continuous", .46, .49, .75, .4, .79, .59, .98, .92, 1d)]
    [DataRow("SCI364-11 p118", .76, .34, .75, .4, 1d, .78, .83, 1d, .83)]
    [DataRow("SCI364-12 p133 class3", .61, .49, .75, .4, .75, .69, .88, .88, 1d)]
    [DataRow("SCI364-13 p146", .63, .34, .75, .4, 1d, .69, .90, 1d, .90)]
    [DataRow("CIC-II-2 B2-I D19", 2.47, .49, .75, .4, .94, 3.29, .16, 1d, .16)]
    [DataRow("CIC-II-2 B2-II D21", 1.31, .49, .75, .4, 1d, 1.37, .47, 1d, .47)]
    [DataRow("CIC-II-4 D33 general", .69, .21, 1d, .2, 1d, .79, .85, 1d, .85)]
    [DataRow("CIC-II-2 B3-II D25", 1.57, .49, .75, .4, 1d, 1.71, .36, 1d, .36)]
    [DataRow("CIC-II-5 D39", .57, .34, .75, .4, 1d, .65, .93, 1d, .93)]
    public void PublishedLtbReductionStage(string source, double lambda, double alpha,
        double beta, double lambda0, double kc, double phi, double chi, double f, double modified)
    {
        // SCI364-4 prints .92 after dividing .88/.96, but the preceding chi is .87.
        // That inconsistent final value is excluded, not replaced by a new expected.
        var p = new BucklingProbe(TabulatedSection.For(Columns[0]),6000,6000);
        var a = p.Ltb(lambda,alpha,beta,lambda0,kc);
        Assert.AreEqual(phi,a.Phi,.005,source+" Phi");
        // Evaluate chi from the printed Phi to bound error due to its 2-decimal rounding.
        double chiLow=Math.Min(chi-.005,Math.Min(Reduction(phi+.005,lambda,beta),1/(lambda*lambda)));
        double chiHigh=Math.Max(chi+.005,Math.Min(Reduction(phi-.005,lambda,beta),1/(lambda*lambda)));
        Assert.IsTrue(a.Chi>=chiLow && a.Chi<=chiHigh,source+" chi");
        Assert.AreEqual(f,a.F,.005,source+" f");
        if (double.IsFinite(modified))
        {
            double lower=Math.Min(chiLow/(f+.005),1/(lambda*lambda));
            double upper=Math.Min(1,Math.Min(chiHigh/(f-.005),1/(lambda*lambda)));
            Assert.IsTrue(a.Modified>=Math.Min(lower,modified-.005) &&
                a.Modified<=Math.Max(upper,modified+.005),source+" modified");
        }
        Assert.IsTrue(a.Modified<=1 && a.Modified<=1/(lambda*lambda)+1e-14);
    }

    [TestMethod]
    public void CicB3LongBeamRawReductionAndSeparateEc3EulerCap()
    {
        // CIC D23 prints the uncapped .18. EC3 eq.(6.57) additionally requires
        // chi <= 1/lambda^2=.173611... . Do not validate the book's 70.3 kNm as EC3.
        var p=new BucklingProbe(TabulatedSection.For(Columns[0]),10000,10000);
        Assert.AreEqual(3.15,p.Ltb(2.4,.49,.75,.4,1).Phi,.005);
        Assert.AreEqual(.18,p.RawReduction(3.15,2.4,.75),.005);
        Assert.AreEqual(1/(2.4*2.4),p.Ltb(2.4,.49,.75,.4,1).Chi,1e-14);
    }

    [DataTestMethod]
    [DataRow("CIC-II-2 D18",10000d,6.45e6,214000d,311e9,1.132,63.8e6)]
    [DataRow("CIC-II-4 D32",6300d,39.1e6,576000d,562e9,1.879,740.2e6)]
    [DataRow("SCI364-13 p146 inputs",5000d,15.5e6,222000d,143e9,1.77,double.NaN)]
    [DataRow("SCI364-5 p47 uniform moment",9000d,12300*76.9*76.9,912000d,1.56e12,1d,double.NaN)]
    [DataRow("SCI374-4 p35 inputs warping neglected",5000d,38.7e6,88.7e6,0d,1.127,double.NaN)]
    public void ElasticCriticalMomentUsingPublishedProperties(string source,double length,
        double iz,double it,double iw,double c1,double published)
    {
        var s=TabulatedSection.For(Columns[0]); s.J22=iz; s.It=it; s.Iw=iw;
        var p=new BucklingProbe(s,length,length);
        double actual=p.Mcr(s,length,c1);
        // Independent product form: C1*sqrt(Ncr,z*(G*It+pi^2*E*Iw/L^2)).
        double euler=Math.PI*Math.PI*210000*iz/(length*length);
        double expected=c1*Math.Sqrt(euler*(210000/2.6*it+Math.PI*Math.PI*210000*iw/(length*length)));
        Assert.AreEqual(expected,actual,expected*1e-12,source);
        if (double.IsFinite(published))
        {
            // Exact effect of G=81000 in the publication, plus its 0.1 kNm digit.
            double sourceG=c1*Math.Sqrt(euler*(81000*it+Math.PI*Math.PI*210000*iw/(length*length)));
            Assert.AreEqual(published,sourceG,50000,source+" published arithmetic");
            Assert.AreEqual(published,actual,Math.Abs(sourceG-expected)+50000,source);
        }
    }

    [DataTestMethod]
    [DataRow(.4)] [DataRow(.4-1e-8)] [DataRow(.4+1e-8)]
    public void LtbPlateauAtTheLimitUsedInSciContinuousBeam(double lambda)
    {
        var p=new BucklingProbe(TabulatedSection.For(Columns[0]),3000,3000);
        var a=p.Ltb(lambda,.49,.75,.4,1);
        if (lambda<=.4) Assert.AreEqual(1d,a.Modified,1e-14);
        else Assert.IsTrue(a.Modified<1 && a.Modified>1-1e-7);
    }

    [DataTestMethod]
    [DataRow(0,6000d,153e4,4.18e12,11e6,.72,.89,.71)]
    [DataRow(1,3000d,91.2e4,1.56e12,18.1e6,.43,.65,.88)]
    public void PublishedTorsionalCriticalForce(int column, double lt, double it, double iw,
        double ncr, double lambda, double phi, double chi)
    {
        var s=TabulatedSection.For(Columns[column]); s.It=it; s.Iw=iw;
        var p=new BucklingProbe(s,lt,lt);
        var a=p.Torsion(s);
        // SCI uses G=81000; GPC derives G=E/(2*(1+.3))=80769.23.
        // Half a unit at 0.1 MN plus that independently known shear-modulus difference.
        double ncrBound=50000+Math.Abs(81000-210000/2.6)*it/(s.R11*s.R11+s.R22*s.R22);
        Assert.AreEqual(ncr,a.Ncr,ncrBound,"SCI P364 torsion");
        Assert.AreEqual(lambda,a.Lambda,.01);
        Assert.AreEqual(phi,a.Phi,.015);
        Assert.AreEqual(chi,a.Chi,.015);
    }

    private static double Reduction(double phi,double lambda,double beta=1) =>
        Math.Min(1,1/(phi+Math.Sqrt(phi*phi-beta*lambda*lambda)));

    // Tabulated properties deliberately isolate EC3 from section integration.
    // Shape is used ONLY to select the automatic curve. No computed property is an oracle.
    internal sealed class TabulatedSection : SteelSection, ISteelSection
    {
        public new double Area { get; }
        public new double J11 { get; }
        public new double J22 { get; set; }
        public new double R11 => Math.Sqrt(J22/Area);
        public new double R22 => Math.Sqrt(J11/Area);
        public double It { get; set; }
        public double Iw { get; set; }
        public new double Jt => It;
        public new double Jw => Iw;
        private TabulatedSection(Section shape,double fy,double a,double iy,double iz)
            : base(shape,new SteelMaterial("Published input",210000,fy,Math.Max(550,fy+50)),
                  Section.SectionTypes.Rolled,Section.FormedTypes.HotFinished)
        { Area=a; J11=iy; J22=iz; }
        public static TabulatedSection For(Column c) => new(
            c.Hollow ? new SectionRHS(c.H,c.B,c.Tf,c.Tf,c.Tw,c.Tw,"Published SHS")
                : new SectionH(c.H,c.Tw,c.B,c.Tf,c.B,c.Tf,"Published I/H",c.R),
            c.Fy,c.A,c.Iy,c.Iz);
    }

    internal sealed class BucklingProbe : EN1993p11Checker
    {
        private readonly double ly,lz;
        public BucklingProbe(SteelSection s,double ly,double lz) : base(
            new BeamElement(Point3d.Origin,new Point3d(ly,0,0),s),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded,Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,
                1,1,effectiveLengthFactorAxialBuck2:lz/ly),new StandardEN1993p11())
        { this.ly=ly; this.lz=lz; }
        public string Curve(ISteelSection s,int axis) =>
            (axis==1?GetBucklingCurve1Axis(s):GetBucklingCurve2Axis(s)).ToString();
        public (string Curve,double Alpha,double Ncr,double Lambda,double Phi,double Chi,double Nb)
            Flexural(ISteelSection s,int cl,int axis)
        {
            var curve=axis==1?GetBucklingCurve1Axis(s):GetBucklingCurve2Axis(s);
            double alpha=GetImperfectionFactorBucklingCurve(curve);
            double ncr=GetNcrEuler(axis==1?ly:lz,axis==1?s.J11:s.J22);
            double lambda=GetLambdaSigned((SC)cl,s,ncr),phi=GetPhiForBuckling(alpha,lambda);
            return (curve.ToString(),alpha,ncr,lambda,phi,GetChi(phi,lambda),
                axis==1?CalculateNbRd1(s,(SC)cl):CalculateNbRd2(s,(SC)cl));
        }
        public (double Phi,double Chi,double F,double Modified)
            Ltb(double lambda,double alpha,double beta,double lambda0,double kc)
        {
            double phi=GetPhiForBuckling(alpha,lambda,beta,lambda0);
            double f=GetFactorFForLTB(kc,lambda);
            return (phi,GetChiLTmod(phi,lambda,beta),f,GetChiLTmod(phi,lambda,beta,f));
        }
        public (double Ncr,double Lambda,double Phi,double Chi) Torsion(ISteelSection s)
        {
            double ncr=GetNcrT(s),lambda=GetLambdaSigned(SC.Class3,s,ncr);
            double phi=GetPhiForBuckling(.49,lambda);
            return (ncr,lambda,phi,GetChi(phi,lambda));
        }
        public double RawReduction(double phi,double lambda,double beta) => GetChi(phi,lambda,beta);
        public double Mcr(ISteelSection s,double length,double c1) =>
            CalculateMcr(s,new ResultBeamForces(0,0,0,0,1e6,0,CoordinateSystem.Global),length,
                Opt.SupportConditions.HingesAtEnds,Opt.LoadConditions.NotDirectlyLoaded,
                Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,
                1,Opt.LoadApplicationPoints.ShearCenter,c1,0);
    }
}
