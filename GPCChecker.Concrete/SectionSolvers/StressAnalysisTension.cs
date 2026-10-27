using GPC.Model.Materials;
using System.Collections.Generic;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// The concrete in tension of a nonlinear stress analysis with <see cref="Checkers.SectionChecker.SectionOptions.StressAnalysisConcreteTension"/>
    /// (<see cref="SectionSolver.GetStressAnalysisTension"/>): the descending branch of Collins and Mitchell on the concrete
    /// (<see cref="ConcreteTensionStiffening"/>), or the tension chord (<see cref="ConcreteTensionChord"/>, BUG-TS-2 option 1) in one of its states:
    /// uncracked (concrete linear in tension, bare bars) or cracked (concrete zero in tension, the bars of the effective tension areas at their
    /// equivalent strain, with the ρeff of each bar, index in the rebar order). With Collins and Mitchell the bars keep the strain of the plane, so
    /// the results are the same as before bit for bit.
    /// </summary>
    internal sealed class StressAnalysisTension
    {
        private readonly ConcreteTensionStiffening _stiffening;
        private readonly IReadOnlyDictionary<int, (ConcreteTensionChord Law, double Ratio)> _chord;

        internal StressAnalysisTension(ConcreteTensionStiffening stiffening)
        {
            _stiffening = stiffening;
            CrackingStrain = stiffening.CrackingStrain;
            TensileStrength = stiffening.TensileStrength;
        }

        internal StressAnalysisTension(ConcreteTensionChord chord, bool cracked, IReadOnlyDictionary<int, (ConcreteTensionChord Law, double Ratio)> bars)
        {
            Chord = chord;
            Cracked = cracked;
            _chord = cracked ? bars : new Dictionary<int, (ConcreteTensionChord, double)>();
            CrackingStrain = chord.CrackingStrain;
            TensileStrength = chord.TensileStrength;
        }

        /// <summary>The tension chord (null with Collins and Mitchell)</summary>
        internal ConcreteTensionChord Chord { get; }

        /// <summary>The cracked state of the tension chord</summary>
        internal bool Cracked { get; }

        /// <summary>The bars of the effective tension areas with their law and ρeff (empty with Collins and Mitchell)</summary>
        internal IReadOnlyDictionary<int, (ConcreteTensionChord Law, double Ratio)> ChordBars => _chord ?? new Dictionary<int, (ConcreteTensionChord, double)>();

        /// <summary>εcr</summary>
        internal double CrackingStrain { get; }

        /// <summary>fct</summary>
        internal double TensileStrength { get; }

        /// <summary>The concrete in tension (ε &gt; 0)</summary>
        internal void EvaluateConcrete(double strain, out double stress, out double tangent)
        {
            if (Chord != null)
                Chord.EvaluateConcrete(strain, Cracked, out stress, out tangent);
            else
                _stiffening.Evaluate(strain, out stress, out tangent);
        }

        /// <summary>The concrete stress in tension (ε &gt; 0)</summary>
        internal double GetConcreteStress(double strain)
        {
            EvaluateConcrete(strain, out double stress, out _);
            return stress;
        }

        /// <summary>The drop of the concrete stress at cracking: fct − σ(εcr+) of Collins and Mitchell; none in a state of the tension chord</summary>
        internal double CrackingDrop => Chord != null ? 0 : TensileStrength - GetConcreteStress(CrackingStrain * (1 + 1e-12));

        /// <summary>The strain at which the steel law of the bar is read: the equivalent strain of the tension chord, the strain of the plane otherwise</summary>
        /// <param name="index">The index of the bar in the rebar order</param>
        /// <param name="strain">The strain of the plane at the bar</param>
        internal double BarStrain(int index, double strain)
        {
            return _chord != null && _chord.TryGetValue(index, out var bar) ? bar.Law.EquivalentStrain(strain, bar.Ratio, out _) : strain;
        }
    }
}
