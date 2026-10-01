using System;
using System.Collections.Generic;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Shear
{
    /// <summary>
    /// Shear check of a section in one direction. Units: N and V in N (N compression negative, as in the section solver),
    /// M in Nmm, lengths mm, areas mm², stresses MPa, angles degrees. Design strengths and γc are explicit inputs, so that
    /// their provenance (standard, national annex, overrides) stays with the caller.
    /// </summary>
    public sealed class SectionShearInput
    {
        public Standard Standard { get; }
        /// <summary>Axial force, N; compression negative.</summary>
        public double N { get; }
        /// <summary>Shear force in the checked direction, N. The sign is kept; the methods use |V|.</summary>
        public double V { get; }
        /// <summary>Concomitant bending moment about the axis normal to V, Nmm (Model Code 2010 only).</summary>
        public double M { get; }
        /// <summary>Gross concrete area Ac, mm².</summary>
        public double Area { get; }
        /// <summary>Web width bw orthogonal to V, mm.</summary>
        public double Bw { get; }
        /// <summary>Effective depth d along V, mm.</summary>
        public double D { get; }
        /// <summary>Anchored longitudinal tension reinforcement Asl, mm².</summary>
        public double Asl { get; }
        public double Fck { get; }
        public double Fcd { get; }
        public double Fyd { get; }
        public double GammaC { get; }
        /// <summary>Elastic modulus of the bars, MPa (Model Code 2010 strain).</summary>
        public double Es { get; }
        /// <summary>Area of all legs of one shear-reinforcement layer, mm²; 0 = member without shear reinforcement.</summary>
        public double Asw { get; }
        /// <summary>Spacing of the shear reinforcement, mm.</summary>
        public double Spacing { get; }
        /// <summary>Inclination of the shear reinforcement α, degrees in [45; 90].</summary>
        public double AlphaDegrees { get; }
        /// <summary>Assigned cot θ; null = the method chooses it within its admissible range.</summary>
        public double? CotTheta { get; }
        /// <summary>Lever arm ratio z/d in (0; 0.9].</summary>
        public double LeverFactor { get; }
        /// <summary>Maximum aggregate size dg, mm (Model Code 2010 and NS).</summary>
        public double Aggregate { get; }
        /// <summary>Axial-force eccentricity Δe of Model Code 2010, mm.</summary>
        public double AxialEccentricity { get; }
        /// <summary>Characteristic ultimate residual tensile strength fFtuk of fibre-reinforced concrete, MPa (CNR-DT 204); 0 for plain concrete.</summary>
        public double ResidualTensileStrength { get; }
        /// <summary>Characteristic tensile strength fctk (5% fractile) of the concrete matrix, MPa (CNR-DT 204).</summary>
        public double MatrixTensileStrength { get; }

        public SectionShearInput(Standard standard, double n, double v, double m, double area, double bw, double d, double asl,
            double fck, double fcd, double fyd, double gammaC, double es, double asw, double spacing,
            double alphaDegrees = 90, double? cotTheta = null, double leverFactor = .9, double aggregate = 20, double axialEccentricity = 0,
            double residualTensileStrength = 0, double matrixTensileStrength = 0)
        {
            Standard = standard ?? throw new ArgumentNullException(nameof(standard));
            N = n; V = v; M = m; Area = area; Bw = bw; D = d; Asl = asl; Fck = fck; Fcd = fcd; Fyd = fyd; GammaC = gammaC; Es = es;
            Asw = asw; Spacing = spacing; AlphaDegrees = alphaDegrees; CotTheta = cotTheta; LeverFactor = leverFactor;
            Aggregate = aggregate; AxialEccentricity = axialEccentricity;
            ResidualTensileStrength = residualTensileStrength; MatrixTensileStrength = matrixTensileStrength;
        }
    }

    public enum ShearVerdict { Satisfied, NotSatisfied, NotEvaluated }

    /// <summary>Intermediate value of the calculation (symbol, value, unit, expression).</summary>
    public sealed class ShearCalculationDetail
    {
        public string Symbol { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Expression { get; }
        internal ShearCalculationDetail(string symbol, double value, string unit, string expression) { Symbol = symbol; Value = value; Unit = unit; Expression = expression; }
    }

    /// <summary>
    /// Resistances in N: VRsd (shear reinforcement), VRcd (concrete strut, or the minimum value without shear reinforcement),
    /// VRd. Ratio = |V|/VRd, null when not defined. A zero resistance with a nonzero demand is NotSatisfied, never a zero ratio.
    /// </summary>
    public sealed class SectionShearResult
    {
        public ShearProfile Profile { get; internal set; }
        public double VRsd { get; internal set; }
        public double VRcd { get; internal set; }
        public double VRd { get; internal set; }
        public double Demand { get; internal set; }
        public double? Ratio { get; internal set; }
        public double CotTheta { get; internal set; }
        public ShearVerdict Verdict { get; internal set; }
        public string Status { get; internal set; }
        public string Model { get; internal set; }
        public string Reference { get; internal set; }
        public bool WithShearReinforcement { get; internal set; }
        public IReadOnlyList<ShearCalculationDetail> Details { get; internal set; } = new ShearCalculationDetail[0];
    }
}
