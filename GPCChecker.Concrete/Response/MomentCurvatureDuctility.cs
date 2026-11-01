using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Response
{
    /// <summary>
    /// Curvature ductility request (0.33.0.0): number of uniform steps of the curvature-controlled branch from zero to the limit curvature (10-500)
    /// and bisections of the conventional first yield and of the post-peak reduction (0-60).
    /// </summary>
    public sealed class MomentCurvatureDuctilityRequest
    {
        public int CurvatureSteps { get; }
        public int Bisections { get; }
        public MomentCurvatureDuctilityRequest(int curvatureSteps = 50, int bisections = 40)
        { CurvatureSteps = curvatureSteps; Bisections = bisections; }
    }

    /// <summary>What reaches the conventional first yield first (NTC 2018 §4.1.2.3.4.2).</summary>
    public enum MomentCurvatureFirstYield
    {
        /// <summary>The most stretched bar reaches the yield strain εyd.</summary>
        TensionSteel,
        /// <summary>The most compressed outline vertex reaches the peak strain of the concrete (εc2 of the parabola-rectangle).</summary>
        ConcretePeakStrain
    }

    /// <summary>What defines the ultimate curvature φu.</summary>
    public enum MomentCurvatureUltimate
    {
        /// <summary>The limit point of the failure domain: ultimate strain of the concrete or of the steel.</summary>
        LimitPoint,
        /// <summary>The moment falls to 0.85 of the maximum on the branch after the peak, before the limit point.</summary>
        PostPeakReduction
    }

    /// <summary>
    /// Curvature ductility of the section at constant N (NTC 2018 §4.1.2.3.4.2, Circolare 2019 Fig. C4.1.12), from a curvature-controlled branch,
    /// which also follows the moment after its peak. Units of the native curve: N, Nmm, 1/mm.
    /// <list type="bullet">
    /// <item>first yield M'yd, φ'yd: the first of the most stretched bar at εyd and the most compressed vertex at the peak strain of the concrete;</item>
    /// <item>MRd: the limit moment of the failure domain at N (the one of the curve); φyd = (MRd/M'yd) φ'yd, the bilinear of Fig. C4.1.12 is
    /// (0; 0), (φyd; MRd), (φu; MRd);</item>
    /// <item>φu: the curvature at which the moment falls to 0.85 Mmax after the peak, if before the limit curvature of the domain, else the latter;</item>
    /// <item>μφ = φu/φyd.</item>
    /// </list>
    /// </summary>
    public sealed class MomentCurvatureDuctility
    {
        /// <summary>Yield strain of the bars εyd and peak strain of the concrete (positive), used for the first yield.</summary>
        public double SteelYieldStrain { get; internal set; }
        public double ConcretePeakStrain { get; internal set; }
        /// <summary>M'yd and φ'yd of the conventional first yield.</summary>
        public double FirstYieldMoment { get; internal set; }
        public double FirstYieldCurvature { get; internal set; }
        public MomentCurvatureFirstYield FirstYield { get; internal set; }
        /// <summary>MRd: limit moment of the failure domain at N in the direction of the curve.</summary>
        public double ResistingMoment { get; internal set; }
        /// <summary>φyd = (MRd/M'yd) φ'yd, the elastic branch of the bilinear.</summary>
        public double YieldCurvature { get; internal set; }
        /// <summary>Largest moment of the curvature-controlled branch up to the limit curvature, and its curvature.</summary>
        public double MaximumMoment { get; internal set; }
        public double MaximumMomentCurvature { get; internal set; }
        /// <summary>Curvature of the limit point of the failure domain (the end of the branch).</summary>
        public double LimitCurvature { get; internal set; }
        /// <summary>φu and its definition.</summary>
        public double UltimateCurvature { get; internal set; }
        public MomentCurvatureUltimate Ultimate { get; internal set; }
        /// <summary>μφ = φu/φyd.</summary>
        public double CurvatureDuctility { get; internal set; }
        /// <summary>
        /// Points of the curvature-controlled branch, uniform in curvature from zero (excluded) to the limit curvature; the last one is the limit
        /// point of the domain (its moment MRd and its plane), because at that curvature a plane in equilibrium at the exact N may pass the ultimate
        /// strain of the concrete, beyond which the law of the material gives no stress.
        /// </summary>
        public IReadOnlyList<MomentCurvaturePoint> Points { get; internal set; }
    }

    /// <summary>Curvature-controlled states at constant N on a native checker and the curvature ductility from them.</summary>
    internal sealed class MomentCurvatureDuctilityAnalysis
    {
        private const double PostPeakFraction = 0.85;
        private readonly SectionSolver solver;
        private readonly ReinforcedConcreteSection section;
        private readonly CoordinateSystem solverAxes, forceAxes;
        private readonly Point2d origin;
        private readonly double axialForce, c, s, momentScale;
        private readonly Point2d[] bars, outline;
        private readonly StrainPlane limitPlane;
        private double psi;     // direction of the curvature vector of the last state, start of the next search
        private double strain;  // ε0 of the last state

        internal sealed class State
        {
            internal double Curvature, Moment, Mx, My, ReferenceStrain, ChiX, ChiY, TensileBarStrain, BarStrain, ConcreteCompression;
        }

        internal MomentCurvatureDuctilityAnalysis(SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section, double axialForce,
            double directionDegrees, StrainPlane limitPlane, double limitMoment)
        {
            solver = checker.SectionSolver;
            this.section = section;
            solverAxes = solver.SolverAxes;
            forceAxes = checker.SectionCheckerOptions.ForceReferenceCoordinateSystem;
            origin = forceAxes.Origin;
            this.axialForce = axialForce;
            double angle = directionDegrees * Math.PI / 180;
            c = Math.Cos(angle); s = Math.Sin(angle);
            momentScale = Math.Abs(limitMoment);
            bars = section.Rebars.Select(b => b.Position).ToArray();
            outline = section.ConcreteShape.Fill2d.Points.ToArray();
            this.limitPlane = limitPlane;
            psi = Math.Atan2(limitPlane.ChiY, limitPlane.ChiX);
            strain = limitPlane.GetStrain(origin);
        }

        /// <summary>Forces of a plane in the force reference axes: N, moment along the direction and across it.</summary>
        private (double N, double M, double Across, double Mx, double My) Forces(double chi, double angle, double referenceStrain)
        {
            var plane = new StrainPlane(chi * Math.Cos(angle), chi * Math.Sin(angle), origin, referenceStrain);
            ForceTuple local = solver.CalculateStressAnalysisForces(plane);
            var forces = new ResultBeamForces(local.N, 0, 0, 0, local.Mx, local.My, solverAxes).ToCoordinateSystem(forceAxes);
            return (forces.N, forces.M1 * c + forces.M2 * s, -forces.M1 * s + forces.M2 * c, forces.M1, forces.M2);
        }

        /// <summary>ε0 at the origin with N of the plane = the requested N, for the curvature vector (chi; angle): bracket and Illinois.</summary>
        private double ReferenceStrainAtN(double chi, double angle, double start)
        {
            double F(double e) => Forces(chi, angle, e).N - axialForce;
            double lo = start, hi = start, flo = F(lo), fhi = flo, step = 1e-4;
            if (double.IsNaN(flo)) throw new ArgumentException("Curvature control: integration failed.");
            if (flo == 0) return lo;
            // N grows with ε0 (tension positive): walk towards the sign change
            for (int i = 0; i < 60 && Math.Sign(flo) == Math.Sign(fhi); i++)
            {
                if (flo > 0) { hi = lo; fhi = flo; lo -= step; flo = F(lo); }
                else { lo = hi; flo = fhi; hi += step; fhi = F(hi); }
                if (double.IsNaN(flo + fhi)) throw new ArgumentException("Curvature control: integration failed.");
                step *= 2;
            }
            if (Math.Sign(flo) == Math.Sign(fhi)) throw new ArgumentException("Curvature control: N not reached.");
            if (lo > hi) { (lo, hi) = (hi, lo); (flo, fhi) = (fhi, flo); }
            int side = 0;
            for (int i = 0; i < 200; i++)
            {
                double e = (lo * fhi - hi * flo) / (fhi - flo);
                if (!(e > lo && e < hi)) e = (lo + hi) / 2;
                double fe = F(e);
                if (double.IsNaN(fe)) throw new ArgumentException("Curvature control: integration failed.");
                if (fe == 0 || hi - lo < 1e-15) return e;
                if (Math.Sign(fe) == Math.Sign(flo)) { lo = e; flo = fe; if (side == -1) fhi /= 2; side = -1; }
                else { hi = e; fhi = fe; if (side == 1) flo /= 2; side = 1; }
                if (Math.Abs(fe) <= 1e-9 * Math.Max(1, Math.Abs(axialForce)) + 1e-6) return e;
            }
            return (lo + hi) / 2;
        }

        /// <summary>State at curvature chi with the moment in the direction of the curve: direction of the curvature vector by the secant method on the
        /// moment across the direction, ε0 from N at each trial.</summary>
        internal State At(double chi, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            double tolerance = 1e-8 * momentScale;
            double a0 = psi, e0 = ReferenceStrainAtN(chi, a0, strain);
            var f0 = Forces(chi, a0, e0);
            double a = a0, e = e0; var f = f0;
            if (Math.Abs(f0.Across) > tolerance)
            {
                double a1 = a0 + 1e-4, e1 = ReferenceStrainAtN(chi, a1, e0);
                var f1 = Forces(chi, a1, e1);
                for (int i = 0; i < 50 && Math.Abs(f1.Across) > tolerance && f1.Across != f0.Across; i++)
                {
                    double a2 = a1 - f1.Across * (a1 - a0) / (f1.Across - f0.Across);
                    a0 = a1; f0 = f1; a1 = a2;
                    e1 = ReferenceStrainAtN(chi, a1, e1);
                    f1 = Forces(chi, a1, e1);
                }
                if (Math.Abs(f1.Across) > 1e-5 * momentScale) throw new ArgumentException("Curvature control: moment direction not reached.");
                a = a1; e = e1; f = f1;
            }
            psi = a; strain = e;
            return Of(new StrainPlane(chi * Math.Cos(a), chi * Math.Sin(a), origin, e), chi, f.M, f.Mx, f.My);
        }

        /// <summary>The state of a plane with its moment: strain at the origin, largest tensile and absolute bar strains, largest concrete compression.</summary>
        private State Of(StrainPlane plane, double chi, double moment, double mx, double my)
        {
            double e = plane.GetStrain(origin);
            double tensile = bars.Select(p => plane.GetStrain(p)).DefaultIfEmpty(double.NegativeInfinity).Max();
            double steel = bars.Select(p => Math.Abs(plane.GetStrain(p))).DefaultIfEmpty(0).Max();
            double concrete = Math.Max(0, -outline.Min(v => plane.GetStrain(v)));
            return new State { Curvature = chi, Moment = moment, Mx = mx, My = my, ReferenceStrain = e, ChiX = plane.ChiX, ChiY = plane.ChiY,
                TensileBarStrain = tensile, BarStrain = steel, ConcreteCompression = concrete };
        }

        internal static MomentCurvaturePoint ToPoint(State v, double steelYieldStrain) => new MomentCurvaturePoint
        {
            Moment = v.Moment, Mx = v.Mx, My = v.My, Curvature = v.Curvature, GradientX = v.ChiX, GradientY = v.ChiY, ReferenceStrain = v.ReferenceStrain,
            ConcreteCompressionStrain = v.ConcreteCompression, SteelStrain = v.BarStrain, Yielded = v.BarStrain >= steelYieldStrain, Limit = false
        };

        /// <summary>The ductility from the branch 0 → chiU: sweep, first yield and post-peak reduction by bisection, peak by golden section.</summary>
        internal MomentCurvatureDuctility Calculate(double chiU, double limitMoment, double steelYieldStrain, MomentCurvatureDuctilityRequest r,
            CancellationToken token)
        {
            double peak = Math.Abs(solver.PeakConcreteStrain);
            if (!(peak > 0)) throw new ArgumentException("Curvature ductility: peak strain of the concrete not available.");
            double YieldRatio(State v) => Math.Max(v.TensileBarStrain / steelYieldStrain, v.ConcreteCompression / peak);
            var states = new List<State>(r.CurvatureSteps);
            for (int i = 1; i < r.CurvatureSteps; i++) states.Add(At(chiU * i / r.CurvatureSteps, token));
            states.Add(Of(limitPlane, chiU, limitMoment, limitMoment * c, limitMoment * s));

            // conventional first yield: first sample at or past it, then bisection on the curvature (the state on the yielded side)
            int first = states.FindIndex(v => YieldRatio(v) >= 1);
            if (first < 0) throw new ArgumentException("Curvature ductility: no conventional first yield before the limit point.");
            State yielded = states[first];
            double lo = first == 0 ? 0 : states[first - 1].Curvature, hi = yielded.Curvature;
            psi = Math.Atan2(yielded.ChiY, yielded.ChiX); strain = yielded.ReferenceStrain;
            for (int i = 0; i < r.Bisections; i++)
            {
                var mid = At((lo + hi) / 2, token);
                if (YieldRatio(mid) >= 1) { hi = mid.Curvature; yielded = mid; } else lo = mid.Curvature;
            }

            // peak: largest sample, refined by golden section between its neighbours
            int top = 0;
            for (int i = 1; i < states.Count; i++) if (states[i].Moment > states[top].Moment) top = i;
            State best = states[top];
            if (top < states.Count - 1)
            {
                double a = top == 0 ? 0 : states[top - 1].Curvature, b = states[top + 1].Curvature, g = (Math.Sqrt(5) - 1) / 2;
                psi = Math.Atan2(best.ChiY, best.ChiX); strain = best.ReferenceStrain;
                for (int i = 0; i < r.Bisections; i++)
                {
                    State x1 = At(b - g * (b - a), token), x2 = At(a + g * (b - a), token);
                    if (x1.Moment > best.Moment) best = x1;
                    if (x2.Moment > best.Moment) best = x2;
                    if (x1.Moment > x2.Moment) b = x2.Curvature; else a = x1.Curvature;
                }
            }

            // post-peak reduction to 0.85 Mmax before the limit curvature
            double target = PostPeakFraction * best.Moment, ultimate = chiU;
            var cause = MomentCurvatureUltimate.LimitPoint;
            int drop = states.FindIndex(top, v => v.Moment < target);
            if (drop > top)
            {
                double a = states[drop - 1].Curvature, b = states[drop].Curvature;
                psi = Math.Atan2(states[drop].ChiY, states[drop].ChiX); strain = states[drop].ReferenceStrain;
                for (int i = 0; i < r.Bisections; i++)
                {
                    var mid = At((a + b) / 2, token);
                    if (mid.Moment < target) b = mid.Curvature; else a = mid.Curvature;
                }
                ultimate = b; cause = MomentCurvatureUltimate.PostPeakReduction;
            }

            double yieldCurvature = limitMoment / yielded.Moment * yielded.Curvature;
            return new MomentCurvatureDuctility
            {
                SteelYieldStrain = steelYieldStrain, ConcretePeakStrain = peak,
                FirstYieldMoment = yielded.Moment, FirstYieldCurvature = yielded.Curvature,
                FirstYield = yielded.TensileBarStrain / steelYieldStrain >= yielded.ConcreteCompression / peak ? MomentCurvatureFirstYield.TensionSteel : MomentCurvatureFirstYield.ConcretePeakStrain,
                ResistingMoment = limitMoment, YieldCurvature = yieldCurvature,
                MaximumMoment = best.Moment, MaximumMomentCurvature = best.Curvature,
                LimitCurvature = chiU,
                UltimateCurvature = ultimate, Ultimate = cause, CurvatureDuctility = ultimate / yieldCurvature,
                Points = states.Select(v => ToPoint(v, steelYieldStrain)).ToList().AsReadOnly()
            };
        }
    }
}
