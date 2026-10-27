using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    /// <summary>Local web patch/section interaction only. Stiffeners, flange lateral
    /// restraint, weld load transfer and global member stability remain separate.
    /// SectionResult is retained even when incomplete or failed.</summary>
    public sealed class SteelHWebPatchAssessment
    {
        public SteelVerificationInput Input => SectionResult.Plan.Input;
        public SteelVerificationResult SectionResult { get; }
        public WebPatchLoadingResistance Patch { get; }
        public double Force { get; }
        public double ForceUtilization => Force/Patch.Resistance;
        public double? Utilization { get; }
        public SteelExecutionStatus Status { get; }
        public bool IsComplete { get; }
        public bool CanQualifyWholeMember => false;
        public string Source { get; }
        public string Reference => "EN1993-1-5:2006 section6 and7.2; NTC2018 uses the declared supplementary EC3 method";
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        private SteelHWebPatchAssessment(SteelVerificationResult section,WebPatchLoadingResistance patch,
            double force,string source)
        {
            SectionResult=section; Patch=patch; Force=force; Source=source;
            if(double.IsNaN(ForceUtilization)||double.IsInfinity(ForceUtilization))
                throw new ArithmeticException("Patch utilization outside finite numerical range.");
            var a=Input.Actions;
            bool noSectionActions=a.N==0&&a.M1==0&&a.M2==0&&a.V1==0&&a.V2==0&&a.T==0;
            var normal=section.Checks.FirstOrDefault(c=>c.Decision.Target==SteelCheckTarget.NormalResistance);
            bool needsNormal=a.N!=0||a.M1!=0;
            IsComplete=(section.IsComplete||noSectionActions)&&(!needsNormal||normal?.Utilization!=null);
            var trace=new List<SectionResistanceValue>{V("Patch.Kf",patch.Kf,"1"),V("Patch.Fcr",patch.Fcr,"N"),
                V("Patch.m1",patch.M1,"1"),V("Patch.m2",patch.M2,"1"),V("Patch.ly",patch.LoadedLength,"mm"),
                V("Patch.lambda",patch.Slenderness,"1"),V("Patch.chi",patch.Chi,"1"),V("Patch.Leff",patch.EffectiveLength,"mm"),
                V("Patch.FRd",patch.Resistance,"N"),V("Patch.FEd",force,"N"),V("Patch.etaF",ForceUtilization,"1")};
            if(IsComplete) {
                double etaNM=needsNormal?normal.Utilization.Value:0;
                // Existing normal check may already include a conservative shear
                // penalty; retaining it is conservative for the patch interaction.
                double local=Ec3WebPatchLoading.Interaction(ForceUtilization,etaNM);
                Utilization=Math.Max(local,section.Checks.Where(c=>c.Utilization.HasValue).Select(c=>c.Utilization.Value).DefaultIfEmpty(0).Max());
                trace.Add(V("Patch.etaNM",etaNM,"1")); trace.Add(V("Patch.Interaction",local,"1"));
            }
            bool failed=ForceUtilization>1||section.Checks.Any(c=>c.Status==SteelExecutionStatus.Failed)||Utilization>1;
            Status=failed?SteelExecutionStatus.Failed:IsComplete?SteelExecutionStatus.Passed:SteelExecutionStatus.NotSupported;
            Trace=Array.AsReadOnly(trace.ToArray());
        }

        /// <summary>Welded, unperforated exact H, homogeneous material, no longitudinal
        /// stiffener. Force is a positive compression magnitude at the chosen flange,
        /// in the SAME FEM state as sectionResult; its sign/direction and footprint,
        /// lateral restraint, end stiffeners, attachment and load spread are qualified
        /// explicitly in source. Both-flange type requires equal opposed flanges.
        /// No free end, skew bearing or interacting multiple patches are inferred.</summary>
        public static SteelHWebPatchAssessment Evaluate(SteelVerificationResult sectionResult,WebPatchLoadType type,
            bool topFlange,double force,double panelLength,double stiffBearingLength,double gammaM1,string source)
            =>EvaluateCore(sectionResult,type,topFlange,force,panelLength,stiffBearingLength,gammaM1,source,null);

        public static SteelHWebPatchAssessment EvaluateAtUnstiffenedEnd(SteelVerificationResult sectionResult,
            bool topFlange,double force,double panelLength,double stiffBearingLength,double endDistance,double gammaM1,string source)
            =>EvaluateCore(sectionResult,WebPatchLoadType.UnstiffenedEnd,topFlange,force,panelLength,stiffBearingLength,gammaM1,source,endDistance);

        private static SteelHWebPatchAssessment EvaluateCore(SteelVerificationResult sectionResult,WebPatchLoadType type,
            bool topFlange,double force,double panelLength,double stiffBearingLength,double gammaM1,string source,double? endDistance)
        {
            if(sectionResult==null)throw new ArgumentNullException(nameof(sectionResult));
            SteelInputValidation.Positive(force,nameof(force));
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Same-case patch, restraint and force-transfer qualification required.");
            var input=sectionResult.Plan.Input; var section=input.Section.CreateSectionCopy();
            if(input.Section.Unperforated!=true||section.SectionShape.GetType()!=typeof(SectionH)
                ||section.SectionType!=Section.SectionTypes.Welded||SteelMethodSelector.ColdFormedOpen(section)
                ||Math.Abs(section.AngleX1)>1e-10||input.Actions.M2!=0||input.Actions.V1!=0||input.Actions.T!=0)
                throw new NotSupportedException("Homogeneous welded H, unperforated planar web, N/M1/V2 actions required.");
            SteelMethodSelector.ValidateGeometry(section);
            var h=(SectionH)section.SectionShape;
            if(h.R!=0)throw new NotSupportedException("Flat welded-plate geometry without fillet-radius substitution required.");
            if(type==WebPatchLoadType.OppositeFlanges&&!h.IsDoubleSymmetric)
                throw new NotSupportedException("Opposite patch route requires identical opposed flanges.");
            if(input.Member!=null&&panelLength>input.Member.Length)throw new ArgumentException("Panel exceeds physical member length.");
            if(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018&&!Ntc2018DesignPolicy.IsGammaM1(gammaM1))
                throw new ArgumentException("NTC patch route requires gammaM1=1.05 (1.10 for bridges).");
            double bf=topFlange?h.LenghtTopFlange:h.LenghtBottomFlange,tf=topFlange?h.ThicknessTopFlange:h.ThicknessBottomFlange;
            var p=endDistance.HasValue?Ec3WebPatchLoading.CalculateAtUnstiffenedEnd(h.HeightWeb,h.ThicknessWeb,bf,tf,
                input.Section.YieldStrength,input.Section.YieldStrength,input.Section.ElasticModulus,panelLength,stiffBearingLength,endDistance.Value,gammaM1)
                :Ec3WebPatchLoading.Calculate(type,h.HeightWeb,h.ThicknessWeb,
                topFlange?h.LenghtTopFlange:h.LenghtBottomFlange,topFlange?h.ThicknessTopFlange:h.ThicknessBottomFlange,
                input.Section.YieldStrength,input.Section.YieldStrength,input.Section.ElasticModulus,panelLength,stiffBearingLength,gammaM1);
            return new SteelHWebPatchAssessment(sectionResult,p,force,source);
        }
        private static SectionResistanceValue V(string n,double v,string unit)=>new SectionResistanceValue(n,v,unit);
    }
}
