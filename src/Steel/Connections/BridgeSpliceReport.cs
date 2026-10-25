using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;

namespace GPC.Checkers.Steel.Connections
{
    public enum SpliceReportRole { Resistance, Applicability, LinkedMember }

    /// <summary>Original check references are retained. A missing scalar demand is not reconstructed
    /// from an interaction utilization. Applicability ratios cannot govern joint resistance.</summary>
    public sealed class SpliceReportRow
    {
        public string CaseId { get; }
        public string Component { get; }
        public bool IsServiceability { get; }
        public SpliceReportRole Role { get; }
        public ConnectionCheck ConnectionCheck { get; }
        public SteelCheckExecution SteelCheck { get; }
        public string Id => ConnectionCheck?.Id ?? SteelCheck.Decision.Target.ToString();
        public double? Demand => ConnectionCheck == null ? (double?)null : ConnectionCheck.Demand;
        public double? Resistance => ConnectionCheck == null ? SteelCheck.Resistance : ConnectionCheck.Resistance;
        public double? Utilization => ConnectionCheck == null ? SteelCheck.Utilization : ConnectionCheck.Utilization;
        public string Unit => ConnectionCheck?.Unit ?? SteelCheck.ResistanceUnit;
        public string Reference => ConnectionCheck?.Reference ?? SteelCheck.Reference;
        public string Status => ConnectionCheck == null ? SteelCheck.Status.ToString() : ConnectionCheck.Passed ? "Passed" : "Failed";
        public bool Failed => ConnectionCheck == null ? SteelCheck.Status == SteelExecutionStatus.Failed : !ConnectionCheck.Passed;
        internal SpliceReportRow(string id, string component, bool sls, SpliceReportRole role, ConnectionCheck connection, SteelCheckExecution steel = null)
        { CaseId = id; Component = component; IsServiceability = sls; Role = role; ConnectionCheck = connection; SteelCheck = steel; }
    }

    public sealed class SpliceReportCase
    {
        public string Id { get; }
        public string Source { get; }
        public ConnectionDesignCode Code { get; }
        public PlanarConnectionActions Actions { get; }
        public bool IsServiceability { get; }
        internal SpliceReportCase(string id, string source, ConnectionDesignCode code, PlanarConnectionActions actions, bool sls = false)
        { Id = id; Source = source; Code = code; Actions = actions; IsServiceability = sls; }
    }

    public sealed class BridgeSpliceReport
    {
        public string Typology { get; }
        public string Scope { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete { get; }
        public bool HasKnownFailure { get; }
        public bool HasFailedLinkedMember => Rows.Any(x => x.Role == SpliceReportRole.LinkedMember && x.Failed);
        public IReadOnlyList<string> MissingRequirements { get; }
        public IReadOnlyList<string> Notes { get; }
        public IReadOnlyList<SpliceReportCase> Cases { get; }
        public IReadOnlyList<SpliceReportRow> Rows { get; }
        public SpliceReportRow GoverningResistance => !IsComplete ? null : Rows.Where(x => x.Role == SpliceReportRole.Resistance)
            .OrderByDescending(x => x.Failed && !x.Utilization.HasValue).ThenByDescending(x => x.Utilization ?? 0).FirstOrDefault();

        private BridgeSpliceReport(string typology, string scope, ConnectionStatus status, bool complete, bool failed,
            IEnumerable<string> missing, IEnumerable<string> notes, IEnumerable<SpliceReportCase> cases, IEnumerable<SpliceReportRow> rows)
        {
            Typology = typology; Scope = scope; Status = status; IsComplete = complete; HasKnownFailure = failed;
            MissingRequirements = Array.AsReadOnly(missing.Distinct().ToArray()); Notes = Array.AsReadOnly(notes.Distinct().ToArray());
            Cases = Array.AsReadOnly(cases.ToArray()); Rows = Array.AsReadOnly(rows.ToArray());
        }

        public static BridgeSpliceReport FromBolted(WeldedIBeamSpliceStandardResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var rows = result.CheckSources.Select(x => new SpliceReportRow(x.CaseId, x.Component, x.IsServiceability,
                // Geometry/pitch constraints are not action utilization.
                x.Component == "Detailing" ? SpliceReportRole.Applicability : SpliceReportRole.Resistance, x.ComponentCheck, x.SectionCheck)).ToList();
            var cases = new List<SpliceReportCase>();
            var notes = new List<string> { "Block method: " + result.Input.BlockMethod,
                "N,mm,MPa; physical X longitudinal,Y up; Steel M1=-Mz after pole transport, V2=Fy.",
                "Fatigue, rotational stiffness and execution certification are excluded. Whole-member assessments remain separate." };
            foreach (var c in result.Cases)
            {
                var b = c.Input.Parents.Detail.Plates.Bolts;
                cases.Add(new SpliceReportCase(c.Input.Id, b.UltimateCombinationSource, b.Code, b.Detail.Actions));
                notes.Add("gammaM1 plates=" + Number(c.Input.Parents.Detail.Plates.Policy.GammaM1));
                Add(c.Input.Id, "BlockHierarchy", c.AdditionalPlates.BlockHierarchy.Checks);
                Add(c.Input.Id, "CompactPlateDomain", c.AdditionalPlates.CompactPlateDomain.Checks);
                foreach (var t in c.AdditionalPlates.BlockHierarchy.Trace.Where(t => t.Name.EndsWith(".ActualCollinearBlockMethod")))
                    notes.Add(c.Input.Id + ":" + t.Name + "=" + Number(t.Value));
                Member(c.Input.Id, "Left.Member", c.Input.Parents.LeftMember);
                Member(c.Input.Id, "Right.Member", c.Input.Parents.RightMember);
                // Preserve unevaluated section checks alongside their aggregate diagnostics.
                Pending(c.Input.Id, "Left.Section", c.Input.Parents.LeftSection);
                Pending(c.Input.Id, "Right.Section", c.Input.Parents.RightSection);
            }
            foreach (var c in result.SlipCases)
            {
                var b = c.Input.Input.Bolts;
                notes.Add(c.Input.Id + ":gammaM3,ULS=" + Number(c.Input.Input.Policy.GammaM3Ultimate)
                    + ";gammaM3,SLS=" + Number(c.Input.Input.Policy.GammaM3Serviceability) + ";tightening=" + c.Input.Input.TighteningControl);
                notes.Add(c.Input.Id + ":assembly=" + c.Input.Input.AssemblyQualificationSource);
                foreach (var surface in c.Input.Input.Surfaces)
                    notes.Add(c.Input.Id + ":" + surface.Cover + ":mu=" + Number(surface.Coefficient) + ";source=" + surface.QualificationSource);
                cases.Add(new SpliceReportCase(c.Input.Id, c.Input.Input.ActionCombinationSource, b.Code, b.Detail.Actions,
                    c.Input.Input.LimitState == BoltSlipLimitState.Serviceability));
            }
            return new BridgeSpliceReport("WeldedI.DoubleCover", result.Scope, result.Status, result.IsStaticResistanceComplete,
                result.HasFailedComponent, result.MissingRequirements, notes, cases, rows);
            void Add(string id, string part, IEnumerable<ConnectionCheck> checks)
                => rows.AddRange(checks.Select(x => new SpliceReportRow(id, part, false, SpliceReportRole.Applicability, x)));
            void Member(string id, string part, SteelVerificationResult r)
            {
                if (r == null) { notes.Add(id + ":" + part + ":NOT_SUPPLIED"); return; }
                rows.AddRange(r.Checks.Select(x => new SpliceReportRow(id, part, false, SpliceReportRole.LinkedMember, null, x)));
            }
            void Pending(string id, string part, SteelVerificationResult r)
                => rows.AddRange(r.Checks.Where(x => !x.IsEvaluated).Select(x => new SpliceReportRow(id, part, false, SpliceReportRole.Resistance, null, x)));
        }

        public static BridgeSpliceReport FromWelded(IEnumerable<WeldedSpliceButtCaseResult> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var results = input.ToArray();
            if (results.Length == 0 || results.Any(x => x == null) || results.Select(x => x.Case.Id).Distinct(StringComparer.Ordinal).Count() != results.Length)
                throw new ArgumentException("Non-null, uniquely identified welded cases required.");
            var rows = new List<SpliceReportRow>(); var missing = new List<string>();
            var cases = new List<SpliceReportCase>(); var notes = new List<string>();
            foreach (var c in results)
            {
                var r = c.Result;
                var first = results[0].Result.Input;
                if (!WeldedIBeamSplicePlates.SameCode(first.Code, r.Input.Code)
                    || !SameSection(first.LeftSection.Plan.Input.Section, r.LeftSection.Plan.Input.Section)
                    || !SameSection(first.RightSection.Plan.Input.Section, r.RightSection.Plan.Input.Section)
                    || !SameWeld(first.TopFlange, r.Input.TopFlange) || !SameWeld(first.Web, r.Input.Web)
                    || !SameWeld(first.BottomFlange, r.Input.BottomFlange))
                    missing.Add(c.Case.Id + ":PHYSICAL_SPLICE_OR_POLICY_MISMATCH");
                cases.Add(new SpliceReportCase(c.Case.Id, c.Case.Source, r.Input.Code, c.Case.Actions));
                missing.AddRange(r.SeamPrerequisites.Diagnostics.Select(x => c.Case.Id + ":" + x));
                foreach (var check in r.SeamPrerequisites.Checks)
                    rows.Add(new SpliceReportRow(c.Case.Id, "CJP.ConsumablePrerequisites", false, SpliceReportRole.Applicability, check));
                Parent("Left.Section", r.LeftSection); Parent("Right.Section", r.RightSection);
                if (!r.IsStaticResistanceComplete) missing.Add(c.Case.Id + ":INCOMPLETE_CJP_SPLICE");
                void Parent(string part, SteelVerificationResult section)
                {
                    foreach (var check in section.Checks)
                    {
                        rows.Add(new SpliceReportRow(c.Case.Id, part, false, SpliceReportRole.Resistance, null, check));
                        if (!check.IsEvaluated && check.Status != SteelExecutionStatus.NotRequired)
                            missing.Add(c.Case.Id + ":" + part + ":" + check.DiagnosticCode);
                    }
                }
            }
            bool complete = missing.Count == 0 && results.All(c => c.Result.IsStaticResistanceComplete);
            bool failed = results.Any(c => c.Result.HasFailedSection || c.Result.SeamPrerequisites.Checks.Any(x => !x.Passed));
            notes.Add("Same positive section-face convention on both members. Full penetration and matching consumables are prerequisites, not inspection results.");
            notes.Add("No member stability, fatigue or rotational stiffness assessment.");
            return new BridgeSpliceReport("WeldedI.CJP", results[0].Result.Scope,
                !complete ? ConnectionStatus.NotSupported : failed ? ConnectionStatus.Failed : ConnectionStatus.Passed,
                complete, failed, missing, notes, cases, rows);
        }

        private static bool SameSection(SteelSectionInput a, SteelSectionInput b)
        {
            var x = a.CreateSectionCopy().SectionShape as GPC.Model.Sections.SectionH;
            var y = b.CreateSectionCopy().SectionShape as GPC.Model.Sections.SectionH;
            return x != null && y != null && x.Height == y.Height && x.ThicknessWeb == y.ThicknessWeb
                && x.LenghtTopFlange == y.LenghtTopFlange && x.LenghtBottomFlange == y.LenghtBottomFlange
                && x.ThicknessTopFlange == y.ThicknessTopFlange && x.ThicknessBottomFlange == y.ThicknessBottomFlange && x.R == y.R
                && a.YieldStrength == b.YieldStrength && a.UltimateStrength == b.UltimateStrength && a.ElasticModulus == b.ElasticModulus
                && a.Unperforated == b.Unperforated && a.NetArea == b.NetArea && a.PrincipalAxisAngle == b.PrincipalAxisAngle;
        }
        private static bool SameWeld(SpliceButtWeldDetail a, SpliceButtWeldDetail b)
            => a.Penetration == b.Penetration && a.FusionDepth == b.FusionDepth && a.EffectiveLength == b.EffectiveLength
                && a.AllWeldMinimumFy == b.AllWeldMinimumFy && a.AllWeldMinimumFu == b.AllWeldMinimumFu;

        /// <summary>Deterministic tab-separated engineering report; blank scalar cells mean unavailable.
        /// Escapes embedded tabs/newlines in caller labels. No filesystem or UI dependency.</summary>
        public string ToText()
        {
            var b = new StringBuilder();
            b.AppendLine(Typology + "\t" + Status + "\tComplete=" + IsComplete);
            b.AppendLine(Escape(Scope));
            foreach (var c in Cases)
            {
                var a = c.Actions; var code = c.Code;
                b.AppendLine("CASE\t" + Escape(c.Id) + "\t" + (c.IsServiceability ? "SLS" : "ULS") + "\t" + Escape(c.Source));
                b.AppendLine("CODE\t" + Escape(code.Plates.Name + " " + code.Plates.Edition + " " + code.Plates.ParameterSet)
                    + "\tgammaM0=" + Number(code.Plates.GammaM0) + "\tgammaM2Plate=" + Number(code.Plates.GammaM2) + "\tgammaM2Connection=" + Number(code.GammaM2Connection));
                b.AppendLine("ACTIONS\tFx=" + Number(a.Fx) + "\tFy=" + Number(a.Fy) + "\tMz=" + Number(a.Mz)
                    + "\tx=" + Number(a.ApplicationX) + "\ty=" + Number(a.ApplicationY) + "\t[N,mm,Nmm]");
            }
            foreach (var x in Notes) b.AppendLine("NOTE\t" + Escape(x));
            foreach (var x in MissingRequirements) b.AppendLine("MISSING\t" + Escape(x));
            b.AppendLine("Case\tLimitState\tRole\tComponent\tCheck\tStatus\tDemand\tResistance\tUtilization\tUnit\tReference");
            foreach (var x in Rows)
                b.AppendLine(string.Join("\t", new[] { Escape(x.CaseId), x.IsServiceability ? "SLS" : "ULS", x.Role.ToString(),
                    Escape(x.Component), Escape(x.Id), x.Status, Number(x.Demand), Number(x.Resistance), Number(x.Utilization), Escape(x.Unit), Escape(x.Reference) }));
            return b.ToString();
        }
        private static string Number(double? x) => x.HasValue ? x.Value.ToString("G17", CultureInfo.InvariantCulture) : "";
        private static string Escape(string x) => (x ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}

