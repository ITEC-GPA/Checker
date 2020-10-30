using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Common;
using GPC.Utilities;
using GPC.Model;
using GPC.Geometry;


namespace GPC.Checker.ReinforcedConcrete
{
    public class RCCheckingSection : CheckingSection
    {
        #region FIELD_CONSTRUCTORS
        public RCCheckingSection(double ascissa, RCSection section) : base(ascissa)
        {
            _section = section;
            double xG, yG;
            _section.Shapes.GetAreaBarycentre(out xG, out yG, out _area);
            _barycentre = new Point2d(xG, yG);
        }
        #endregion

        #region FIELD_DECONSTRUCTORS
        #endregion

        #region FIELD_COMMANDS
        #endregion

        #region FIELD_METHODS
        #endregion

        #region FIELD_VARIABLES
        protected RCSection _section;
        protected Point2d _barycentre;
        protected double _area;
        #endregion

        #region FIELD_PROPERTIES
        public RCSection Section => _section;
        public Point2d Barycentre => _barycentre;
        public double Area => _area;
        #endregion
    }
}
