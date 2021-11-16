using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    internal static class SectionSolverHelper
    {

        /// <param name="rebar"></param>
        /// <param name="strain"></param>
        /// <returns></returns>
        internal static double CalculatecharacteristicStressRebar(ReinforcedConcreteRebar rebar, double strain)
        {
            return rebar.RebarMaterial.CalculateStress(strain + rebar.EpsilonP);
        }


        internal static double CalculatePointStrain(StrainPlane strainPlane, Point2d point)
        {
            return strainPlane.StrainReferencePoint + strainPlane.ChiX * (point.X - strainPlane.ReferencePoint.X) + strainPlane.ChiY * (point.Y - strainPlane.ReferencePoint.Y);
        }

    }
}
