using GPC.Checkers.CompositeBridge;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;

namespace CompositeBridgePerformance;

// Shared, deterministic inputs for numerical regression and timing. No random generator or wall-clock inputs.
public static class BridgePerformanceCases
{
    private static readonly BridgeMaterialSet Materials = new(ConcreteMaterialEN1992Data.C35_45,
        SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C);

    public static HBridgeInput[] Station(int station, bool class4, int combinations = 100)
    {
        var input = new HBridgeInput {
            Materials = Materials,
            Geometry = new() { SlabWidth = 3000 + 2 * station, SlabHeight = 250, WebHeight = 1800 + station,
                WebThickness = 14, TopWidth = 500, TopThickness = 25, BottomWidth = 700, BottomThickness = 30 },
            TopRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
            BottomRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
            Options = new() { Class4 = class4, LimitState = BridgeLimitState.Rare, CommonLoadY = 0 }
        };
        double stationFactor = 1 + station / 500d;
        var permanent = new[] {
            new BridgePhase { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 * stationFactor, ShearKN = 300,
                Reference = BridgeLoadReference.CommonElevation },
            new BridgePhase { Name = "G2", MomentKNm = 2000 * stationFactor, ForceKN = -100, Phi = 2, PsiL = 1.1, ShearKN = 200,
                Reference = BridgeLoadReference.CommonElevation },
            new BridgePhase { Name = "Ritiro", Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -200, Phi = 2, PsiL = .55,
                Reference = BridgeLoadReference.CommonElevation }
        };
        return Enumerable.Range(0, combinations).Select(i => input with {
            Phases = permanent.Append(new BridgePhase { Name = "Q", MomentKNm = (-2500 + 5500 * i / Math.Max(1d, combinations - 1)) * stationFactor,
                ForceKN = -200 + 400 * i / Math.Max(1d, combinations - 1), ShearKN = -350 + 700 * i / Math.Max(1d, combinations - 1),
                Reference = BridgeLoadReference.CommonElevation }).ToArray()
        }).ToArray();
    }
}
