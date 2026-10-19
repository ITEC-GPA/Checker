using GPC.Model.Geotechnics.EmbeddedWalls;

namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

public static partial class EmbeddedWallAnalysis
{
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }

    /// <summary>Validates the complete history before calculation; no silent truncation or default soil parameters.</summary>
    public static void Validate(EmbeddedWallModel m)
    {
        if (m == null) throw new ArgumentNullException(nameof(m));
        Require(!string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.CaseId), "Wall name and case identifier required.");
        Require(Finite(m.Top) && Finite(m.Bottom) && m.Top > m.Bottom, "Wall top must be above bottom.");
        Require(Finite(m.TributaryWidth) && m.TributaryWidth > 0 && Finite(m.MaximumElementLength) && m.MaximumElementLength > 0, "Positive tributary width and mesh length required.");
        Require(m.Segments != null && m.Segments.Count > 0 && m.LeftSoils != null && m.RightSoils != null && m.Supports != null && m.Stages != null && m.Stages.Count > 0, "Sections, soil collections and at least one stage required.");
        double next = m.Top;
        foreach (var s in m.Segments!)
        {
            Require(s != null && Finite(s.Top) && Finite(s.Bottom) && Math.Abs(s.Top - next) <= 1e-8 && s.Bottom < s.Top
                && Finite(s.FlexuralRigidity) && s.FlexuralRigidity > 0 && Finite(s.WeightPerLength) && s.WeightPerLength >= 0
                && !string.IsNullOrWhiteSpace(s.Source) && !string.IsNullOrWhiteSpace(s.SectionId), "Contiguous ordered wall segments with positive EI, section identifier and source required.");
            next = s!.Bottom;
        }
        Require(Math.Abs(next - m.Bottom) <= 1e-8, "Wall segments must cover the complete wall.");
        foreach (var soils in new[] { m.LeftSoils!, m.RightSoils! })
        {
            Require(soils.All(s => s != null), "Soil profile contains a null layer.");
            next = soils.Count == 0 ? m.Top : soils[0].Top;
            foreach (var s in soils)
            {
                Require(s != null && s.Soil != null && Finite(s.Top) && Finite(s.Bottom) && s.Top > s.Bottom && Math.Abs(s.Top - next) <= 1e-8
                    && Finite(s.HorizontalModulus) && s.HorizontalModulus > 0 && Finite(s.AtRestCoefficient) && s.AtRestCoefficient > 0
                    && !string.IsNullOrWhiteSpace(s.StiffnessSource) && Enum.IsDefined(typeof(EmbeddedWallDrainage), s.Drainage), "Invalid soil profile, K0, stiffness or provenance.");
                Require(s!.Drainage != EmbeddedWallDrainage.UndrainedTotalStress || s.Soil!.UndrainedShearStrength.HasValue, "Undrained analysis requires cu.");
                next = s.Bottom;
            }
            Require(soils.Count == 0 || next <= m.Bottom, "Soil layers must reach the wall toe.");
        }
        var supportIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in m.Supports!)
        {
            Require(s != null && !string.IsNullOrWhiteSpace(s.Id) && supportIds.Add(s.Id) && Inside(s.Elevation, m)
                && Finite(s.AxialStiffness) && s.AxialStiffness > 0 && Finite(s.Spacing) && s.Spacing > 0
                && Finite(s.Inclination) && Math.Abs(s.Inclination) < Math.PI / 2 && Math.Abs(s.ResistingDirection) == 1
                && (!s.Capacity.HasValue || Finite(s.Capacity.Value) && s.Capacity.Value > 0)
                && Enum.IsDefined(typeof(EmbeddedWallSupportKind), s.Kind), "Invalid or duplicate support; define EA/L, spacing, inclination and direction.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in m.Stages!)
        {
            Require(stage != null && !string.IsNullOrWhiteSpace(stage.Id) && ids.Add(stage.Id) && stage.Left != null && stage.Right != null
                && stage.ActiveSupports != null && stage.PointLoads != null && stage.DistributedLoads != null && Finite(stage.DistributedLoad)
                && Finite(stage.StiffnessFactor) && stage.StiffnessFactor > 0, "Invalid or duplicate stage.");
            foreach (var pair in new[] { (stage!.Left, m.LeftSoils!), (stage.Right, m.RightSoils!) })
            {
                var g = pair.Item1!;
                Require(Finite(g.Elevation) && Finite(g.Surcharge) && g.Surcharge >= 0 && (!g.WaterElevation.HasValue || Finite(g.WaterElevation.Value)), "Invalid ground, surcharge or water level.");
                Require(pair.Item2.Count == 0 || g.Elevation <= pair.Item2[0].Top, "Soil profile must include the highest ground elevation (also backfill).");
                WallSoilPressure.ValidatePorePressures(g);
                Require(g.PorePressures.Count == 0 || g.PorePressures[0].Elevation >= Math.Min(g.Elevation, m.Top)
                    && g.PorePressures[g.PorePressures.Count - 1].Elevation <= m.Bottom, "Pore-pressure profile must cover the wall below ground.");
            }
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in stage!.ActiveSupports!)
            {
                Require(a != null && supportIds.Contains(a.SupportId) && active.Add(a.SupportId) && Finite(a.Prestress) && a.Prestress >= 0, "Invalid active support or lock-off force.");
                var s = m.Supports!.First(x => x.Id == a!.SupportId);
                Require(!s.Capacity.HasValue || a!.Prestress <= s.Capacity.Value, "Lock-off force exceeds support capacity.");
            }
            foreach (var p in stage.PointLoads!) Require(p != null && Inside(p.Elevation, m) && Finite(p.HorizontalForce) && Finite(p.Moment) && Finite(p.DownwardForce), "Invalid point action.");
            var distributedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in stage.DistributedLoads!) Require(q != null && !string.IsNullOrWhiteSpace(q.Id) && distributedIds.Add(q.Id)
                && Inside(q.Top, m) && Inside(q.Bottom, m) && q.Top > q.Bottom && Finite(q.TopLoad) && Finite(q.BottomLoad)
                && !string.IsNullOrWhiteSpace(q.Source), "Distributed loads require unique stage identifiers, ordered elevations inside the wall, finite intensities and provenance.");
        }
    }
    private static bool Inside(double z, EmbeddedWallModel m) => Finite(z) && z >= m.Bottom && z <= m.Top;

    internal static EmbeddedWallModel Snapshot(EmbeddedWallModel m) => new EmbeddedWallModel
    {
        Name = m.Name, CaseId = m.CaseId, Top = m.Top, Bottom = m.Bottom, TributaryWidth = m.TributaryWidth, MaximumElementLength = m.MaximumElementLength,
        FixTopDisplacement = m.FixTopDisplacement, FixTopRotation = m.FixTopRotation, FixBottomDisplacement = m.FixBottomDisplacement, FixBottomRotation = m.FixBottomRotation,
        Segments = m.Segments.Select(s => new EmbeddedWallSegment { Top = s.Top, Bottom = s.Bottom, FlexuralRigidity = s.FlexuralRigidity, WeightPerLength = s.WeightPerLength, Source = s.Source, SectionId = s.SectionId }).ToArray(),
        LeftSoils = CopySoils(m.LeftSoils), RightSoils = CopySoils(m.RightSoils),
        Supports = m.Supports.Select(s => new EmbeddedWallSupport { Id = s.Id, Elevation = s.Elevation, Kind = s.Kind, AxialStiffness = s.AxialStiffness, Inclination = s.Inclination, ResistingDirection = s.ResistingDirection, Spacing = s.Spacing, Capacity = s.Capacity }).ToArray(),
        Stages = m.Stages.Select(s => new EmbeddedWallStage { Id = s.Id, Left = CopyGround(s.Left), Right = CopyGround(s.Right), DistributedLoad = s.DistributedLoad, StiffnessFactor = s.StiffnessFactor,
            PointLoads = s.PointLoads.Select(p => new EmbeddedWallPointLoad { Elevation = p.Elevation, HorizontalForce = p.HorizontalForce, Moment = p.Moment, DownwardForce = p.DownwardForce }).ToArray(),
            DistributedLoads = s.DistributedLoads.Select(q => new EmbeddedWallDistributedLoad { Id = q.Id, Top = q.Top, Bottom = q.Bottom,
                TopLoad = q.TopLoad, BottomLoad = q.BottomLoad, Source = q.Source }).ToArray(),
            ActiveSupports = s.ActiveSupports.Select(a => new EmbeddedWallSupportActivation { SupportId = a.SupportId, Prestress = a.Prestress }).ToArray() }).ToArray()
    };
    private static EmbeddedWallGround CopyGround(EmbeddedWallGround g) => new EmbeddedWallGround { Elevation = g.Elevation, WaterElevation = g.WaterElevation, Surcharge = g.Surcharge,
        PorePressures = g.PorePressures.ToArray(), PorePressureSource = g.PorePressureSource, SaturationFromPorePressures = g.SaturationFromPorePressures };
    private static EmbeddedWallSoil[] CopySoils(IReadOnlyList<EmbeddedWallSoil> soils) => soils.Select(s => new EmbeddedWallSoil { Soil = s.Soil, Top = s.Top, Bottom = s.Bottom, HorizontalModulus = s.HorizontalModulus, AtRestCoefficient = s.AtRestCoefficient, Drainage = s.Drainage, StiffnessSource = s.StiffnessSource }).ToArray();
}
