using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Steel.EuroCode
{
    public class Annex
    {
        public double Gm0 { get; set; }
        public double Gm1 { get; set; }
        public double Gm2 { get; set; }

        public double LambdaLT0 { get; set; } //EN 1993-1-1:2005
        public double Beta { get; set; } //EN 1993-1-1:2005

        /*public Annex(double gm0, double gm1, double gm2)
        {
            Gm0 = gm0;
            Gm1 = gm1;
            Gm2 = gm2;
            LambdaLT0 = 0.4;
            Beta = 0.75;
        }*/

        public Annex() {
            Gm0 = 1.0;
            Gm1= 1.1;
            Gm2 = 1.25;

            LambdaLT0 = 0.4;
            Beta = 0.75;
        }
    }

    public class ItalyAnnex : Annex
    {
        public ItalyAnnex() {
            Gm0 = 1.05;
            Gm1 = 1.1;
            Gm2 = 1.25;
        }
    }
}
