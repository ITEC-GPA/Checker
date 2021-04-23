using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Models
{
    public class ModelOptions
    {
        public enum GravityAxes
        {
            X, 
            Y, 
            Z,
        }

        public GravityAxes GravityAxis { get; set; }

        public ModelOptions()
        {
            GravityAxis = GravityAxes.Z;
        }


    }
}
