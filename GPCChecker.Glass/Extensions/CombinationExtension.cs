using GPC.Checkers.Glasses.LoadCases;
using GPC.Model.Combinations;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checker.Glasses.Extensions
{
    public static class CombinationExtension
    {
        /// <summary>
        /// This function call <see cref="Combination.GetLoadCases()"/> and cast the results into <see cref="IGlassLoadCase"/>
        /// </summary>
        /// <param name="combination"></param>
        /// <inheritdoc cref="Combination.GetLoadCases()"/>
        public static List<IGlassLoadCase> GetIGlassLoadCase(this Combination combination)
        {
            return combination.GetLoadCases().Cast<IGlassLoadCase>().ToList();
        }

        /// <summary>
        /// This function call <see cref="Combination.GetLoadCaseCoefficientsTuple()"/> and cast the results into <see cref="IGlassLoadCase"/>
        /// </summary>
        /// <param name="combination"></param>
        /// <param name="loadCases"></param>
        /// <inheritdoc cref="Combination.GetLoadCaseCoefficientsTuple()"/>
        public static (IGlassLoadCase loadCase, double coefficient)[] GetLoadCaseCoefficientsTuple(this Combination combination, IEnumerable<IGlassLoadCase> loadCases)
        {
            (Model.LoadCases.LoadCaseBase loadcase, double coefficient)[] b = combination.GetLoadCaseCoefficientsTuple(loadCases.Cast<Model.LoadCases.LoadCaseBase>().ToList());

            return b.Select(i => ((IGlassLoadCase loadcase, double coefficient))i).ToArray();
        }

        /// <summary>
        /// This function call <see cref="Combination.GetLoadCaseCoefficientsTuple()"/> and cast the results into <see cref="IGlassLoadCase"/>
        /// </summary>
        /// <param name="combination"></param>
        /// <param name="loadCases"></param>
        /// <inheritdoc cref="Combination.GetLoadCaseCoefficientsTuple()"/>
        public static bool ContainsLoadCaseCoefficients(this Combination combination, IEnumerable<(IGlassLoadCase, double)> loadCases)
        {
            (IGlassLoadCase, double) d = loadCases.First();

            return combination.ContainsLoadCaseCoefficients(loadCases.Select(i => ((Model.LoadCases.LoadCaseBase, double))i));
        }
    }
}