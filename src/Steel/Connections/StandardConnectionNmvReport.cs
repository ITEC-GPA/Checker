using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Small, acyclic report snapshot. Stiffness diagnostics never replace
    /// the resistance verdict. An exported matrix remains local to the beam face.</summary>
    public sealed class EndPlateNmvStiffnessReport
    {
        public bool HasQualifiedLocalTangent { get; }
        public bool CanUseAsGlobalConnectionSpring { get; }
        public bool? GlobalMatrixMatches { get; }
        public bool RequiresGlobalModelReview { get; }
        public EndPlateNmvTangent LocalTangent { get; }
        public IReadOnlyList<string> LocalDiagnostics { get; }
        public IReadOnlyList<string> GlobalDiagnostics { get; }
        public string Scope { get; }
        internal EndPlateNmvStiffnessReport(EndPlateNmvStiffnessResult s)
        {
            HasQualifiedLocalTangent = s.HasQualifiedLocalTangent;
            CanUseAsGlobalConnectionSpring = s.CanUseAsGlobalConnectionSpring;
            GlobalMatrixMatches = s.GlobalMatrixMatches; RequiresGlobalModelReview = s.RequiresGlobalModelReview;
            LocalTangent = s.LocalTangent; LocalDiagnostics = s.LocalDiagnostics;
            GlobalDiagnostics = s.GlobalDiagnostics; Scope = s.Scope;
        }
    }

    public sealed partial class StandardConnectionCaseReport
    {
        /// <summary>Present only for the supplementary N-M-V route.</summary>
        public EndPlateNmvStiffnessReport NmvStiffness { get; private set; }

        public static StandardConnectionCaseReport Create(EndPlateNmvResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var p = r.Input; var g = p.Geometry; var m = g.Baseline.Input;
            var input = Factors(m.Policy.Code); var values = new List<SectionResistanceValue>(r.Trace);
            Actions("BeamFace", p.BeamFaceActions); Actions("Column.Upper", p.ColumnUpper); Actions("Column.Lower", p.ColumnLower);
            Section("Beam", m.Beam); Section("Column", m.Column);
            input.AddRange(new[] { V("Column.FlangeFy", m.ColumnFlangeFy, "MPa"),
                V("Plate.Width", m.EndPlate.Width, "mm"), V("Plate.Thickness", m.EndPlate.Thickness, "mm"),
                V("Plate.Fy", m.EndPlate.Fy, "MPa"), V("Plate.Fu", m.EndPlate.Fu, "MPa"),
                V("Bolt.Diameter", m.Bolt.Diameter, "mm"), V("Bolt.HoleDiameter", m.Bolt.HoleDiameter, "mm"),
                V("Contact.InitialGap", g.InitialContactGap, "mm") });
            for (int i = 0; i < g.Rows.Count; i++)
            {
                var row = g.Rows[i];
                input.Add(V(row.Id + ".Z", row.Z, "mm")); input.Add(V(row.Id + ".Stiffness", row.Stiffness, "N/mm"));
            }
            for (int i = 0; i < g.Contacts.Count; i++)
            {
                var c = g.Contacts[i];
                input.AddRange(new[] { V(c.Id + ".LowerZ", c.LowerZ, "mm"), V(c.Id + ".UpperZ", c.UpperZ, "mm"),
                    V(c.Id + ".Width", g.ContactWidths[i], "mm"), V(c.Id + ".Stiffness", c.Stiffness, "N/mm"),
                    V(c.Id + ".ColumnEffectiveWidth", g.CompressionWidths[i].Width, "mm") });
            }
            Add("ConditionalComponent.NtRd", r.AxialTensionResistance, "N"); Add("ConditionalComponent.NcRd", r.AxialCompressionResistance, "N");
            Add("ConditionalComponent.MpositiveRd", r.PositiveMomentResistance, "Nmm"); Add("ConditionalComponent.MnegativeRd", r.NegativeMomentResistance, "Nmm");
            Add("Components.Utilization", r.ComponentUtilization, "1");
            if (r.Equilibrium != null)
            {
                foreach (var row in r.Equilibrium.Rows)
                {
                    Add(row.Row.Id + ".Tension", row.Tension, "N"); Add(row.Row.Id + ".Opening", row.Opening, "mm");
                }
                foreach (var c in r.Equilibrium.Contacts)
                {
                    string id = c.Band.Id;
                    Add(id + ".Compression", c.Compression, "N"); Add(id + ".Moment", c.SignedMoment, "Nmm");
                    Add(id + ".ClosedLength", c.ClosedLength, "mm"); Add(id + ".ClosedLowerZ", c.ClosedLowerZ, "mm");
                    Add(id + ".ClosedUpperZ", c.ClosedUpperZ, "mm"); Add(id + ".ResultantZ", c.ResultantZ, "mm");
                    Add(id + ".PeakLineCompression", c.MaximumCompressionPerLength, "N/mm");
                }
            }
            var stiffness = EndPlateNmvStiffness.Evaluate(r);
            if (stiffness.HasQualifiedLocalTangent)
            {
                var k = stiffness.LocalTangent;
                Add("LocalTangent.KNN", k.Axial, "N/mm"); Add("LocalTangent.KNM", k.Coupling, "N");
                Add("LocalTangent.KMM", k.Rotation, "Nmm/rad");
                Add("LocalTangent.RotationAtConstantN", k.RotationAtConstantAxialForce, "Nmm/rad");
            }
            if (p.AppliedGlobalTangent != null)
                input.AddRange(new[] { V("Global.AppliedKNN", p.AppliedGlobalTangent.Axial, "N/mm"),
                    V("Global.AppliedKNM", p.AppliedGlobalTangent.Coupling, "N"), V("Global.AppliedKMM", p.AppliedGlobalTangent.Rotation, "Nmm/rad") });
            var sources = new List<string> { p.CombinationSource, p.ColumnReferenceSource, g.ContactSource,
                "EN1993-1-8:2005 6.2.7.1(3), Eq6.24; component resistances and conservative supplementary elastic N-M equilibrium",
                "END_PLATE_NMV_USAGE.txt; analytic/numerical independent oracles, no whole-joint experimental validation" };
            if (!string.IsNullOrWhiteSpace(p.ColumnPanelModelSource)) sources.Add(p.ColumnPanelModelSource);
            if (g.InitialComponents != null)
                sources.AddRange(g.InitialComponents.Input.Rows.Select(x => x.HardwareSource));
            return new StandardConnectionCaseReport("Steel.Connection.EndPlate.StandardNMV", m.Policy.Code,
                "Same-case standard local I/H end plate N-M-V; initially closed contact and elastic compatible rows. Conditional pure component bounds; no member stability or rotation capacity.",
                r.Status, r.IsComplete, input, values, r.Checks, r.Diagnostics, r.ExternalConditions, sources)
                { NmvStiffness = new EndPlateNmvStiffnessReport(stiffness) };

            void Add(string id, double? value, string unit) { if (value.HasValue) values.Add(V(id, value.Value, unit)); }
            void Actions(string id, GPC.Checkers.Steel.Inputs.SteelActions a)
            {
                input.AddRange(new[] { V(id + ".N", a.N, "N"), V(id + ".V1", a.V1, "N"), V(id + ".V2", a.V2, "N"),
                    V(id + ".M1", a.M1, "Nmm"), V(id + ".M2", a.M2, "Nmm"), V(id + ".T", a.T, "Nmm") });
            }
            void Section(string id, ConnectionRolledISection s)
            {
                input.AddRange(new[] { V(id + ".Height", s.Height, "mm"), V(id + ".Width", s.Width, "mm"),
                    V(id + ".WebThickness", s.WebThickness, "mm"), V(id + ".FlangeThickness", s.FlangeThickness, "mm"),
                    V(id + ".RootRadius", s.RootRadius, "mm"), V(id + ".Fy", s.Fy, "MPa"), V(id + ".Fu", s.Fu, "MPa") });
            }
        }
    }
}
