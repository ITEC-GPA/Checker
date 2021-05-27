using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.LoadCases;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;
using System.ComponentModel;
using GPC.Model.Sections;
using GPC.Checkers.Steel.Results;
using GPC.Model.Materials;

namespace GPC.Checkers.Steel.CopSuos2011
{
    public class CopSuos2011BeamCheckerResults : BeamCheckerResults
    {

        #region Public enum

        public enum SectionClass
        {
            [Description("Plastic")] Class1 = 1,
            [Description("Compact")] Class2 = 2,
            [Description("Semi-Compact")] Class3 = 3,
            [Description("Slender")] Class4 = 4,
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

        private double _py;
        private double _nTenrd;
        private double _nComrd;
        private double _txrd;
        private double _tyrd;
        private double _mxrd;
        private double _myrd;
        private double _trd;
        private SectionClass _class;
        private CopSuos2011Checker.CopSuos2011Options.SteelClasses _steelClass;

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

        public SteelMaterial Material => Section.Material;

        public CopSuos2011Checker.CopSuos2011Options.SteelClasses SteelClass => _steelClass;

        public double Py { get => _py; set => Py = value; }

        #endregion


        #region Constructor

        public CopSuos2011BeamCheckerResults(ILoadCase loadCase, ResultBeamForces[] forces, ResultStation[] stations, ISteelSection section, CopSuos2011Checker.CopSuos2011Options options, StandardCopSuos2011 standard)
            : base(loadCase, forces, stations, section, options, standard)
        {
        }

        #endregion



        internal override void PerformCheck()
        {

        }


        private double CalculateAxialWR()
        {
            return 1; // FORZA AGENTE / section.AxialCompressionCapacity;
        }

        private double CalculateShearWR()
        {
            return 1; // FORZA AGENTE / section.LA PROPRIETA' CHE MI INTERESSA;
        }

        private double CalculateBendingMomentWR()
        {
            return 1; // FORZA AGENTE / section.LA PROPRIETA' CHE MI INTERESSA;
        }





        #region Section Private Method

        private double CalculateAxialTensionCapacity()      //stazione??
        {
            throw new NotImplementedException();
        }

        private double CalculateAxialCompressionCapacity()
        {
            throw new NotImplementedException();
        }

        #region Shear Capacity

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1
        /// </summary>
        /// <returns></returns>
        private double CalculateShearXCapacity()
        {
            return GetPy(Material, SteelClass) * GetShearArea(Section) / Math.Sqrt(3);
        }

        /// <summary>
        /// CopSuos 2011 chapter 8.2.1 Calculate Av parameter
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        private double GetShearArea(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return sech.ThicknessWeb * sech.H;
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return secH.ThicknessWeb * secH.HeightWeb;
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return secC.Tw * secC.H;
            if (section is SectionRHS sectionRHS)
                return 2 * ((sectionRHS.TWebLeft + sectionRHS.TWebLeft) / 2) * sectionRHS.Hinternal;
            if (section is SectionCHS sectionCHS)
                return 0.6 * sectionCHS.Area;
            if (section is SectionT sectionT)
                return sectionT.Tw * (sectionT.H - sectionT.Tf);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        private double CalculateShearYCapacity()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Bending Moment Capacity

        private double CalculateBendingMoment1Capacity(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            if (resultBeamForces.V2 < 0.6 * CalculateShearXCapacity())   // low shear condition
            {
                if (Class == SectionClass.Class1 || Class == SectionClass.Class2)
                    return Math.Min(Py * ((Section)section).Wpl2, 1.2 * Py * ((Section)section).Wel2);
                else if (Class == SectionClass.Class3)
                    return Py * ((Section)section).Wel2;
                else// SectionClass.Class4
                    return Py * CalculateEffettiveElasticModulus();       // TODO: implementare Wel effettivo (vedi 8.2.2)
            }
            else // high shear condition
            {
                if (Class == SectionClass.Class1 || Class == SectionClass.Class2)
                    return Math.Min(Py * ( ((Section)section).Wpl2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section)), 
                                    1.2 * Py * (((Section)section).Wel2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5));
                else if (Class == SectionClass.Class3)
                    return Py * (((Section)section).Wel2 - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5);

                else// SectionClass.Class4
                    return Py * (CalculateEffettiveElasticModulus() - CalculateRhoMomentShearInteraction(resultBeamForces) * CalculatePlasticModulusShear(section) / 1.5);       
            }
        }

        private double CalculatePlasticModulusShear(ISteelSection section)
        {
            if (section is SectionH sech && sech.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1/4) * sech.H * Math.Pow(sech.ThicknessWeb, 2);
            if (section is SectionH secH && secH.SectionType == Model.Sections.Section.SectionTypes.Welded)
                return (1 / 4) * secH.HeightWeb * Math.Pow(secH.ThicknessWeb, 2);
            if (section is SectionC secC && secC.SectionType == Model.Sections.Section.SectionTypes.Rolled)
                return (1 / 4) * secC.Tw * Math.Pow(secC.H, 2);
            if (section is SectionRHS sectionRHS)
                return  2 *(1/4) * ((sectionRHS.TWebLeft + sectionRHS.TWebLeft) / 2) * Math.Pow(sectionRHS.Hinternal,2);
            if (section is SectionCHS sectionCHS)
                return 0.6 * (1 / 6) * Math.Pow(sectionCHS.D, 3) * Math.Pow(sectionCHS.Dint, 3);
            if (section is SectionT sectionT)
                return (1/4) * sectionT.Tw * Math.Pow((sectionT.H - sectionT.Tf),3);
            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// Return the rho coefficien in CopSuos2011 chapter 8.2.2.2
        /// </summary>
        /// <returns></returns>
        private double CalculateRhoMomentShearInteraction(ResultBeamForces resultBeamForces)
        {
            return Math.Pow((2 * resultBeamForces.V2 / CalculateShearXCapacity()) - 1, 2);
        }

        private double CalculateEffettiveElasticModulus()
        {
            throw new NotImplementedException("Effettive elastic modulus not implemented");
        }

        private double CalculateBendingMoment2Capacity()
        {
            throw new NotImplementedException();
        }

        #endregion

        private double CalculateTorsionalMomentCapacity()
        {
            throw new NotImplementedException();
        }





        #region Section Class

        /// <summary>
        /// Return the <see cref="SectionClass"/> of the section <paramref name="section"/> with the <paramref name="resultBeamForces"/> 
        /// </summary>
        /// <param name="section"></param>
        /// <param name="resultBeamForces"></param>
        /// <param name="steelClass"></param>
        /// <param name="material"></param>
        /// <returns></returns>
        private SectionClass CalculateSectionClass(ISteelSection section, ResultBeamForces resultBeamForces, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass, SteelMaterial material)
        {
            double epsilon = Math.Sqrt(235.0 / GetPy(material, steelClass));
            SectionClass sectionClass = SectionClass.Class1;

            if (MinSigma(section, resultBeamForces.N, resultBeamForces.M2, resultBeamForces.M1) < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    if (sectionH.IsDoubleSymmetric)
                    {
                        if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.N) > 0)        // AxialAndBendingStrongDirection                        
                            //classification of flanged for axial force due to bending
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedOuterFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, epsilon, sectionH.SectionType),
                                                                            GetClassCompressedOuterFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, epsilon, sectionH.SectionType),
                                                                            GetClassCompressedWebBendingMoment(sectionH.HeightWeb, sectionH.ThicknessWeb, epsilon, resultBeamForces, section)});


                        if (Math.Abs(resultBeamForces.M1) > 0)                                            // AxialAndBendingWeakDirection                        
                            //classification only for Compression. Other detailed calculation should be found and implemented
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedInternalFlangeBending(sectionH.HeightWeb, sectionH.ThicknessWeb, epsilon),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, epsilon),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, epsilon) });
                    }

                    else if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        //classification only for Compression. Other detailed calculation should be found and implemented
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedInternalFlangeBending(sectionH.HeightWeb, sectionH.ThicknessWeb, epsilon),
                                                                        GetClassCompressedInternalFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, epsilon),
                                                                        GetClassCompressedInternalFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, epsilon) });

                    return sectionClass;
                }

                else if (section is SectionCHS sectionCHS)
                    return GetSectionClassCHS(sectionCHS.D, sectionCHS.T, epsilon);


                else if (section is SectionRHS sectionRHS)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0)              // ClassificationAxialBendingStrongAxis
                    {
                        //flange are load with constant load     //classification webs                           //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.TTop == sectionRHS.TBottom && sectionRHS.TWebLeft == sectionRHS.TWebRight && (int)sectionClass <= 3)
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType, resultBeamForces, section),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType)});

                        else
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType, resultBeamForces, section),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType, resultBeamForces, section) });
                    }

                    if (Math.Abs(resultBeamForces.M1) > 0)                                // ClassificationAxialBendingWeakAxis
                    {
                        if (sectionRHS.TWebLeft == sectionRHS.TWebRight && sectionRHS.TTop == sectionRHS.TBottom)
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType, resultBeamForces, section),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType, resultBeamForces, section),
                                                                            GetClassCompressedWebRHS(sectionRHS.Binternal, sectionRHS.TTop, epsilon, sectionRHS.FormedType, resultBeamForces, section)});

                        else
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType, resultBeamForces, section),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType, resultBeamForces, section) });
                    }

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, resultBeamForces, section),
                                                                        GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, resultBeamForces, section),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType) });

                    return sectionClass;
                }

                else if (section is SectionT sectionT)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M2) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionT.B / 2, sectionT.Tf / 2, epsilon, sectionT.SectionType),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, epsilon) });
                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionT.B / 2, sectionT.Tf / 2, epsilon),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, epsilon) });
                    return sectionClass;
                }

                else if (section is SectionC sectionC)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M1) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, epsilon),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LBottom, sectionC.ThicknessBottom, epsilon, sectionC.SectionType),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LTop, sectionC.ThicknessTop, epsilon, sectionC.SectionType)});

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, epsilon),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LBottom, sectionC.ThicknessBottom, epsilon),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LTop, sectionC.ThicknessTop, epsilon)});
                    return sectionClass;
                }

                else if (section is SectionL sectionL)
                {
                    if (Math.Abs(resultBeamForces.M2) > 0 || Math.Abs(resultBeamForces.M1) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionL.LHor, sectionL.THor, epsilon, sectionL.SectionType),
                                                                    GetClassCompressedOuterFlangeBending(sectionL.LVert, sectionL.TVert, epsilon, sectionL.SectionType)});

                    if (Math.Abs(resultBeamForces.N) > 0 && Math.Abs(resultBeamForces.M1) == 0 && Math.Abs(resultBeamForces.M2) == 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionL.LHor, sectionL.THor, epsilon),
                                                                    GetClassCompressedOuterFlangeAxial(sectionL.LVert, sectionL.TVert, epsilon)});

                    return sectionClass;
                }

                else
                    throw new NotImplementedException("CalculateSectionClassException: not implemented Section");
            }
            else
                return SectionClass.Class1;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, outstand element, bending moment
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <param name="sectionTypes"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedOuterFlangeBending(double b, double t, double epsilon, Section.SectionTypes sectionTypes)
        {
            double ctRatio = b / t;
            if (sectionTypes == Model.Sections.Section.SectionTypes.Rolled)
            {
                if (ctRatio <= 9.0 * epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 10.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 15.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else if (sectionTypes == Model.Sections.Section.SectionTypes.Welded)
            {
                if (ctRatio <= 8.0 * epsilon)
                    return SectionClass.Class1;
                else if (ctRatio <= 9.0 * epsilon)
                    return SectionClass.Class2;
                else if (ctRatio <= 13.0 * epsilon)
                    return SectionClass.Class3;
                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, outstand element, axial compression
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <param name="sectionTypes"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedOuterFlangeAxial(double b, double t, double epsilon)
        {
            double ctRatio = b / t;
            if (ctRatio <= 13.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, bending moment
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeBending(double b, double t, double epsilon)
        {
            double ctRatio = b / t;
            if (ctRatio <= 28.0 * epsilon)
                return SectionClass.Class1;
            else if (ctRatio <= 32.0 * epsilon)
                return SectionClass.Class2;
            else if (ctRatio <= 40.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Flange, internal element, axial compression
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedInternalFlangeAxial(double b, double t, double epsilon)
        {
            double ctRatio = b / t;
            if (ctRatio <= 40.0 * epsilon)
                return SectionClass.Class3;
            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Generally
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebBendingMoment(double b, double t, double epsilon, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double ctRatio = b / t;
            if (ctRatio <= 80.0 * epsilon / (1 + GetR1(Section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40 * epsilon)      //TODO: da completare
                return SectionClass.Class1;

            if (GetR1(Section, GetFcForClassification(resultBeamForces, section), Py) <= 0 && 
                (ctRatio <= 100 * epsilon / (1 + GetR1(Section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;

            if (GetR1(Section, GetFcForClassification(resultBeamForces, section), Py) >= 0 && 
                (ctRatio <= 100 * epsilon / (1 + 1.5 * GetR1(Section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;

            else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40 * epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Web of I, H, Box section. Axial Compression
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebAxialCompression(double b, double t, double epsilon, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double ctRatio = b / t;
            if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 CHS Classification
        /// </summary>
        /// <param name="D"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetSectionClassCHS(double D, double t, double epsilon)
        {
            if (D / t <= 40.0 * epsilon * epsilon)
                return SectionClass.Class1;

            else if (D / t <= 50.0 * epsilon * epsilon)
                return SectionClass.Class2;

            else if (D / t <= 140 * epsilon * epsilon)
                return SectionClass.Class3;

            else
                return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Flange
        /// </summary>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <param name="d"></param>
        /// <param name="epsilon"></param>
        /// <param name="formed"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedFlangeRHS(double b, double t, double d, double epsilon, Section.FormedTypes formed)
        {
            double ctRatio = b / t;

            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 28.0 * epsilon && ctRatio <= 80 * epsilon - d / t)      // <= 80 * epsilon - d/t    TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 32.0 * epsilon && ctRatio <= 62.0 * epsilon - 0.5 * d / t)
                    return SectionClass.Class2;

                else if (ctRatio <= 40.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 26.0 * epsilon && ctRatio <= 72.0 * epsilon - d / t)
                    return SectionClass.Class1;

                else if (ctRatio <= 28.0 * epsilon && ctRatio <= 54 * epsilon - 0.5 * d / t)
                    return SectionClass.Class2;

                else if (ctRatio <= 35.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Web
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <param name="formed"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebRHS(double d, double t, double epsilon, Section.FormedTypes formed, ResultBeamForces resultBeamForces, ISteelSection section)
        {
            double ctRatio = d / t;

            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 64.0 * epsilon / (1 + 0.6 * GetR1(section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 80.0 * epsilon / (1 + GetR1(section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 56.0 * epsilon / (1 + 0.6 * GetR1(section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 35.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 70.0 * epsilon / (1 + GetR1(section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 35.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 105 * epsilon / (1 + 2 * GetR2(resultBeamForces, section, GetFcForClassification(resultBeamForces, section), Py)) && ctRatio >= 35.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending and axial compression
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleWithAxialCompression(double b, double d, double t, double epsilon)
        {
            if (b / t < 15.0 * epsilon && d / t < 15.0 * epsilon && (b + d) / t < 24.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Angle, compression due to bending
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedAngleOnlyBending(double b, double d, double t, double epsilon)
        {
            if (b / t < 9.0 * epsilon && d / t < 9.0 * epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * epsilon && d / t < 10.0 * epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * epsilon && d / t < 15.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Outstand Leg of an angle 
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedOutstandLeg(double b, double t, double epsilon)
        {
            if (b / t < 9.0 * epsilon)
                return SectionClass.Class1;
            if (b / t < 10.0 * epsilon)
                return SectionClass.Class2;
            if (b / t < 15.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.2 Stem of a T section
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedStemT(double d, double t, double epsilon)
        {
            if (d / t < 8.0 * epsilon)
                return SectionClass.Class1;
            if (d / t < 9.0 * epsilon)
                return SectionClass.Class2;
            if (d / t < 18.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        /// <summary>
        /// CopSuos2011 Table 7.1 Channel of a C section
        /// </summary>
        /// <param name="d"></param>
        /// <param name="t"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        private SectionClass GetClassCompressedWebChannel(double d, double t, double epsilon)
        {
            if (d / t < 40.0 * epsilon)
                return SectionClass.Class3;
            return SectionClass.Class4;
        }

        private double GetFcForClassification(ResultBeamForces resultBeamForces, ISteelSection section)
        {
            return resultBeamForces.N / GetCrossSectionalArea(section);
        }

        /// <summary>
        /// Return the worst <see cref="SectionClass"/> section between <paramref name="obj1"/> e <paramref name="obj2"/>
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        private SectionClass SetWorstClass(SectionClass obj1, SectionClass obj2)
        {
            int cl1 = (int)obj1;
            int cl2 = (int)obj2;

            return (SectionClass)Math.Max(cl1, cl2);
        }

        /// <summary>
        /// Return the worst class section between an array of <see cref="SectionClass"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private SectionClass SetWorstClass(SectionClass[] obj)
        {
            SectionClass sectionClass = SectionClass.Class1;

            for (int i = 0; i < obj.Count(); i++)
                sectionClass = SetWorstClass(sectionClass, obj[i]);

            return sectionClass;
        }

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate R1 paramenter
        /// </summary>
        /// <param name="section"></param>
        /// <param name="fc"></param>
        /// <param name="py"></param>
        /// <returns></returns>
        private double GetR1(ISteelSection section, double fc, double py)
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

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate R2 paramenter
        /// </summary>
        /// <param name="section"></param>
        /// <param name="fc"></param>
        /// <param name="py"></param>
        /// <returns></returns>
        private double GetR2(ResultBeamForces resultBeamForces, ISteelSection section, double fc, double py)
        {
            if (section is SectionH sectionH)
            {
                if (sectionH.ThicknessBottomFlange == sectionH.ThicknessTopFlange && sectionH.LenghtBottomFlange == sectionH.LenghtTopFlange)
                    return fc / (GetFcForClassification(resultBeamForces, section) * py);

                else
                    throw new NotImplementedException("Not supported case");
            }
            else if (section is SectionRHS sectionRHS)
                return fc / (GetFcForClassification(resultBeamForces, section) * py);

            else
                throw new NotImplementedException("Not supported Section type");
        }

        /// <summary>
        /// Calculate the cross sectional area
        /// </summary>
        /// <returns></returns>
        private double GetCrossSectionalArea(ISteelSection section)
        {
            return ((Section)section).Area;
        }

        #endregion

        #region py

        /// <summary>
        /// Return the py value 
        /// </summary>
        /// <param name="steelMaterial"></param>
        /// <param name="steelClass"></param>
        /// <returns></returns>
        private double GetPy(SteelMaterial steelMaterial, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (steelMaterial.Fyk < 460)
                return Math.Min(steelMaterial.Fyk / GetGammaM1(steelMaterial.Fyk, steelClass), steelMaterial.Fu / GetGammaM2(steelMaterial.Fyk, steelClass));
            else            // (steelMaterial.Fyk >= 460)
                return Math.Min(steelMaterial.Fyk / GetGammaM1(steelMaterial.Fyk, steelClass), steelMaterial.Fu / GetGammaM2(steelMaterial.Fyk, steelClass));
        }

        private double GetGammaM1(double Ys, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (Ys < 460)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1)
                    return 1.0;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2)
                    return 1.2;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Ys > 460 && Ys < 690)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1 || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2
                    || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    return 1.0;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        private double GetGammaM2(double Ys, CopSuos2011Checker.CopSuos2011Options.SteelClasses steelClass)
        {
            if (Ys < 460)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1)
                    return 1.2;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2)
                    return 1.3;
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
            }
            if (Ys > 460 && Ys < 690)
            {
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1 || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class2
                    || steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class3)
                    throw new ArgumentException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
                if (steelClass == CopSuos2011Checker.CopSuos2011Options.SteelClasses.Class1H)
                    return 1.2;
            }
            throw new NotImplementedException("CopSuos2011 not implemented GammaM1 coefficient for this combination of Material and Steel Class");
        }

        #endregion

        #endregion





    }
}
