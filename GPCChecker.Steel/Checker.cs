using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Checkers.Steel.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel
{
    public abstract class Checker
    {
        #region Variables

        protected readonly BeamCheckerAttributes[] _beamCheckersAttributes;
        protected BeamStationCheckerResults[] _beamStationCheckerResults;
        protected Standard _standard;
        protected Options _options;

        #endregion


        #region Properties

        public BeamCheckerAttributes[] BeamCheckersAttribute => _beamCheckersAttributes;

        public BeamStationCheckerResults[] BeamStationCheckerResults => _beamStationCheckerResults; 

        public Standard Standard => _standard;

        public Options CheckerOptions => _options;

        public double BeamLength => _beamCheckersAttributes.FirstOrDefault().Station.ElementLenght;

        public LoadCase[] LoadCases => GetLoadCases();

        #endregion


        #region Constructor

        public Checker(BeamCheckerAttributes[] beamCheckers, Options options, Standard standard)
            : this(beamCheckers, options)
        {
            if (beamCheckers is null)
                throw new ArgumentNullException(nameof(beamCheckers));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
        }

        public Checker(BeamCheckerAttributes[] beamCheckers, Options options)
        {
            _beamCheckersAttributes = beamCheckers ?? throw new ArgumentNullException(nameof(beamCheckers));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        #endregion



        #region Public abstract method

        public abstract void PerformCheck();

        #endregion

        public double GetLenghtAxialBuckling1()
        {
            return BeamLength * CheckerOptions.UnbracedLengthFactorAxialBuck1 * CheckerOptions.EffectiveLengthFactorAxialBuck1;
        }

        public double GetLenghtAxialBuckling2()
        {
            return BeamLength * CheckerOptions.UnbracedLengthFactorAxialBuck2 * CheckerOptions.EffectiveLengthFactorAxialBuck2;
        }

        public double GetEffectiveLenghtAxialBuckling1()
        {
            return BeamLength * CheckerOptions.EffectiveLengthFactorAxialBuck1;
        }

        public double GetEffectiveLenghtAxialBuckling2()
        {
            return BeamLength * CheckerOptions.EffectiveLengthFactorAxialBuck2;
        }

        public double GetLenghtLatTorsBuckling()
        {
            return BeamLength * CheckerOptions.UnbracedLengthFactorLatTorsBuck * CheckerOptions.EffectiveLengthFactorLatTorsBuck;
        }

        public double GetLenghtCriticalMoment1()
        {
            return BeamLength * CheckerOptions.UnbracedLengthFactorCriticalMoment1 * CheckerOptions.EffectiveLengthFactorCriticalMoment1;
        }

        public double GetLenghtCriticalMoment2()
        {
            return BeamLength * CheckerOptions.UnbracedLengthFactorCriticalMoment2 * CheckerOptions.EffectiveLengthFactorCriticalMoment2;
        }

        private LoadCase[] GetLoadCases()
        {
            List<LoadCase> loadCases = new List<LoadCase>();

            for (int i = 0; i < _beamCheckersAttributes.Count(); i++)
                for (int j = 0; j < _beamCheckersAttributes[i].BeamResults.Count(); j++)
                    loadCases.Add((LoadCase)_beamCheckersAttributes[i].BeamResults[j].Case);

            return loadCases.ToArray();
        }

        internal virtual double MinSigma(ISteelSection section, double N, double M2, double M1)
        {
            if (section is SectionCHS sectionCHS)
            {
                double sigmaN = N / sectionCHS.Area;
                double M = Math.Sqrt(M1 * M1 + M2 * M2);
                double sigmaM = -M / sectionCHS.CalculateWel();

                return sigmaN + sigmaM;
            }
            else if (section is SectionH sectionH)
            {
                double sigmap1 = N / sectionH.Area - M2 / sectionH.CalculateWelyTop() +
                                M1 / sectionH.J11 * sectionH.LenghtTopFlange / 2.0;
                double sigmap2 = N / sectionH.Area - M2 / sectionH.CalculateWelyTop() -
                                M1 / sectionH.J11 * sectionH.LenghtTopFlange / 2.0;
                double sigmap3 = N / sectionH.Area + M2 / sectionH.CalculateWelyBottom() +
                                M1 / sectionH.J11 * sectionH.LenghtBottomFlange / 2.0;
                double sigmap4 = N / sectionH.Area + M2 / sectionH.CalculateWelyBottom() -
                                M1 / sectionH.J11 * sectionH.LenghtBottomFlange / 2.0;

                double sigmaMin = Math.Min(sigmap1, sigmap2);
                sigmaMin = Math.Min(sigmaMin, sigmap3);
                sigmaMin = Math.Min(sigmaMin, sigmap4);

                return sigmaMin;
            }
            else if (section is SectionRHS sectionRHS)
            {
                double sigmae1 = N / sectionRHS.Area - M2 / sectionRHS.J22 * (sectionRHS.DistanceYCentroidFromTop()) +
                                M1 / sectionRHS.J11 * (sectionRHS.DistanceXCentroidFromLeft());
                double sigmae2 = N / sectionRHS.Area - M2 / sectionRHS.J22 * (sectionRHS.DistanceYCentroidFromTop()) -
                                M1 / sectionRHS.J11 * (sectionRHS.DistanceXCentroidFromRight());
                double sigmae3 = N / sectionRHS.Area + M2 / sectionRHS.J22 * (sectionRHS.DistanceYCentroidFromBottom()) +
                                M1 / sectionRHS.J11 * (sectionRHS.DistanceXCentroidFromLeft());
                double sigmae4 = N / sectionRHS.Area + M2 / sectionRHS.J22 * (sectionRHS.DistanceYCentroidFromBottom()) -
                                M1 / sectionRHS.J11 * (sectionRHS.DistanceXCentroidFromRight());

                double sigmaMin = Math.Min(sigmae1, sigmae2);
                sigmaMin = Math.Min(sigmaMin, sigmae3);
                sigmaMin = Math.Min(sigmaMin, sigmae4);

                return sigmaMin;
            }
            else if (section is SectionC sectionC)
            {
                if (sectionC.IsSymmetricAlongXLocalAxis)
                {
                    double sigmaP1 = N / sectionC.Area - M2 / sectionC.J22 * (sectionC.DistanceYCentroidFromTop()) +
                                    M1 / sectionC.J11 * (sectionC.DistanceXCentroidFromLeft());
                    double sigmaP2 = N / sectionC.Area - M2 / sectionC.J22 * (sectionC.DistanceYCentroidFromTop()) -
                                    M1 / sectionC.J11 * (sectionC.DistanceXCentroidFromRight());
                    double sigmaP3 = N / sectionC.Area + M2 / sectionC.J22 * (sectionC.DistanceYCentroidFromBottom()) +
                                    M1 / sectionC.J11 * (sectionC.DistanceXCentroidFromLeft());
                    double sigmaP4 = N / sectionC.Area + M2 / sectionC.J22 * (sectionC.DistanceYCentroidFromBottom()) -
                                    M1 / sectionC.J11 * (sectionC.DistanceXCentroidFromRight());

                    double sigmaMin = Math.Min(sigmaP1, sigmaP2);
                    sigmaMin = Math.Min(sigmaMin, sigmaP3);
                    sigmaMin = Math.Min(sigmaMin, sigmaP4);

                    return sigmaMin;
                }
                else
                    throw new Exception("calculation of unequal C not yet supported");

            }
            else if (section is SectionT sectionT)
            {
                double sigmaP1 = N / sectionT.Area - M2 / sectionT.CalculateWelxTop() + M1 / sectionT.CalculateWelyLeft();
                double sigmaP2 = N / sectionT.Area - M2 / sectionT.CalculateWelxTop() - M1 / sectionT.CalculateWelyRight();
                double sigmaP3 = N / sectionT.Area + M2 / sectionT.CalculateWelxBottom();

                double sigmaMin = Math.Min(sigmaP1, sigmaP2);
                sigmaMin = Math.Min(sigmaMin, sigmaP3);

                return sigmaMin;
            }
            else
                throw new NotImplementedException();
        }

        public abstract class Options
        {
            #region Variables

            protected double _kAxialBuckling1;
            protected double _kAxialBuckling2;
            protected double _kLatTorsBuckling;
            protected double _kCriticalMoment1;
            protected double _kCriticalMoment2;
            protected double _mAxialBuckling1;
            protected double _mAxialBuckling2;
            protected double _mLatTorsBuckling;
            protected double _mCriticalMoment1;
            protected double _mCriticalMoment2;
            protected double _m1;
            protected double _m2;
            protected double _mLT;

            #endregion


            #region Properties

            /// <summary>
            /// Unbraced length factor for axial buckling about the frame object 1-axis
            /// </summary>
            public double UnbracedLengthFactorAxialBuck1 { get => _kAxialBuckling1; set => _kAxialBuckling1 = value; }

            /// <summary>
            /// Unbraced length factor for buckling about the frame object 2-axis
            /// </summary>
            public double UnbracedLengthFactorAxialBuck2 { get => _kAxialBuckling2; set => _kAxialBuckling2 = value; }

            /// <summary>
            /// Unbraced length factor for lateral torsional buckling
            /// </summary>
            public double UnbracedLengthFactorLatTorsBuck { get => _kLatTorsBuckling; set => _kLatTorsBuckling = value; }

            /// <summary>
            /// Unbraced length factor for critical moment about the frame object 1-axis
            /// </summary>
            public double UnbracedLengthFactorCriticalMoment1 { get => _kCriticalMoment1; set => _kCriticalMoment1 = value; }

            /// <summary>
            /// Unbraced length factor for critical moment about the frame object 2-axis
            /// </summary>
            public double UnbracedLengthFactorCriticalMoment2 { get => _kCriticalMoment2; set => _kCriticalMoment2 = value; }

            /// <summary>
            /// Effective length factor for axial buckling about the frame object 1-axis
            /// </summary>
            public double EffectiveLengthFactorAxialBuck1 { get => _mAxialBuckling1; set => _mAxialBuckling1 = value; }

            /// <summary>
            /// Effective length factor for axial buckling about the frame object 2-axis
            /// </summary>
            public double EffectiveLengthFactorAxialBuck2 { get => _mAxialBuckling2; set => _mAxialBuckling2 = value; }

            /// <summary>
            /// Effective length factor for lateral torsional buckling
            /// </summary>
            public double EffectiveLengthFactorLatTorsBuck { get => _mLatTorsBuckling; set => _mLatTorsBuckling = value; }

            /// <summary>
            /// Effective length factor for critical moment about the frame object 1-axis
            /// </summary>
            public double EffectiveLengthFactorCriticalMoment1 { get => _mCriticalMoment1; set => _mCriticalMoment1 = value; }

            /// <summary>
            /// Effective length factor for critical moment about the frame object 2-axis
            /// </summary>
            public double EffectiveLengthFactorCriticalMoment2 { get => _mCriticalMoment2; set => _mCriticalMoment2 = value; }

            /// <summary>
            /// Equivalent uniform moment factor for lateral torsional buckling 
            /// </summary>
            public double UniformMomentFactormLT { get => _mLT; set => _mLT = value; }

            /// <summary>
            /// Equivalent uniform moment factor for lateral torsional buckling 
            /// </summary>
            public double UniformMomentFactorm1 { get => _m1; set => _m1 = value; }

            /// <summary>
            /// Equivalent uniform moment factor for lateral torsional buckling 
            /// </summary>
            public double UniformMomentFactorm2 { get => _m2; set => _m2 = value; }

            #endregion


            #region Constructor

            public Options(double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1,
                            double unbracedLengthFactorAxialBuck2 = 1, double effectiveLengthFactorAxialBuck2 = 1,
                            double unbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                            double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1,
                            double unbracedLengthFactorCriticalMoment2 = 1, double effectiveLengthFactorCriticalMoment2 = 1,
                            double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1,
                            double eqvUniformMomentFactormLT = 1)
            {
                if (unbracedLengthFactorAxialBuck1 < 0)
                    throw new ArgumentException("UnbracedLengthFactorAxialBuck1 must be positive");
                _kAxialBuckling1 = unbracedLengthFactorAxialBuck1;

                if (unbracedLengthFactorAxialBuck2 < 0)
                    throw new ArgumentException("UnbracedLengthFactorAxialBuck2 must be positive");
                _kAxialBuckling2 = unbracedLengthFactorAxialBuck2;

                if (unbracedLengthFactorLatTorsBuck < 0)
                    throw new ArgumentException("UnbracedLengthFactorLatTorsBuck must be positive");
                _kLatTorsBuckling = unbracedLengthFactorLatTorsBuck;

                if (unbracedLengthFactorCriticalMoment1 < 0)
                    throw new ArgumentException("UnbracedLengthFactorCriticalMoment1 must be positive");
                _kCriticalMoment1 = unbracedLengthFactorCriticalMoment1;

                if (unbracedLengthFactorCriticalMoment2 < 0)
                    throw new ArgumentException("UnbracedLengthFactorCriticalMoment2 must be positive");
                _kCriticalMoment2 = unbracedLengthFactorCriticalMoment2;

                if (effectiveLengthFactorAxialBuck1 < 0)
                    throw new ArgumentException("EffectiveLengthFactorAxialBuck1 must be positive");
                _mAxialBuckling1 = effectiveLengthFactorAxialBuck1;

                if (effectiveLengthFactorAxialBuck2 < 0)
                    throw new ArgumentException("EffectiveLengthFactorAxialBuck2 must be positive");
                _mAxialBuckling2 = effectiveLengthFactorAxialBuck2;

                if (effectiveLengthFactorLatTorsBuck < 0)
                    throw new ArgumentException("EffectiveLengthFactorLatTorsBuck must be positive");
                _mLatTorsBuckling = effectiveLengthFactorLatTorsBuck;

                if (effectiveLengthFactorCriticalMoment1 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment1 must be positive");
                _mCriticalMoment1 = effectiveLengthFactorCriticalMoment1;

                if (effectiveLengthFactorCriticalMoment2 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
                _mCriticalMoment2 = effectiveLengthFactorCriticalMoment2;

                if (eqvUniformMomentFactorm1 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
                _m1 = eqvUniformMomentFactorm1;

                if (eqvUniformMomentFactorm2 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
                _m2 = eqvUniformMomentFactorm2;

                if (eqvUniformMomentFactormLT < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
                _mLT = eqvUniformMomentFactormLT;

            }


            #endregion


            #region Setter

            public void SetUnbracedLengthFactorAxialBuck1(double unbracedLengthFactorAxialBuck1)
            {
                if (unbracedLengthFactorAxialBuck1 < 0)
                    throw new ArgumentException("UnbracedLengthFactorAxialBuck1 must be positive");
                _kAxialBuckling1 = unbracedLengthFactorAxialBuck1;
            }

            public void SetUnbracedLengthFactorAxialBuck2(double unbracedLengthFactorAxialBuck2)
            {
                if (unbracedLengthFactorAxialBuck2 < 0)
                    throw new ArgumentException("UnbracedLengthFactorAxialBuck2 must be positive");
                _kAxialBuckling2 = unbracedLengthFactorAxialBuck2;
            }

            public void SetUnbracedLengthFactorLatTorsBuck(double unbracedLengthFactorLatTorsBuck)
            {
                if (unbracedLengthFactorLatTorsBuck < 0)
                    throw new ArgumentException("UnbracedLengthFactorLatTorsBuck must be positive");
                _kLatTorsBuckling = unbracedLengthFactorLatTorsBuck;
            }

            public void SetUnbracedLengthFactorCriticalMoment1(double unbracedLengthFactorCriticalMoment1)
            {
                if (unbracedLengthFactorCriticalMoment1 < 0)
                    throw new ArgumentException("UnbracedLengthFactorCriticalMoment1 must be positive");
                _kCriticalMoment1 = unbracedLengthFactorCriticalMoment1;
            }

            public void SetUnbracedLengthFactorCriticalMoment2(double unbracedLengthFactorCriticalMoment2)
            {
                if (unbracedLengthFactorCriticalMoment2 < 0)
                    throw new ArgumentException("UnbracedLengthFactorCriticalMoment2 must be positive");
                _kCriticalMoment2 = unbracedLengthFactorCriticalMoment2;
            }

            public void SetEffectiveLengthFactorAxialBuck1(double effectiveLengthFactorAxialBuck1)
            {
                if (effectiveLengthFactorAxialBuck1 < 0)
                    throw new ArgumentException("EffectiveLengthFactorAxialBuck1 must be positive");
                _mAxialBuckling1 = effectiveLengthFactorAxialBuck1;
            }

            public void SetEffectiveLengthFactorAxialBuck2(double effectiveLengthFactorAxialBuck2)
            {
                if (effectiveLengthFactorAxialBuck2 < 0)
                    throw new ArgumentException("EffectiveLengthFactorAxialBuck2 must be positive");
                _mAxialBuckling2 = effectiveLengthFactorAxialBuck2;
            }

            public void SetEffectiveLengthFactorLatTorsBuck(double effectiveLengthFactorLatTorsBuck)
            {
                if (effectiveLengthFactorLatTorsBuck < 0)
                    throw new ArgumentException("EffectiveLengthFactorLatTorsBuck must be positive");
                _mLatTorsBuckling = effectiveLengthFactorLatTorsBuck;
            }

            public void SetEffectiveLengthFactorCriticalMoment1(double effectiveLengthFactorCriticalMoment1)
            {
                if (effectiveLengthFactorCriticalMoment1 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment1 must be positive");
                _mCriticalMoment1 = effectiveLengthFactorCriticalMoment1;
            }

            public void SetEffectiveLengthFactorCriticalMoment2(double effectiveLengthFactorCriticalMoment2)
            {
                if (effectiveLengthFactorCriticalMoment2 < 0)
                    throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
                _mCriticalMoment2 = effectiveLengthFactorCriticalMoment2;
            }

            public void SetUniformMomentFactorm1(double uniformMomentFactorm1)
            {
                if (uniformMomentFactorm1 < 0)
                    throw new ArgumentException("UniformMomentFactorm1 must be positive");
                _m1 = uniformMomentFactorm1;
            }

            public void SetUniformMomentFactorm2(double uniformMomentFactorm2)
            {
                if (uniformMomentFactorm2 < 0)
                    throw new ArgumentException("UniformMomentFactorm2 must be positive");
                _m2 = uniformMomentFactorm2;
            }

            public void SetUniformMomentFactormLT(double uniformMomentFactormLT)
            {
                if (uniformMomentFactormLT < 0)
                    throw new ArgumentException("UniformMomentFactormLT must be positive");
                _mLT = uniformMomentFactormLT;
            }

            #endregion
        }
    }
}
