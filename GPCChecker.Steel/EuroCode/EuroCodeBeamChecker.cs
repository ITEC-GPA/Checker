using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Geometry;
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
        protected Annex _annex;

        protected Section _sec;
        protected int _classificationSection; //for the current forces

        protected double _L;
      
        protected double? _psiy; //MEd(End 2) = psi * MEd(End 1)
        protected double? _psiz;
        protected LoadCondition _loadConditiony;
        protected SupportCondition _supportConditiony;
        protected LoadCondition _loadConditionz;
        protected SupportCondition _supportConditionz;

        protected double _betay;
        protected double _betaz;
        protected double _betaLT;
        protected double _L0y;
        protected double _L0z;
        protected double _L0LT;

        protected bool _method1AnnexA = true;
        protected bool _useEquation_6_57 = false; //EN1993-1-1

        protected double _Aeff;
        protected double _Weffy;
        protected double _J2eff;
        protected double _Weffz;
        protected double _J1eff;
        protected Point2d _deltaG;
        protected Point2d _centroidEff;

        protected double _NEd;
        protected double _VEd1;
        protected double _VEd2;
        protected double _MEd1;
        protected double _MEd2;
        protected double _TEd;

        protected double _NRd;
        protected double _VRdy;
        protected double _VRdz;
        protected double _MRdy;
        protected double _MRdz;
        protected double _TRd;

        protected double _Ncry;
        protected double _Ncrz;
        protected double _NcrTorsional;
        protected double _NcrFlexuralTorsional;
        protected double _McrLateralTorsional;

        protected double _alphay;
        protected double _alphaz;
        protected double _alphaLT;
        protected double _alphaT;

        protected double _lambday;
        protected double _lambdaz;
        protected double _lambdaLT;
        protected double _lambdaT;
        
        protected double _Phiy;
        protected double _Phiz;
        protected double _PhiLT;
        protected double _PhiT;

        protected double _Chiy;
        protected double _Chiz;
        protected double _ChiLT;
        protected double _ChiT;

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

        protected double _NbRdy;
        protected double _NbRdz;
        protected double _NbRdT;

        protected double _MbRdy;
        protected double _MbRdz;

        protected double _kyy;
        protected double _kyz;
        protected double _kzy;
        protected double _kzz;        
        #endregion

        #region Properties
        public double NRd => _NRd;
        public double VRdy => _VRdy;
        public double VRdz => _VRdz;
        public double MRdy => _MRdy;
        public double MRdz => _MRdz;
        public double TRd => _TRd;

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

        public EuroCodeBeamChecker(Section sect, double NEd, double V1Ed, double V2Ed, double M1Ed, double M2Ed, double TEd, Annex annex)
        {
            _sec = sect;
            _annex = annex;

            _NEd = NEd;
            _VEd1 = V1Ed;
            _VEd2 = V2Ed;
            _MEd1 = M1Ed;
            _MEd2 = M2Ed;
            _TEd = TEd;

            CheckResistance();
        }

        public void CheckResistance() {
            
            #region classification
            double minSigma = _sec.MinSigma(_NEd, _MEd2, _MEd1);

            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double epsilon = Math.Sqrt(235.0 / fy);

            if (minSigma < 0)
            {
                Type typeShape = _sec.GetType();
                if (typeShape == typeof(SectionCHS))
                {
                    SectionCHS sec = (SectionCHS)_sec;
                    double D = sec.D;
                    double t = sec.T;
                    if (D / t <= 50.0 * epsilon * epsilon)
                    {
                        _classificationSection = 1;
                    }
                    else if (D / t <= 70.0 * epsilon * epsilon)
                    {
                        _classificationSection = 2;
                    }
                    else if (D / t <= 90.0 * epsilon * epsilon)
                    {
                        _classificationSection = 3;
                    }
                    else
                    {
                        _classificationSection = 4;
                        throw new Exception("CHS class 4 not supported");
                    }
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    SectionRHS sec = (SectionRHS)_sec;

                    #region ClassificationAxialBendingStrongAxis
                    if (Math.Abs(_MEd2) > 0 || Math.Abs(_NEd) > 0)
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
                    if (Math.Abs(_MEd1) > 0)
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
                    #endregion
                } else if (typeShape == typeof(SectionH)) {
                    SectionH sec = (SectionH)_sec;
                    double cTWeb = sec.HeightWeb / sec.ThicknessWeb;
                    double cTFlangeTop = (sec.LenghtTopFlange / 2.0 - sec.ThicknessWeb / 2.0) / sec.ThicknessTopFlange;
                    double cTFlangeBottom = (sec.LenghtBottomFlange / 2.0 - sec.ThicknessWeb / 2.0) / sec.ThicknessBottomFlange;

                    if (sec.IsDoubleSymmetric)
                    {
                        #region AxialAndBendingStrongDirection
                        if (Math.Abs(_MEd2) > 0 || Math.Abs(_NEd) > 0)
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
                        if (Math.Abs(_MEd1) > 0)
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
                } if (typeShape == typeof(SectionC)) {
                    SectionC sec = (SectionC)_sec;
                    double cTWeb = sec.Hw / sec.Tw;
                    double cTFlangeTop = (sec.LTop - sec.Tw) / sec.ThicknessTop;
                    double cTFlangeBottom = (sec.LBottom - sec.Tw) / sec.ThicknessBottom;

                    //classification only for Compression.
                    //Other detailed calculation should be found and implemented
                    _classificationSection = Math.Max(_classificationSection, GetClassCompressedInnerPlate(cTWeb, epsilon));
                    _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeTop, epsilon));
                    _classificationSection = Math.Max(_classificationSection, GetClassCompressedOuterPlate(cTFlangeBottom, epsilon));
                }
                else
                {
                    throw new Exception("Classification of this kind of section not yet implemented");
                }
            } else
            {
                _classificationSection = 1;
            }

            if (_classificationSection == 4)
            {
                Class4Section secCL4 = new Class4Section(_sec);

                secCL4.Calc(0, Math.Sign(_MEd2) * 1e6, 0); //indipendent from the value
                _Weffy = secCL4.Weff2;
                _J2eff = secCL4.J2eff;

                secCL4.Calc(0, 0, Math.Sign(_MEd1) * 1e6); //indipendent from the value
                _Weffz = secCL4.Weff1;
                _J1eff = secCL4.J1eff;

                secCL4.Calc(-1000, 0, 0); //indipendent from the value
                _Aeff = secCL4.Aeff;

                _deltaG = _sec.Centroid - secCL4.CentroidEff;
                _centroidEff = secCL4.CentroidEff;
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

                #region shear
                GetVRdTRd(out _VRdy, out _VRdz, out _TRd);
                WRShear1 = Math.Abs(_VEd1) / _VRdz;
                WRShear2 = Math.Abs(_VEd2) / _VRdy;
                WRTorsion = Math.Abs(_TEd) / _TRd;

                WRMax = Math.Max(WRShear1, WRAxial);
                WRMax = Math.Max(WRShear2, WRMax);
                WRMax = Math.Max(WRTorsion, WRMax);
                #endregion

                #region bending
                GetMRd(out _MRdy, out _MRdz);
                WRBending1 = _MEd1 / _MRdz;
                WRBending2 = _MEd2 / _MRdy;

                WRMax = Math.Max(WRBending1, WRMax);
                WRMax = Math.Max(WRBending2, WRMax);

                WRCombined = GetWrCombined();
                WRMax = Math.Max(WRCombined, WRMax);
                #endregion
            }
            #endregion
        }

        public void CheckBuckling(double L, double betay, double betaz, double betaLT, SupportCondition supportConditiony, LoadCondition loadConditiony, double? psiy, SupportCondition supportConditionz, LoadCondition loadConditionz, double? psiz) {
            
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

                if (_sec.MinSigma(_NEd, _MEd2, _MEd1) < 0.0)
                {
                    if (_NEd < 0)
                    {
                        NEd = -_NEd;
                    } else
                    {
                        NEd = 0;
                    }
                    double MEd1 = Math.Abs(_MEd1);
                    double MEd2 = Math.Abs(_MEd2);

                    double E = _sec.Material.E;
                    double G = E / (2.0 * (1.0 + _sec.Material.Ni));

                    _L0y = _betay * _L;
                    _L0z = _betaz * _L;
                    _L0LT = _betaLT * _L;

                    if (_classificationSection < 4)
                    {
                        _Ncry = GetNcrEuler(E, _sec.J22, _L0y);
                        _Ncrz = GetNcrEuler(E, _sec.J11, _L0z);

                        _lambday = GetLambdaSegn(_sec.Area, fy, _Ncry);
                        _lambdaz = GetLambdaSegn(_sec.Area, fy, _Ncrz);
                    } else
                    {
                        _Ncry = GetNcrEuler(E, _J2eff, _L0y);
                        _Ncrz = GetNcrEuler(E, _J1eff, _L0z);

                        _lambday = GetLambdaSegn(_Aeff, fy, _Ncry);
                        _lambdaz = GetLambdaSegn(_Aeff, fy, _Ncrz);
                    }

                    GetImperfectionFactor(out _alphay, out _alphaz);

                    _Phiy = GetPhi(_alphay, _lambday);
                    _Phiz = GetPhi(_alphaz, _lambdaz);

                    _Chiy = GetChi(_Phiy, _lambday);
                    _Chiz = GetChi(_Phiz, _lambdaz);

                    if (_classificationSection < 4)
                    {
                        _NbRdy = _Chiy * _sec.Area * fy / _annex.Gm1;
                        _NbRdz = _Chiz * _sec.Area * fy / _annex.Gm1;
                    } else
                    {
                        _NbRdy = _Chiy * _Aeff * fy / _annex.Gm1;
                        _NbRdz = _Chiz * _Aeff * fy / _annex.Gm1;
                    }
                    
                    double iy = _sec.InertiaRadius1;
                    double iz = _sec.InertiaRadius2;

                    Point2d shearCenterToCentroid = _sec.Centroid - _sec.ShearCenter;
                    _NcrTorsional = GetNcrT(iy, iz, shearCenterToCentroid.Y, shearCenterToCentroid.X, E, G, _sec.Jt, _sec.Jw, _L0LT);
                    _NcrFlexuralTorsional = GetNcrTF(iy, iz, shearCenterToCentroid.Y, _Ncry, _Ncrz, _NcrTorsional);

                    if (_classificationSection < 4)
                    {
                        _lambdaT = GetLambdaSegn(_sec.Area, fy, Math.Min(_NcrTorsional, _NcrFlexuralTorsional));
                    }
                    else
                    {
                        _lambdaT = GetLambdaSegn(_Aeff, fy, Math.Min(_NcrTorsional, _NcrFlexuralTorsional));
                    }

                    _alphaT = _alphaz;
                    _PhiT = GetPhi(_alphaT, _lambdaT);
                    _ChiT = GetChi(_PhiT, _lambdaT);

                    if (_classificationSection < 4)
                    {
                        _NbRdT = _ChiT * _sec.Area * fy / _annex.Gm1;
                    } else
                    {
                        _NbRdT = _ChiT * _Aeff * fy / _annex.Gm1;
                    }

                    _McrLateralTorsional = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _sec.J11, E, G, _supportConditiony, _loadConditiony, _psiy, 1, 1);

                    if (_classificationSection < 3) {
                        _lambdaLT = GetLambdaSegn(_sec.Wpl22, fy, _McrLateralTorsional);
                    } else if (_classificationSection == 3) {
                        _lambdaLT = GetLambdaSegn(_sec.Wel22Min, fy, _McrLateralTorsional);
                    } else
                    {
                        _lambdaLT = GetLambdaSegn(_Weffy, fy, _McrLateralTorsional);
                    }

                    _alphaLT = GetImperfectionFactorLT(_useEquation_6_57);
                    double kc = Getkc(_lambdaLT, _supportConditiony, _loadConditiony, _psiy);

                    if (_sec.GetType() == typeof(SectionH) && _useEquation_6_57 == true)
                    {
                        _PhiLT = GetPhi(_alphaLT, _lambdaLT, _annex.Beta, _annex.LambdaLT0);
                        double factorF = Math.Min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * Math.Pow(_lambdaLT - 0.8, 2.0)));
                        _ChiLT = GetChiLTmod(_PhiLT, _lambdaLT, _annex.Beta, factorF);
                    } else
                    {
                        _PhiLT = GetPhi(_alphaLT, _lambdaLT);
                        _ChiLT = GetChi(_PhiLT, _lambdaLT);
                    }

                    if (_classificationSection < 3)
                    {
                        _MbRdy = _ChiLT * _sec.Wpl22 * fy / _annex.Gm1;
                        _MbRdz = _sec.Wpl11 * fy / _annex.Gm1;
                    }
                    else if (_classificationSection == 3)
                    {
                        _MbRdy = _ChiLT * _sec.Wel22Min * fy / _annex.Gm1;
                        _MbRdz = _sec.Wel11Min * fy / _annex.Gm1;
                    }
                    else
                    {
                        _MbRdy = _ChiLT * _Weffy * fy / _annex.Gm1;
                        _MbRdz = _Weffz * fy / _annex.Gm1;
                    }

                    if (_method1AnnexA)
                    {
                        //Annex A
                        double? MEdyMax = null;
                        double? deflectiony = null;
                        double? MEdzMax = null;
                        double? deflectionz = null;

                        _cmy0 = GetCMi0(_loadConditiony, _supportConditiony, _psiy, MEdyMax, deflectiony, NEd, _Ncry);
                        _cmz0 = GetCMi0(_loadConditionz, _supportConditionz, _psiz, MEdzMax, deflectionz, NEd, _Ncrz);

                        _muy = GetMu(NEd, _Ncry, _Chiy);
                        _muz = GetMu(NEd, _Ncrz, _Chiz);

                        if (_classificationSection < 3) //Rules for member stability in en 1993-1-1 pg. 113
                        {
                            _wy = Math.Min(_sec.Wpl22 / _sec.Wel22Min, 1.5);
                            _wz = Math.Min(_sec.Wpl11 / _sec.Wel11Min, 1.5);
                        } else
                        {
                            _wy = 1.0; //e con Weff?
                            _wz = 1.0; //e con Weff?
                        }

                        double lambdaMax = Math.Max(_lambday, _lambdaz);
                        double mCrLT0;
                        if (_classificationSection < 4)
                        {
                            mCrLT0 = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _sec.J11, E, G, _supportConditiony, LoadCondition.NotDirectlyLoaded, 1.0, 1, 1);
                        } else
                        {
                            mCrLT0 = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _J1eff, E, G, _supportConditiony, LoadCondition.NotDirectlyLoaded, 1.0, 1, 1); //Jw eff?
                        }

                        double lambda0;
                        if (_classificationSection < 3)
                        {
                            lambda0 = GetLambdaSegn(_sec.Wpl22, fy, mCrLT0);
                        } else if (_classificationSection == 3)
                        {
                            lambda0 = GetLambdaSegn(_sec.Wel22Min, fy, mCrLT0);
                        } else
                        {
                            lambda0 = GetLambdaSegn(_Weffy, fy, mCrLT0); //or Wel?
                        } 

                        if (_classificationSection < 4)
                        {
                            _epsilony = MEd2 / Math.Max(NEd,1E-3) * _sec.Area / _sec.Wel22Min;
                        } else
                        {
                            _epsilony = MEd2 / Math.Max(NEd, 1E-3) * _Aeff / _Weffy;
                        }
         
                        _aLT = Math.Max(1.0 - _sec.Jt / _sec.J22,0);
                                                   
                        double C1 = Math.Pow(kc, -2.0);
                        double lambda0Limit = 0.2 * Math.Pow(C1, 0.5) * Math.Pow((1.0 - NEd / _Ncrz) * (1.0 - NEd / _NcrFlexuralTorsional), 0.25);

                        if (lambda0 <= lambda0Limit)
                        {
                            _cmy = _cmy0;
                            _cmz = _cmz0;
                            _cmLT = 1.0;
                        } else
                        {
                            _cmy = _cmy0 + (1.0 - _cmy0) * Math.Sqrt(_epsilony) * _aLT / (1.0 + Math.Sqrt(_epsilony) * _aLT);
                            _cmz = _cmz0;
                            _cmLT = Math.Max(_cmy*_cmy * _aLT / (Math.Sqrt(1.0-NEd/_Ncrz) * (1.0 - NEd/_NcrTorsional)),1.0);
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
                            mplzRd = _sec.Wpl11 * fy / _annex.Gm0;
                        } else if (_classificationSection == 3)
                        {
                            mplyRd = _sec.Wel22Min * fy / _annex.Gm0;
                            mplzRd = _sec.Wel11Min * fy / _annex.Gm0;
                        } else
                        {
                            mplyRd = _Weffy * fy / _annex.Gm0;
                            mplzRd = _Weffz * fy / _annex.Gm0;
                        }                        

                        _bLT = 0.5 * _aLT * lambda0 * lambda0 * MEd2 * MEd1 / (_ChiLT * mplyRd * mplzRd);
                        _cLT = 10.0 * _aLT * lambda0 * lambda0 * MEd2 / ((5.0 + Math.Pow(_lambdaz,4.0)) * _cmy * _ChiLT * mplyRd);
                        _dLT = 2.0 * _aLT * lambda0 * MEd2 * MEd1 / ((0.1 + Math.Pow(_lambdaz,4.0)) * _cmy * _ChiLT * mplyRd * _cmz * mplzRd);
                        _eLT = 1.7 * _aLT * lambda0 * MEd2 / ((0.1 + Math.Pow(_lambdaz, 4.0)) * _cmy * _ChiLT * mplyRd);

                        double npl = NEd / (fy * _sec.Area / _annex.Gm0);
                        _cyy = Math.Max(1.0 + (_wy - 1.0) * ((2.0 - 1.6/_wy * _cmy * _cmy * lambdaMax - 1.6 / _wy * _cmy * _cmy * lambdaMax * lambdaMax) * npl - _bLT), _sec.Wel22Min / _sec.Wpl22);
                        _cyz = Math.Max(1.0 + (_wz - 1.0) * ((2.0 - 14.0 * _cmz * _cmz * lambdaMax * lambdaMax / Math.Pow(_wz,5.0)) * npl - _cLT), 0.6 * Math.Sqrt(_wz / _wy) * _sec.Wel11Min / _sec.Wpl11);
                        _czy = Math.Max(1.0 + (_wy - 1.0) * ((2.0 - 14.0 * _cmy * _cmy * lambdaMax * lambdaMax / Math.Pow(_wy, 5.0)) * npl - _dLT),0.6 * Math.Sqrt(_wy / _wz) * _sec.Wel22Min / _sec.Wpl22);
                        _czz = Math.Max(1.0 + (_wz - 1) * (2.0 - 1.6 / _wz * _cmz * _cmz * lambdaMax - 1.6 / _wz * _cmz * _cmz * lambdaMax * lambdaMax - _eLT) * npl, _sec.Wel11Min / _sec.Wpl11); //RIGHT VERSION
                        /* WRONG - TO BE COMMENTED!! -  ONLY FOR COMPARISON WITH SAP */
                        //_czz = Math.Max(1.0 + (_wz - 1) * ((2.0 - 1.6 / _wz * _cmz * _cmz * lambdaMax - 1.6 / _wz * _cmz * _cmz * lambdaMax * lambdaMax) * npl - _eLT), _sec.Wel11Min / _sec.Wpl11); //SAP200 WRONG OLD VERSION
                        /* STOP WRONG */
                        if (_classificationSection <= 2)
                        {
                            _kyy = _cmy * _cmLT * _muy / (1.0 - NEd / _Ncry) * 1.0 / _cyy;
                            _kyz = _cmz * _muy/(1.0 - NEd/_Ncrz) * 1.0 / _cyz * 0.6 * Math.Sqrt(_wz/_wy);
                            _kzy = _cmy * _cmLT * _muz/(1.0 - NEd/_Ncry) * 1.0 / _czy * 0.6 * Math.Sqrt(_wy/_wz);
                            _kzz = _cmz * _muz / (1.0 - NEd/_Ncrz) * 1.0 / _czz;
                        } else
                        {
                            _kyy = _cmy * _cmLT * _muy / (1.0 - NEd / _Ncry);
                            _kyz = _cmz * _muy / (1.0 - NEd / _Ncrz);
                            _kzy = _cmy * _cmLT * _muz / (1.0 - NEd/_Ncry);
                            _kzz = _cmz * _muz / (1.0 - NEd / _Ncrz);
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
                        myrk = _sec.Wpl22 * fy;
                        mzrk = _sec.Wpl11 * fy;
                        deltaMy = 0;
                        deltaMz = 0;
                    } else if (_classificationSection == 3)
                    {
                        nrk = _sec.Area * fy;
                        myrk = _sec.Wel22Min * fy;
                        mzrk = _sec.Wel11Min * fy;
                        deltaMy = 0;
                        deltaMz = 0;
                    } else
                    {
                        nrk = _Aeff * fy;
                        myrk = _Weffy * fy;
                        mzrk = _Weffz * fy;
                        deltaMy = NEd * _deltaG.Y; //check segno
                        deltaMz = NEd * _deltaG.X; //check segno
                    }
                    WRBuckling1 = NEd / (_Chiy * nrk / _annex.Gm1) + _kyy * Math.Abs(MEd2 + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kyz * Math.Abs(MEd1 + deltaMz) / (mzrk / _annex.Gm1);
                    WRBuckling2 = NEd / (_Chiz * nrk / _annex.Gm1) + _kzy * Math.Abs(MEd2 + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kzz * Math.Abs(MEd1 + deltaMz) / (mzrk / _annex.Gm1);
                    WRBuckling3 = NEd / _NbRdT;

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

        #region Classification
        protected int GetClassCompressedInnerPlate(double ctRatio, double epsilon)
        {
            if (ctRatio <= 33.0 * epsilon)
            {
                return 1;
            }
            else if (ctRatio <= 38.0 * epsilon)
            {
                return 2;
            }
            else if (ctRatio <= 42.0 * epsilon)
            {
                return 3;
            }
            else
            {
                return 4;
            }
        }

        protected int GetClassInnerPlate(double ctRatio, double epsilon, double alpha, double psi)
        {
            if (alpha > 0.5 && alpha < 1)
            {
                if (ctRatio <= 396.0 * epsilon / (13.0 * alpha - 1.0))
                {
                    return 1;
                }
                else if (ctRatio <= 456.0 * epsilon / (13.0 * alpha - 1.0))
                {
                    return 2;
                } else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                        {
                            return 3;
                        }
                        else
                        {
                            return 4;
                        }
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                        {
                            return 3;
                        }
                        else
                        {
                            return 4;
                        }
                    }
                    else
                    {
                        return 4;
                        throw new Exception("classification");
                    }
                }
            }
            else if (alpha <= 0.5 && alpha > 0)
            {
                if (ctRatio <= 36.0 * epsilon / alpha)
                {
                    return 1;
                }
                else if (ctRatio <= 41.5 * epsilon / alpha)
                {
                    return 2;
                } else
                {
                    if (psi > -1)
                    {
                        if (ctRatio <= 42.0 * epsilon / (0.67 + 0.33 * psi))
                        {
                            return 3;
                        }
                        else
                        {
                            return 4;
                        }
                    }
                    else if (psi <= -1)
                    {
                        if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                        {
                            return 3;
                        }
                        else
                        {
                            return 4;
                        }
                    }
                    else
                    {
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
                        return 3;
                    }
                    else
                    {
                        return 4;
                    }
                }
                else if (psi <= -1)
                {
                    if (ctRatio <= 62.0 * epsilon * (1 - psi) * Math.Sqrt(-psi))
                    {
                        return 3;
                    }
                    else
                    {
                        return 4;
                    }
                }
                else
                {
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
                return 1;
            }
            else if (ctRatio <= 10.0 * epsilon)
            {
                return 2;
            }
            else if (ctRatio <= 14.0 * epsilon)
            {
                return 3;
            }
            else
            {
                return 4;
            }
        }
        #endregion

        #region ResistanceFunctions
        protected double GetNtRd(double Anet)
        {
            double A = _sec.Area;
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double fu = ((SteelMaterial)_sec.Material).Fu;
            double gm0 = _annex.Gm0;
            double gm2 = _annex.Gm2;

            double NtRd = Math.Min(A * fy / gm0, 0.9 * Anet * fu / gm2);
            return NtRd;
        }
        protected double GetNcRd()
        {
            double A;
            if (_classificationSection == 4)
            {
                A = _Aeff;
            }
            else
            {
                A = _sec.Area;
            }
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double gm0 = _annex.Gm0;

            double NcRd = A * fy / gm0;
            return NcRd;
        }

        protected void GetVRdTRd(out double VRdy, out double VRdz, out double TRd)
        {
            double eta = 1.0;
            double Avy, Avz;
            Type typeShape = _sec.GetType();
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double gm0 = _annex.Gm0;

            #region Av
            if (typeShape == typeof(SectionRectangular))
            {
                Avy = _sec.Area;
                Avz = Avy;
            } else if (typeShape == typeof(SectionH)) {
                SectionH sec = (SectionH)_sec;
                Avy = Math.Min(_sec.Area - sec.LenghtBottomFlange * sec.ThicknessBottomFlange - sec.LenghtTopFlange * sec.ThicknessTopFlange, eta * sec.ThicknessWeb * sec.HeightWeb);
                Avz = sec.Area - sec.ThicknessWeb * sec.HeightWeb;
            } else if (typeShape == typeof(SectionCHS))
            {
                Avy = 2.0 * _sec.Area / Math.PI;
                Avz = Avy;
            } else if (typeShape == typeof(SectionC))
            {
                SectionC sec = (SectionC)_sec;
                if (sec.LBottom == sec.LTop && sec.ThicknessBottom == sec.ThicknessTop)
                {
                    Avy = sec.Area - sec.ThicknessTop * sec.LTop - sec.ThicknessBottom * sec.LBottom;
                    Avz = sec.Area - sec.Hw * sec.Tw;
                } else
                {
                    throw new Exception("gestione angolo C not yet supported");
                }
            } else if (typeShape == typeof(SectionT))
            {
                SectionT sec = (SectionT)_sec;
                Avy = sec.Tw * (sec.H - sec.Tf / 2.0);
                Avz = sec.Tf * sec.H;
            } else if (typeShape == typeof(SectionRHS))
            {
                SectionRHS sec = (SectionRHS)_sec;
                Avy = eta * sec.H * sec.ThicknessWeb * 2.0;
                Avz = sec.Area - 2.0 * sec.Hw * sec.ThicknessWeb;
            }
            else
            {
                Avy = 0;
                Avz = 0;
                throw new Exception("Section not supported yet");
            }
             #endregion

            double VplRdTy = 0.0;
            double VplRdTz = 0.0;

            double VplRdy = Avy * fy / gm0 / Math.Pow(3.0, 0.5);
            double VplRdz = Avz * fy / gm0 / Math.Pow(3.0, 0.5);

            #region calculationTauTandTauW
            if (_classificationSection == 4 && Math.Abs(_TEd) > 0)
            {
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

                TRd = fy / Math.Pow(3.0, 0.5) * Wt;
            } else if (typeShape == typeof(SectionRHS))
            {
                //Bredt - Plastic Theory
                SectionRHS sec = (SectionRHS)_sec;
                double Hmed = sec.H - sec.ThicknessFlange;
                double Bmed = sec.B - sec.ThicknessWeb;
                double Omega = Hmed * Bmed;
                double denom = 2.0 * Omega * Math.Min(sec.ThicknessWeb, sec.ThicknessFlange);
                tauT = Math.Abs(_TEd) / denom;

                TRd = fy / Math.Pow(3.0, 0.5) * denom;
                
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

                TRd = fy / Math.Pow(3.0, 0.5) * denominator;

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

                TRd = fy / Math.Pow(3.0, 0.5) * denominator;
            } else if (typeShape == typeof(SectionL))
            {
                SectionL sec = (SectionL)_sec;
                double tmax = Math.Max(sec.T1, sec.T2);

                double L1 = sec.L1;
                double a1 = sec.T1;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.L2;
                double a2 = sec.T2;
                denominator = denominator + L2 * Math.Pow(a2, 3.0);
                denominator = denominator / 3.0;

                tauT = 3.0 * Math.Abs(_TEd) * tmax / denominator;

                TRd = fy / Math.Pow(3.0, 0.5) * denominator;
            } else if (typeShape == typeof(SectionRectangular))
            {
                SectionRectangular sec = (SectionRectangular)_sec;
                double a = Math.Min(sec.B, sec.H);
                double b = Math.Max(sec.B, sec.H);
                double alpha = 3.0 + 1.8 * a / b;
                tauT = alpha * Math.Abs(_TEd) / (b * Math.Pow(a, 2.0));

                TRd = b * Math.Pow(a, 2.0) / alpha * fy / (Math.Pow(3.0, 0.5));
            } else
            {
                tauT = 0;
                TRd = 0;
                new Exception("Section not yet supported");
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
                    VplRdTz = Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) * VplRdz;
                } else if (typeShape == typeof(SectionC)) {
                    VplRdTy = (Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) - tau_w / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    VplRdTz = (Math.Pow(1.0 - Math.Abs(tauT) / (1.25 * (fy / Math.Pow(3.0, 0.5) / gm0)), 0.5) - tau_w / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                } else if (typeShape == typeof(SectionCHS)) { 
                    VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                    VplRdTz = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                } else if (typeShape == typeof(SectionRHS)) { 
                        VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdy;
                } else if (typeShape == typeof(SectionT)) { 
                        VplRdTz = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
                } else {
                    VRdy = VplRdTy;
                    VRdz = VplRdTz;
                    throw new Exception("section not yet supported");
                }
                VRdy = VplRdTy;
                VRdz = VplRdTz;
            }
        }

        protected void GetMRd(out double MRdNy, out double MRdNz)
        {
            double gm0 = _annex.Gm0;
            double fy = ((SteelMaterial)_sec.Material).Fyk;

            double NEd = _NEd;
            double VEdy = _VEd2;
            double VEdz = _VEd1;

            double A = _sec.Area;
            double Wy;
            double Wz;

            if (_classificationSection < 3)
            {
                Wy = _sec.Wpl22;
                Wz = _sec.Wpl11;
            } else if (_classificationSection == 3)
            {
                Wy = _sec.Wel22Min;
                Wz = _sec.Wel11Min;
            } else
            {
                Wy = _Weffy;
                Wz = _Weffz;
            }
            Type typeShape = _sec.GetType();

            double rhoy = Math.Min(Math.Pow(2.0 * Math.Abs(VEdy) / _VRdy - 1.0, 2.0), 1.0);
            if (VEdy <= 0.5 * _VRdy)
            {
                rhoy = 0.0;
            }
            double rhoz = Math.Min(Math.Pow(2.0 * Math.Abs(VEdz) / _VRdz - 1.0, 2.0), 1.0);
            if (VEdz <= 0.5 * _VRdz)
            {
                rhoz = 0.0;
            }
            
            double Mrdy = Wy * (1.0 - rhoy) * fy / gm0;
            double Mrdz = Wz * (1.0 - rhoz) * fy / gm0;

            if (_classificationSection < 3)
            {
                MRdNy = 0.0;
                MRdNz = 0.0;
                double NplRd = fy * A / gm0;
                double n = Math.Abs(NEd) / NplRd;

                if (typeShape == typeof(SectionRectangular))
                {
                    MRdNy = Mrdy * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
                    MRdNz = Mrdz * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
                }
                else if (typeShape == typeof(SectionH))
                {
                    SectionH secH = (SectionH)_sec;
                    if (secH.LenghtBottomFlange == secH.LenghtTopFlange && secH.ThicknessTopFlange == secH.ThicknessBottomFlange)
                    {
                        double a = Math.Min((A - 2.0 * secH.LenghtTopFlange * secH.ThicknessTopFlange) / A, 0.5);
                        MRdNy = Math.Min(Mrdy * (1.0 - n) / (1.0 - 0.5 * a), Mrdy);
                        MRdNz = 0.0;
                        if (n <= a)
                        {
                            MRdNz = Mrdz;
                        }
                        else
                        {
                            MRdNy = Mrdz * (1.0 - Math.Pow((n - a) / (1.0 - a), 2.0));
                        }
                    }
                    else
                    {
                        MRdNy = Mrdy;
                        MRdNz = Mrdz;
                    }
                }
                else if (typeShape == typeof(SectionCHS))
                {
                    MRdNy = Mrdy * (1.0 - Math.Pow(n, 1.7));
                    MRdNz = Mrdz * (1.0 - Math.Pow(n, 1.7));
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    SectionRHS secRHS = (SectionRHS)_sec;
                    double b = secRHS.B;
                    double h = secRHS.H;
                    double thk_flange = secRHS.ThicknessFlange;
                    double thk_web = secRHS.ThicknessWeb;

                    double aw = Math.Min((A - 2.0 * b * thk_flange) / A, 0.5);
                    double af = Math.Min((A - 2.0 * h * thk_web) / A, 0.5);

                    MRdNy = Math.Min(Mrdy * (1.0 - n) / (1 - 0.5 * aw), Mrdy);

                    MRdNz = Math.Min(Mrdz * (1.0 - n) / (1 - 0.5 * af), Mrdz);
                }
                else
                {
                    MRdNy = Mrdy;
                    MRdNz = Mrdz;
                    //throw new Exception("Section not yet supported");
                }
            } else
            {
                MRdNy = Mrdy;
                MRdNz = Mrdz;
            }
        }

        protected double GetWrCombined()
        {
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

                        beta = 5.0 * n;
                        if (beta < 1.0)
                        {
                            beta = 1.0;
                        }
                    } else
                    {
                        return Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
                    }
                }
                else if (typeShape == typeof(SectionCHS))
                {
                    alpha = 2.0;
                    beta = 2.0;
                }
                else if (typeShape == typeof(SectionRHS))
                {
                    alpha = Math.Min(1.66 / (1.0 - 1.13 * Math.Pow(n, 2.0)), 6.0);
                    beta = alpha;
                }
                else
                {
                    return Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
                }

                return Math.Pow(Math.Abs(_MEd2) / _MRdy, alpha) + Math.Pow(Math.Abs(_MEd1) / _MRdz, beta);
            }
            else if (_classificationSection == 3)
            {
                return Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
            }
            else
            {
                return  Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2 + _NEd * _deltaG.Y) / _MRdy + Math.Abs(_MEd1 + _NEd * _deltaG.X) / _MRdz; //attention to sign
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
            Chi = Math.Min(Chi, 1.0);
            Chi = Math.Min(Chi, 1.0 / Math.Pow(lambda_segn, 2.0));

            double ChiLTMod = Math.Min(Chi/f, 1.0);
            ChiLTMod = Math.Min(ChiLTMod, 1.0 / Math.Pow(lambda_segn, 2.0));
            return ChiLTMod;
        }

        protected double GetNcrT(double iy, double iz, double y0, double z0, double E, double G, double It, double Jw, double _L0LT)  //EN 1993-1-3 eq 6.33a
        {
            /*
             * y0 and z0 = coordinates of shear center in respect of the centroid gross section
             */
            double i0 = Math.Pow(Math.Pow(iy, 2.0) + Math.Pow(iz, 2.0) + Math.Pow(y0, 2.0) + Math.Pow(z0, 2.0), 0.5);
            double Ncr_T = 1.0 / Math.Pow(i0, 2.0) * (G * It + Math.Pow(Math.PI, 2.0) * E * Jw / Math.Pow(_L0LT, 2.0));
            return Ncr_T;
        }

        protected double GetNcrTF(double iy, double iz, double y0, double Ncr_y, double Ncr_z, double Ncr_T) //EN 1993-1-3 eq 6.35
        {
            /*
             * y0 coordinates of shear center in respect of the centroid gross section
             */
            if (_sec.IsSymmetricAlongYLocalAxis)
            {
                double i0 = Math.Pow(Math.Pow(iy, 2.0) + Math.Pow(iz, 2.0) + Math.Pow(y0, 2.0), 0.5);
                double beta = 1.0 - Math.Pow(y0 / i0, 2.0);

                double Ncr_TF = Math.Min(Ncr_z, Ncr_y / (2.0 * beta) * (1.0 + Ncr_T / Ncr_y - Math.Pow(Math.Pow(1.0 - Ncr_T / Ncr_y, 2.0) + 4.0 * Math.Pow(y0 / i0, 2.0) * Ncr_T / Ncr_y, 0.5)));
                Ncr_TF = Math.Min(Ncr_TF, Ncr_y);
                return Ncr_TF;
            } else
            {
                throw new Exception("Cannot calc NcrTF");
            }
        }

        protected double GetMcrLT(double L, double Jt, double Jw, double Jz,   double E, double G, SupportCondition supportCondition, LoadCondition loadCondition, double? psi, double k = 1.0, double kw = 1.0)
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
            double zg; //coordinate of point of application vs coordinate of shear center
            double zj; //zs (shear center) - 0.5 integral(y^2+z^2) * z / Jy dA
            Type typeSection = _sec.GetType();
            if (typeSection == typeof(SectionCHS))
            {
                SectionCHS sec = (SectionCHS)_sec;
                zg = sec.D - sec.ShearCenter.Y;
            } else if (typeSection == typeof(SectionRHS))
            {
                SectionRHS sec = (SectionRHS)_sec;
                zg = sec.H - sec.ShearCenter.Y;
            }
            else if (typeSection == typeof(SectionH))
            {
                SectionH sec = (SectionH)_sec;
                zg = sec.H - sec.ShearCenter.Y;
            }
            else
            {
                throw new Exception("McrLT not yet supported for this section");
            }

            double C1 = 0;
            double C2 = 0;
            double C3 = 0;

            if (_sec.IsDoubleSymmetric)
            {
                C3 = 0.0;
                zj = 0.0;

                if (loadCondition != LoadCondition.NotDirectlyLoaded) { 
                    if (supportCondition == SupportCondition.HingesAtEnds)
                    {
                        if (loadCondition == LoadCondition.Constant)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            C1 = 1.127; 
                            C2 = 0.454;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            C1 = 1.348;
                            C2 = 0.630;
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
                            C1 = 2.578;
                            C2 = 1.554;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            //From NCCI: Elastic critical moment for lateral torsional buckling SN003a-EN-EU
                            C1 = 1.683;
                            C2 = 1.645;
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
                            C1 = Math.Min(1.77 - 1.04 * psi.Value + 0.27 * psi.Value * psi.Value, 2.6);
                        } else
                        {
                            throw new Exception("k != 1 : cannot calculate C1 for Mcr");
                        }
                        //ENV 1993-1-1:1992 (F3)
                        //C1 = Math.Min(1.88 - 1.40 * psi + 0.52 * psi * psi, 2.7);
                        C2 = 0;
                    } else
                    {
                        throw new Exception("Set the value of psi = M(x=0)/M(x=L)");
                    }
                } else
                {
                    throw new Exception("Load condition + Support not yet supported");
                }
                
            }
            else if (_sec.IsSymmetricAlongYLocalAxis)
            {
                if (typeSection == typeof(SectionH))
                {
                    SectionH sec = (SectionH)_sec;
                    double Ifc; //inertia along the weak axis of the beam of compression flange
                    double Ift; //inertia along the weak axis of the beam of tension flange
                    if (_MEd2 > 0) { //tension bottom
                        Ift = 1.0 / 12.0 * sec.ThicknessBottomFlange * Math.Pow(sec.LenghtBottomFlange,3);
                        Ifc = 1.0 / 12.0 * sec.ThicknessTopFlange * Math.Pow(sec.LenghtTopFlange, 3);
                    } else { //tension up
                        Ifc = 1.0 / 12.0 * sec.ThicknessBottomFlange * Math.Pow(sec.LenghtBottomFlange, 3);
                        Ift = 1.0 / 12.0 * sec.ThicknessTopFlange * Math.Pow(sec.LenghtTopFlange, 3);
                    }
                    //Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability
                    //pg 230
                    double psif = (Ifc - Ift) / (Ifc + Ift);
                    double hs = sec.H - sec.ThicknessBottomFlange /2.0 - sec.ThicknessTopFlange / 2.0; // distance between the shear center of the flanges
                    
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
                        //tables 63 and 64 of Book: Rules for Member Stability in EN 1993-1-1 - Background documentation and design guidelines - ECCS Techinacl Committee - Stability can be used
                        if (supportCondition == SupportCondition.EndsRestrained)
                        {
                            if (loadCondition == LoadCondition.NotDirectlyLoaded)
                            {
                                C2 = 0;
                                double interpolation(double x0, double y0, double x1, double y1, double xc) { return (y1-y0)/(x1-x0)*(xc-x1)+y1; }
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
                                            C3 = interpolation(-0.5, 1.3-1.2*psif, -0.25, 0.85, psi.Value);
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
                                            C3 = interpolation(-1.0, -psif, -0.75, 0.55-psif, psi.Value);
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("cannot calc McrLT");
                                    }
                                } else if (k == 0.5)
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
                                            C3 = interpolation(-1.0, 0.125-0.7*psif, -0.75, 0.85, psi.Value);
                                        }
                                        else
                                        {
                                            C3 = interpolation(-1.0, -0.125-0.7*psif, -0.75, 0.35 - psif, psi.Value);
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("cannot calc McrLT, psi < -1 or psi > 1!");
                                    }
                                } else
                                {
                                    throw new Exception("cannot calc McrLT, k != 1 or k != 0.5");
                                }
                            } else
                            {
                                throw new Exception("cannot calc McrLT, no literature");
                            }
                        } else if (supportCondition == SupportCondition.HingesAtEnds)
                        {
                            if (k == 1)
                            {
                                if (loadCondition == LoadCondition.Constant)
                                {
                                    C1 = 1.12;
                                    C2 = 0.45;
                                    C3 = 0.525;
                                } else if (loadCondition == LoadCondition.SingleForce)
                                {
                                    C1 = 1.35;
                                    C2 = 0.59;
                                    C3 = 0.411;
                                }
                            } else if (k == 0.5)
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
                            } else
                            {
                                throw new Exception("cannot calc McrLT, no literature");
                            }
                        } else
                        {
                            throw new Exception("cannot calc McrLT, no literature");
                        }
                    } else
                    {
                        throw new Exception("cannot calc McrLT. Section too asymmetric");
                    }
                } else {
                    throw new Exception("Section not yet supported for calculation of McrLT");
                }
            } else //NO sysmmetry
            {
                throw new Exception("Cannot calc McrLT. Any symmetry");
            }

            double McrLT = C1 * Math.Pow(Math.PI, 2.0) * E * Jz / Math.Pow(k * L, 2.0) * (Math.Pow(Math.Pow(k / kw, 2.0) * Jw / Jz + Math.Pow(k * L, 2.0) * G * Jt / (Math.Pow(Math.PI, 2.0) * E * Jz) + Math.Pow(C2 * zg - C3 * zj, 2.0), 0.5) - (C2 * zg - C3 * zj));
            return McrLT;
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
                                _alphaz = SectionBucklingCurves["a0"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["a"];
                                _alphaz = SectionBucklingCurves["b"];
                            }
                        }
                        else if (sec.ThicknessBottomFlange <= 100.0 && sec.ThicknessTopFlange <= 100.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["a"];
                                _alphaz = SectionBucklingCurves["a"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["b"];
                                _alphaz = SectionBucklingCurves["c"];
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
                                _alphaz = SectionBucklingCurves["a"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["b"];
                                _alphaz = SectionBucklingCurves["c"];
                            }
                        }
                        else //thicknessflanges > 100 mm 
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                _alphay = SectionBucklingCurves["c"];
                                _alphaz = SectionBucklingCurves["c"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                _alphay = SectionBucklingCurves["d"];
                                _alphaz = SectionBucklingCurves["d"];
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
                            _alphaz = SectionBucklingCurves["c"];
                        }
                        else //steel S235, 275, 355, 420
                        {
                            _alphay = SectionBucklingCurves["b"];
                            _alphaz = SectionBucklingCurves["c"];
                        }
                    } else
                    {
                        if (sec.Material.Name.Contains("460"))
                        {
                            _alphay = SectionBucklingCurves["c"];
                            _alphaz = SectionBucklingCurves["d"];
                        }
                        else //steel S235, 275, 355, 420
                        {
                            _alphay = SectionBucklingCurves["c"];
                            _alphaz = SectionBucklingCurves["d"];
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
            } else if (typeShape == typeof(SectionC) || typeShape == typeof(SectionT) || typeShape == typeof(SectionRectangular) || typeShape == typeof(SectionCircular))
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

        public static double C1(double k, double kw, double MMax, double M1, double M2, double M3, double M4, double M5)
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
    }
}
