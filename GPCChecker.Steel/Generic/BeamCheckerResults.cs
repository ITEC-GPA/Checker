using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;
using GPCCheckers.Steel.Generic;

namespace GPC.Checkers.Steel.Results
{

    public abstract class BeamCheckerResults
    {
        #region Variables

        private readonly BeamChecker _beamChecker;
        private double _workingRatio;
        private readonly double _length;
        private BeamStationCheckerResults[] _beamStationCheckerResults;
        private readonly Standard _standard;
        private readonly ILoadCase _combination;

        #endregion


        #region Properties

        public BeamChecker BeamChecker => _beamChecker;

        public ResultBeamForces[] ResultBeamForces => BeamChecker.ResultBeamForces;

        public ResultStation[] Stations => BeamChecker.Stations;

        public Checker.Options Options => BeamChecker.Options;

        public ISteelSection Section => BeamChecker.Section;

        public double WorkingRatio { get => _workingRatio; set => _workingRatio = value; }

        public BeamStationCheckerResults[] BeamStationCheckerResults { get => _beamStationCheckerResults; set => _beamStationCheckerResults = value; }
        
        public double Length => _length; 

        public Standard Standard => _standard;

        public ILoadCase LoadCase => _combination;

        #endregion


        #region Constructor

        internal BeamCheckerResults(BeamChecker beamChecker, ILoadCase loadCase,  Standard standard)
        {
            _beamChecker = beamChecker;
            _combination = loadCase;
            _standard = standard;
        }

        #endregion


        internal abstract void PerformCheck();
        // deve settare  le variabili che mancano
        // _beamStationCheckerResults = .....
        // _workingRatio = .....

        public virtual double MinSigma(ISteelSection section, double N, double M2, double M1)
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
            else if(section is SectionC sectionC)
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

    }
}
