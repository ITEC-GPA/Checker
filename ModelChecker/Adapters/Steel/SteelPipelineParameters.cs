using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Checker.Configuration;

namespace GPC.Model.Checker
{
    /// <summary>Closed vocabulary stored in the existing EngineDefinition.Parameters map.
    /// Section-specific declarations bind to the complete Model property fingerprint.</summary>
    internal sealed class SteelPipelineParameters
    {
        internal readonly Dictionary<string, string> Values;
        internal string HWebResistance { get; }
        internal string HWebSupportSource { get; }
        internal bool? Unperforated { get; }
        internal double? NetArea { get; }
        internal int? UniformCompressionClass { get; }
        internal string ClassificationSource { get; }
        internal string SectionFingerprint { get; }
        internal bool? ShearExcluded1 { get; }
        internal bool? ShearExcluded2 { get; }
        internal string ShearSource1 { get; }
        internal string ShearSource2 { get; }
        internal SteelNegligibleActionPolicy NegligibleActions { get; }

        internal SteelPipelineParameters(IDictionary<string, string> parameters)
        {
            Values = parameters == null ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            var known = new HashSet<string>(SteelParameterSchemas.Section.Parameters.Select(p => p.Key), StringComparer.Ordinal);
            foreach (var pair in Values)
                if (!known.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("UnknownOrEmptySteelPipelineParameter: " + pair.Key);
            NegligibleActions = Negligible(Values);
            Unperforated = Boolean("Unperforated"); NetArea = Positive("NetArea");
            if (NetArea.HasValue && Unperforated != false) throw new ArgumentException("NetAreaRequiresPerforatedDeclaration");
            var cl = Text("UniformCompressionClass");
            if (cl != null)
            {
                if (!int.TryParse(cl, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > 4)
                    throw new ArgumentException("InvalidUniformCompressionClass");
                UniformCompressionClass = value;
            }
            ClassificationSource = Text("ClassificationSource"); SectionFingerprint = Text("SectionFingerprint");
            ShearExcluded1 = Boolean("ShearBucklingExcluded1"); ShearExcluded2 = Boolean("ShearBucklingExcluded2");
            ShearSource1 = Text("ShearBucklingSource1"); ShearSource2 = Text("ShearBucklingSource2");
            HWebResistance = Text("HWebResistance"); HWebSupportSource = Text("HWebSupportSource");
            Pair(HWebResistance != null, HWebSupportSource, "HWebSupport");
            if (HWebResistance != null && (HWebResistance != "NonRigidEndPost" || ShearExcluded2.HasValue || ShearSource2 != null))
                throw new ArgumentException("InvalidOrConflictingHWebResistance");
            Pair(UniformCompressionClass.HasValue, ClassificationSource, "Classification");
            Pair(ShearExcluded1.HasValue, ShearSource1, "ShearBuckling1");
            Pair(ShearExcluded2.HasValue, ShearSource2, "ShearBuckling2");
            if ((NetArea.HasValue || UniformCompressionClass.HasValue || ShearExcluded1.HasValue || ShearExcluded2.HasValue || HWebResistance != null)
                && SectionFingerprint == null) throw new ArgumentException("SectionFingerprintRequiredForSteelAssessment");
        }

        private string Text(string key) => Values.TryGetValue(key, out var value) ? value : null;
        private bool? Boolean(string key)
        {
            string value = Text(key); if (value == null) return null;
            if (!bool.TryParse(value, out bool result)) throw new ArgumentException("InvalidSteelBoolean: " + key);
            return result;
        }
        private double? Positive(string key)
        {
            string value = Text(key); if (value == null) return null;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                || !(result > 0) || double.IsInfinity(result)) throw new ArgumentException("InvalidSteelPositiveValue: " + key);
            return result;
        }
        /// <summary>Negligible.Relative/Force/Moment of both steel schemas; omitted values keep the library defaults.</summary>
        internal static SteelNegligibleActionPolicy Negligible(IDictionary<string, string> values)
        {
            double? Read(string key, double max)
            {
                if (!values.TryGetValue(key, out var value)) return null;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                    || !(result >= 0) || !(result <= max)) throw new ArgumentException("InvalidSteelNegligibleLimit: " + key);
                return result;
            }
            double? relative = Read("Negligible.Relative", .01), force = Read("Negligible.Force", double.MaxValue),
                moment = Read("Negligible.Moment", double.MaxValue);
            if (relative == null && force == null && moment == null) return SteelNegligibleActionPolicy.Default;
            return new SteelNegligibleActionPolicy(relative ?? SteelNegligibleActionPolicy.DefaultRelativeToResistance, force ?? 0, moment ?? 0);
        }
        private static void Pair(bool supplied, string source, string name)
        { if (supplied != (source != null)) throw new ArgumentException(name + "ValueAndSourceRequiredTogether"); }
    }
}
