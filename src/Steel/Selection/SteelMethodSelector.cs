using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Selection
{
    /// <summary>Deterministic routing over qualified section and member methods.
    /// EC3 and NTC share contracts/arithmetic, retaining explicit policy references.
    /// No implicit national profile, whole-member model or legacy-checker fallback.</summary>
    public static class SteelMethodSelector
    {
        public static SteelSelectionPlan Select(SteelVerificationInput input,
            SteelCheckScope scope = SteelCheckScope.Section, SteelSectionClassification classification = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!Enum.IsDefined(typeof(SteelCheckScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (classification != null && !ReferenceEquals(classification.Input, input))
                throw new ArgumentException("Classification belongs to a different input snapshot/code/action state.", nameof(classification));
            var decisions = new List<SteelMethodDecision>();
            if (scope != SteelCheckScope.Member)
            {
                var section = input.Section.CreateSectionCopy();
                decisions.Add(Guard(SteelCheckTarget.NormalResistance, () => Normal(input, section, classification)));
                decisions.Add(Guard(SteelCheckTarget.Shear1, () => Shear(input, section, 1)));
                decisions.Add(Guard(SteelCheckTarget.Shear2, () => Shear(input, section, 2)));
                decisions.Add(input.Actions.T == 0 ? NotRequired(SteelCheckTarget.Torsion)
                    : Unsupported(SteelCheckTarget.Torsion, "Torsion.NotIntegrated", "No torsion route is integrated in the common input selector."));
            }
            if (scope != SteelCheckScope.Section) decisions.Add(Member(input));
            return new SteelSelectionPlan(input, scope, decisions);
        }

        /// <summary>Explicit member-analysis route; the original section-only selection remains unchanged.</summary>
        public static SteelSelectionPlan SelectMember(SteelMemberAnalysis analysis,
            SteelCheckScope scope = SteelCheckScope.Member, SteelSectionClassification classification = null)
        {
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            if (scope != SteelCheckScope.Member && scope != SteelCheckScope.SectionAndMember) throw new ArgumentOutOfRangeException(nameof(scope));
            if (analysis.BeamColumnParameters != null && scope != SteelCheckScope.Member)
                throw new ArgumentException("Beam-column input contains whole-member maxima; evaluate sections separately with concomitant cut actions.", nameof(scope));
            var original = Select(analysis.Input, scope, classification);
            var decisions = original.Decisions.Where(d => d.Target != SteelCheckTarget.MemberStability).ToList();
            decisions.Add(Guard(SteelCheckTarget.MemberStability, () => SteelMemberAnalysisExecution.Select(analysis)));
            return new SteelSelectionPlan(analysis.Input, scope, decisions, analysis);
        }

        private static SteelMethodDecision Normal(SteelVerificationInput input, ISteelSection section,
            SteelSectionClassification suppliedClass)
        {
            const SteelCheckTarget target = SteelCheckTarget.NormalResistance;
            var a = input.Actions; var s = input.Section; var shape = section.SectionShape;
            if (a.N == 0 && a.M1 == 0 && a.M2 == 0) return NotRequired(target);
            if (a.T != 0) return Unsupported(target, "TorsionInteraction.NotIntegrated", "Normal resistance with torsion needs a qualified interaction method.");
            if (!KnownGeometry(shape)) return Unsupported(target, "Geometry.NotQualified", "This exact geometry type has no common-input route.");
            if (ColdFormedOpen(section)) return Unsupported(target, "ColdFormedOpenSection.NotQualified", "Cold-formed open sections require their own qualified design route.");
            ValidateGeometry(section);
            if (s.UltimateStrength < s.YieldStrength) return Invalid(target, "Fu.BelowFy", "Ultimate strength must not be below yield strength.");
            bool moment = a.M1 != 0 || a.M2 != 0;
            bool shear = a.V1 != 0 || a.V2 != 0;

            if (!moment && !shear && a.N > 0)
            {
                if (!s.Unperforated.HasValue) return Missing(target, "NetSection.DeclarationRequired", "Declare holes before selecting tension.", "Section.Unperforated");
                if (!s.Unperforated.Value && !s.NetArea.HasValue) return Missing(target, "NetArea.Required", "Supply the net area for a perforated section.", "Section.NetArea");
                return Ready(target, SteelMethod.SectionTension);
            }
            if (s.Unperforated != true)
                return s.Unperforated == false
                    ? Unsupported(target, "PerforatedSection.NotQualified", "Compression/bending of perforated sections requires its own qualified treatment.")
                    : Missing(target, "NetSection.DeclarationRequired", "Declare an unperforated section before using the gross-section route.", "Section.Unperforated");

            bool ec3 = input.Code.SectionCode.Standard == SectionResistanceStandard.Eurocode3_2005;
            bool combined = a.N != 0 && moment || a.M1 != 0 && a.M2 != 0;
            bool asymmetricH = shape is SectionH h &&
                (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange);
            bool interaction = combined || moment && asymmetricH;
            var classInfo = Classification(input, section, suppliedClass);
            if (classInfo.Failure != null) return classInfo.Failure;
            int cl = classInfo.Class.Value;
            if (cl == 4) return new SteelMethodDecision(target, SteelSelectionStatus.NotSupported, null,
                "Class4.EffectivePropertiesRequired", "A qualified effective-section method or a separately established state classification is required.", cl, classInfo.Source);
            bool plasticH = interaction && ec3 && cl <= 2 && shape.GetType() == typeof(SectionH)
                && ((SectionH)shape).R == 0 && Math.Abs(shape.AngleX1) <= 1e-10;
            bool elastic = interaction && !plasticH || moment &&
                (shape.GetType() == typeof(SectionL) || shape.GetType() == typeof(SectionRHSRoundedCorners));
            SteelMethod method = !moment ? SteelMethod.SectionCompression
                : plasticH ? SteelMethod.Ec3PlasticHInteraction
                : elastic ? (ec3 ? SteelMethod.ElasticNormalInteraction : SteelMethod.NtcElasticNormalInteraction)
                : a.M1 != 0 ? SteelMethod.SectionBending1 : SteelMethod.SectionBending2;
            // A resistance-domain bound, not a shear-stress field or a von Mises check.
            // It also bounds simultaneous shears by superposition of admitted pure-action states.
            if (shear && (!moment || cl == 3 || a.V1 != 0 && a.V2 != 0))
                return LinearInteraction(input, section, cl, classInfo.Source);
            if (moment)
            {
                if (elastic)
                { Positive(section.Wel1); Positive(section.Wel2); }
                else if (method != SteelMethod.Ec3PlasticHInteraction)
                    Positive(a.M1 != 0 ? (cl <= 2 ? section.Wpl1 : section.Wel1) : (cl <= 2 ? section.Wpl2 : section.Wel2));
            }
            if (!shear) return Ready(target, method, cl, classInfo.Source);
            if (!ShearMappingQualified(section)) return Unsupported(target, "ShearAxes.NotQualified", "Low shear cannot be established from an unqualified principal-axis shear mapping.");
            if (Math.Abs(a.V1) > .5 * ShearResistance(input, section, 1)
                || Math.Abs(a.V2) > .5 * ShearResistance(input, section, 2))
            {
                if (cl <= 2 && shape.GetType() == typeof(SectionH) && !asymmetricH
                    && a.N == 0 && a.M2 == 0 && a.V1 == 0 && Math.Abs(shape.AngleX1) <= 1e-10)
                    return AwaitShearAssessment(target, SteelMethod.HMajorBendingShear, cl, classInfo.Source);
                return LinearInteraction(input, section, cl, classInfo.Source);
            }
            return AwaitShearAssessment(target, method, cl, classInfo.Source);
        }

        internal static ClassificationInfo Classification(SteelVerificationInput input, ISteelSection section,
            SteelSectionClassification supplied)
        {
            if (supplied != null) return new ClassificationInfo(supplied.SectionClass, supplied.Source);
            var shape = section.SectionShape;
            bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            if (shape.GetType() == typeof(SectionRectangular) || shape.GetType() == typeof(SectionCircular))
                return new ClassificationInfo(3, (ntc ? "NTC 2018 4.2.3.2" : "EN 1993-1-1:2005 6.2.1(4)")
                    + "; solid section, conservative elastic resistance; no plate local buckling");
            if (shape.GetType() == typeof(SectionL))
            {
                var angle = (SectionL)shape;
                if (angle.HorizontalLegThickness != angle.VerticalLegThickness)
                    return new ClassificationInfo(Unsupported(SteelCheckTarget.NormalResistance, "AngleThickness.NotQualified", "Automatic angle compression classification requires equal leg thicknesses."));
                return new ClassificationInfo(AngleCompressionArithmetic.Classify(angle.VerticalLegLength, angle.HorizontalLegLength,
                    angle.VerticalLegThickness, input.Section.YieldStrength, angle.R),
                    (ntc ? "Ntc2018CompressionClassification; Table 4.2.V" : "Ec3AngleCompression; Table 5.2") + "; nominal uniform compression");
            }
            if (!ModelCompressionGeometry.CanMap(section))
                return new ClassificationInfo(Unsupported(SteelCheckTarget.NormalResistance, "Classification.GeometryNotQualified", "No automatic uniform-compression mapping for this exact geometry."));
            var result = ntc ? Ntc2018CompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), input.Section.YieldStrength)
                : Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), input.Section.YieldStrength);
            if (!result.IsEvaluated) return new ClassificationInfo(Invalid(SteelCheckTarget.NormalResistance, result.DiagnosticCode, "Uniform-compression classification could not be established."));
            return new ClassificationInfo(result.NominalClass.Value,
                (ntc ? "Ntc2018CompressionClassification (Ntc2018DesignPolicy); Tables 4.2.III-V" : "Ec3UniformCompressionClassification; Table 5.2")
                + "; conservative nominal uniform-compression class");
        }

        private static SteelMethodDecision Shear(SteelVerificationInput input, ISteelSection section, int axis)
        {
            var target = axis == 1 ? SteelCheckTarget.Shear1 : SteelCheckTarget.Shear2;
            var a = input.Actions;
            if ((axis == 1 ? a.V1 : a.V2) == 0) return NotRequired(target);
            if (a.T != 0) return Unsupported(target, "ShearTorsionInteraction.NotIntegrated", "Shear with torsion requires a qualified interaction method.");
            if (!ShearMappingQualified(section)) return Unsupported(target, "ShearAxes.NotQualified", "No qualified shear-area mapping for this exact geometry and principal-axis orientation.");
            if (ColdFormedOpen(section)) return Unsupported(target, "ColdFormedOpenSection.NotQualified", "Cold-formed open sections require their own qualified design route.");
            ValidateGeometry(section);
            if (input.Section.Unperforated != true)
                return input.Section.Unperforated == false ? Unsupported(target, "PerforatedSection.NotQualified", "Gross shear areas cannot qualify a perforated section.")
                    : Missing(target, "NetSection.DeclarationRequired", "Declare an unperforated section for the gross shear-area route.", "Section.Unperforated");
            ShearResistance(input, section, axis); // Check mapping and finite arithmetic; no demand verdict.
            if (a.V1 != 0 && a.V2 != 0)
                return AwaitShearAssessment(target, SteelMethod.LinearShearInteraction);
            return AwaitShearAssessment(target, axis == 1 ? SteelMethod.SectionShear1 : SteelMethod.SectionShear2);
        }

        private static SteelMethodDecision LinearInteraction(SteelVerificationInput input, ISteelSection section, int cl, string source)
        {
            if (!ShearMappingQualified(section)) return Unsupported(SteelCheckTarget.NormalResistance,
                "ShearAxes.NotQualified", "The linear interaction requires qualified pure-shear resistances on both active axes.");
            if (input.Actions.M1 != 0) Positive(section.Wel1);
            if (input.Actions.M2 != 0) Positive(section.Wel2);
            return AwaitShearAssessment(SteelCheckTarget.NormalResistance, SteelMethod.LinearResistanceInteraction, cl, source);
        }

        private static SteelMethodDecision Member(SteelVerificationInput input)
        {
            var missing = new List<string>();
            if (input.Member == null) missing.Add("Member");
            if (!input.Code.GammaM1.HasValue) missing.Add("Code.GammaM1");
            if (missing.Count != 0) return new SteelMethodDecision(SteelCheckTarget.MemberStability,
                SteelSelectionStatus.MissingInput, null, "Member.InputRequired", "Supply member data and the member resistance factor.", missing: missing);
            return Unsupported(SteelCheckTarget.MemberStability, "MemberRouting.NotIntegrated",
                "One cut and end restraints do not establish a member load diagram, uniformity, first/second-order analysis or curve selection. Existing specialized member APIs remain explicit until the member-analysis adapter is implemented.");
        }

        internal static bool KnownGeometry(ISectionShape shape)
        {
            var t = shape.GetType();
            return t == typeof(SectionRectangular) || t == typeof(SectionCircular) || t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC) || t == typeof(SectionL)
                || t == typeof(SectionCHS) || t == typeof(SectionRHSRoundedCorners)
                || t == typeof(SectionRHS) && ((SectionRHS)shape).R == 0;
        }

        private static bool ShearMappingQualified(ISteelSection section)
        {
            var t = section.SectionShape.GetType();
            bool mapped = t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC)
                || t == typeof(SectionCHS) || t == typeof(SectionRHS) && ((SectionRHS)section.SectionShape).R == 0
                || t == typeof(SectionRHSRoundedCorners) && section.SectionType == Section.SectionTypes.Rolled
                    && section.FormedType == Section.FormedTypes.HotFinished;
            return mapped && Math.Abs(section.AngleX1) <= 1e-10;
        }

        internal static bool ColdFormedOpen(ISteelSection section)
        {
            var t = section.SectionShape.GetType();
            return section.FormedType == Section.FormedTypes.ColdFormed
                && (t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC) || t == typeof(SectionL));
        }

        internal static void ValidateGeometry(ISteelSection section)
        {
            if (!Enum.IsDefined(typeof(Section.SectionTypes), section.SectionType)
                || !Enum.IsDefined(typeof(Section.FormedTypes), section.FormedType))
                throw new ArgumentException("Unknown section fabrication/formation.");
            var shape = section.SectionShape;
            if (shape.GetType() == typeof(SectionRectangular) || shape.GetType() == typeof(SectionCircular))
            { Positive(shape.Height); Positive(shape.Width); Positive(shape.Area); return; }
            if (shape.GetType() == typeof(SectionL))
            {
                var angle = (SectionL)shape;
                Positive(angle.HorizontalLegLength - angle.VerticalLegThickness - angle.R);
                Positive(angle.VerticalLegLength - angle.HorizontalLegThickness - angle.R);
                Positive(angle.HorizontalLegThickness); Positive(angle.VerticalLegThickness);
                if (!(angle.R >= 0) || double.IsInfinity(angle.R)) throw new ArgumentException("Invalid angle radius.");
                return;
            }
            if (shape.GetType() == typeof(SectionCHS))
            {
                var chs = (SectionCHS)shape;
                Positive(chs.Diameter - 2 * chs.Thickness);
            }
            // Validate mapped widths even when a caller supplies a section class.
            // External classification cannot make impossible geometry acceptable.
            var geometry = ModelCompressionGeometry.Create(section);
            foreach (var part in geometry.Parts)
            { Positive(part.WidthOrDiameter); Positive(part.Thickness); }
        }

        private static double ShearResistance(SteelVerificationInput input, ISteelSection section, int axis)
        {
            double area = axis == 1 ? ModelShearAreas.GetArea1(section)
                : ModelShearAreas.GetArea2(section, input.Code.SectionCode.Standard);
            var result = SectionResistanceVerifier.Shear(input.Code.SectionCode, 0, area, input.Section.YieldStrength);
            if (!result.IsEvaluated) throw new ArgumentException(result.DiagnosticCode);
            return result.Resistance.Value;
        }

        private static SteelMethodDecision Guard(SteelCheckTarget target, Func<SteelMethodDecision> select)
        {
            try { return select(); }
            catch (NotSupportedException ex) { return Unsupported(target, "Preparation.NotQualified", ex.Message); }
            catch (ArgumentException ex) { return Invalid(target, "Input.InvalidGeometryOrProperty", ex.Message); }
            catch (ArithmeticException ex) { return Invalid(target, "Input.NumericalRange", ex.Message); }
        }
        private static void Positive(double value)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentException("A finite positive section property is required."); }
        private static SteelMethodDecision Ready(SteelCheckTarget target, SteelMethod method, int? cl = null, string source = null)
            => new SteelMethodDecision(target, SteelSelectionStatus.Ready, method, null, "Inputs are available for the named method; no resistance check has been executed.", cl, source);
        private static SteelMethodDecision AwaitShearAssessment(SteelCheckTarget target, SteelMethod method, int? cl = null, string source = null)
            => new SteelMethodDecision(target, SteelSelectionStatus.RequiresPreparation, method, "ShearBuckling.AssessmentRequired",
                "The pipeline must exclude shear buckling under the selected code before executing this primitive. No eta or panel model is assumed.", cl, source,
                new[] { SteelPreparationRequirement.ShearBucklingAssessment });
        private static SteelMethodDecision NotRequired(SteelCheckTarget target)
            => new SteelMethodDecision(target, SteelSelectionStatus.NotRequired, null, "Actions.Zero", "No associated action at this cut; no resistance or member-wide conclusion is implied.");
        private static SteelMethodDecision Unsupported(SteelCheckTarget target, string code, string message)
            => new SteelMethodDecision(target, SteelSelectionStatus.NotSupported, null, code, message);
        private static SteelMethodDecision Invalid(SteelCheckTarget target, string code, string message)
            => new SteelMethodDecision(target, SteelSelectionStatus.InvalidInput, null, code, message);
        private static SteelMethodDecision Missing(SteelCheckTarget target, string code, string message, params string[] names)
            => new SteelMethodDecision(target, SteelSelectionStatus.MissingInput, null, code, message, missing: names);

        internal sealed class ClassificationInfo
        {
            internal int? Class { get; }
            internal string Source { get; }
            internal SteelMethodDecision Failure { get; }
            internal ClassificationInfo(int cl, string source) { Class = cl; Source = source; }
            internal ClassificationInfo(SteelMethodDecision failure) { Failure = failure; }
        }
    }
}
