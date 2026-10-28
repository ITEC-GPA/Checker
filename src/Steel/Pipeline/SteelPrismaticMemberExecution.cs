using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelPrismaticMemberExecution
    {
        internal static SteelMethodDecision Select(SteelMemberAnalysis analysis)
        {
            var i = analysis.Input; var a = i.Actions; bool compression = analysis.Kind == SteelMemberAnalysisKind.PrismaticCompression;
            var missing = new List<string>();
            if (i.Member == null) missing.Add("Member");
            if (!i.Code.GammaM1.HasValue) missing.Add("Code.GammaM1");
            if (!i.Section.Unperforated.HasValue) missing.Add("Section.Unperforated");
            if (missing.Count > 0) return Missing(missing);
            if (i.Section.Unperforated != true && analysis.PackedPair?.ComputedPacking == null) return Unsupported("Member.PerforatedNotQualified", "Gross-section stability requires an unperforated declaration.");
            if (a.T != 0 || a.M2 != 0 || compression && (a.M1 != 0 || a.V1 != 0 || a.V2 != 0)
                || !compression && (a.N != 0 || a.V1 != 0))
                return Unsupported("Member.PrismaticActionPattern", "Compression route requires concentric constant N only; LTB route requires major-axis bending without N, minor bending or torsion.");
            if (compression ? a.N >= 0 : a.M1 == 0)
                return new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.NotRequired, null,
                    "Member.NoActiveDemand", "No compressive force or major-axis moment for this selected mechanism.");
            var section = i.Section.ReadOnlySection; var shape = section.SectionShape;
            if (!SteelMethodSelector.KnownGeometry(shape) || SteelMethodSelector.ColdFormedOpen(section))
                return Unsupported("Member.PrismaticGeometry", "This exact family/manufacturing route is not qualified.");
            if(shape is SectionBuiltUp)
            {
                if(!compression || analysis.PackedPair==null)
                    return Unsupported("Member.PackingAssessmentRequired","Built-up members require the packed-pair compression route and local assessments.");
                SteelPackedPairExecution.Validate(analysis);
            }
            else if(analysis.PackedPair!=null)throw new ArgumentException("Packed-pair assessment on a single profile.");
            SteelMethodSelector.ValidateGeometry(section);
            if (i.Section.UltimateStrength < i.Section.YieldStrength) throw new ArgumentException("Ultimate strength below yield strength.");
            // No implicit use of a coupled-angle principal bending model for LTB.
            if (!compression && (shape.GetType() == typeof(SectionL) || section.J11 < section.J22
                || shape.GetType() == typeof(SectionH) && (Math.Abs(shape.AngleX1) > 1e-10 || ((SectionH)shape).LenghtTopFlange != ((SectionH)shape).LenghtBottomFlange
                    || ((SectionH)shape).ThicknessTopFlange != ((SectionH)shape).ThicknessBottomFlange)))
                return Unsupported("Member.LtbGeometry", "Use a separately qualified method for asymmetric/rotated H or angle bending; this LTB route uses the major principal axis.");
            var cl = SteelMethodSelector.Classification(i, section, analysis.ExternalMemberClass.HasValue
                ? new SteelSectionClassification(i, analysis.ExternalMemberClass.Value, analysis.ClassificationSource) : null);
            if (cl.Failure != null) return new SteelMethodDecision(SteelCheckTarget.MemberStability, cl.Failure.Status, null,
                cl.Failure.DiagnosticCode, cl.Failure.Message, missing: cl.Failure.MissingInputs);
            if (cl.Class == 4)
            {
                try { SteelEffectiveMemberProperties.Qualify(i.Section); }
                catch (NotSupportedException ex) { return Unsupported("Member.Class4NotQualified", ex.Message); }
            }
            if (compression)
            {
                if (!Has(analysis, SteelStabilityQuantity.Ncr1) && !i.Member.BucklingLength1.HasValue) missing.Add("Ncr1 or Member.BucklingLength1");
                if (!Has(analysis, SteelStabilityQuantity.Ncr2) && !i.Member.BucklingLength2.HasValue) missing.Add("Ncr2 or Member.BucklingLength2");
                foreach (var mode in new[] { (SteelStabilityQuantity.NcrT, analysis.Prismatic.TorsionalExclusionSource),
                    (SteelStabilityQuantity.NcrFT, analysis.Prismatic.FlexuralTorsionalExclusionSource) })
                {
                    if (Has(analysis, mode.Item1) && mode.Item2 != null)
                        return new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.InvalidInput, null,
                            "Member.ConflictingModeAssessment", "A mode cannot be both evaluated and excluded.");
                    if (!Has(analysis, mode.Item1) && mode.Item2 == null) missing.Add(mode.Item1 + " or explicit mode exclusion");
                }
                if (analysis.Values.Any(v => v.Quantity != SteelStabilityQuantity.Ncr1 && v.Quantity != SteelStabilityQuantity.Ncr2
                    && v.Quantity != SteelStabilityQuantity.NcrT && v.Quantity != SteelStabilityQuantity.NcrFT))
                    throw new ArgumentException("Only compressive critical forces belong to this route.");
            }
            else
            {
                if (!Has(analysis, SteelStabilityQuantity.Mcr)) missing.Add("Mcr");
                if (analysis.Values.Any(v => v.Quantity != SteelStabilityQuantity.Mcr)) throw new ArgumentException("Only Mcr belongs to this route.");
                if (i.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                    && analysis.Prismatic.CriticalMomentBasis != SteelCriticalMomentBasis.UniformMoment)
                    return Unsupported("Member.NtcUniformMcrRequired", "NTC 4.2.4.1.3.2 uses Mcr under uniform moment; this conservative route uses f=beta=Kchi=1 and lambda0=0.2.");
            }
            if (missing.Count > 0) return Missing(missing);
            return new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.Ready,
                compression ? SteelMethod.PrismaticCompression : SteelMethod.PrismaticLateralTorsionalBuckling,
                null, null, cl.Class, cl.Source);
        }

        /// <summary>Typed intermediates of the compression route, written to the trace in the same pass:
        /// the beam-column route reads them here, never back from the trace by name.</summary>
        internal sealed class CompressionValues
        {
            /// <summary>EN1993-1-5 Aeff under uniform compression at fy; null for classes 1-3.</summary>
            internal double? EffectiveArea { get; set; }
            internal double Slenderness1 { get; set; }
            internal double Chi1 { get; set; }
            internal double Slenderness2 { get; set; }
            internal double Chi2 { get; set; }
        }

        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis, SteelMethodDecision decision)
            => Execute(analysis, decision, out _);

        /// <param name="values">Compression intermediates; null on the LTB route.</param>
        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis, SteelMethodDecision decision, out CompressionValues values)
        {
            var i = analysis.Input; var section = i.Section.ReadOnlySection; var trace = new List<SectionResistanceValue>();
            values = null;
            bool compression = analysis.Kind == SteelMemberAnalysisKind.PrismaticCompression;
            bool ntc = i.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            double fy = i.Section.YieldStrength, gamma = i.Code.GammaM1.Value;
            Add(trace, "GammaM0", i.Code.SectionCode.GammaM0, "1"); Add(trace, "GammaM1", gamma, "1");
            Add(trace, "Fy", fy, "MPa"); Add(trace, "MemberClass", decision.SectionClass.Value, "1");
            bool effective = decision.SectionClass == 4;
            double resistance; string reference, scope, unit;
            if (compression)
            {
                var typed = new CompressionValues();
                double area = effective ? MemoizedEffectiveProperties.CompressionArea(i.Section) : section.Area;
                if (effective) { Add(trace, "AreaEffective", area, "mm2"); Add(trace, "EffectiveCentroidShiftX", 0, "mm"); Add(trace, "EffectiveCentroidShiftY", 0, "mm"); typed.EffectiveArea = area; }
                double characteristic = area * fy;
                Positive(characteristic, "Characteristic axial resistance"); Add(trace, "Area", section.Area, "mm2");
                resistance = characteristic / i.Code.SectionCode.GammaM0; Add(trace, "SectionResistance", resistance, "N");
                foreach (var mode in new[] { SteelStabilityQuantity.Ncr1, SteelStabilityQuantity.Ncr2, SteelStabilityQuantity.NcrT, SteelStabilityQuantity.NcrFT })
                {
                    bool first = mode == SteelStabilityQuantity.Ncr1, second = mode == SteelStabilityQuantity.Ncr2;
                    var value = analysis.Values.SingleOrDefault(v => v.Quantity == mode);
                    if (value == null && !first && !second) { Add(trace, mode + ".Excluded", 1, "1"); continue; }
                    double ncr;
                    if (value != null) ncr = value.Value;
                    else
                    {
                        double length = first ? i.Member.BucklingLength1.Value : i.Member.BucklingLength2.Value;
                        double inertia = first ? section.J11 : section.J22;
                        Positive(inertia, "Inertia");
                        ncr = Math.PI * Math.PI * i.Section.ElasticModulus * inertia / (length * length);
                        Add(trace, mode + ".EffectiveLength", length, "mm"); Add(trace, mode + ".Inertia", inertia, "mm4");
                        Add(trace, mode + ".ElasticModulus", i.Section.ElasticModulus, "MPa");
                    }
                    Positive(ncr, "Critical force");
                    var curve = first ? analysis.Prismatic.Curve1 : second ? analysis.Prismatic.Curve2 : analysis.Prismatic.TorsionalCurve;
                    double slenderness = Math.Sqrt(characteristic) / Math.Sqrt(ncr);
                    double chi = BucklingCurves.Reduction(slenderness, curve), nb = chi * characteristic / gamma;
                    Positive(nb, "Buckling resistance"); resistance = Math.Min(resistance, nb);
                    Add(trace, mode.ToString(), ncr, "N"); Add(trace, mode + ".Alpha", BucklingCurves.Alpha(curve), "1");
                    Add(trace, mode + ".Slenderness", slenderness, "1"); Add(trace, mode + ".Chi", chi, "1"); Add(trace, mode + ".NbRd", nb, "N");
                    if (first) { typed.Slenderness1 = slenderness; typed.Chi1 = chi; }
                    else if (second) { typed.Slenderness2 = slenderness; typed.Chi2 = chi; }
                }
                values = typed;
                unit = "N";
                reference = ntc ? "NTC 2018 4.2.4.1.3.1, equations 4.2.41-46; Table 4.2.VIII"
                    : "EN 1993-1-1:2005 6.3.1.1-4, equations 6.47/6.49/6.52; Table 6.1";
                scope = "Prismatic concentric constant compression, classes 1-3. All four instability modes evaluated or explicitly excluded; curves are caller-established. No N-M interaction.";
            }
            else
            {
                // Rounded RHS uses its independently qualified elastic modulus;
                // a conservative elastic check is also permitted for compact sections.
                bool elastic = decision.SectionClass >= 3 || section.SectionShape.GetType() == typeof(SectionRHSRoundedCorners);
                double w = effective ? MemoizedEffectiveProperties.BendingModulus(i, 1) : elastic ? section.Wel1 : section.Wpl1; Positive(w, "Section modulus");
                double mcr = analysis.Values.Single(v => v.Quantity == SteelStabilityQuantity.Mcr).Value;
                double characteristic = w * fy; Positive(characteristic, "Characteristic moment resistance");
                var curve = BucklingCurves.LateralTorsional(section, ntc);
                double slenderness = Math.Sqrt(characteristic) / Math.Sqrt(mcr), chi = BucklingCurves.Reduction(slenderness, curve);
                resistance = Math.Min(chi * characteristic / gamma, characteristic / i.Code.SectionCode.GammaM0);
                Add(trace, effective ? "Weff1Min" : elastic ? "Wel1Min" : "Wpl1", w, "mm3"); Add(trace, "Mcr", mcr, "Nmm");
                Add(trace, "AlphaLT", BucklingCurves.Alpha(curve), "1"); Add(trace, "SlendernessLT", slenderness, "1"); Add(trace, "ChiLT", chi, "1");
                Add(trace, "BetaLT", 1, "1"); Add(trace, "LambdaLT0", .2, "1"); Add(trace, "MomentCorrectionF", 1, "1");
                unit = "Nmm";
                reference = ntc ? "NTC 2018 4.2.4.1.3.2, equations 4.2.48-51; Table 4.2.IX (general conservative case)"
                    : "EN 1993-1-1:2005 6.3.2.1-2, equations 6.55-56; recommended Table 6.4 curves";
                scope = "Prismatic major-axis LTB; explicit gross-section elastic Mcr and whole-member maximum M1. Shear/section checks remain separate. " + analysis.Prismatic.CriticalMomentBasis;
            }
            if (effective) {
                reference += "; EN1993-1-5:2006 4.3(3)/(4), 4.4; EN1993-1-1:2005 6.3.1.1(3), 6.3.1.2, 6.3.2.1";
                scope = "Class4 doubly symmetric H/RHS; Aeff under uniform compression / Weff under pure bending at fy. Effective centroid under N unchanged. Elastic critical values use gross properties. Section/shear interaction remains separate. " + analysis.Prismatic.CriticalMomentBasis;
            }
            Positive(resistance, "Design resistance"); double demand = compression ? Math.Abs(i.Actions.N) : Math.Abs(i.Actions.M1);
            double utilization = demand / resistance; SteelInputValidation.Finite(utilization, "Utilization");
            Add(trace, "Resistance", resistance, unit); Add(trace, "Utilization", utilization, "1");
            var result = new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: reference, scope: scope, resistance: resistance, resistanceUnit: unit, utilization: utilization, trace: trace);
            return analysis.PackedPair==null?result:SteelPackedPairExecution.Complete(analysis,decision,result);
        }

        private static bool Has(SteelMemberAnalysis a, SteelStabilityQuantity q) => a.Values.Any(v => v.Quantity == q);
        private static void Positive(double value, string name)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArithmeticException(name + " outside finite positive range."); }
        private static void Add(List<SectionResistanceValue> t, string n, double v, string u)
        { SteelInputValidation.Finite(v, n); t.Add(new SectionResistanceValue(n, v, u)); }
        private static SteelMethodDecision Unsupported(string code, string message)
            => new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.NotSupported, null, code, message);
        private static SteelMethodDecision Missing(List<string> names)
            => new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.MissingInput, null,
                "Member.PrismaticInputRequired", string.Join(", ", names), missing: names);
    }
}
