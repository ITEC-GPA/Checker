using GPC.Model.Elements.Glasses;
using GPC.Geometry;
using System.Collections.Generic;

namespace GPC.Checker.Glasses.Wrappers
{
    internal interface IGlassPanelWrapper
    {
        #region Properties
        double GetTotalThickness();

        double GetDeformationThickness(double loadDuration);

        double GetStressThickness(double loadDuration); 

        #endregion

        #region Material

        double GetElasticModulus();

        double GetPoissonRatios();

        double GetSelfWeightPerUnitArea();

        double GetSelfWeightTotal(); 

        #endregion

    }
}
