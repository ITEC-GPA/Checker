using System.Collections.Generic;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Materials;

namespace GPC.Checker.Glasses.Extensions
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
    }
}
