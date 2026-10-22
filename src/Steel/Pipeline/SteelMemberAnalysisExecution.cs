using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelMemberAnalysisExecution
    {
        internal static SteelMethodDecision Select(SteelMemberAnalysis analysis)
        {
            if (analysis.BeamColumnParameters != null) return SteelBeamColumnExecution.Select(analysis);
            if (analysis.Prismatic != null) return SteelPrismaticMemberExecution.Select(analysis);
            var i = analysis.Input; var missing = new List<string>();
            if (i.Member == null) missing.Add("Member");
            if (!i.Code.GammaM1.HasValue) missing.Add("Code.GammaM1");
            if (i.Section.Unperforated == null) missing.Add("Section.Unperforated");
            if (missing.Count > 0) return Decision(SteelSelectionStatus.MissingInput, "Member.InputRequired", string.Join(", ", missing), missing);
            if (i.Code.SectionCode.Standard != SectionResistanceStandard.Eurocode3_2005)
                return Decision(SteelSelectionStatus.NotSupported, "Member.NtcMethodNotQualified", "This EC3 general-method route is not qualified as an NTC method.");
            var a = i.Actions;
            if (i.Section.Unperforated != true || a.N > 0 || a.M2 != 0 || a.V1 != 0 || a.V2 != 0 || a.T != 0)
                return Decision(SteelSelectionStatus.NotSupported, "Member.ActionPatternNotQualified", "Unperforated in-plane compression/major bending only; no transverse actions or torsion.");
            if (a.N == 0 && a.M1 == 0) return Decision(SteelSelectionStatus.NotRequired, "Member.NoActions", "No active member action pattern.");
            if (analysis.Kind == SteelMemberAnalysisKind.UniformForkHOrT)
            {
                var member = i.Member;
                if (member.Imperfections != null || !Length(member.BucklingLength1, member.Length)
                    || !Length(member.BucklingLength2, member.Length) || !Length(member.TorsionalBucklingLength, member.Length)
                    || !Fork(member.StartRestraints) || !Fork(member.EndRestraints))
                    return Decision(SteelSelectionStatus.NotSupported, "Member.UniformForkConflict", "Member metadata conflicts with the explicit fork/first-order route and its internal imperfection model.");
                var section = i.Section.CreateSectionCopy();
                var shape = section.SectionShape;
                if (section.FormedType == Section.FormedTypes.ColdFormed || Math.Abs(shape.AngleX1) > 1e-10
                    || !(shape.GetType() == typeof(SectionH) && ((SectionH)shape).R == 0 || shape.GetType() == typeof(SectionT) && ((SectionT)shape).R == 0))
                    return Decision(SteelSelectionStatus.NotSupported, "Member.UniformGeometryNotQualified", "Uniform fork route requires exact sharp H/T and horizontal major axis; cold-formed excluded.");
                return Ready(SteelMethod.Ec3UniformForkMember);
            }
            foreach (var quantity in new[] { SteelStabilityQuantity.AlphaUltK, SteelStabilityQuantity.AlphaCrOp })
                if (!analysis.Values.Any(v => v.Quantity == quantity)) missing.Add(quantity.ToString());
            if (!analysis.ExternalMemberClass.HasValue) missing.Add("MemberClassification");
            if (missing.Count > 0) return Decision(SteelSelectionStatus.MissingInput, "Member.ExternalAnalysisIncomplete", string.Join(", ", missing), missing);
            if (analysis.ExternalMemberClass == 4)
                return Decision(SteelSelectionStatus.NotSupported, "Member.Class4NotQualified", "A qualified effective-section analysis route is required.");
            return Ready(SteelMethod.Ec3ExternalGeneralMember);
        }

        private static bool Length(double? actual, double expected) => !actual.HasValue || actual == expected;
        private static bool Matches(SteelRestraintState value, SteelRestraintState expected) => value == SteelRestraintState.Unknown || value == expected;
        private static bool Fork(SteelEndRestraints r) => r == null || Matches(r.Translation1, SteelRestraintState.Fixed)
            && Matches(r.Translation2, SteelRestraintState.Fixed) && Matches(r.Rotation1, SteelRestraintState.Free)
            && Matches(r.Rotation2, SteelRestraintState.Free) && Matches(r.Rotation3, SteelRestraintState.Fixed)
            && Matches(r.Warping, SteelRestraintState.Free);
        private static SteelMethodDecision Ready(SteelMethod method) => new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.Ready, method, null, null);
        private static SteelMethodDecision Decision(SteelSelectionStatus status, string code, string message, IEnumerable<string> missing = null)
            => new SteelMethodDecision(SteelCheckTarget.MemberStability, status, null, code, message, missing: missing);

        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis, SteelMethodDecision decision)
        {
            if (analysis.BeamColumnParameters != null) return SteelBeamColumnExecution.Execute(analysis, decision);
            if (analysis.Prismatic != null) return SteelPrismaticMemberExecution.Execute(analysis, decision);
            var i = analysis.Input; var trace = new List<SectionResistanceValue>();
            Ec3GeneralMethodResult result; double utilization;
            if (analysis.Kind == SteelMemberAnalysisKind.UniformForkHOrT)
            {
                var r = Ec3MonosymmetricMember.EvaluateUniformFork(i.Section.CreateSectionCopy(), i.Member.Length, i.Actions.N, i.Actions.M1,
                    analysis.InPlaneCurve, analysis.LateralCurve, analysis.LateralTorsionalCurve,
                    i.Code.SectionCode.GammaM0, i.Code.GammaM1.Value, analysis.ParameterSet);
                result = r.GeneralMethod; utilization = r.Utilization;
                Add(trace, "Ncr1", r.NcrInPlane, "N"); Add(trace, "Ncr2", r.NcrOutOfPlane, "N");
                Add(trace, "InitialBow", r.InitialBow, "mm"); Add(trace, "SectionUtilization", r.SectionUtilization, "1");
                Add(trace, "MemberClass", r.NominalCompressionClass, "1");
                if (i.Actions.N == 0) Add(trace, "Mcr", result.AlphaCrOp * Math.Abs(i.Actions.M1), "Nmm");
            }
            else
            {
                foreach (var v in analysis.Values) Add(trace, v.Quantity.ToString(), v.Value, v.Unit);
                result = Ec3GeneralMethod.EvaluateInPlane(analysis.Values.Single(v => v.Quantity == SteelStabilityQuantity.AlphaUltK).Value,
                    analysis.Values.Single(v => v.Quantity == SteelStabilityQuantity.AlphaCrOp).Value,
                    analysis.LateralCurve, analysis.LateralTorsionalCurve, i.Code.GammaM1.Value, analysis.ParameterSet,
                    analysis.LoadPatternReference + "; " + string.Join("; ", analysis.Values.Select(v => v.Quantity + ": " + v.Source)));
                utilization = result.Utilization; Add(trace, "MemberClass", analysis.ExternalMemberClass.Value, "1");
            }
            if (analysis.Kind == SteelMemberAnalysisKind.UniformForkHOrT)
            { Add(trace, "AlphaUltK", result.AlphaUltK, "1"); Add(trace, "AlphaCrOp", result.AlphaCrOp, "1"); }
            Add(trace, "Slenderness", result.Slenderness, "1"); Add(trace, "Chi", result.Chi, "1"); Add(trace, "ChiLT", result.ChiLT, "1");
            Add(trace, "GammaM1", result.GammaM1, "1"); Add(trace, "DesignLoadAmplifier", result.DesignLoadAmplifier, "1");
            SteelInputValidation.Finite(utilization, "member utilization");
            return new SteelCheckExecution(decision, utilization <= 1 ? SteelExecutionStatus.Passed : SteelExecutionStatus.Failed,
                reference: result.Reference, scope: analysis.Kind + "; " + analysis.LoadPatternReference + "; " + analysis.ParameterSet,
                utilization: utilization, trace: trace);
        }
        private static void Add(List<SectionResistanceValue> trace, string name, double value, string unit)
        { SteelInputValidation.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
    }
}
