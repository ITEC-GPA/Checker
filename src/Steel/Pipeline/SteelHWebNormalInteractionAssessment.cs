using System;
using System.Linq;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    public sealed partial class SteelShearBucklingAssessment
    {
        /// <summary>Explicit EN1993-1-5 7.1(4)-(5) extension on the SAME H web:
        /// N-M1-V2, no longitudinal stiffeners, continuous web/flange attachments,
        /// no transverse patch load. The whole web is also its only longitudinal
        /// subpanel. Average shear is used (at least half the peak for the ordinary
        /// beam shear distribution). Support verification remains in this assessment.</summary>
        public bool ConservativeInPlaneNormalInteraction { get; private set; }

        public SteelShearBucklingAssessment WithConservativeInPlaneNormalInteraction()
        {
            var s=Input.Section.ReadOnlySection;var a=Input.EffectiveActions;
            if(Axis!=2 || !BucklingResistance.HasValue || s.SectionShape.GetType()!=typeof(SectionH)
                || !s.SectionShape.IsDoubleSymmetric || Math.Abs(s.AngleX1)>1e-10
                || a.V1!=0 || a.M2!=0 || a.T!=0)
                throw new NotSupportedException("Computed H-web resistance with continuous flange edges, no longitudinal stiffeners and in-plane N-M1-V2 is required. External exclusion alone is insufficient.");
            if(Input.Code.GammaM1.HasValue && Trace.Single(v=>v.Name=="GammaM1").Value!=Input.Code.GammaM1.Value)
                throw new ArgumentException("Web and member must use the same declared gammaM1 for the coupled interaction.");
            var copy=(SteelShearBucklingAssessment)MemberwiseClone();
            copy.ConservativeInPlaneNormalInteraction=true;
            return copy;
        }
    }
}
