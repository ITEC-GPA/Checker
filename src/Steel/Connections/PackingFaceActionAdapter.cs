using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Geometry;
using GPC.Model.Core.Coordinates;
using GPC.Model.Results;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One explicitly supplied six-component action state ON the packing block,
    /// from the chord identified by PartIndex (0 or 1 in Model). N, Nmm and mm.
    /// Local X is member +Z; local Z is the outward normal from block to chord;
    /// local Y completes a right-handed triad. The application point is relative
    /// to the centre of that face in these same local axes. No force is inferred
    /// from the global member N. LocalStateId includes the concomitant combination
    /// and, where relevant, the local imperfection/load-introduction scenario.</summary>
    public sealed class PackingFaceActionState
    {
        public SteelVerificationInput SourceInput { get; }
        public string LocalStateId { get; }
        public string LocalAnalysisSource { get; }
        public int PartIndex { get; }
        public PlanarConnectionActions ActionsOnBlock { get; }

        public PackingFaceActionState(SteelVerificationInput sourceInput, string localStateId,
            string localAnalysisSource, int partIndex, PlanarConnectionActions actionsOnBlock)
        {
            SourceInput = sourceInput ?? throw new ArgumentNullException(nameof(sourceInput));
            if (string.IsNullOrWhiteSpace(localStateId)) throw new ArgumentException("A concomitant local state identifier is required.", nameof(localStateId));
            if (string.IsNullOrWhiteSpace(localAnalysisSource)) throw new ArgumentException("The local force model and its provenance are required.", nameof(localAnalysisSource));
            if (partIndex < 0 || partIndex > 1) throw new ArgumentOutOfRangeException(nameof(partIndex));
            ActionsOnBlock = actionsOnBlock ?? throw new ArgumentNullException(nameof(actionsOnBlock));
            LocalStateId = localStateId; LocalAnalysisSource = localAnalysisSource; PartIndex = partIndex;
        }
    }

    /// <summary>Immutable face demand. Frames are exported as defensive copies of the
    /// existing Geometry CoordinateSystem. ActionsOnBlockAtFaceCentre can be used by a
    /// future qualified attachment model in the local XY face plane. Action/reaction
    /// on the chord has the opposite sign in these same axes; it is not applied here.</summary>
    public sealed class PackingFaceDemand
    {
        private readonly CoordinateSystem _axes;
        public PackingFaceActionState Source { get; }
        public int PartIndex => Source.PartIndex;
        public CoordinateSystem LocalAxes => Copy(_axes);
        public PlanarConnectionActions ActionsOnBlockAtFaceCentre { get; }
        /// <summary>All six components in Model geometry X/Y and member Z, reduced at
        /// the packing block centre. These are NOT principal-section-axis components.</summary>
        public PlanarConnectionActions ActionsOnBlockAtBlockCentre { get; }

        internal PackingFaceDemand(PackingFaceActionState source, CoordinateSystem axes,
            CoordinateSystem blockAxes)
        {
            Source = source; _axes = Copy(axes);
            var a = source.ActionsOnBlock;
            Point3d application = axes.ToGlobal(new Point3d(a.ApplicationX, a.ApplicationY, a.ApplicationZ));
            var applicationAxes = new CoordinateSystem(application, axes.V1, axes.V2, axes.V3);
            Axes.Validate(applicationAxes);
            // Reuse Model's existing wrench transport; N/T belong to coordinate axis3.
            var resultant = new ResultBeamForces(a.Fz, a.Fx, a.Fy, a.Mz, a.Mx, a.My, applicationAxes);
            ActionsOnBlockAtFaceCentre = Actions(resultant.ToCoordinateSystemWithEccentricity(axes));
            ActionsOnBlockAtBlockCentre = Actions(resultant.ToCoordinateSystemWithEccentricity(blockAxes));
        }
        private static PlanarConnectionActions Actions(ResultBeamForces a)
            => new PlanarConnectionActions(a.V1, a.V2, a.N, a.M1, a.M2, a.T);
        internal static CoordinateSystem Copy(CoordinateSystem a)
            => new CoordinateSystem(new Point3d(a.Origin.X, a.Origin.Y, a.Origin.Z),
                new Vector3d(a.V1.X, a.V1.Y, a.V1.Z), new Vector3d(a.V2.X, a.V2.Y, a.V2.Z),
                new Vector3d(a.V3.X, a.V3.Y, a.V3.Z));
    }

    /// <summary>Geometry, demand transformation and static equilibrium ONLY.
    /// No resistance, member qualification, automatic force split, or successful
    /// joint status is represented. A nonzero residual remains available for diagnosis.</summary>
    public sealed class PackingFaceActionAdapterResult
    {
        private readonly double _centreX, _centreY, _centreZ;
        public SteelVerificationInput SourceInput { get; }
        public string LocalStateId { get; }
        public string LocalAnalysisSource { get; }
        public double PackingThickness { get; }
        public double FaceWidth { get; }
        public double FaceLength { get; }
        public Point3d BlockCentre => new Point3d(_centreX, _centreY, _centreZ);
        public IReadOnlyList<PackingFaceDemand> Faces { get; }
        public PlanarConnectionActions ResidualAtBlockCentre { get; }
        public bool IsInEquilibrium { get; }
        public string Scope => "Two-face local action adaptation and 3D equilibrium of one rectangular packing block in identical Model2L/2C. "
            + "No external actions other than the two declared faces; no resistance, spacing, member-stability, weld/bolt/contact or complete-joint qualification.";

        internal PackingFaceActionAdapterResult(PackingFaceActionState source, Point3d centre,
            double thickness, double width, double length, PackingFaceDemand first, PackingFaceDemand second)
        {
            SourceInput = source.SourceInput; LocalStateId = source.LocalStateId; LocalAnalysisSource = source.LocalAnalysisSource;
            _centreX = centre.X; _centreY = centre.Y; _centreZ = centre.Z;
            PackingThickness = thickness; FaceWidth = width; FaceLength = length;
            Faces = Array.AsReadOnly(new[] { first, second });
            var a = first.ActionsOnBlockAtBlockCentre; var b = second.ActionsOnBlockAtBlockCentre;
            ResidualAtBlockCentre = new PlanarConnectionActions(a.Fx + b.Fx, a.Fy + b.Fy, a.Fz + b.Fz,
                a.Mx + b.Mx, a.My + b.My, a.Mz + b.Mz);
            IsInEquilibrium = Balanced(a.Fx, b.Fx) && Balanced(a.Fy, b.Fy) && Balanced(a.Fz, b.Fz)
                && Balanced(a.Mx, b.Mx) && Balanced(a.My, b.My) && Balanced(a.Mz, b.Mz);
        }
        // A per-component relative roundoff allowance. No moment is compared with
        // a force scale; a genuine unbalanced pure couple cannot pass this criterion.
        private static bool Balanced(double a, double b) => Math.Abs(a + b) <= 1e-10 * Math.Abs(a) + 1e-10 * Math.Abs(b);
    }

    public static class PackingFaceActionAdapter
    {
        /// <summary>Adapts both explicitly supplied face actions for one rectangular
        /// packing station. Station is the block-centre coordinate along member +Z.
        /// TransverseCentre is measured along the component's physical back from its
        /// unreflected local origin: Y for vertical backs, X for horizontal angle backs.
        /// FaceWidth is across that back; FaceLength is along the member. All in mm.
        /// If member length is present the full block must fit within its station range.
        /// Input face order is immaterial; output Faces are ordered by Model PartIndex.</summary>
        public static PackingFaceActionAdapterResult FromModel(double station, double transverseCentre,
            double faceWidth, double faceLength, PackingFaceActionState first, PackingFaceActionState second)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (!ReferenceEquals(first.SourceInput, second.SourceInput)
                || !string.Equals(first.LocalStateId, second.LocalStateId, StringComparison.Ordinal)
                || !string.Equals(first.LocalAnalysisSource, second.LocalAnalysisSource, StringComparison.Ordinal))
                throw new ArgumentException("Both faces must belong to the same input object, concomitant local state and local-analysis source.");
            if (first.PartIndex == second.PartIndex) throw new ArgumentException("Both distinct physical chord faces are required.");
            ConnectionValidation.Nonnegative(station, nameof(station));
            ConnectionValidation.Nonnegative(transverseCentre, nameof(transverseCentre));
            ConnectionValidation.Positive(faceWidth, nameof(faceWidth));
            ConnectionValidation.Positive(faceLength, nameof(faceLength));
            ConnectionNumbers.Finite(station + faceLength / 2, "packing end station");
            var input = first.SourceInput;
            if (station < faceLength / 2 || (input.Member != null && station + faceLength / 2 > input.Member.Length))
                throw new ArgumentException("The full packing block must fit within the member station range.");
            var property = input.Section.ReadOnlySection;
            if (property.SectionShape.GetType() != typeof(SectionBuiltUp)) throw new NotSupportedException("An exact Model SectionBuiltUp is required.");
            var pair = (SectionBuiltUp)property.SectionShape;
            ModelBuiltUpGeometry.Create(pair); // Existing identical shared C/L, reflection/alignment/non-overlap contract.
            var p = pair.Parts[0]; var q = pair.Parts[1]; var chord = p.Section;
            bool horizontal = p.MirrorX != q.MirrorX;
            double backLength;
            if (chord is SectionC channel)
            {
                if (!horizontal || channel.LengthTop != channel.LengthBottom || channel.ThicknessTop != channel.ThicknessBottom)
                    throw new NotSupportedException("Packed2C requires equal flanges and webs facing back to back.");
                backLength = channel.Height;
            }
            else
            {
                var angle = (SectionL)chord;
                backLength = horizontal ? angle.VerticalLegLength : angle.HorizontalLegLength;
            }
            if (transverseCentre < faceWidth / 2 || transverseCentre + faceWidth / 2 > backLength)
                throw new ArgumentException("The packing face width must fit on the connected physical back.");
            var pb = p.Place(0, 0); var qb = q.Place(0, 0);
            double difference = horizontal ? qb.X - pb.X : qb.Y - pb.Y;
            double gap = Math.Abs(difference);
            ConnectionValidation.Positive(gap, "clear packing gap");
            int interior0 = (horizontal ? p.MirrorX : p.MirrorY) ? -1 : 1;
            int interior1 = (horizontal ? q.MirrorX : q.MirrorY) ? -1 : 1;
            if (interior0 != -Math.Sign(difference) || interior1 != Math.Sign(difference))
                throw new NotSupportedException("Only actual facing backs are supported, not toe-to-toe or star layouts.");
            // Do not redo the member's 3t/15imin/spacing qualification here. This adapter
            // preserves a local demand; SteelPackedPairExecution remains responsible for it.
            var fp = p.Place(horizontal ? 0 : transverseCentre, horizontal ? transverseCentre : 0);
            var fq = q.Place(horizontal ? 0 : transverseCentre, horizontal ? transverseCentre : 0);
            var origin0 = new Point3d(fp.X, fp.Y, station); var origin1 = new Point3d(fq.X, fq.Y, station);
            var centre = new Point3d(fp.X / 2 + fq.X / 2, fp.Y / 2 + fq.Y / 2, station);
            var blockAxes = new CoordinateSystem(centre, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
            var sources = new[] { first, second }.OrderBy(f => f.PartIndex).ToArray();
            var face0 = new PackingFaceDemand(sources[0], FaceAxes(origin0, horizontal, interior0), blockAxes);
            var face1 = new PackingFaceDemand(sources[1], FaceAxes(origin1, horizontal, interior1), blockAxes);
            return new PackingFaceActionAdapterResult(sources[0], centre, gap, faceWidth, faceLength, face0, face1);
        }

        private static CoordinateSystem FaceAxes(Point3d origin, bool horizontal, int sign)
        {
            var normal = horizontal ? new Vector3d(sign, 0, 0) : new Vector3d(0, sign, 0);
            var transverse = horizontal ? new Vector3d(0, -sign, 0) : new Vector3d(sign, 0, 0);
            var result = new CoordinateSystem(origin, Vector3d.ZAxis, transverse, normal);
            Axes.Validate(result); return result;
        }
    }
}
