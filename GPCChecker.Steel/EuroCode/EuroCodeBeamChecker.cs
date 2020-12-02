using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Geometry;

namespace GPC.Checker.Steel.EuroCode
{
    public class EuroCodeBeamChecker
    {
        #region Variables
        protected Annex _annex;

        protected Section _sec;
        protected int _classificationSection; //for the current forces

        protected double _L;
      
        protected double _psiy; //MEd(End 2) = psi * MEd(End 1)
        protected double _psiz;
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
        protected bool _useEquation_6_57 = true; //EN1993-1-1

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

        protected double _alphay;
        protected double _alphaz;
        protected double _alphaLT;
        protected double _Phiy;
        protected double _Phiz;
        protected double _PhiLT;
        protected double _Chiy;
        protected double _Chiz;
        protected double _ChiLT;

        protected double _kyy;
        protected double _kyz;
        protected double _kzy;
        protected double _kzz;        
        #endregion

        #region 
        public double NRd => _NRd;
        public double VRdy => _VRdy;
        public double VRdz => _VRdz;
        public double MRdy => _MRdy;
        public double MRdz => _MRdz;
        public double TRd => _TRd;

        public int ClassificationSection => _classificationSection;

        public double L0y => _L0y;
        public double L0z => _L0z;
        public double L0LT => _L0LT;
        public double Ncry => _Ncry;
        public double Ncrz => _Ncrz;

        public double Alphay => _alphay;
        public double Alphaz => _alphaz;
        public double AlphaLT => _alphaLT;
        public double Phiy => _Phiy;
        public double Phiz => _Phiz;
        public double PhiLT => _PhiLT;
        public double Chiy => _Chiy;
        public double Chiz => _Chiz;
        public double ChiLT => _ChiLT;

        public double Kyy => _kyy;
        public double Kyz => _kyz;
        public double Kzy => _kzy;
        public double Kzz => _kzz;

        public double WRAxial { get; }
        public double WRShear1 { get; }
        public double WRShear2 { get; }
        public double WRBending2 { get; }
        public double WRBending1 { get; }
        public double WRTorsion { get; }
        public double WRResistance { get; }
        public double WRBuckling1 { get; }
        public double WRBuckling2 { get; }
        #endregion

        protected enum LoadCondition
        {
            Constant,
            SingleForce,
            NotDirectlyLoaded
        }
        protected enum SupportCondition
        {
            FixHinge,
            Restrained,
            OneSideRestrained_OneSideHinged
        }

        public EuroCodeBeamChecker(Section sect, double NEd, double V1Ed, double V2Ed, double M1Ed, double M2Ed, double TEd, double L, double betay, double betaz, double betaLT, Annex annex)
        {
            _sec = sect;
            _annex = annex;

            _NEd = NEd;
            _VEd1 = V1Ed;
            _VEd2 = V2Ed;
            _MEd1 = M1Ed;
            _MEd2 = M2Ed;
            _TEd = TEd;

            _L = L;
            _betaLT = betaLT;
            _betay = betay;
            _betaz = betaz;

            #region classification
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double epsilon = Math.Sqrt(235.0/fy);

            Type typeShape = _sec.GetType();
            if (typeShape == typeof(SectionCHS))
            {
                SectionCHS sec = (SectionCHS)_sec;
                double D = sec.D;
                double t = sec.T;
                if ( D / t  <= 50.0 * epsilon * epsilon)
                {
                    _classificationSection = 1;
                } else if (D /t <= 70.0 * epsilon * epsilon)
                {
                    _classificationSection = 2;
                } else if (D/t <= 90.0 * epsilon * epsilon)
                {
                    _classificationSection = 3;
                } else
                {
                    _classificationSection = 4;
                }
            }
                
            #endregion

            #region resistance
            {
                #region axial
                if (NEd > 0)
                {
                    double Anet = _sec.Area;
                    _NRd = GetNtRd(Anet);
                }
                else
                {
                    _NRd = GetNcRd();
                }
                WRAxial = Math.Abs(NEd) / _NRd;
                #endregion

                #region shear
                GetVRdTRd(out _VRdy, out _VRdz, out _TRd);
                WRShear1 = Math.Abs(_VEd1) / _VRdz;
                WRShear2 = Math.Abs(_VEd2) / _VRdy;
                WRTorsion = Math.Abs(_TEd) / _TRd;
                #endregion

                #region bending
                GetMRd(out _MRdy, out _MRdz);
                WRBending1 = Math.Abs(_MEd1) / _MRdz;
                WRBending2 = Math.Abs(_MEd2) / _MRdy;

                WRResistance = GetWrCombined();
                #endregion
            }
            #endregion

            #region buckling
            {
                if (_sec.MinSigma(_NEd, _MEd2, _MEd1) < 0.0)
                {
                    if (_NEd < 0)
                    {
                        _NEd = -NEd;
                    } else
                    {
                        _NEd = 0;
                    }

                    double E = _sec.Material.E;
                    double G = E / (2.0 * (1.0 + _sec.Material.Ni));

                    _L0y = _betay * _L;
                    _L0z = _betaz * _L;
                    _L0LT = _betaLT * _L;

                    _Ncry = GetNcrEuler(E, _sec.J22, _L0y);
                    _Ncrz = GetNcrEuler(E, _sec.J11, _L0z);

                    double lambday = GetLambdaSegn(_sec.Area, fy, _Ncry);
                    double lambdaz = GetLambdaSegn(_sec.Area, fy, _Ncrz);

                    GetImperfectionFactor(out _alphay, out _alphaz);

                    _Phiy = GetPhi(_alphay, lambday);
                    _Phiz = GetPhi(_alphaz, lambdaz);

                    _Chiy = GetChi(_Phiy, lambday);
                    _Chiz = GetChi(_Phiz, lambdaz);

                    double nbRdy = _Chiy * _sec.Area * fy / _annex.Gm1;
                    double nbRdz = _Chiz * _sec.Area * fy / _annex.Gm1;
                    double nbRd = Math.Min(nbRdy, nbRdz);

                    double iy = _sec.InertiaRadius1;
                    double iz = _sec.InertiaRadius2;

                    Point2d shearCenterToCentroid = _sec.Centroid - _sec.ShearCenter;
                    double ncrTorsional = GetNcrT(iy, iz, shearCenterToCentroid.Y, shearCenterToCentroid.X, E, G, _sec.Jt, _sec.Jw, _L0LT);
                    double ncrFlexuralTorsional = GetNcrTF(iy, iz, shearCenterToCentroid.Y, _Ncry, _Ncrz, ncrTorsional);

                    double mcrLateralTorsional = GetMcrLT(_L, _sec.Jt, _sec.Jw, _sec.J11, E, G, _psiy, _supportConditiony, _loadConditiony, 0, _betaLT);

                    double lambdaSegnLT;
                    if (_classificationSection < 3) {
                        lambdaSegnLT = GetLambdaSegn(_sec.Wpl22, fy, mcrLateralTorsional);
                    } else if (_classificationSection == 3) {
                        lambdaSegnLT = GetLambdaSegn(_sec.Wel22Min, fy, mcrLateralTorsional);
                    } else
                    {
                        throw new Exception("class 4 not yet supported");
                    }

                    _alphaLT = GetImperfectionFactorLT(_useEquation_6_57);
                    double kc = Getkc(lambdaSegnLT, _supportConditiony, _loadConditiony, _psiy);

                    if (_sec.GetType() == typeof(SectionH) && _useEquation_6_57 == true)
                    {
                        _PhiLT = GetPhi(_alphaLT, lambdaSegnLT, _annex.Beta, _annex.LambdaLT0);
                        double factorF = Math.Min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * Math.Pow(lambdaSegnLT - 0.8, 2.0)));
                        _ChiLT = GetChiLTmod(_PhiLT, lambdaSegnLT, _annex.Beta, factorF);
                    } else
                    {
                        _PhiLT = GetPhi(_alphaLT, lambdaSegnLT);
                        _ChiLT = GetChi(_PhiLT, lambdaSegnLT);
                    }

                    double MbRdy, MbRdz;
                    if (_classificationSection < 3)
                    {
                        MbRdy = _ChiLT * _sec.Wpl22 * fy / annex.Gm1;
                        MbRdz = _sec.Wpl11 * fy / annex.Gm1;
                    }
                    else if (_classificationSection == 3)
                    {
                        MbRdy = _ChiLT * _sec.Wel22Min * fy / annex.Gm1;
                        MbRdz = _sec.Wel11Min * fy / annex.Gm1;
                    }
                    else
                    {
                        throw new Exception("class 4 not yet supported");
                    }

                    if (_method1AnnexA)
                    {
                        //Annex A
                        double MEdyMax = 0;
                        double deflectiony = 0;
                        double MEdzMax = 0;
                        double deflectionz = 0;

                        double cmy0 = GetCMi0(_loadConditiony, _supportConditiony, _psiy, MEdyMax, deflectiony, _NEd, _Ncry);
                        double cmz0 = GetCMi0(_loadConditionz, _supportConditionz, _psiz, MEdzMax, deflectionz, _NEd, _Ncrz);

                        double muy = GetMu(_NEd, _Ncry, _Chiy);
                        double muz = GetMu(_NEd, _Ncrz, _Chiz);

                        double wy = Math.Min(_sec.Wpl22 / _sec.Wel22Min, 1.5);
                        double wz = Math.Min(_sec.Wpl11 / _sec.Wel11Min, 1.5);

                        double lambdaMax = Math.Max(lambday, lambdaz);

                        double mCrLT0 = GetMcrLT(_L0LT, _sec.Jt, _sec.Jw, _sec.J11, E, G, 1.0, _supportConditiony, _loadConditiony, 0, 1, 1);
                        double lambda0 = GetLambdaSegn(_sec.Wpl22, fy, mCrLT0);

                        double epsilony;
                        if (_classificationSection < 4) {
                            epsilony = _MEd2 / _NEd * _sec.Area / _sec.Wel22Min;
                            double aLT = Math.Max(1.0 - _sec.Jt / _sec.J22,0.0);
                            
                            double C1 = Math.Pow(kc, -2.0);
                            double lambda0Limit = 0.2 * Math.Pow(C1, 0.5) * Math.Pow((1.0 - NEd / _Ncrz) * (1.0 - NEd / ncrFlexuralTorsional), 0.25);

                            double cmy;
                            double cmz;
                            double cmLT;
                            if (lambda0 <= lambda0Limit)
                            {
                                cmy = cmy0;
                                cmz = cmz0;
                                cmLT = 1.0;
                            } else
                            {
                                cmy = cmy0 + (1.0 - cmy0) * Math.Sqrt(epsilony) * aLT / (1.0 + Math.Sqrt(epsilony) * aLT);
                                cmz = cmz0;
                                cmLT = cmy*cmy * aLT / (Math.Sqrt(1.0-_NEd/_Ncrz) * (1.0 - _NEd/ncrTorsional));
                                if (cmLT < 1)
                                {
                                    throw new Exception("cmLT < 1");
                                }
                            }

                            double mplyRd = _sec.Wpl22 * fy / _annex.Gm0;
                            double mplzRd = _sec.Wpl11 * fy / _annex.Gm0;

                            double bLT = 0.5 * aLT * lambda0 * lambda0 * _MEd2 * _MEd1 / (_ChiLT * mplyRd * mplzRd);
                            double cLT = 10.0 * aLT * lambda0 * lambda0 * _MEd2 / (5.0 + Math.Pow(lambdaz,4.0) * cmy * _ChiLT * mplyRd);
                            double dLT = 2.0 * aLT * lambda0 * _MEd2 * _MEd1 / ((0.1 + Math.Pow(lambdaz,4.0)) * cmy * _ChiLT * mplyRd * cmz * mplzRd);
                            double eLT = 1.7 * aLT * lambda0 * _MEd2 / ((0.1 + Math.Pow(lambdaz, 4.0)) * cmy * _ChiLT * mplyRd);

                            double npl = _NEd / (fy * _sec.Area / _annex.Gm0);
                            double cyy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 1.6/wy * cmy * cmy * lambdaMax - 1.6 / wy * cmy * cmy * lambdaMax * lambdaMax) * npl - bLT), _sec.Wel22Min / _sec.Wpl22);
                            double cyz = Math.Max(1.0 + (wz - 1.0) * ((2.0 - 14.0 * cmz * cmz * lambdaMax * lambdaMax / Math.Pow(wz,5.0)) * npl - cLT), 0.6 * Math.Sqrt(wz / wy) * _sec.Wel11Min / _sec.Wel22Min);
                            double czy = Math.Max(1.0 + (wy - 1.0) * ((2.0 - 14.0 * cmy * cmy * lambdaMax * lambdaMax / Math.Pow(wy, 5.0)) * npl - dLT),0.6 * Math.Sqrt(wy / wz) * _sec.Wel22Min / _sec.Wel11Min);
                            double czz = Math.Max(1.0 + (wz - 1) * (2.0 - 1.6 / wz * cmz * cmz * lambdaMax - 1.6 / wz * cmz * cmz * lambdaMax * lambdaMax - eLT) * npl, _sec.Wel11Min / _sec.Wpl11);
  
                            if (_classificationSection <= 2)
                            {
                                _kyy = cmy * cmLT * muy / (1.0 - _NEd / _Ncry) * 1.0 / cyy;
                                _kyz = cmz * muy/(1.0 - _NEd/_Ncrz) * 1.0 / cyz * 0.6 * Math.Sqrt(wz/wy);
                                _kzy = cmy * cmLT * muz/(1.0 - _NEd/_Ncry) * 1.0 / czy * 0.6 * Math.Sqrt(wy/wz);
                                _kzz = cmz * muz / (1.0 - _NEd/_Ncrz) * 1.0 / czz;
                            } else
                            {
                                _kyy = cmy * cmLT * muy / (1.0 - _NEd / _Ncry);
                                _kyz = cmz * muy / (1.0 - _NEd / _Ncrz);
                                _kzy = cmy * cmLT * muz / (1.0 - NEd/_Ncry);
                                _kzz = cmz * muz / (1.0 - _NEd / _Ncrz);
                            }
                        } else
                        {
                            //epsilony = _MEd2 / _NEd * _sec.Area / _sec.Weff;
                            throw new Exception();
                        }
                    } else
                    {
                        //Annex B
                        _kyy = _kzy = _kyz = _kzz = 0;
                    }

                    double deltaMy = 0;
                    double deltaMz = 0;
                    double nrk;
                    double myrk;
                    double mzrk;
                    if (_classificationSection < 3)
                    {
                        nrk = _sec.Area * fy;
                        myrk = _sec.Wpl22 * fy;
                        mzrk = _sec.Wpl11 * fy;
                    } else if (_classificationSection == 3)
                    {
                        nrk = _sec.Area * fy;
                        myrk = _sec.Wel22Min * fy;
                        mzrk = _sec.Wel11Min * fy;
                    } else
                    {
                        throw new Exception("not supported yet");
                    }
                    WRBuckling1 = _NEd / (_Chiy * nrk / _annex.Gm1) + _kyy * (_MEd2 + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kyz * (_MEd1 + deltaMz) / (mzrk / _annex.Gm1);
                    WRBuckling2 = _NEd / (_Chiz * nrk / _annex.Gm1) + _kzy * (_MEd2 + deltaMy) / (_ChiLT * myrk / _annex.Gm1) + _kzz * (_MEd1 + deltaMz) / (mzrk / _annex.Gm1);

                } else
                {
                    //no instability check needed :
                    WRBuckling1 = 0.0;
                    WRBuckling2 = 0.0;
                }
            }
            #endregion
        }

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
            double A = _sec.Area;
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
                if (sec.Lbottom == sec.Ltop && sec.ThicknessBottom == sec.ThicknessTop)
                {
                    Avy = sec.Area - sec.ThicknessTop * sec.Ltop - sec.ThicknessBottom * sec.Lbottom;
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
                /*SectionRHS sec = (SectionRHS)_sec;
                double Hmed = sec.H - sec.ThicknessFlange;
                double Bmed = sec.B - sec.ThicknessWeb;
                double Omega = Hmed * Bmed;
                double denom = 2.0 * Omega * Math.Min(sec.ThicknessWeb, sec.ThicknessFlange);
                tauT = Math.Abs(_TEd) / denom;

                TRd = fy / Math.Pow(3.0, 0.5) * denom;*/
                throw new Exception("to be implemented elastic theory");
            } else if (typeShape == typeof(SectionC))
            {
                SectionC sec = (SectionC)_sec;
                double tmax = Math.Max(sec.Tw, sec.ThicknessBottom);
                tmax = Math.Max(tmax, sec.ThicknessTop);

                double L1 = sec.Lbottom;
                double a1 = sec.ThicknessBottom;
                double denominator = L1 * Math.Pow(a1, 3.0);
                double L2 = sec.Ltop;
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
                new Exception("Section not supported");
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
                Wy = Wz = 0;
                new Exception("not yet supported");
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

            MRdNy = 0.0;
            MRdNz = 0.0;
            double NplRd = fy * A / gm0;
            double n = Math.Abs(NEd) / NplRd;

            if (typeShape == typeof(SectionRectangular)) {
                MRdNy = Mrdy * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
                MRdNz = Mrdz * Math.Pow(1.0 - Math.Abs(NEd) / NplRd, 2.0);
            } else if (typeShape == typeof(SectionH)) {
                SectionH secH = (SectionH) _sec;
                if (secH.LenghtBottomFlange == secH.LenghtTopFlange && secH.ThicknessTopFlange == secH.ThicknessBottomFlange)
                {
                    double a = Math.Min((A - 2.0 * secH.LenghtTopFlange * secH.ThicknessTopFlange) / A, 0.5);
                    double MNyRd = Math.Min(Mrdy * (1.0 - n) / (1.0 - 0.5 * a), Mrdy);
                    double MNzRd = 0.0;
                    if (n <= a)
                    {
                        MNzRd = Mrdz;
                    }
                    else
                    {
                        MNzRd = Mrdz * (1.0 - Math.Pow((n - a) / (1.0 - a), 2.0));
                    }
                }
                else
                {
                    MRdNy = 0.0;
                    MRdNz = 0.0;
                }
            } else if (typeShape == typeof(SectionCHS))
            {
                MRdNy = Mrdy * (1.0 - Math.Pow(n, 1.7));
                MRdNz = Mrdz * (1.0 - Math.Pow(n, 1.7));
            } else if (typeShape == typeof(SectionRHS))
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
            } else
            {
                throw new Exception("");
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
                else if (typeShape == typeof(SectionT))
                {
                    alpha = 1.0;
                    beta = 1.0;
                } else
                {
                    alpha = 1.0;
                    beta = 1.0;
                }

                return Math.Pow(Math.Abs(_MEd2) / _MRdy, alpha) + Math.Pow(Math.Abs(_MEd1) / _MRdz, beta);
            }
            else if (_classificationSection == 3)
            {
                return Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
            }
            else
            {
                return  Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
                throw new Exception("Section class 4 not supported");
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

        protected double GetMcrLT(double L, double Jt, double Jw, double Jz,   double E, double G, double psi, SupportCondition supportCondition, LoadCondition loadCondition,  double zg = 0, double k = 1.0, double kw = 1.0)
        {
            /*
             * C1 = factor that account for the shaper of the moment diagram
             * C2 = factor that account for the point of load application in relation to the shear center
             * C3 = factor that account asymmetry about y-axis
             * 
             * zg = è la distanza tra il punto di applicazione del carico e il centro di taglio:
             *      -positiva se il carico è diretto dall'alto verso il basso ed è applicato all'estradosso. Se invece agisce dal basso verso l'alto il segno va cambiato
             *      -negativa se il carico è diretto dall'alto verso il basso ed è applicato all'intradosso.
             * 
             */
            double C1;
            double C2;
            double C3;
            //symmetric section at least along Z-Z
            if (_sec.IsSymmetricAlongZLocalAxis)
            {
                C3 = 0.0;
                double zj = 0.0;

                if (loadCondition != LoadCondition.NotDirectlyLoaded) { 
                    if (supportCondition == SupportCondition.FixHinge)
                    {
                        if (loadCondition == LoadCondition.Constant)
                        {
                            C1 = 1.127;
                            C2 = 0.454;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            C1 = 1.348;
                            C2 = 0.630;
                        } else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else if (supportCondition == SupportCondition.Restrained)
                    {
                        if (loadCondition == LoadCondition.Constant)
                        {
                            C1 = 2.578;
                            C2 = 1.554;
                        }
                        else if (loadCondition == LoadCondition.SingleForce)
                        {
                            C1 = 1.683;
                            C2 = 1.645;
                        } else
                        {
                            throw new NotSupportedException();
                        }
                    } else
                    {
                        throw new Exception();
                    }
                } else if (loadCondition == LoadCondition.NotDirectlyLoaded)//Beam not directly loaded but with bending moment at the ends
                {
                    C1 = Math.Min(1.77 - 1.04 * psi + 0.27 * psi * psi, 2.6);
                    //C1 = Math.Min(1.88 - 1.40 * psi + 0.52 * psi * psi, 2.7);
                    C2 = 0;
                } else
                {
                    throw new Exception();
                }
                double McrLT = C1 * Math.Pow(Math.PI, 2.0) * E * Jz / Math.Pow(k * L, 2.0) * (Math.Pow(Math.Pow(k / kw, 2.0) * Jw / Jz + Math.Pow(k * L, 2.0) * G * Jt / (Math.Pow(Math.PI, 2.0) * E * Jz) + Math.Pow(C2 * zg, 2.0), 0.5) - (C2 * zg - C3 * zj));
                return McrLT;
            }
            else
            {
                //double zj = zs - 0.5 * INTEGRALE(y^2+z^2)*z dA / Jy
                throw new Exception("cannot calc McrLT");
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
            if (typeShape == typeof(SectionRectangular))
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
                            _alphaz = SectionBucklingCurves["v"];
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
            if (typeShape == typeof(SectionRectangular))
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
                else
                {
                    if (sec.H / sec.B <= 2.0)
                    {
                        alpha_LT = SectionBucklingLTCurves["c"];
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

        protected double Getkc(double lambda_segn_LT, SupportCondition supportCondition, LoadCondition loadCondition, double psi)
        {
            double kc;
            if (loadCondition != LoadCondition.NotDirectlyLoaded)
            {
                if (supportCondition == SupportCondition.FixHinge && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.94;
                } else if (supportCondition == SupportCondition.Restrained && loadCondition ==  LoadCondition.Constant)
                {
                    kc = 0.90;
                } else if (supportCondition == SupportCondition.OneSideRestrained_OneSideHinged && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.91;
                } else if (supportCondition == SupportCondition.FixHinge && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.86;
                } else if (supportCondition == SupportCondition.Restrained && loadCondition == LoadCondition.SingleForce)
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
                if (psi == 1)
                {
                    kc = 1;
                }  else
                {
                    kc = 1.0 / (1.33 - 0.33 * psi);
                }
            }
            return kc;
        }

        protected double GetCMi0(LoadCondition loadCondition, SupportCondition supportCondition, double psi, double MEdMax, double deflection, double NEd, double Ncr)
        {
            if (loadCondition == LoadCondition.SingleForce && supportCondition == SupportCondition.FixHinge)
            {
                return 1.0 - 0.18 * NEd / Ncr;
            } else if (loadCondition == LoadCondition.Constant && supportCondition == SupportCondition.FixHinge)
            {
                return 1 + 0.03 * NEd / Ncr;
            } else if (loadCondition == LoadCondition.NotDirectlyLoaded)
            {
                return 0.79 + 0.21 * psi + 0.36 * (psi - 0.33) * NEd / Ncr;
            } else {
                return 1.0 + (Math.PI * Math.PI * _sec.Material.E * Math.Abs(deflection) / (_L * _L * MEdMax) - 1.0) * NEd / Ncr;
            }            
        }

        public double GetMu(double Ned, double Ncr, double Chi)
        { 
            double mu = (1.0 - Ned / Ncr) / (1.0 - Chi * Ned / Ncr);
            return mu;
        }
        #endregion
    }
}
