using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Connections
{
    public enum EndPlateAlphaMethod { SuppliedChartValues, SciP398Approximation }
    public enum EndPlateFlangeBoundaries { UpperOnly, BothFlanges }

    /// <summary>Physical plate and two-column bolt layout. Y is measured downwards
    /// from the upper beam flange exterior, in mm. Projections are actual plate
    /// extensions beyond the beam. Weld sizes are equal-leg fillet LEGS.</summary>
    public sealed class StandardEndPlateLayout
    {
        public ConnectionPlate Plate { get; }
        public ConnectionBolt Bolt { get; }
        public double Gauge { get; }
        public IReadOnlyList<double> RowY { get; }
        public double TopProjection { get; }
        public double BottomProjection { get; }
        public double WebWeldLeg { get; }
        public double UpperWeldLeg { get; }
        public double LowerWeldLeg { get; }
        public EndPlateAlphaMethod AlphaMethod { get; }
        public EndPlateFlangeBoundaries Boundaries { get; }
        public TStubChartAlpha UpperAlpha { get; }
        public TStubChartAlpha LowerAlpha { get; }
        public string Source { get; }
        public StandardEndPlateLayout(ConnectionPlate plate, ConnectionBolt bolt, double gauge,
            IEnumerable<double> rowY, double topProjection, double bottomProjection,
            double webWeldLeg, double upperWeldLeg, double lowerWeldLeg,
            EndPlateAlphaMethod alphaMethod, EndPlateFlangeBoundaries boundaries, string source,
            TStubChartAlpha upperAlpha = null, TStubChartAlpha lowerAlpha = null)
        {
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            if (rowY == null) throw new ArgumentNullException(nameof(rowY));
            var rows = rowY.ToArray();
            if (rows.Length < 1 || rows.Length > 20) throw new ArgumentException("One to twenty physical rows required.", nameof(rowY));
            foreach (double y in rows) ConnectionNumbers.Finite(y, nameof(rowY));
            foreach (double v in new[] { gauge, webWeldLeg, upperWeldLeg, lowerWeldLeg }) ConnectionValidation.Positive(v, nameof(v));
            ConnectionValidation.Nonnegative(topProjection, nameof(topProjection));
            ConnectionValidation.Nonnegative(bottomProjection, nameof(bottomProjection));
            if (!Enum.IsDefined(typeof(EndPlateAlphaMethod), alphaMethod) || !Enum.IsDefined(typeof(EndPlateFlangeBoundaries), boundaries))
                throw new ArgumentOutOfRangeException(nameof(alphaMethod));
            Gauge = gauge; RowY = Array.AsReadOnly(rows); TopProjection = topProjection; BottomProjection = bottomProjection;
            WebWeldLeg = webWeldLeg; UpperWeldLeg = upperWeldLeg; LowerWeldLeg = lowerWeldLeg;
            AlphaMethod = alphaMethod; Boundaries = boundaries; UpperAlpha = upperAlpha; LowerAlpha = lowerAlpha; Source = source;
        }
    }

    public sealed class StandardEndPlateColumn
    {
        public ConnectionRolledISection Section { get; }
        public double FlangeFy { get; }
        public double FlangeFu { get; }
        public double FirstEndContinuation { get; }
        public double LastEndContinuation { get; }
        public string MaterialSource { get; }
        public string RestraintSource { get; }
        public StandardEndPlateColumn(ConnectionRolledISection section, double flangeFy, double flangeFu,
            double firstEndContinuation, double lastEndContinuation, string materialSource, string restraintSource)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            foreach (double v in new[] { flangeFy, flangeFu, firstEndContinuation, lastEndContinuation }) ConnectionValidation.Positive(v, nameof(v));
            if (flangeFu < flangeFy) throw new ArgumentException("Flange fu must not be below fy.");
            FlangeFy = flangeFy; FlangeFu = flangeFu; FirstEndContinuation = firstEndContinuation; LastEndContinuation = lastEndContinuation;
            MaterialSource = materialSource; RestraintSource = restraintSource;
        }
    }

    /// <summary>Same hardware and elongation length in every physical two-bolt row.
    /// Length includes the actual grip/head/nut/washer contributions, not plate thickness alone.</summary>
    public sealed class StandardEndPlateFasteners
    {
        public EndPlateBoltHardware Hardware { get; }
        public double ElongationLength { get; }
        public string ElongationSource { get; }
        public string HoleServiceabilitySource { get; }
        public string UniformShearTransferSource { get; }
        public StandardEndPlateFasteners(EndPlateBoltHardware hardware, double elongationLength,
            string elongationSource, string holeServiceabilitySource, string uniformShearTransferSource)
        {
            Hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
            ConnectionValidation.Positive(elongationLength, nameof(elongationLength));
            ElongationLength = elongationLength; ElongationSource = elongationSource;
            HoleServiceabilitySource = holeServiceabilitySource; UniformShearTransferSource = uniformShearTransferSource;
        }
    }

    /// <summary>Exactly one weld topology: extended paired fillets, or upper-flush
    /// CJP plus physical web/lower paired fillets. Sources are execution evidence.</summary>
    public sealed class StandardEndPlateWelds
    {
        public FilletWeldSteelGrade BeamGrade { get; }
        public FilletWeldSteelGrade PlateGrade { get; }
        public EndPlateWeldDetail Detail { get; }
        public EndPlateCjpFlangeDetail TopCjp { get; }
        private StandardEndPlateWelds(FilletWeldSteelGrade beamGrade, FilletWeldSteelGrade plateGrade,
            EndPlateWeldDetail detail, EndPlateCjpFlangeDetail topCjp)
        {
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), beamGrade) || !Enum.IsDefined(typeof(FilletWeldSteelGrade), plateGrade))
                throw new ArgumentOutOfRangeException(nameof(beamGrade));
            BeamGrade = beamGrade; PlateGrade = plateGrade;
            Detail = detail ?? throw new ArgumentNullException(nameof(detail)); TopCjp = topCjp;
        }
        public static StandardEndPlateWelds PairedFillets(FilletWeldSteelGrade beamGrade, FilletWeldSteelGrade plateGrade, EndPlateWeldDetail detail)
            => new StandardEndPlateWelds(beamGrade, plateGrade, detail, null);
        public static StandardEndPlateWelds TopFlushCjp(FilletWeldSteelGrade beamGrade, FilletWeldSteelGrade plateGrade,
            EndPlateWeldDetail detail, EndPlateCjpFlangeDetail cjp)
            => new StandardEndPlateWelds(beamGrade, plateGrade, detail, cjp ?? throw new ArgumentNullException(nameof(cjp)));
    }

    public sealed class StandardEndPlateContact
    {
        public double UpperWidth { get; }
        public double UpperHeight { get; }
        public double LowerWidth { get; }
        public double LowerHeight { get; }
        public double InitialGap { get; }
        public string Source { get; }
        public StandardEndPlateContact(double upperWidth, double upperHeight, double lowerWidth, double lowerHeight,
            string source, double initialGap = 0)
        {
            foreach (double v in new[] { upperWidth, upperHeight, lowerWidth, lowerHeight }) ConnectionValidation.Positive(v, nameof(v));
            ConnectionValidation.Nonnegative(initialGap, nameof(initialGap));
            UpperWidth = upperWidth; UpperHeight = upperHeight; LowerWidth = lowerWidth; LowerHeight = lowerHeight;
            Source = source; InitialGap = initialGap;
        }
    }

    /// <summary>Application input: Beam.Actions contains the ACTUAL concomitant
    /// beam-face N-M1-V2. Column cuts use the continuous axes and centroid reference
    /// defined by EndPlateNmvInput. No zero-load preparation or scalar capacity is supplied.</summary>
    public sealed class StandardEndPlateInput
    {
        public ColumnWebPolicy Policy { get; }
        public EndPlateBeamSectionInput Beam { get; }
        public StandardEndPlateColumn Column { get; }
        public StandardEndPlateLayout Layout { get; }
        public StandardEndPlateFasteners Fasteners { get; }
        public StandardEndPlateWelds Welds { get; }
        public StandardEndPlateContact Contact { get; }
        public SteelActions ColumnUpper { get; }
        public SteelActions ColumnLower { get; }
        public string ColumnReferenceSource { get; }
        public bool ColumnPanelModelledSeparately { get; }
        public string ColumnPanelModelSource { get; }
        public EndPlateNmvTangent AppliedGlobalTangent { get; }
        public StandardEndPlateInput(ColumnWebPolicy policy, EndPlateBeamSectionInput beam, StandardEndPlateColumn column,
            StandardEndPlateLayout layout, StandardEndPlateFasteners fasteners, StandardEndPlateWelds welds,
            StandardEndPlateContact contact, SteelActions columnUpper, SteelActions columnLower, string columnReferenceSource,
            bool columnPanelModelledSeparately = false, string columnPanelModelSource = null, EndPlateNmvTangent appliedGlobalTangent = null)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Column = column ?? throw new ArgumentNullException(nameof(column)); Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Fasteners = fasteners ?? throw new ArgumentNullException(nameof(fasteners)); Welds = welds ?? throw new ArgumentNullException(nameof(welds));
            Contact = contact ?? throw new ArgumentNullException(nameof(contact)); ColumnUpper = columnUpper ?? throw new ArgumentNullException(nameof(columnUpper));
            ColumnLower = columnLower ?? throw new ArgumentNullException(nameof(columnLower)); ColumnReferenceSource = columnReferenceSource;
            ColumnPanelModelledSeparately = columnPanelModelledSeparately; ColumnPanelModelSource = columnPanelModelSource;
            if (appliedGlobalTangent != null && !appliedGlobalTangent.IsPositiveDefinite) throw new ArgumentException("Applied matrix must be positive definite.");
            AppliedGlobalTangent = appliedGlobalTangent;
        }
        private StandardEndPlatePreparation prepared;
        /// <summary>Action-independent preparation of this physical joint; shared by the cases of one PreparedStandardEndPlate.</summary>
        internal StandardEndPlatePreparation Prepared
        {
            get => prepared ?? System.Threading.LazyInitializer.EnsureInitialized(ref prepared, () => new StandardEndPlatePreparation(this));
            set => prepared = value;
        }
    }
}
