using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.EuroCode
{
    public class EN1993p11Checker : EuroCodeChecker
    {

        public EN1993p11Checker(ISteelSection[] section, BeamResult[] beamResult, EN1993_1Options[] options)
            : base(section, beamResult, options)
        {

        }
        public EN1993p11Checker(ISteelSection[] section, BeamResult[] beamResult, EN1993_1Options[] options, StandardEN1993p11 standard)
            : base(section, beamResult, options, standard)
        {

        }




        public override bool PerformCheck()
        {
            throw new NotImplementedException();
        }




        public class EN1993_1Options : Options
        {
            #region Enumerable

            public enum BuckingCurves
            {
                a,
                b,
                c,
                d
            }

            public enum LoadConditions
            {
                Constant,
                SingleForce,
                NotDirectlyLoaded
            }

            public enum SupportConditions
            {
                HingesAtEnds,
                EndsRestrained,
                OneSideRestrainedOneSideHinged
            }

            #endregion


            #region Variables

            private readonly BuckingCurves _buckingCurve;
            private readonly SupportConditions _supportCondition;
            private readonly LoadConditions _loadCondition;

            #endregion


            #region Properties

            public BuckingCurves BuckingCurve => _buckingCurve;

            public SupportConditions SupportCondition => _supportCondition;

            public LoadConditions LoadCondition => _loadCondition;

            #endregion


            #region Constructor

            public EN1993_1Options(double columnEffectiveLength)
                :base(columnEffectiveLength)
            {

            }

            #endregion


        }
    }
}
