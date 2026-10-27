using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum FilletWeldSteelGrade { S235 = 235, S275 = 275, S355 = 355, S420 = 420, S460 = 460 }
    public enum FilletWeldMethod { Directional, Simplified }
    public enum FilletWeldEndCondition { FullSize, NominalWithoutFullSizeEvidence, Defective }

    /// <summary>Carbon-steel parent material. Fu is the product/thickness-specific nominal ultimate strength in MPa.
    /// Grade is a supported EN steel strength grade; thickness is mm. This is not a consumable qualification.</summary>
    public sealed class FilletWeldMaterial
    {
        public FilletWeldSteelGrade Grade { get; }
        public double Fu { get; }
        public double Thickness { get; }
        public FilletWeldMaterial(FilletWeldSteelGrade grade, double fu, double thickness)
        {
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            ConnectionValidation.Positive(fu, nameof(fu)); ConnectionValidation.Positive(thickness, nameof(thickness));
            Grade = grade; Fu = fu; Thickness = thickness;
        }
    }

    /// <summary>Straight weld root in XY; mm, radians. The effective throat is supplied, not inferred from a leg.
    /// t = unit vector Start to End, p = (-t.Y,t.X,0), n = cos(angle)*p + sin(angle)*Z is the
    /// positive throat normal. Positive normal stress means tension. Reversing the root requires angle -> pi-angle
    /// to preserve n. FullSize includes starts and terminations. No penetration or packing allowance is implicit.</summary>
    public sealed class FilletWeldSegment
    {
        public ConnectionPoint2D Start { get; }
        public ConnectionPoint2D End { get; }
        public double Throat { get; }
        public FilletWeldEndCondition EndCondition { get; }
        public double ThroatNormalAngleRadians { get; }
        public double FusionAngleDegrees { get; }
        public double Length { get; }
        public FilletWeldSegment(ConnectionPoint2D start, ConnectionPoint2D end, double throat,
            FilletWeldEndCondition endCondition, double throatNormalAngleRadians = Math.PI / 4, double fusionAngleDegrees = 90)
        {
            Start = start; End = end;
            ConnectionValidation.Positive(throat, nameof(throat)); FilletWeldNumbers.Finite(throatNormalAngleRadians, nameof(throatNormalAngleRadians));
            ConnectionValidation.Positive(fusionAngleDegrees, nameof(fusionAngleDegrees));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), endCondition)) throw new ArgumentOutOfRangeException(nameof(endCondition));
            Length = FilletWeldNumbers.Norm(end.X - start.X, end.Y - start.Y, 0);
            ConnectionValidation.Positive(Length, nameof(end));
            Throat = throat; EndCondition = endCondition; ThroatNormalAngleRadians = throatNormalAngleRadians; FusionAngleDegrees = fusionAngleDegrees;
        }
    }

    /// <summary>Elastic line-weld group: identical elastic response per throat area, rigid load distributor,
    /// six actions applied at the supplied application point, lengths mm and forces N/Nmm.
    /// LocalEccentricitySource and ConsumableCompatibilitySource record caller evidence, not checks performed here.
    /// LapLength is the overall lap in the force-transfer direction (0 explicitly means not a lap joint).
    /// No intermittent stitch pattern, transverse stiffener, cold-formed-zone, packing, root-gap or penetration model.</summary>
    public sealed class FilletWeldGroupInput
    {
        public ConnectionDesignCode Code { get; }
        public IReadOnlyList<FilletWeldSegment> Segments { get; }
        public FilletWeldMaterial FirstMaterial { get; }
        public FilletWeldMaterial SecondMaterial { get; }
        public PlanarConnectionActions Actions { get; }
        public FilletWeldMethod Method { get; }
        public string ConsumableCompatibilitySource { get; }
        public bool LocalEccentricityAccounted { get; }
        public string LocalEccentricitySource { get; }
        public double LapLength { get; }
        public FilletWeldGroupInput(ConnectionDesignCode code, IEnumerable<FilletWeldSegment> segments,
            FilletWeldMaterial firstMaterial, FilletWeldMaterial secondMaterial, PlanarConnectionActions actions,
            FilletWeldMethod method, string consumableCompatibilitySource, bool localEccentricityAccounted = false,
            string localEccentricitySource = null, double lapLength = 0)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            var copy = segments.ToArray();
            if (copy.Length == 0 || copy.Length > 1000 || copy.Any(s => s == null)) throw new ArgumentException("One to 1000 non-null weld segments required.", nameof(segments));
            Segments = Array.AsReadOnly(copy);
            FirstMaterial = firstMaterial ?? throw new ArgumentNullException(nameof(firstMaterial));
            SecondMaterial = secondMaterial ?? throw new ArgumentNullException(nameof(secondMaterial));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (!Enum.IsDefined(typeof(FilletWeldMethod), method)) throw new ArgumentOutOfRangeException(nameof(method));
            ConnectionValidation.Nonnegative(lapLength, nameof(lapLength));
            Method = method; ConsumableCompatibilitySource = consumableCompatibilitySource;
            LocalEccentricityAccounted = localEccentricityAccounted; LocalEccentricitySource = localEccentricitySource; LapLength = lapLength;
        }
    }

    public sealed class FilletWeldStressResult
    {
        public double SigmaPerpendicular { get; }
        public double TauPerpendicular { get; }
        public double TauParallel { get; }
        public double Fu { get; }
        public double Beta { get; }
        public double LongJointFactor { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete { get; }
        public double? Utilization => IsComplete ? (double?)Checks.Max(c => c.Utilization) : null;
        internal FilletWeldStressResult(double sigma, double perpendicular, double parallel, double fu, double beta,
            double longJointFactor, IList<ConnectionCheck> checks, IList<string> diagnostics)
        {
            var retained = ConnectionOutcome.Retained(checks, diagnostics.Count);
            SigmaPerpendicular = sigma; TauPerpendicular = perpendicular; TauParallel = parallel; Fu = fu; Beta = beta;
            LongJointFactor = longJointFactor; Checks = Array.AsReadOnly(retained); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            IsComplete = ConnectionOutcome.IsComplete(retained, diagnostics.Count);
            Status = ConnectionOutcome.Status(retained, diagnostics.Count);
        }
    }

    public sealed class FilletWeldPointResult
    {
        public int SegmentIndex { get; }
        public bool AtStart { get; }
        public ConnectionPoint2D Position { get; }
        public double StressX { get; }
        public double StressY { get; }
        public double StressZ { get; }
        public double ForcePerLengthX { get; }
        public double ForcePerLengthY { get; }
        public double ForcePerLengthZ { get; }
        public FilletWeldStressResult Verification { get; }
        internal FilletWeldPointResult(int segmentIndex, bool atStart, ConnectionPoint2D position,
            double sx, double sy, double sz, double throat, FilletWeldStressResult verification)
        {
            SegmentIndex = segmentIndex; AtStart = atStart; Position = position; StressX = sx; StressY = sy; StressZ = sz;
            ForcePerLengthX = sx * throat; ForcePerLengthY = sy * throat; ForcePerLengthZ = sz * throat; Verification = verification;
        }
    }

    public sealed class FilletWeldGroupResult
    {
        public const string MethodId = "Steel.Connection.FilletWeldGroup.Elastic";
        public FilletWeldGroupInput Input { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete { get; }
        public IReadOnlyList<FilletWeldPointResult> Points { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public FilletWeldPointResult GoverningPoint => IsComplete ? Points.OrderByDescending(p => p.Verification.Utilization).First() : null;
        public double? Utilization => GoverningPoint?.Verification.Utilization;
        public string Scope => "Static ULS resistance of planar straight fillet-weld roots under an elastic line distribution. "
            + "Component only: no parent-part strength/stability, local root bending, joint rotation, fatigue, seismic ductility or execution certification.";
        internal FilletWeldGroupResult(FilletWeldGroupInput input, IList<FilletWeldPointResult> points,
            IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            Input = input; Points = Array.AsReadOnly(points.ToArray()); Trace = Array.AsReadOnly(trace.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            // A failed point is a known failure even beside diagnostics or unsupported points.
            IsComplete = diagnostics.Count == 0 && points.Count != 0 && points.All(p => p.Verification.IsComplete);
            Status = points.Any(p => p.Verification.Status == ConnectionStatus.Failed) ? ConnectionStatus.Failed
                : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        }
    }
    internal static class FilletWeldNumbers
    {
        internal static void Finite(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        internal static double Norm(double x, double y, double z)
        {
            double scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
            if (scale == 0) return 0;
            double result = scale * Math.Sqrt((x / scale) * (x / scale) + (y / scale) * (y / scale) + (z / scale) * (z / scale));
            Finite(result, "numeric magnitude"); return result;
        }
    }
}
