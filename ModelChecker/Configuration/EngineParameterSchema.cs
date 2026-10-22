using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GPC.Model.Checker.Configuration
{
    public enum EngineParameterKind { Text, Boolean, Integer, Number, Choice }

    /// <summary>Runtime metadata, not a persisted model or an engineering assessment.</summary>
    public sealed class EngineParameterDescriptor
    {
        public string Key { get; }
        public EngineParameterKind Kind { get; }
        public string Unit { get; }
        public string Description { get; }
        public double? Minimum { get; }
        public double? Maximum { get; }
        public bool ExclusiveMinimum { get; }
        public IReadOnlyList<string> Choices { get; }
        public IReadOnlyList<string> Modes { get; }
        internal EngineParameterDescriptor(string key, EngineParameterKind kind, string unit, string description,
            double? minimum, double? maximum, bool exclusiveMinimum, IEnumerable<string> choices, IEnumerable<string> modes)
        {
            Key=key; Kind=kind; Unit=unit; Description=description; Minimum=minimum; Maximum=maximum;
            ExclusiveMinimum=exclusiveMinimum;
            Choices=Array.AsReadOnly((choices??Enumerable.Empty<string>()).ToArray());
            Modes=Array.AsReadOnly((modes??Enumerable.Empty<string>()).ToArray());
        }
        internal bool Accepts(string value)
        {
            if(string.IsNullOrWhiteSpace(value)) return false;
            if(Kind==EngineParameterKind.Text) return true;
            if(Kind==EngineParameterKind.Boolean) return bool.TryParse(value,out _);
            if(Kind==EngineParameterKind.Choice) return Choices.Contains(value,StringComparer.Ordinal);
            double number;
            if(Kind==EngineParameterKind.Integer) {
                if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int n)) return false;
                number=n;
            } else if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)) return false;
            return !double.IsNaN(number)&&!double.IsInfinity(number)
                &&(!Minimum.HasValue||(ExclusiveMinimum?number>Minimum:number>=Minimum))
                &&(!Maximum.HasValue||number<=Maximum);
        }
    }

    /// <summary>Null value means the key must be present. Conditions in a rule are ANDed.</summary>
    public sealed class EngineParameterCondition
    {
        public string Key { get; }
        public string Value { get; }
        internal EngineParameterCondition(string key,string value=null) { Key=key;Value=value; }
        internal bool Matches(IReadOnlyDictionary<string,string> values) => values.TryGetValue(Key,out var v)
            && !string.IsNullOrWhiteSpace(v) && (Value==null||string.Equals(v,Value,StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>At least one alternative must be complete. An empty alternatives list imposes no presence requirement.</summary>
    public sealed class EngineParameterRule
    {
        public string Code { get; }
        public string Description { get; }
        public IReadOnlyList<EngineParameterCondition> WhenAll { get; }
        public IReadOnlyList<IReadOnlyList<string>> Alternatives { get; }
        public IReadOnlyList<string> ForbiddenKeys { get; }
        internal EngineParameterRule(string code,string description,IEnumerable<EngineParameterCondition> when,
            IEnumerable<string[]> alternatives,IEnumerable<string> forbidden=null)
        {
            Code=code;Description=description;
            WhenAll=Array.AsReadOnly((when??Enumerable.Empty<EngineParameterCondition>()).ToArray());
            Alternatives=Array.AsReadOnly((alternatives??Enumerable.Empty<string[]>())
                .Select(a=>(IReadOnlyList<string>)Array.AsReadOnly(a.ToArray())).ToArray());
            ForbiddenKeys=Array.AsReadOnly((forbidden??Enumerable.Empty<string>()).ToArray());
        }
    }
    public sealed class EngineParameterIssue
    {
        public string Code { get; }
        public string Key { get; }
        public string Message { get; }
        internal EngineParameterIssue(string code,string key,string message) { Code=code;Key=key;Message=message; }
    }

    /// <summary>Configuration preflight only. No issue does not certify geometry, actions, code applicability or engineering resistance.</summary>
    public sealed class EngineParameterSchema
    {
        public string EngineKind { get; }
        public int Schema => 1;
        public IReadOnlyList<EngineParameterDescriptor> Parameters { get; }
        public IReadOnlyList<EngineParameterRule> Rules { get; }
        public IReadOnlyList<string> ExternalRequirements { get; }
        internal EngineParameterSchema(string kind,IEnumerable<EngineParameterDescriptor> parameters,
            IEnumerable<EngineParameterRule> rules,IEnumerable<string> external)
        {
            EngineKind=kind;Parameters=Array.AsReadOnly(parameters.ToArray());Rules=Array.AsReadOnly(rules.ToArray());
            ExternalRequirements=Array.AsReadOnly(external.ToArray());
            if(Parameters.Select(p=>p.Key).Distinct(StringComparer.Ordinal).Count()!=Parameters.Count)
                throw new ArgumentException("DuplicateParameterMetadata");
        }
        public IReadOnlyList<EngineParameterIssue> Validate(IDictionary<string,string> parameters)
        {
            var values=parameters==null?new Dictionary<string,string>(StringComparer.Ordinal)
                :new Dictionary<string,string>(parameters,StringComparer.Ordinal);
            var fields=Parameters.ToDictionary(p=>p.Key,StringComparer.Ordinal);
            var issues=new List<EngineParameterIssue>();
            values.TryGetValue("AnalysisMode",out var mode);
            foreach(var pair in values) {
                if(!fields.TryGetValue(pair.Key,out var field)) issues.Add(new EngineParameterIssue("UnknownParameter",pair.Key,"Parameter is not in the engine vocabulary."));
                else if(!field.Accepts(pair.Value)) issues.Add(new EngineParameterIssue("InvalidParameterValue",pair.Key,"Value does not satisfy the declared type, unit choice or range."));
                else if(mode!=null&&field.Modes.Count!=0&&!field.Modes.Contains(mode,StringComparer.Ordinal))
                    issues.Add(new EngineParameterIssue("ParameterOnAnotherMode",pair.Key,"Parameter is not applicable to this analysis mode."));
            }
            bool Present(string key)=>values.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value);
            foreach(var rule in Rules.Where(r=>r.WhenAll.All(c=>c.Matches(values)))) {
                if(rule.Alternatives.Count!=0&&!rule.Alternatives.Any(a=>a.All(Present)))
                    issues.Add(new EngineParameterIssue(rule.Code,null,rule.Description));
                foreach(var key in rule.ForbiddenKeys.Where(Present)) issues.Add(new EngineParameterIssue(rule.Code,key,rule.Description));
            }
            return Array.AsReadOnly(issues.GroupBy(i => (i.Code, i.Key)).Select(g => g.First()).ToArray());
        }
    }
}
