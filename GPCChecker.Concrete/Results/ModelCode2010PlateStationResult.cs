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
    public class ModelCode2010PlateStationResult : PlateStationResults, ISerializable
    {
        #region Constructor

		public ModelCode2010PlateStationResult(IConcreteSection section, ResultPlateForces[] forces, ResultStation station, ILoadCase[] Case,
            StandardEN1992p11 standard, ULSCheckerResultsType uLSCheckerResults, SLSCheckerResultsType[] sLSCheckerResults, 
            PlateCheckerModelCode2010.EN1992p11Options checkerOptions, string name = "", int id = -1) 
            : base(section, forces, station, Case, standard, uLSCheckerResults, sLSCheckerResults, checkerOptions, name, id)
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
