
using GPC.Checkers.Glasses.Glasses;


namespace GPC.Checkers.Glasses.LoadCases
{
    public interface IGlassLoadCase
    {
        string Name { get; }

        double LoadDuration { get; }

        double Temperature { get; }

        GlassSurface.LoadRestrainCondition LoadRestrainCondition { get; }
    }
}
