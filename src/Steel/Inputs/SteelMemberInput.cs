using System;

namespace GPC.Checkers.Steel.Inputs
{
    public enum SteelRestraintState { Unknown, Free, Fixed }

    /// <summary>Idealized end restraints in principal member axes 1/2 and longitudinal
    /// axis 3. Unknown is not free. Springs and intermediate restraints are not described
    /// by this contract; no effective length is inferred from these declarations.</summary>
    public sealed class SteelEndRestraints
    {
        public SteelRestraintState Translation1 { get; }
        public SteelRestraintState Translation2 { get; }
        public SteelRestraintState Translation3 { get; }
        public SteelRestraintState Rotation1 { get; }
        public SteelRestraintState Rotation2 { get; }
        public SteelRestraintState Rotation3 { get; }
        public SteelRestraintState Warping { get; }

        public SteelEndRestraints(SteelRestraintState translation1 = SteelRestraintState.Unknown,
            SteelRestraintState translation2 = SteelRestraintState.Unknown,
            SteelRestraintState translation3 = SteelRestraintState.Unknown,
            SteelRestraintState rotation1 = SteelRestraintState.Unknown,
            SteelRestraintState rotation2 = SteelRestraintState.Unknown,
            SteelRestraintState rotation3 = SteelRestraintState.Unknown,
            SteelRestraintState warping = SteelRestraintState.Unknown)
        {
            Validate(translation1, nameof(translation1)); Validate(translation2, nameof(translation2));
            Validate(translation3, nameof(translation3)); Validate(rotation1, nameof(rotation1));
            Validate(rotation2, nameof(rotation2)); Validate(rotation3, nameof(rotation3));
            Validate(warping, nameof(warping));
            Translation1 = translation1; Translation2 = translation2; Translation3 = translation3;
            Rotation1 = rotation1; Rotation2 = rotation2; Rotation3 = rotation3; Warping = warping;
        }

        private static void Validate(SteelRestraintState value, string name)
        {
            if (!Enum.IsDefined(typeof(SteelRestraintState), value))
                throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Explicit imperfection metadata, not an automatically applied load.
    /// Signed bow amplitudes in mm describe e_i(x)=e_i*sin(pi*x/L), along principal
    /// axes 1/2. Sway values are signed dimensionless slopes along those axes.
    /// Null means unknown; zero means explicitly zero. The analysis declaration
    /// distinguishes included imperfections from values still to be applied.</summary>
    public sealed class SteelMemberImperfections
    {
        public bool IncludedInAnalysis { get; }
        public string Reference { get; }
        public double? SinusoidalBow1 { get; }
        public double? SinusoidalBow2 { get; }
        public double? Sway1 { get; }
        public double? Sway2 { get; }

        public SteelMemberImperfections(bool includedInAnalysis, string reference,
            double? sinusoidalBow1 = null, double? sinusoidalBow2 = null,
            double? sway1 = null, double? sway2 = null)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("An imperfection source or analysis reference is required.", nameof(reference));
            Validate(sinusoidalBow1, nameof(sinusoidalBow1)); Validate(sinusoidalBow2, nameof(sinusoidalBow2));
            Validate(sway1, nameof(sway1)); Validate(sway2, nameof(sway2));
            IncludedInAnalysis = includedInAnalysis; Reference = reference;
            SinusoidalBow1 = sinusoidalBow1; SinusoidalBow2 = sinusoidalBow2; Sway1 = sway1; Sway2 = sway2;
        }

        private static void Validate(double? value, string name)
        {
            if (value.HasValue) SteelInputValidation.Finite(value.Value, name);
        }
    }

    /// <summary>Member data distinct from section geometry and actions at a cut.
    /// Lengths are mm. BucklingLength1/2 concern bending ABOUT principal axes 1/2;
    /// TorsionalBucklingLength is not an automatically inferred LTB length.
    /// Effective lengths are supplied analysis data, never deduced from end labels.
    /// Missing data remain missing. One cut's actions do not describe a member diagram.</summary>
    public sealed class SteelMemberInput
    {
        public double Length { get; }
        public double? BucklingLength1 { get; }
        public double? BucklingLength2 { get; }
        public double? TorsionalBucklingLength { get; }
        public SteelEndRestraints StartRestraints { get; }
        public SteelEndRestraints EndRestraints { get; }
        public SteelMemberImperfections Imperfections { get; }

        public SteelMemberInput(double length, double? bucklingLength1 = null,
            double? bucklingLength2 = null, double? torsionalBucklingLength = null,
            SteelEndRestraints startRestraints = null, SteelEndRestraints endRestraints = null,
            SteelMemberImperfections imperfections = null)
        {
            SteelInputValidation.Positive(length, nameof(length));
            SteelInputValidation.PositiveOptional(bucklingLength1, nameof(bucklingLength1));
            SteelInputValidation.PositiveOptional(bucklingLength2, nameof(bucklingLength2));
            SteelInputValidation.PositiveOptional(torsionalBucklingLength, nameof(torsionalBucklingLength));
            Length = length; BucklingLength1 = bucklingLength1; BucklingLength2 = bucklingLength2;
            TorsionalBucklingLength = torsionalBucklingLength;
            StartRestraints = startRestraints; EndRestraints = endRestraints; Imperfections = imperfections;
        }
    }
}
