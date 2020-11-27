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

        public Annex(double gm0, double gm1, double gm2)
        {
            Gm0 = gm0;
            Gm1 = gm1;
            Gm2 = gm2;
        }

        public Annex() { }
    }

    public class DefaultAnnex : Annex
    {
        public DefaultAnnex()
        {
            Gm0 = 1.0;
            Gm0 = 1.1;
            Gm0 = 1.25;
        }
    }

    public class ItalyAnnex : Annex
    {
        public ItalyAnnex() {
            Gm0 = 1.05;
            Gm0 = 1.1;
            Gm0 = 1.25;
        }
    }
}
