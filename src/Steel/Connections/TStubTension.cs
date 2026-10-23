using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum TStubMode1Method { PointForce, DistributedContact }
    public enum TStubPryingModel { Develops, FromBoltElongation }

    /// <summary>Total effective lengths for the two plastic mechanisms, not physical plate lengths.
    /// The caller derives them for the actual single row or adjacent-row group using EN1993-1-8
    /// 6.2.6 and identifies that derivation in Source. All lengths are in mm.</summary>
    public sealed class TStubEffectiveLengths
    {
        public double Mode1 { get; }
        public double Mode2 { get; }
        public string Source { get; }
        public TStubEffectiveLengths(double mode1, double mode2, string source)
        {
            ConnectionValidation.Positive(mode1, nameof(mode1));
            ConnectionValidation.Positive(mode2, nameof(mode2));
            if (mode1 > mode2) throw new ArgumentException("Mode 1 length cannot exceed the non-circular Mode 2 length.");
            Mode1 = mode1; Mode2 = mode2; Source = source;
        }
    }

    /// <summary>Actual steel-bolt elongation length: grip including washers + half head/nut heights.
    /// Anchors, grout and foundation bolts are outside this contract. Dimensions in mm.</summary>
    public sealed class TStubBoltElongation
    {
        public double GripLength { get; }
        public double HeadHeight { get; }
        public double NutHeight { get; }
        public double Length => GripLength + .5 * HeadHeight + .5 * NutHeight;
        public string Source { get; }
        public TStubBoltElongation(double gripLength, double headHeight, double nutHeight, string source)
        {
            ConnectionValidation.Positive(gripLength, nameof(gripLength));
            ConnectionValidation.Positive(headHeight, nameof(headHeight));
            ConnectionValidation.Positive(nutHeight, nameof(nutHeight));
            GripLength = gripLength; HeadHeight = headHeight; NutHeight = nutHeight; Source = source;
            ConnectionValidation.Positive(Length, nameof(Length));
        }
    }

    /// <summary>One symmetric, unbacked equivalent T-stub, with two identical bolts per row.
    /// Positive axial tension N; geometry mm; yield strength MPa at the actual flange thickness.
    /// M is the Figure6.2 plastic-hinge-to-bolt distance; Emin refers to both connected sides.
    /// No load redistribution between rows is inferred. No shear or bolt punching is included.</summary>
    public sealed class TStubTensionInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionBolt Bolt { get; }
        public double FlangeThickness { get; }
        public double FlangeYieldStrength { get; }
        public double M { get; }
        public double Emin { get; }
        public int BoltRows { get; }
        public TStubEffectiveLengths EffectiveLengths { get; }
        public double TensionForce { get; }
        public TStubMode1Method Method { get; }
        public double ContactWidth { get; }
        public TStubPryingModel PryingModel { get; }
        public TStubBoltElongation BoltElongation { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public TStubTensionInput(ConnectionDesignCode code, ConnectionBolt bolt,
            double flangeThickness, double flangeYieldStrength, double m, double emin,
            int boltRows, TStubEffectiveLengths effectiveLengths, double tensionForce,
            TStubMode1Method method = TStubMode1Method.PointForce, double contactWidth = 0,
            TStubPryingModel pryingModel = TStubPryingModel.Develops,
            TStubBoltElongation boltElongation = null, string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            EffectiveLengths = effectiveLengths ?? throw new ArgumentNullException(nameof(effectiveLengths));
            ConnectionValidation.Positive(flangeThickness, nameof(flangeThickness));
            ConnectionValidation.Positive(flangeYieldStrength, nameof(flangeYieldStrength));
            ConnectionValidation.Positive(m, nameof(m)); ConnectionValidation.Positive(emin, nameof(emin));
            ConnectionValidation.Nonnegative(tensionForce, nameof(tensionForce));
            ConnectionValidation.Nonnegative(contactWidth, nameof(contactWidth));
            if (boltRows < 1 || boltRows > 20) throw new ArgumentOutOfRangeException(nameof(boltRows));
            if (!Enum.IsDefined(typeof(TStubMode1Method), method)) throw new ArgumentOutOfRangeException(nameof(method));
            if (!Enum.IsDefined(typeof(TStubPryingModel), pryingModel)) throw new ArgumentOutOfRangeException(nameof(pryingModel));
            FlangeThickness = flangeThickness; FlangeYieldStrength = flangeYieldStrength;
            M = m; Emin = emin; BoltRows = boltRows; TensionForce = tensionForce;
            Method = method; ContactWidth = contactWidth; PryingModel = pryingModel;
            BoltElongation = boltElongation; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    public static class TStubTension
    {
        public static ConnectionVerification<TStubTensionInput> Verify(TStubTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>();
            string reference = "EN1993-1-8:2005 6.2.4, Table6.2 (including AC2)";
            if (p.Code.IsNtc) reference += "; supplementary component model with NTC2018 factors; bolt tension 4.2.68";
            if (string.IsNullOrWhiteSpace(p.EffectiveLengths.Source)) issues.Add("EffectiveLengthDerivationRequired");
            if (p.FlangeYieldStrength > 460) issues.Add("CarbonSteelUpToS460Only");
            if (p.M <= p.Bolt.HoleDiameter / 2) issues.Add("BoltHoleIntersectsPlasticHingeLine");
            if (p.Emin < 1.2 * p.Bolt.HoleDiameter) issues.Add("MinimumEdgeDistance");
            if (p.Method == TStubMode1Method.DistributedContact
                && (p.ContactWidth < p.Bolt.HoleDiameter || p.ContactWidth >= 2 * Math.Min(p.M, p.Emin)))
                issues.Add("DistributedContactFootprintOutsideQualifiedGeometry");
            if (p.Method == TStubMode1Method.PointForce && p.ContactWidth != 0)
                issues.Add("ContactWidthRequiresDistributedContactMethod");
            if (p.PryingModel == TStubPryingModel.FromBoltElongation && p.BoltElongation == null)
                issues.Add("ActualBoltElongationRequired");
            if (p.BoltElongation != null)
            {
                if (string.IsNullOrWhiteSpace(p.BoltElongation.Source)) issues.Add("BoltElongationDerivationRequired");
                if (p.BoltElongation.GripLength < p.FlangeThickness) issues.Add("GripShorterThanTStubFlange");
            }
            var bolt = BoltStrength.Verify(new BoltStrengthInput(p.Code, p.Bolt, 0, 0,
                holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource));
            issues.AddRange(bolt.Diagnostics.Select(d => "Bolt:" + d));
            if (issues.Count != 0) return Result();

            double n = Math.Min(p.Emin, 1.25 * p.M), t = p.FlangeThickness;
            double moment1 = .25 * p.EffectiveLengths.Mode1 * t * t * p.FlangeYieldStrength / p.Code.Plates.GammaM0;
            double moment2 = .25 * p.EffectiveLengths.Mode2 * t * t * p.FlangeYieldStrength / p.Code.Plates.GammaM0;
            double totalBolt = 2 * p.BoltRows * bolt.Checks.Single(c => c.Id == "Bolt.Tension").Resistance;
            // Corrected Table6.2 uses n_b = number of rows, NOT number of bolts.
            double lengthLimit = 8.8 * p.M * p.M * p.M * p.Bolt.TensileArea * p.BoltRows
                / (p.EffectiveLengths.Mode1 * t * t * t);
            if (!(lengthLimit > 0)) issues.Add("PryingLengthLimitOutsideNumericRange");
            bool prying = p.PryingModel == TStubPryingModel.Develops || p.BoltElongation.Length <= lengthLimit;
            Value("M", p.M, "mm"); Value("Emin", p.Emin, "mm"); Value("N", n, "mm");
            Value("EffectiveLengthMode1", p.EffectiveLengths.Mode1, "mm");
            Value("EffectiveLengthMode2", p.EffectiveLengths.Mode2, "mm");
            Value("PlasticMomentMode1", moment1, "Nmm"); Value("PlasticMomentMode2", moment2, "Nmm");
            Value("BoltRows", p.BoltRows, "1"); Value("BoltCount", 2 * p.BoltRows, "1");
            Value("TotalBoltTensionResistance", totalBolt, "N");
            Value("PryingLengthLimit", lengthLimit, "mm"); Value("PryingDevelops", prying ? 1 : 0, "1");
            Value("GammaM0", p.Code.Plates.GammaM0, "1"); Value("GammaM2Connection", p.Code.GammaM2Connection, "1");
            if (p.BoltElongation != null) Value("BoltElongationLength", p.BoltElongation.Length, "mm");
            if (prying)
            {
                double ew = p.ContactWidth / 4;
                double mode1 = p.Method == TStubMode1Method.PointForce ? 4 * moment1 / p.M
                    : (8 * n - 2 * ew) * moment1 / (2 * p.M * n - ew * (p.M + n));
                double mode2 = (2 * moment2 + n * totalBolt) / (p.M + n);
                Value("ContactEw", ew, "mm");
                Check("TStub.Mode1", mode1); Check("TStub.Mode2", mode2); Check("TStub.Mode3", totalBolt);
            }
            else
            {
                Check("TStub.NoPryingPlate", 2 * moment1 / p.M);
                Check("TStub.Mode3", totalBolt);
            }
            return Result();

            void Value(string name, double value, string unit)
            {
                if (double.IsInfinity(value) || double.IsNaN(value) || value < 0) issues.Add(name + "OutsideNumericRange");
                else trace.Add(new SectionResistanceValue(name, value, unit));
            }
            void Check(string name, double resistance)
            {
                if (!(resistance > 0) || double.IsInfinity(resistance)) issues.Add(name + "OutsideNumericRange");
                else checks.Add(new ConnectionCheck(name, p.TensionForce, resistance, reference));
            }
            ConnectionVerification<TStubTensionInput> Result() => new ConnectionVerification<TStubTensionInput>(
                p, p.Code, "Steel.Connection.TStub.Tension",
                "Unbacked symmetric equivalent T-stub in tension, two identical bolts per row. "
                + "Only flange bending and bolt tensile failure; not a whole joint, row-force allocation, punching, shear, weld, web or stiffness check.",
                checks, trace, issues, new[] {
                    "Effective lengths are externally derived for this actual row/group: " + p.EffectiveLengths.Source,
                    "Prying is implicit in Table6.2 resistance modes; the result does not provide individual bolt forces or a service-load Q.",
                    "The Develops option invokes Table6.2 note1 for bolted beam-to-column joints/beam splices. Other applications require the actual bolt-elongation option.",
                    "Distributed-contact width is the actual washer diameter or head/nut width across points applicable on this flange; footprint, material bearing and punching remain caller checks.",
                    "Flange yield strength is supplied for the actual thickness. No backing plates, anchors, fatigue, seismic/cyclic loading or countersunk bolts are included.",
                    "For NTC2018 the T-stub model supplements the national standard with EN1993-1-8; it is not presented as an NTC T-stub formula." });
        }
    }
}
