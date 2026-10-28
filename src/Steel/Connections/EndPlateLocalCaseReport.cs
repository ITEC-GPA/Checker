using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Original checks and traces of one child; no reconstructed utilization.</summary>
    internal sealed class EndPlateReportComponent
    {
        public string Id { get; }
        public ConnectionStatus Status { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        internal EndPlateReportComponent(string id, ConnectionStatus status, IEnumerable<ConnectionCheck> checks,
            IEnumerable<SectionResistanceValue> trace)
        { Id = id; Status = status; Checks = Array.AsReadOnly(checks.ToArray()); Trace = Array.AsReadOnly(trace.ToArray()); }
    }

    /// <summary>Reviewable local calculation evidence even when aggregation is unsupported.
    /// A passing component set never becomes a passing complete moment joint.</summary>
    internal sealed class EndPlateLocalCaseReport
    {
        public string CaseId { get; }
        public EndPlateLocalCaseAssessmentResult Assessment { get; }
        public ConnectionCapability Capability => ConnectionCapabilityCatalog.Get("MomentEndPlate");
        public IReadOnlyList<EndPlateReportComponent> Components { get; }
        public bool HasKnownFailure => Assessment.HasFailedComponent || Components.Any(c => c.Checks.Any(x => !x.Passed));
        public bool IsWholeJointComplete => false;
        public IReadOnlyList<string> PendingChecks => Assessment.PendingWholeJointChecks;
        private EndPlateLocalCaseReport(string id, EndPlateLocalCaseAssessmentResult assessment)
        {
            CaseId = id; Assessment = assessment; var rows = new List<EndPlateReportComponent>();
            var b = assessment.BeamAndMoment; var c = assessment.Connection;
            rows.Add(new EndPlateReportComponent("BeamSection", b.BeamSection.Status, b.BeamSection.Checks, b.BeamSection.Trace));
            AddMoment("BeamMoment", b.MomentComponents);
            if (!ReferenceEquals(b.MomentComponents, c.MomentComponents)) AddMoment("ConnectionMoment", c.MomentComponents);
            if (!ReferenceEquals(c.MomentComponents, c.WeldMomentComponents)) AddMoment("WeldMoment", c.WeldMomentComponents);
            rows.Add(new EndPlateReportComponent("Bolts", c.Bolts.BoltAssessment.Status, c.Bolts.BoltAssessment.Checks, c.Bolts.BoltAssessment.Trace));
            if (c.FilletWelds != null)
                rows.Add(new EndPlateReportComponent("FilletWelds", c.FilletWelds.WeldAssessment.Status, c.FilletWelds.WeldAssessment.Checks, c.FilletWelds.WeldAssessment.Trace));
            else
                rows.Add(new EndPlateReportComponent("TopFlushCjpWelds", c.TopFlushCjpWelds.WeldAssessment.Status, c.TopFlushCjpWelds.WeldAssessment.Checks, c.TopFlushCjpWelds.WeldAssessment.Trace));
            var s = assessment.InitialStiffnessAssessment;
            rows.Add(new EndPlateReportComponent("InitialStiffness", s.Status, Array.Empty<ConnectionCheck>(), s.Trace));
            Components = Array.AsReadOnly(rows.ToArray());
            void AddMoment(string name, EndPlateMomentComponentResult m)
            { if (m != null) rows.Add(new EndPlateReportComponent(name, m.ComponentAssessment.Status, m.ComponentAssessment.Checks, m.ComponentAssessment.Trace)); }
        }
        public static EndPlateLocalCaseReport Create(string caseId, EndPlateLocalCaseAssessmentResult assessment)
        {
            if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("A case ID is required.", nameof(caseId));
            if (assessment == null) throw new ArgumentNullException(nameof(assessment));
            return new EndPlateLocalCaseReport(caseId, assessment);
        }
        public string ToText(bool includeTrace = false)
        {
            var a = Assessment.Actions; var m = Assessment.BeamAndMoment.Input; var code = m.Policy.Code;
            var text = new StringBuilder();
            Line("CASE", CaseId); Line("COVERAGE", Capability.Level); Line("WHOLE_JOINT_COMPLETE", false);
            Line("COHERENT_CASE", Assessment.HasCoherentCase); Line("COVERED_COMPONENTS", Assessment.CoveredComponentsStatus);
            Line("KNOWN_FAILURE", HasKnownFailure); Line("SCOPE", Assessment.Scope);
            Line("CODE", code.IsNtc ? "NTC2018; EC3:2005 supplementary component model" : "EC3:2005");
            Line("SOURCE", m.ActionCombinationSource);
            Line("AXES", "Beam: longitudinal3=Z; 1=X,2=Y. M1>0 tensions upper flange. V2 keeps its sign; local bolt/weld checks use magnitude.");
            Value("N", a.N, "N"); Value("V1", a.V1, "N"); Value("V2", a.V2, "N");
            Value("T", a.T, "Nmm"); Value("M1", a.M1, "Nmm"); Value("M2", a.M2, "Nmm");
            Value("beta", m.Beta, "1"); Value("gammaM0", code.Plates.GammaM0, "1");
            Value("gammaM2Plate", code.Plates.GammaM2, "1"); Value("gammaM2Connection", code.GammaM2Connection, "1");
            Value("Beam.h", m.Beam.Height, "mm"); Value("Beam.b", m.Beam.Width, "mm");
            Value("Beam.tw", m.Beam.WebThickness, "mm"); Value("Beam.tf", m.Beam.FlangeThickness, "mm");
            Value("Column.h", m.Column.Height, "mm"); Value("Column.b", m.Column.Width, "mm");
            Value("Column.tw", m.Column.WebThickness, "mm"); Value("Column.tf", m.Column.FlangeThickness, "mm");
            Value("EndPlate.b", m.EndPlate.Width, "mm"); Value("EndPlate.t", m.EndPlate.Thickness, "mm");
            Value("EndPlate.fy", m.EndPlate.Fy, "MPa"); Value("EndPlate.fu", m.EndPlate.Fu, "MPa");
            Value("Bolt.d", m.Bolt.Diameter, "mm"); Value("Bolt.d0", m.Bolt.HoleDiameter, "mm");
            Value("Bolt.As", m.Bolt.TensileArea, "mm2"); Line("Bolt.grade", m.Bolt.Grade);
            Value("Column.sigmaLongitudinal", m.ColumnLongitudinalCompressionStress, "MPa");
            Line("ColumnStressSource", m.ColumnStressSource); Line("TransformationSource", m.TransformationSource);
            Line("ColumnSwayRestraintSource", m.ColumnSwayRestraintSource);
            Line("HardwareSource", Assessment.Connection.Bolts.BoltAssessment.Input.Hardware.Source);
            Line("ShearDistributionSource", Assessment.Connection.Bolts.BoltAssessment.Input.ShearDistributionSource);
            // No stiffness is exported from mismatched assessments; child evidence is still listed.
            if (Assessment.InitialStiffness.HasValue) Value("Sj.initial", Assessment.InitialStiffness.Value, "Nmm/rad");
            foreach (var component in Components)
            {
                Line("COMPONENT", component.Id, component.Status);
                foreach (var check in component.Checks)
                    Line("CHECK", component.Id, check.Id, check.Passed ? "Passed" : "Failed", Number(check.Demand),
                        Number(check.Resistance), check.Unit, Number(check.Utilization), check.Reference);
                if (includeTrace) foreach (var v in component.Trace) Line("TRACE", component.Id, v.Name, Number(v.Value), v.Unit);
            }
            foreach (string issue in Assessment.Diagnostics) Line("DIAGNOSTIC", issue);
            foreach (string pending in PendingChecks) Line("PENDING", pending);
            return text.ToString();
            void Value(string name, double value, string unit) => Line("INPUT", name, Number(value), unit);
            void Line(params object[] values) => text.Append(string.Join("\t", values.Select(v => Escape(v?.ToString())))).Append('\n');
        }
        private static string Number(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
