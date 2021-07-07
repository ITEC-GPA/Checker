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
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993p11Checker : EuroCodeChecker
    {

        public EN1993p11Checker(EN1993p11BeamCheckerAttributes[] beamCheckers, Options options) 
            : base(beamCheckers, options)
        {
        }

        public EN1993p11Checker(EN1993p11BeamCheckerAttributes[] beamCheckers, Options options, StandardEN1990 standardEN1990) 
            : base(beamCheckers, options, standardEN1990)
        {
        }


        /*
        public EN1993p11Checker(EN1993p11BeamCheckerAttributes eN1993P11BeamCheckers, ILoadCase[] loadCase)
            : base(eN1993P11BeamCheckers, loadCase)
        {

        }
        public EN1993p11Checker(EN1993p11BeamCheckerAttributes eN1993P11BeamCheckers, ILoadCase[] loadCase, StandardEN1993p11 standard)
            : base(eN1993P11BeamCheckers, loadCase, standard)
        {

        }




        public override void PerformCheck()
        {
            throw new NotImplementedException();
        }




        public class EN1993p11Options : Options
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

            public EN1993p11Options(BuckingCurves buckingCurve, SupportConditions supportCondition, LoadConditions loadCondition, double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1,
                double unbracedLengthFactorAxialBuck2 = 1, double effectiveLengthFactorAxialBuck2 = 1,
                double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1,
                double unbracedLengthFactorCriticalMoment2 = 1, double effectiveLengthFactorCriticalMoment2 = 1)
                :base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1,
                unbracedLengthFactorAxialBuck2, effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck,
                unbracedLengthFactorCriticalMoment1, effectiveLengthFactorCriticalMoment1, unbracedLengthFactorCriticalMoment2, effectiveLengthFactorCriticalMoment2)
            {
                _buckingCurve = buckingCurve;
                _supportCondition = supportCondition;
                _loadCondition = loadCondition;
            }

            #endregion


        }*/

    }
}
