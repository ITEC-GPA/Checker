using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Selection
{
    /// <summary>Deterministic routing over existing APIs. Uses existing classification
    /// and shear-area/capacity helpers only as applicability gates. No new resistance
    /// method, implicit national profile, member model or legacy-checker fallback.</summary>
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
            if (!moment && shear) return Unsupported(target, "AxialShearInteraction.NotIntegrated", "Isolated axial and shear checks do not establish N-V interaction.");

            if (!moment && a.N > 0)
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
            if (interaction && (shape.GetType() != typeof(SectionH)
                || ec3 && (((SectionH)shape).R != 0 || Math.Abs(shape.AngleX1) > 1e-10)))
                return Unsupported(target, "NormalInteraction.GeometryNotQualified", "The integrated N-M route requires exact H geometry; EC3 additionally requires sharp edges and horizontal principal axis 1.");
            if (moment && shape.GetType() == typeof(SectionL))
                return Unsupported(target, "AngleBending.NotQualified", "Angle bending is not enabled by the compression-only classification route.");
            if (moment && shape.GetType() == typeof(SectionRHSRoundedCorners))
                return Unsupported(target, "RoundedRhs.BendingNotQualified", "Rounded RHS mapping is currently qualified for uniform compression only.");
            if (moment && !ec3 && interaction && shear)
                return Unsupported(target, "NtcElasticInteraction.ShearExcluded", "The NTC elastic normal-stress bound excludes shear.");

            var classInfo = Classification(input, section, suppliedClass);
            if (classInfo.Failure != null) return classInfo.Failure;
            int cl = classInfo.Class.Value;
            if (cl == 4) return new SteelMethodDecision(target, SteelSelectionStatus.NotSupported, null,
                "Class4.EffectivePropertiesRequired", "A qualified effective-section method or a separately established state classification is required.", cl, classInfo.Source);
            if (interaction && ec3 && cl > 2) return new SteelMethodDecision(target, SteelSelectionStatus.NotSupported, null,
                "PlasticH.Class1Or2Required", "Plastic H equilibrium requires class 1 or 2; no elastic fallback is inferred.", cl, classInfo.Source);

            SteelMethod method = !moment ? SteelMethod.SectionCompression
                : interaction ? (ec3 ? SteelMethod.Ec3PlasticHInteraction : SteelMethod.NtcElasticNormalInteraction)
                : a.M1 != 0 ? SteelMethod.SectionBending1 : SteelMethod.SectionBending2;
            if (moment)
            {
                if (method == SteelMethod.NtcElasticNormalInteraction)
                { Positive(section.Wel1); Positive(section.Wel2); }
                else if (method != SteelMethod.Ec3PlasticHInteraction)
                    Positive(a.M1 != 0 ? (cl <= 2 ? section.Wpl1 : section.Wel1) : (cl <= 2 ? section.Wpl2 : section.Wel2));
            }
            if (!shear) return Ready(target, method, cl, classInfo.Source);
            if (!ShearMappingQualified(section)) return Unsupported(target, "ShearAxes.NotQualified", "Low shear cannot be established from an unqualified principal-axis shear mapping.");
            if (Math.Abs(a.V1) > .5 * ShearResistance(input, section, 1)
                || Math.Abs(a.V2) > .5 * ShearResistance(input, section, 2))
                return Unsupported(target, "HighShearInteraction.NotIntegrated", "The selected normal-resistance method requires both shear ratios <= 0.5.");
            return AwaitShearAssessment(target, method, cl, classInfo.Source);
        }

        private static ClassificationInfo Classification(SteelVerificationInput input, ISteelSection section,
            SteelSectionClassification supplied)
        {
            if (supplied != null) return new ClassificationInfo(supplied.SectionClass, supplied.Source);
            var shape = section.SectionShape;
            if (input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018)
            {
                if (shape.GetType() == typeof(SectionCHS))
                {
                    var chs = (SectionCHS)shape; Positive(chs.Diameter); Positive(chs.Thickness);
                    if (2 * chs.Thickness >= chs.Diameter) throw new ArgumentException("Invalid hollow geometry.");
                    return new ClassificationInfo(Ntc2018DesignPolicy.CircularClass(chs.Diameter, chs.Thickness, input.Section.YieldStrength),
                        "Ntc2018DesignPolicy.CircularClass; nominal uniform compression");
                }
                return new ClassificationInfo(Missing(SteelCheckTarget.NormalResistance, "NtcClassification.Required",
                    "Supply a classification for this NTC input. The selector does not use the EC3 classifier for NTC.", "Classification"));
            }
            if (shape.GetType() == typeof(SectionL))
            {
                var angle = (SectionL)shape;
                if (angle.HorizontalLegThickness != angle.VerticalLegThickness)
                    return new ClassificationInfo(Unsupported(SteelCheckTarget.NormalResistance, "AngleThickness.NotQualified", "Automatic angle compression classification requires equal leg thicknesses."));
                return new ClassificationInfo(Ec3AngleCompression.Classify(angle.VerticalLegLength, angle.HorizontalLegLength,
                    angle.VerticalLegThickness, input.Section.YieldStrength, angle.R), "Ec3AngleCompression; nominal uniform compression");
            }
            if (!ModelCompressionGeometry.CanMap(section))
                return new ClassificationInfo(Unsupported(SteelCheckTarget.NormalResistance, "Classification.GeometryNotQualified", "No automatic uniform-compression mapping for this exact geometry."));
            var result = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), input.Section.YieldStrength);
            if (!result.IsEvaluated) return new ClassificationInfo(Invalid(SteelCheckTarget.NormalResistance, result.DiagnosticCode, "Uniform-compression classification could not be established."));
            return new ClassificationInfo(result.NominalClass.Value, "Ec3UniformCompressionClassification; conservative nominal uniform-compression class");
        }

        private static SteelMethodDecision Shear(SteelVerificationInput input, ISteelSection section, int axis)
        {
            var target = axis == 1 ? SteelCheckTarget.Shear1 : SteelCheckTarget.Shear2;
            var a = input.Actions;
            if ((axis == 1 ? a.V1 : a.V2) == 0) return NotRequired(target);
            if (a.T != 0) return Unsupported(target, "ShearTorsionInteraction.NotIntegrated", "Shear with torsion requires a qualified interaction method.");
            if (a.V1 != 0 && a.V2 != 0) return Unsupported(target, "BiaxialShear.NotIntegrated", "Two independent directional checks do not certify simultaneous biaxial shear.");
            if (!ShearMappingQualified(section)) return Unsupported(target, "ShearAxes.NotQualified", "No qualified shear-area mapping for this exact geometry and principal-axis orientation.");
            if (ColdFormedOpen(section)) return Unsupported(target, "ColdFormedOpenSection.NotQualified", "Cold-formed open sections require their own qualified design route.");
            ValidateGeometry(section);
            if (input.Section.Unperforated != true)
                return input.Section.Unperforated == false ? Unsupported(target, "PerforatedSection.NotQualified", "Gross shear areas cannot qualify a perforated section.")
                    : Missing(target, "NetSection.DeclarationRequired", "Declare an unperforated section for the gross shear-area route.", "Section.Unperforated");
            ShearResistance(input, section, axis); // Check mapping and finite arithmetic; no demand verdict.
            return AwaitShearAssessment(target, axis == 1 ? SteelMethod.SectionShear1 : SteelMethod.SectionShear2);
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

        private static bool KnownGeometry(ISectionShape shape)
        {
            var t = shape.GetType();
            return t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC) || t == typeof(SectionL)
                || t == typeof(SectionCHS) || t == typeof(SectionRHSRoundedCorners)
                || t == typeof(SectionRHS) && ((SectionRHS)shape).R == 0;
        }

        private static bool ShearMappingQualified(ISteelSection section)
        {
            var t = section.SectionShape.GetType();
            bool mapped = t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC)
                || t == typeof(SectionCHS) || t == typeof(SectionRHS) && ((SectionRHS)section.SectionShape).R == 0;
            return mapped && Math.Abs(section.AngleX1) <= 1e-10;
        }

        private static bool ColdFormedOpen(ISteelSection section)
        {
            var t = section.SectionShape.GetType();
            return section.FormedType == Section.FormedTypes.ColdFormed
                && (t == typeof(SectionH) || t == typeof(SectionT) || t == typeof(SectionC) || t == typeof(SectionL));
        }

        private static void ValidateGeometry(ISteelSection section)
        {
            if (!Enum.IsDefined(typeof(Section.SectionTypes), section.SectionType)
                || !Enum.IsDefined(typeof(Section.FormedTypes), section.FormedType))
                throw new ArgumentException("Unknown section fabrication/formation.");
            var shape = section.SectionShape;
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

        private sealed class ClassificationInfo
        {
            internal int? Class { get; }
            internal string Source { get; }
            internal SteelMethodDecision Failure { get; }
            internal ClassificationInfo(int cl, string source) { Class = cl; Source = source; }
            internal ClassificationInfo(SteelMethodDecision failure) { Failure = failure; }
        }
    }
}
