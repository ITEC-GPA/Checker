using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.EffectiveSections
{
    /// <summary>One converged simultaneous N/M state. This is not a member buckling
    /// result, nor an effective stiffness for global analysis. Stresses in MPa.</summary>
    public sealed class SteelEffectiveSectionResult
    {
        public EffectiveSectionState State { get; }
        public IReadOnlyList<EffectivePlate> Plates { get; }
        public int Iterations { get; }
        public double Residual { get; }
        public double MaximumAbsoluteStress { get; }
        public double Utilization { get; }
        internal SteelEffectiveSectionResult(EffectiveSectionState state,EffectivePlate[] plates,
            int iterations,double residual,double stress,double utilization)
        {
            State=state;Plates=Array.AsReadOnly(plates);Iterations=iterations;Residual=residual;
            MaximumAbsoluteStress=stress;Utilization=utilization;
        }
    }

    /// <summary>EN1993-1-5:2006 4.3(4) note: a single effective section under simultaneous
    /// N/M, with centroid-shift moments. EN 4.4 reduction uses fy, without stress relaxation.
    /// Unstiffened unperforated flat plates; no shear lag, shell or shear interaction.</summary>
    public static class SteelEffectiveSectionSolver
    {
        public static SteelEffectiveGeometry Qualify(SteelSectionInput section)
        {
            if(section==null)throw new ArgumentNullException(nameof(section));
            if(section.Unperforated!=true)throw new NotSupportedException("Effective section requires a declared unperforated section.");
            if(Math.Abs(section.ElasticModulus-210000)>1e-6||Math.Abs(section.PoissonRatio-.3)>1e-10)
                throw new NotSupportedException("The 28.4 plate slenderness coefficient requires E=210000 MPa and nu=0.3.");
            if(section.UltimateStrength<section.YieldStrength)throw new ArgumentException("Ultimate strength must not be below yield strength.");
            // Nonconcentric corner geometry can be integrated, but its curved transition
            // does not establish the local plate boundary conditions used by this solver.
            var shape=section.ReadOnlySection.SectionShape;
            if(shape is SectionRHSRoundedCorners r&&Math.Abs(r.OutsideRadius-r.InsideRadius-r.Thickness)>1e-8*r.Thickness)
                throw new NotSupportedException("Class4 rounded RHS currently requires concentric constant-thickness corners.");
            return SteelEffectiveGeometry.Create(section);
        }

        public static SteelEffectiveSectionResult Solve(SteelVerificationInput input,int maximumIterations=120,
            double tolerance=1e-8,CancellationToken cancellation=default)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            var a=input.Actions;
            if(a.V1!=0||a.V2!=0||a.T!=0)throw new NotSupportedException("This effective-section solver handles normal forces only; shear/torsion require separate qualified interaction.");
            var geometry=Qualify(input.Section);double fy=input.Section.YieldStrength;
            double length=Math.Max(geometry.MaxX-geometry.MinX,geometry.MaxY-geometry.MinY);
            double scale=Math.Max(Math.Abs(a.N),Math.Max(Math.Abs(a.M1/length),Math.Abs(a.M2/length)));
            // Homogeneous normalization makes effective widths independent of load units
            // and magnitude; it also avoids the plate kernel's absolute zero threshold.
            double n=scale==0?0:a.N/scale,m1=scale==0?0:a.M1/scale,m2=scale==0?0:a.M2/scale;
            EffectivePlate[] Evaluate(EffectiveSectionState state)=>geometry.Plates.Select(p=>
                Reduce(p,state.Stress(p.StartX,p.StartY,n,m1,m2),state.Stress(p.EndX,p.EndY,n,m1,m2),fy)).ToArray();
            EffectiveSectionState ReduceState(EffectivePlate[] plates)=>geometry.WithWidths(plates.SelectMany((p,i)=>RetainedWidths(geometry.Plates[i],p)).ToArray());
            double Distance(EffectiveSectionState x,EffectiveSectionState y)=>x.Widths.Select((v,i)=>
                Math.Abs(v-y.Widths[i])/geometry.Plates[i/2].Length).Max();
            EffectiveSectionState Relax(EffectiveSectionState x,EffectiveSectionState y,double f)=>geometry.WithWidths(
                x.Widths.Select((v,i)=>v+f*(y.Widths[i]-v)).ToArray());
            var result=EffectiveSectionIteration.Solve(geometry.Gross(),Evaluate,ReduceState,Distance,Relax,
                maximumIterations,tolerance,coordinates:s=>s.Widths.Select((v,i)=>v/geometry.Plates[i/2].Length).ToArray(),cancellation:cancellation);
            // Report actual stresses on the returned state, never on an unevaluated iterate.
            var actual=geometry.Plates.Select(p=>Reduce(p,result.State.Stress(p.StartX,p.StartY,a.N,a.M1,a.M2),
                result.State.Stress(p.EndX,p.EndY,a.N,a.M1,a.M2),fy)).ToArray();
            double stress=result.State.MaximumAbsoluteStress(a.N,a.M1,a.M2),utilization=stress/(fy/input.Code.SectionCode.GammaM0);
            SteelInputValidation.Finite(utilization,"effective-section utilization");
            return new SteelEffectiveSectionResult(result.State,actual,result.Iterations,result.Residual,stress,utilization);
        }

        /// <summary>Uniform stress at fy, EN1993-1-5 4.3(3). This differs from a force
        /// applied at the gross centroid of an asymmetric effective section. Member methods
        /// must separately account for the resulting eccentricity.</summary>
        public static EffectiveSectionState UniformCompression(SteelSectionInput section)
        {
            var g=Qualify(section);double fy=section.YieldStrength;
            return g.WithWidths(g.Plates.SelectMany(p=> {
                var r=Reduce(p,-fy,-fy,fy);return RetainedWidths(p,r);
            }).ToArray());
        }

        private static double[] RetainedWidths(EffectiveSectionPlate p,EffectivePlate r)
        {
            // Subtract the entire inactive design width, including when the normative
            // width exceeds the mapped flat (angle legs / rounded RHS). Multiplying only
            // the shorter flat by rho would retain part of that inactive width.
            double total=r.EffectiveAtStart+r.EffectiveAtEnd;
            double retained=Math.Max(0,Math.Min(p.Length,p.Length-(r.Width-total)));
            double factor=total==0?0:retained/total;
            return new[]{r.EffectiveAtStart*factor,r.EffectiveAtEnd*factor};
        }

        private static EffectivePlate Reduce(EffectiveSectionPlate p,double s1,double s2,double fy)
        {
            double scale=Math.Max(Math.Abs(s1),Math.Abs(s2));
            double a=scale==0?0:s1/scale*fy,b=scale==0?0:s2/scale*fy;
            var r=p.Kind==EffectivePlateKind.Internal?EffectivePlateReduction.InternalPlate(p.BucklingWidth,p.Thickness,a,b,fy)
                :EffectivePlateReduction.Outstand(p.BucklingWidth,p.Thickness,a,b,fy);
            double f=p.DesignWidth/p.BucklingWidth;
            return new EffectivePlate(p.DesignWidth,r.Thickness,r.Psi,r.KSigma,r.Lambda,r.Rho,r.CompressedWidth*f,
                r.EffectiveAtStart*f,r.EffectiveAtEnd*f,s1,s2,r.StressRatioBounded);
        }
    }
}
