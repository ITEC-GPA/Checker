using System.Collections.Generic;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;

namespace GPC.Model.Checker
{
    /// <summary>Fingerprint of the owned copies an engine receives for ONE task (forces,
    /// property, sample, assignments), taken before and after the engine call: a mutating
    /// engine is caught in O(task). A change of the shared model is caught by the whole-plan
    /// check made at the start of each plan and again at the end of the run. Revalidating the
    /// whole plan around every task made a run quadratic in its number of tasks.</summary>
    internal static class OwnedCheckInputs
    {
        internal static string Of(CheckWorkItem item)
        {
            var owned = new List<object>();
            Add(owned, item.Actions?.Input); Add(owned, item.Section?.Input);
            if (item.Member != null)
            {
                foreach (var section in item.Member.Sections) Add(owned, section);
                foreach (var section in item.Member.ActionSections) Add(owned, section);
            }
            return ModelValues.Fingerprint(owned);
        }

        internal static string Of(ShellCheckInput input)
            => input == null ? null : ModelValues.Fingerprint(new object[] { input.Sample, input.LocalForces, input.Assignments, input.Property });

        private static void Add(List<object> owned, BeamActionInput input)
        { if (input != null) { owned.Add(input.Sample); owned.Add(input.Forces); owned.Add(input.Property); } }

        private static void Add(List<object> owned, BeamCheckInput input)
        { if (input != null) { owned.Add(input.Sample); owned.Add(input.Forces); owned.Add(input.Section); } }
    }
}
