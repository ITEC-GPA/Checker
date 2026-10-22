using System.Collections.Generic;
using System.Threading;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelEffectiveSectionExecution
    {
        internal static SteelCheckExecution Execute(SteelVerificationInput input,SteelMethodDecision decision,CancellationToken cancellation)
        {
            var r=SteelEffectiveSectionSolver.Solve(input,cancellation:cancellation);var s=r.State;
            var trace=new List<SectionResistanceValue>();
            void Add(string name,double value,string unit)=>trace.Add(new SectionResistanceValue(name,value,unit));
            Add("N",input.Actions.N,"N");Add("M1",input.Actions.M1,"Nmm");Add("M2",input.Actions.M2,"Nmm");
            Add("AreaGross",input.Section.Area,"mm2");Add("AreaEffective",s.Area,"mm2");
            Add("CentroidShiftX",s.CentroidShiftX,"mm");Add("CentroidShiftY",s.CentroidShiftY,"mm");
            Add("EffectiveIxx",s.Ixx,"mm4");Add("EffectiveIyy",s.Iyy,"mm4");Add("EffectiveIxy",s.Ixy,"mm4");
            Add("EffectiveModulus1",s.Modulus1,"mm3");Add("EffectiveModulus2",s.Modulus2,"mm3");
            Add("Fy",input.Section.YieldStrength,"MPa");Add("GammaM0",input.Code.SectionCode.GammaM0,"1");
            Add("MaximumAbsoluteStressBound",r.MaximumAbsoluteStress,"MPa");Add("Utilization",r.Utilization,"1");
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
                +"EN1993-1-5:2006 4.3(4) note, 4.4 Tables4.1/4.2; simultaneous effective section";
            return new SteelCheckExecution(decision,r.Utilization<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:reference,scope:"Class4 unperforated unstiffened plates under concomitant N-M1-M2; iterative effective widths at fy, centroid eccentricity included. Conservative gross bounding-rectangle stress. Shear lag, transverse loads, shear/torsion interaction and member stability excluded.",
                utilization:r.Utilization,trace:trace);
        }
    }
}
