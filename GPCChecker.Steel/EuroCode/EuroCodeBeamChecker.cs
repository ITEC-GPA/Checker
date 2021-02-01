using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Geometry;
using Word = Microsoft.Office.Interop.Word;
using GPCChecker.Steel.EuroCode;

namespace GPC.Checker.Steel.EuroCode
{
    public enum LoadCondition
    {
        Constant,
        SingleForce,
        NotDirectlyLoaded
    }
    public enum SupportCondition
    {
        HingesAtEnds,
        EndsRestrained,
        OneSideRestrained_OneSideHinged
    }

    public class EuroCodeBeamChecker
    {
        #region Variables
        protected string _formatInt = "{0}";
        protected string _formatDouble = "{0.00}";
        protected string _formatExponential = "{0.##E+00}";

        protected Annex _annex;

        protected Section _sec;
        protected int _classificationSection; //for the current forces

        protected double _L; //lenght of the beams
      
        protected double? _psiy; //MEd(End 2) = psi * MEd(End 1) - y dir
        protected double? _psiz; //MEd(End 2) = psi * MEd(End 1) - z dir
        protected LoadCondition _loadConditiony;
        protected SupportCondition _supportConditiony;
        protected LoadCondition _loadConditionz;
        protected SupportCondition _supportConditionz;

        protected double _betay; // L0y = betay * L
        protected double _betaz; // L0z = betaz * L
        protected double _betaLT; // L0LT = betaLT * L
        protected double _L0y; //lunghezza libera di inflessione y dir
        protected double _L0z; //lunghezza libera di inflessione z dir
        protected double _L0LT; //lunghezza libera di inflessione lateral torsional

        protected bool _method1AnnexA = true; //se false -> si usa method2AnnexB EN 1993-1-1
        protected bool _useEquation_6_57 = false; //EN1993-1-1

        protected double _Aeff;//area sezione efficace
        protected double _Weffy;//modulo elastico sezione efficace
        protected double _J2eff; //inerzia max sezione efficace
        protected double _Weffz; //modulo elastico sezione efficace
        protected double _J1eff; //inerzia min sezione efficace
        protected Point2d _deltaG; //differenza tra baricentro sezione efficace e sezione lorda
        protected Point2d _centroidEff; //baricentro sezione efficace

        protected double _NEd; //sforzo assiale agente: NEd>0 trazione
        protected double _V1Ed; //sforzo di taglio agente principale
        protected double _V2Ed; //sforzo di taglio agente principale
        protected double _M1Ed; //momento flettente agente principale
        protected double _M2Ed; //momento flettente agente principale
        protected double _TEd;  //momento torcente agente
        protected double _VzEd; //taglio agente direzione z
        protected double _VyEd; //taglio agente direzione y
        protected double _MzEd; //momento flettente direzione z
        protected double _MyEd; //momento flettente direzione y

        protected double _NRd; //sforzo assiale resistente
        protected double _VplRdy; //taglio resistente
        protected double _VplRdz; //taglio resistente
        protected double _VplTRdy; //taglio resistente con decurtazione dovuta a torsione
        protected double _VplTRdz; //taglio resistente con decurtazione dovuta a torsione

        protected double _MRdNy; //momento flettente resistente decurtato per sforzo assiale
        protected double _MRdNz;//momento flettente resistente decurtato per sforzo assiale

        protected double _McRdy;//momento flettente resistente
        protected double _McRdz;//momento flettente resistente

        protected double _MvRdy;//momento flettente resistente decurtato per sforzo di taglio
        protected double _MvRdz;//momento flettente resistente decurtato per sforzo di taglio

        protected double _TRd;//momento torcente resistente

        protected double _Ncry; //sforzo assiale di buckling laterale
        protected double _Ncrz;//sforzo assiale di buckling laterale
        protected double _NcrTorsional;//sforzo assiale di buckling torsionale
        protected double _NcrFlexuralTorsional;//sforzo assiale di buckling flesso torsionale
        protected double _McrLateralTorsional;//momento critico di buckling flesso torsionale
        protected double _Mcr0LateralTorsional;//momento critico di buckling flesso torsionale per momento flettente costante
        protected double _c1; //fattore per calcolo Mcr
        protected double _c2;//fattore per calcolo Mcr
        protected double _c3;//fattore per calcolo Mcr
        protected double _zg; //distanza tra applicazione del carico e baricentro
        protected double _zj;

        protected double _alphay; //fattore di imperfezione
        protected double _alphaz;//fattore di imperfezione
        protected double _alphaLT;//fattore di imperfezione
        protected double _alphaT;//fattore di imperfezione

        protected double _lambday; //snellezza adimensionale
        protected double _lambdaz;//snellezza adimensionale
        protected double _lambdaLT;//snellezza adimensionale
        protected double _lambdaT;//snellezza adimensionale
        protected double _lambda0;//snellezza adimensionale

        protected double _Phiy;
        protected double _Phiz;
        protected double _PhiLT;
        protected double _PhiT;

        protected double _Chiy; //fattore riduzione per buckling
        protected double _Chiz; //fattore riduzione per buckling 
        protected double _ChiLT; //fattore riduzione per buckling
        protected double _ChiT; //fattore riduzione per buckling

        protected double _cmy0;
        protected double _cmz0;

        protected double _muy;
        protected double _muz;

        protected double _wy;
        protected double _wz;

        protected double _cmy;
        protected double _cmz;
        protected double _cmLT;

        protected double _aLT;
        protected double _bLT;
        protected double _cLT;
        protected double _dLT;
        protected double _eLT;
        protected double _cyy;
        protected double _cyz;
        protected double _czy;
        protected double _czz;

        protected double _epsilony;

        protected double _NbRdy; //resistenza per buckling
        protected double _NbRdz; //resistenza per buckling
        protected double _NbRdT; //resistenza per buckling

        protected double _MbRdy; //resistenza per buckling
        protected double _MbRdz; //resistenza per buckling

        protected double _kyy;
        protected double _kyz;
        protected double _kzy;
        protected double _kzz;

        bool _createReport;
        Word._Document _wordDocument;
        #endregion

        #region Properties
        public double NRd => _NRd;
        public double VplRdy => _VplRdy;
        public double VplRdz => _VplRdz;
        public double VplTRdy => _VplTRdy;
        public double VplTRdz => _VplTRdz;
        public double MRdNy => _MRdNy;
        public double MRdNz => _MRdNz;
        public double McRdy => _McRdy;
        public double McRdz => _McRdz;
        public double MvRdy => _MvRdy;
        public double MvRdz => _MvRdz;
        public double TRd => _TRd;
        public double VyEd => _VyEd;
        public double VzEd => _VzEd;
        public double MyEd => _MyEd;
        public double MzEd => _MzEd;

        public int ClassificationSection => _classificationSection;

        public double Aeff {
            get {
                if (_classificationSection == 4) {
                    return _Aeff;
                }
                else
                {
                    return 0;
                }
            }
        }
        public double Weffy
        {
            get
            {
                if (_classificationSection == 4)
                {
                    return _Weffy;
                }
                else
                {
                    return 0;
                }
            }
        }
        public double J2eff
        {
            get
            {
                if (_classificationSection == 4)
                {
                    return _J2eff;
                }
                else
                {
                    return 0;
                }
            }
        }
        public double Weffz
        {
            get
            {
                if (_classificationSection == 4)
                {
                    return _Weffz;
                }
                else
                {
                    return 0;
                }
            }
        }
        public double J1eff
        {
            get
            {
                if (_classificationSection == 4)
                {
                    return _J1eff;
                } else
                {
                    return 0;
                }
            }
        }
        public Point2d CentroidEff
        {
            get
            {
                if (_classificationSection == 4)
                {
                    return _centroidEff;
                } else
                {
                    return _sec.Centroid;
                }
            }
        }

        public double L0y => _L0y;
        public double L0z => _L0z;
        public double L0LT => _L0LT;

        public double Ncry => _Ncry;
        public double Ncrz => _Ncrz;
        public double NcrT => _NcrTorsional;
        public double NcrTF => _NcrFlexuralTorsional;

        public double McrLateralTorsional => _McrLateralTorsional;
        public double Mcr0LateralTorsional => _Mcr0LateralTorsional;

        public double C1 => _c1;
        public double C2 => _c2;
        public double C3 => _c3;
        public double Zg => _zg;
        public double Zj => _zj;

        public double Alphay => _alphay;
        public double Alphaz => _alphaz;
        public double AlphaLT => _alphaLT;
        public double AlphaT => _alphaT;

        public double Phiy => _Phiy;
        public double Phiz => _Phiz;
        public double PhiLT => _PhiLT;
        public double PhiT => _PhiT;

        public double Chiy => _Chiy;
        public double Chiz => _Chiz;
        public double ChiLT => _ChiLT;
        public double ChiT => _ChiT;

        public double NbRdy => _NbRdy;
        public double NbRdz => _NbRdz;
        public double NbRdT => _NbRdT;

        public double MbRdy => _MbRdy;
        public double MbRdz => _MbRdz;

        public double Muy => _muy;
        public double Muz => _muz;

        public double Lambday => _lambday;
        public double Lambdaz => _lambdaz;
        public double LambdaT => _lambdaT;
        public double LambdaLT => _lambdaLT;
        public double Lambda0 => _lambda0;

        public double Wy => _wy;
        public double Wz => _wz;

        public double Epsilony => _epsilony;

        public double Cmy0 => _cmy0;
        public double Cmz0 => _cmz0;

        public double Cmy => _cmy;
        public double Cmz => _cmz;
        public double CmLT => _cmLT;

        public double ALT => _aLT;
        public double BLT => _bLT;
        public double CLT => _cLT;
        public double DLT => _dLT;
        public double ELT => _eLT;

        public double Cyy => _cyy;
        public double Cyz => _cyz;
        public double Czy => _czy;
        public double Czz => _czz;

        public double Kyy => _kyy;
        public double Kyz => _kyz;
        public double Kzy => _kzy;
        public double Kzz => _kzz;

        public double WRAxial { get; set; }
        public double WRShear1 { get; set; }
        public double WRShear2 { get; set; }
        public double WRBending2 { get; set; }
        public double WRBending1 { get; set; }
        public double WRTorsion { get; set; }
        public double WRCombined { get; set; }
        public double WRBuckling1 { get; set; }
        public double WRBuckling2 { get; set; }
        public double WRBuckling3 { get; set; }
        public double WRMax { get; set; }
        #endregion

        #region Constructor
        public EuroCodeBeamChecker(Section sect, double NEd, double V1Ed, double V2Ed, double M1Ed, double M2Ed, double TEd, Annex annex, bool createReport = false)
        {
            _createReport = createReport;

            _sec = sect;
            _annex = annex;

            _NEd = NEd;
            _V1Ed = V1Ed; //horizontal
            _V2Ed = V2Ed; //vertical
            _M1Ed = M1Ed;
            _M2Ed = M2Ed;
            _TEd = TEd;

            CheckResistance();
        }
        #endregion

        #region Public Function
        public void CheckResistance() {

            NewParagraph("Safety Factor");
            NewFormula(@"\gamma_{m0} = " + _annex.Gm0.ToString(_formatDouble));
            NewFormula(@"\gamma_{m1} = " + _annex.Gm1.ToString(_formatDouble));
            NewFormula(@"\gamma_{m2} = " + _annex.Gm2.ToString(_formatDouble));

            NewParagraph("Acting forces and moments");
            NewFormula(@"N_{Ed} = " + (_NEd/1000.0).ToString(_formatDouble) + " kN");
            NewFormula(@"V_{1,Ed} = " + (_V1Ed/1000.0).ToString(_formatDouble) + " kN");
            NewFormula(@"V_{2,Ed} = " + (_V2Ed/1000.0).ToString(_formatDouble) + " kN");
            NewFormula(@"M_{1,Ed} = " + (_M1Ed/1e6).ToString(_formatDouble) + " kNm");
            NewFormula(@"M_{2,Ed} = " + (_M2Ed/1e6).ToString(_formatDouble) + " kNm");
            NewFormula(@"T_{Ed} = " + (_TEd/1e6).ToString(_formatDouble) + " kNm");

            NewParagraph("Section Properties");
            string sectionDescription = _sec.ToString();
            string[] rws = sectionDescription.Split('\n');
            foreach (string s in rws)
            {
                NewParagraph(s);
            }            
            NewFormula(@"A = " + _sec.Area.ToString(_formatDouble) + " mm^2");
            NewFormula(@"x_G = " + _sec.Centroid.X.ToString(_formatDouble) + " mm");
            NewFormula(@"y_G = " + _sec.Centroid.Y.ToString(_formatDouble) + " mm");
            NewFormula(@"\vartheta = " + (_sec.AngleX1 * 180.0 / Math.PI).ToString(_formatDouble) + " deg");
            NewFormula(@"x_C = " + _sec.ShearCenter.X.ToString(_formatDouble) + " mm");
            NewFormula(@"y_C = " + _sec.ShearCenter.Y.ToString(_formatDouble) + " mm");
            NewFormula(@"J_{max} = " + _sec.J22.ToString(_formatExponential) + " mm^6");
            NewFormula(@"J_{min} = " + _sec.J11.ToString(_formatExponential) + " mm^6");
            NewFormula(@"J_t = " + _sec.Jt.ToString(_formatExponential) + " mm^6");
            NewFormula(@"J_w = " + _sec.Jw.ToString(_formatExponential) + " mm^6");
            NewFormula(@"W_{el,max} = " + _sec.Wel22Min.ToString(_formatExponential) + " mm^3");
            NewFormula(@"W_{el,min} = " + _sec.Wel11Min.ToString(_formatExponential) + " mm^3");
            NewFormula(@"W_{pl,max} = " + _sec.Wpl22.ToString(_formatExponential) + " mm^3");
            NewFormula(@"W_{pl,min} = " + _sec.Wpl11.ToString(_formatExponential) + " mm^3");


            #region classification
            double minSigma = _sec.MinSigma(_NEd, _M2Ed, _M1Ed);
            NewFormula(@"\sigma_{Min} = f(N/A,M/W) = " + minSigma.ToString(_formatDouble) + " MPa");

            double fy = ((SteelMaterial)_sec.Material).Fyk;
            NewFormula("f_y = " + fy + " MPa");

            if (minSigma < 0)
            {
                NewParagraph("Classification");
                double epsilon = Math.Sqrt(235.0 / fy);
                NewFormula(@"\varepsilon = \sqrt{235/f_y} = " + epsilon.ToString(_formatDouble));

                Type typeShape = _sec.GetType();
                if (typeShape == typeof(SectionCHS))
                {
                    SectionCHS sec = (SectionCHS)_sec;
                    double D = sec.D;
                    double t = sec.T;
                    if (D / t <= 50.0 * epsilon * epsilon)
                    {
                        _classificationSection = 1;
                        NewFormula(@"D/t \leq 50 \varepsilon^2 -> " + _classificationSection.ToString(_formatInt));
                    }
                    else if (D / t <= 70.0 * epsilon * epsilon)
                    {
                        _classificationSection = 2;
                        NewFormula(@"D/t \leq 70 \varepsilon^2 -> " + _classificationSection.ToString(_formatInt));
                    }
                    else if (D / t <= 90.0 * epsilon * epsilon)
                    {
                        _classificationSection = 3;
                        NewFormula(@"D/t \leq 90 \varepsilon^2 -> " + _classificationSection.ToString(_formatInt));
                    }
                    else
                    {
                        _classificationSection = 4;
                        NewFormula(@"D/t \geq 90 \varepsilon^2 -> " + _classificationSection.ToString(_formatInt));
                        throw new Exception("CHS class 4 not supported");
                    }
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    SectionRHS sec = (SectionRHS)_sec;

                    #region ClassificationAxialBendingStrongAxis
                    if (Math.Abs(_M2Ed) > 0 || Math.Abs(_NEd) > 0)
                    {
                        double cTFlange;
                        /*if (M2Ed > 0)
                        {
                            cTFlange = sec.Bint / sec.TTop;
                        } else
                        {
                            cTFlange = sec.Bint / sec.TBottom;
                        }*/
                        cTFlange = Math.Max(sec.Bint / sec.TBottom, sec.Bint / sec.TTop);
                        NewFormula(@"c_{flange}/t_{flange} = " + cTFlange.ToString(_formatDouble));

                        //flange are load with constant load
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTFlange, epsilon));

                        //classification webs
                        //from equilibrium of Σ sigma = Ned
                        if (sec.TTop == sec.TBottom && sec.TWebLeft == sec.TWebRight && _classificationSection <= 3)
                        {
                            double alphaClassification = -_NEd / (4.0 * sec.ThicknessWeb * fy * sec.Hw) + 0.5;

                            //from equlibrium sigma = N/A+M/W:
                            double psiClassification = 1;
                            if (_classificationSection < 4)
                            {
                                psiClassification = -_NEd * 2.0 / (sec.Area * fy) - 1.0;
                            }

                            double cTWeb = sec.Hw / sec.ThicknessWeb;
                            NewFormula(@"c_{web}/t_{web} = " + cTWeb.ToString(_formatDouble));
                            _classificationSection = Math.Max(_classificationSection, GetClassInnerPlate(cTWeb, epsilon, alphaClassification, psiClassification));
                        }
                        else
                        {
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Hw / sec.TWebLeft, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Hw / sec.TWebRight, epsilon));
                        }
                    }
                    #endregion

                    #region ClassificationAxialBendingWeakAxis
                    if (Math.Abs(_M1Ed) > 0)
                    {
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Hw / sec.TWebLeft, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Hw / sec.TWebRight, epsilon));

                        if (sec.TWebLeft == sec.TWebRight && sec.TTop == sec.TBottom)
                        {
                            double alphaClassification = -_NEd / (4.0 * sec.ThicknessFlange * fy * sec.Bint) + 0.5;
                            double psiClassification = -_NEd * 2.0 / (sec.Area * fy) - 1.0;
                            _classificationSection = Math.Max(_classificationSection, GetClassInnerPlate(sec.Bint / sec.TTop, epsilon, alphaClassification, psiClassification));
                        } else
                        {
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Bint / sec.TTop, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(sec.Bint / sec.TBottom, epsilon));
                        }
                    }
                    NewParagraph(@"Classification RHS : " + _classificationSection.ToString(_formatInt));
                    #endregion
                } else if (typeShape == typeof(SectionH)) {
                    SectionH sec = (SectionH)_sec;
                    double cTWeb = sec.HeightWeb / sec.ThicknessWeb;
                    NewFormula(@"c_{web}/t_{web} = " + cTWeb.ToString(_formatDouble));
                    double cTFlangeTop = (sec.LenghtTopFlange / 2.0 - sec.ThicknessWeb / 2.0) / sec.ThicknessTopFlange;
                    NewFormula(@"c_{top}/t_{top} = " + cTFlangeTop.ToString(_formatDouble));
                    double cTFlangeBottom = (sec.LenghtBottomFlange / 2.0 - sec.ThicknessWeb / 2.0) / sec.ThicknessBottomFlange;
                    NewFormula(@"c_{bottom}/t_{bottom} = " + cTFlangeBottom.ToString(_formatDouble));

                    if (sec.IsDoubleSymmetric)
                    {
                        #region AxialAndBendingStrongDirection
                        if (Math.Abs(_M2Ed) > 0 || Math.Abs(_NEd) > 0)
                        {
                            //classification of flanged for axial force due to bending
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));

                            //classification web
                            double alphaClassification = -_NEd / (2.0 * sec.HeightWeb * fy * sec.ThicknessWeb) + 0.5;
                            double psiClassification = 1;
                            if (_classificationSection < 4)
                            {
                                psiClassification = -2.0 * _NEd / (sec.Area * fy) - 1.0;
                            }
                            _classificationSection = Math.Max(_classificationSection, GetClassInnerPlate(cTWeb, epsilon, alphaClassification, psiClassification));
                        }
                        #endregion

                        #region AxialAndBendingWeakDirection
                        if (Math.Abs(_M1Ed) > 0)
                        {
                            //classification only for Compression.
                            //Other detailed calculation should be found and implemented
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTWeb, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTFlangeTop, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTFlangeBottom, epsilon));
                        }
                        #endregion
                    }
                    else
                    {
                        //classification only for Compression.
                        //Other detailed calculation should be found and implemented
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTWeb, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeBottom, epsilon));
                    }
                    NewParagraph(@"Classification H : " + _classificationSection.ToString(_formatInt));
                } else if (typeShape == typeof(SectionC)) {
                    SectionC sec = (SectionC)_sec;
                    double cTWeb = sec.Hw / sec.Tw;
                    NewFormula(@"c_{web}/t_{web} = " + cTWeb.ToString(_formatDouble));
                    double cTFlangeTop = (sec.LTop - sec.Tw) / sec.ThicknessTop;
                    NewFormula(@"c_{top}/t_{top} = " + cTFlangeTop.ToString(_formatDouble));
                    double cTFlangeBottom = (sec.LBottom - sec.Tw) / sec.ThicknessBottom;
                    NewFormula(@"c_{bottom}/t_{bottom} = " + cTFlangeBottom.ToString(_formatDouble));

                    if (sec.IsSymmetricAlongYLocalAxis)
                    {
                        #region AxialAndBendingStrongDirection
                        //in web the compression zone "annul" the tension zone also for bendings moments
                        if (Math.Abs(_M2Ed) > 0 || Math.Abs(_NEd) > 0)
                        {
                            //classification of flanged for axial force due to bending
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));

                            //classification web
                            double alphaClassification = -_NEd / (2.0 * sec.Hw * fy * sec.Tw) + 0.5;
                            double psiClassification = 1;
                            if (_classificationSection < 4)
                            {
                                psiClassification = -2.0 * _NEd / (sec.Area * fy) - 1.0;
                            }
                            _classificationSection = Math.Max(_classificationSection, GetClassInnerPlate(cTWeb, epsilon, alphaClassification, psiClassification));
                        }
                        #endregion

                        #region AxialAndBendingWeakDirection
                        if (Math.Abs(_M1Ed) > 0)
                        {
                            //classification only for Compression.
                            //Other detailed calculation should be found and implemented
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTWeb, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTFlangeTop, epsilon));
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTFlangeBottom, epsilon));
                        }
                        #endregion
                    }
                    else
                    {
                        //classification only for Compression.
                        //Other detailed calculation should be found and implemented
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTWeb, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeBottom, epsilon));
                    }
                    NewParagraph(@"Classification C : " + _classificationSection.ToString(_formatInt));
                }
                else if (typeShape == typeof(SectionT))
                {
                    SectionT sec = (SectionT)_sec;
                    double cTWeb = sec.Hw / sec.Tw;
                    NewFormula(@"c_{web}/t_{web} = " + cTWeb.ToString(_formatDouble));
                    double cTFlangeTop = (sec.B - sec.Tw) / 2.0 / sec.Tf;
                    NewFormula(@"c_{top}/t_{top} = " + cTFlangeTop.ToString(_formatDouble));


                    if (_NEd < 0)
                    {
                        //classification only for Compression.
                        //Other detailed calculation should be found and implemented
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTWeb, epsilon));
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                    } else 
                    {
                        //Ned >= 0
                        if (_M2Ed < 0)
                        {
                            _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                            //due to NEd >= 0 --> neutral axis max in plastic neutral axis for NEd = 0:
                            double alpha = sec.yPlastic / sec.Hw;
                            if (alpha <= 1.0 && alpha >= 0.0)
                            {
                                if (cTWeb <= 9.0 * epsilon / alpha)
                                {
                                    _classificationSection = Math.Max(_classificationSection, 1);
                                }
                                else if (cTWeb <= 10.0 * epsilon / alpha)
                                {
                                    _classificationSection = Math.Max(_classificationSection, 2);
                                }
                                else if (cTWeb <= 14.0 * epsilon)
                                {
                                    _classificationSection = Math.Max(_classificationSection, 3);
                                }
                                else
                                {
                                    _classificationSection = Math.Max(_classificationSection, 4);
                                }
                            } else
                            {
                                throw new Exception("alpha classification T section");
                            }

                        }
                        if (_M1Ed != 0)
                        _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                    }
                    NewParagraph(@"Classification T : " + _classificationSection.ToString(_formatInt));
                }
                else if (typeShape == typeof(SectionL))
                {
                    SectionL sec = (SectionL)_sec;
                    double cT1 = sec.LHor / sec.THor;
                    NewFormula(@"c_{hor}/t_{hor} = " + cT1.ToString(_formatDouble));
                    double cT2 = sec.LVert / sec.TVert;
                    NewFormula(@"c_{vert}/t_{vert} = " + cT2.ToString(_formatDouble));

                    //classification only for Compression.
                    //Other detailed calculation should be found and implemented
                    _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cT1, epsilon));
                    _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cT2, epsilon));
                }
                else
                {
                    throw new Exception("Classification of this kind of section not yet implemented");
                }
            } else
            {
                //Section with no compression
                _classificationSection = 1;
            }

            if (_classificationSection == 4)
            {
                NewParagraph("Class 4 Section Properties:");
                Class4Section secCL4 = new Class4Section(_sec);

                secCL4.Calc(0, Math.Sign(_M2Ed) * 1e6, 0); //indipendent from the value but dependent on the sign
                _Weffy = secCL4.Weff2;
                _J2eff = secCL4.J2eff;
                NewFormula(@"W_{eff,y}: " + _Weffy.ToString(_formatExponential));
                NewFormula(@"J_{eff,y}: " + _J2eff.ToString(_formatExponential));

                secCL4.Calc(0, 0, Math.Sign(_M1Ed) * 1e6); //indipendent from the value but dependent on the sign
                _Weffz = secCL4.Weff1;
                _J1eff = secCL4.J1eff;
                NewFormula(@"W_{eff,z}: " + _Weffz.ToString(_formatExponential));
                NewFormula(@"J_{eff,z}: " + _J1eff.ToString(_formatExponential));

                secCL4.Calc(-1000, 0, 0); //indipendent from the value but dependent on the sign
                _Aeff = secCL4.Aeff;
                NewFormula(@"A_{eff}: " + _Aeff.ToString(_formatExponential));
                
                _deltaG = _sec.Centroid - secCL4.CentroidEff;
                _centroidEff = secCL4.CentroidEff;
                NewFormula(@"z_{G.eff}: " + _centroidEff.X.ToString(_formatDouble));
                NewFormula(@"y_{G,eff}: " + _centroidEff.Y.ToString(_formatDouble));
            }

            #endregion

            #region resistance
            {
                #region axial
                if (_NEd > 0)
                {
                    double Anet = _sec.Area;
                    _NRd = GetNtRd(Anet);
                }
                else
                {
                    _NRd = GetNcRd();
                }
                WRAxial = Math.Abs(_NEd) / _NRd;
                #endregion

                #region ShearAndTorsion
                GetVRdTRd(_V1Ed, _V2Ed, out _VyEd, out _VzEd, out _VplTRdy, out _VplTRdz, out _TRd, out _VplRdz, out _VplRdy);
                WRShear2 = Math.Abs(VzEd) / _VplTRdz; //vertical
                WRShear1 = Math.Abs(VyEd) / _VplTRdy; //horizontal
                WRTorsion = Math.Abs(_TEd) / _TRd;

                WRMax = Math.Max(WRShear1, WRAxial);
                WRMax = Math.Max(WRShear2, WRMax);
                WRMax = Math.Max(WRTorsion, WRMax);
                #endregion

                #region bending
                GetMRd(_M1Ed, _M2Ed, out _MzEd, out _MyEd, out _MRdNz, out _MRdNy, out _McRdy, out _McRdz, out _MvRdy, out _MvRdz);
                WRBending1 = Math.Abs(MzEd) / _MRdNz;
                WRBending2 = Math.Abs(MyEd) / _MRdNy;

                WRMax = Math.Max(WRBending1, WRMax);
                WRMax = Math.Max(WRBending2, WRMax);

                WRCombined = GetWrCombined();
                WRMax = Math.Max(WRCombined, WRMax);
                #endregion
            }
            #endregion
        }

        public void CheckBuckling(double L, double betay, double betaz, double betaLT, SupportCondition supportConditiony, LoadCondition loadConditiony, double? psiy, SupportCondition supportConditionz, LoadCondition loadConditionz, double? psiz) {
            NewParagraph("Buckling");
            if (_sec.IsSymmetricAlongYLocalAxis == false && _sec.IsSymmetricAlongZLocalAxis == false)
            {
                throw new Exception("No symmetry : §6.3.3 unapplicable, this element should be verified with §6.3.4 EN 1991-1-1.");
            }
            //Should be valid only for Double Symmetric, 1 axis of symmetry accepted but should be show a warning
            _L = L;
            _betaLT = betaLT;
            _betay = betay;
            _betaz = betaz;

            _supportConditiony = supportConditiony;
            _supportConditionz = supportConditionz;

            _loadConditiony = loadConditiony;
            _loadConditionz = loadConditionz;

            _psiy = psiy;
            _psiz = psiz;

            #region buckling
            {
                double NEd;
                double fy = ((SteelMaterial)_sec.Material).Fyk;

                if (_sec.MinSigma(_NEd, _M2Ed, _M1Ed) < 0.0)
                {
                    if (_NEd < 0)
                    {
                        NEd = -_NEd;
                    } else
                    {
                        NEd = 0;
                    }
                    double AbsMzEd = Math.Abs(_MzEd);
                    double AbsMyEd = Math.Abs(_MyEd);

                    double E = _sec.Material.E;
                    double G = E / (2.0 * (1.0 + _sec.Material.Ni));

                    _L0y = _betay * _L;
                    NewFormula(@"L_{0,y} = \beta_y \cdot L = " + (_L0y).ToString(_formatInt) + " mm");
                    _L0z = _betaz * _L;
                    NewFormula(@"L_{0,z} = \beta_z \cdot L = " + (_L0z).ToString(_formatInt) + " mm");
                    _L0LT = _betaLT * _L;
                    NewFormula(@"L_{0,LT} = \beta_{LT} \cdot L = " + (_L0LT).ToString(_formatInt) + " mm");

                    if (_classificationSection < 4)
                    {
                        _Ncry = GetNcrEuler(E, _sec.J22, _L0y);
                        NewFormula(@"N_{cr,y} = \pi^2 \cdot E \cdot J_y / L_{0y}^2 = " + (_Ncry / 1000.0).ToString(_formatDouble) + " kN");
                        _Ncrz = GetNcrEuler(E, _sec.J11, _L0z);
                        NewFormula(@"N_{cr,z} = \pi^2 \cdot E \cdot J_z / L_{0z}^2 = " + (_Ncrz / 1000.0).ToString(_formatDouble) + " kN");

                        _lambday = GetLambdaSegn(_sec.Area, fy, _Ncry);
                        NewFormula(@"\lambda_{y} = \sqrt{A \cdot f_y / N_{cr,y}} = " + (_lambday).ToString(_formatDouble) + " ");
                        
                        _lambdaz = GetLambdaSegn(_sec.Area, fy, _Ncrz);
                        NewFormula(@"\lambda_{z} = \sqrt{A \cdot f_y / N_{cr,z}} = " + (_lambdaz).ToString(_formatDouble) + " ");
                    } else
                    {
                        _Ncry = GetNcrEuler(E, _J2eff, _L0y);
                        NewFormula(@"N_{cr,y} = \pi^2 \cdot E \cdot J_y / L_{0y}^2 = " + (_Ncry / 1000.0).ToString(_formatDouble) + " kN");
                        _Ncrz = GetNcrEuler(E, _J1eff, _L0z);
                        NewFormula(@"N_{cr,z} = \pi^2 \cdot E \cdot J_z / L_{0z}^2 = " + (_Ncrz / 1000.0).ToString(_formatDouble) + " kN");

                        _lambday = GetLambdaSegn(_Aeff, fy, _Ncry);
                        NewFormula(@"\lambda_{y} = \sqrt{A \cdot f_y / N_{cr,y}} = " + (_lambday).ToString(_formatDouble) + " ");
                        _lambdaz = GetLambdaSegn(_Aeff, fy, _Ncrz);
                        NewFormula(@"\lambda_{z} = \sqrt{A \cdot f_y / N_{cr,z}} = " + (_lambdaz).ToString(_formatDouble) + " ");
                    }

                    GetImperfectionFactor(out _alphay, out _alphaz);

                    _Phiy = GetPhi(_alphay, _lambday);
                    NewFormula(@"\Phi_{y} = 0.5 \cdot \left[ 1 + \alpha_{y} \cdot ( \lambda_{y} - 0.2 ) + \lambda_{y}^{2} \right] = " + (_Phiy).ToString(_formatDouble) + " ");
                    _Phiz = GetPhi(_alphaz, _lambdaz);
                    NewFormula(@"\Phi_{z} = 0.5 \cdot \left[ 1 + \alpha_{z} \cdot ( \lambda_{z} - 0.2 ) + \lambda_{z}^{2} \right] = " + (_Phiy).ToString(_formatDouble) + " ");

                    _Chiy = GetChi(_Phiy, _lambday);
                    NewFormula(@"\chi_{y} = \frac{1}{\Phi_y + \sqrt{\Phi_y^2 - \lambda_y^2}} = " + (_Chiy).ToString(_formatDouble) + " ");
                    _Chiz = GetChi(_Phiz, _lambdaz);
                    NewFormula(@"\chi_{z} = \frac{1}{\Phi_z + \sqrt{\Phi_z^2 - \lambda_z^2}} = " + (_Chiy).ToString(_formatDouble) + " ");

                    if (_classificationSection < 4)
                    {
                        _NbRdy = _Chiy * _sec.Area * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,y} = \chi_y \cdot A \cdot f_y / \gamma_{m1} = " + (_NbRdy / 1000.0).ToString(_formatDouble) + " kN");
                        _NbRdz = _Chiz * _sec.Area * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,z} = \chi_z \cdot A \cdot f_y / \gamma_{m1} = " + (_NbRdz / 1000.0).ToString(_formatDouble) + " kN");

                    } else
                    {
                        _NbRdy = _Chiy * _Aeff * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,y} = \chi_y \cdot A_{eff} \cdot f_y / \gamma_{m1} = " + (_NbRdy / 1000.0).ToString(_formatDouble) + " kN");
                        _NbRdz = _Chiz * _Aeff * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,z} = \chi_z \cdot A_{eff} \cdot f_y / \gamma_{m1} = " + (_NbRdz / 1000.0).ToString(_formatDouble) + " kN");
                    }
                    
                    double iy = _sec.InertiaRadius1;
                    NewFormula(@"i_y = \sqrt{J_y / A} = " + (iy).ToString(_formatDouble) + " mm");
                    double iz = _sec.InertiaRadius2;
                    NewFormula(@"i_z = \sqrt{J_z / A} = " + (iz).ToString(_formatDouble) + " mm");

                    Point2d shearCenterToCentroid = _sec.ShearCenter - _sec.Centroid;
                    NewFormula(@"z_0 = z_C - z_G = " + (shearCenterToCentroid.X).ToString(_formatDouble) + " mm");
                    NewFormula(@"y_0 = y_C - y_G = " + (shearCenterToCentroid.Y).ToString(_formatDouble) + " mm");

                    _NcrTorsional = GetNcrT(iy, iz, shearCenterToCentroid.Y, shearCenterToCentroid.X, E, G, _sec.Jt, _sec.Jw, _L0LT);
                    //report written inside the last function

                    _NcrFlexuralTorsional = GetNcrTF(iy, iz, shearCenterToCentroid.X, shearCenterToCentroid.Y, _Ncry, _Ncrz, _NcrTorsional);
                    //report written inside the last function

                    if (_classificationSection < 4)
                    {
                        _lambdaT = GetLambdaSegn(_sec.Area, fy, Math.Min(_NcrTorsional, _NcrFlexuralTorsional));
                        NewFormula(@"\lambda_{T} = \sqrt{A \cdot f_y / min(N_{cr,T}, N_{cr,TF})} = " + (_lambdaT).ToString(_formatDouble) + " ");
                    }
                    else
                    {
                        _lambdaT = GetLambdaSegn(_Aeff, fy, Math.Min(_NcrTorsional, _NcrFlexuralTorsional));
                        NewFormula(@"\lambda_{T} = \sqrt{A_{eff} \cdot f_y / min(N_{cr,T}, N_{cr,TF})} = " + (_lambdaT).ToString(_formatDouble) + " ");
                    }

                    _alphaT = _alphaz;
                    NewFormula(@"\alpha_{T} = \alpha_z = " + (_alphaT).ToString(_formatDouble) + " ");
                    _PhiT = GetPhi(_alphaT, _lambdaT);
                    NewFormula(@"\Phi_{z} = 0.5 \cdot \left[ 1 + \alpha_{T} \cdot ( \lambda_{T} - 0.2 ) + \lambda_{T}^{2} \right] = " + (_PhiT).ToString(_formatDouble) + " ");

                    _ChiT = GetChi(_PhiT, _lambdaT);
                    NewFormula(@"\chi_{T} = \frac{1}{\Phi_T + \sqrt{\Phi_T^2 - \lambda_T^2}} = " + (_Chiy).ToString(_formatDouble) + " ");
                   
                    if (_classificationSection < 4)
                    {
                        _NbRdT = _ChiT * _sec.Area * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,T} = \chi_T \cdot A \cdot f_y / \gamma_{m1} = " + (_NbRdT / 1000.0).ToString(_formatDouble) + " kN");
                    } else
                    {
                        _NbRdT = _ChiT * _Aeff * fy / _annex.Gm1;
                        NewFormula(@"N_{b,Rd,T} =  \chi_T \cdot A_{eff} \cdot f_y / \gamma_{m1} = " + (_NbRdT / 1000.0).ToString(_formatDouble) + " kN");
                    }

                    NewParagraph("Calculation of Mcr");
                    _McrLateralTorsional = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _sec.J11, E, G, _supportConditiony, _loadConditiony, _psiy, 1, 1, out _c1, out _c2, out _c3, out _zg, out _zj);

                    if (_classificationSection < 3) {
                        _lambdaLT = GetLambdaSegn(_sec.Wpl22, fy, _McrLateralTorsional);
                        NewFormula(@"\lambda_{LT} = \sqrt{ W_{pl,y} \cdot f_{y} / M_{cr,LT} } = " + _lambdaLT.ToString(_formatDouble) + "");
                    } else if (_classificationSection == 3) {
                        _lambdaLT = GetLambdaSegn(_sec.Wel22Min, fy, _McrLateralTorsional);
                        NewFormula(@"\lambda_{LT} = \sqrt{ W_{el,y} \cdot f_{y} / M_{cr,LT} } = " + _lambdaLT.ToString(_formatDouble) + "");
                    } else
                    {
                        _lambdaLT = GetLambdaSegn(_Weffy, fy, _McrLateralTorsional);
                        NewFormula(@"\lambda_{LT} = \sqrt{ W_{eff,y} \cdot f_{y} / M_{cr,LT} } = " + _lambdaLT.ToString(_formatDouble) + "");
                    }

                    _alphaLT = GetImperfectionFactorLT(_useEquation_6_57);
                    NewFormula(@"\alpha_{LT} = " + _alphaLT.ToString(_formatDouble) + "");

                    NewParagraph("kc factor using Table 6.6 EN 1993-1-1:");
                    double kc = Getkc(_lambdaLT, _supportConditiony, _loadConditiony, _psiy);
                    NewFormula(@"k_c = " + kc.ToString(_formatDouble) + "");

                    if (_sec.GetType() == typeof(SectionH) && _useEquation_6_57 == true)
                    {
                        NewFormula(@"\beta = " + _annex.Beta);
                        NewFormula(@"\lambda_{LT0} = " + _annex.LambdaLT0);

                        _PhiLT = GetPhi(_alphaLT, _lambdaLT, _annex.Beta, _annex.LambdaLT0);
                        NewFormula(@"\Phi_{LT} = 0.5 \cdot [ 1 + \alpha_{LT} \cdot ( \lambda_{LT} - \lambda_{LT0} ) + \beta \cdot \lambda_{LT}^{2} ] = " + _PhiLT.ToString(_formatDouble) + "");

                        double factorF = Math.Min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * Math.Pow(_lambdaLT - 0.8, 2.0)));
                        NewFormula(@"f = min(1, 1 - 0.5 \cdot (1 - k_c) * (1 - 2 \cdot (_lambda_{LT} - 0.8)^{2} = " + factorF.ToString(_formatDouble) + "");
                        
                        _ChiLT = GetChiLTmod(_PhiLT, _lambdaLT, _annex.Beta, factorF);
                        NewFormula(@"\chi_{LT} = \frac{1}{ \Phi_{LT} + \sqrt{ \Phi_{LT}^2 - \lambda_{LT}^2 } } = " + _ChiLT.ToString(_formatDouble) + "");
                    } else
                    {
                        _PhiLT = GetPhi(_alphaLT, _lambdaLT);
                        NewFormula(@"\Phi_{LT} =  0.5 \cdot [ 1 + \alpha_{LT} \cdot ( \lambda_{LT} - 0.2 ) + \lambda_{LT}^{2} ] = " + _PhiLT.ToString(_formatDouble) + "");
                        _ChiLT = GetChi(_PhiLT, _lambdaLT);
                        NewFormula(@"\chi_{LT} = \frac{1}{ \Phi_{LT} + \sqrt{ \Phi_{LT}^2 - \lambda_{LT}^2 } } =" + _ChiLT.ToString(_formatDouble) + "");
                    }

                    if (_classificationSection < 3)
                    {
                        _MbRdy = _ChiLT * _sec.Wpl22 * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,y} = \chi_{LT} \cdot W_{pl,y} \cdot f_{y} / \gamma_{m1} = " + (_MbRdy / 1e6).ToString(_formatDouble) + " kNm");
                        _MbRdz = _sec.Wpl11 * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,z} = W_{pl,z} \cdot f_{y} / \gamma_{m1} = " + (_MbRdz / 1e6).ToString(_formatDouble) + " kNm");
                    }
                    else if (_classificationSection == 3)
                    {
                        _MbRdy = _ChiLT * _sec.Wel22Min * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,y} = \chi_{LT} \cdot W_{el,y} \cdot f_{y} / \gamma_{m1} = " + (_MbRdy / 1e6).ToString(_formatDouble) + " kNm");
                        _MbRdz = _sec.Wel11Min * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,z} = W_{el,z} \cdot f_{y} / \gamma_{m1} = " + (_MbRdz / 1e6).ToString(_formatDouble) + " kNm");
                    }
                    else
                    {
                        _MbRdy = _ChiLT * _Weffy * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,y} = \chi_{LT} \cdot W_{eff,y} \cdot f_{y} / \gamma_{m1} = " + (_MbRdy / 1e6).ToString(_formatDouble) + " kNm");
                        _MbRdz = _Weffz * fy / _annex.Gm1;
                        NewFormula(@"M_{b,Rd,z} =  W_{eff,z} \cdot f_{y} / \gamma_{m1} = " + (_MbRdz / 1e6).ToString(_formatDouble) + " kNm");
                    }

                    if (_method1AnnexA)
                    {
                        NewParagraph("Method 1 - Annex A - EN 1993-1-1");
                        
                        //Annex A
                        double? MEdyMax = null;
                        double? deflectiony = null;
                        double? MEdzMax = null;
                        double? deflectionz = null;

                        _cmy0 = GetCMi0(_loadConditiony, _supportConditiony, _psiy, MEdyMax, deflectiony, NEd, _Ncry);
                        NewFormula(@"c_{my0} = " + (_cmy0).ToString(_formatDouble) + "");
                        _cmz0 = GetCMi0(_loadConditionz, _supportConditionz, _psiz, MEdzMax, deflectionz, NEd, _Ncrz);
                        NewFormula(@"c_{mz0} = " + (_cmz0).ToString(_formatDouble) + "");

                        _muy = GetMu(NEd, _Ncry, _Chiy);
                        NewFormula(@"\mu_y = \frac{1-N_{Ed}/N_{cr,y}}{1 - \chi_y \cdot N_{Ed}/N_{cr_y}} = " + (_muy).ToString(_formatDouble) + "");
                        
                        _muz = GetMu(NEd, _Ncrz, _Chiz);
                        NewFormula(@"\mu_z = \frac{1-N_{Ed}/N_{cr,z}}{1 - \chi_z \cdot N_{Ed}/N_{cr_z}} = " + (_muz).ToString(_formatDouble) + "");

                        if (_classificationSection < 3) //Rules for member stability in en 1993-1-1 pg. 113
                        {
                            _wy = Math.Min(_sec.Wpl22 / _sec.Wel22Min, 1.5);
                            NewFormula(@"w_y = min(W_{pl,y} / W_{el,y} , 1.5) = " + (_wy).ToString(_formatDouble) + "");
                            _wz = Math.Min(_sec.Wpl11 / _sec.Wel11Min, 1.5);
                            NewFormula(@"w_z = min(W_{pl,z} / W_{el,z} , 1.5) = " + (_muy).ToString(_formatDouble) + "");
                        } else
                        {
                            _wy = 1.0; //e con Weff?
                            NewFormula(@"w_y = " + (_wy).ToString(_formatDouble) + "");
                            _wz = 1.0; //e con Weff?
                            NewFormula(@"w_z = " + (_wz).ToString(_formatDouble) + "");
                        }

                        double lambdaMax = Math.Max(_lambday, _lambdaz);
                        NewFormula(@"\lambda_{max} = max(\lambda_y , \lambda_z) = " + lambdaMax.ToString(_formatDouble));

                        NewParagraph(@"Calculation of non dimensional slenderness for lateral-torsional buckling due to uniform bending moment Ψ = 1:");
                        if (_classificationSection < 4)
                        {
                            _Mcr0LateralTorsional = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _sec.J11, E, G, _supportConditiony, LoadCondition.NotDirectlyLoaded, 1.0, 1.0, 1.0, out double fakec1, out double fakec2, out double fakec3,out double fakezg, out double fakezj);
                            NewFormula(@"M_{cr,LT,0} = " + (_Mcr0LateralTorsional / 1e6).ToString(_formatDouble) + " kNm");
                        } else
                        {
                            _Mcr0LateralTorsional = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _J1eff, E, G, _supportConditiony, LoadCondition.NotDirectlyLoaded, 1.0, 1.0, 1.0, out double fakec1, out double fakec2, out double fakec3, out double fakezg, out double fakezj); //Jw eff?
                            NewFormula(@"M_{cr,LT,0} = " + (_Mcr0LateralTorsional / 1e6).ToString(_formatDouble) + " kNm");
                        }
                        
                        if (_classificationSection < 3)
                        {
                            _lambda0 = GetLambdaSegn(_sec.Wpl22, fy, _Mcr0LateralTorsional);
                            NewFormula(@"\lambda_0 = \sqrt{ W_{pl,y} \cdot f_y / M_{cr,LT,0} } = " + _lambda0.ToString(_formatDouble) + "");
                        } else if (_classificationSection == 3)
                        {
                            _lambda0 = GetLambdaSegn(_sec.Wel22Min, fy, _Mcr0LateralTorsional);
                            NewFormula(@"\lambda_0 = \sqrt{ W_{el,y} \cdot f_y / M_{cr,LT,0} } = " + _lambda0.ToString(_formatDouble) + "");
                        } else
                        {
                            _lambda0 = GetLambdaSegn(_Weffy, fy, _Mcr0LateralTorsional); //or Wel?
                            NewFormula(@"\lambda_0 = \sqrt{ W_{eff,y} \cdot f_y / M_{cr,LT,0} } = " + _lambda0.ToString(_formatDouble) + "");
                        } 

                        if (_classificationSection < 4)
                        {
                            _epsilony = AbsMyEd / Math.Max(NEd,1E-3) * _sec.Area / _sec.Wel22Min;
                            NewFormula(@"\epsilon_y = M_{y,Ed} / N_{Ed} \cdot A / W_{el,y} = " + _epsilony.ToString(_formatDouble) + "");
                        } else
                        {
                            _epsilony = AbsMyEd / Math.Max(NEd, 1E-3) * _Aeff / _Weffy;
                            NewFormula(@"\epsilon_y = M_{y,Ed} / N_{Ed} \cdot A_{eff} / W_{el,y} =  " + _epsilony.ToString(_formatDouble) + "");
                        }
         
                        _aLT = Math.Max(1.0 - _sec.Jt / _sec.J22,0);
                        NewFormula(@"a_{LT} = max(1 - J_t / J_y , 0) = " + _aLT.ToString(_formatDouble) + "");

                        double C1 = Math.Pow(kc, -2.0);
                        NewFormula(@"C1 = k_{c}^{-2} = " + C1.ToString(_formatDouble) + "");
                        double lambda0Limit = 0.2 * Math.Pow(C1, 0.5) * Math.Pow((1.0 - NEd / _Ncrz) * (1.0 - NEd / _NcrFlexuralTorsional), 0.25);
                        NewFormula(@"\lambda_{0,lim} = 0.2 \cdot \sqrt{C1} \cdot ((1-N_{Ed}/N_{cr,z}) \cdot (1-N_{Ed}/N_{cr,TF}))^{1/4} = " + lambda0Limit.ToString(_formatDouble) + "");

                        if (_lambda0 <= lambda0Limit)
                        {
                            _cmy = _cmy0;
                            NewFormula(@"c_{my} = c_{my0} = " + _cmy.ToString(_formatDouble) + "");
                            _cmz = _cmz0;
                            NewFormula(@"c_{mz} = c_{mz0} = " + _cmz.ToString(_formatDouble) + "");
                            _cmLT = 1.0;
                            NewFormula(@"c_{mLT} = " + _cmLT.ToString(_formatDouble) + "");
                        } else
                        {
                            _cmy = _cmy0 + (1.0 - _cmy0) * Math.Sqrt(_epsilony) * _aLT / (1.0 + Math.Sqrt(_epsilony) * _aLT);
                            NewFormula(@"c_{my} = c_{my0} + (1 - c_{my0} ) \cdot \frac{ \sqrt{ \varepsilon_y \cdot a_{LT} } }{ 1 + \sqrt{ \varepsilon_y } \cdot a_{LT} } = " + _cmy.ToString(_formatDouble) + "");
                            _cmz = _cmz0;
                            NewFormula(@"c_{mz} = c_{mz0} = " + _cmz.ToString(_formatDouble) + "");
                            _cmLT = Math.Max(_cmy*_cmy * _aLT / Math.Sqrt((1.0-NEd/_Ncrz) * (1.0 - NEd/_NcrTorsional)),1.0);
                            NewFormula(@"c_{mLT} = " + _cmLT.ToString(_formatDouble) + "");
                            /*if (cmLT < 1)
                            {
                                throw new Exception("cmLT < 1");
                            }*/
                        }

                        double mplyRd;
                        double mplzRd;
                        if (_classificationSection < 3)
                        {
                            mplyRd = _sec.Wpl22 * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,y} = W_{pl,y} \cdot f_{y} / \gamma_{m0} = " + (mplyRd / 1e6).ToString(_formatDouble) + " kNm");
                            mplzRd = _sec.Wpl11 * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,z} = W_{pl,z} \cdot f_{y} / \gamma_{m0} =  " + (mplzRd / 1e6).ToString(_formatDouble) + " kNm");
                        } else if (_classificationSection == 3)
                        {
                            mplyRd = _sec.Wel22Min * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,y} =  W_{el,y} \cdot f_{y} / \gamma_{m0} = " + (mplyRd / 1e6).ToString(_formatDouble) + " kNm");
                            mplzRd = _sec.Wel11Min * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,z} =  W_{el,z} \cdot f_{y} / \gamma_{m0} = " + (mplzRd / 1e6).ToString(_formatDouble) + " kNm");
                        } else
                        {
                            mplyRd = _Weffy * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,y} = W_{eff,y} \cdot f_{y} / \gamma_{m0} = " + (mplyRd / 1e6).ToString(_formatDouble) + " kNm");
                            mplzRd = _Weffz * fy / _annex.Gm0;
                            NewFormula(@"M_{pl,Rd,z} =  W_{eff,z} \cdot f_{y} / \gamma_{m0} = " + (mplzRd / 1e6).ToString(_formatDouble) + " kNm");
                        }                        

                        _bLT = 0.5 * _aLT * _lambda0 * _lambda0 * AbsMyEd * AbsMzEd / (_ChiLT * mplyRd * mplzRd);
                        NewFormula(@"b_{LT} = 0.5 \cdot a_{LT} \lambda_0^2 \frac{ M_{y,Ed} \cdot M_{z,Ed} }{ \chi_{LT} \cdot M_{pl,y,Rd} \cdot M_{pl,z,Rd} } = " + (_bLT).ToString(_formatDouble) + "");
                        _cLT = 10.0 * _aLT * _lambda0 * _lambda0 * AbsMyEd / ((5.0 + Math.Pow(_lambdaz,4.0)) * _cmy * _ChiLT * mplyRd);
                        NewFormula(@"c_{LT} = 10 \cdot a_{LT} \cdot \frac{ \lambda_0^2 }{ 5 + \lambda_z^4 } \cdot \frac{ M_{y,Ed} }{ c_{my} \chi_{LT} \cdot M_{pl,y,Rd} } = " + (_cLT).ToString(_formatDouble) + "");
                        _dLT = 2.0 * _aLT * _lambda0 * AbsMyEd * AbsMzEd / ((0.1 + Math.Pow(_lambdaz,4.0)) * _cmy * _ChiLT * mplyRd * _cmz * mplzRd);
                        NewFormula(@"d_{LT} = 2 \cdot a_{LT} \cdot \frac{ \lambda_0 }{ 0.1 + \lambda_z^4 } \cdot \frac{ M_{y,Ed} }{ c_{my} \cdot \chi_{LT} \cdot M_{pl,y,Rd} } \cdot \frac{ M_{z,Ed} }{ c_{mz} \cdot M_{pl,z,Rd} } = " + (_dLT).ToString(_formatDouble) + "");
                        _eLT = 1.7 * _aLT * _lambda0 * AbsMyEd / ((0.1 + Math.Pow(_lambdaz, 4.0)) * _cmy * _ChiLT * mplyRd);
                        NewFormula(@"e_{LT} = 1.7 a_{LT} \cdot \frac{ \lambda_0 }{0.1 + \lambda_z^4} \cdot \frac{ M_{y,Ed} }{ c_{my} \cdot \chi_{LT} \cdot M_{pl,y,Rd} } = " + (_eLT).ToString(_formatDouble) + "");

                        double npl = NEd / (fy * _sec.Area / _annex.Gm0);
                        NewFormula(@"n_{pl} = N_{Ed} / (A \cdot f_y / \gamma_{m0} ) = " + npl.ToString(_formatDouble));
                        _cyy = Math.Max(1.0 + (_wy - 1.0) * ((2.0 - 1.6/_wy * _cmy * _cmy * lambdaMax - 1.6 / _wy * _cmy * _cmy * lambdaMax * lambdaMax) * npl - _bLT), _sec.Wel22Min / _sec.Wpl22);
                        NewFormula(@"c_{yy} = max(1 + (w_y - 1) \cdot [ (2 - \frac{1.6}{w_y} c_{my}^2 \lambda_{max} - \frac{1.6}{w_y} c_{my}^2 \lambda_max^2) n_{pl} - b_{LT} ] , \frac{ W_{el,y} }{ W_{pl,y} } ) = " + (_cyy).ToString(_formatDouble) + "");
                        _cyz = Math.Max(1.0 + (_wz - 1.0) * ((2.0 - 14.0 * _cmz * _cmz * lambdaMax * lambdaMax / Math.Pow(_wz,5.0)) * npl - _cLT), 0.6 * Math.Sqrt(_wz / _wy) * _sec.Wel11Min / _sec.Wpl11);
                        NewFormula(@"c_{yz} = max(1 + (w_z - 1) [ ( 2-14 \frac{ c_{mz}^2 \lambda_{max} }{ w_z^5 } ) n_{pl} - c_{LT} ] , 0.6 \cdot \sqrt{ \frac{ w_z }{ w_y } } \cdot \frac{ W_{el,z} }{ W_{pl,z} } ) = " + (_cyz).ToString(_formatDouble) + "");
                        _czy = Math.Max(1.0 + (_wy - 1.0) * ((2.0 - 14.0 * _cmy * _cmy * lambdaMax * lambdaMax / Math.Pow(_wy, 5.0)) * npl - _dLT),0.6 * Math.Sqrt(_wy / _wz) * _sec.Wel22Min / _sec.Wpl22);
                        NewFormula(@"c_{zy} = max(1 + (w_y - 1) [ ( 2-14 \frac{ c_{my}^2 \lambda_{max} }{ w_y^5 } ) n_{pl} - d_{LT} ] , 0.6 \cdot \sqrt{ \frac{ w_y }{ w_z } } \cdot \frac{ W_{el,y} }{ W_{pl,y} } ) = " + (_czy).ToString(_formatDouble) + "");
                        _czz = Math.Max(1.0 + (_wz - 1) * (2.0 - 1.6 / _wz * _cmz * _cmz * lambdaMax - 1.6 / _wz * _cmz * _cmz * lambdaMax * lambdaMax - _eLT) * npl, _sec.Wel11Min / _sec.Wpl11); //RIGHT VERSION
                        NewFormula(@"c_{zz} = max(1 + (w_z - 1) \cdot [ 2 - \frac{1.6}{w_z} c_{mz}^2 \lambda_{max} - \frac{1.6}{w_z} c_{mz}^2 \lambda_max^2 - e_{LT} ] n_{pl} , \frac{ W_{el,z} }{ W_{pl,z} } ) = " + (_czz).ToString(_formatDouble) + "");
                        /* WRONG - TO BE COMMENTED!! -  ONLY FOR COMPARISON WITH SAP */
                        //_czz = Math.Max(1.0 + (_wz - 1) * ((2.0 - 1.6 / _wz * _cmz * _cmz * lambdaMax - 1.6 / _wz * _cmz * _cmz * lambdaMax * lambdaMax) * npl - _eLT), _sec.Wel11Min / _sec.Wpl11); //SAP200 WRONG OLD VERSION
                        /* STOP WRONG */
                        if (_classificationSection <= 2)
                        {
                            _kyy = _cmy * _cmLT * _muy / (1.0 - NEd / _Ncry) * 1.0 / _cyy;
                            NewFormula(@"k_{yy} = c_{my} \cdot c_{mLT} \cdot \frac{ \mu_y }{ 1-N_{Ed} / N_{cr,y} } \cdot \frac{ 1 }{ c_{yy} } = " + (_kyy).ToString(_formatDouble) + "");
                            _kyz = _cmz * _muy/(1.0 - NEd/_Ncrz) * 1.0 / _cyz * 0.6 * Math.Sqrt(_wz/_wy);
                            NewFormula(@"k_{yz} = c_{mz} \cdot \frac{ \mu_y }{ 1 - N_{Ed} / N_{cr,z} } \cdot \frac{ 1 }{ c_{yz} } \cdot 0.6 \sqrt{ \frac{ w_z }{ w_y } } = " + (_kyz).ToString(_formatDouble) + "");
                            _kzy = _cmy * _cmLT * _muz/(1.0 - NEd/_Ncry) * 1.0 / _czy * 0.6 * Math.Sqrt(_wy/_wz);
                            NewFormula(@"k_{zy} = c_{my} \cdot c_{mLT} \frac{ \mu_z }{ 1 - N_{Ed} / N_{cr,y} } \cdot \frac{ 1 }{ c_{zy} } \cdot 0.6 \sqrt{ \frac{ w_y }{ w_z } } = " + (_kzy).ToString(_formatDouble) + "");
                            _kzz = _cmz * _muz / (1.0 - NEd/_Ncrz) * 1.0 / _czz;
                            NewFormula(@"k_{zz} = c_{mz} \cdot \frac{ \mu_z }{ 1-N_{Ed} / N_{cr,z} } \cdot \frac{ 1 }{ c_{zz} } = " + (_kzz).ToString(_formatDouble) + "");
                        } else
                        {
                            _kyy = _cmy * _cmLT * _muy / (1.0 - NEd / _Ncry);
                            NewFormula(@"k_{yy} = c_{my} \cdot c_{mLT} \cdot \frac{ \mu_y }{ 1-N_{Ed} / N_{cr,y} } = " + (_kyy).ToString(_formatDouble) + "");
                            _kyz = _cmz * _muy / (1.0 - NEd / _Ncrz);
                            NewFormula(@"k_{yz} = c_{mz} \cdot \frac{ \mu_y }{ 1 - N_{Ed} / N_{cr,z} } = " + (_kyz).ToString(_formatDouble) + "");
                            _kzy = _cmy * _cmLT * _muz / (1.0 - NEd/_Ncry);
                            NewFormula(@"k_{zy} = c_{my} \cdot c_{mLT} \frac{ \mu_z }{ 1 - N_{Ed} / N_{cr,y} } = " + (_kzy).ToString(_formatDouble) + "");
                            _kzz = _cmz * _muz / (1.0 - NEd / _Ncrz);
                            NewFormula(@"k_zz} = c_{mz} \cdot \frac{ \mu_z }{ 1-N_{Ed} / N_{cr,z} } = " + (_kzz).ToString(_formatDouble) + "");
                        }
                        
                    } else
                    {
                        //Annex B
                        throw new Exception("Annex B not implemented yet");
                    }

                    double deltaMy;
                    double deltaMz;
                    double nrk;
                    double myrk;
                    double mzrk;
                    if (_classificationSection < 3)
                    {
                        nrk = _sec.Area * fy;
                        NewFormula(@"N_{Rk} = A \cdot f_y = " + (nrk / 1e3).ToString(_formatDouble) + " kN");
                        myrk = _sec.Wpl22 * fy;
                        NewFormula(@"M_{Rk,y} = W_{pl,y} \cdot f_y = " + (myrk / 1e6).ToString(_formatDouble) + " kNm");
                        mzrk = _sec.Wpl11 * fy;
                        NewFormula(@"M_{Rk,z} = W_{pl,z} \cdot f_y = " + (mzrk / 1e6).ToString(_formatDouble) + " kNm");
                        deltaMy = 0;
                        deltaMz = 0;
                    } else if (_classificationSection == 3)
                    {
                        nrk = _sec.Area * fy;
                        NewFormula(@"N_{Rk} = A \cdot f_y = " + (nrk / 1e3).ToString(_formatDouble) + " kN");
                        myrk = _sec.Wel22Min * fy;
                        NewFormula(@"M_{Rk,y} = W_{el,y} \cdot f_y = " + (myrk / 1e6).ToString(_formatDouble) + " kNm");
                        mzrk = _sec.Wel11Min * fy;
                        NewFormula(@"M_{Rk,z} = W_{el,y} \cdot f_y = " + (mzrk / 1e6).ToString(_formatDouble) + " kNm");
                        deltaMy = 0;
                        deltaMz = 0;
                    } else
                    {
                        nrk = _Aeff * fy;
                        NewFormula(@"N_{Rk} = A_{eff} \cdot f_y = " + (nrk / 1e3).ToString(_formatDouble) + " kN");
                        myrk = _Weffy * fy;
                        NewFormula(@"M_{Rk,y} = W_{pl,y} \cdot f_y = " + (myrk / 1e6).ToString(_formatDouble) + " kNm");
                        mzrk = _Weffz * fy;
                        NewFormula(@"M_{Rk,z} = W_{pl,z} \cdot f_y = " + (mzrk / 1e6).ToString(_formatDouble) + " kNm");
                        deltaMy = NEd * _deltaG.Y; //check segno
                        NewFormula(@"\Delta M_{y} = N_{Ed} \cdot \Delta_y = " + (deltaMy / 1e6).ToString(_formatDouble) + " kNm");
                        deltaMz = NEd * _deltaG.X; //check segno
                        NewFormula(@"\Delta M_{z} = N_{Ed} \cdot \Delta_z = " + (deltaMz / 1e6).ToString(_formatDouble) + " kNm");
                    }
                    WRBuckling1 = NEd / (_Chiy * nrk / _annex.Gm1) + _kyy * Math.Abs(AbsMyEd + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kyz * Math.Abs(AbsMzEd + deltaMz) / (mzrk / _annex.Gm1);
                    NewFormula(@"w.r._{1} = \frac{N_{Ed}}{\chi_y \cdot N_{Rk} / \gamma_{m1}} + k_{yy} \cdot \frac{M_{y,Ed} + \Delta M_{y,Ed}}{\chi_{LT} \cdot M_{y,Rk} / \gamma_{m1} } + k_{zy} \cdot \frac{Mz_{Ed} + \Delta M_{z,Ed}}{ M_{z,Rk} / \gamma_{m1}} = " + (WRBuckling1).ToString(_formatDouble) + "");
                    WRBuckling2 = NEd / (_Chiz * nrk / _annex.Gm1) + _kzy * Math.Abs(AbsMyEd + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kzz * Math.Abs(AbsMzEd + deltaMz) / (mzrk / _annex.Gm1);
                    NewFormula(@"w.r._{2} = \frac{N_{Ed}}{\chi_z \cdot N_{Rk} / \gamma_{m1}} + k_{zy} \cdot \frac{M_{y,Ed} + \Delta M_{y,Ed}}{\chi_{LT} \cdot M_{y,Rk} / \gamma_{m1} } + k_{zz} \cdot \frac{Mz_{Ed} + \Delta M_{z,Ed}}{ M_{z,Rk} / \gamma_{m1}} = " + (WRBuckling1).ToString(_formatDouble) + "");
                    WRBuckling3 = NEd / _NbRdT;
                    NewFormula(@"w.r._{3} = N_{Ed}/N_{b,Rd,T} = " + (WRBuckling3).ToString(_formatDouble) + "");

                    WRMax = Math.Max(WRBuckling1, WRMax);
                    WRMax = Math.Max(WRBuckling2, WRMax);
                    WRMax = Math.Max(WRBuckling3, WRMax);

                } else
                {
                    //no instability check needed :
                    WRBuckling1 = 0.0;
                    WRBuckling2 = 0.0;
                }
            }
            #endregion
        }
        #endregion

        #region Classification
        protected int GetClassCompressedInnerPlate(double ctRatio, double epsilon)
        {
            if (ctRatio <= 33.0 * epsilon)
            {
                NewFormula(@"c/t \leq 33 \cdot \varepsilon \Rightarrow Class 1");
                return 1;
            }
            else if (ctRatio <= 38.0 * epsilon)
            {
                NewFormula(@"c/t \leq 38 \cdot \varepsilon \Rightarrow Class 2");
                return 2;
            }
            else if (ctRatio <= 42.0 * epsilon)
            {
                NewFormula(@"c/t \leq 42 \cdot \varepsilon \Rightarrow Class 3");
                return 3;
            }
            else
            {
                NewFormula(@"c/t \geq 42 \cdot \varepsilon \Rightarrow Class 1");
                return 4;
            }
        }

        protected int GetClassInnerPlate(double ctRatio, double epsilon, double alpha, double psi)
        {
            NewFormula(@"\alpha = " + alpha.ToString(_formatDouble));
            NewFormula(@"\psi = " + psi.ToString(_formatDouble));

            if (alpha > 0.5 && alpha < 1)
            {
                if (ctRatio <= 396.0 * epsilon / (13.0 * alpha - 1.0))
                {
                    NewFormula(@"c/t \leq 396 \cdot \varepsilon / (13 \cdot \alpha - 1) \Rightarrow Class 1");
                    return 1;
                }
                else if (ctRatio <= 456.0 * epsilon / (13.0 * alpha - 1.0))
                {
                    NewFormula(@"c/t \leq 456 \cdot \varepsilon / (13 \cdot \alpha - 1) \Rightarrow Class 2");
                    return 2;
                } else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                        {
                            NewFormula(@"c/t \leq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 3");
                            return 3;
                        }
                        else
                        {
                            NewFormula(@"c/t \geq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 4");
                            return 4;
                        }
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                        {
                            NewFormula(@"c/t \leq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 3");
                            return 3;
                        }
                        else
                        {
                            NewFormula(@"c/t \geq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 4");
                            return 4;
                        }
                    }
                    else
                    {
                        NewFormula(@"Class 4");
                        return 4;
                        throw new Exception("classification");
                    }
                }
            }
            else if (alpha <= 0.5 && alpha > 0)
            {
                if (ctRatio <= 36.0 * epsilon / alpha)
                {
                    NewFormula(@"c/t \leq 36 \cdot \varepsilon / \alpha \Rightarrow Class 1");
                    return 1;
                }
                else if (ctRatio <= 41.5 * epsilon / alpha)
                {
                    NewFormula(@"c/t \leq 41.5 \cdot \varepsilon / \alpha \Rightarrow Class 2");
                    return 2;
                } else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                        {
                            NewFormula(@"c/t \leq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 3");
                            return 3;
                        }
                        else
                        {
                            NewFormula(@"c/t \geq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 4");
                            return 4;
                        }
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                        {
                            NewFormula(@"c/t \leq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 3");
                            return 3;
                        }
                        else
                        {
                            NewFormula(@"c/t \geq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 4");
                            return 4;
                        }
                    }
                    else
                    {
                        NewFormula(@"Class 4");
                        return 4;
                        throw new Exception("classification");
                    }
                }
            } else if (alpha > 1)
            {
                if (psi > -1)
                {
                    if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                    {
                        NewFormula(@"c/t \leq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 3");
                        return 3;
                    }
                    else
                    {
                        NewFormula(@"c/t \geq 42 \cdot \varepsilon / (0.67 + 0.33 \cdot \psi) \Rightarrow Class 4");
                        return 4;
                    }
                }
                else if (psi <= -1)
                {
                    if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                    {
                        NewFormula(@"c/t \leq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 3");
                        return 3;
                    }
                    else
                    {
                        NewFormula(@"c/t \geq 62 \cdot \varepsilon (1 - \psi) \cdot \sqrt{-\psi} \Rightarrow Class 4");
                        return 4;
                    }
                }
                else
                {
                    NewFormula(@"Class 4");
                    return 4;
                    throw new Exception("classification");
                }
            }
            else
            {
                return 4; 
                throw new Exception("Problems classification");
            }
        }

        protected int GetClassCompressedOuterPlate(double ctRatio, double epsilon)
        {
            if (ctRatio <= 9.0 * epsilon)
            {
                NewFormula(@"c/t \leq 9 \cdot \varepsilon \Rightarrow Class 1");
                return 1;
            }
            else if (ctRatio <= 10.0 * epsilon)
            {
                NewFormula(@"c/t \leq 10 \cdot \varepsilon \Rightarrow Class 2");
                return 2;
            }
            else if (ctRatio <= 14.0 * epsilon)
            {
                NewFormula(@"c/t \leq 14 \cdot \varepsilon \Rightarrow Class 3");
                return 3;
            }
            else
            {
                NewFormula(@"c/t \geq 14 \cdot \varepsilon \Rightarrow Class 4");
                return 4;
            }
        }
        #endregion

        #region ResistanceFunctions
        protected double GetNtRd(double Anet)
        {
            NewParagraph("Tension Resistace");
            double A = _sec.Area;
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double fu = ((SteelMaterial)_sec.Material).Fu;
            NewFormula(@"f_{u} = " + fu.ToString(_formatDouble) + " MPa");
            double gm0 = _annex.Gm0;
            double gm2 = _annex.Gm2;

            NewFormula(@"A_{net} = " + Anet.ToString(_formatDouble) + " mm^2");

            double NtRd = Math.Min(A * fy / gm0, 0.9 * Anet * fu / gm2);
            NewFormula(@"N_{t,Rd} = min(A \cdot f_y / \gamma_{m0}; A_{net} \cdot f_u / \gamma_{m2}) = " + (NtRd / 1000.0).ToString(_formatDouble) + " kN");
            return NtRd;
        }
        protected double GetNcRd()
        {
            NewParagraph("Compression Resistance");
            double A;
            if (_classificationSection == 4)
            {
                A = _Aeff;
                NewFormula(@"A = A_{eff} =" + _Aeff.ToString(_formatDouble) + " kN");
            }
            else
            {
                A = _sec.Area;
            }
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double gm0 = _annex.Gm0;

            double NcRd = A * fy / gm0;
            NewFormula(@"N_{c,Rd} = A \cdot f_y / \gamma_{m0} = " + (NcRd / 1000.0).ToString(_formatDouble) + " kN");
            return NcRd;
        }

        protected void GetVRdTRd(double V1Ed, double V2Ed, out double VyEd, out double VzEd, out double VRdy, out double VRdz, out double TRd, out double VplRdz, out double VplRdy)
        {
            NewParagraph("Shear Resistance");

            VzEd = Double.MaxValue;
            VyEd = Double.MaxValue;

            double eta = 1.0;
            NewFormula(@"\eta = " + (eta).ToString(_formatDouble) + "");

            double Avy, Avz;
            //z = vertical axis
            //y = horizz axis
            Type typeShape = _sec.GetType();
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double gm0 = _annex.Gm0;

            #region Av
            /*if (typeShape == typeof(SectionRectangular))
            {
                Avy = _sec.Area;
                Avz = Avy;
            } else*/ if (typeShape == typeof(SectionH)) {
                VzEd = V2Ed;
                VyEd = V1Ed;
                SectionH sec = (SectionH)_sec;
                Avz = Math.Min(_sec.Area - sec.LenghtBottomFlange * sec.ThicknessBottomFlange - sec.LenghtTopFlange * sec.ThicknessTopFlange, eta * sec.ThicknessWeb * sec.HeightWeb);
                NewFormula(@"A_{v,z} = " + (Avz).ToString(_formatDouble) + " mm^2");

                Avy = sec.Area - sec.ThicknessWeb * sec.HeightWeb;
                NewFormula(@"A_{v,y} = " + (Avy).ToString(_formatDouble) + " mm^2");

                double epsilon = Math.Sqrt(235.0 / ((SteelMaterial) sec.Material).Fyk);
                if (sec.HeightWeb / sec.ThicknessWeb > 72.0 * epsilon / 1.0)
                {
                    NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                    NewParagraph("Check shear buckling web!");
                    throw new Exception("Check shear buckling web!");
                }
            } else if (typeShape == typeof(SectionCHS))
            {
                VzEd = V2Ed;
                VyEd = V1Ed;
                Avz = 2.0 * _sec.Area / Math.PI;
                NewFormula(@"A_{v,y} = " + (Avz).ToString(_formatDouble) + " mm^2");
                Avy = Avz;
                NewFormula(@"A_{v,y} = " + (Avy).ToString(_formatDouble) + " mm^2");
            } else if (typeShape == typeof(SectionC))
            {
                SectionC sec = (SectionC)_sec;
                if (sec.LBottom == sec.LTop && sec.ThicknessBottom == sec.ThicknessTop)
                {
                    VzEd = V2Ed;
                    VyEd = V1Ed;
                    Avz = sec.Area - sec.ThicknessTop * sec.LTop - sec.ThicknessBottom * sec.LBottom;
                    NewFormula(@"A_{v,z} = " + (Avz).ToString(_formatDouble) + " mm^2");

                    Avy = sec.Area - sec.Hw * sec.Tw;
                    NewFormula(@"A_{v,y} = A - h_w * _t_w = " + (Avy).ToString(_formatDouble) + " mm^2");

                    double epsilon = Math.Sqrt(235.0 / ((SteelMaterial)sec.Material).Fyk);
                    if (sec.Hw / sec.Tw > 72.0 * epsilon / 1.0)
                    {
                        NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                        NewParagraph("Check shear buckling web!");
                        throw new Exception("Check shear buckling web!");
                    }
                } else
                {
                    throw new Exception("gestione angolo C not yet supported");
                }
            } else if (typeShape == typeof(SectionT))
            {
                VzEd = V2Ed;
                VyEd = V1Ed;
                SectionT sec = (SectionT)_sec;
                Avz = sec.Tw * (sec.H - sec.Tf / 2.0);
                NewFormula(@"A_{v,z} = " + (Avz).ToString(_formatDouble) + " mm^2");
                Avy = sec.Tf * sec.H;
                NewFormula(@"A_{v,y} = " + (Avy).ToString(_formatDouble) + " mm^2");

                eta = 1.0;
                double epsilon = Math.Sqrt(235.0 / ((SteelMaterial)sec.Material).Fyk);
                if (sec.Hw / sec.Tw > 72.0 * epsilon / eta)
                {
                    NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                    NewParagraph("Check shear buckling web!");
                    throw new Exception("Check shear buckling web!");
                }
            } else if (typeShape == typeof(SectionRHS))
            {
                SectionRHS sec = (SectionRHS)_sec;
                if (sec.IsSymmetricAlongYLocalAxis && sec.IsSymmetricAlongZLocalAxis)
                {
                    VzEd = V2Ed;
                    VyEd = V1Ed;

                    Avz = eta * sec.H * sec.ThicknessWeb * 2.0;
                    NewFormula(@"A_{v,z} = " + (Avz).ToString(_formatDouble) + " mm^2");

                    Avy = sec.Area - 2.0 * sec.Hw * sec.ThicknessWeb;
                    NewFormula(@"A_{v,y} = " + (Avy).ToString(_formatDouble) + " mm^2");

                    double epsilon = Math.Sqrt(235.0 / ((SteelMaterial)sec.Material).Fyk);
                    if (sec.Hw / sec.ThicknessWeb > 72.0 * epsilon / 1.0)
                    {
                        NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                        NewParagraph("Check shear buckling web!");
                        throw new Exception("Check shear buckling web!");
                    }
                    if (sec.Bint / sec.ThicknessFlange > 72.0 * epsilon / 1.0)
                    {
                        NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                        NewParagraph("Check shear buckling flanges!");
                        throw new Exception("Check shear buckling web!");
                    }
                } else
                {
                    throw new Exception("Section not supported yet");
                }
            }
            else if (typeShape == typeof(SectionL))
            {
                //Check along local axis not principal!
                SectionL sec = (SectionL)_sec;
                double angle = sec.AngleX1;
                Avz = sec.LVert * sec.TVert;
                NewFormula(@"A_{v,z} = " + (Avz).ToString(_formatDouble) + " mm^2");
                Avy = sec.LHor*sec.THor;
                NewFormula(@"A_{v,y} = " + (Avy).ToString(_formatDouble) + " mm^2");

                double angle2 = Math.PI / 2.0 - sec.AngleX1;

                VyEd = _V1Ed * Math.Cos(sec.AngleX1) - _V2Ed * Math.Cos(angle2);
                NewFormula(@"V_{y,Ed} = " + (VyEd / 1000.0).ToString(_formatDouble) + " kN");
                VzEd = _V1Ed * Math.Sin(sec.AngleX1) + _V2Ed * Math.Sin(angle2);
                NewFormula(@"V_{z,y} = " + (VzEd / 1000.0).ToString(_formatDouble) + " kN");

                double epsilon = Math.Sqrt(235.0 / ((SteelMaterial)sec.Material).Fyk);
                if (sec.LVert / sec.TVert > 72.0 * epsilon / 1.0)
                {
                    NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                    NewParagraph("Check shear buckling web!");
                    throw new Exception("Check shear buckling web!");
                }
                if (sec.LHor / sec.THor > 72.0 * epsilon / 1.0)
                {
                    NewFormula(@"h_w / t_w > 72 \cdot \varepsilon !");
                    NewParagraph("Check shear buckling flanges!");
                    throw new Exception("Check shear buckling web!");
                }
            }
            else
            {
                Avz = 0;
                Avy = 0;
                throw new Exception("Section not supported yet");
            }
             #endregion

            double VplRdTy = 0.0;
            double VplRdTz = 0.0;

            VplRdy = Avy * fy / gm0 / Math.Pow(3.0, 0.5);
            NewFormula(@"V_{pl,Rd,y} = A_{vy} \cdot f_y / \gamma_{m0} / 3^{0.5} = " + (VplRdy / 1000.0).ToString(_formatDouble) + " kN");
            VplRdz = Avz * fy / gm0 / Math.Pow(3.0, 0.5);
            NewFormula(@"V_{pl,Rd,z} =  A_{vz} \cdot f_y / \gamma_{m0} / 3^{0.5} = " + (VplRdz / 1000.0).ToString(_formatDouble) + " kN");

            #region calculationTauTandTauW
            if (_classificationSection == 4 && Math.Abs(_TEd) > 0)
            {
                NewParagraph("Class 4 with torsion not supported by Eurocode.");
                throw new Exception("Class 4 with torsion not supported by Eurocode.");
            }
            double tau_w = 0;
            double tauT;

            if (typeShape == typeof(SectionCHS))
            {
                //Bredt - Plastic Theory
                /*
                SectionCHS sec = (SectionCHS)_sec;
                double Dmed = sec.Dext - sec.T / 2.0;
                double Omega = Math.PI * Math.Pow(Dmed, 2.0) / 4.0;
                double denom = 2.0 * Omega * sec.T;
                tauT = Math.Abs(_TEd) / denom;*/

                //Elastic Theory
                SectionCHS sec = (SectionCHS)_sec;
                double Wt = _sec.Jt / (sec.D / 2.0);
                tauT = Math.Abs(_TEd) / Wt;
                NewFormula(@"\tau(T_{Ed}) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * Wt;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");
            } else if (typeShape == typeof(SectionRHS))
            {
                //Bredt - Plastic Theory
                SectionRHS sec = (SectionRHS)_sec;
                double Hmed = sec.H - sec.ThicknessFlange;
                double Bmed = sec.B - sec.ThicknessWeb;
                double Omega = Hmed * Bmed;
                double denom = 2.0 * Omega * Math.Min(sec.ThicknessWeb, sec.ThicknessFlange);
                tauT = Math.Abs(_TEd) / denom;
                NewFormula(@"\tau(T_{Ed}) = T_{Ed} / (2 \cdot \Omega \cdot t) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * denom;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");

            } else if (typeShape == typeof(SectionC))
            {
                SectionC sec = (SectionC)_sec;
                double tmax = Math.Max(sec.Tw, sec.ThicknessBottom);
                tmax = Math.Max(tmax, sec.ThicknessTop);

                double L1 = sec.LBottom;
                double a1 = sec.ThicknessBottom;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.LTop;
                double a2 = sec.ThicknessTop;
                denominator = denominator + L2 * Math.Pow(a2, 3.0);
                double L3 = sec.Hw;
                double a3 = sec.Tw;
                denominator = denominator + L3 * Math.Pow(a3, 3.0);
                denominator = denominator / 3.0;

                tauT = Math.Abs(_TEd) * tmax / denominator;
                NewFormula(@"\tau(T_{Ed}) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * denominator / tmax;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");

            } else if (typeShape == typeof(SectionH))
            {
                SectionH sec = (SectionH)_sec;
                double tmax = Math.Max(sec.ThicknessWeb, sec.ThicknessTopFlange);
                tmax = Math.Max(tmax, sec.ThicknessBottomFlange);

                double L1 = sec.LenghtBottomFlange;
                double a1 = sec.ThicknessBottomFlange;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.LenghtTopFlange;
                double a2 = sec.ThicknessTopFlange;
                denominator = denominator + L2 * Math.Pow(a2, 3.0);
                double L3 = sec.HeightWeb;
                double a3 = sec.ThicknessWeb;
                denominator = denominator + L3 * Math.Pow(a3, 3.0);
                denominator = denominator / 3.0;

                tauT = Math.Abs(_TEd) * tmax / denominator;
                NewFormula(@"\tau(T_{Ed}) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * denominator / tmax;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");
            } else if (typeShape == typeof(SectionL))
            {
                SectionL sec = (SectionL)_sec;
                double tmax = Math.Max(sec.TVert, sec.THor);

                double L1 = sec.LHor;
                double a1 = sec.THor;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.LVert;
                double a2 = sec.TVert;
                denominator = denominator + L2 * Math.Pow(a2, 3.0);
                denominator = denominator / 3.0;

                tauT = Math.Abs(_TEd) * tmax / denominator;
                NewFormula(@"\tau(T_{Ed}) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * denominator / tmax;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");
            }/* else if (typeShape == typeof(SectionRectangular))
            {
                SectionRectangular sec = (SectionRectangular)_sec;
                double a = Math.Min(sec.B, sec.H);
                double b = Math.Max(sec.B, sec.H);
                double alpha = 3.0 + 1.8 * a / b;
                tauT = alpha * Math.Abs(_TEd) / (b * Math.Pow(a, 2.0));

                TRd = b * Math.Pow(a, 2.0) / alpha * fy / (Math.Pow(3.0, 0.5));
            }*/
            else if (typeShape == typeof(SectionT))
            {
                SectionT sec = (SectionT)_sec;
                double tmax = Math.Max(sec.Tf, sec.Tw);

                double L1 = sec.B;
                double a1 = sec.Tf;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.Hw;
                double a2 = sec.Tw;
                denominator = denominator + L2 * Math.Pow(a2, 3.0);
                denominator = denominator / 3.0;

                tauT = Math.Abs(_TEd) * tmax / denominator;
                NewFormula(@"\tau(T_{Ed}) = " + (tauT).ToString(_formatDouble) + " MPa");

                TRd = fy / Math.Pow(3.0, 0.5) * denominator / tmax;
                NewFormula(@"T_{Rd} = " + (TRd / 1e6).ToString(_formatDouble) + " kNm");
            }
            else
            {
                tauT = 0;
                TRd = 0;
                throw new Exception("Torsion: Section not yet supported");
            }
            #endregion

            if (tauT == 0.0 && tau_w == 0.0)
            {
                VRdy = VplRdy;
                VRdz = VplRdz;
            }
            else
            {
                if (typeShape == typeof(SectionH))
                {
                    VplRdTy = Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) * VplRdy;
                    NewFormula(@"V_{pl,Rd,T,y} = \sqrt{ 1 - \frac{\tau_{Ed}}{1.25 \cdot f_y / 3^{0.5} / \gamma_{m0}} } \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                    VplRdTz = Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) * VplRdz;
                    NewFormula(@"V_{pl,Rd,T,z} = \sqrt{ 1 - \frac{\tau_{Ed}}{1.25 \cdot f_y / 3^{0.5} / \gamma_{m0}} } \cdot V_{pl,Rd,z} = " + (VplRdTz / 1e3).ToString(_formatDouble) + " kN");
                } else if (typeShape == typeof(SectionC)) {
                    VplRdTy = (Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) - tau_w / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ sqrt{ 1 - \frac{\tau_{Ed}}{1.25 \cdot f_y / 3^{0.5} / \gamma_{m0}} } - \frac{\tau_w}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                    VplRdTz = (Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) - tau_w / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                    NewFormula(@"V_{pl,Rd,T,z} = \left[ sqrt{ 1 - \frac{\tau_{Ed}}{1.25 \cdot f_y / 3^{0.5} / \gamma_{m0}} } - \frac{\tau_w}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,z} = " + (VplRdTz / 1e3).ToString(_formatDouble) + " kN");
                } else if (typeShape == typeof(SectionCHS)) { 
                    VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                    VplRdTz = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                    NewFormula(@"V_{pl,Rd,T,z} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,z} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                } else if (typeShape == typeof(SectionRHS)) { 
                    VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                    VplRdTz = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                } else if (typeShape == typeof(SectionT)) {
                    VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                    VplRdTz = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                    NewFormula(@"V_{pl,Rd,T,y} = \left[ 1 - \frac{\tau_{Ed}}{f_y / 3^{0.5} / \gamma_{m0}} \right] \cdot V_{pl,Rd,y} = " + (VplRdTy / 1e3).ToString(_formatDouble) + " kN");
                } else {
                    VRdy = VplRdTy;
                    VRdz = VplRdTz;
                    throw new Exception("section not yet supported");
                }
                VRdy = VplRdTy;
                VRdz = VplRdTz;
                if (VRdy < 0)
                {
                    VRdy = 0;
                }
                if (VRdz < 0) { 
                    VRdz = 0;
                }
            }
        }

        protected void GetMRd(double M1Ed, double M2Ed, out double MzEd, out double MyEd, out double MRdNz, out double MRdNy, out double McRdy, out double McRdz, out double MvRdy, out double MvRdz)
        {
            NewParagraph("Bending Moment Resistance");
            double gm0 = _annex.Gm0;
            double fy = ((SteelMaterial)_sec.Material).Fyk;

            double NEd = _NEd;
            double VyEd = _VyEd;
            double VzEd = _VzEd;

            double A = _sec.Area;
            double Wy;
            double Wz;

            if (_classificationSection < 3)
            {
                Wy = _sec.Wpl22;
                NewFormula(@"W_y = W_{pl,y} = " + Wy.ToString(_formatExponential) + " mm^3");
                Wz = _sec.Wpl11;
                NewFormula(@"W_y = W_{pl,z} = " + Wz.ToString(_formatExponential) + " mm^3");
            } else if (_classificationSection == 3)
            {
                Wy = _sec.Wel22Min;
                NewFormula(@"W_y = W_{el,y} = " + Wy.ToString(_formatExponential) + " mm^3");
                Wz = _sec.Wel11Min;
                NewFormula(@"W_z = W_{el,z} = " + Wz.ToString(_formatExponential) + " mm^3");
            } else
            {
                Wy = _Weffy;
                NewFormula(@"W_y = W_{eff,y} = " + Wy.ToString(_formatExponential) + " mm^3");
                Wz = _Weffz;
                NewFormula(@"W_z = W_{eff,z} = " + Wz.ToString(_formatExponential) + " mm^3");
            }

            McRdy = Wy * fy / gm0;
            NewFormula(@"M_{c,Rd,y} = W_y \cdot f_y / \gamma_{m0} = " + (McRdy / 1e6).ToString(_formatDouble) + " kNm");
            McRdz = Wz * fy / gm0;
            NewFormula(@"M_{c,Rd,z} = W_z \cdot f_y / \gamma_{m0} = " + (McRdz / 1e6).ToString(_formatDouble) + " kNm");

            double rhoy;
            if (VyEd <= 0.5 * _VplTRdy)
            {
                rhoy = 0.0;
                NewFormula(@"V_{Edy} \leq 0.5 \cdot V_{pl,y,T,Rd} \rightarrow \rho_y = " + (rhoy).ToString(_formatDouble) + "");
            } else
            {
                rhoy = Math.Min(Math.Pow(2.0 * Math.Abs(VyEd) / _VplTRdy - 1.0, 2.0), 1.0);
                NewFormula(@"\rho_y = ( \frac{ 2 \cdot V_{Ed,y} }{ V_{pl,y,T,Rd} } - 1 )^2 = " + (rhoy).ToString(_formatDouble) + "");
            }


            double rhoz;
            if (VzEd <= 0.5 * _VplTRdz)
            {
                rhoz = 0.0;
                NewFormula(@"V_{Edz} \leq 0.5 \cdot V_{pl,z,T,Rd} \rightarrow \rho_z = " + (rhoy).ToString(_formatDouble) + "");
            } else
            {
                rhoz = Math.Min(Math.Pow(2.0 * Math.Abs(VzEd) / _VplTRdz - 1.0, 2.0), 1.0);
                NewFormula(@"\rho_z = ( \frac{ 2 \cdot V_{Ed,z} }{ V_{pl,yzT,Rd} } - 1 )^2 = " + (rhoz).ToString(_formatDouble) + "");
            }

            Type typeShape = _sec.GetType();
            //check if bending moment should be recalculated with each plate bending moment contribution multiplied for each rho
            if (typeShape == typeof(SectionH) && _sec.IsDoubleSymmetric == true && _classificationSection < 3)
            {
                SectionH sec = (SectionH)_sec;
                MvRdy = Math.Min((Wy - rhoz * Math.Pow(sec.HeightWeb * sec.ThicknessWeb,2.0)/(4.0 * sec.ThicknessWeb)) * fy / gm0,  Wy * fy / gm0);
                NewFormula(@"M_{v,Rd,y} = min(W_{y} - \rho_z \cdot (h_w \cdt t_W)^2/(4 \cdot t_w) \cdot f_y \cdot \gamma_{m0}, Wy \cdot f_y / \gamma_{m0}) = " + (MvRdy / 1e6).ToString(_formatDouble) + " kNm");
            }
            else
            {
                MvRdy = Wy * (1.0 - rhoz) * fy / gm0;
                NewFormula(@"M_{v,Rd,y} = W_y \cdot (1 - \rho_z) \cdot f_y / \gamma_{m0} = " + (MvRdy / 1e6).ToString(_formatDouble) + " kNm");
            }
            MvRdz = Wz * (1.0 - rhoy) * fy / gm0;
            NewFormula(@"M_{v,Rd,z} = W_z \cdot (1 - \rho_y) \cdt f_y / \gamma_{m0} = " + (MvRdz / 1e6).ToString(_formatDouble) + " kNm");

            if (_classificationSection < 3)
            {
                MRdNy = 0.0;
                MRdNz = 0.0;
                double NplRd = fy * A / gm0;
                double n = Math.Abs(NEd) / NplRd;
                NewFormula(@"n = N_{Ed} / N_{pl,Rd} = " + (n).ToString(_formatDouble) + " ");

                /*if (typeShape == typeof(SectionRectangular))
                {
                    MRdNy = Mrdy * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
                    MRdNz = Mrdz * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
                }
                else */
                if (typeShape == typeof(SectionH))
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    SectionH secH = (SectionH)_sec;
                    if (secH.LenghtBottomFlange == secH.LenghtTopFlange && secH.ThicknessTopFlange == secH.ThicknessBottomFlange)
                    {
                        double a = Math.Min((A - 2.0 * secH.LenghtTopFlange * secH.ThicknessTopFlange) / A, 0.5);
                        NewFormula(@"a = " + (a).ToString(_formatDouble) + "");
                        MRdNy = Math.Min(MvRdy * (1.0 - n) / (1.0 - 0.5 * a), MvRdy);
                        NewFormula(@"M_{Rd,N,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                        MRdNz = 0.0;
                        if (n <= a)
                        {
                            MRdNz = MvRdz;
                            NewFormula(@"n \leq a : M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                        }
                        else
                        {
                            MRdNz = MvRdz * (1.0 - Math.Pow((n - a) / (1.0 - a), 2.0));
                            NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} \cdot (1 - (\frac{n-a}{1-a})^2 = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                        }
                    }
                    else
                    {
                        MRdNy = MvRdy;
                        NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                        MRdNz = MvRdz;
                        NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                    }
                }
                else if (typeShape == typeof(SectionCHS))
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy * (1.0 - Math.Pow(n, 1.7));
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} \cdot (1 - n)^{1.7} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz * (1.0 - Math.Pow(n, 1.7));
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} \cdot (1 - n)^{1.7} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    SectionRHS secRHS = (SectionRHS)_sec;
                    if (secRHS.IsSymmetricAlongZLocalAxis && secRHS.IsSymmetricAlongYLocalAxis)
                    {
                        MzEd = _M1Ed;
                        MyEd = _M2Ed;

                        double b = secRHS.B;
                        double h = secRHS.H;
                        double thk_flange = secRHS.ThicknessFlange;
                        double thk_web = secRHS.ThicknessWeb;

                        double aw = Math.Min((A - 2.0 * b * thk_flange) / A, 0.5);
                        NewFormula(@"a_w = min(\frac{ A-2 \cdot b \cdot t_f }{ A },0.5) = " + aw.ToString(_formatDouble));
                        double af = Math.Min((A - 2.0 * h * thk_web) / A, 0.5);
                        NewFormula(@"a_f = min(\frac{ A-2 \cdot h \cdot t_w }{ A },0.5) = " + af.ToString(_formatDouble));

                        MRdNy = Math.Min(MvRdy * (1.0 - n) / (1 - 0.5 * aw), MvRdy);
                        NewFormula(@"M_{Rd,N,y} = min( M_{v,Rd,y} \cdot \frac{ 1-n }{ 1- 0.5 \cdot a_w } , M_{v,Rd,y} ) = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                        MRdNz = Math.Min(MvRdz * (1.0 - n) / (1 - 0.5 * af), MvRdz);
                        NewFormula(@"M_{Rd,N,z} = min( M_{v,Rd,z} \cdot \frac{ 1-n }{ 1- 0.5 \cdot a_f } , M_{v,Rd,z} ) = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                    } else
                    {
                        throw new Exception("Section not yet supported");
                    }
                }
                else if (typeShape == typeof(SectionC) && _sec.IsSymmetricAlongYLocalAxis == true)
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy;
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz;
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else if (typeShape == typeof(SectionT))
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy;
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz;
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else if (typeShape == typeof(SectionL))
                {
                    //Check are made in principal axis
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy; //Mrd22
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz; //Mrd11
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else
                {
                    /*MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = Mrdy;
                    MRdNz = Mrdz;*/
                    throw new Exception("Section not yet supported");
                }
            } else //Class 3 or 4: Ned/Nrd + My/Myrd + Mz/Mzrd --> No influence of N in Mrd
            {
                if (typeShape == typeof(SectionH) || typeShape == typeof(SectionCHS) || typeShape == typeof(SectionRHS) || typeShape == typeof(SectionT))
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy;
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz;
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else if (typeShape == typeof(SectionC) && _sec.IsSymmetricAlongYLocalAxis == true)
                {
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy;
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz;
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else if (typeShape == typeof(SectionL))
                {
                    //Check are made in principal axis
                    MzEd = _M1Ed;
                    MyEd = _M2Ed;
                    MRdNy = MvRdy; //Mrd22
                    NewFormula(@"M_{Rd,N,y} = M_{v,Rd,y} = " + (MRdNy / 1e6).ToString(_formatDouble) + " kNm");
                    MRdNz = MvRdz; //Mrd11
                    NewFormula(@"M_{Rd,N,z} = M_{v,Rd,z} = " + (MRdNz / 1e6).ToString(_formatDouble) + " kNm");
                }
                else { 
                    //Section L
                    throw new Exception("Section not yet supported");
                }
            }

            if (MvRdy < 0)
            {
                MvRdy = 0;
            }
            if (MvRdz < 0)
            {
                MvRdz = 0;
            }
            if (MRdNy < 0)
            {
                MRdNy = 0;
            }
            if (MRdNz < 0)
            {
                MRdNz = 0;
            }
        }

        protected double GetWrCombined()
        {
            NewParagraph("Bending combined with Axial Force");
            Type typeShape = _sec.GetType();

            if (_classificationSection < 3)
            {
                double alpha = 1.0;
                double beta = 1.0;
                double n = Math.Abs(_NEd) / (_sec.Area * ((SteelMaterial)_sec.Material).Fyk / _annex.Gm0);

                if (typeShape == typeof(SectionH))
                {
                    SectionH sec = (SectionH)_sec;
                    if (sec.LenghtBottomFlange == sec.LenghtTopFlange && sec.ThicknessBottomFlange == sec.ThicknessTopFlange)
                    {
                        alpha = 2.0;
                        NewFormula(@"\alpha = " + (alpha).ToString(_formatDouble) + "");

                        beta = 5.0 * n;
                        NewFormula(@"\beta = " + (beta).ToString(_formatDouble) + "");
                        if (beta < 1.0)
                        {
                            beta = 1.0;
                        }
                    } else
                    {
                        double wr1 = Math.Abs(_NEd / _NRd) + Math.Abs(_MyEd) / _MRdNy + Math.Abs(_MzEd) / _MRdNz;
                        NewFormula(@"w.r. = N/N_{Rd} + M_{Ed,y} / M_{Rd,N,y} + M_{Ed,z} / M_{Rd,N,z} = " + wr1.ToString(_formatDouble) + "");
                        return wr1;
                    }
                }
                else if (typeShape == typeof(SectionCHS))
                {
                    alpha = 2.0;
                    NewFormula(@"\alpha = " + alpha.ToString(_formatDouble));
                    beta = 2.0;
                    NewFormula(@"\beta = " + beta.ToString(_formatDouble));
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    alpha = Math.Min(1.66 / (1.0 - 1.13 * Math.Pow(n, 2.0)), 6.0);
                    NewFormula(@"\alpha = " + alpha.ToString(_formatDouble));
                    beta = alpha;
                    NewFormula(@"\beta = " + beta.ToString(_formatDouble));
                }
                else
                {
                    double wr2 = Math.Abs(_NEd / _NRd) + Math.Abs(_MyEd) / _MRdNy + Math.Abs(_MzEd) / _MRdNz;
                    NewFormula(@"w.r. = N/N_{Rd} + M_{Ed,y} / M_{Rd,N,y} + M_{Ed,z} / M_{Rd,N,z} = " + wr2.ToString(_formatDouble) + "");
                    return wr2;
                }
                double wr3 = Math.Pow(Math.Abs(_MyEd) / _MRdNy, alpha) + Math.Pow(Math.Abs(_MzEd) / _MRdNz, beta);
                NewFormula(@"w.r. = (M_{Ed,y} / M_{Rd,N,y})^{\alpha} + (M_{Ed,z} / M_{Rd,N,z})^{\beta} = " + wr3.ToString(_formatDouble) + "");
                return wr3;
            }
            else if (_classificationSection == 3)
            {
                double wr = Math.Abs(_NEd / _NRd) + Math.Abs(_MyEd) / _MRdNy + Math.Abs(_MzEd) / _MRdNz;
                NewFormula(@"w.r. = N/N_{Rd} + M_{Ed,y} / M_{Rd,N,y} + M_{Ed,z} / M_{Rd,N,z} = " + wr.ToString(_formatDouble) + "");
                return wr;
            }
            else
            {
                double wr = Math.Abs(_NEd / _NRd) + Math.Abs(_MyEd + _NEd * _deltaG.Y) / _MRdNy + Math.Abs(_MyEd + _NEd * _deltaG.X) / _MRdNz; //attention to sign
                NewFormula(@"w.r. = N/N_{Rd} + (N_{Ed} \cdot e + M_{Ed,y}) / M_{Rd,N,y} + (N_{Ed} \cdot e + M_{Ed,z}) / M_{Rd,N,z} = " + wr.ToString(_formatDouble) + "");
                return wr;
            }
        }
        #endregion

        #region BucklingFunction
        protected double GetNcrEuler(double E, double J, double L0)
        {
            double Ncr = Math.Pow(Math.PI, 2.0) * E * J / (Math.Pow(L0, 2.0));
            return Ncr;
        }

        protected double GetLambdaSegn(double A, double fy, double Ncr)
        {
            double lambdaSegn = Math.Pow(A * fy / Ncr, 0.5);
            return lambdaSegn;
        }

        protected double GetPhi(double alpha, double lambda_segn, double beta = 1.0, double lambda_LT0 = 0.2) //default beta=1, lambda_LT0 = 0.2
        {
            double Phi = 0.5 * (1 + alpha * (lambda_segn - lambda_LT0) + beta * Math.Pow(lambda_segn, 2.0));
            return Phi;
        }

        protected double GetChi(double Phi, double lambda_segn)
        {
            double Chi = Math.Min(1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - Math.Pow(lambda_segn, 2.0), 0.5)),1.0);
            return Chi;
        }

        protected double GetChiLTmod(double Phi, double lambda_segn, double beta = 1.0, double f = 1.0) //beta_default = 1, f=1
        {
            double Chi = 1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - beta * Math.Pow(lambda_segn, 2.0), 0.5));
            NewFormula(@"\chi_{LT} = \frac{1}{\Phi_{LT} + \sqrt{\Phi_{LT}^2 - \beta \cdot \lambda_{LT}^2}} = " + Chi.ToString(_formatDouble));
            Chi = Math.Min(Chi, 1.0);
            NewFormula(@"\chi_{LT} = min(\chi_{LT}, 1) = " + Chi.ToString(_formatDouble));
            Chi = Math.Min(Chi, 1.0 / Math.Pow(lambda_segn, 2.0));
            NewFormula(@"\chi_{LT} = min(\chi_{LT}, 1 / \lambda_{LT}^2) = " + Chi.ToString(_formatDouble));

            double ChiLTMod = Math.Min(Chi/f, 1.0);
            ChiLTMod = Math.Min(ChiLTMod, 1.0 / Math.Pow(lambda_segn, 2.0));
            NewFormula(@"\chi_{LT, mod} = min(\chi_{LT} / f, 1) = " + Chi.ToString(_formatDouble));
            return ChiLTMod;
        }

        protected double GetNcrT(double iy, double iz, double y0, double z0, double E, double G, double It, double Jw, double _L0LT)  //EN 1993-1-3 eq 6.33a
        {
            /*
             * y0 and z0 = coordinates of shear center in respect of the centroid gross section
             */
            double i0 = Math.Pow(Math.Pow(iy, 2.0) + Math.Pow(iz, 2.0) + Math.Pow(y0, 2.0) + Math.Pow(z0, 2.0), 0.5);
            NewFormula(@"i_0 = \sqrt{i_y^2 + i_z^2 + y_0^2 + z_0^2} = " + i0.ToString(_formatDouble) + " mm");
            double Ncr_T = 1.0 / Math.Pow(i0, 2.0) * (G * It + Math.Pow(Math.PI, 2.0) * E * Jw / Math.Pow(_L0LT, 2.0));
            NewFormula(@"N_{cr,T} = \frac{1}{ i_{0}^{2} } \cdot \left( G \cdot J_{t} + \frac{ \pi^2 \cdot E \cdot J_{w} }{ L_{0,LT}^{2} } \right) = " + (NcrT/1e3).ToString(_formatDouble) + " kN");
            return Ncr_T;
        }

        protected double GetNcrTF(double iy, double iz, double y0, double z0, double Ncr_y, double Ncr_z, double Ncr_T) //EN 1993-1-3 eq 6.35
        {
            /*
             * z0 coordinates of shear center in respect of the centroid gross section
             * y0 coordinates of shear center in respect of the centroid gross section
             */

            double i0 = Math.Pow(Math.Pow(iy, 2.0) + Math.Pow(iz, 2.0) + Math.Pow(y0, 2.0) + Math.Pow(z0, 2.0), 0.5);
            double beta;
            double Ncr_TF;
            if (z0 == 0 && y0 != 0)
            {
                beta = 1.0 - Math.Pow(y0 / i0, 2.0);
                NewFormula(@"\beta = 1 - (y_0 / i_0)^2 = " + beta.ToString(_formatDouble));
                Ncr_TF = Ncr_y / (2.0 * beta) * (1.0 + Ncr_T / Ncr_y - Math.Pow(Math.Pow(1.0 - Ncr_T / Ncr_y, 2.0) + 4.0 * Math.Pow(y0 / i0, 2.0) * Ncr_T / Ncr_y, 0.5));
                NewFormula(@"N_{cr,TF} = \frac{N_{cr,y}}{2 \cdot \beta} \cdot \left( 1 + N_{crT} / N_{cry} - \sqrt{(1 - N_{crT} / N_{cry})^{2} + 4 \cdot (y_0 / i_0)^{2} \cdot N_{crT} / N_{cr,y}} \right) = " + (NcrTF / 1e3).ToString(_formatDouble) + "kN");
            }
            else if (y0 == 0 && z0 != 0)
            {
                beta = 1.0 - Math.Pow(z0 / i0, 2.0);
                NewFormula(@"\beta = 1 - (z_0 / i_0)^2 = " + beta.ToString(_formatDouble));
                Ncr_TF = Ncr_z / (2.0 * beta) * (1.0 + Ncr_T / Ncr_z - Math.Pow(Math.Pow(1.0 - Ncr_T / Ncr_z, 2.0) + 4.0 * Math.Pow(z0 / i0, 2.0) * Ncr_T / Ncr_z, 0.5));
                NewFormula(@"N_{cr,TF} = \frac{N_{cr,z}}{2 \cdot \beta} \cdot \left( 1 + N_{crT} / N_{crz} - \sqrt{(1 - N_{crT} / N_{crz})^{2} + 4 \cdot (z_0 / i_0)^{2} \cdot N_{crT} / N_{cr,z}} \right) = " + (NcrTF / 1e3).ToString(_formatDouble) + "kN");
            }
            else if (z0 == 0 && y0 == 0)
            {
                Ncr_TF = Ncr_T;
                NewFormula(@"N_{cr,TF} = N_{cr,T} = " + (NcrTF / 1e3).ToString(_formatDouble) + " kN");
            }
            else
            {
                /* solve --> x:
                 * (Ncrz * x ) * (Ncry * x) * (NcrT * x) - (Ncrz - x) * x^2 * x0^2/i0^2 - (Ncry - x) * x^2 * y0^2/i0^2 = 0
                 * x = g(x)
                 * x = - (Ncrz - x) / ((NcrT - x) * (Ncry - x)) * x * x * x0*x0/(i0*i0) - (Ncry - x) / ((NcrT - x) * (Ncry - x)) * x * x * y0*x0/(i0*i0) + Ncry;
                 * check if Ncry and Ncrz should be inverted in this equation
                 */
                int iter = 0;
                Ncr_TF = 0;
                while (iter < 15)
                {
                    iter++;
                    Ncr_TF = -(Ncr_z - Ncr_TF) / ((Ncr_T - Ncr_TF) * (Ncr_y - Ncr_TF)) * Ncr_TF * Ncr_TF * z0 * z0 / (i0 * i0) - (Ncr_y - Ncr_TF) / ((Ncr_T - Ncr_TF) * (Ncr_y - Ncr_TF)) * Ncr_TF * Ncr_TF * y0 * z0 / (i0 * i0) + Ncr_y;
                }
                //to be checked
                NewParagraph("..iterations for NcrTF..");
                NewFormula(@"N_{cr,TF} = " + (NcrTF / 1e3).ToString(_formatDouble) + " kN");
            }

            Ncr_TF = Math.Min(Ncr_TF, Ncr_y);
            Ncr_TF = Math.Min(Ncr_TF, Ncr_z);
            Ncr_TF = Math.Min(Ncr_TF, Ncr_T);
            NewFormula(@"N_{cr,TF} = min(N_{cr,T}, N_{cr,y}, N_{cr,z}) =" + (NcrTF / 1e3).ToString(_formatDouble) + " kN");

            return Ncr_TF;
        }

        protected double GetMcrLT(double L, double Jt, double Jw, double Jz,   double E, double G, SupportCondition supportCondition, LoadCondition loadCondition, double? psi, double k, double kw, out double c1, out double c2, out double c3, out double zg, out double zj)
        {
            /*
             * C1 = factor that account for the shape of the moment diagram
             * C2 = factor that account for the point of load application in relation to the shear center
             * C3 = factor that account asymmetry about y-axis
             * 
             * zg = è la distanza tra il punto di applicazione del carico e il centro di taglio:
             *      -positiva se il carico è diretto dall'alto verso il basso ed è applicato all'estradosso. Se invece agisce dal basso verso l'alto il segno va cambiato
             *      -negativa se il carico è diretto dall'alto verso il basso ed è applicato all'intradosso.
             * 
             */

            //calculation of zg calculatet from the top of section to the shear center:
            //zg coordinate of point of application vs coordinate of shear center
            //zj zs (shear center) - 0.5 integral(y^2+z^2) * z / Jy dA
            Type typeSection = _sec.GetType();
            if (typeSection == typeof(SectionCHS))
            {
                SectionCHS sec = (SectionCHS)_sec;
                zg = sec.D - sec.ShearCenter.Y;
                //NewFormula("z_g = " + zg.ToString(_formatDouble));
            } else if (typeSection == typeof(SectionRHS))
            {
                SectionRHS sec = (SectionRHS)_sec;
                zg = sec.H - sec.ShearCenter.Y;
                //NewFormula("z_g = " + zg.ToString(_formatDouble));
            }
            else if (typeSection == typeof(SectionH))
            {
                SectionH sec = (SectionH)_sec;
                zg = sec.H - sec.ShearCenter.Y;
                //NewFormula("z_g = " + zg.ToString(_formatDouble));
            }
            else if (typeSection == typeof(SectionC))
            {
                SectionC sec = (SectionC)_sec;
                zg = sec.H - sec.ShearCenter.Y;
                //NewFormula("z_g = " + zg.ToString(_formatDouble));
            }
            else if (typeSection == typeof(SectionT))
            {
                SectionT sec = (SectionT)_sec;
                zg = sec.H - sec.ShearCenter.Y;
                //NewFormula("z_g = " + zg.ToString(_formatDouble));
            }
            else
            {
                throw new Exception("McrLT not yet supported for this section");
            }

            if (_sec.IsDoubleSymmetric)
            {
                c3 = 0.0;
                zj = 0.0;

                if (loadCondition != LoadCondition.NotDirectlyLoaded) { 
                    if (supportCondition == SupportCondition.HingesAtEnds)
                    {
                        if (loadCondition == LoadCondition.Constant)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            c1 = 1.127; 
                            c2 = 0.454;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            c1 = 1.348;
                            c2 = 0.630;
                        } else
                        {
                            throw new NotSupportedException("Load condition + Support not yet supported");
                        }
                    }
                    else if (supportCondition == SupportCondition.EndsRestrained)
                    {
                        if (loadCondition == LoadCondition.Constant)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            c1 = 2.578;
                            c2 = 1.554;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            c1 = 1.683;
                            c2 = 1.645;
                        } else
                        {
                            throw new NotSupportedException("Load condition + Support not yet supported");
                        }
                    } else
                    {
                        throw new Exception("SupportCondition not supported");
                    }
                } else if (loadCondition == LoadCondition.NotDirectlyLoaded) //Beam not directly loaded but with bending moment at the ends
                {
                    if (psi.HasValue)
                    {
                        if (k == 1)
                        {
                            //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                            c1 = Math.Min(1.77 - 1.04 * psi.Value + 0.27 * psi.Value * psi.Value, 2.6);
                        } else
                        {
                            throw new Exception("k != 1 : cannot calculate C1 for Mcr");
                        }
                        //ENV 1993-1-1:1992 (F3)
                        //C1 = Math.Min(1.88 - 1.40 * psi + 0.52 * psi * psi, 2.7);
                        c2 = 0;
                    } else
                    {
                        throw new Exception("Set the value of psi = M(x=0)/M(x=L)");
                    }
                } else
                {
                    throw new Exception("Load condition + Support not yet supported");
                }
                
            }
            else if (_sec.IsSymmetricAlongZLocalAxis || typeSection == typeof(SectionC))
            {
                /* note: use of Mcr also for C sections came from :
                Lateral-torsional Buckling of Steel Channel Beams
                A parametric study through FE - analysis
                Master’s Thesis in the Master’s Programme Structural Engineering and Building Technology
                CARL - MARCUS EKSTRÖM - DAVID WESLEY
                */
                if (typeSection == typeof(SectionH))
                {
                    SectionH sec = (SectionH)_sec;
                    double Ifc; //inertia along the weak axis of the beam of compression flange
                    double Ift; //inertia along the weak axis of the beam of tension flange
                    if (_MyEd >= 0) { //tension bottom
                        Ift = 1.0 / 12.0 * sec.ThicknessBottomFlange * Math.Pow(sec.LenghtBottomFlange, 3.0);
                        Ifc = 1.0 / 12.0 * sec.ThicknessTopFlange * Math.Pow(sec.LenghtTopFlange, 3.0);
                    } else { //tension up
                        Ifc = 1.0 / 12.0 * sec.ThicknessBottomFlange * Math.Pow(sec.LenghtBottomFlange, 3.0);
                        Ift = 1.0 / 12.0 * sec.ThicknessTopFlange * Math.Pow(sec.LenghtTopFlange, 3.0);
                    }
                    //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                    //pg 230
                    double psif = (Ifc - Ift) / (Ifc + Ift);
                    double hs = sec.H - sec.ThicknessBottomFlange / 2.0 - sec.ThicknessTopFlange / 2.0; // distance between the shear center of the flanges

                    if (psif >= 0)
                    {
                        zj = 0.8 * psif * hs / 2.0; //Wagner coeff
                    } else
                    {
                        zj = psif * hs / 2.0;
                    }

                    //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                    //pg 233
                    if (psif <= 0.9 && psif >= -0.9)
                    {
                        McrC1C2C3(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                    } else
                    {
                        throw new Exception("cannot calc McrLT. Section too asymmetric");
                    }
                } else if (typeSection == typeof(SectionC)) {
                    SectionC sec = (SectionC)_sec;
                    if (sec.LBottom == sec.LTop && sec.ThicknessBottom == sec.ThicknessTop)
                    {
                        double psif = 0;
                        zj = 0; //z centroid = z shear center + integral is 0 due to symmetry
                        McrC1C2C3(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                    } else
                    {
                        throw new Exception("Section not yet supported for calculation of McrLT");
                    }
                }
                else if (typeSection == typeof(SectionT))
                {
                    SectionT sec = (SectionT)_sec;
                    double psif;
                    if (_MyEd > 0)
                    {
                        psif = 1;
                    } else
                    {
                        psif = -1;
                    }

                    //calculation of Wagner coefficiente: zj = zs - 0.5 integral((y^2 + z^2) * z dA) / Jy
                    //needed for for Mcr calculation
                    int nxFlange = 20;
                    int nyFlange = 4;
                    int nxWeb = 4;
                    int nyWeb = 30;
                    double integral = 0;
                    
                    for (int i = 0; i < nxFlange; i++)
                    {
                        for (int j = 0; j < nyFlange; j++)
                        {
                            double Ai = (sec.B / nxFlange) * (sec.Tf / nyFlange);
                            double zi = sec.H - (2.0 * j + 1.0) / (2.0 * nyFlange) * sec.Tf - sec.Centroid.Y;
                            double yi = -sec.B / 2.0 + (2.0 * i + 1.0) / (2.0 * nxFlange) * sec.B;

                            integral = integral + (yi * yi + zi * zi) * zi * Ai;
                        }
                    }

                    for (int i = 0; i < nxWeb; i++)
                    {
                        for (int j = 0; j < nyWeb; j++)
                        {
                            double Ai = (sec.Hw / nyWeb) * (sec.Tw / nxWeb);
                            double zi = (2.0 * j + 1.0) / (2.0 * nyWeb) * (sec.Hw) - sec.Centroid.Y;
                            double yi = -sec.Tw / 2.0 + (2.0 * i + 1.0) * sec.Tw / (2.0 * nxWeb);

                            integral = integral + (yi * yi + zi * zi) * zi * Ai;
                        }
                    }
                    zj = (sec.ShearCenter.Y - sec.Centroid.Y) - 0.5 * integral / sec.J22;
                    if (_M2Ed < 0)
                    {
                        zj = -zj; //check this   
                    }
            
                    //this should be used if  -0.9 < psif < 0.9. There is no data...so...what to do?
                    McrC1C2C3(supportCondition, loadCondition, k, psi, psif, out c1, out c2, out c3);
                }
                else {
                    throw new Exception("Section not yet supported for calculation of McrLT");
                }
            } else //NO sysmmetry
            {
                throw new Exception("Cannot calc McrLT. Any symmetry");
            }

            NewFormula("C_1 = " + c1.ToString(_formatDouble));
            NewFormula("C_2 = " + c1.ToString(_formatDouble));
            NewFormula("C_3 = " + c1.ToString(_formatDouble));
            NewFormula("k = " + k.ToString(_formatDouble));
            NewFormula("k_w = " + kw.ToString(_formatDouble));
            NewFormula("z_g = " + zg.ToString(_formatDouble) + " mm");
            NewFormula("z_j = " + zj.ToString(_formatDouble) + " mm");

            double McrLT = c1 * Math.Pow(Math.PI, 2.0) * E * Jz / Math.Pow(k * L, 2.0) * (Math.Pow(Math.Pow(k / kw, 2.0) * Jw / Jz + Math.Pow(k * L, 2.0) * G * Jt / (Math.Pow(Math.PI, 2.0) * E * Jz) + Math.Pow(c2 * zg - c3 * zj, 2.0), 0.5) - (c2 * zg - c3 * zj));
            NewFormula(@"M_{cr,LT} = C_{1} \cdot \frac{ \pi^{2} \cdot E \cdot J_{z} }{ ( k \cdot L )^{2} } \left{ [ ( \frac{ k }{ k_{w} } )^{2} \cdot \frac{ J_{w} }{ J_{z} } + \frac{ ( k \cdot L )^{2} \cdot G \cdot J_{t} }{ \pi^{2} \cdot E \cdot J_{z} } + ( C_{2} \cdot z_{g} - C_{3} \cdot z_{j} )^{2} ]^{0.5} - ( C_{2} \cdot z_{g} - C_{3} \cdot z_{j} ) \right} = "+ (McrLT/1e6).ToString(_formatDouble) + " kNm");
            return McrLT;
        }

        protected void McrC1C2C3(SupportCondition supportCondition, LoadCondition loadCondition, double k, double? psi, double psif, out double C1, out double C2, out double C3)
        {
            C1 = 0;
            C2 = 0;
            C3 = 0;
            //tables 63 and 64 of Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability can be used
            if (supportCondition == SupportCondition.EndsRestrained)
            {
                if (loadCondition == LoadCondition.NotDirectlyLoaded)
                {
                    C2 = 0;
                    double interpolation(double x0, double y0, double x1, double y1, double xc) { return (y1 - y0) / (x1 - x0) * (xc - x1) + y1; }
                    if (k == 1)
                    {
                        if (0.75 <= psi && psi <= 1.0)
                        {
                            C1 = interpolation(0.75, 1.14, 1, 1, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.5 <= psi && psi <= 0.75)
                        {
                            C1 = interpolation(0.5, 1.31, 0.75, 1.14, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.25 <= psi && psi <= 0.5)
                        {
                            C1 = interpolation(0.25, 1.52, 0.5, 1.31, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.0 <= psi && psi <= 0.25)
                        {
                            C1 = interpolation(0.0, 1.77, 0.25, 1.52, psi.Value);
                            C3 = 1.0;
                        }
                        else if (-0.25 <= psi && psi <= 0)
                        {
                            C1 = interpolation(-0.25, 2.06, 0, 1.77, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.25, 0.85, 0, 1, psi.Value);
                            }
                        }
                        else if (-0.5 <= psi && psi <= -0.25)
                        {
                            C1 = interpolation(-0.5, 2.35, -0.25, 2.06, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.5, 1.3 - 1.2 * psif, -0.25, 0.85, psi.Value);
                            }
                        }
                        else if (-0.75 <= psi && psi <= -0.5)
                        {
                            C1 = interpolation(-0.75, 2.60, -0.50, 2.35, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.75, 0.55 - psif, -0.5, 1.3 - 1.2 * psif, psi.Value);
                            }
                        }
                        else if (-1 <= psi && psi <= -0.75)
                        {
                            C1 = interpolation(-1.0, 2.60, -0.75, 2.60, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-1.0, -psif, -0.75, 1.0, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-1.0, -psif, -0.75, 0.55 - psif, psi.Value);
                            }
                        }
                        else
                        {
                            throw new Exception("cannot calc McrLT");
                        }
                    }
                    else if (k == 0.5)
                    {
                        if (0.75 <= psi && psi <= 1.0)
                        {
                            C1 = interpolation(0.75, 1.19, 1, 1.05, psi.Value);
                            C3 = interpolation(0.75, 1.017, 1, 1.019, psi.Value);
                        }
                        else if (0.5 <= psi && psi <= 0.75)
                        {
                            C1 = interpolation(0.5, 1.37, 0.75, 1.19, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.25 <= psi && psi <= 0.5)
                        {
                            C1 = interpolation(0.25, 1.60, 0.5, 1.37, psi.Value);
                            C3 = 1.0;
                        }
                        else if (0.0 <= psi && psi <= 0.25)
                        {
                            C1 = interpolation(0.0, 1.86, 0.25, 1.60, psi.Value);
                            C3 = 1.0;
                        }
                        else if (-0.25 <= psi && psi <= 0)
                        {
                            C1 = interpolation(-0.25, 2.15, 0, 1.86, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = 1.0;
                            }
                            else
                            {
                                C3 = interpolation(-0.25, 0.65, 0, 1, psi.Value);
                            }
                        }
                        else if (-0.5 <= psi && psi <= -0.25)
                        {
                            C1 = interpolation(-0.5, 2.42, -0.25, 2.15, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-0.5, 0.95, -0.25, 1, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-0.5, 0.77 - psif, -0.25, 0.65, psi.Value);
                            }
                        }
                        else if (-0.75 <= psi && psi <= -0.5)
                        {
                            C1 = interpolation(-0.75, 2.45, -0.50, 2.42, psi.Value);
                            if (psif <= 0)
                            {
                                C3 = interpolation(-0.75, 0.85, -0.5, 0.95, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-0.75, 0.35 - psif, -0.5, 0.77 - psif, psi.Value);
                            }
                        }
                        else if (-1 <= psi && psi <= -0.75)
                        {
                            C1 = 2.45;
                            if (psif <= 0)
                            {
                                C3 = interpolation(-1.0, 0.125 - 0.7 * psif, -0.75, 0.85, psi.Value);
                            }
                            else
                            {
                                C3 = interpolation(-1.0, -0.125 - 0.7 * psif, -0.75, 0.35 - psif, psi.Value);
                            }
                        }
                        else
                        {
                            throw new Exception("cannot calc McrLT, psi < -1 or psi > 1!");
                        }
                    }
                    else
                    {
                        throw new Exception("cannot calc McrLT, k != 1 or k != 0.5");
                    }
                }
                else
                {
                    throw new Exception("cannot calc McrLT, no literature");
                }
            }
            else if (supportCondition == SupportCondition.HingesAtEnds)
            {
                if (k == 1)
                {
                    if (loadCondition == LoadCondition.Constant)
                    {
                        C1 = 1.12;
                        C2 = 0.45;
                        C3 = 0.525;
                    }
                    else if (loadCondition == LoadCondition.SingleForce)
                    {
                        C1 = 1.35;
                        C2 = 0.59;
                        C3 = 0.411;
                    }
                }
                else if (k == 0.5)
                {
                    if (loadCondition == LoadCondition.Constant)
                    {
                        C1 = 0.97;
                        C2 = 0.36;
                        C3 = 0.478;
                    }
                    else if (loadCondition == LoadCondition.SingleForce)
                    {
                        C1 = 1.05;
                        C2 = 0.48;
                        C3 = 0.338;
                    }
                }
                else
                {
                    throw new Exception("cannot calc McrLT, no literature");
                }
            }
            else
            {
                throw new Exception("cannot calc McrLT, no literature");
            }
        }

        protected void GetImperfectionFactor(out double _alphay, out double _alphaz)
        {
            Dictionary<string, double> SectionBucklingCurves = new Dictionary<string, double>();
            SectionBucklingCurves.Add("a0", 0.13);
            SectionBucklingCurves.Add("a", 0.21);
            SectionBucklingCurves.Add("b", 0.34);
            SectionBucklingCurves.Add("c", 0.49);
            SectionBucklingCurves.Add("d", 0.76);

            _alphay = 1;
            _alphaz = 1;
            Type typeShape = _sec.GetType();
            if (typeShape == typeof(SectionH))
            {
                SectionH sec = (SectionH)_sec;
                if (sec.IsRolled)
                {
                    if (sec.H / sec.B > 1.2)
                    {
                        if (sec.ThicknessBottomFlange <= 40.0 && sec.ThicknessTopFlange <= 40.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["a0"];
                                NewParagraph("y direction : curve a0");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["a0"];
                                NewParagraph("z direction : curve a0");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["a"];
                                NewParagraph("y direction : curve a");
                                NewFormula(@"\alpha_y = " + _alphaz.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["b"];
                                NewParagraph("z direction : curve b");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                        }
                        else if (sec.ThicknessBottomFlange <= 100.0 && sec.ThicknessTopFlange <= 100.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["a"];
                                NewParagraph("y direction : curve a");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["a"];
                                NewParagraph("z direction : curve a");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["b"];
                                NewParagraph("y direction : curve b");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["c"];
                                NewParagraph("z direction : curve c");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                        }
                    }
                    else // H/B <= 1.2
                    {
                        if (sec.ThicknessBottomFlange <= 100.0 && sec.ThicknessTopFlange <= 100.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["a"];
                                NewParagraph("y direction : curve a");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["a"];
                                NewParagraph("z direction : curve a");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["b"];
                                NewParagraph("y direction : curve b");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["c"];
                                NewParagraph("z direction : curve c");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                        }
                        else //thicknessflanges > 100 mm 
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["c"];
                                NewParagraph("y direction : curve c");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["c"];
                                NewParagraph("z direction : curve c");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["d"];
                                NewParagraph("y direction : curve d");
                                NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                                _alphaz = SectionBucklingCurves["d"];
                                NewParagraph("z direction : curve d");
                                NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                            }
                        }
                    }
                } else
                {
                    if (sec.ThicknessTopFlange <= 40.0 & sec.ThicknessBottomFlange <= 40.0)
                    {
                        if (sec.Material.Name.Contains("460"))
                        {
                            _alphay = SectionBucklingCurves["b"];
                            NewParagraph("y direction : curve b");
                            NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                            _alphaz = SectionBucklingCurves["c"];
                            NewParagraph("z direction : curve c");
                            NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                        }
                        else //steel S235, 275, 355, 420
                        {
                            _alphay = SectionBucklingCurves["b"];
                            NewParagraph("y direction : curve b");
                            NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                            _alphaz = SectionBucklingCurves["c"];
                            NewParagraph("z direction : curve c");
                            NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                        }
                    } else
                    {
                        if (sec.Material.Name.Contains("460"))
                        {
                            _alphay = SectionBucklingCurves["c"];
                            NewParagraph("y direction : curve c");
                            NewFormula(@"\alpha_y = " + _alphaz.ToString(_formatDouble));
                            _alphaz = SectionBucklingCurves["d"];
                            NewParagraph("z direction : curve d");
                            NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                        }
                        else //steel S235, 275, 355, 420
                        {
                            _alphay = SectionBucklingCurves["c"];
                            NewParagraph("y direction : curve c");
                            NewFormula(@"\alpha_y = " + _alphay.ToString(_formatDouble));
                            _alphaz = SectionBucklingCurves["d"];
                            NewParagraph("z direction : curve d");
                            NewFormula(@"\alpha_z = " + _alphaz.ToString(_formatDouble));
                        }
                    }
                }
            } else if (typeShape == typeof(SectionRHS) || typeShape == typeof(SectionCHS))
            {
                bool isColdFormed;
                if (typeShape == typeof(SectionRHS))
                {
                    isColdFormed = ((SectionRHS)_sec).IsColdFormed;
                } else
                {
                    isColdFormed = ((SectionCHS)_sec).IsColdFormed;
                }

                if (isColdFormed)
                {
                    _alphay = SectionBucklingCurves["c"];
                    _alphaz = SectionBucklingCurves["c"];
                } else //Hot Finished
                {
                    if (_sec.Material.Name.Contains("460"))
                    {
                        _alphay = SectionBucklingCurves["a0"];
                        _alphaz = SectionBucklingCurves["a0"];
                    }
                    else //steel S235, 275, 355, 420
                    {
                        _alphay = SectionBucklingCurves["a"];
                        _alphaz = SectionBucklingCurves["a"];
                    }
                }
            } else if (typeShape == typeof(SectionC) || typeShape == typeof(SectionT) /*|| typeShape == typeof(SectionRectangular)*/ || typeShape == typeof(SectionCircular))
            {
                _alphay = SectionBucklingCurves["c"];
                _alphaz = SectionBucklingCurves["c"];
            } else if (typeShape == typeof(SectionL))
            {
                _alphay = SectionBucklingCurves["b"];
                _alphaz = SectionBucklingCurves["b"];
            } else
            {
                throw new Exception("Which curve for buckling?");
            }
        }

        protected double GetImperfectionFactorLT(bool useEquation_6_57)
        {
            if (useEquation_6_57 == true)
            {
                NewParagraph("Use eq. 6.57 EN 1993-1-1:");
            }
            double alpha_LT;

            Dictionary<string, double> SectionBucklingLTCurves = new Dictionary<string, double>();
            SectionBucklingLTCurves.Add("a", 0.21);
            SectionBucklingLTCurves.Add("b", 0.34);
            SectionBucklingLTCurves.Add("c", 0.49);
            SectionBucklingLTCurves.Add("d", 0.76);

            Type typeShape = _sec.GetType();
            //TABLE 6.4 - EN 1993-1-1
            if (typeShape == typeof(SectionH))
            {
                SectionH sec = (SectionH)_sec;
                if (sec.IsRolled)
                {
                    if (sec.H / sec.B <= 2.0)
                    {
                        alpha_LT = SectionBucklingLTCurves["a"];
                        if (useEquation_6_57)
                        {
                            alpha_LT = SectionBucklingLTCurves["b"];
                        }
                    } else
                    {
                        alpha_LT = SectionBucklingLTCurves["b"];
                        if (useEquation_6_57)
                        {
                            alpha_LT = SectionBucklingLTCurves["c"];
                        }
                    }
                }
                else //welded
                {
                    if (sec.IsDoubleSymmetric)
                    {
                        if (sec.H / sec.B <= 2.0)
                        {
                            alpha_LT = SectionBucklingLTCurves["c"];
                        }
                        else
                        {
                            alpha_LT = SectionBucklingLTCurves["d"];
                        }
                    } else
                    {
                        alpha_LT = SectionBucklingLTCurves["d"];
                    }
                }
            }
            else
            {
                alpha_LT = SectionBucklingLTCurves["d"];
            }
            return alpha_LT;
        }

        protected double Getkc(double lambda_segn_LT, SupportCondition supportCondition, LoadCondition loadCondition, double? psi)
        {
            double kc;
            if (loadCondition != LoadCondition.NotDirectlyLoaded)
            {
                if (supportCondition == SupportCondition.HingesAtEnds && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.94;
                } else if (supportCondition == SupportCondition.EndsRestrained && loadCondition ==  LoadCondition.Constant)
                {
                    kc = 0.90;
                } else if (supportCondition == SupportCondition.OneSideRestrained_OneSideHinged && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.91;
                } else if (supportCondition == SupportCondition.HingesAtEnds && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.86;
                } else if (supportCondition == SupportCondition.EndsRestrained && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.77;
                } else if (supportCondition == SupportCondition.OneSideRestrained_OneSideHinged && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.82;
                } else
                {
                    throw new Exception("unexpected kc");
                }
            } else
            {
                if (psi.HasValue)
                {
                    kc = 1.0 / (1.33 - 0.33 * psi.Value);
                } else
                {
                    throw new Exception("Se a psi value = M(x=0)/M(x=L)");
                }
            }
            return kc;
        }

        protected double GetCMi0(LoadCondition loadCondition, SupportCondition supportCondition, double? psi, double? MEdMax, double? deflection, double NEd, double Ncr)
        {
            NewParagraph("Calculation of Cmi according to Table A.2 EN 1993-1-1");
            if (loadCondition == LoadCondition.SingleForce && supportCondition == SupportCondition.HingesAtEnds)
            {
                return 1.0 - 0.18 * NEd / Ncr;
            }
            else if (loadCondition == LoadCondition.Constant && supportCondition == SupportCondition.HingesAtEnds)
            {
                return 1 + 0.03 * NEd / Ncr;
            }
            else if (loadCondition == LoadCondition.NotDirectlyLoaded)
            {
                if (psi.HasValue)
                {
                    return 0.79 + 0.21 * psi.Value + 0.36 * (psi.Value - 0.33) * NEd / Ncr;
                }
                else
                {
                    throw new Exception("Set a value to phi = M(x=0)/M(x=L);");
                }
            }
            else
            {
                if (deflection.HasValue && MEdMax.HasValue)
                {
                    return 1.0 + (Math.PI * Math.PI * _sec.Material.E * Math.Abs(deflection.Value) / (_L * _L * MEdMax.Value) - 1.0) * NEd / Ncr;
                }
                else
                {
                    throw new Exception("Set delta and Mmax");
                }
            }
        }
        protected double GetMu(double Ned, double Ncr, double Chi)
        { 
            double mu = (1.0 - Ned / Ncr) / (1.0 - Chi * Ned / Ncr);
            return mu;
        }

        public static double getC1(double k, double kw, double MMax, double M1, double M2, double M3, double M4, double M5)
        {
            //C1 for Mcr = C1 * PI^2 * E Jz / (kz * L)^2 * ((kz/kw)^2 * Jw / Jz + (kz * L)^2 * G * Jt / (PI^2*E*Jz))^0.5
            //valid for any distribution of bending moment, but, with bisymmetric section
            //reference: Lateral torsional buckling of steel beams: a general expresion for the moment gradient factor - Aitzilber Lopez, Danny J. Yong and Miguel A. Serna
            //Mmax = max abosolute bending moment in the beam
            //M1 = M(x=0);
            //M2 = M(x=L/4);
            //M3 = M(x=L/2);
            //M4 = M(x=3/4*L);
            //M5 = M(x=L);
            double keq = Math.Sqrt(k * kw);
            double alpha1 = 1 - kw;
            double alpha2 = 5.0 * Math.Pow(k, 3.0) / Math.Pow(kw, 2.0);
            double alpha3 = 5.0 * (1.0 / k + 1.0 / kw);
            double alpha4 = 5.0 * Math.Pow(kw, 3.0) / Math.Pow(k, 2.0);
            double alpha5 = 1 - k;

            double A1 = Math.Pow(MMax, 2.0) + alpha1 * Math.Pow(M1, 2.0) + alpha2 * Math.Pow(M2, 2.0) + alpha3 * Math.Pow(M3, 2.0) + alpha4 * Math.Pow(M4, 2.0) + alpha5 * Math.Pow(M5, 2.0);
            A1 = A1 / ((1.0 + alpha1 + alpha2 + alpha3 + alpha4 + alpha5) * Math.Pow(MMax,2.0));
            double A2 = Math.Abs((M1 + 2.0 * M2 + 3.0 * M3 + 2.0 * M4 + M5)/(9.0*MMax));
            double C1 = (Math.Sqrt(Math.Sqrt(keq) * A1 + Math.Pow((1.0 - Math.Sqrt(keq))/2.0 * A2, 2.0)) + (1 - Math.Sqrt(keq)) / 2.0 * A2) / A1;
            return C1;
        }
        #endregion

        #region Report
        public void CreateReport()
        {
            object oMissing = System.Reflection.Missing.Value;
            object oEndOfDoc = "\\endofdoc"; /* \endofdoc is a predefined bookmark */

            //Start Word and create a new document.
            Word._Application oWord;
            oWord = new Word.Application();
            oWord.Visible = true;
            _wordDocument = oWord.Documents.Add(ref oMissing, ref oMissing, ref oMissing, ref oMissing);

            _createReport = true;
            _wordDocument.OMathJc = Word.WdOMathJc.wdOMathJcLeft;

            CheckResistance();

            CheckBuckling(_L, _betay, _betaz, _betaLT, _supportConditiony, _loadConditiony, _psiy, _supportConditionz, _loadConditionz, _psiz);

            _wordDocument.OMaths.BuildUp();
        }

        private void NewParagraph(string text)
        {
            if (_createReport)
            {
                object oMissing = System.Reflection.Missing.Value;
                Word.Paragraph oPara1;
                oPara1 = _wordDocument.Content.Paragraphs.Add(ref oMissing);
                oPara1.Range.Text = text;
                /*oPara1.Range.Font.Bold = 1;
                oPara1.Format.SpaceAfter = 24;    //24 pt spacing after paragraph.*/
                oPara1.Range.InsertParagraphAfter();
            }
        }

        private void NewFormula(string text)
        {
            if (_createReport)
            {
                object oMissing = System.Reflection.Missing.Value;
                Word.Paragraph oPara1;
                oPara1 = _wordDocument.Content.Paragraphs.Add(ref oMissing);
                oPara1.Range.Text = text;
                /*oPara1.Range.Font.Bold = 1;
                oPara1.Format.SpaceAfter = 24;    //24 pt spacing after paragraph.*/
                _wordDocument.OMaths.Add(oPara1.Range);
                
                oPara1.Range.InsertParagraphAfter();
            }
        }
        #endregion
    }
}
