using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
namespace GPC.Checkers.Glasses.Models
{
    public class ModelOptions
    {
        /// <summary>
        /// Rapresent width of the line load strip used in the eq thickness analysis
        /// </summary>
        public double LineLoadWidthEqThickness { get; set; }

        /// <summary>
        /// Rapresent lenght of the point load square side used in the eq thickness analysis
        /// </summary>
        public double PointLoadWidthEqThickness { get; set; }


        public ModelOptions()
        {
            LineLoadWidthEqThickness = 20;
            PointLoadWidthEqThickness = 20;
        }

    }
}
