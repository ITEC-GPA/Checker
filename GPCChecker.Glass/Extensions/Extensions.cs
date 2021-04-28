using System.Collections.Generic;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Model.Materials;
using System.Linq;

namespace GPC.Checkers.Glasses.Extensions
{
    public static class Extensions
    {
        /// <summary>
        ///  
        /// </summary>
        /// <param name="combination"></param>
        /// <param name="material"></param>
        /// <param name="shearModuleLimit"></param>
        /// <returns>The list of loadcases that have a Gvalue lower than <paramref name="shearModuleLimit"/>, then they can be considered as long term loads</returns>
        public static List<LoadCase> GetLongTermLoadCases(this GPC.Model.Combinations.Combination combination, InterlayerMaterial material, double shearModuleLimit)
        {
            List<LoadCase> loadCases = new List<LoadCase>();

            foreach (var loadcase in combination.GetLoadCases())
            {
                var lc = loadcase as LoadCase;

                double lcShearModule = material.GetShearModule(lc.LoadDuration, lc.Temperature);

                if (lcShearModule <= shearModuleLimit)
                    loadCases.Add(lc);
            }

            return loadCases;
        }


        /// <summary>
        ///  
        /// </summary>
        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <returns>The loadCase that has the lower Gvalue</returns>
        /// <inheritdoc cref="Enumerable.Aggregate{TSource}(IEnumerable{TSource}, System.Func{TSource, TSource, TSource})"/>
        public static LoadCase GetLowerGvalueLoadCase(this IEnumerable<LoadCase> loadCases, InterlayerMaterial material)
        {
            return loadCases.Aggregate((i,g) => (material.GetShearModule(i.LoadDuration, i.Temperature) < material.GetShearModule(g.LoadDuration, g.Temperature)) ? i : g);
        }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <returns>The loadCase that has the higher Gvalue</returns>
        /// <inheritdoc cref="Enumerable.Aggregate{TSource}(IEnumerable{TSource}, System.Func{TSource, TSource, TSource})"/>
        public static LoadCase GetHigherGvalueLoadCase(this IEnumerable<LoadCase> loadCases, InterlayerMaterial material)
        {
            return loadCases.Aggregate((i, g) => (material.GetShearModule(i.LoadDuration, i.Temperature) > material.GetShearModule(g.LoadDuration, g.Temperature)) ? i : g);
        }

    }
}
