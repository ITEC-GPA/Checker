using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One physical bolted welded-I double-cover splice, prepared once: geometry, materials,
    /// hardware, code, plate policy and exposure, without actions. Every case created here shares the
    /// demand-independent results (bolt groups, strengths, bearing resistances, section and block
    /// data, identifiers); each evaluation only adds its own concomitant N, V, M. Results are
    /// bit-identical to independently built inputs. Immutable and safe to share between threads.</summary>
    public sealed class PreparedWeldedIBeamSplice
    {
        private readonly WeldedIBeamSpliceDetailInput physical;
        private PreparedWeldedIBeamSplice(WeldedIBeamSpliceDetailInput physical)
        {
            this.physical = physical;
            // Publish the shared preparations before any case is created from them.
            _ = physical.Prepared; _ = physical.Plates.Prepared; _ = physical.Plates.Bolts.Prepared; _ = physical.Plates.Bolts.Detail.Geometry;
        }

        /// <summary>The physical detail of <paramref name="template"/>; its actions and combination
        /// source are not part of the preparation and are never reused for a case.</summary>
        public static PreparedWeldedIBeamSplice Prepare(WeldedIBeamSpliceDetailInput template)
            => new PreparedWeldedIBeamSplice(template ?? throw new ArgumentNullException(nameof(template)));

        /// <summary>The template detail with the welded section read from the member model.</summary>
        internal static PreparedWeldedIBeamSplice Prepare(WeldedIBeamSpliceDetailInput template, WeldedIBeamSpliceSection section)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            var b = template.Plates.Bolts; var d = b.Detail;
            var geometry = new WeldedIBeamSpliceForceInput(section, d.Top, d.Bottom, d.EachWebCover, d.WebBolts, d.WebCoverEnd, d.ButtGap, d.Actions, d.Packing);
            var bolts = new WeldedIBeamSpliceBoltInput(b.Code, geometry, b.FlangeBolt, b.WebBolt,
                b.FlangeHardwareDiameter, b.WebHardwareDiameter, b.UltimateCombinationSource, b.HoleClearanceServiceabilitySource);
            return new PreparedWeldedIBeamSplice(new WeldedIBeamSpliceDetailInput(new WeldedIBeamSplicePlateInput(template.Plates.Policy, bolts), template.Exposure));
        }

        /// <summary>The prepared physical detail (its own actions are only a placeholder).</summary>
        public WeldedIBeamSpliceDetailInput Detail => physical;

        /// <summary>One concomitant ULS/SLS action state at the documented splice pole, on this prepared detail.</summary>
        public WeldedIBeamSpliceDetailInput CreateCase(PlanarConnectionActions actions, string ultimateCombinationSource)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            var forces = new WeldedIBeamSpliceForceInput(physical.Plates.Bolts.Detail, actions);
            var bolts = new WeldedIBeamSpliceBoltInput(physical.Plates.Bolts, forces, ultimateCombinationSource);
            return new WeldedIBeamSpliceDetailInput(physical, new WeldedIBeamSplicePlateInput(physical.Plates, bolts));
        }

        /// <summary>True when <paramref name="input"/> is an action state of this prepared detail (reference identity).</summary>
        public bool Owns(WeldedIBeamSpliceDetailInput input) => input != null && physical.SharesPreparation(input);
    }

    /// <summary>Structural identity of physical splice parts. Inputs built from one prepared detail are
    /// identified by reference first; separately constructed inputs fall back to these value
    /// comparisons, so a mismatched detail is still rejected. One implementation for both I-splice families.</summary>
    internal static class SpliceIdentity
    {
        internal static bool Plate(ConnectionPlate a, ConnectionPlate b)
            => ReferenceEquals(a, b) || a.Width == b.Width && a.Thickness == b.Thickness && a.Fy == b.Fy && a.Fu == b.Fu;
        internal static bool Bolt(ConnectionBolt a, ConnectionBolt b)
            => ReferenceEquals(a, b) || a.Diameter == b.Diameter && a.HoleDiameter == b.HoleDiameter && a.TensileArea == b.TensileArea
                && a.Grade == b.Grade && a.ThreadedShearPlanes == b.ThreadedShearPlanes;
        /// <summary>Standard and the three partial factors (IsNtc is the standard: two values exist).</summary>
        internal static bool Code(ConnectionDesignCode a, ConnectionDesignCode b)
            => ReferenceEquals(a, b) || a.Plates.Standard == b.Plates.Standard && a.Plates.GammaM0 == b.Plates.GammaM0
                && a.Plates.GammaM2 == b.Plates.GammaM2 && a.GammaM2Connection == b.GammaM2Connection;
        /// <summary>With <paramref name="pitchOnlyIfRepeated"/> a pitch without a second bolt in that direction is not compared.</summary>
        internal static bool Grid(SpliceBoltLayout a, SpliceBoltLayout b, bool pitchOnlyIfRepeated)
            => ReferenceEquals(a, b) || a.LongitudinalRows == b.LongitudinalRows && a.TransverseRows == b.TransverseRows
                && (pitchOnlyIfRepeated && a.LongitudinalRows == 1 || a.LongitudinalPitch == b.LongitudinalPitch)
                && (pitchOnlyIfRepeated && a.TransverseRows == 1 || a.TransversePitch == b.TransversePitch)
                && a.FirstBoltDistance == b.FirstBoltDistance;
        internal static bool Actions(PlanarConnectionActions a, PlanarConnectionActions b)
            => ReferenceEquals(a, b) || a.Fx == b.Fx && a.Fy == b.Fy && a.Fz == b.Fz && a.Mx == b.Mx && a.My == b.My && a.Mz == b.Mz
                && a.ApplicationX == b.ApplicationX && a.ApplicationY == b.ApplicationY && a.ApplicationZ == b.ApplicationZ;
        internal static bool Flange(SpliceFlangeCoverDetail a, SpliceFlangeCoverDetail b)
            => ReferenceEquals(a, b) || Plate(a.Outer, b.Outer) && Plate(a.EachInner, b.EachInner) && a.InnerGap == b.InnerGap
                && a.EndDistance == b.EndDistance && Grid(a.Bolts, b.Bolts, true);

        /// <summary>Same physical welded-I bolt detail and code: reference identity of the shared
        /// preparation, otherwise every geometric, material and hardware value.</summary>
        internal static bool WeldedPhysical(WeldedIBeamSpliceBoltInput a, WeldedIBeamSpliceBoltInput b)
        {
            if (a.SharesPreparation(b)) return true;
            var x = a.Detail; var y = b.Detail;
            return Code(a.Code, b.Code) && Bolt(a.FlangeBolt, b.FlangeBolt) && Bolt(a.WebBolt, b.WebBolt)
                && a.FlangeHardwareDiameter == b.FlangeHardwareDiameter && a.WebHardwareDiameter == b.WebHardwareDiameter
                && Plate(x.Section.TopFlange, y.Section.TopFlange) && Plate(x.Section.Web, y.Section.Web) && Plate(x.Section.BottomFlange, y.Section.BottomFlange)
                && x.Section.TopWeldToe == y.Section.TopWeldToe && x.Section.BottomWeldToe == y.Section.BottomWeldToe
                && Flange(x.Top, y.Top) && Flange(x.Bottom, y.Bottom) && Plate(x.EachWebCover, y.EachWebCover) && Grid(x.WebBolts, y.WebBolts, true)
                && x.WebCoverEnd == y.WebCoverEnd && x.ButtGap == y.ButtGap && x.Packing.SamePhysical(y.Packing);
        }

        internal static bool IBeamGeometry(IBeamSpliceForceInput a, IBeamSpliceForceInput b)
        {
            if (ReferenceEquals(a.Section, b.Section) && a.StrongAxisSecondMoment == b.StrongAxisSecondMoment
                && ReferenceEquals(a.FlangeBolts, b.FlangeBolts) && ReferenceEquals(a.WebBolts, b.WebBolts)) return true;
            var x = a.Section; var y = b.Section;
            return a.StrongAxisSecondMoment == b.StrongAxisSecondMoment && x.Height == y.Height && x.Width == y.Width
                && x.WebThickness == y.WebThickness && x.FlangeThickness == y.FlangeThickness && x.RootRadius == y.RootRadius
                && x.Area == y.Area && x.Fy == y.Fy && x.Fu == y.Fu
                && Grid(a.FlangeBolts, b.FlangeBolts, false) && Grid(a.WebBolts, b.WebBolts, false);
        }
        internal static bool IBeamBolts(IBeamSpliceBoltSet a, IBeamSpliceBoltSet b)
            => ReferenceEquals(a, b) || Bolt(a.FlangeBolt, b.FlangeBolt) && Bolt(a.WebBolt, b.WebBolt)
                && a.FlangeHardwareEnvelopeDiameter == b.FlangeHardwareEnvelopeDiameter
                && a.WebHardwareEnvelopeDiameter == b.WebHardwareEnvelopeDiameter;
        internal static bool Welds(SpliceLongitudinalFillets a, SpliceLongitudinalFillets b)
            => ReferenceEquals(a, b) || a.TopThroat == b.TopThroat && a.BottomThroat == b.BottomThroat && a.EffectiveLengthEachSide == b.EffectiveLengthEachSide
                && a.TopGrade == b.TopGrade && a.WebGrade == b.WebGrade && a.BottomGrade == b.BottomGrade;
    }

    /// <summary>Demand-independent detailing and standard-plate data of one physical detail.</summary>
    internal sealed class SpliceDetailPreparation
    {
        private readonly Lazy<StandardPlatePreparation> standardPlates;
        /// <summary>Built on first use, when the force groups of this geometry are known to be supported.</summary>
        internal StandardPlatePreparation StandardPlates => standardPlates.Value;
        internal SpliceDetailPreparation(WeldedIBeamSpliceDetailInput p)
        {
            standardPlates = new Lazy<StandardPlatePreparation>(() => new StandardPlatePreparation(p), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
