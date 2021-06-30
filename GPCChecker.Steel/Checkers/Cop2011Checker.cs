using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using System.ComponentModel;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.BeamChecker;
using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.Checkers
{
    public class Cop2011Checker : Checker
    {
        // VARIABILI EREDITATE DA CHECKER
        // BeamChecker[]
        // dentro beamchecker c'è : 
        //                          ISteelSection[] 
        //                          ResultBeamForces[]
        //                          ResultStation[] 
        //                          Checker.Options 
        // BeamCheckerResults[]
        // Standard


        public Cop2011Checker(Cop2011BeamCheckerAttribute[] beamCheckers, ILoadCase loadCase)
            :base(beamCheckers, loadCase)
        {
            _standard = new StandardCopSuos2011();
        }

        public Cop2011Checker(Cop2011BeamCheckerAttribute[] beamCheckers, ILoadCase loadCase, StandardCopSuos2011 standard)
            : base(beamCheckers, loadCase, standard)
        {

        }

        public override void PerformCheck()
        {
            _beamCheckerResults = new BeamCheckerResults[BeamCheckersAttributes.Count()];

            for (int i = 0; i < BeamCheckersAttributes.Count(); i++)
            {
                try
                {
                    Cop2011BeamChecker beamCheckerResults = new Cop2011BeamChecker((Cop2011BeamCheckerAttribute)BeamCheckersAttributes[i], LoadCase, (StandardCopSuos2011)Standard);
                    beamCheckerResults.PerformCheck();
                    _beamCheckerResults[i] = beamCheckerResults;
                }
                catch(Exception ex)
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }
            }
        }


        public class Cop2011Options : Options
        {
            #region Enumerable

            public enum BuckingCurves
            {
                a0,
                a,
                b,
                c,
                d
            }

            /// <summary>
            /// The class of the steel material. See CopSuos2011 table 4.1
            /// </summary>
            public enum SteelClasses
            {
                [Description("Class 1")]
                Class1,
                [Description("Class 2")]
                Class2,
                [Description("Class 3")]
                Class3,
                [Description("Class 1H")]
                Class1H,
            }

            public enum LateralTorsionalBucklingConditions
            {
                [Description("Compressed flange restrained at ends")] 
                Default,
                [Description("Compressed flange fully Restrained")] 
                FullyRestrained,
                [Description("Compressed flange unrestrained")] 
                Unrestrained,
                [Description("Compressed flange unrestrained and under destabilizing loads")] 
                DestabilizingLoad,
            }

            #endregion


            #region Variables

            protected SteelClasses _steelClass;
            protected LateralTorsionalBucklingConditions _lateralTorsionalBucklingConditions;

            #endregion


            #region Properties

            public SteelClasses SteelClass { get => _steelClass; internal set => _steelClass = value; }

            public LateralTorsionalBucklingConditions LateralTorsionalBucklingCondition { get => _lateralTorsionalBucklingConditions; internal set => _lateralTorsionalBucklingConditions = value; }

            #endregion


            #region Constructor

            public Cop2011Options(SteelClasses steelGrade = SteelClasses.Class1, LateralTorsionalBucklingConditions latTorsBucklingCondition = LateralTorsionalBucklingConditions.Default, 
                double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1, 
                double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1, 
                double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1, double unbracedLengthFactorCriticalMoment2 = 1,
                double effectiveLengthFactorCriticalMoment2 = 1, double eqvUniformMomentFactormLT = 1)
                : base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2, 
                      effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 
                      unbracedLengthFactorCriticalMoment1, effectiveLengthFactorCriticalMoment1, unbracedLengthFactorCriticalMoment2, 
                      effectiveLengthFactorCriticalMoment2, eqvUniformMomentFactormLT)
            {
                _steelClass = steelGrade;
                _lateralTorsionalBucklingConditions = latTorsBucklingCondition;
            }

            #endregion


            #region Setter

            public void SetSteelClass(SteelClasses steelGrade)
            {
                _steelClass = steelGrade;
            }

            public void SetLateralTorsionalCondition(LateralTorsionalBucklingConditions lateralTorsionalBucklingConditions)
            {
                _lateralTorsionalBucklingConditions = lateralTorsionalBucklingConditions;
            }

            #endregion
        }



    }
}
