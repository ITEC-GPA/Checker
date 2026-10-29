using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    public enum SteelIdealFlexuralBoundary { PinnedPinned, FixedPinned, FixedFixed, Cantilever }

    /// <summary>Explicit isolated, straight, prismatic elastic-column model in ONE
    /// principal bending plane. Ideal rotations/translations only; no springs, frame
    /// sway interaction, intermediate restraints or deductions from FEM node labels.</summary>
    public sealed class SteelIdealFlexuralSpan
    {
        public double Length { get; }
        public SteelIdealFlexuralBoundary Boundary { get; }
        public string Source { get; }
        public double EffectiveLengthFactor => Boundary == SteelIdealFlexuralBoundary.FixedFixed ? .5
            : Boundary == SteelIdealFlexuralBoundary.Cantilever ? 2
            : Boundary == SteelIdealFlexuralBoundary.FixedPinned ? Math.PI / 4.493409457909064 : 1;
        public double EffectiveLength => Length * EffectiveLengthFactor;
        /// <summary>Only the cantilever lets one end translate relative to the other in this plane: a sway
        /// flexural mode. Pinned/fixed boundaries fix both end translations (nonsway modes).</summary>
        public bool IsSwayMode => Boundary == SteelIdealFlexuralBoundary.Cantilever;
        public SteelIdealFlexuralSpan(double length, SteelIdealFlexuralBoundary boundary, string source)
        {
            SteelInputValidation.Positive(length, nameof(length));
            if (!Enum.IsDefined(typeof(SteelIdealFlexuralBoundary), boundary)) throw new ArgumentOutOfRangeException(nameof(boundary));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("An isolated ideal-column model declaration is required.", nameof(source));
            Length = length; Boundary = boundary; Source = source;
            SteelInputValidation.Positive(EffectiveLength, nameof(EffectiveLength));
        }
    }

    /// <summary>Equivalent uniform moment factor of a principal bending plane whose relevant flexural buckling
    /// mode is a sway mode: EN 1993-1-1:2005 Annex B Table B.3 note, Cmy=0.9 or Cmz=0.9 respectively, whatever
    /// the moment diagram. NTC 2018: the Circolare 2019 Method B (C4.2.4.1.3.3.2, Table C4.2.VI) follows Annex B;
    /// the Circolare text is not among the library sources, so the same note is a stated GPC assumption.
    /// CmLT belongs to the lateral-torsional segment and is not affected. The factor is consistent only with a
    /// sway Ncr of the same plane; the sway mode is a caller declaration, never inferred from a mesh.</summary>
    public static class SteelSwayMomentFactor
    {
        public const double Value = .9;

        public static string Reference(SectionResistanceStandard standard)
        {
            if (standard == SectionResistanceStandard.Eurocode3_2005)
                return "EN1993-1-1:2005 AnnexB TableB.3 note: sway buckling mode, Cm=0.9 whatever the diagram";
            if (standard == SectionResistanceStandard.Ntc2018)
                return "Circolare2019 C4.2.4.1.3.3.2 Method B TableC4.2.VI: sway buckling mode, Cm=0.9 as the EN1993-1-1:2005 TableB.3 note"
                    + " (GPC assumption: Method B follows Annex B, Circolare text not among the library sources)";
            throw new ArgumentOutOfRangeException(nameof(standard));
        }

        /// <summary>Cm=0.9 for the plane of an ideal isolated column whose idealization is a sway mode
        /// (cantilever). Pinned/fixed boundaries fix both end translations: their Ncr is a nonsway value
        /// and cannot carry the sway factor.</summary>
        public static double For(SteelIdealFlexuralSpan span)
        {
            if (span == null) throw new ArgumentNullException(nameof(span));
            if (!span.IsSwayMode)
                throw new NotSupportedException("The sway factor Cm=0.9 needs a sway Ncr; the " + span.Boundary
                    + " idealization fixes both end translations (nonsway mode).");
            return Value;
        }
    }

    public enum SteelForkMomentShape { EndMoments, UniformTransverseLoad, CentralPointLoad, GenericEnvelope }

    /// <summary>Actual signed FEM moment at a physical station, mm/Nmm. Duplicate
    /// positions may retain the two faces of a concentrated applied moment.</summary>
    public sealed class SteelMomentStation
    {
        public double Position { get; }
        public double Moment { get; }
        public SteelMomentStation(double position, double moment)
        { SteelInputValidation.Finite(position, nameof(position)); SteelInputValidation.Finite(moment, nameof(moment)); Position = position; Moment = moment; }
    }

    /// <summary>Signed section moments in a single physical principal-axis convention,
    /// Nmm. End moments have no distributed transverse load; transverse-load shapes
    /// have zero end moments. Fork ends, free warping, no intermediate restraint.
    /// LoadHeight is positive when the load acts toward the shear centre from its
    /// application point (destabilizing), independently of the sign of the diagram.</summary>
    public sealed class SteelForkMomentDiagram
    {
        public SteelForkMomentShape Shape { get; }
        public double StartMoment { get; }
        public double EndMoment { get; }
        public double PeakMoment { get; }
        public double LoadHeight { get; }
        public string Source { get; }
        public IReadOnlyList<SteelMomentStation> Stations { get; }
        public double MaximumMoment => Shape == SteelForkMomentShape.EndMoments
            ? (Math.Abs(StartMoment) >= Math.Abs(EndMoment) ? StartMoment : EndMoment) : PeakMoment;
        public double EndMomentRatio => Shape == SteelForkMomentShape.EndMoments
            ? (Math.Abs(StartMoment) >= Math.Abs(EndMoment) ? EndMoment / StartMoment : StartMoment / EndMoment) : 0;
        public double C1 { get; }
        public double C2 { get; }
        public bool UsesConservativeUniformC1 { get; }
        /// <summary>For the declared nonsway diagram between the relevant braced points.
        /// Cm is not C1; it is not derived from Mcr or assigned to other restraint spans.</summary>
        public double EquivalentMomentFactor => Shape == SteelForkMomentShape.GenericEnvelope ? 1 : Shape == SteelForkMomentShape.EndMoments
            ? Math.Max(.4, .6 + .4 * EndMomentRatio) : Shape == SteelForkMomentShape.UniformTransverseLoad ? .95 : .9;
        private SteelForkMomentDiagram(SteelForkMomentShape shape, double start, double end, double peak, double height, string source,
            SteelMomentStation[] stations = null)
        {
            foreach (double v in new[] { start, end, peak, height }) SteelInputValidation.Finite(v, "moment diagram value");
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Physical load/diagram declaration is required.", nameof(source));
            Shape = shape; StartMoment = start; EndMoment = end; PeakMoment = peak; LoadHeight = height; Source = source;
            Stations = Array.AsReadOnly(stations ?? Array.Empty<SteelMomentStation>());
            if (MaximumMoment == 0) throw new ArgumentException("A nonzero bending pattern is required; an inactive mode has no normalized load shape.");
            if (shape == SteelForkMomentShape.GenericEnvelope) { C1 = 1; C2 = 0; UsesConservativeUniformC1 = true; return; }
            if (shape != SteelForkMomentShape.EndMoments)
            { C1 = shape == SteelForkMomentShape.UniformTransverseLoad ? 1.127 : 1.348; C2 = shape == SteelForkMomentShape.UniformTransverseLoad ? .454 : .630; return; }
            // SN003b Table3.1; keep the published rows rather than a fitted polynomial.
            // Off-table ratios use the conservative uniform envelope C1=1 explicitly.
            var coefficients = new[] { 2.55, 2.57, 2.33, 2.05, 1.77, 1.52, 1.31, 1.14, 1.0 };
            double index = (EndMomentRatio + 1) * 4; int nearest = (int)Math.Round(index);
            bool tabulated = Math.Abs(index - nearest) <= 16 * 2.2204460492503131e-16;
            C1 = tabulated ? coefficients[nearest] : 1; C2 = 0; UsesConservativeUniformC1 = !tabulated;
        }
        public static SteelForkMomentDiagram EndMoments(double start, double end, string source)
            => new SteelForkMomentDiagram(SteelForkMomentShape.EndMoments, start, end, 0, 0, source);
        public static SteelForkMomentDiagram UniformTransverseLoad(double peakMoment, double loadHeight, string source)
            => new SteelForkMomentDiagram(SteelForkMomentShape.UniformTransverseLoad, 0, 0, peakMoment, loadHeight, source);
        public static SteelForkMomentDiagram CentralPointLoad(double peakMoment, double loadHeight, string source)
            => new SteelForkMomentDiagram(SteelForkMomentShape.CentralPointLoad, 0, 0, peakMoment, loadHeight, source);
        /// <summary>Conservative envelope for arbitrary same-case diagrams, with all
        /// extrema and both boundary cuts exported. No interpolation is used to claim
        /// an unknown peak. Transverse forces act through the shear centre; no follower
        /// forces or applied-torque/height effects. C1=Cm=1 explicitly for this bound.</summary>
        public static SteelForkMomentDiagram GenericEnvelope(double length, IEnumerable<SteelMomentStation> stations,
            double loadHeight, string coverageSource)
        {
            SteelInputValidation.Positive(length, nameof(length)); SteelInputValidation.Finite(loadHeight, nameof(loadHeight));
            if (loadHeight != 0) throw new NotSupportedException("The arbitrary-diagram bound requires transverse loading through the shear centre.");
            if (stations == null) throw new ArgumentNullException(nameof(stations));
            var copy = stations.ToArray();
            if (copy.Length < 2 || copy.Any(x => x == null)) throw new ArgumentException("Both boundary cuts and all actual extrema are required.");
            if (copy.First().Position != 0 || copy.Last().Position != length) throw new ArgumentException("Both physical span boundaries must be represented exactly.");
            for (int i = 0; i < copy.Length; i++)
                if (copy[i].Position < 0 || copy[i].Position > length || i > 0 && copy[i].Position < copy[i - 1].Position)
                    throw new ArgumentException("Physical station order/range is inconsistent.");
            double peak = copy.OrderByDescending(x => Math.Abs(x.Moment)).First().Moment;
            return new SteelForkMomentDiagram(SteelForkMomentShape.GenericEnvelope, copy.First().Moment, copy.Last().Moment, peak, 0, coverageSource, copy);
        }
        public double MomentAt(double normalizedPosition)
        {
            if (!(normalizedPosition >= 0 && normalizedPosition <= 1)) throw new ArgumentOutOfRangeException(nameof(normalizedPosition));
            if (Shape == SteelForkMomentShape.GenericEnvelope) throw new NotSupportedException("No interpolation is inferred for generic FEM diagrams; retain the original stations and exported extrema.");
            return Shape == SteelForkMomentShape.EndMoments ? (1 - normalizedPosition) * StartMoment + normalizedPosition * EndMoment
                : Shape == SteelForkMomentShape.UniformTransverseLoad ? 4 * normalizedPosition * (1 - normalizedPosition) * PeakMoment
                : 2 * Math.Min(normalizedPosition, 1 - normalizedPosition) * PeakMoment;
        }
    }

    public sealed class SteelPreparedCriticalMoment
    {
        public SteelSectionInput Section { get; }
        public SteelMemberInput Member { get; }
        public SteelForkMomentDiagram Diagram { get; }
        public SteelStabilityValue ActualDiagram { get; }
        public SteelStabilityValue UniformMoment { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        internal SteelPreparedCriticalMoment(SteelSectionInput section, SteelMemberInput member, SteelForkMomentDiagram diagram,
            SteelStabilityValue actual, SteelStabilityValue uniform, IEnumerable<SectionResistanceValue> trace)
        { Section = section; Member = member; Diagram = diagram; ActualDiagram = actual; UniformMoment = uniform; Trace = Array.AsReadOnly(trace.ToArray()); }

        /// <summary>The NTC conservative LTB path requires uniform Mcr. A nonzero load
        /// height cannot be discarded to manufacture that basis; use a separately
        /// qualified analysis for NTC in that case.</summary>
        public SteelStabilityValue ForCode(SectionResistanceStandard standard)
        {
            if (standard == SectionResistanceStandard.Eurocode3_2005) return ActualDiagram;
            if (standard != SectionResistanceStandard.Ntc2018) throw new ArgumentOutOfRangeException(nameof(standard));
            if (Diagram.LoadHeight != 0) throw new NotSupportedException("NTC uniform-moment route does not qualify transverse-load height effects; an independent analysis is required.");
            return UniformMoment;
        }

        /// <summary>Bind the preparation to the SAME immutable section/member and the
        /// actual diagram maximum. Section cuts/shear remain separately checked.</summary>
        public SteelMemberAnalysis CreateBendingAnalysis(SteelVerificationInput input, string parameterSet,
            int? memberClass = null, string classificationSource = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!ReferenceEquals(input.Section, Section) || !ReferenceEquals(input.Member, Member))
                throw new ArgumentException("The same immutable section and member preparation are required.");
            var a = input.Actions;
            if (a.N != 0 || a.M2 != 0 || a.T != 0 || a.V1 != 0 || a.V2 != 0 || a.M1 != Diagram.MaximumMoment)
                throw new ArgumentException("Pure major-axis member maximum from this diagram is required; actual section/shear cuts are separate.");
            bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            return SteelMemberAnalysis.Bending(input, Diagram.Source, parameterSet,
                ntc || Diagram.Shape == SteelForkMomentShape.GenericEnvelope ? SteelCriticalMomentBasis.UniformMoment : SteelCriticalMomentBasis.ActualMomentDiagram,
                ForCode(input.Code.SectionCode.Standard), memberClass, classificationSource);
        }
    }

    public static class SteelMemberSchemePreparation
    {
        public static IReadOnlyList<SteelStabilityValue> FlexuralModes(SteelSectionInput section,
            SteelIdealFlexuralSpan aboutAxis1, SteelIdealFlexuralSpan aboutAxis2)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (aboutAxis1 == null || aboutAxis2 == null) throw new ArgumentNullException("span");
            if (section.Unperforated != true) throw new NotSupportedException("Unperforated gross section required.");
            var s = section.ReadOnlySection; SteelMethodSelector.ValidateGeometry(s);
            var result = new List<SteelStabilityValue>();
            Add(1, s.J11, aboutAxis1); Add(2, s.J22, aboutAxis2); return Array.AsReadOnly(result.ToArray());
            void Add(int axis, double inertia, SteelIdealFlexuralSpan span)
            {
                SteelInputValidation.Positive(inertia, "principal gross inertia");
                double n = ElasticStabilityArithmetic.Euler(span.EffectiveLength, inertia, section.ElasticModulus);
                result.Add(new SteelStabilityValue(axis == 1 ? SteelStabilityQuantity.Ncr1 : SteelStabilityQuantity.Ncr2, n, SteelStabilityUnit.N,
                    "Ideal isolated prismatic Euler column ABOUT principal axis" + axis + "; " + span.Boundary + "; L=" + F(span.Length)
                    + "mm; Leff=" + F(span.EffectiveLength) + "mm; " + span.Source + "; torsional modes not assessed"));
            }
        }

        /// <summary>SN003b-EN-EU equations2/3, Tables3.1/3.2. Doubly symmetric H only;
        /// lateral translations and twist fixed at ends, bending rotations and warping
        /// free. Actual gross Model properties, no plastic/effective stiffness.</summary>
        public static SteelPreparedCriticalMoment ForkBending(SteelSectionInput section, SteelMemberInput member,
            SteelForkMomentDiagram diagram, string restraintSource)
        {
            if (section == null || member == null || diagram == null) throw new ArgumentNullException("preparation input");
            if (string.IsNullOrWhiteSpace(restraintSource)) throw new ArgumentException("Complete fork/free-warping model declaration required.");
            if (section.Unperforated != true) throw new NotSupportedException("Unperforated gross section required.");
            var s = section.ReadOnlySection; SteelMethodSelector.ValidateGeometry(s);
            if (s.SectionShape.GetType() != typeof(SectionH) || !((SectionH)s.SectionShape).IsDoubleSymmetric
                || Math.Abs(s.SectionShape.AngleX1) > 1e-10 || SteelMethodSelector.ColdFormedOpen(s))
                throw new NotSupportedException("Doubly symmetric H with horizontal major principal axis required.");
            double l = member.Length;
            if (diagram.Shape == SteelForkMomentShape.GenericEnvelope && diagram.Stations.Last().Position != l)
                throw new ArgumentException("Generic FEM stations must cover this same physical member span.");
            foreach (double? effective in new[] { member.BucklingLength1, member.BucklingLength2, member.TorsionalBucklingLength })
                if (effective.HasValue && Math.Abs(effective.Value - l) > 1e-10 * l)
                    throw new ArgumentException("Effective length conflicts with the full-span fork model.");
            foreach (var end in new[] { member.StartRestraints, member.EndRestraints })
                if (end != null && (!Compatible(end.Translation1, SteelRestraintState.Fixed) || !Compatible(end.Translation2, SteelRestraintState.Fixed)
                    || !Compatible(end.Rotation1, SteelRestraintState.Free) || !Compatible(end.Rotation2, SteelRestraintState.Free)
                    || !Compatible(end.Rotation3, SteelRestraintState.Fixed) || !Compatible(end.Warping, SteelRestraintState.Free)))
                    throw new ArgumentException("End metadata conflicts with the explicit fork/free-warping model.");
            if (Math.Abs(diagram.LoadHeight) > ((SectionH)s.SectionShape).Height / 2)
                throw new NotSupportedException("Load application must be within the physical section depth in this preparation.");
            double e = section.ElasticModulus, g = e / (2 * (1 + section.PoissonRatio));
            foreach (double value in new[] { e, g, s.J22, s.Jt }) SteelInputValidation.Positive(value, "gross elastic property");
            SteelInputValidation.Finite(s.Jw, "Iw"); if (s.Jw < 0) throw new ArgumentOutOfRangeException("Iw");
            double uniform = ElasticStabilityArithmetic.LateralTorsional(e, g, s.J22, s.Jt, s.Jw, l, 1, 1, 1, 0);
            double actual = ElasticStabilityArithmetic.LateralTorsional(e, g, s.J22, s.Jt, s.Jw, l, 1, 1, diagram.C1, diagram.C2 * diagram.LoadHeight);
            string source = "SN003b-EN-EU equations2/3, Tables3.1/3.2; L=" + F(l) + "mm; " + restraintSource;
            var trace = new[] { V("Length", l, "mm"), V("E", e, "MPa"), V("G", g, "MPa"), V("IminorGross", s.J22, "mm4"),
                V("ItGross", s.Jt, "mm4"), V("IwGross", s.Jw, "mm6"), V("C1", diagram.C1, "1"), V("C2", diagram.C2, "1"),
                V("LoadHeight", diagram.LoadHeight, "mm"), V("C1UniformFallback", diagram.UsesConservativeUniformC1 ? 1 : 0, "1"),
                V("GenericEnvelope", diagram.Shape == SteelForkMomentShape.GenericEnvelope ? 1 : 0, "1"),
                V("ExportedMomentStations", diagram.Stations.Count, "1"), V("McrActualDiagram", actual, "Nmm"), V("McrUniformMoment", uniform, "Nmm") };
            return new SteelPreparedCriticalMoment(section, member, diagram,
                new SteelStabilityValue(SteelStabilityQuantity.Mcr, actual, SteelStabilityUnit.Nmm, source + "; actual diagram: " + diagram.Source
                    + "; C1=" + F(diagram.C1) + "; C2=" + F(diagram.C2) + "; zg=" + F(diagram.LoadHeight)
                    + (diagram.Shape == SteelForkMomentShape.GenericEnvelope ? "; GENERIC FEM DIAGRAM: uniform conservative bound, no actual-diagram eigenvalue claimed"
                        : diagram.UsesConservativeUniformC1 ? "; off-table end ratio: conservative C1=1" : "")),
                new SteelStabilityValue(SteelStabilityQuantity.Mcr, uniform, SteelStabilityUnit.Nmm, source + "; UNIFORM MOMENT REFERENCE, C1=1, no transverse load"
                    + (diagram.Shape == SteelForkMomentShape.GenericEnvelope ? "; conservative bound for same-case generic FEM diagram; exported extrema qualification: " + diagram.Source : "")), trace);
        }
        private static bool Compatible(SteelRestraintState actual, SteelRestraintState expected) => actual == SteelRestraintState.Unknown || actual == expected;
        private static SectionResistanceValue V(string name, double value, string unit) => new SectionResistanceValue(name, value, unit);
        private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
