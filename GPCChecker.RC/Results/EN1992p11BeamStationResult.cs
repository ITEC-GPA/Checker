using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Sections.Concrete;
using GPC.Checkers.RC.Checkers;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a beam station with a given ILoadCase
    /// </summary>

    [Serializable]
    public class EN1992p11BeamStationResult : BeamStationResults, ISerializable
    {
        #region Variables


        #endregion


        #region Properties


        #endregion


        #region Constructor

        protected EN1992p11BeamStationResult(IConcreteSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case,
            EN1992p11Checker.EN1992p11Options checkerOptions, StandardEN1992p11 standard, string name = "") 
            : this(section, forces, station, Case, standard, checkerOptions, name)
        {

        }

        internal EN1992p11BeamStationResult(IConcreteSection section, ResultBeamForces forces, ResultStation station, ILoadCase Case, 
            StandardEN1992p11 standard, EN1992p11Checker.EN1992p11Options checkerOptions, string name = "") 
            : base(section, forces, station, Case, standard, checkerOptions, name)
        {

        }

        #endregion


        #region Public Method

        internal void SetCapacity()
        {

        }

        internal void SetWorkingRatio()
        {

        }        

        internal override double GetMaxWorkingRatio()
        {
            List<double> workingRatioList = new List<double>() { };

            return workingRatioList.Max();
        }

        #endregion
                
    }
}
