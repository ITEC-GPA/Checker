using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Alpha read/qualified from EN1993-1-8 Figure6.11 for this physical geometry.
    /// This explicit value is not silently replaced by the distinct SCI AppendixG approximation.</summary>
    public sealed class TStubChartAlpha
    {
        public double Value { get; }
        public string Source { get; }
        public TStubChartAlpha(double value, string source)
        {
            ConnectionValidation.Positive(value, nameof(value));
            if (value < 4.45 || value > 8) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value; Source = source;
        }
    }

    /// <summary>Rectangular welded end plate with two symmetric bolt columns and internal
    /// tension rows only. Coordinates are distances from the INNER face of the tension flange
    /// towards the compression flange. ClearDepth is between both beam-flange inner faces.
    /// Weld dimensions are equal-leg fillet weld legs, not throats. No intervening stiffeners.
    /// External bolt rows are separate T-stubs, never included in this row set.</summary>
    public sealed class TStubInternalEndPlateGeometry
    {
        public double PlateWidth { get; }
        public double OpposingFlangeWidth { get; }
        public double BoltGauge { get; }
        public double BeamWebThickness { get; }
        public double WebWeldLeg { get; }
        public double TensionFlangeWeldLeg { get; }
        public double CompressionFlangeWeldLeg { get; }
        public double BeamClearDepth { get; }
        public IReadOnlyList<double> OrderedInternalRows { get; }
        public TStubChartAlpha FirstRowAlpha { get; }
        public string DetailSource { get; }
        public TStubInternalEndPlateGeometry(double plateWidth, double opposingFlangeWidth, double boltGauge,
            double beamWebThickness, double webWeldLeg, double tensionFlangeWeldLeg,
            double compressionFlangeWeldLeg, double beamClearDepth, IEnumerable<double> orderedInternalRows,
            TStubChartAlpha firstRowAlpha, string detailSource)
        {
            foreach (var value in new[] { plateWidth, opposingFlangeWidth, boltGauge, beamWebThickness, webWeldLeg,
                tensionFlangeWeldLeg, compressionFlangeWeldLeg, beamClearDepth }) ConnectionValidation.Positive(value, nameof(value));
            if (orderedInternalRows == null) throw new ArgumentNullException(nameof(orderedInternalRows));
            var rows = orderedInternalRows.ToArray();
            if (rows.Length < 1 || rows.Length > 20) throw new ArgumentOutOfRangeException(nameof(orderedInternalRows));
            foreach (var row in rows) ConnectionNumbers.Finite(row, nameof(orderedInternalRows));
            FirstRowAlpha = firstRowAlpha ?? throw new ArgumentNullException(nameof(firstRowAlpha));
            PlateWidth = plateWidth; OpposingFlangeWidth = opposingFlangeWidth; BoltGauge = boltGauge;
            BeamWebThickness = beamWebThickness; WebWeldLeg = webWeldLeg;
            TensionFlangeWeldLeg = tensionFlangeWeldLeg; CompressionFlangeWeldLeg = compressionFlangeWeldLeg;
            BeamClearDepth = beamClearDepth; OrderedInternalRows = Array.AsReadOnly(rows); DetailSource = detailSource;
        }
    }

    public static class TStubInternalEndPlateLengths
    {
        public static TStubDerivedGeometry Calculate(TStubInternalEndPlateGeometry p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var rows = new List<TStubEffectiveLengths>(); var groups = new List<TStubDerivedGroupLengths>();
            double m = (p.BoltGauge - p.BeamWebThickness) / 2 - .8 * p.WebWeldLeg;
            double e = (p.PlateWidth - p.BoltGauge) / 2;
            double emin = Math.Min(e, (p.OpposingFlangeWidth - p.BoltGauge) / 2);
            double m2 = p.OrderedInternalRows[0] - .8 * p.TensionFlangeWeldLeg;
            double clearWebWeld = (p.BoltGauge - p.BeamWebThickness) / 2 - p.WebWeldLeg;
            double clearTopWeld = p.OrderedInternalRows[0] - p.TensionFlangeWeldLeg;
            double clearBottomWeld = p.BeamClearDepth - p.OrderedInternalRows[p.OrderedInternalRows.Count - 1] - p.CompressionFlangeWeldLeg;
            double clearToObstruction = Math.Min(clearWebWeld, Math.Min(clearTopWeld, clearBottomWeld));
            double? minimumPitch = null;
            if (string.IsNullOrWhiteSpace(p.DetailSource)) issues.Add("InternalEndPlateDetailSourceRequired");
            if (string.IsNullOrWhiteSpace(p.FirstRowAlpha.Source)) issues.Add("FirstRowAlphaDerivationRequired");
            if (!(m > 0) || !(e > 0) || !(emin > 0) || !(m2 > 0) || !(clearToObstruction > 0))
                issues.Add("InternalRowsOutsideClearBeamAndPlateGeometry");
            for (int i = 1; i < p.OrderedInternalRows.Count; i++)
            {
                double pitch = p.OrderedInternalRows[i] - p.OrderedInternalRows[i - 1];
                if (!(pitch > 0) || !Finite(pitch)) issues.Add("InternalRowsMustIncreaseWithFinitePitch");
                else minimumPitch = minimumPitch.HasValue ? Math.Min(minimumPitch.Value, pitch) : pitch;
            }
            if (issues.Count != 0) return Result();
            double lambda1 = m / (m + e), lambda2 = m2 / (m + e);
            // First field remains inside the plotted EN Figure6.11 domain; no extrapolation.
            if (!(lambda1 > 0) || lambda1 > .9 || !(lambda2 > 0) || lambda2 > 1.4)
                issues.Add("AlphaPointOutsideFigure611QualifiedDomain");
            // The straight/asymptotic branch cannot be below the unstiffened non-circular mechanism.
            double minimumAlpha = 4 + 1.25 * e / m;
            if (p.FirstRowAlpha.Value + 1e-12 < minimumAlpha)
                issues.Add("AlphaBelowUnstiffenedAsymptoteForThisGeometry");
            double endMechanism = 2 * m + .625 * e;
            // No second flange/stiffener or physical end may cut the trailing end-row pattern.
            // Cases needing both flange boundaries require a different yield-line model.
            if (clearBottomWeld < endMechanism)
                issues.Add("CompressionFlangeTooCloseForUnrestrictedEndRowMechanism");
            double circularSingle = 2 * Math.PI * m, plainNonCircular = 4 * m + 1.25 * e;
            if (!Finite(circularSingle) || !Finite(plainNonCircular) || !Finite(p.FirstRowAlpha.Value * m))
                issues.Add("InternalLengthsOutsideNumericRange");
            Value("M", m); Value("M2", m2); Value("E", e); Value("Emin", emin);
            Value("Lambda1", lambda1, "1"); Value("Lambda2", lambda2, "1"); Value("Alpha", p.FirstRowAlpha.Value, "1");
            Value("MinimumAlphaAsymptote", minimumAlpha, "1"); Value("TrailingMechanismLength", endMechanism);
            Value("ClearDistanceToCompressionWeld", clearBottomWeld);
            if (issues.Count != 0) return Result();
            for (int i = 0; i < p.OrderedInternalRows.Count; i++)
                rows.Add(Lengths("InternalRow" + (i + 1), circularSingle,
                    i == 0 ? p.FirstRowAlpha.Value * m : plainNonCircular));
            for (int count = 2; count <= rows.Count; count++)
                for (int first = 0; first + count <= rows.Count; first++)
                {
                    double circular = 0, nonCircular = 0;
                    for (int index = first; index < first + count; index++)
                    {
                        bool groupEnd = index == first || index == first + count - 1;
                        double pitch = index == first ? p.OrderedInternalRows[index + 1] - p.OrderedInternalRows[index]
                            : index == first + count - 1 ? p.OrderedInternalRows[index] - p.OrderedInternalRows[index - 1]
                            : (p.OrderedInternalRows[index] - p.OrderedInternalRows[index - 1]) / 2
                              + (p.OrderedInternalRows[index + 1] - p.OrderedInternalRows[index]) / 2;
                        circular += groupEnd ? Math.PI * m + pitch : 2 * pitch;
                        nonCircular += index == 0 ? .5 * pitch + p.FirstRowAlpha.Value * m - endMechanism
                            : groupEnd ? endMechanism + .5 * pitch : pitch;
                    }
                    string id = "InternalRows" + (first + 1) + "To" + (first + count);
                    if (!Finite(circular) || !Finite(nonCircular) || !(nonCircular > 0))
                    { issues.Add(id + "OutsideNumericRange"); continue; }
                    groups.Add(new TStubDerivedGroupLengths(first, count, Lengths(id, circular, nonCircular)));
                }
            return Result();

            TStubEffectiveLengths Lengths(string name, double cp, double nc)
            {
                Value(name + ".CircularLength", cp); Value(name + ".NonCircularLength", nc);
                return new TStubEffectiveLengths(Math.Min(cp, nc), nc, "EN1993-1-8:2005 Table6.6, " + name
                    + "; first-row alpha: " + p.FirstRowAlpha.Source + "; " + p.DetailSource);
            }
            void Value(string name, double value, string unit = "mm")
            { if (!Finite(value)) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            TStubDerivedGeometry Result() => new TStubDerivedGeometry("Steel.Connection.TStub.InternalEndPlateGeometry",
                p.DetailSource, Safe(m), Safe(e), Safe(emin), p.BoltGauge, minimumPitch, Safe(clearToObstruction), rows, groups, issues, trace);
        }
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        private static double Safe(double value) => Finite(value) ? value : 0;
    }
}
