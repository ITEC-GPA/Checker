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

        public GPC.Model.Materials.SteelMaterial Material => Section.Material;

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
            SectionClass sectionClass = SectionClass.Class1;

            if (MinSigma() < 0.0)
            {
                if (section is SectionH sectionH)
                {
                    if (sectionH.IsDoubleSymmetric)
                    {
                        if (Math.Abs(_M2Ed) > 0 || Math.Abs(resultBeamForces.N) > 0)        // AxialAndBendingStrongDirection
                        {
                            //classification of flanged for axial force due to bending
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedOuterFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, epsilon, sectionH.SectionType),
                                                                            GetClassCompressedOuterFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, epsilon, sectionH.SectionType),
                                                                            GetClassCompressedWebBendingMoment(sectionH.HeightWeb, sectionH.ThicknessWeb, epsilon)});
                        }

                        if (Math.Abs(_M1Ed) > 0)                                            // AxialAndBendingWeakDirection
                        {
                            //classification only for Compression. Other detailed calculation should be found and implemented
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedInternalFlangeBending(sectionH.HeightWeb, sectionH.ThicknessWeb, epsilon),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtTopFlange / 2.0, sectionH.ThicknessTopFlange, epsilon),
                                                                            GetClassCompressedInternalFlangeBending(sectionH.LenghtBottomFlange / 2.0, sectionH.ThicknessBottomFlange, epsilon) });
                        }
                    }

                    else
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
                    if (Math.Abs(_M2Ed) > 0)              // ClassificationAxialBendingStrongAxis
                    {
                        //flange are load with constant load     //classification webs                           //from equilibrium of Σ sigma = Ned
                        if (sectionRHS.TTop == sectionRHS.TBottom && sectionRHS.TWebLeft == sectionRHS.TWebRight && (int)sectionClass <= 3)
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType)});

                        else
                            sectionClass = SetWorstClass(new SectionClass[] {GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType) });
                    }

                    if (Math.Abs(_M1Ed) > 0)                                // ClassificationAxialBendingWeakAxis
                    {
                        if (sectionRHS.TWebLeft == sectionRHS.TWebRight && sectionRHS.TTop == sectionRHS.TBottom)
                            sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Binternal, sectionRHS.TTop, epsilon, sectionRHS.FormedType)});

                        else
                            sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon, sectionRHS.FormedType),
                                                                            GetClassCompressedWebRHS(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon, sectionRHS.FormedType) });
                    }

                    if (Math.Abs(_NEd) > 0 && Math.Abs(_M1Ed) = 0 && Math.Abs(_M2Ed) = 0)
                    {
                        sectionClass = SetWorstClass(new SectionClass[] { GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebLeft, epsilon),
                                                                        GetClassCompressedWebAxialCompression(sectionRHS.Hinternal, sectionRHS.TWebRight, epsilon),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TTop, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType),
                                                                        GetClassCompressedFlangeRHS(sectionRHS.Binternal, sectionRHS.TBottom, sectionRHS.Hinternal, epsilon, sectionRHS.FormedType) });
                    }

                    return sectionClass;
                }

                else if (section is SectionT sectionT)
                {
                    if (Math.Abs(_M2Ed) > 0 || Math.Abs(_M1Ed) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionT.B / 2, sectionT.Tf / 2, epsilon, sectionT.SectionType),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, epsilon) });
                    if (Math.Abs(_NEd) > 0 && Math.Abs(_M1Ed) = 0 && Math.Abs(_M2Ed) = 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeAxial(sectionT.B / 2, sectionT.Tf / 2, epsilon),
                                                                    GetClassCompressedStemT(sectionT.H, sectionT.Tw, epsilon) });
                    return sectionClass;
                }

                else if (section is SectionC sectionC)
                {
                    if (Math.Abs(_M2Ed) > 0 || Math.Abs(_M1Ed) > 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, epsilon),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LBottom, sectionC.ThicknessBottom, epsilon, sectionC.SectionType),
                                                                    GetClassCompressedOuterFlangeBending(sectionC.LTop, sectionC.ThicknessTop, epsilon, sectionC.SectionType)});

                    if (Math.Abs(_NEd) > 0 && Math.Abs(_M1Ed) = 0 && Math.Abs(_M2Ed) = 0)
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedWebChannel(sectionC.Hw / 2, sectionC.Tw / 2, epsilon),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LBottom, sectionC.ThicknessBottom, epsilon),
                                                                    GetClassCompressedOuterFlangeAxial(sectionC.LTop, sectionC.ThicknessTop, epsilon)});
                    return sectionClass;
                }

                else if (section is SectionL sectionL)
                {
                    if (Math.Abs(_M2Ed) > 0 || Math.Abs(_M1Ed) > 0)                                  
                        sectionClass = SetWorstClass(new SectionClass[]{ GetClassCompressedOuterFlangeBending(sectionL.LHor, sectionL.THor, epsilon, sectionL.SectionType),
                                                                    GetClassCompressedOuterFlangeBending(sectionL.LVert, sectionL.TVert, epsilon, sectionL.SectionType)});
                    
                    if (Math.Abs(_NEd) > 0 && Math.Abs(_M1Ed) = 0 && Math.Abs(_M2Ed) = 0)                    
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
        private SectionClass GetClassCompressedWebBendingMoment(double b, double t, double epsilon)
        {
            double ctRatio = b / t;
            if (ctRatio <= 80.0 * epsilon / (1 +  GetR1(Section, fc, py)) && ctRatio >= 40 * epsilon)      //TODO: da completare
                return SectionClass.Class1;

            if (GetR1(Section, fc, py) <= 0 && (ctRatio <= 100 * epsilon / (1 + GetR1(Section, fc, py)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;

            if (GetR1(Section, fc, py) >= 0 && (ctRatio <= 100 * epsilon / (1 + 1.5 * GetR1(Section, fc, py)) && ctRatio >= 40 * epsilon))
                return SectionClass.Class2;

            else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(Section, fc, py)) && ctRatio >= 40 * epsilon)
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
        private SectionClass GetClassCompressedWebAxialCompression(double b, double t, double epsilon)
        {
            double ctRatio = b / t;
            if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(Section, fc, py)) && ctRatio >= 40 * epsilon)
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

                else if (ctRatio <= 32.0 * epsilon && ctRatio <= 62.0 * epsilon - 0.5* d / t)
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
        private SectionClass GetClassCompressedWebRHS(double d, double t, double epsilon, Section.FormedTypes formed)
        {
            double ctRatio = d / t;

            if (formed == Model.Sections.Section.FormedTypes.HotFinished)
            {
                if (ctRatio <= 64.0 * epsilon / (1 + 0.6 * GetR1(Section, fc, py)) && ctRatio >= 40.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 80.0 * epsilon / (1 + GetR1(Section, fc, py)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 120 * epsilon / (1 + 2 * GetR2(Section, fc, py)) && ctRatio >= 40.0 * epsilon)
                    return SectionClass.Class3;

                else
                    return SectionClass.Class4;
            }
            else if (formed == Model.Sections.Section.FormedTypes.ColdFormed)
            {
                if (ctRatio <= 56.0 * epsilon / (1 + 0.6 * GetR1(Section, fc, py)) && ctRatio >= 35.0 * epsilon)      //TODO: da completare
                    return SectionClass.Class1;

                else if (ctRatio <= 70.0 * epsilon / (1 + GetR1(Section, fc, py)) && ctRatio >= 35.0 * epsilon)
                    return SectionClass.Class2;

                else if (ctRatio <= 105 * epsilon / (1 + 2 * GetR2(Section, fc, py)) && ctRatio >= 35.0 * epsilon)
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

        /// <summary>
        /// CopSuos2011 Chapter 7.3. Calculate R2 paramenter
        /// </summary>
        /// <param name="section"></param>
        /// <param name="fc"></param>
        /// <param name="py"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Calculate the cross sectional area
        /// </summary>
        /// <returns></returns>
        private double GetCrossSectionalArea()
        {
            throw new NotImplementedException();
        }

        #endregion

        



    }
}
