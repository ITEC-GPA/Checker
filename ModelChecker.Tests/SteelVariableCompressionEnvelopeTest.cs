using GPC.Examples;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

public partial class SteelBeamColumnWorkflowTest
{
    private static void UseAxialEnvelope(Dictionary<string,string> options)
    {
        options["AxialForcePreparation"]="UniformCompressionEnvelope";
        options["AxialForceCoverageSource"]="Synthetic reference: piecewise-linear N, all extrema and discontinuity faces exported in this one state; uniform maximum accepted.";
        options["MomentFactorPreparation"]="ConservativeEnvelope";
        options["MomentDiagramCoverageSource"]="All moment extrema exported, same physical span, same case.";
        foreach(string key in new[]{"Cm1","Cm2","CmLT"})options.Remove(key);
    }

    [DataTestMethod][DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void VariableCompressionMatchesIndependentUniformWorstForceReference(bool ntc,bool changesSign)
    {
        var variable=SteelBeamColumnFrame.Create();var uniform=SteelBeamColumnFrame.Create();
        // This is a bounding-model oracle, not sampled expected output: the most
        // compressive cut is -120kN. N elsewhere is smaller (or tensile); moments,
        // material, section and end-restraint/Euler data are identical to the
        // established uniform-force method in the second independent model.
        var samples=C1A(variable).ToArray();
        for(int i=0;i<samples.Length;i++)samples[i].ResultBeamForces.N=changesSign?40000:-40000;
        samples.Last().ResultBeamForces.N=-120000;
        foreach(var sample in C1A(uniform))sample.ResultBeamForces.N=-120000;
        var p=Options(variable,ntc);UseAxialEnvelope(p);
        var result=Single(variable,p,ntc);
        var expected=Single(uniform,Options(uniform,ntc),ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied,result.Outcome,Diagnostics(new[]{result}));
        Assert.AreEqual(EngineeringOutcome.Satisfied,expected.Outcome,Diagnostics(new[]{expected}));
        Assert.AreEqual(-120000,Value(result,"Input.N"),1e-7);
        Assert.AreEqual(-120000,Value(result,"Member.Envelope.N"),1e-7);
        Assert.AreEqual(3000,Value(result,"Member.Envelope.N.Distance"),1e-7);
        Assert.AreEqual(expected.Utilization!.Value,result.Utilization!.Value,1e-12);
        // Independent Annex-B hand calculation, H300x150x12/18 S355, L1=L2=3000:
        // Ag=8568, I1=125902944, I2=10163016, Wpl1=970488, Wpl2=212004 [mm units].
        // Uniform Nmax=120kN, Cm=1, curves b/c; both interaction equations retained.
        double gm1=ntc?1.05:1,nrk=8568*355d;
        double l1=Math.Sqrt(nrk/(Math.PI*Math.PI*210000*125902944/9e6));
        double l2=Math.Sqrt(nrk/(Math.PI*Math.PI*210000*10163016/9e6));
        static double Chi(double l,double imperfection) {
            double phi=.5*(1+imperfection*(l-.2)+l*l);
            return 1/(phi+Math.Sqrt(phi*phi-l*l));
        }
        double n1=120000*gm1/(Chi(l1,.34)*nrk),n2=120000*gm1/(Chi(l2,.49)*nrk);
        double m1=10e6*gm1/(970488*355d),m2=8e6*gm1/(212004*355d);
        double k11=1+Math.Min(l1-.2,.8)*n1,k22=1+Math.Min(2*l2-.6,1.4)*n2;
        if(ntc)k22=Math.Max(k22,1+Math.Min(2*l1-.6,1.4)*n1);
        double hand=Math.Max(n1+k11*m1+.6*k22*m2,n2+.6*k11*m1+k22*m2);
        Assert.AreEqual(hand,result.Utilization.Value,1e-10);
        Assert.IsTrue(result.Details.Trace.Any(t=>t.Key=="Member.Envelope.N.BeamId"));
        Assert.AreEqual(1,Value(result,"MemberStability.Cm1"));
        Assert.AreEqual(1,Value(result,"MemberStability.Cm2"));
        Assert.AreEqual(1,Value(result,"MemberStability.CmLT"));
        // The source FE cuts are preserved, not replaced by the fictitious envelope.
        Assert.AreEqual(changesSign?40000:-40000,samples.First().ResultBeamForces.N);
    }

    [DataTestMethod]
    [DataRow("coverage")][DataRow("moments")][DataRow("basis")][DataRow("class")]
    [DataRow("sway")][DataRow("external-critical")][DataRow("explicit-cm")][DataRow("typo")]
    public void AxialEnvelopeRefusesMissingOrIncompatibleQualifications(string change)
    {
        var model=SteelBeamColumnFrame.Create();C1A(model).Last().ResultBeamForces.N=-120000;
        var p=Options(model);UseAxialEnvelope(p);
        switch(change)
        {
            case "coverage":p.Remove("AxialForceCoverageSource");break;
            case "moments":p["MomentFactorPreparation"]="LinearDiagrams";break;
            case "basis":p["McrBasis"]="ActualMomentDiagram";break;
            case "class":p["MemberClass"]="1";p["ClassificationSource"]="Favourable M-dependent class";break;
            case "sway":p["SwayBucklingPlanes"]="1";p["SwayModeSource"]="Global mode";break;
            case "external-critical":p["Ncr1.Value"]="1e6";p["Ncr1.Unit"]="N";p["Ncr1.Source"]="Variable-load eigenvalue";break;
            case "explicit-cm":p["Cm1"]="0.8";break;
            case "typo":p["AxialForcePreparation"]="Unknown";break;
        }
        var r=Single(model,p);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
    }

    [TestMethod]
    public void CoverageCannotEnableTheNewModelWithoutExplicitSelection()
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model);
        p["AxialForceCoverageSource"]="Available cuts";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Single(model,p).Outcome);
    }

    [TestMethod]
    public void ParameterSchemaAcceptsOnlyTheConsistentEnvelopeContract()
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model);UseAxialEnvelope(p);
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        p["McrBasis"]="ActualMomentDiagram";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Any(i=>i.Code=="AxialEnvelopeMomentBasis"));
        p["McrBasis"]="UniformMoment";p["MomentFactorPreparation"]="LinearDiagrams";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Any(i=>i.Code=="AxialEnvelopeMomentFactors"));
        p["MomentFactorPreparation"]="ConservativeEnvelope";p["Ncr1.Value"]="1000";p["Ncr1.Unit"]="N";p["Ncr1.Source"]="Not the uniform model";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Any(i=>i.Code=="AxialEnvelopeConflictingModel"));
    }
}
