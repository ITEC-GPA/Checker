using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Two aligned bolt columns on an unstiffened rolled I/H column flange.
    /// Row coordinates run along the column. Continuations are actual distances from
    /// outermost rows to free column-flange ends, mm; both must be beyond the mechanisms.
    /// No intervening stiffeners, backing plates, notches or haunches are represented.</summary>
    internal sealed class TStubUnstiffenedColumnGeometry
    {
        public double ColumnFlangeWidth { get; }
        public double ColumnWebThickness { get; }
        public double ColumnRootRadius { get; }
        public double OpposingPlateWidth { get; }
        public double BoltGauge { get; }
        public IReadOnlyList<double> OrderedRowCoordinates { get; }
        public double FirstEndContinuation { get; }
        public double LastEndContinuation { get; }
        public string DetailSource { get; }
        public TStubUnstiffenedColumnGeometry(double columnFlangeWidth, double columnWebThickness,
            double columnRootRadius, double opposingPlateWidth, double boltGauge,
            IEnumerable<double> orderedRowCoordinates, double firstEndContinuation,
            double lastEndContinuation, string detailSource)
        {
            ConnectionValidation.Positive(columnFlangeWidth, nameof(columnFlangeWidth));
            ConnectionValidation.Positive(columnWebThickness, nameof(columnWebThickness));
            ConnectionValidation.Nonnegative(columnRootRadius, nameof(columnRootRadius));
            ConnectionValidation.Positive(opposingPlateWidth, nameof(opposingPlateWidth));
            ConnectionValidation.Positive(boltGauge, nameof(boltGauge));
            ConnectionValidation.Positive(firstEndContinuation, nameof(firstEndContinuation));
            ConnectionValidation.Positive(lastEndContinuation, nameof(lastEndContinuation));
            if (orderedRowCoordinates == null) throw new ArgumentNullException(nameof(orderedRowCoordinates));
            var rows = orderedRowCoordinates.ToArray();
            if (rows.Length < 1 || rows.Length > 20) throw new ArgumentOutOfRangeException(nameof(orderedRowCoordinates));
            foreach (double row in rows) ConnectionNumbers.Finite(row, nameof(orderedRowCoordinates));
            ColumnFlangeWidth = columnFlangeWidth; ColumnWebThickness = columnWebThickness;
            ColumnRootRadius = columnRootRadius; OpposingPlateWidth = opposingPlateWidth;
            BoltGauge = boltGauge; OrderedRowCoordinates = Array.AsReadOnly(rows);
            FirstEndContinuation = firstEndContinuation; LastEndContinuation = lastEndContinuation;
            DetailSource = detailSource;
        }
    }

    /// <summary>One bolt row outside the beam tension flange on a rectangular extended end plate.
    /// FlangeToRow is measured from the beam-flange exterior face; WeldLeg is the actual
    /// equal-leg fillet weld leg (not throat). Two bolts symmetric about the beam web; mm.
    /// No stiffener or second row exists in this extension.</summary>
    public sealed class TStubExtendedEndPlateGeometry
    {
        public double PlateWidth { get; }
        public double BoltGauge { get; }
        public double FlangeToRow { get; }
        public double FlangeWeldLeg { get; }
        public double EndDistance { get; }
        public string DetailSource { get; }
        public TStubExtendedEndPlateGeometry(double plateWidth, double boltGauge, double flangeToRow,
            double flangeWeldLeg, double endDistance, string detailSource)
        {
            ConnectionValidation.Positive(plateWidth, nameof(plateWidth));
            ConnectionValidation.Positive(boltGauge, nameof(boltGauge));
            ConnectionValidation.Positive(flangeToRow, nameof(flangeToRow));
            ConnectionValidation.Positive(flangeWeldLeg, nameof(flangeWeldLeg));
            ConnectionValidation.Positive(endDistance, nameof(endDistance));
            PlateWidth = plateWidth; BoltGauge = boltGauge; FlangeToRow = flangeToRow;
            FlangeWeldLeg = flangeWeldLeg; EndDistance = endDistance; DetailSource = detailSource;
        }
    }

    internal sealed class TStubDerivedGroupLengths
    {
        public int FirstRow { get; }
        public int RowCount { get; }
        public TStubEffectiveLengths EffectiveLengths { get; }
        internal TStubDerivedGroupLengths(int first, int count, TStubEffectiveLengths lengths)
        { FirstRow = first; RowCount = count; EffectiveLengths = lengths; }
    }

    /// <summary>Contribution of ONE physical row to one adjacent-row mechanism,
    /// before summation and before taking the circular/non-circular minimum.</summary>
    internal sealed class TStubRowGroupLength
    {
        public int FirstRow { get; }
        public int RowCount { get; }
        public int RowIndex { get; }
        public double CircularLength { get; }
        public double NonCircularLength { get; }
        public double Minimum => Math.Min(CircularLength, NonCircularLength);
        internal TStubRowGroupLength(int first,int count,int row,double circular,double nonCircular)
        { FirstRow=first;RowCount=count;RowIndex=row;CircularLength=circular;NonCircularLength=nonCircular; }
    }

    /// <summary>Geometric derivation only. IsSupported is not a resistance or joint qualification.
    /// Unsupported geometry exposes no lengths and cannot construct a verification input.</summary>
    internal sealed class TStubDerivedGeometry
    {
        public string MethodId { get; }
        public string DetailSource { get; }
        public bool IsSupported => Diagnostics.Count == 0;
        public double M { get; }
        public double E { get; }
        public double Emin { get; }
        public double BoltGauge { get; }
        public double? MinimumRowPitch { get; }
        public double ClearDistanceToRootOrWeld { get; }
        public IReadOnlyList<TStubEffectiveLengths> IndividualRows { get; }
        public IReadOnlyList<TStubDerivedGroupLengths> AdjacentGroups { get; }
        public IReadOnlyList<TStubRowGroupLength> RowGroupContributions { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        internal TStubDerivedGeometry(string methodId, string detail, double m, double e, double emin,
            double gauge, double? minimumPitch, double clearToObstruction,
            IEnumerable<TStubEffectiveLengths> rows, IEnumerable<TStubDerivedGroupLengths> groups,
            IEnumerable<string> diagnostics, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<TStubRowGroupLength> rowGroupContributions = null)
        {
            MethodId = methodId; DetailSource = detail; M = m; E = e; Emin = emin;
            BoltGauge = gauge; MinimumRowPitch = minimumPitch; ClearDistanceToRootOrWeld = clearToObstruction;
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            IndividualRows = Array.AsReadOnly(IsSupported ? rows.ToArray() : Array.Empty<TStubEffectiveLengths>());
            AdjacentGroups = Array.AsReadOnly(IsSupported ? groups.ToArray() : Array.Empty<TStubDerivedGroupLengths>());
            RowGroupContributions = Array.AsReadOnly(IsSupported
                ? (rowGroupContributions ?? Enumerable.Empty<TStubRowGroupLength>()).ToArray() : Array.Empty<TStubRowGroupLength>());
            Trace = Array.AsReadOnly(trace.ToArray());
        }

        /// <summary>EN Table6.11 minimum for THIS row: individual and each relevant
        /// group contribution. Never a group total divided by the number of rows.</summary>
        public JointStiffnessEffectiveLength StiffnessLength(int rowIndex)
        {
            if(!IsSupported)throw new InvalidOperationException("Unsupported geometry cannot supply stiffness lengths.");
            if(rowIndex<0||rowIndex>=IndividualRows.Count)throw new ArgumentOutOfRangeException(nameof(rowIndex));
            var contributions=RowGroupContributions.Where(r=>r.RowIndex==rowIndex).ToArray();
            int groups=AdjacentGroups.Count(g=>g.FirstRow<=rowIndex&&rowIndex<g.FirstRow+g.RowCount);
            if(contributions.Length!=groups)throw new InvalidOperationException("Every relevant group must retain its actual row contribution.");
            return new JointStiffnessEffectiveLength(new[]{IndividualRows[rowIndex].Mode1}.Concat(contributions.Select(c=>c.Minimum)),
                MethodId+"; EN1993-1-8:2005 Table6.11, physical row"+(rowIndex+1)+"; individual and all adjacent-group row contributions; "+DetailSource);
        }

        public TStubBoltRowsInput CreateInput(ConnectionDesignCode code, ConnectionBolt bolt,
            double flangeThickness, double flangeYieldStrength, IEnumerable<double> orderedRowTensions,
            TStubMode1Method method = TStubMode1Method.PointForce, double contactWidth = 0,
            TStubPryingModel pryingModel = TStubPryingModel.Develops,
            TStubBoltElongation boltElongation = null, string holeClearanceServiceabilitySource = null)
        {
            if (!IsSupported) throw new InvalidOperationException("Unsupported T-stub geometry cannot construct a verification input.");
            if (bolt == null) throw new ArgumentNullException(nameof(bolt));
            // Conservative minimum pitch in both in-plane directions; full detailing,
            // maximum pitch, contact pressure and punching remain parent-joint checks.
            if (BoltGauge < 2.4 * bolt.HoleDiameter || MinimumRowPitch.HasValue && ConnectionValidation.BelowMinimumWithRoundoff(MinimumRowPitch.Value, 2.4 * bolt.HoleDiameter)
                || E < 1.2 * bolt.HoleDiameter || Emin < 1.2 * bolt.HoleDiameter
                || ClearDistanceToRootOrWeld <= bolt.HoleDiameter / 2
                || method == TStubMode1Method.DistributedContact && (ClearDistanceToRootOrWeld <= contactWidth / 2
                    || E < contactWidth / 2 || BoltGauge < contactWidth || MinimumRowPitch.HasValue && ConnectionValidation.BelowMinimumWithRoundoff(MinimumRowPitch.Value, contactWidth)))
                throw new ArgumentException("The selected bolt hole/contact footprint does not fit the derived T-stub geometry.");
            if (orderedRowTensions == null) throw new ArgumentNullException(nameof(orderedRowTensions));
            var forces = orderedRowTensions.ToArray();
            if (forces.Length != IndividualRows.Count) throw new ArgumentException("One concomitant force is required for each ordered physical row.");
            foreach (double force in forces) ConnectionValidation.Nonnegative(force, nameof(orderedRowTensions));
            TStubTensionInput Component(int rows, TStubEffectiveLengths lengths, double force) =>
                new TStubTensionInput(code, bolt, flangeThickness, flangeYieldStrength, M, Emin, rows, lengths,
                    force, method, contactWidth, pryingModel, boltElongation, holeClearanceServiceabilitySource);
            var rowInputs = IndividualRows.Select((lengths, i) => Component(1, lengths, forces[i])).ToArray();
            var groupInputs = AdjacentGroups.Select(g => new TStubAdjacentRows(g.FirstRow, g.RowCount,
                Component(g.RowCount, g.EffectiveLengths, forces.Skip(g.FirstRow).Take(g.RowCount).Sum()))).ToArray();
            return new TStubBoltRowsInput(rowInputs, groupInputs, MethodId + ": " + DetailSource);
        }
    }

    internal static class TStubEffectiveGeometry
    {
        public static TStubDerivedGeometry UnstiffenedColumnFlange(TStubUnstiffenedColumnGeometry p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var rows = new List<TStubEffectiveLengths>(); var groups = new List<TStubDerivedGroupLengths>();
            var rowLengths = new List<TStubRowGroupLength>();
            double m = (p.BoltGauge - p.ColumnWebThickness) / 2 - .8 * p.ColumnRootRadius;
            double e = (p.ColumnFlangeWidth - p.BoltGauge) / 2;
            double emin = Math.Min(e, (p.OpposingPlateWidth - p.BoltGauge) / 2);
            double? minimumPitch = null;
            if (string.IsNullOrWhiteSpace(p.DetailSource)) issues.Add("ColumnFlangeDetailSourceRequired");
            if (!(m > 0) || !(e > 0) || !(emin > 0)
                || p.BoltGauge <= p.ColumnWebThickness + 2 * p.ColumnRootRadius)
                issues.Add("ColumnFlangeBoltColumnsOutsideClearFlange");
            for (int i = 1; i < p.OrderedRowCoordinates.Count; i++)
            {
                double pitch = p.OrderedRowCoordinates[i] - p.OrderedRowCoordinates[i - 1];
                if (!(pitch > 0) || !Finite(pitch)) issues.Add("RowCoordinatesMustIncreaseWithFinitePitch");
                else minimumPitch = minimumPitch.HasValue ? Math.Min(minimumPitch.Value, pitch) : pitch;
            }
            if (issues.Count != 0) return Result();
            // Table6.4 end-row terms cannot reduce either circular/non-circular mechanism
            // when e1 >= max(pi*m/2, 2*m+.625*e). This check is made at both physical ends.
            double continuation = Math.Max(Math.PI * m / 2, 2 * m + .625 * e);
            if (p.FirstEndContinuation < continuation || p.LastEndContinuation < continuation)
                issues.Add("NearbyFreeColumnEndRequiresSeparateEffectiveLengthModel");
            double singleCp = 2 * Math.PI * m, singleNc = 4 * m + 1.25 * e;
            if (!Finite(singleCp) || !Finite(singleNc)) issues.Add("IndividualLengthOutsideNumericRange");
            Value("M", m, "mm"); Value("E", e, "mm"); Value("Emin", emin, "mm");
            Value("RequiredEndContinuation", continuation, "mm");
            Value("FirstEndContinuation", p.FirstEndContinuation, "mm");
            Value("LastEndContinuation", p.LastEndContinuation, "mm");
            if (issues.Count != 0) return Result();
            for (int i = 0; i < p.OrderedRowCoordinates.Count; i++)
                rows.Add(Lengths("Row" + (i + 1), singleCp, singleNc));
            for (int count = 2; count <= rows.Count; count++)
                for (int first = 0; first + count <= rows.Count; first++)
                {
                    // Summation of Table6.4 contributions: end rows use adjacent pitch,
                    // inner rows use the mean of their two adjacent pitches. Sum first,
                    // then take min(sum cp,sum nc); do not sum per-row minima.
                    double circular = 0, nonCircular = 0;
                    for (int index = first; index < first + count; index++)
                    {
                        bool end = index == first || index == first + count - 1;
                        double pitch = index == first ? p.OrderedRowCoordinates[index + 1] - p.OrderedRowCoordinates[index]
                            : index == first + count - 1 ? p.OrderedRowCoordinates[index] - p.OrderedRowCoordinates[index - 1]
                            : (p.OrderedRowCoordinates[index] - p.OrderedRowCoordinates[index - 1]) / 2
                              + (p.OrderedRowCoordinates[index + 1] - p.OrderedRowCoordinates[index]) / 2;
                        double cp = end ? Math.PI * m + pitch : 2 * pitch;
                        double nc = end ? 2 * m + .625 * e + .5 * pitch : pitch;
                        circular += cp; nonCircular += nc;
                        rowLengths.Add(new TStubRowGroupLength(first,count,index,cp,nc));
                    }
                    string id = "Rows" + (first + 1) + "To" + (first + count);
                    if (!Finite(circular) || !Finite(nonCircular)) { issues.Add(id + "OutsideNumericRange"); continue; }
                    groups.Add(new TStubDerivedGroupLengths(first, count, Lengths(id, circular, nonCircular)));
                }
            return Result();

            TStubEffectiveLengths Lengths(string name, double circular, double nonCircular)
            {
                Value(name + ".CircularLength", circular, "mm"); Value(name + ".NonCircularLength", nonCircular, "mm");
                return new TStubEffectiveLengths(Math.Min(circular, nonCircular), nonCircular,
                    "EN1993-1-8:2005 Table6.4/Figure6.8, " + name + "; " + p.DetailSource);
            }
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            TStubDerivedGeometry Result() => new TStubDerivedGeometry("Steel.Connection.TStub.UnstiffenedColumnFlangeGeometry",
                p.DetailSource, Safe(m), Safe(e), Safe(emin), p.BoltGauge, minimumPitch,
                Safe((p.BoltGauge - p.ColumnWebThickness) / 2 - p.ColumnRootRadius), rows, groups, issues, trace, rowLengths);
        }

        public static TStubDerivedGeometry ExtendedEndPlateOuterRow(TStubExtendedEndPlateGeometry p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var rows = new List<TStubEffectiveLengths>();
            double m = p.FlangeToRow - .8 * p.FlangeWeldLeg;
            double e = (p.PlateWidth - p.BoltGauge) / 2, ex = p.EndDistance;
            if (string.IsNullOrWhiteSpace(p.DetailSource)) issues.Add("ExtendedEndPlateDetailSourceRequired");
            if (!(e > 0) || !(m > 0) || p.FlangeToRow <= p.FlangeWeldLeg)
                issues.Add("OuterBoltRowOutsideClearPlateExtension");
            if (issues.Count != 0) return Result();
            double cp1 = 2 * Math.PI * m, cp2 = Math.PI * m + p.BoltGauge, cp3 = Math.PI * m + 2 * e;
            double nc1 = 4 * m + 1.25 * ex, nc2 = e + 2 * m + .625 * ex;
            double nc3 = .5 * p.PlateWidth, nc4 = .5 * p.BoltGauge + 2 * m + .625 * ex;
            var values = new[] { m, e, ex, cp1, cp2, cp3, nc1, nc2, nc3, nc4 };
            var names = new[] { "Mx", "E", "Ex", "Circular.2PiMx", "Circular.PiMxPlusGauge", "Circular.PiMxPlus2E",
                "NonCircular.4MxPlus1Point25Ex", "NonCircular.EPlus2MxPlusPoint625Ex", "NonCircular.HalfWidth", "NonCircular.HalfGaugePlus2MxPlusPoint625Ex" };
            for (int i = 0; i < values.Length; i++)
                if (!Finite(values[i])) issues.Add(names[i] + "OutsideNumericRange");
                else trace.Add(new SectionResistanceValue(names[i], values[i], "mm"));
            if (issues.Count == 0)
            {
                double cp = Math.Min(cp1, Math.Min(cp2, cp3));
                double nc = Math.Min(Math.Min(nc1, nc2), Math.Min(nc3, nc4));
                rows.Add(new TStubEffectiveLengths(Math.Min(cp, nc), nc,
                    "EN1993-1-8:2005 Table6.6/Figure6.10 outer row, separate from rows inside beam flanges; " + p.DetailSource));
            }
            return Result();

            TStubDerivedGeometry Result() => new TStubDerivedGeometry("Steel.Connection.TStub.ExtendedEndPlateOuterRowGeometry",
                p.DetailSource, Safe(m), Safe(e), ex, p.BoltGauge, null, Safe(p.FlangeToRow - p.FlangeWeldLeg),
                rows, Array.Empty<TStubDerivedGroupLengths>(), issues, trace);
        }

        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        private static double Safe(double value) => Finite(value) ? value : 0;
    }
}
