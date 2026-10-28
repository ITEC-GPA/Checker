using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One contiguous subgroup in the ordered bolt-row set. Indices are zero based.
    /// The equivalent T-stub lengths must be derived for this subgroup, not copied/summed
    /// from the individually acting rows. Its tension is the sum of their concomitant forces.</summary>
    internal sealed class TStubAdjacentRows
    {
        public int FirstRow { get; }
        public int RowCount { get; }
        public TStubTensionInput Component { get; }
        public TStubAdjacentRows(int firstRow, int rowCount, TStubTensionInput component)
        {
            if (firstRow < 0 || firstRow > 19) throw new ArgumentOutOfRangeException(nameof(firstRow));
            if (rowCount < 2 || rowCount > 20) throw new ArgumentOutOfRangeException(nameof(rowCount));
            FirstRow = firstRow; RowCount = rowCount;
            Component = component ?? throw new ArgumentNullException(nameof(component));
        }
    }

    /// <summary>One unbacked flange's single-row and all adjacent-subgroup mechanisms.
    /// OrderedRows follow physical order in one uninterrupted tension zone. Split at a
    /// stiffener/other boundary according to the applicable EN1993-1-8 component model.
    /// All forces are one externally established concomitant row-force distribution.</summary>
    internal sealed class TStubBoltRowsInput
    {
        public IReadOnlyList<TStubTensionInput> OrderedRows { get; }
        public IReadOnlyList<TStubAdjacentRows> AdjacentGroups { get; }
        public string RowLayoutSource { get; }
        public TStubBoltRowsInput(IEnumerable<TStubTensionInput> orderedRows,
            IEnumerable<TStubAdjacentRows> adjacentGroups, string rowLayoutSource)
        {
            if (orderedRows == null) throw new ArgumentNullException(nameof(orderedRows));
            if (adjacentGroups == null) throw new ArgumentNullException(nameof(adjacentGroups));
            var rows = orderedRows.ToArray(); var groups = adjacentGroups.ToArray();
            if (rows.Length < 1 || rows.Length > 20) throw new ArgumentOutOfRangeException(nameof(orderedRows));
            if (rows.Any(r => r == null) || groups.Any(g => g == null)) throw new ArgumentException("Null row/group components are not allowed.");
            OrderedRows = Array.AsReadOnly(rows); AdjacentGroups = Array.AsReadOnly(groups); RowLayoutSource = rowLayoutSource;
        }
    }

    internal static class TStubBoltRows
    {
        public static ConnectionVerification<TStubBoltRowsInput> Verify(TStubBoltRowsInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>(); var first = p.OrderedRows[0];
            if (string.IsNullOrWhiteSpace(p.RowLayoutSource)) issues.Add("OrderedRowLayoutDerivationRequired");
            for (int row = 0; row < p.OrderedRows.Count; row++)
            {
                if (p.OrderedRows[row].BoltRows != 1) issues.Add("Row" + (row + 1) + ":RequiresSingleRowComponent");
                SameFlange("Row" + (row + 1), p.OrderedRows[row]);
            }
            var present = new HashSet<string>();
            foreach (var g in p.AdjacentGroups)
            {
                string key = g.FirstRow + ":" + g.RowCount;
                if (!present.Add(key)) issues.Add("DuplicateAdjacentGroup:" + key);
                if (g.FirstRow + g.RowCount > p.OrderedRows.Count)
                { issues.Add("AdjacentGroupOutsideRowSet:" + key); continue; }
                if (g.Component.BoltRows != g.RowCount) issues.Add("AdjacentGroupBoltRowCountMismatch:" + key);
                SameFlange("Group" + key, g.Component);
                double sum = p.OrderedRows.Skip(g.FirstRow).Take(g.RowCount).Sum(r => r.TensionForce);
                if (double.IsInfinity(sum) || Math.Abs(sum - g.Component.TensionForce) > 1e-10 * Math.Max(sum, g.Component.TensionForce))
                    issues.Add("AdjacentGroupTensionMismatch:" + key);
            }
            for (int count = 2; count <= p.OrderedRows.Count; count++)
                for (int start = 0; start + count <= p.OrderedRows.Count; start++)
                    if (!present.Contains(start + ":" + count)) issues.Add("MissingAdjacentGroup:" + start + ":" + count);
            if (issues.Count == 0)
            {
                for (int i = 0; i < p.OrderedRows.Count; i++) Include("Row" + (i + 1), p.OrderedRows[i]);
                foreach (var g in p.AdjacentGroups.OrderBy(g => g.RowCount).ThenBy(g => g.FirstRow))
                    Include("Rows" + (g.FirstRow + 1) + "To" + (g.FirstRow + g.RowCount), g.Component);
            }
            return new ConnectionVerification<TStubBoltRowsInput>(p, first.Code, "Steel.Connection.TStub.AdjacentRows",
                "One equivalent flange under a prescribed row-force distribution: individual and every adjacent-group mechanism, EN1993-1-8 6.2.4.2. "
                + "No force allocation, connection moment resistance, stiffness, punching or whole-joint qualification.", checks, trace, issues,
                new[] { "Physical row order and the uninterrupted tension-zone definition: " + p.RowLayoutSource,
                    "Every single row and every contiguous subgroup is checked. Individual capacities are not added to obtain a joint capacity.",
                    "Each subgroup requires its own effective-length derivation for that mechanism. Geometric derivation is outside this kernel.",
                    "The same immutable code, bolt, flange geometry/material and contact model are used throughout this single flange zone.",
                    "The supplied row forces are externally established and concomitant; satisfying these inequalities does not prove equilibrium or admissible redistribution of the complete joint." });

            void SameFlange(string id, TStubTensionInput component)
            {
                if (!ReferenceEquals(first.Code, component.Code) || !ReferenceEquals(first.Bolt, component.Bolt)
                    || first.FlangeThickness != component.FlangeThickness || first.FlangeYieldStrength != component.FlangeYieldStrength
                    || first.M != component.M || first.Emin != component.Emin || first.Method != component.Method
                    || first.ContactWidth != component.ContactWidth || first.PryingModel != component.PryingModel
                    || !ReferenceEquals(first.BoltElongation, component.BoltElongation)
                    || first.HoleClearanceServiceabilitySource != component.HoleClearanceServiceabilitySource)
                    issues.Add(id + ":InconsistentFlangeSnapshot");
            }
            void Include(string id, TStubTensionInput component)
            {
                var result = TStubTension.Verify(component);
                issues.AddRange(result.Diagnostics.Select(d => id + ":" + d));
                foreach (var c in result.Checks)
                    checks.Add(new ConnectionCheck(id + "." + c.Id, c.Demand, c.Resistance,
                        c.Reference + "; EN1993-1-8:2005 6.2.4.2(3)", c.Unit, c.Role));
                foreach (var value in result.Trace) trace.Add(new SectionResistanceValue(id + "." + value.Name, value.Value, value.Unit));
            }
        }
    }
}
