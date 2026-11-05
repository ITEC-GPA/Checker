using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class RestrainedMemberPreparationTests
{
    private static SteelEndRestraints End(bool fixedWarping)=>new(translation1:SteelRestraintState.Fixed,
        translation2:SteelRestraintState.Fixed,rotation1:SteelRestraintState.Fixed,
        rotation2:SteelRestraintState.Fixed,rotation3:SteelRestraintState.Fixed,
        warping:fixedWarping?SteelRestraintState.Fixed:SteelRestraintState.Free);

    [DataTestMethod][DataRow(SteelIdealWarpingBoundary.FreeBothEnds)]
    [DataRow(SteelIdealWarpingBoundary.FixedOneEnd)][DataRow(SteelIdealWarpingBoundary.FixedBothEnds)]
    public void TorsionalEigenvaluesMatchAnIndependentRayleighIntegral(SteelIdealWarpingBoundary boundary)
    {
        var section=MemberSchemePreparationTests.Section();const double length=5000;
        var value=SteelMemberSchemePreparation.TorsionalMode(section,length,boundary,"Ideal twist restraint and stated warping constraints");
        // Independent rectangle/thin-wall integrals of H600x300x12x20.
        double area=2*300*20+560*12;
        double i1=2*(300*Math.Pow(20,3)/12+6000*290*290)+12*Math.Pow(560,3)/12;
        double i2=2*20*Math.Pow(300,3)/12+560*Math.Pow(12,3)/12;
        // It/Iw are INPUTS to this eigenproblem, not recalculated by it. Model
        // includes finite-width torsion and web inertia in Iw; the elementary
        // thin-wall approximation is not a bit-identical oracle of those inputs.
        var properties=section.CreateSectionCopy();
        double it=properties.Jt,iw=properties.Jw;
        Assert.AreEqual(i1,properties.J11,i1*1e-12);Assert.AreEqual(i2,properties.J22,i2*1e-12);
        double q=boundary==SteelIdealWarpingBoundary.FixedBothEnds?2*Math.PI:Math.PI;
        if(boundary==SteelIdealWarpingBoundary.FixedOneEnd) {
            // Newton solve of the endpoint slope residual, independent of the production root constant.
            q=4.5;for(int k=0;k<10;k++)q-=(q*Math.Cos(q)-Math.Sin(q))/(-q*Math.Sin(q));
        }
        // Rayleigh quotient integral(EIw theta''²+GIt theta'²)/integral(i0² theta'²).
        // theta=sin(pi x/L), sin(q x/L)-(x/L)sin(q), or 1-cos(2pi x/L).
        double first=0,second=0;const int steps=20000;
        for(int k=0;k<=steps;k++) {
            double x=(double)k/steps;
            double d1=boundary==SteelIdealWarpingBoundary.FixedBothEnds?q*Math.Sin(q*x)
                :q*Math.Cos(q*x)-(boundary==SteelIdealWarpingBoundary.FixedOneEnd?Math.Sin(q):0);
            double d2=boundary==SteelIdealWarpingBoundary.FixedBothEnds?q*q*Math.Cos(q*x):-q*q*Math.Sin(q*x);
            double weight=k==0||k==steps?1:k%2==0?2:4;
            first+=weight*d1*d1;second+=weight*d2*d2;
        }
        double expected=(210000*iw*second/(first*length*length)+210000/2.6*it)/((i1+i2)/area);
        Assert.AreEqual(expected,value.Value,expected*2e-11);
        Assert.AreEqual(SteelStabilityQuantity.NcrT,value.Quantity);
        StringAssert.Contains(value.Source,boundary.ToString());
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void AddedEndRestraintsUseExplicitUniformLowerBoundWithTheOriginalInput(bool ntc)
    {
        var s=MemberSchemePreparationTests.Section();var member=new SteelMemberInput(6000,startRestraints:End(true),endRestraints:End(true));
        var diagram=SteelForkMomentDiagram.GenericEnvelope(6000,new[]{new SteelMomentStation(0,0),new(1000,-2e7),
            new(2100,1e7),new(4300,-1e7),new(6000,0)},0,"All actual extrema and boundaries from one FEM state");
        var prepared=SteelMemberSchemePreparation.EndRestrainedBendingLowerBound(s,member,diagram,"Both twists/lateral translations fixed; end bending rotations and warping also fixed");
        double iz=2*20*Math.Pow(300,3)/12+560*Math.Pow(12,3)/12;
        var properties=s.CreateSectionCopy();double it=properties.Jt,iw=properties.Jw;
        double a=210000*Math.Pow(Math.PI/6000,2);
        Assert.AreEqual(Math.Sqrt(a*iz*(a*iw+210000/2.6*it)),prepared.UniformMoment.Value,prepared.UniformMoment.Value*1e-12);
        Assert.IsTrue(prepared.IsConservativeLowerBound);Assert.AreSame(member,prepared.Member);
        Assert.AreSame(prepared.ActualDiagram,prepared.UniformMoment);
        var baseInput=CommonSectionCoverageTests.Input((GPC.Model.Sections.Section)s.CreateSectionCopy().SectionShape,ntc,fy:355);
        var input=new SteelVerificationInput(s,new(0,0,0,0,-2e7,0),baseInput.Code,member);
        var analysis=prepared.CreateBendingAnalysis(input,"code policy");
        Assert.AreEqual(SteelCriticalMomentBasis.UniformMoment,analysis.Prismatic.CriticalMomentBasis);
        var result=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis));
        Assert.IsTrue(result.IsComplete,result.Checks.First().Message);
        StringAssert.Contains(analysis.Values.Single().Source,"LOWER BOUND ONLY");
    }

    [TestMethod]
    public void LowerBoundNeverInfersAnUnspecifiedRestraintOrIgnoresLoadHeight()
    {
        var s=MemberSchemePreparationTests.Section();var d=SteelForkMomentDiagram.EndMoments(1e7,-1e7,"moments");
        Assert.ThrowsException<NotSupportedException>(()=>SteelMemberSchemePreparation.EndRestrainedBendingLowerBound(s,new(6000),d,"unknown ends"));
        var m=new SteelMemberInput(6000,startRestraints:End(true),endRestraints:End(false));
        Assert.ThrowsException<NotSupportedException>(()=>SteelMemberSchemePreparation.EndRestrainedBendingLowerBound(s,m,
            SteelForkMomentDiagram.UniformTransverseLoad(1e7,1,"load height"),"ends"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>SteelMemberSchemePreparation.TorsionalMode(s,6000,(SteelIdealWarpingBoundary)42,"invalid"));
        Assert.ThrowsException<ArgumentException>(()=>SteelMemberSchemePreparation.TorsionalMode(s,6000,SteelIdealWarpingBoundary.FixedBothEnds,""));
    }
}
