using System;
using System.Collections.Generic;
using System.Globalization;

namespace GPC.Model.Checker
{
    /// <summary>Closed vocabulary stored in the existing EngineDefinition.Parameters map.
    /// Section-specific declarations bind to the complete Model property fingerprint.</summary>
    internal sealed class SteelPipelineParameters
    {
        internal readonly Dictionary<string, string> Values;
        internal bool? Unperforated { get; }
        internal double? NetArea { get; }
        internal int? UniformCompressionClass { get; }
        internal string ClassificationSource { get; }
        internal string SectionFingerprint { get; }
        internal bool? ShearExcluded1 { get; }
        internal bool? ShearExcluded2 { get; }
        internal string ShearSource1 { get; }
        internal string ShearSource2 { get; }

        internal SteelPipelineParameters(IDictionary<string, string> parameters)
        {
            Values = parameters == null ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            var known = new HashSet<string>(new[] { "Unperforated", "NetArea", "UniformCompressionClass",
                "ClassificationSource", "SectionFingerprint", "ShearBucklingExcluded1", "ShearBucklingExcluded2",
                "ShearBucklingSource1", "ShearBucklingSource2" }, StringComparer.Ordinal);
            foreach (var pair in Values)
                if (!known.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("UnknownOrEmptySteelPipelineParameter: " + pair.Key);
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
            Pair(UniformCompressionClass.HasValue, ClassificationSource, "Classification");
            Pair(ShearExcluded1.HasValue, ShearSource1, "ShearBuckling1");
            Pair(ShearExcluded2.HasValue, ShearSource2, "ShearBuckling2");
            if ((NetArea.HasValue || UniformCompressionClass.HasValue || ShearExcluded1.HasValue || ShearExcluded2.HasValue)
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
        private static void Pair(bool supplied, string source, string name)
        { if (supplied != (source != null)) throw new ArgumentException(name + "ValueAndSourceRequiredTogether"); }
    }
}
