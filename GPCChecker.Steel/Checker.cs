using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections;
using GPC.Model.Results;
using GPC.Checkers.Steel.Results;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel
{
    public abstract class Checker
    {
        #region Variables

        private readonly ISteelSection _section;  
        private readonly BeamResult[] _result;
        private readonly Options _options;
        private readonly BeamCheckerResults[] _beamCheckerResults;
        private readonly Standard _standard;

        #endregion

            
        #region Properties

        public ISteelSection Section => _section;

        public double Length { get => BeamResult.Length; set => Length = value; }

        public BeamResult[] BeamResult => _result;

        public Options CheckerOptions => _options;

        public BeamCheckerResults[] BeamCheckerResults => _beamCheckerResults;

        public Standard Standard { get => _standard; set => Standard = value; }

        #endregion


        #region Constructor

        public Checker(ISteelSection section, BeamResult[] beamResult, Options options, Standard standard)
        {
            _section = section;
            Length = beamResult.Length < 0 ? throw new ArgumentException($"Length cannot be lower than zero") : Length;
            if (beamResult.Length < 1)
                throw new ArgumentException("BeamResult can not be null");
            _result = beamResult;
            _options = options;
            _standard = standard;
        }

        public Checker(ISteelSection section, BeamResult[] beamResult, Options options)
        {
            _section = section;
            Length = beamResult.Length < 0 ? throw new ArgumentException($"Length cannot be lower than zero") : Length;
            if (beamResult.Length < 1)
                throw new ArgumentException("BeamResult can not be null");
            _result = beamResult;
            _options = options;
        }

        #endregion



        #region Public abstract method

        public abstract bool PerformCheck();

        #endregion


        public abstract class Options
        {

        }


        #region Nested class: SectionProperties

        public abstract class SectionProperties
        {
            #region Variables

            private readonly ISteelSection _section;
            private readonly Options _options;

            #endregion


            #region Properties

            public ISteelSection Section => _section;            

            public Checker.Options Options => _options;

            #endregion


            public SectionProperties(ISteelSection section, Checker.Options standard)
            {
                _section = section;
                _options = standard;
            }


            public virtual double MinSigma(double N, double M2, double M1)
            {
                if (Section is SectionCHS sectionCHS)
                {
                    double sigmaN = N / sectionCHS.Area;
                    double M = Math.Sqrt(M1 * M1 + M2 * M2);
                    double sigmaM = -M / Wel22Min;

                    return sigmaN + sigmaM;
                }

                else if (Section is SectionH sectionH)
                {
                    double sigmap1 = N / sectionH.Area - M2 / _wel22Top + M1 / _j11 * sectionH.LenghtTopFlange / 2.0;
                    double sigmap2 = N / sectionH.Area - M2 / _wel22Top - M1 / _j11 * sectionH.LenghtTopFlange / 2.0;
                    double sigmap3 = N / sectionH.Area + M2 / _wel22Bottom + M1 / _j11 * sectionH.LenghtBottomFlange / 2.0;
                    double sigmap4 = N / sectionH.Area + M2 / _wel22Bottom - M1 / _j11 * sectionH.LenghtBottomFlange / 2.0;

                    double sigmaMin = Math.Min(sigmap1, sigmap2);
                    sigmaMin = Math.Min(sigmaMin, sigmap3);
                    sigmaMin = Math.Min(sigmaMin, sigmap4);

                    return sigmaMin;
                }

                else if (Section is SectionRHS sectionRHS)
                {
                    double sigma1 = N / sectionRHS.Area - M2 / J22 * (sectionRHS.H - _centroid.Y) + M1 / J11 * (_centroid.X);
                    double sigma2 = N / sectionRHS.Area - M2 / J22 * (sectionRHS.H - _centroid.Y) - M1 / J11 * (sectionRHS.B - _centroid.X);
                    double sigma3 = N / sectionRHS.Area + M2 / J22 * (_centroid.Y) + M1 / J11 * (_centroid.X);
                    double sigma4 = N / sectionRHS.Area + M2 / J22 * (_centroid.Y) - M1 / J11 * (sectionRHS.B - _centroid.X);

                    double sigmaMin = Math.Min(sigma1, sigma2);
                    sigmaMin = Math.Min(sigmaMin, sigma3);
                    sigmaMin = Math.Min(sigmaMin, sigma4);

                    return sigmaMin;
                }
                else
                    throw new NotImplementedException();
            }

        }

        #endregion
    }


}
