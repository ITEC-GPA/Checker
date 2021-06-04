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
using GPCCheckers.Steel.Generic;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.EuroCode
{
    public class EN1993p11Checker : EuroCodeChecker
    {

        public EN1993p11Checker(EN1993p11BeamChecker[] eN1993P11BeamCheckers, ILoadCase loadCase)
            : base(eN1993P11BeamCheckers, loadCase)
        {

        }
        public EN1993p11Checker(EN1993p11BeamChecker[] eN1993P11BeamCheckers, ILoadCase loadCase, StandardEN1993p11 standard)
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

            public EN1993p11Options(double columnEffectiveLength)
                :base(columnEffectiveLength)
            {

            }

            #endregion


        }
    }
}
