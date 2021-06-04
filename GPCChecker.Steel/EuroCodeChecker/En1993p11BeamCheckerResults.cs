using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Checkers.Steel.Results;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.EuroCode
{
    public class En1993p11BeamCheckerResults : BeamCheckerResults
    {

        #region Public enum

        public enum SectionClass
        {
            Class1 = 1,
            Class2 = 2,
            Class3 = 3,
            Class4 = 4,
        }

        #endregion


        #region Variables

        // VARIABILI EREDITATE
        // Combination 
        // BeamResult 
        // WorkingRatio 
        // Section
        // BeamStationCheckerResults
        // Length 
        // Options 

        private double _nTenrd;
        private double _nComrd;
        private double _txrd;
        private double _tyrd;
        private double _mxrd;
        private double _myrd;
        private double _trd;
        private SectionClass _class;


        #endregion


        #region Properties

        public double AxialTensionCapacity
        {
            get => _nTenrd;
            set => _nTenrd = value;
        }

        public double AxialCompressionCapacity
        {
            get => _nComrd;
            set => _nComrd = value;
        }

        public double ShearXCapacity
        {
            get => _txrd;
            set => _txrd = value;
        }

        public double ShearYCapacity
        {
            get => _tyrd;
            set => _tyrd = value;
        }

        public double BendingMomentXCapacity
        {
            get => _mxrd;
            set => _mxrd = value;
        }

        public double BendingMomentYCapacity
        {
            get => _myrd;
            set => _myrd = value;
        }

        public double TorsionMomentCapacity
        {
            get => _trd;
            set => _trd = value;
        }

        public SectionClass Class
        {
            get => _class;
            set => _class = value;
        }

        #endregion


        #region Constructor

        public En1993p11BeamCheckerResults(ILoadCase loadCase, ResultBeamForces[] forces, ResultStation[] stations, ISteelSection[] section, EN1993p11Checker.EN1993_1Options options, StandardEN1990 standard)
            : base(loadCase, forces, stations, section, options, standard)
        { 
        }        

        #endregion



        internal override void PerformCheck()
        {

        }






        /*

        #region Section Private Method

        private double CalculateAxialTensionCapacity()      // stazione ??
        {
            throw new NotImplementedException();
        }

        private double CalculateAxialCompressionCapacity()
        {
            throw new NotImplementedException();
        }

        private double CalculateShearXCapacity()
        {
            throw new NotImplementedException();
        }

        private double CalculateShearYCapacity()
        {
            throw new NotImplementedException();
        }

        private double CalculateBendingMomentXCapacity()
        {
            throw new NotImplementedException();
        }

        private double CalculateBendingMomentYCapacity()
        {
            throw new NotImplementedException();
        }

        private double CalculateTorsionalMomentCapacity()
        {
            throw new NotImplementedException();
        }

        private SectionClass CalculateSectionClass(ISteelSection section, ResultBeamForces resultBeamForces, double py)
        {
            double epsilon = Math.Sqrt(235.0 / py);

            if (MinSigma() < 0.0)
            {

                if (section is SectionH sectionH)
                {
                    double cTWeb = sectionH.HeightWeb / sectionH.ThicknessWeb;
                    double cTFlangeTop = (sectionH.LenghtTopFlange / 2.0 - sectionH.ThicknessWeb / 2.0) / sectionH.ThicknessTopFlange;
                    double cTFlangeBottom = (sectionH.LenghtBottomFlange / 2.0 - sectionH.ThicknessWeb / 2.0) / sectionH.ThicknessBottomFlange;
                    SectionClass sectionClass = SectionClass.Class1;

                    if (sectionH.IsDoubleSymmetric)
                    {
                        #region AxialAndBendingStrongDirection
                        if (Math.Abs(_M2Ed) > 0 || Math.Abs(resultBeamForces.N) > 0)
                        {
                            //classification of flanged for axial force due to bending
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));

                            //classification web
                            double alphaClassification = -resultBeamForces.N / (2.0 * sectionH.HeightWeb * py * sectionH.ThicknessWeb) + 0.5;
                            double psiClassification = 1;

                            if ((int)sectionClass < 4)
                                psiClassification = -2.0 * resultBeamForces.N / (sectionH.Area * py) - 1.0;

                            sectionClass = SetWorstClass(sectionClass, GetClassInnerPlate(cTWeb, epsilon, alphaClassification, psiClassification));
                        }
                        #endregion

                        #region AxialAndBendingWeakDirection
                        if (Math.Abs(_M1Ed) > 0)
                        {
                            //classification only for Compression.
                            //Other detailed calculation should be found and implemented
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlate(cTWeb, epsilon));
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlate(cTFlangeTop, epsilon));
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlate(cTFlangeBottom, epsilon));
                        }
                        #endregion
                    }
                    else
                    {
                        //classification only for Compression.
                        //Other detailed calculation should be found and implemented
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlate(cTWeb, epsilon));
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedOuterPlate(cTFlangeBottom, epsilon));
                    }
                    return sectionClass;
                }

                else if (section is SectionCHS sectionCHS)
                {
                    double D = sectionCHS.D;
                    double t = sectionCHS.T;
                    if (D / t <= 40.0 * epsilon * epsilon)
                        return SectionClass.Class1;

                    else if (D / t <= 70.0 * epsilon * epsilon)
                        return SectionClass.Class2;

                    else if (D / t <= 140 * epsilon * epsilon)
                        return SectionClass.Class3;

                    else
                        return SectionClass.Class4;

                }

                else if (section is SectionRHS sectionRHS)
                {
                    SectionClass sectionClass = SectionClass.Class1;

                    #region ClassificationAxialBendingStrongAxis

                    if (Math.Abs(_M2Ed) > 0 || Math.Abs(_NEd) > 0)
                    {
                        double cTFlange = Math.Max(sectionRHS.Binternal / sectionRHS.TBottom, sectionRHS.Binternal / sectionRHS.TTop);

                        //flange are load with constant load
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(cTFlange, epsilon, sectionRHS.FormedType));

                        //classification webs
                        //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.TTop == sectionRHS.TBottom && sectionRHS.TWebLeft == sectionRHS.TWebRight && (int)sectionClass <= 3)
                        {
                            double alphaClassification = -_NEd / (4.0 * sectionRHS.TWebLeft * py * sectionRHS.Hinternal) + 0.5;

                            //from equlibrium sigma = N/A+M/W:
                            double psiClassification = 1;
                            if ((int)sectionClass < 4)
                            {
                                psiClassification = -_NEd * 2.0 / (sectionRHS.Area * py) - 1.0;
                            }

                            double cTWeb = sectionRHS.Hinternal / sectionRHS.TWebLeft;      // TWebLeft == TWebRight

                            sectionClass = SetWorstClass(sectionClass, GetClassInnerPlate(cTWeb, epsilon, alphaClassification, psiClassification));
                        }
                        else
                        {
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Hinternal / sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType));
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Hinternal / sectionRHS.TWebRight, epsilon, sectionRHS.FormedType));
                        }
                    }

                    #endregion

                    #region ClassificationAxialBendingWeakAxis

                    if (Math.Abs(_M1Ed) > 0)
                    {
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Hinternal / sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType));
                        sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Hinternal / sectionRHS.TWebRight, epsilon, sectionRHS.FormedType));

                        if (sectionRHS.TWebLeft == sectionRHS.TWebRight && sectionRHS.TTop == sectionRHS.TBottom)
                        {
                            double alphaClassification = -_NEd / (4.0 * sectionRHS.TBottom * py * sectionRHS.Binternal) + 0.5;
                            double psiClassification = -_NEd * 2.0 / (sectionRHS.Area * py) - 1.0;
                            sectionClass = SetWorstClass(sectionClass, GetClassInnerPlate(sectionRHS.Binternal / sectionRHS.TTop, epsilon, alphaClassification, psiClassification));
                        }
                        else
                        {
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Binternal / sectionRHS.TTop, epsilon, sectionRHS.FormedType));
                            sectionClass = SetWorstClass(sectionClass, GetClassCompressedInnerPlateRHS(sectionRHS.Binternal / sectionRHS.TBottom, epsilon, sectionRHS.FormedType));
                        }
                    }
                    return sectionClass;

                    #endregion
                }

                else
                    throw new NotImplementedException();
            }
            else
                return SectionClass.Class1;
        }

        private SectionClass GetClassCompressedOuterPlate(double ctRatio, double epsilon)
        {
            if (ctRatio <= 9.0 * epsilon)
                return SectionClass.Class1;

            else if (ctRatio <= 10.0 * epsilon)
                return SectionClass.Class2;

            else if (ctRatio <= 14.0 * epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        private SectionClass GetClassInnerPlate(double ctRatio, double epsilon, double alpha, double psi)
        {
            if (alpha > 0.5 && alpha < 1)
            {
                if (ctRatio <= 396.0 * epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class1;

                else if (ctRatio <= 456.0 * epsilon / (13.0 * alpha - 1.0))
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;
                        else
                            return SectionClass.Class4;
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                            return SectionClass.Class3;
                        else
                            return SectionClass.Class4;
                    }
                    else
                        return SectionClass.Class4;
                }
            }
            else if (alpha <= 0.5 && alpha > 0)
            {
                if (ctRatio <= 36.0 * epsilon / alpha)
                    return SectionClass.Class1;

                else if (ctRatio <= 41.5 * epsilon / alpha)
                    return SectionClass.Class2;

                else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                            return SectionClass.Class3;
                        else
                            return SectionClass.Class4;
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                            return SectionClass.Class3;
                        else
                            return SectionClass.Class4;
                    }
                    else
                        return SectionClass.Class4;
                }
            }
            else if (alpha > 1)
            {
                if (psi > -1)
                {
                    if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                        return SectionClass.Class3;
                    else
                        return SectionClass.Class4;
                }
                else if (psi <= -1)
                {
                    if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                        return SectionClass.Class3;
                    else
                        return SectionClass.Class4;
                }
                else
                    return SectionClass.Class4;
            }
            else
                return SectionClass.Class4;
        }

        private SectionClass GetClassCompressedInnerPlate(double ctRatio, double epsilon)
        {
            if (ctRatio <= 28.0 * epsilon)
                return SectionClass.Class1;

            else if (ctRatio <= 32.0 * epsilon)
                return SectionClass.Class2;

            else if (ctRatio <= 40.0 * epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        private SectionClass GetClassCompressedInnerPlateRHS(double ctRatio, double epsilon, Section.FormedTypes formed)
        {
            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 28.0 * epsilon)      // <= 80 * epsilon - d/t    TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 32.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 40.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 26.0 * epsilon)
                    return SectionClass.Class1;

                else if (ctRatio <= 28.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 35.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        private SectionClass SetWorstClass(SectionClass obj1, SectionClass obj2)
        {
            int cl1 = (int)obj1;
            int cl2 = (int)obj2;

            return (SectionClass)Math.Max(cl1, cl2);
        }

        private double GetR1(Section section, double fc, double py)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                {
                    double r1 = fc / (sectionH.HeightWeb * sectionH.ThicknessWeb * py);
                    r1 = r1 < 1 ? 1 : r1;
                    r1 = r1 > -1 ? -1 : r1;
                    return r1;
                }
                else
                {
                    double r1 = fc / (sectionH.HeightWeb * sectionH.ThicknessWeb * py) +
                        (((sectionH.LenghtBottomFlange * sectionH.LenghtBottomFlange - sectionH.LenghtTopFlange * sectionH.ThicknessTopFlange) * py) /
                            (sectionH.HeightWeb * sectionH.ThicknessWeb * py));
                    r1 = r1 < 1 ? 1 : r1;
                    r1 = r1 > -1 ? -1 : r1;
                    return r1;
                }
            }
            else if (section is SectionRHS sectionRHS)
            {
                double r1 = fc / (2 * sectionRHS.Hinternal * sectionRHS.TWebLeft * py);
                r1 = r1 < 1 ? 1 : r1;
                r1 = r1 > -1 ? -1 : r1;
                return r1;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        private double GetR2(Section section, double fc, double py)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                    return fc / (GetCrossSectionalArea() * py);

                else
                    throw new NotImplementedException("Not supported case");
            }
            else if (section is SectionRHS sectionRHS)
                return fc / (GetCrossSectionalArea() * py);

            else
                throw new NotImplementedException("Not supported Section type");
        }

        private double GetCrossSectionalArea()
        {
            throw new NotImplementedException();
        }

        

        #endregion

        */



    }
}
