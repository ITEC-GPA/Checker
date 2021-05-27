using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;
using GPC.Model.Results;

namespace GPC.Checkers.Steel.CopSuos2011
{
    public class CopSuos2011Checker : Checker
    {
        // VARIABILI EREDITATE DA CHECKER
        // Section
        // Length
        // BeamResult
        // CheckerOptions
        // BeamCheckerResults
        // Standard


        public CopSuos2011Checker(ISteelSection section, BeamResult[] beamResult, CopSuos2011Options[] options)
            :base(section, beamResult, options)
        {
            Standard = new StandardCopSuos2011();
        }

        public CopSuos2011Checker(ISteelSection section, BeamResult[] beamResult, CopSuos2011Options[] options, StandardCopSuos2011 standard)
            : base(section, beamResult, options, standard)
        {

        }

        public override bool PerformCheck()
        {
            throw new NotImplementedException();
        }






        public class CopSuos2011Options : Options
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


            #endregion


            #region Variables

            private readonly double _l0;
            private readonly BuckingCurves _buckingCurve;
            private SteelClasses _steelClass;

            #endregion


            #region Properties

            public double ColumnEffectiveLength => _l0;

            public BuckingCurves BuckingCurve => _buckingCurve;

            public SteelClasses SteelClass => _steelClass;

            #endregion


            #region Constructor

            public CopSuos2011Options(double columnEffectiveLength, BuckingCurves buckingCurve, SteelClasses steelGrade = SteelClasses.Class1)
            {
                _l0 = columnEffectiveLength;
                _buckingCurve = buckingCurve;
                _steelClass = steelGrade;
            }

            #endregion
        }



    }
}
