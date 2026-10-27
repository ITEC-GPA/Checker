using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Sections;
using System.Threading;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelEffectiveSectionExecution
    {
        internal static SteelCheckExecution Execute(SteelVerificationInput input,SteelMethodDecision decision,CancellationToken cancellation,IReadOnlyList<SteelShearBucklingAssessment> assessments)
        {
            var a=input.Actions;
            var normal=new SteelVerificationInput(input.Section,new SteelActions(a.N,0,0,0,a.M1,a.M2),input.Code);
            var r=SteelEffectiveSectionSolver.Solve(normal,cancellation:cancellation);var s=r.State;
            double utilization=r.Utilization,shearRatio=0,interaction=0;
            if(a.V2!=0) {
                var section=input.Section.ReadOnlySection;
                double resistance=assessments.Single().BucklingResistance ?? ModelShearAreas.GetArea2(section,input.Code.SectionCode.Standard)*input.Section.YieldStrength/(Math.Sqrt(3)*input.Code.SectionCode.GammaM0);
                shearRatio=Math.Abs(a.V2)/resistance;
                if(shearRatio>.5 && (a.N!=0||a.M2!=0||!section.SectionShape.IsDoubleSymmetric))
                    return new SteelCheckExecution(decision,SteelExecutionStatus.NotSupported,"Class4.HighShearInteractionNotQualified",
                        "Above 0.5 shear resistance this route requires doubly symmetric H, pure major bending and web shear; axial force and minor bending require a separate interaction.",shearAssessments:assessments);
                // EN1993-1-5 7.1(1): conservative lower bounds MfRd=0 and
                // MplRd=effective elastic MRd. The latter cannot exceed the plastic
                // resistance of effective flanges plus fully effective web.
                interaction=shearRatio<=.5?0:Math.Pow(2*shearRatio-1,2);
                utilization=Math.Max(shearRatio,r.Utilization+interaction);
                SteelInputValidation.Finite(utilization,"class4 web interaction");
            }
            var trace=new List<SectionResistanceValue>();
            void Add(string name,double value,string unit)=>trace.Add(new SectionResistanceValue(name,value,unit));
            Add("N",input.Actions.N,"N");Add("M1",input.Actions.M1,"Nmm");Add("M2",input.Actions.M2,"Nmm");
            Add("AreaGross",input.Section.Area,"mm2");Add("AreaEffective",s.Area,"mm2");
            Add("CentroidShiftX",s.CentroidShiftX,"mm");Add("CentroidShiftY",s.CentroidShiftY,"mm");
            Add("EffectiveIxx",s.Ixx,"mm4");Add("EffectiveIyy",s.Iyy,"mm4");Add("EffectiveIxy",s.Ixy,"mm4");
            Add("EffectiveModulus1",s.Modulus1,"mm3");Add("EffectiveModulus2",s.Modulus2,"mm3");
            Add("Fy",input.Section.YieldStrength,"MPa");Add("GammaM0",input.Code.SectionCode.GammaM0,"1");
            Add("MaximumAbsoluteStressBound",r.MaximumAbsoluteStress,"MPa");Add("NormalUtilization",r.Utilization,"1");Add("ShearRatio2",shearRatio,"1");Add("ShearInteractionPenalty",interaction,"1");Add("Utilization",utilization,"1");
            Add("EffectiveIterations",r.Iterations,"1");Add("EffectiveResidual",r.Residual,"1");
            for(int i=0;i<r.Plates.Count;i++) {
                var p=r.Plates[i];string id="Plate."+s.Geometry.Plates[i].Id+".";
                Add(id+"Psi",p.Psi,"1");Add(id+"KSigma",p.KSigma,"1");Add(id+"Lambda",p.Lambda,"1");Add(id+"Rho",p.Rho,"1");
                Add(id+"StressRatioBounded",p.StressRatioBounded?1:0,"1");
                Add(id+"WidthStart",s.Widths[2*i],"mm");Add(id+"WidthEnd",s.Widths[2*i+1],"mm");
                Add(id+"StressStart",p.StartStress,"MPa");Add(id+"StressEnd",p.EndStress,"MPa");
            }
            string reference=(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018
                ?"NTC 2018 4.2.4.1.2.2-3, 4.2.4.1.2.8; ":"EN1993-1-1:2005 6.2.2.5, 6.2.9.3; ")
                +"EN1993-1-5:2006 4.3(4) note, 4.4 Tables4.1/4.2; simultaneous effective section"+(a.V2!=0?"; 7.1(1), conservative MfRd=0 and MplRd=effective elastic MRd":"");
            return new SteelCheckExecution(decision,utilization<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:reference,scope:"Class4 unperforated unstiffened plates under concomitant N-M1-M2; iterative effective widths at fy, centroid eccentricity included. Conservative gross bounding-rectangle stress. H web shear: no reduction below 0.5 resistance; conservative pure-major-bending interaction above. Shear lag, transverse loads, torsion and member stability excluded.",
                utilization:utilization,trace:trace,shearAssessments:assessments);
        }
    }
}
