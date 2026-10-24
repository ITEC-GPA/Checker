using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Masonry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;

namespace GPC.Design.Walls;

public enum GravityWallMaterialKind { Assigned, PlainConcrete, Masonry }
public enum GravityCheckMethod { ElasticStability, AssignedCompression, NoTension, AssignedFoundationFlexure, AssignedShear,
    CompressedBlock, MasonryJointShear, PlainConcreteShear, FoundationCompression, FoundationTension, FoundationShear, Contact }

/// <summary>Material projection used by the coordinator; constituent formulae remain in their checkers.</summary>
public sealed class GravityWallMaterial
{
    public GravityWallMaterialKind Kind { get; }
    public double Compression { get; }
    public double Tension { get; }
    /// <summary>MPa; zero means unavailable, only for the assigned-strength model which never uses stiffness.</summary>
    public double EffectiveModulus { get; }
    public bool HasStiffness => EffectiveModulus > 0;
    public double FoundationShear { get; }
    readonly Func<double, double> shearStress;
    GravityWallMaterial(GravityWallMaterialKind kind, double compression, double tension, double modulus, double foundationShear, Func<double, double> shear)
    {
        if (!PortableMath.IsFinite(compression) || compression <= 0 || !PortableMath.IsFinite(tension) || tension < 0 || !PortableMath.IsFinite(modulus) || modulus < 0 || modulus == 0 && kind != GravityWallMaterialKind.Assigned
            || !PortableMath.IsFinite(foundationShear) || foundationShear < 0) throw new ArgumentOutOfRangeException(nameof(compression));
        Kind = kind; Compression = compression; Tension = tension; EffectiveModulus = modulus; FoundationShear = foundationShear; shearStress = shear;
    }
    public static GravityWallMaterial Assigned(double compression, double tension, double modulus, double shear)
    {
        if (!PortableMath.IsFinite(modulus) || modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus));
        return new(GravityWallMaterialKind.Assigned, compression, tension, modulus, shear, _ => shear);
    }
    /// <summary>Assigned strengths without an elastic modulus. No stiffness, stability or curvature is inferred.</summary>
    public static GravityWallMaterial Assigned(double compression, double tension, double shear)
        => new(GravityWallMaterialKind.Assigned, compression, tension, 0, shear, _ => shear);
    public static GravityWallMaterial From(PlainConcreteWallMaterial material)
    {
        Guard.NotNull(material);
        return new(GravityWallMaterialKind.PlainConcrete, material.Compression, material.Tension, material.EffectiveModulus, material.Tension, material.ShearStress);
    }
    public static GravityWallMaterial From(MasonryWallMaterial material)
    {
        Guard.NotNull(material);
        return new(GravityWallMaterialKind.Masonry, material.Compression, 0, material.EffectiveModulus, material.FoundationShear, material.ShearStress);
    }
    internal double ShearStress(double sigma) => shearStress(sigma);
}

/// <summary>Per unit wall width: position/thickness in mm, N and V in N/mm, M in Nmm/mm; N positive in compression.</summary>
public sealed record GravitySection(WallMember Member, double Position, double Thickness, double N, double V, double M);
public sealed record GravityCombination(string Id, bool Serviceability, bool ContactValid, IReadOnlyList<GravitySection> Sections);
public sealed record GravityCurvature(double Height, double Curvature);
public sealed record GravityCheck(GravityCheckMethod Method, string Combination, WallMember? Member, double? Position, double Demand, double? Capacity, string Unit)
{
    public double? Ratio => Capacity is > 0 ? Math.Abs(Demand) / Capacity : Capacity == 0 && Demand == 0 ? 0 : null;
    public EngineeringOutcome Outcome => Capacity is null or < 0 ? EngineeringOutcome.NotEvaluated
        : Capacity == 0 ? Demand == 0 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied
        : Ratio <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
    public ExecutionStatus Execution => ExecutionStatus.Completed;
    public DataStatus Data => DataStatus.Ready;
}
public sealed record GravityCombinationResult(string Id, IReadOnlyList<GravityCheck> Checks, IReadOnlyList<GravityCurvature> Curvatures);

/// <summary>Limited gravity-wall orchestration inherited from ANTHEA. mm/N/MPa; no JSON or UI dependency.
/// Elastic magnification and compressed block apply only to stable, uncracked cantilevers. Assigned strengths retain their explicit independent policy.</summary>
public static class GravityWallAnalysis
{
    public static IReadOnlyList<GravityCombinationResult> Calculate(GravityWallMaterial material, double height, double topThickness,
        double effectiveHeight, double initialEccentricity, IEnumerable<GravityCombination> combinations, CancellationToken cancellation = default)
    {
        Guard.NotNull(material); Guard.NotNull(combinations);
        if (!PortableMath.IsFinite(height) || height <= 0 || !PortableMath.IsFinite(topThickness) || topThickness <= 0
            || !PortableMath.IsFinite(effectiveHeight) || effectiveHeight < 2 * height || !PortableMath.IsFinite(initialEccentricity) || initialEccentricity < 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Positive dimensions, effective height >= 2H and nonnegative eccentricity required.");
        // Snapshot all caller-owned lists before solving.
        var cases = combinations.Select(c => c with { Sections = Array.AsReadOnly(c.Sections.ToArray()) }).ToArray();
        if (cases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != cases.Length) throw new ArgumentException("Unique combination IDs required.");
        foreach (var c in cases)
        {
            if (string.IsNullOrWhiteSpace(c.Id) || c.Sections.Count == 0) throw new ArgumentException("Combination identity and sections required.");
            foreach (var f in c.Sections)
                if (!PortableEnum.IsDefined(f.Member) || !PortableMath.IsFinite(f.Position) || !PortableMath.IsFinite(f.Thickness) || f.Thickness <= 0
                    || !PortableMath.IsFinite(f.N) || !PortableMath.IsFinite(f.V) || !PortableMath.IsFinite(f.M)) throw new ArgumentException("Invalid section actions or geometry.");
            if (material.Kind != GravityWallMaterialKind.Assigned && !c.Sections.Any(f => f.Member == WallMember.Stem)) throw new ArgumentException("Stem sections required for stability.");
        }
        double inertia = Math.Pow(topThickness, 3) / 12;
        bool assigned = material.Kind == GravityWallMaterialKind.Assigned;
        double ncr = assigned ? 0 : Math.PI * Math.PI * material.EffectiveModulus * inertia / (effectiveHeight * effectiveHeight);
        if (!assigned && (!PortableMath.IsFinite(ncr) || ncr <= 0)) throw new ArgumentOutOfRangeException(nameof(topThickness), "Stability dimensions exceed the numeric range.");
        var results = new List<GravityCombinationResult>();
        foreach (var c in cases)
        {
            cancellation.ThrowIfCancellationRequested();
            var checks = new List<GravityCheck>(); var curvatures = new List<GravityCurvature>();
            double maxN = c.Sections.Where(f => f.Member == WallMember.Stem).Select(f => f.N).DefaultIfEmpty().Max();
            bool stable = assigned || maxN < .8 * ncr;
            double amplification = !assigned && stable ? 1 / (1 - Math.Max(0, maxN) / ncr) : 1;
            if (!assigned && !c.Serviceability) checks.Add(new(GravityCheckMethod.ElasticStability, c.Id, null, null, Math.Max(0, maxN), .8 * ncr, "N/mm"));
            foreach (var f in c.Sections)
            {
                cancellation.ThrowIfCancellationRequested();
                if (assigned && c.Serviceability) continue;
                bool stem = f.Member == WallMember.Stem;
                void Add(GravityCheckMethod method, double demand, double? capacity, string unit)
                {
                    if (!PortableMath.IsFinite(demand) || capacity.HasValue && !PortableMath.IsFinite(capacity.Value)) throw new ArithmeticException("Gravity section exceeds the numeric range.");
                    checks.Add(new(method, c.Id, f.Member, f.Position, Math.Abs(demand), capacity, unit));
                }
                if (!stem && !c.ContactValid && (assigned || f.N != 0 || f.V != 0 || f.M != 0)) { Add(GravityCheckMethod.Contact, 0, null, "MPa"); continue; }
                if (f.N == 0 && f.V == 0 && f.M == 0 && (!assigned || f.Position <= 0)) continue;
                double width = f.Thickness;
                double e0 = stem && !assigned ? Math.Max(height / 200, initialEccentricity) : 0;
                double moment = (Math.Abs(f.M) + Math.Max(0, f.N) * e0) * (stem && !assigned ? amplification : 1);
                double elasticC = f.N / width + 6 * moment / (width * width);
                double elasticT = Math.Max(0, 6 * moment / (width * width) - f.N / width);
                if (assigned)
                {
                    Add(GravityCheckMethod.AssignedCompression, elasticC, material.Compression, "MPa");
                    if (stem) Add(GravityCheckMethod.NoTension, moment, f.N * width / 6, "Nmm/mm");
                    else Add(GravityCheckMethod.AssignedFoundationFlexure, elasticC, material.Tension, "MPa");
                    Add(GravityCheckMethod.AssignedShear, 1.5 * Math.Abs(f.V) / width, material.FoundationShear, "MPa");
                    continue;
                }
                bool uncracked = f.N > 0 && moment <= f.N * width / 6;
                if (c.Serviceability)
                {
                    if (stem && uncracked && stable) curvatures.Add(new(height - f.Position, Math.Sign(f.M) * moment / (material.EffectiveModulus * Math.Pow(width, 3) / 12)));
                    continue;
                }
                if (stem)
                {
                    Add(GravityCheckMethod.NoTension, moment, Math.Max(0, f.N * width / 6), "Nmm/mm");
                    double compressed = f.N > 0 ? Math.Max(0, width - 2 * moment / f.N) : 0;
                    Add(GravityCheckMethod.CompressedBlock, Math.Max(0, f.N), stable && uncracked ? material.Compression * compressed : null, "N/mm");
                    double sigma = compressed > 0 ? Math.Max(0, f.N) / compressed : 0;
                    bool masonry = material.Kind == GravityWallMaterialKind.Masonry;
                    Add(masonry ? GravityCheckMethod.MasonryJointShear : GravityCheckMethod.PlainConcreteShear, Math.Abs(f.V),
                        stable && uncracked ? material.ShearStress(sigma) * compressed / (masonry ? 1 : 1.5) : null, "N/mm");
                }
                else
                {
                    Add(GravityCheckMethod.FoundationCompression, elasticC, material.Compression, "MPa");
                    Add(GravityCheckMethod.FoundationTension, elasticT, material.Tension, "MPa");
                    Add(GravityCheckMethod.FoundationShear, 1.5 * Math.Abs(f.V) / width, material.FoundationShear, "MPa");
                }
            }
            results.Add(new(c.Id, checks.AsReadOnly(), curvatures.AsReadOnly()));
        }
        cancellation.ThrowIfCancellationRequested(); return results.AsReadOnly();
    }
}
