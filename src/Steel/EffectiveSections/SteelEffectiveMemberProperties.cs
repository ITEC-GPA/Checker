using System;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.EffectiveSections
{
    /// <summary>Separate EN1993-1-5 4.3(3)/(4) resistance properties for member checks.
    /// Gross Model J/Iw remain the elastic critical-property source.</summary>
    internal static class SteelEffectiveMemberProperties
    {
        internal static void Qualify(SteelSectionInput input)
        {
            SteelEffectiveSectionSolver.Qualify(input);
            var shape=input.CreateSectionCopy().SectionShape;var type=shape.GetType();
            if(!shape.IsDoubleSymmetric || !(type==typeof(SectionH)||type==typeof(SectionRHS)||type==typeof(SectionRHSRoundedCorners)))
                throw new NotSupportedException("Class4 member route requires doubly symmetric H/RHS. Asymmetric effective-centroid eccentricity needs a separately qualified member interaction.");
        }
        internal static EffectiveSectionState Compression(SteelSectionInput input)
        {
            Qualify(input);var state=SteelEffectiveSectionSolver.UniformCompression(input);
            double size=Math.Max(state.Geometry.MaxX-state.Geometry.MinX,state.Geometry.MaxY-state.Geometry.MinY);
            if(Math.Abs(state.CentroidShiftX)>size*1e-10||Math.Abs(state.CentroidShiftY)>size*1e-10)
                throw new NotSupportedException("Effective centroid shift requires an eccentric beam-column analysis.");
            return state;
        }
        internal static double BendingModulus(SteelVerificationInput input,int axis)
        {
            Qualify(input.Section);if(axis!=1&&axis!=2)throw new ArgumentOutOfRangeException(nameof(axis));
            double minimum=double.PositiveInfinity;
            // Whole-member inputs can contain both moment signs. Retain the smaller
            // resistance of both effective sections instead of freezing one cut's state.
            foreach(double sign in new[]{-1d,1d}) {
                var bending=new SteelVerificationInput(input.Section,new SteelActions(0,0,0,0,axis==1?sign:0,axis==2?sign:0),input.Code);
                var r=SteelEffectiveSectionSolver.Solve(bending);
                minimum=Math.Min(minimum,1/r.MaximumAbsoluteStress);
            }
            SteelInputValidation.Positive(minimum,"effective member modulus");return minimum;
        }
    }
}
