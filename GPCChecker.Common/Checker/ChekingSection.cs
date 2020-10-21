using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Common
{
    public abstract class CheckingSection
    {    
        #region FIELD_CONSTRUCTORS
        protected CheckingSection(double ascissa)
        {
            _ascissa = ascissa;
        }
        #endregion

        #region FIELD_DECONSTRUCTORS
        #endregion

        #region FIELD_COMMANDS
        #endregion

        #region FIELD_METHODS
        #endregion

        #region FIELD_VARIABLES
        protected double _ascissa;
        #endregion

        #region FIELD_PROPERTIES
        public double Ascissa => _ascissa; 
        #endregion
    }
}
