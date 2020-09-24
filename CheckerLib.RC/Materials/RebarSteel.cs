namespace CheckerLib.RC.Materials
{
    public class RebarSteel
    {
        public readonly double Epsilon0;
        public readonly double ElasticModulusE;
        public readonly double Fyk;

        /// <summary>
        /// </summary>
        /// <param name="epsilon0">yielding strain</param>
        /// <param name="elasticModulusE">Elastic modulus [kPa]</param>
        /// <param name="fyk">yielding stress [kPa]</param>
        public RebarSteel(double epsilon0, double elasticModulusE, double fyk)
        {
            this.Epsilon0 = epsilon0;
            this.ElasticModulusE = elasticModulusE;
            this.Fyk = fyk;
        }
    }
}