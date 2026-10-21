using System.Text.Json;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace EmbeddedWalls.Tests;

/// <summary>Models of the October 2026 review, identical to the cases of tools/attesi/paratie_indipendente.py.
/// Units N, mm, MPa, N/mm³; 1000 mm strip, default 250 mm mesh.</summary>
internal static class ReviewModels
{
    internal static readonly Soil Sand = new("Sand", 18e-6, 20e-6, Math.PI / 6, 0, "review benchmark");
    internal static readonly Soil Clay = new("Clay", 19e-6, 19e-6, 0, 0, "review benchmark", .04);

    static EmbeddedWallSoil Layer(Soil soil, double top, double bottom, double kh, double k0, EmbeddedWallDrainage drainage = EmbeddedWallDrainage.Drained)
        => new() { Soil = soil, Top = top, Bottom = bottom, HorizontalModulus = kh, AtRestCoefficient = k0, Drainage = drainage, StiffnessSource = "review benchmark" };
    static EmbeddedWallSegment Segment(double top, double bottom, double ei) => new() { Top = top, Bottom = bottom, FlexuralRigidity = ei, SectionId = "S", Source = "review benchmark" };

    /// <summary>Cantilever in dry sand, excavation 0 → −2 m → −4 m.</summary>
    internal static EmbeddedWallModel CantileverSand(double mesh = 250) => new()
    {
        CaseId = "cantilever_sand", Top = 0, Bottom = -12000, MaximumElementLength = mesh, Segments = [Segment(0, -12000, 2e14)],
        LeftSoils = [Layer(Sand, 0, -12000, .01, .5)], RightSoils = [Layer(Sand, 0, -12000, .01, .5)],
        Stages = [
            new() { Id = "S0", Left = new() { Elevation = 0 }, Right = new() { Elevation = 0 } },
            new() { Id = "S1", Left = new() { Elevation = 0 }, Right = new() { Elevation = -2000 } },
            new() { Id = "S2", Left = new() { Elevation = 0 }, Right = new() { Elevation = -4000 } }]
    };

    /// <summary>Water at −3 m, 20 kPa behind the wall, anchor at −1.5 m (EA/L 20 MN/m, 20°, i = 2.5 m, P0 = 150 kN),
    /// excavation to −2 m, then to −7 m with dewatering to the excavation level.</summary>
    internal static EmbeddedWallModel AnchoredWater(double mesh = 250)
    {
        EmbeddedWallGround Back() => new() { Elevation = 0, WaterElevation = -3000, Surcharge = .02 };
        return new()
        {
            CaseId = "anchored_water", Top = 0, Bottom = -14000, MaximumElementLength = mesh, Segments = [Segment(0, -14000, 3e14)],
            LeftSoils = [Layer(Sand, 0, -14000, .015, .5)], RightSoils = [Layer(Sand, 0, -14000, .015, .5)],
            Supports = [new() { Id = "T1", Elevation = -1500, Kind = EmbeddedWallSupportKind.Anchor, AxialStiffness = 20000,
                Inclination = 20 * Math.PI / 180, ResistingDirection = 1, Spacing = 2500 }],
            Stages = [
                new() { Id = "S0", Left = Back(), Right = new() { Elevation = 0, WaterElevation = -3000 } },
                new() { Id = "S1", Left = Back(), Right = new() { Elevation = -2000, WaterElevation = -3000 } },
                new() { Id = "S2", Left = Back(), Right = new() { Elevation = -2000, WaterElevation = -3000 }, ActiveSupports = [new() { SupportId = "T1", Prestress = 150000 }] },
                new() { Id = "S3", Left = Back(), Right = new() { Elevation = -7000, WaterElevation = -7000 }, ActiveSupports = [new() { SupportId = "T1", Prestress = 150000 }] }]
        };
    }

    /// <summary>Undrained clay (cu 40 kPa, K0 0.6), 10 kPa behind the wall, excavation to −3 m, total stresses.</summary>
    internal static EmbeddedWallModel CantileverUndrained(double mesh = 250) => new()
    {
        CaseId = "cantilever_undrained", Top = 0, Bottom = -10000, MaximumElementLength = mesh, Segments = [Segment(0, -10000, 2e14)],
        LeftSoils = [Layer(Clay, 0, -10000, .008, .6, EmbeddedWallDrainage.UndrainedTotalStress)],
        RightSoils = [Layer(Clay, 0, -10000, .008, .6, EmbeddedWallDrainage.UndrainedTotalStress)],
        Stages = [
            new() { Id = "S0", Left = new() { Elevation = 0, Surcharge = .01 }, Right = new() { Elevation = 0 } },
            new() { Id = "S1", Left = new() { Elevation = 0, Surcharge = .01 }, Right = new() { Elevation = -3000 } }]
    };

    /// <summary>Soil-free cantilever (L 6 m, EI 2e14, toe fixed) with a top support EA/L 10000 N/mm:
    /// installed unloaded, pushed away by −10 kN, then pushed back by +10 kN.</summary>
    internal static EmbeddedWallModel ReversedSupport(EmbeddedWallSupportKind kind) => new()
    {
        CaseId = "reversed_" + kind, Top = 0, Bottom = -6000, MaximumElementLength = 500, FixBottomDisplacement = true, FixBottomRotation = true,
        Segments = [Segment(0, -6000, 2e14)],
        Supports = [new() { Id = "P1", Elevation = 0, Kind = kind, AxialStiffness = 10000, ResistingDirection = 1, Spacing = 1000 }],
        Stages = [Stage("Install", 0), Stage("Away", -10000), Stage("Back", 10000)]
    };
    static EmbeddedWallStage Stage(string id, double force) => new()
    {
        Id = id, Left = new() { Elevation = -6000 }, Right = new() { Elevation = -6000 }, ActiveSupports = [new() { SupportId = "P1" }],
        PointLoads = force == 0 ? [] : [new() { Elevation = 0, HorizontalForce = force }]
    };

    internal static JsonElement Expected() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "independent-expected.json"))).RootElement;
    internal static JsonElement ExpectedStage(string caseId, string stageId)
        => Expected().GetProperty("cases").GetProperty(caseId).EnumerateArray().Single(s => s.GetProperty("id").GetString() == stageId);
}
