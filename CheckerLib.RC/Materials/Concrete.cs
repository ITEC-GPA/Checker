using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckerLib.RC.Materials
{
    public class Concrete
    {
        private readonly double ElasticModulusE;
        private readonly double Fck;

        /// <summary>
        /// </summary>
        /// <param name="elasticModulusE">Secant modulus [kPa]</param>
        /// <param name="fck">cylindric concrete resistance [kPa]</param>
        public Concrete(double elasticModulusE, double fck)
        {
            this.ElasticModulusE = elasticModulusE;
            this.Fck = fck;
        }
    }
}
