using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    internal static class ModelCode2010Helper
    {
        #region Conrete

        internal static double CalculateFcd(ConcreteMaterialModelCode2010 material, StandardModelCode2010 standard)
        {
            if (material.CompressionStressStrainDiagram == ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.StressBlock)
            {
                if (material.Fck > 90)
                    throw new ArgumentException("Fck > 90 not supported by Stress block");

                double eta;
                if (material.Fck <= 50.0)
                    eta = 1.0;
                else
                    eta = 1.0 - (material.Fck - 50.0) / 200;

                return eta * standard.AlphaCC * material.Fck / standard.GammaC;
            }
            else
            {
                return standard.AlphaCC * material.Fck / standard.GammaC;
            }
        }

        internal static double CalculateFctd(ConcreteMaterialModelCode2010 material, StandardModelCode2010 standard)
        {
            return standard.AlphaCT * material.Fctk05 / standard.GammaC;
        }

        internal static double CalculateFcdAccidental(ConcreteMaterialModelCode2010 material, StandardModelCode2010 standard)
        {
            return standard.AlphaCC * material.Fck / standard.GammaCAccidental;
        }

        internal static double CalculateFctdAccidental(ConcreteMaterialModelCode2010 material, StandardModelCode2010 standard)
        {
            return standard.AlphaCT * material.Fctk05 / standard.GammaCAccidental;
        }

        internal static double CalculateECd(ConcreteMaterialModelCode2010 material, StandardModelCode2010 standard)
        {
            return material.E / standard.GammaCE;
        }

        /// <returns>The design concrete stress related to <paramref name="strain"/></returns>
        internal static double CalculateSigmaC(double strain, double fcd, double fctd, ConcreteMaterialModelCode2010 material)
        {
            if (strain < 0)
            {
                return material.GetStress(strain) * fcd / material.Fck;
            }
            else
            {
                return material.GetStress(strain) * fctd / material.Fctk;
            }
        }
        #endregion

        #region Rebars

        /// <returns>The design rebar yielding stress</returns>
        internal static double CalculateFyd(RebarMaterial material, StandardModelCode2010 standard)
        {
            return material.Fyk / standard.GammaS;
        }

        /// <returns>The design rebar stress related to <paramref name="strain"/></returns>
        internal static double CalculateDesignStressRebar(double strain, RebarMaterial material, StandardModelCode2010 standard)
        {
            if (strain < CalculateDesignYieldingStrainRebar(material, standard))
            {
                return material.CalculateStress(strain);
            }
            else
            {
                return CalculateFyd(material, standard) + (strain - CalculateDesignYieldingStrainRebar(material, standard)) * material.Et;
            }
        }

        internal static double CalculateUltimateDesignStrainRebar(ReinforcedConcreteRebar rebar, StandardModelCode2010 standard)
        {
            return rebar.RebarMaterial.StrainU * standard.SteelCoefficientStrainTension;
        }

        internal static double CalculateUltimateDesignStrainRebar(IConcreteSection concreteSection, int rebar, StandardModelCode2010 standard)
        {
            return concreteSection.Rebars[rebar].RebarMaterial.StrainU * standard.SteelCoefficientStrainTension;
        }

        internal static double CalculateDesignYieldingStressRebar(RebarMaterial material, StandardModelCode2010 standard)
        {
            return material.Fyk / standard.GammaS;
        }

        internal static double CalculateDesignYieldingStrainRebar(RebarMaterial material, StandardModelCode2010 standard)
        {
            return CalculateDesignYieldingStressRebar(material, standard) / material.E;
        }

        internal static double CalculateDesignUltimateStrainRebar(RebarMaterial material, StandardModelCode2010 standard)
        {
            return material.StrainU * standard.SteelCoefficientStrainTension;
        } 
        #endregion

    }
}
