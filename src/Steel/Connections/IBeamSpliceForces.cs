using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Rectangular grid on EACH side of a butt splice, mirrored about X=0.
    /// X is along the beam; the second coordinate is vertical for web bolts and
    /// transverse for flange bolts. All dimensions are mm. FirstBoltDistance is
    /// measured from the splice centre, not from a free plate edge.</summary>
    public sealed class SpliceBoltLayout
    {
        public int LongitudinalRows { get; }
        public int TransverseRows { get; }
        public double LongitudinalPitch { get; }
        public double TransversePitch { get; }
        public double FirstBoltDistance { get; }
        public int CountPerSide => LongitudinalRows * TransverseRows;
        public double LongitudinalSpan => (LongitudinalRows - 1) * LongitudinalPitch;
        public double TransverseSpan => (TransverseRows - 1) * TransversePitch;
        public double CentroidDistance => FirstBoltDistance + LongitudinalSpan / 2;
        public SpliceBoltLayout(int longitudinalRows, int transverseRows, double longitudinalPitch,
            double transversePitch, double firstBoltDistance)
        {
            if (longitudinalRows < 1 || longitudinalRows > 100) throw new ArgumentOutOfRangeException(nameof(longitudinalRows));
            if (transverseRows < 1 || transverseRows > 100) throw new ArgumentOutOfRangeException(nameof(transverseRows));
            ConnectionValidation.Positive(longitudinalPitch, nameof(longitudinalPitch));
            ConnectionValidation.Positive(transversePitch, nameof(transversePitch));
            ConnectionValidation.Positive(firstBoltDistance, nameof(firstBoltDistance));
            LongitudinalRows = longitudinalRows; TransverseRows = transverseRows;
            LongitudinalPitch = longitudinalPitch; TransversePitch = transversePitch; FirstBoltDistance = firstBoltDistance;
            ConnectionNumbers.Finite(LongitudinalSpan, nameof(longitudinalPitch));
            ConnectionNumbers.Finite(TransverseSpan, nameof(transversePitch));
            ConnectionNumbers.Finite(FirstBoltDistance + LongitudinalSpan, nameof(firstBoltDistance));
            for (int i = 1; i < LongitudinalRows; i++)
                if (!(FirstBoltDistance + i * LongitudinalPitch > FirstBoltDistance + (i - 1) * LongitudinalPitch))
                    throw new ArgumentException("Longitudinal spacing cannot be represented at this coordinate magnitude.");
        }
        internal ConnectionPoint2D[] Points(SpliceSide side)
        {
            double sign = side == SpliceSide.Left ? -1 : 1;
            return Enumerable.Range(0, LongitudinalRows).SelectMany(i =>
                Enumerable.Range(0, TransverseRows).Select(j => new ConnectionPoint2D(
                    sign * (FirstBoltDistance + i * LongitudinalPitch),
                    (j - (TransverseRows - 1) / 2.0) * TransversePitch))).ToArray();
        }
    }
    public enum SpliceSide { Left, Right }
    public enum IBeamSplicePart { TopFlange, BottomFlange, Web }
    public enum IBeamSpliceForceStatus { Balanced, NotSupported, NumericalFailure }

    /// <summary>Two identical, aligned, doubly symmetric rolled I/H sections with
    /// mirrored grids; the same flange layout is used above and below. N, mm, Nmm.
    /// Actions are those transmitted TO THE LEFT MEMBER, in the right-handed frame
    /// X along beam to the right, Y upward, Z normal to web. Fx positive is tension;
    /// positive Mz compresses the top flange. The right member receives opposite actions.
    /// Iy is the gross strong-axis second moment (mm4), including the roots.</summary>
    public sealed class IBeamSpliceForceInput
    {
        public ConnectionRolledISection Section { get; }
        public double StrongAxisSecondMoment { get; }
        public PlanarConnectionActions Actions { get; }
        public SpliceBoltLayout WebBolts { get; }
        public SpliceBoltLayout FlangeBolts { get; }
        public IBeamSpliceForceInput(ConnectionRolledISection section, double strongAxisSecondMoment,
            PlanarConnectionActions actions, SpliceBoltLayout webBolts, SpliceBoltLayout flangeBolts)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            WebBolts = webBolts ?? throw new ArgumentNullException(nameof(webBolts));
            FlangeBolts = flangeBolts ?? throw new ArgumentNullException(nameof(flangeBolts));
            ConnectionValidation.Positive(strongAxisSecondMoment, nameof(strongAxisSecondMoment));
            StrongAxisSecondMoment = strongAxisSecondMoment;
        }
    }
    public sealed class IBeamSpliceResultants
    {
        public double TopFlangeAxial { get; }
        public double BottomFlangeAxial { get; }
        public double WebAxial { get; }
        public double WebShear { get; }
        public double WebMoment { get; }
        public double FlangeLeverArm { get; }
        internal IBeamSpliceResultants(double top, double bottom, double axial, double shear, double moment, double lever)
        { TopFlangeAxial = top; BottomFlangeAxial = bottom; WebAxial = axial; WebShear = shear; WebMoment = moment; FlangeLeverArm = lever; }
    }
    public sealed class IBeamSpliceBoltGroup
    {
        public SpliceSide Side { get; }
        public IBeamSplicePart Part { get; }
        public ElasticBoltGroupResult Distribution { get; }
        internal IBeamSpliceBoltGroup(SpliceSide side, IBeamSplicePart part, ElasticBoltGroupResult distribution)
        { Side = side; Part = part; Distribution = distribution; }
    }
    /// <summary>A force distribution, never a resistance verification. Resultants and
    /// BoltGroups are unavailable on failure; no favourable utilization is returned.</summary>
    public sealed class IBeamSpliceForceResult
    {
        public IBeamSpliceForceInput Input { get; }
        public IBeamSpliceForceStatus Status { get; }
        public bool IsSupported => Status == IBeamSpliceForceStatus.Balanced;
        public PlanarConnectionActions ActionsAtJoint { get; }
        public IBeamSpliceResultants Resultants { get; }
        public IReadOnlyList<IBeamSpliceBoltGroup> BoltGroups { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string MethodId => "Steel.Connection.IBeamSplice.ElasticForceDistribution";
        public string Scope => "SCI P398 section4 Step1 elastic N-V-M distribution and point-bolt equilibrium on BOTH sides. "
            + "Identical rolled I/H sections and mirrored rectangular grids. No bolt/plate/member resistance, contact-bearing, "
            + "slip, local secondary bending, buckling, stiffness or whole-joint qualification.";
        internal IBeamSpliceForceResult(IBeamSpliceForceInput input, IBeamSpliceForceStatus status,
            PlanarConnectionActions actions, IBeamSpliceResultants resultants, IList<IBeamSpliceBoltGroup> groups,
            IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            Input = input; Status = status; ActionsAtJoint = actions;
            Resultants = IsSupported ? resultants : null;
            BoltGroups = Array.AsReadOnly(IsSupported ? groups.ToArray() : new IBeamSpliceBoltGroup[0]);
            Trace = Array.AsReadOnly(trace.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        }
    }
    public static class IBeamSpliceForces
    {
        public static IBeamSpliceForceResult Calculate(IBeamSpliceForceInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var groups = new List<IBeamSpliceBoltGroup>();
            PlanarConnectionActions atJoint = null; IBeamSpliceResultants resultants = null;
            var status = IBeamSpliceForceStatus.NotSupported;
            IBeamSpliceForceResult Result() => new IBeamSpliceForceResult(p, status, atJoint, resultants, groups, trace, issues);
            void Value(string name, double value, string unit) => trace.Add(new SectionResistanceValue(name, value, unit));
            var a = p.Actions; var s = p.Section;
            if (a.Fz != 0 || a.Mx != 0 || a.My != 0 || a.ApplicationZ != 0) issues.Add("IN_PLANE_N_V_M_ONLY");
            if (p.WebBolts.TransverseSpan >= s.ClearWeb) issues.Add("WEB_BOLT_CENTRES_INTERSECT_ROOTS");
            if (p.FlangeBolts.TransverseSpan >= s.Width) issues.Add("FLANGE_BOLT_CENTRES_OUTSIDE_FLANGE");
            double hw = s.Height - 2 * s.FlangeThickness, lever = s.Height - s.FlangeThickness;
            double aw = hw * s.WebThickness, iw = s.WebThickness * hw * hw * hw / 12;
            // Integrate each root as a square minus a quadrant: u measured down from
            // the flange underside. Area, first and second u moments are exact.
            double r = s.RootRadius, tf = s.FlangeThickness, b = s.Width;
            double rootArea = (1 - Math.PI / 4) * r * r;
            double rootFirst = (5.0 / 6 - Math.PI / 4) * r * r * r;
            double rootSecond = (1 - 5 * Math.PI / 16) * r * r * r * r;
            double geometricIy = iw + 2 * (b * tf * tf * tf / 12 + b * tf * lever * lever / 4)
                + 4 * (rootArea * hw * hw / 4 - hw * rootFirst + rootSecond);
            double mz = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            if (!Finite(geometricIy) || !Finite(iw) || !Finite(aw) || !Finite(mz))
            { status = IBeamSpliceForceStatus.NumericalFailure; issues.Add("SECTION_OR_ACTIONS_OUTSIDE_NUMERIC_RANGE"); return Result(); }
            // Same 0.5% catalogue rounding allowance as ConnectionRolledISection.Area.
            if (!(geometricIy > 0) || Math.Abs(p.StrongAxisSecondMoment - geometricIy) > .005 * geometricIy)
                issues.Add("STRONG_AXIS_INERTIA_INCONSISTENT_WITH_SECTION");
            if (!(aw < s.Area) || !(iw < p.StrongAxisSecondMoment)) issues.Add("WEB_PROPERTIES_MUST_BE_SMALLER_THAN_WHOLE_SECTION");
            if (issues.Count != 0) return Result();
            atJoint = new PlanarConnectionActions(a.Fx, a.Fy, 0, 0, 0, mz);
            double ratioA = aw / s.Area, ratioI = iw / p.StrongAxisSecondMoment;
            double nw = ratioA * a.Fx, mw = ratioI * mz;
            double halfN = (1 - ratioA) * (a.Fx / 2);
            double bendingForce = (1 - ratioI) * (mz / lever);
            double top = halfN - bendingForce, bottom = halfN + bendingForce;
            if (!new[] { nw, mw, top, bottom, bendingForce }.All(Finite))
            { status = IBeamSpliceForceStatus.NumericalFailure; issues.Add("DISTRIBUTION_OUTSIDE_NUMERIC_RANGE"); return Result(); }
            Value("Web.Area", aw, "mm2"); Value("Web.StrongAxisSecondMoment", iw, "mm4");
            Value("Section.GeometricStrongAxisSecondMoment", geometricIy, "mm4");
            Value("Web.AxialFraction", ratioA, "1"); Value("Web.MomentFraction", ratioI, "1");
            resultants = new IBeamSpliceResultants(top, bottom, nw, a.Fy, mw, lever);
            // Both sides are explicit. Adding |V*e| on both sides would violate
            // equilibrium; the signed transport is delegated to ElasticBoltGroup.
            foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            {
                double sign = side == SpliceSide.Left ? 1 : -1;
                foreach (IBeamSplicePart part in new[] { IBeamSplicePart.TopFlange, IBeamSplicePart.BottomFlange, IBeamSplicePart.Web })
                {
                    bool web = part == IBeamSplicePart.Web;
                    double fx = web ? nw : part == IBeamSplicePart.TopFlange ? top : bottom;
                    var distribution = ElasticBoltGroup.Calculate((web ? p.WebBolts : p.FlangeBolts).Points(side),
                        new PlanarConnectionActions(sign * fx, web ? sign * a.Fy : 0, 0, 0, 0, web ? sign * mw : 0));
                    if (!distribution.IsSupported)
                    {
                        issues.AddRange(distribution.Diagnostics.Select(x => side + "." + part + ":" + x));
                        if (distribution.Status == ElasticBoltGroupStatus.NumericalFailure) status = IBeamSpliceForceStatus.NumericalFailure;
                    }
                    groups.Add(new IBeamSpliceBoltGroup(side, part, distribution));
                }
            }
            if (issues.Count != 0) return Result();
            // Reconstruct each member cut in the common frame from actual bolt forces.
            // Web basis=(X,Y,Z); flange basis=(X,Z,-Y), at Y=+/-lever/2.
            foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            {
                double sign = side == SpliceSide.Left ? 1 : -1;
                double nx = 0, vy = 0, moment = 0, forceScale = Math.Abs(a.Fx), shearScale = Math.Abs(a.Fy), momentScale = Math.Abs(mz);
                foreach (var g in groups.Where(g => g.Side == side)) foreach (var f in g.Distribution.Forces)
                {
                    nx += f.Fx; forceScale += Math.Abs(f.Fx);
                    double term;
                    if (g.Part == IBeamSplicePart.Web) { vy += f.Fy; shearScale += Math.Abs(f.Fy); term = f.Position.X * f.Fy - f.Position.Y * f.Fx; }
                    else term = (g.Part == IBeamSplicePart.TopFlange ? -1 : 1) * (lever / 2) * f.Fx;
                    moment += term; momentScale += Math.Abs(term);
                }
                double nr = nx - sign * a.Fx, vr = vy - sign * a.Fy, mr = moment - sign * mz;
                if (!NearZero(nr, forceScale) || !NearZero(vr, shearScale) || !NearZero(mr, momentScale))
                { status = IBeamSpliceForceStatus.NumericalFailure; issues.Add(side + ".RECONSTRUCTED_SECTION_EQUILIBRIUM_FAILED"); }
                else { Value(side + ".AxialResidual", nr, "N"); Value(side + ".ShearResidual", vr, "N"); Value(side + ".MomentResidual", mr, "Nmm"); }
            }
            if (issues.Count == 0) status = IBeamSpliceForceStatus.Balanced;
            return Result();
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool NearZero(double value, double scale) => Finite(value) && Finite(scale)
            && Math.Abs(value) <= ElasticBoltGroup.EquilibriumRelativeTolerance * scale;
    }
}
