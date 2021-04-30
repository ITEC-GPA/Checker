using GPC.Checkers.Glasses.LoadCases;
using GPC.Model.Materials;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Glasses.Extensions
{
    public static class Extensions
    {
        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <param name="shearModuleLimit"></param>
        /// <returns>The list of loadcases that have a Gvalue lower than <paramref name="shearModuleLimit"/>, then they can be considered as long term loads</returns>
        public static List<IGlassLoadCase> GetLongTermLoadCases(this IEnumerable<IGlassLoadCase> loadCases, InterlayerMaterial material, double shearModuleLimit)
        {
            List<IGlassLoadCase> ltLoadCases = new List<IGlassLoadCase>();

            foreach (var loadcase in loadCases)
            {
                if (material.GetShearModule(loadcase.LoadDuration, loadcase.Temperature) <= shearModuleLimit)
                    ltLoadCases.Add(loadcase);
            }

            return ltLoadCases;
        }

        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <returns>The loadCase that has the lower Gvalue</returns>
        /// <inheritdoc cref="Enumerable.Aggregate{TSource}(IEnumerable{TSource}, System.Func{TSource, TSource, TSource})"/>
        public static IGlassLoadCase GetLowerGvalueLoadCase(this IEnumerable<IGlassLoadCase> loadCases, InterlayerMaterial material)
        {
            return loadCases.Aggregate((i, g) => (material.GetShearModule(i.LoadDuration, i.Temperature) < material.GetShearModule(g.LoadDuration, g.Temperature)) ? i : g);
        }

        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <returns>The loadCase that has the higher Gvalue</returns>
        /// <inheritdoc cref="Enumerable.Aggregate{TSource}(IEnumerable{TSource}, System.Func{TSource, TSource, TSource})"/>
        public static IGlassLoadCase GetHigherGvalueLoadCase(this IEnumerable<IGlassLoadCase> loadCases, InterlayerMaterial material)
        {
            return loadCases.Aggregate((i, g) => (material.GetShearModule(i.LoadDuration, i.Temperature) > material.GetShearModule(g.LoadDuration, g.Temperature)) ? i : g);
        }
    }
}