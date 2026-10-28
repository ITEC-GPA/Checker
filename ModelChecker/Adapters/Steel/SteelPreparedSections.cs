using System;
using System.Collections.Concurrent;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Core;
using GPC.Model.Sections.Steel;

namespace GPC.Model.Checker
{
    /// <summary>Exact-input memo of the owned section snapshots of the steel engines of this assembly.
    /// A member engine is bound to ONE member and ONE design state, so every combination and every cut
    /// copied the same property again (an XML round trip, about 0.4 ms for an H) and rebuilt Model's lazy
    /// section caches (torsion, shear centre, plastic moduli) and the effective properties memoized on the
    /// snapshot. The key is Model's fingerprint of the property, its revision identity, plus the hole
    /// declaration: an equal key copies to an equal value, so every result is bit-identical. Snapshots
    /// are only read (thread-safe lazy caches); the memo is bounded and simply restarts when full.</summary>
    internal static class SteelPreparedSections
    {
        private const int Capacity = 512;
        private static readonly ConcurrentDictionary<(string Property, bool? Unperforated, double? NetArea), SteelSectionInput> Snapshots
            = new ConcurrentDictionary<(string, bool?, double?), SteelSectionInput>();

        /// <summary>Same value, validation and exceptions as new SteelSectionInput(section, unperforated, netArea).</summary>
        internal static SteelSectionInput Get(ISteelSection section, bool? unperforated, double? netArea = null)
        {
            string fingerprint;
            // A value without a fingerprint contract keeps the plain copy and its own exception.
            try { fingerprint = section == null ? null : ModelValues.Fingerprint(new object[] { section }); }
            catch (Exception) { fingerprint = null; }
            return fingerprint == null ? new SteelSectionInput(section, unperforated, netArea) : Get(section, fingerprint, unperforated, netArea);
        }

        /// <param name="fingerprint">ModelValues.Fingerprint of exactly { section }.</param>
        internal static SteelSectionInput Get(ISteelSection section, string fingerprint, bool? unperforated, double? netArea = null)
        {
            var key = (fingerprint, unperforated, netArea);
            if (Snapshots.TryGetValue(key, out var existing)) return existing;
            // A rejected section throws here and is never memoized.
            var created = new SteelSectionInput(section, unperforated, netArea);
            if (Snapshots.Count >= Capacity) Snapshots.Clear();
            return Snapshots.GetOrAdd(key, created);
        }
    }
}
