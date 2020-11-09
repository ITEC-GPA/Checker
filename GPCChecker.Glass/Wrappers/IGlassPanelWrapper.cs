
using GPC.Model.Elements.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    public interface IGlassPanelWrapper
    {
        double GetTotalThickness();

        double GetDeformationThickness();

        double GetStressThickness();
    }
}
