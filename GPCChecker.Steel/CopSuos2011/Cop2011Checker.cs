using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;
using GPC.Model.Results;
using System.ComponentModel;

namespace GPC.Checkers.Steel.Cop2011
{
    public class Cop2011Checker : Checker
    {
        // VARIABILI EREDITATE DA CHECKER
        // Section
        // Length
        // BeamResult
        // CheckerOptions
        // BeamCheckerResults
        // Standard


        public Cop2011Checker(ISteelSection[] section, BeamResult[] beamResult, Cop2011Options[] options)
            :base(section, beamResult, options)
        {
            Standard = new StandardCopSuos2011();
        }

        public Cop2011Checker(ISteelSection[] section, BeamResult[] beamResult, Cop2011Options[] options, StandardCopSuos2011 standard)
            : base(section, beamResult, options, standard)
        {

        }

        public override bool PerformCheck()
        {
            throw new NotImplementedException();
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
                Class1,
                Class2,
                Class3,
                Class1H,
            }

            public enum LateralTorsionalBucklingConditions
            {
                [Description("Compressed flange restrained at ends")] Default,
                [Description("Compressed flange fully Restrained")] FullyRestrained,
                [Description("Compressed flange unrestrained")] Unrestrained,
                [Description("Compressed flange unrestrained and under destabilizing loads")] DestabilizingLoad,
            }

            #endregion


            #region Variables

            private readonly SteelClasses _steelClass;
            private readonly LateralTorsionalBucklingConditions _lateralTorsionalBucklingConditions;

            #endregion


            #region Properties

            public SteelClasses SteelClass => _steelClass;

            public LateralTorsionalBucklingConditions LateralTorsionalBucklingCondition => _lateralTorsionalBucklingConditions;

            #endregion


            #region Constructor

            public Cop2011Options(double length, SteelClasses steelGrade = SteelClasses.Class1, LateralTorsionalBucklingConditions latTorsBucklingCondition = default, 
                double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1, 
                double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1, 
                double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1, double unbracedLengthFactorCriticalMoment2 = 1,
                double effectiveLengthFactorCriticalMoment2 = 1)
                : base(length, unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2, 
                      effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 
                      unbracedLengthFactorCriticalMoment1, effectiveLengthFactorCriticalMoment1, unbracedLengthFactorCriticalMoment2, effectiveLengthFactorCriticalMoment2)
            {
                _steelClass = steelGrade;
                _lateralTorsionalBucklingConditions = latTorsBucklingCondition;
            }

            #endregion
        }



    }
}
