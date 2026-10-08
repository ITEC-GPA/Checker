using System;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>
    /// Codes of the refusals of the crack check (0.0.18.0). The exception stays an <see cref="ArgumentException"/> of the exact type with the message of
    /// 0.0.17.0; the code is in <see cref="Exception.Data"/> under <see cref="DataKey"/> (a string), so a caller can tell refusals with the same
    /// message apart (<see cref="WidthParameters"/> and <see cref="UpperBoundParameters"/>) without reading the text. The parameter refusals
    /// (<see cref="ArgumentOutOfRangeException"/> of designLimit, nominalCover, coverOverride, spacingOverride, effectiveDepthCover) carry no code:
    /// their <see cref="ArgumentException.ParamName"/> identifies them.
    /// </summary>
    public static class CrackRejection
    {
        /// <summary>Key of the code in <see cref="Exception.Data"/>.</summary>
        public const string DataKey = "GPC.Checkers.Concrete.Cracking.CrackRejection";

        /// <summary>Invalid parameters of the crack-width formula (<see cref="CrackWidthCalculator.Width"/>).</summary>
        public const string WidthParameters = "WidthParameters";
        /// <summary>Invalid parameters of the upper bound without bonded bars (<see cref="CrackWidthCalculator.UnbondedUpperBound"/>), same message as <see cref="WidthParameters"/>.</summary>
        public const string UpperBoundParameters = "UpperBoundParameters";
        /// <summary>Model Code 2010 and DIN crack models with plain bars (formula and upper bound).</summary>
        public const string RibbedBarsRequired = "RibbedBarsRequired";
        /// <summary>Bar stresses missing or not finite (<see cref="CrackWidthCalculator.K2"/>, and the check at use of <see cref="SectionCrackOptions.ValidateAtUse"/>).</summary>
        public const string BarStresses = "BarStresses";
        /// <summary>Decompression or crack formation without the stress of the uncracked section (<see cref="SectionCrackInput.UncrackedMaximumConcreteStress"/>).</summary>
        public const string UncrackedStressRequired = "UncrackedStressRequired";

        /// <summary>Code of a refusal of the crack check; null when the exception carries none.</summary>
        public static string CodeOf(Exception exception) => exception?.Data[DataKey] as string;

        internal static ArgumentException Create(string code, string message)
        {
            var exception = new ArgumentException(message);
            exception.Data[DataKey] = code;
            return exception;
        }
    }
}
