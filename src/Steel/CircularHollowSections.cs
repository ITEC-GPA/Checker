using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.Sections
{
    public sealed class SteelSectionResistanceCheck
    {
        public string Method { get; }
        public string Reference { get; }
        public string Unit { get; }
        public double Demand { get; }
        public double? Capacity { get; }
        public double? Utilization { get; }
        public EngineeringOutcome Outcome { get; }
        public string Diagnostic { get; }
        internal SteelSectionResistanceCheck(string method, string reference, string unit, double demand, double? capacity, string diagnostic)
        {
            Method = method; Reference = reference; Unit = unit; Demand = demand; Capacity = capacity; Diagnostic = diagnostic;
            Utilization = capacity > 0 ? demand / capacity.Value : capacity == 0 && demand == 0 ? 0 : (double?)null;
            if (Utilization.HasValue && (double.IsNaN(Utilization.Value) || double.IsInfinity(Utilization.Value)))
                throw new ArithmeticException("Section utilization exceeds finite arithmetic range.");
            Outcome = !capacity.HasValue ? EngineeringOutcome.NotEvaluated
                : demand <= capacity.Value ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
        }
    }

    /// <summary>Local cross-section resistance only; member instability, joints and shell checks are separate.
    /// Normal force positive tension; all concomitant actions belong to one physical tube, N and Nmm.</summary>
    public sealed class CircularHollowSectionResult
    {
        public CircularHollowSteelSection Section { get; }
        public double NormalForce { get; }
        public double Shear { get; }
        public double Moment { get; }
        public int? SectionClass { get; }
        public double? PartialFactor { get; }
        public double? ShearReduction { get; }
        public double? NeutralAxisDistance { get; }
        public double? AxialEquilibriumResidual { get; }
        public IReadOnlyList<SteelSectionResistanceCheck> Checks { get; }
        public EngineeringOutcome Outcome { get; }
        internal CircularHollowSectionResult(CircularHollowSteelSection section, double normal, double shear, double moment,
            int? sectionClass, double? factor, double? reduction, double? neutralAxis, double? residual, IList<SteelSectionResistanceCheck> checks)
        {
            Section = section; NormalForce = normal; Shear = shear; Moment = moment; SectionClass = sectionClass;
            PartialFactor = factor; ShearReduction = reduction; NeutralAxisDistance = neutralAxis; AxialEquilibriumResidual = residual;
            Checks = Array.AsReadOnly(checks.ToArray());
            Outcome = Checks.Any(c => c.Outcome == EngineeringOutcome.NotSatisfied) ? EngineeringOutcome.NotSatisfied
                : Checks.Count == 0 || Checks.Any(c => c.Outcome == EngineeringOutcome.NotEvaluated) ? EngineeringOutcome.NotEvaluated : EngineeringOutcome.Satisfied;
        }
    }

    /// <summary>NTC2018 ordinary static unperforated CHS classes 1-2, N-M-V in one plane, no torsion.
    /// Exact plastic annulus N-M equilibrium. For high shear, (1-rho) reduces yield over the WHOLE section,
    /// conservatively rather than only Av. This does not assert the exact maximum N-M-V capacity.
    /// Gross E*I remains an analysis input; this local verification does not introduce a plastic hinge.</summary>
    public static class Ntc2018CircularHollowSections
    {
        public static CircularHollowSectionResult Check(CircularHollowSteelSection section, double normalForce, double shear,
            double moment, StandardEN1993p11 standard, CancellationToken cancellation = default)
        {
            if (section == null) throw new ArgumentNullException(nameof(section)); cancellation.ThrowIfCancellationRequested();
            if (!Finite(normalForce) || !Finite(shear) || !Finite(moment)) throw new ArgumentException("Finite concomitant N-M-V actions required.");
            var checks = new List<SteelSectionResistanceCheck>(); int? sectionClass = null;
            CircularHollowSectionResult Missing(string applicabilityNote)
            {
                checks.Add(new SteelSectionResistanceCheck("Applicabilità sezione CHS", "NTC 2018 §4.2", "", 0, null, applicabilityNote));
                return new CircularHollowSectionResult(section, normalForce, shear, moment, sectionClass, null, null, null, null, checks);
            }
            var basis = new StandardNTC2018Steel();
            if (standard == null || standard.GetType() != typeof(StandardNTC2018Steel) || standard.GammaM0 != basis.GammaM0 || section.IsBridge)
                return Missing("Serve il profilo NTC 2018 per costruzioni ordinarie statiche, con coefficienti GPC non modificati.");
            if (section.MaterialType != SteelMaterial.SteelTypes.Structural || !new[] { 235, 275, 355, 420, 460 }.Contains(section.NominalGrade)
                || section.YieldStrength > section.NominalGrade || string.IsNullOrWhiteSpace(section.MaterialSource))
                return Missing("Documentare grado strutturale S235–S460 e resistenza per lo spessore effettivo del prodotto.");
            if (!section.UnperforatedConfirmed) return Missing("Confermare sezione integra senza aperture, fori o riduzioni locali; non si sostituiscono proprietà nette/efficaci con quelle lorde.");
            double epsilon2 = 235 / section.YieldStrength, slenderness = section.Diameter / section.Thickness;
            sectionClass = slenderness <= 50 * epsilon2 ? 1 : slenderness <= 70 * epsilon2 ? 2 : slenderness <= 90 * epsilon2 ? 3 : 4;
            if (sectionClass > 2) return Missing("Percorso plastico limitato alle CHS di classe 1–2; classe " + sectionClass + " richiede un percorso elastico/locale distinto.");
            double gamma = basis.GammaM0, yield = section.YieldStrength / gamma;
            double shearCapacity = 2 * section.Area / Math.PI * yield / Math.Sqrt(3);
            if (!Finite(shearCapacity) || shearCapacity <= 0) throw new ArithmeticException("Invalid finite shear resistance.");
            double shearRatio = Math.Abs(shear) / shearCapacity;
            double reduction = shearRatio <= .5 ? 0 : shearRatio >= 1 ? 1 : Math.Pow(2 * shearRatio - 1, 2);
            double reducedYield = yield * (1 - reduction), normalCapacity = section.Area * reducedYield;
            double radius = section.Diameter / 2, inner = radius - section.Thickness, axis = radius, residual = 0, momentCapacity = 0;
            string note = reduction > 0 ? "Riduzione per taglio applicata cautelativamente a tutta la sezione. " : "Taglio non superiore a metà Vrd. ";
            if (normalCapacity > 0 && Math.Abs(normalForce) < normalCapacity)
            {
                double low = 0, high = radius;
                if (normalForce == 0) axis = 0;
                else
                {
                    // Absolute neutral-axis distance: symmetry makes resistance independent of N and M signs.
                    for (int i = 0; i < 80; i++)
                    {
                        cancellation.ThrowIfCancellationRequested(); axis = (low + high) / 2;
                        double force = reducedYield * (section.Area - 2 * (CapArea(radius, axis) - CapArea(inner, axis)));
                        if (force < Math.Abs(normalForce)) low = axis; else high = axis;
                    }
                }
                residual = reducedYield * (section.Area - 2 * (CapArea(radius, axis) - CapArea(inner, axis))) - Math.Abs(normalForce);
                if (Math.Abs(residual) > Math.Max(1e-8, normalCapacity * 1e-10))
                    throw new ArithmeticException("Plastic neutral-axis equilibrium did not converge.");
                momentCapacity = 4.0 / 3 * reducedYield * (Math.Pow(Math.Max(0, radius * radius - axis * axis), 1.5)
                    - Math.Pow(Math.Max(0, inner * inner - axis * axis), 1.5));
            }
            else residual = normalCapacity - Math.Abs(normalForce);
            if (!Finite(normalCapacity) || !Finite(momentCapacity)) throw new ArithmeticException("Cross-section resistance exceeds finite arithmetic range.");
            checks.Add(new SteelSectionResistanceCheck("Taglio CHS", "NTC 2018 §4.2.4.1.2.4 [4.2.17, 4.2.23]", "N", Math.Abs(shear), shearCapacity,
                "Av=2A/π; torsione, instabilità del guscio e zone di introduzione dei carichi richiedono verifiche distinte."));
            checks.Add(new SteelSectionResistanceCheck("Sforzo normale con taglio", "NTC 2018 §4.2.4.1.2.9 [4.2.40]", "N", Math.Abs(normalForce), normalCapacity, note));
            checks.Add(new SteelSectionResistanceCheck("Flessione con N e V concomitanti", "NTC 2018 §4.2.4.1.2, §4.2.4.1.2.9", "Nmm", Math.Abs(moment), momentCapacity,
                note + "Equilibrio plastico dell'anello. Verifica locale della sezione; stabilità della membratura, collegamenti e pressioni radiali separati."));
            return new CircularHollowSectionResult(section, normalForce, shear, moment, sectionClass, gamma, reduction, axis, residual, checks);
        }
        private static double CapArea(double radius, double axis)
        {
            if (axis >= radius) return 0;
            double theta = Math.Acos(axis / radius);
            return radius * radius * (theta - Math.Sin(theta) * Math.Cos(theta));
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
