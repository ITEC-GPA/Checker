using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using System.Runtime.Serialization;
using GPC.Model.Sections.Concrete;
using GPC.Checkers.Concrete.Checkers;

namespace GPC.Checkers.Concrete.Results
{
	/// <summary>
	/// This class contains the result of a check performed on a beam station with a given ILoadCase
	/// </summary>

	[Serializable]
    public class ModelCode2010BeamStationResult : BeamStationResults, ISerializable
    {
        #region Variables


        #endregion


        #region Properties


        #endregion


        #region Constructor

		public ModelCode2010BeamStationResult(IConcreteSection section, ResultStation station, ResultBeamForces[] forces, ILoadCase[] Case,
            StandardEN1992p11 standard, ULSCheckerResults uLSCheckerResults, SLSCheckerResults[] sLSCheckerResults, 
            ModelCode2010BeamChecker.ModelCode2010Options checkerOptions, string name = "", int id = -1) 
            : base(section, station, forces, Case, standard, uLSCheckerResults, sLSCheckerResults, checkerOptions, name, id)
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
