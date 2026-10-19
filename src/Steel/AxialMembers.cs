using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Members
{
    public sealed class AxialMemberCheck
    {
        public string Method { get; }
        public string Reference { get; }
        public double Demand { get; }
        public double? Capacity { get; }
        public double? Utilization { get; }
        public double? PartialFactor { get; }
        public double? CriticalForce { get; }
        public double? NormalizedSlenderness { get; }
        public double? GeometricSlenderness { get; }
        public double? Reduction { get; }
        public string Curve { get; }
        public string Diagnostic { get; }
        public EngineeringOutcome Outcome { get; }
        internal AxialMemberCheck(string method, string reference, double demand, double? capacity,
            double? factor, string diagnostic, double? critical = null, double? normalized = null,
            double? geometric = null, double? reduction = null, string curve = "")
        {
            Method = method; Reference = reference; Demand = demand; Capacity = capacity; PartialFactor = factor;
            Diagnostic = diagnostic; CriticalForce = critical; NormalizedSlenderness = normalized;
            GeometricSlenderness = geometric; Reduction = reduction; Curve = curve;
            Utilization = capacity.HasValue ? demand / capacity.Value : (double?)null;
            Outcome = !Utilization.HasValue ? EngineeringOutcome.NotEvaluated
                : Utilization.Value <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
        }
    }

    /// <summary>Checks of the axial MEMBER only. Connections, anchorages, ground bond and durability are separate.</summary>
    public sealed class AxialSteelResult
    {
        public AxialSteelMember Member { get; }
        /// <summary>N, positive tension.</summary>
        public double NormalForce { get; }
        public int? CompressionClass { get; }
        public IReadOnlyList<AxialMemberCheck> Checks { get; }
        public EngineeringOutcome Outcome { get; }
        internal AxialSteelResult(AxialSteelMember member, double force, int? sectionClass, IList<AxialMemberCheck> checks)
        {
            Member = member; NormalForce = force; CompressionClass = sectionClass;
            Checks = Array.AsReadOnly(checks.ToArray());
            Outcome = Checks.Any(c => c.Outcome == EngineeringOutcome.NotSatisfied) ? EngineeringOutcome.NotSatisfied
                : Checks.Count == 0 || Checks.Any(c => c.Outcome == EngineeringOutcome.NotEvaluated) ? EngineeringOutcome.NotEvaluated : EngineeringOutcome.Satisfied;
        }
    }

    /// <summary>NTC2018, Italy, ordinary static axial structural steel: circular solid bars and CHS classes 1-3.
    /// This explicit path does not use the historical checker's gross-area placeholders for net/effective area.
    /// No action factor, load combination, width or support-spacing conversion is applied here.</summary>
    public static class Ntc2018AxialSteel
    {
        public static AxialSteelResult Check(AxialSteelMember member, double normalForce, StandardEN1993p11 standard)
            => Check(member, normalForce, standard, false);

        /// <summary>compressionWhenUnloaded preserves the intended strut checks at zero force, so an unloaded
        /// construction stage cannot hide missing buckling data or an unsupported compression section class.</summary>
        public static AxialSteelResult Check(AxialSteelMember member, double normalForce, StandardEN1993p11 standard, bool compressionWhenUnloaded)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (!Finite(normalForce)) throw new ArgumentException("A finite design axial force is required.", nameof(normalForce));
            double demand = Math.Abs(normalForce);
            var checks = new List<AxialMemberCheck>(); int? sectionClass = null;
            AxialSteelResult Missing(string diagnostic)
            {
                checks.Add(new AxialMemberCheck("Applicabilità acciaio assiale", "NTC 2018 §4.2", demand, null, null, diagnostic));
                return new AxialSteelResult(member, normalForce, sectionClass, checks);
            }
            var basis = new StandardNTC2018Steel();
            if (standard == null || standard.GetType() != typeof(StandardNTC2018Steel)
                || standard.GammaM0 != basis.GammaM0 || standard.GammaM1 != basis.GammaM1 || standard.GammaM2 != basis.GammaM2
                || standard.AlphaImperfectionFactorForCurveA0 != basis.AlphaImperfectionFactorForCurveA0
                || standard.AlphaImperfectionFactorForCurveA != basis.AlphaImperfectionFactorForCurveA
                || standard.AlphaImperfectionFactorForCurveC != basis.AlphaImperfectionFactorForCurveC || member.IsBridge)
                return Missing("Profilo supportato: NTC 2018, Italia, costruzioni ordinarie; coefficienti del profilo GPC non modificati. Ponti e altre norme richiedono un profilo verificato distinto.");
            if (member.MaterialType != SteelMaterial.SteelTypes.Structural
                || !new[] { 235, 275, 355, 420, 460 }.Contains(member.NominalGrade)
                || member.YieldStrength > member.NominalGrade || string.IsNullOrWhiteSpace(member.MaterialSource))
                return Missing("Servono acciaio strutturale S235/S275/S355/S420/S460 e fonte delle resistenze per lo spessore del prodotto. Armature, funi, barre filettate speciali e acciai da precompressione non sono verificati da questo metodo.");
            if (!member.AxialOnlyConfirmed)
                return Missing("Confermare l'asta soggetta alla sola azione assiale centrata. Eccentricità, flessione e azioni trasversali richiedono una verifica di interazione.");
            if (member.Kind == AxialSteelSectionKind.Other)
                return Missing("Famiglia di sezione non ancora integrata nel percorso assiale: " + member.ShapeType + ".");
            if (normalForce > 0 || normalForce == 0 && !compressionWhenUnloaded)
            {
                Add("Snervamento a trazione", "NTC 2018 §4.2.4.1.2.1 [4.2.6]", member.Area * member.YieldStrength / basis.GammaM0, basis.GammaM0,
                    "Sezione lorda; collegamenti e ancoraggi da verificare separatamente.");
                if (member.NetArea.HasValue)
                {
                    if (string.IsNullOrWhiteSpace(member.NetAreaSource))
                        return Missing("Manca la fonte dell'area netta della sezione forata. Nessuna sostituzione con l'area lorda.");
                    Add("Rottura della sezione netta", "NTC 2018 §4.2.4.1.2.1 [4.2.7]", .9 * member.NetArea.Value * member.UltimateStrength / basis.GammaM2,
                        basis.GammaM2, "Sezione con fori per collegamenti ordinari; " + member.NetAreaSource);
                }
            }
            else
            {
                if (member.NetArea.HasValue)
                    return Missing("La verifica a compressione di sezioni forate richiede i dettagli della bullonatura; non si assume automaticamente la sezione lorda.");
                sectionClass = CompressionClass(member);
                if (sectionClass == 4)
                    return Missing("CHS di classe 4: occorre la resistenza all'instabilità locale del guscio. L'area lorda non sostituisce un'area efficace verificata.");
                if (member.Kind == AxialSteelSectionKind.CircularHollow
                    && member.Formation != Section.FormedTypes.HotFinished && member.Formation != Section.FormedTypes.ColdFormed)
                    return Missing("Indicare la formazione a caldo o a freddo della sezione cava per scegliere la curva d'instabilità.");
                Add("Resistenza a compressione", "NTC 2018 §4.2.4.1.2.2 [4.2.10]", member.Area * member.YieldStrength / basis.GammaM0,
                    basis.GammaM0, "Compressione centrata; sezione di classe " + sectionClass + ".");
                Buckling(member.EffectiveLength1, member.Inertia1, "1");
                Buckling(member.EffectiveLength2, member.Inertia2, "2");
            }
            return new AxialSteelResult(member, normalForce, sectionClass, checks);

            void Add(string method, string reference, double capacity, double factor, string diagnostic)
            {
                if (!Finite(capacity) || capacity <= 0) throw new ArithmeticException("Nonfinite or nonpositive steel resistance.");
                checks.Add(new AxialMemberCheck(method, reference, demand, capacity, factor, diagnostic));
            }
            void Buckling(double? length, double inertia, string axis)
            {
                const string reference = "NTC 2018 §4.2.4.1.3.1 [4.2.42-47], tab. 4.2.VIII";
                string method = "Instabilità flessionale asse " + axis;
                if (!length.HasValue || string.IsNullOrWhiteSpace(member.BucklingLengthSource))
                {
                    checks.Add(new AxialMemberCheck(method, reference, demand, null, basis.GammaM1,
                        "Lunghezza libera d'inflessione e fonte dei vincoli obbligatorie per questo asse.")); return;
                }
                bool coldOrSolid = member.Kind == AxialSteelSectionKind.CircularBar || member.Formation == Section.FormedTypes.ColdFormed;
                string curve = coldOrSolid ? "c" : member.NominalGrade == 460 ? "a0" : "a";
                double alpha = coldOrSolid ? basis.AlphaImperfectionFactorForCurveC
                    : member.NominalGrade == 460 ? basis.AlphaImperfectionFactorForCurveA0 : basis.AlphaImperfectionFactorForCurveA;
                double critical = Math.PI * Math.PI * member.ElasticModulus * inertia / (length.Value * length.Value);
                double normalized = Math.Sqrt(member.Area * member.YieldStrength / critical);
                double geometric = length.Value / Math.Sqrt(inertia / member.Area);
                double phi = .5 * (1 + alpha * (normalized - .2) + normalized * normalized);
                double reduction = normalized <= .2 ? 1 : Math.Min(1, 1 / (phi + Math.Sqrt(phi * phi - normalized * normalized)));
                double capacity = reduction * member.Area * member.YieldStrength / basis.GammaM1;
                if (!Finite(critical) || critical <= 0 || !Finite(normalized) || !Finite(geometric) || !Finite(capacity) || capacity <= 0)
                    throw new ArithmeticException("Axial buckling calculation outside finite numerical range.");
                string note = "Asta circolare, carico centrato, proprietà lorde. " + member.BucklingLengthSource;
                if (geometric > 200) note += " Snellezza geometrica maggiore di 200: valutare la raccomandazione NTC per le membrature primarie (250 per le secondarie).";
                checks.Add(new AxialMemberCheck(method, reference, demand, capacity, basis.GammaM1, note,
                    critical, normalized, geometric, reduction, curve));
            }
        }
        private static int CompressionClass(AxialSteelMember member)
        {
            if (member.Kind == AxialSteelSectionKind.CircularBar) return 1;
            double ratio = member.Diameter / member.Thickness, epsilonSquared = 235 / member.YieldStrength;
            return ratio <= 50 * epsilonSquared ? 1 : ratio <= 70 * epsilonSquared ? 2 : ratio <= 90 * epsilonSquared ? 3 : 4;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
