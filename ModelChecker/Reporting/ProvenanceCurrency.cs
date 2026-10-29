using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker
{
    /// <summary>The currency of the row provenances of one CurrentOutcome call against one state of the model. The rows of a run share
    /// one provenance (the same object, or equal values after an archive): each distinct provenance is compared with the model once, by
    /// <see cref="VerificationProvenance.IsCurrent(Models.Model)"/> itself, instead of once per row (rows x elements). Created for one
    /// call and dropped with it: the next call compares again and sees any change made to the model in between.</summary>
    internal sealed class ProvenanceCurrency
    {
        private readonly Models.Model _model;
        private readonly Dictionary<string, bool> _byValues = new Dictionary<string, bool>(StringComparer.Ordinal);
        private VerificationProvenance _last;
        private bool _lastCurrent;
        internal ProvenanceCurrency(Models.Model model) { _model = model; }

        internal bool IsCurrent(VerificationProvenance provenance)
        {
            if (provenance == null) throw new ArgumentNullException(nameof(provenance));
            if (ReferenceEquals(provenance, _last)) return _lastCurrent;
            var key = Values(provenance);
            // A provenance whose values cannot be read is not memoized: IsCurrent answers (or fails) for it as it always did.
            if (key == null) return provenance.IsCurrent(_model);
            if (!_byValues.TryGetValue(key, out var current)) _byValues.Add(key, current = provenance.IsCurrent(_model));
            _last = provenance; _lastCurrent = current;
            return current;
        }

        /// <summary>Everything IsCurrent compares with the model, each value length-prefixed (null apart), the datasets in ordinal order
        /// (IsCurrent does not depend on their order); null when the values cannot be read.</summary>
        private static string Values(VerificationProvenance p)
        {
            IReadOnlyDictionary<string, string> datasets;
            try { datasets = p.DatasetFingerprints; }
            catch (ArgumentException) { return null; }
            var key = new StringBuilder();
            void Add(string value) { if (value == null) key.Append('-'); else key.Append(value.Length).Append(':').Append(value); }
            Add(p.AnalysisSnapshotId); Add(p.AnalysisFingerprint); Add(p.ScenarioId); Add(p.ScenarioFingerprint); Add(p.DesignFingerprint);
            key.Append(datasets.Count).Append('#');
            foreach (var dataset in datasets.OrderBy(d => d.Key, StringComparer.Ordinal)) { Add(dataset.Key); Add(dataset.Value); }
            return key.ToString();
        }
    }
}
