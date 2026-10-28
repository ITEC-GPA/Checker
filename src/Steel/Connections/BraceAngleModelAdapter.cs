using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Immutable adapter output for one physical angle. Section points are in the
    /// Model geometry's X/Y coordinates, not principal axes and not the connection plane.
    /// Member tension acts along +Z. The moments at the bolt line transport that axial
    /// resultant from the angle centroid: they remain demands on the connection, not
    /// resistances verified by the effective-net-section calculation.</summary>
    public sealed class BraceAngleModelComponent
    {
        public int PartIndex { get; }
        public BraceAngleTensionInput Input { get; }
        public ConnectionPoint2D Centroid { get; }
        public ConnectionPoint2D BoltLine { get; }
        public double MomentAtBoltLineAboutSectionX { get; }
        public double MomentAtBoltLineAboutSectionY { get; }
        internal BraceAngleModelComponent(int index, BraceAngleTensionInput input,
            ConnectionPoint2D centroid, ConnectionPoint2D boltLine)
        {
            PartIndex = index; Input = input; Centroid = centroid; BoltLine = boltLine;
            MomentAtBoltLineAboutSectionX = (centroid.Y - boltLine.Y) * input.Tension;
            MomentAtBoltLineAboutSectionY = -(centroid.X - boltLine.X) * input.Tension;
            ConnectionNumbers.Finite(MomentAtBoltLineAboutSectionX, nameof(MomentAtBoltLineAboutSectionX));
            ConnectionNumbers.Finite(MomentAtBoltLineAboutSectionY, nameof(MomentAtBoltLineAboutSectionY));
        }
    }

    /// <summary>Model-to-connection snapshot, containing one angle or two identical angles.
    /// This is an input adaptation and equilibrium check, never a successful joint assessment.
    /// The caller evaluates each Input with BraceAngleTension and retains the eccentric
    /// bolt-line moments for the remaining connection and support checks.</summary>
    public sealed class BraceAngleModelAdapterResult
    {
        public SteelSectionInput SourceSection { get; }
        public SteelActions SourceActions { get; }
        public ConnectionPoint2D SectionCentroid { get; }
        public IReadOnlyList<BraceAngleModelComponent> Components { get; }
        public double? ClearBackGap { get; }
        public string ForceDistributionSource { get; }
        public bool IsPair => Components.Count == 2;
        public string Scope => "Model angle geometry/material and explicit axial force adaptation only. "
            + "No bolt, gusset, packing, support, stiffness or complete-joint qualification; no C-section equivalence.";
        internal BraceAngleModelAdapterResult(SteelSectionInput section, SteelActions actions,
            ConnectionPoint2D centroid, IEnumerable<BraceAngleModelComponent> components,
            double? gap, string distributionSource)
        {
            SourceSection = section; SourceActions = actions; SectionCentroid = centroid;
            Components = Array.AsReadOnly(components.ToArray()); ClearBackGap = gap;
            ForceDistributionSource = distributionSource;
        }
    }

    /// <summary>Uses the existing immutable SteelSectionInput and real Model SectionL/SectionBuiltUp.
    /// First means the horizontal leg of SectionL; Second means its vertical leg.
    /// Their identity is not changed by AngleX1 or by reflection of a built-up component.
    /// All dimensions remain mm and all actions N/Nmm, already factored and concomitant.</summary>
    internal static class BraceAngleModelAdapter
    {
        /// <summary>Adapts pure centroidal tension of a single actual L. The equivalent
        /// equal-short-leg Model section is optional only where 3.10.3 does not require it.
        /// Its material is not substituted for the actual member material.</summary>
        public static BraceAngleModelAdapterResult FromSingle(SteelSectionInput section,
            ConnectionDesignCode code, SteelActions actions, BraceAngleConnectedLeg connectedLeg,
            ConnectionBolt bolt, int boltCount, double pitch, double toeEdgeDistance,
            SteelSectionInput equivalentShortLegSection = null, string equivalentSectionSource = null)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (bolt == null) throw new ArgumentNullException(nameof(bolt));
            var property = section.ReadOnlySection;
            var angle = Angle(property);
            if (actions.N < 0 || actions.V1 != 0 || actions.V2 != 0 || actions.T != 0 || actions.M1 != 0 || actions.M2 != 0)
                throw new NotSupportedException("A single-angle adapter requires pure centroidal tension; nonzero member actions must not be discarded.");
            CheckNetDeclaration(section, angle.Area - bolt.HoleDiameter * angle.HorizontalLegThickness);
            var input = CreateInput(property, angle, code, connectedLeg, bolt, actions.N,
                boltCount, pitch, toeEdgeDistance, equivalentShortLegSection, equivalentSectionSource);
            var centroid = Point(angle.Centroid.X, angle.Centroid.Y);
            var boltLine = LocalBoltLine(angle, connectedLeg, toeEdgeDistance);
            var component = new BraceAngleModelComponent(0, input, centroid, boltLine);
            return new BraceAngleModelAdapterResult(section, actions, centroid, new[] { component }, null,
                "Single physical angle; the supplied centroidal axial force is preserved.");
        }

        /// <summary>Adapts two identical reflected angles joined on their facing backs.
        /// Both chord tensions and their derivation are mandatory. They must reconstruct
        /// all six supplied pair actions at its centroid: N and bending due to unequal
        /// chord forces are checked after rotating principal moments to Model X/Y.
        /// No automatic N/2 split or composite-action qualification is introduced.</summary>
        public static BraceAngleModelAdapterResult FromPair(SteelSectionInput section,
            ConnectionDesignCode code, SteelActions actions, double firstTension, double secondTension,
            string forceDistributionSource, ConnectionBolt bolt, int boltCount, double pitch,
            double toeEdgeDistance, SteelSectionInput equivalentShortLegSection = null,
            string equivalentSectionSource = null)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (bolt == null) throw new ArgumentNullException(nameof(bolt));
            ConnectionValidation.Nonnegative(firstTension, nameof(firstTension));
            ConnectionValidation.Nonnegative(secondTension, nameof(secondTension));
            if (string.IsNullOrWhiteSpace(forceDistributionSource))
                throw new ArgumentException("An explicit concomitant chord-force distribution source is required.", nameof(forceDistributionSource));
            var property = section.ReadOnlySection;
            Manufacturing(property);
            if (property.SectionShape.GetType() != typeof(SectionBuiltUp))
                throw new NotSupportedException("An exact Model SectionBuiltUp pair is required.");
            var pair = (SectionBuiltUp)property.SectionShape;
            ModelBuiltUpGeometry.Create(pair);
            if (pair.Parts[0].Section.GetType() != typeof(SectionL))
                throw new NotSupportedException("This adapter only qualifies L components, not C sections.");
            var angle = (SectionL)pair.Parts[0].Section;
            UniformAngle(angle);
            var p = pair.Parts[0]; var q = pair.Parts[1];
            bool verticalLegs = p.MirrorX != q.MirrorX && p.MirrorY == q.MirrorY && p.Y == q.Y;
            bool horizontalLegs = p.MirrorY != q.MirrorY && p.MirrorX == q.MirrorX && p.X == q.X;
            if (!verticalLegs && !horizontalLegs)
                throw new NotSupportedException("The identical angles must have aligned facing backs.");
            var pp = p.Place(0, 0); var qp = q.Place(0, 0);
            double backDifference = verticalLegs ? qp.X - pp.X : qp.Y - pp.Y;
            double gap = Math.Abs(backDifference);
            int firstInterior = (verticalLegs ? p.MirrorX : p.MirrorY) ? -1 : 1;
            int secondInterior = (verticalLegs ? q.MirrorX : q.MirrorY) ? -1 : 1;
            if (gap > 0 && (firstInterior != -Math.Sign(backDifference) || secondInterior != Math.Sign(backDifference)))
                throw new NotSupportedException("Only back-to-back angles are supported; toe-to-toe placement is not equivalent.");
            if (!Close(pair.Area, 2 * angle.Area, 2 * angle.Area))
                throw new NotSupportedException("The assembled area is inconsistent with its identical components.");
            CheckNetDeclaration(section, 2 * (angle.Area - bolt.HoleDiameter * angle.HorizontalLegThickness));
            var cp = p.Place(angle.Centroid.X, angle.Centroid.Y);
            var cq = q.Place(angle.Centroid.X, angle.Centroid.Y);
            // Equal areas: use the direct mean to preserve exact symmetry in the aligned
            // coordinate. The area-weighted cached Model centroid can differ by one ulp;
            // that roundoff must not create an artificial bending demand on a symmetric pair.
            var centroid = Point(cp.X / 2 + cq.X / 2, cp.Y / 2 + cq.Y / 2);
            if (!Close(pair.Centroid.X, centroid.X, Math.Max(Math.Abs(cp.X), Math.Abs(cq.X)))
                || !Close(pair.Centroid.Y, centroid.Y, Math.Max(Math.Abs(cp.Y), Math.Abs(cq.Y))))
                throw new NotSupportedException("The assembled centroid is inconsistent with its identical components.");
            CheckPairEquilibrium(section, actions, firstTension, secondTension,
                Point(cp.X, cp.Y), Point(cq.X, cq.Y), centroid);
            var leg = verticalLegs ? BraceAngleConnectedLeg.Second : BraceAngleConnectedLeg.First;
            var localBolt = LocalBoltLine(angle, leg, toeEdgeDistance);
            var pb = p.Place(localBolt.X, localBolt.Y); var qb = q.Place(localBolt.X, localBolt.Y);
            var first = new BraceAngleModelComponent(0, CreateInput(property, angle, code, leg, bolt,
                firstTension, boltCount, pitch, toeEdgeDistance, equivalentShortLegSection, equivalentSectionSource),
                Point(cp.X, cp.Y), Point(pb.X, pb.Y));
            var second = new BraceAngleModelComponent(1, CreateInput(property, angle, code, leg, bolt,
                secondTension, boltCount, pitch, toeEdgeDistance, equivalentShortLegSection, equivalentSectionSource),
                Point(cq.X, cq.Y), Point(qb.X, qb.Y));
            return new BraceAngleModelAdapterResult(section, actions, centroid, new[] { first, second }, gap, forceDistributionSource);
        }

        private static BraceAngleTensionInput CreateInput(ISteelSection property, SectionL angle,
            ConnectionDesignCode code, BraceAngleConnectedLeg leg, ConnectionBolt bolt, double tension,
            int boltCount, double pitch, double edge, SteelSectionInput equivalentSection, string equivalentSource)
        {
            if (!Enum.IsDefined(typeof(BraceAngleConnectedLeg), leg)) throw new ArgumentOutOfRangeException(nameof(leg));
            var converted = new BraceAngleSection(angle.HorizontalLegLength, angle.VerticalLegLength,
                angle.HorizontalLegThickness, angle.Area, property.SteelMaterial.Fyk, property.SteelMaterial.Fu);
            double connected = leg == BraceAngleConnectedLeg.First ? angle.HorizontalLegLength : angle.VerticalLegLength;
            double other = leg == BraceAngleConnectedLeg.First ? angle.VerticalLegLength : angle.HorizontalLegLength;
            double? equivalentArea = null;
            if (equivalentSection != null)
            {
                var equivalentProperty = equivalentSection.ReadOnlySection; var equal = Angle(equivalentProperty);
                double shortLength = Math.Min(angle.HorizontalLegLength, angle.VerticalLegLength);
                if (equal.HorizontalLegLength != shortLength || equal.VerticalLegLength != shortLength
                    || equal.HorizontalLegThickness != angle.HorizontalLegThickness
                    || equivalentProperty.SectionType != property.SectionType)
                    throw new ArgumentException("The equivalent Model section must have equal legs of the shorter size, the same thickness and manufacturing type.", nameof(equivalentSection));
                if (equivalentSection.Unperforated == false || equivalentSection.NetArea.HasValue)
                    throw new ArgumentException("The equivalent section must supply its gross geometry, not a pre-reduced area.", nameof(equivalentSection));
                equivalentArea = equal.Area;
            }
            if (connected < other && boltCount > 1 && (equivalentSection == null || string.IsNullOrWhiteSpace(equivalentSource)))
                throw new ArgumentException("A short-leg connection needs an explicit equal-short-leg Model section and source.", nameof(equivalentSection));
            return new BraceAngleTensionInput(code, converted, leg, bolt, tension, boltCount, pitch, edge,
                equivalentArea, equivalentSource);
        }

        private static SectionL Angle(ISteelSection property)
        {
            Manufacturing(property);
            if (property.SectionShape.GetType() != typeof(SectionL))
                throw new NotSupportedException("Only an exact Model SectionL is supported; a generic or C shape is not an angle.");
            var angle = (SectionL)property.SectionShape; UniformAngle(angle); return angle;
        }
        private static void Manufacturing(ISteelSection property)
        {
            if (property.FormedType != Section.FormedTypes.HotFinished)
                throw new NotSupportedException("Cold-formed angle connections require their own qualified method.");
            if (property.SectionType != Section.SectionTypes.Rolled && property.SectionType != Section.SectionTypes.Welded)
                throw new NotSupportedException("Unknown angle manufacturing type.");
        }
        private static void UniformAngle(SectionL angle)
        {
            if (angle.HorizontalLegThickness != angle.VerticalLegThickness)
                throw new NotSupportedException("The one-leg effective-net method requires equal leg thicknesses.");
            ConnectionValidation.Positive(angle.Area, "ModelAngleArea");
            ConnectionValidation.Positive(angle.HorizontalLegThickness, "ModelAngleThickness");
        }
        private static ConnectionPoint2D LocalBoltLine(SectionL angle, BraceAngleConnectedLeg leg, double edge)
        {
            if (!Enum.IsDefined(typeof(BraceAngleConnectedLeg), leg)) throw new ArgumentOutOfRangeException(nameof(leg));
            ConnectionValidation.Positive(edge, nameof(edge));
            return leg == BraceAngleConnectedLeg.First
                ? Point(angle.HorizontalLegLength - edge, angle.HorizontalLegThickness / 2)
                : Point(angle.VerticalLegThickness / 2, angle.VerticalLegLength - edge);
        }
        private static void CheckNetDeclaration(SteelSectionInput section, double actualNetArea)
        {
            if (section.Unperforated == false && !section.NetArea.HasValue)
                throw new NotSupportedException("A perforated Model snapshot with an unspecified net area cannot be reduced to one declared bolt line.");
            if (section.NetArea.HasValue && !Close(section.NetArea.Value, actualNetArea, section.Area))
                throw new NotSupportedException("The Model net-area declaration differs from the declared one-hole-per-angle bolt line.");
        }
        private static void CheckPairEquilibrium(SteelSectionInput section, SteelActions actions,
            double first, double second, ConnectionPoint2D p, ConnectionPoint2D q, ConnectionPoint2D c)
        {
            double nx = first + second;
            double mpx = (p.Y - c.Y) * first, mqx = (q.Y - c.Y) * second;
            double mpy = -(p.X - c.X) * first, mqy = -(q.X - c.X) * second;
            double cos = Math.Cos(section.PrincipalAxisAngle), sin = Math.Sin(section.PrincipalAxisAngle);
            double mx = cos * actions.M1 - sin * actions.M2;
            double my = sin * actions.M1 + cos * actions.M2;
            if (actions.V1 != 0 || actions.V2 != 0 || actions.T != 0 || actions.N < 0
                || !Close(actions.N, nx, nx)
                || !Close(mx, mpx + mqx, Math.Abs(mpx) + Math.Abs(mqx) + Math.Abs(mx))
                || !Close(my, mpy + mqy, Math.Abs(mpy) + Math.Abs(mqy) + Math.Abs(my)))
                throw new NotSupportedException("The two declared axial forces do not reconstruct the concomitant pair actions at its centroid.");
        }
        private static ConnectionPoint2D Point(double x, double y) => new ConnectionPoint2D(x, y);
        private static bool Close(double a, double b, double scale)
            => !double.IsNaN(a) && !double.IsNaN(b) && !double.IsInfinity(a) && !double.IsInfinity(b)
                && !double.IsInfinity(scale) && Math.Abs(a - b) <= 1e-10 * Math.Abs(scale);
    }
}
