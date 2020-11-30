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

        protected double L;
        protected double _psi;

        protected double _betay;
        protected double _betaz;
        protected double _betaLT;

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

        protected bool _method1AnnexA;
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

        public double WRAxial { get; }
        public double WRShear1 { get; }
        public double WRShear2 { get; }
        public double WRBending2 { get; }
        public double WRBending1 { get; }
        public double WRTorsion { get; }
        public double WRResistance { get; }
        #endregion

        protected enum LoadCondition
        {
            Constant,
            SingleForce
        }
        protected enum SupportCondition
        {
            FixHinge,
            Restrained,
            Restrained_Hinged
        }

        public EuroCodeBeamChecker(Section sec, double NEd, double V1Ed, double V2Ed, double M1Ed, double M2Ed, double TEd, Annex annex)
        {
            _sec = sec;
            _annex = annex;

            _NEd = NEd;
            _VEd1 = V1Ed;
            _VEd2 = V2Ed;
            _MEd1 = M1Ed;
            _MEd2 = M2Ed;
            _TEd = TEd;           

            #region classification
            _classificationSection = 1;
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
                GetVRdTRd(out double _VRdy, out double _VRdz, out double _TRd);
                WRShear1 = Math.Abs(V1Ed) / _VRdz;
                WRShear2 = Math.Abs(V2Ed) / _VRdy;
                WRTorsion = Math.Abs(TEd) / _TRd;
                #endregion

                #region bending
                GetMRd(out double _MRdy, out double _MRdz);
                WRBending1 = Math.Abs(_MEd1) / _MRdz;
                WRBending2 = Math.Abs(_MEd2) / _MRdy;

                GetWrCombined(out double WRResistance);
                #endregion
            }
            #endregion

            #region buckling
            {
                if (sec.MinSigma(_NEd, _MEd2, _MEd1) < 0.0)
                {
                    double E = _sec.Material.E;
                    double G = E / (2.0 * (1.0 + _sec.Material.Ni));
                    double fy = ((SteelMaterial)_sec.Material).Fyk;

                    double L0y = _betay * L;
                    double L0z = _betaz * L;
                    double L0T = _betaLT * L;

                    double Ncry = NcrEuler(E, _sec.J22, L0y);
                    double Ncrz = NcrEuler(E, _sec.J11, L0z);

                    double lambday = LambdaSegn(_sec.Area, fy, Ncry);
                    double lambdaz = LambdaSegn(_sec.Area, fy, Ncrz);

                    GetImperfectionFactor(out double alphay, out double alphaz);

                    double Phiy = Phi(alphay, lambday);
                    double Phiz = Phi(alphay, lambday);

                    double Chiy = Chi(Phiy, lambday);
                    double Chiz = Chi(Phiz, lambdaz);

                    double NbRdy = Chiy * _sec.Area * fy / _annex.Gm1;
                    double NbRdz = Chiz * _sec.Area * fy / _annex.Gm1;
                    double NbRd = Math.Min(NbRdy, NbRdz);

                    double iy = _sec.InertiaRadius1;
                    double iz = _sec.InertiaRadius2;

                    Point2d ShearCenterToCentroid = _sec.Centroid - _sec.ShearCenter;
                    double NcrTorsional = NcrT(iy, iz, ShearCenterToCentroid.Y, ShearCenterToCentroid.X, E, G, _sec.Jt, _sec.Jw, L0T);
                    double NcrFlexuralTorsional = NcrTF(iy, iz, ShearCenterToCentroid.Y, Ncry, Ncrz, NcrTorsional);

                    double McrLateralTorsional = McrLT(L, _sec.Jt, _sec.Jw, _sec.J11, E, G, _psi, null, null, 0, _betaLT);

                    double lambdaSegnLT;
                    if (_classificationSection < 3) {
                        lambdaSegnLT = LambdaSegn(_sec.Wpl22, fy, McrLateralTorsional);
                    } else if (_classificationSection == 3) {
                        lambdaSegnLT = LambdaSegn(Math.Min(_sec.Wel22Bottom, _sec.Wel22Top), fy, McrLateralTorsional);
                    } else
                    {
                        throw new Exception("class 4 not yet supported");
                    }

                    bool useEquation_6_57 = true;
                    double alpha_LT = GetImperfectionFactorLT(useEquation_6_57);

                    double PhiLT;
                    if (_sec.GetType() == typeof(SectionH) && useEquation_6_57 == true)
                    {
                        PhiLT = Phi(alpha_LT, lambdaSegnLT, _annex.Beta, _annex.LambdaLT0);
                    } else
                    {
                        PhiLT = Phi(alpha_LT, lambdaSegnLT);
                    }

                    double ChiLT;
                    if (useEquation_6_57)
                    {
                        double factorF = f(lambdaSegnLT, null, null, _psi);
                        ChiLT = ChiLTmod(PhiLT, lambdaSegnLT, _annex.Beta, factorF);
                    } else
                    {
                        ChiLT = Chi(PhiLT, lambdaSegnLT);
                    }

                    double MbRdy, MbRdz;
                    if (_classificationSection < 3)
                    {
                        MbRdy = ChiLT * _sec.Wpl22 * fy / annex.Gm1;
                        MbRdz = _sec.Wpl11 * fy / annex.Gm1;
                    }
                    else if (_classificationSection == 3)
                    {
                        MbRdy = ChiLT * Math.Min(_sec.Wel22Bottom, _sec.Wel22Top) * fy / annex.Gm1;
                        MbRdz = Math.Min(_sec.Wel11Left, _sec.Wel11Right) * fy / annex.Gm1;
                    }
                    else
                    {
                        throw new Exception("class 4 not yet supported");
                    }

                    _kyy = _kyz = _kzy = _kzz = 0;

                } else
                {
                    //no instability check needed
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
                //Bredt
                SectionCHS sec = (SectionCHS)_sec;
                double Dmed = sec.Dext - sec.T / 2.0;
                double Omega = Math.PI * Math.Pow(Dmed, 2.0) / 4.0;
                double denom = 2.0 * Omega * sec.T;
                tauT = Math.Abs(_TEd) / denom;

                TRd = fy / Math.Pow(3.0, 0.5) * denom;
            } else if (typeShape == typeof(SectionRHS))
            {
                //Bredt
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
                    VplRdTy = (1.0 - Math.Abs(tauT) / (fy / Math.Pow(3.0, 0.5) / gm0)) * VplRdz;
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
            double MEdy = _MEd2;
            double MEdz = _MEd1;

            double A = _sec.Area;
            double Wy;
            double Wz;
            if (_classificationSection < 3)
            {
                Wy = _sec.Wpl22;
                Wz = _sec.Wpl11;
            } else if (_classificationSection == 3)
            {
                Wy = Math.Min(_sec.Wel22Bottom, _sec.Wel22Top);
                Wz = Math.Min(_sec.Wel11Left, _sec.Wel11Right);
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

        protected void GetWrCombined(out double WRResistance)
        {
            Type typeShape = _sec.GetType();

            if (_classificationSection < 3)
            {
                double alpha = 1.0;
                double beta = 1.0;
                double n = _sec.Area * ((SteelMaterial)_sec.Material).Fyk / _annex.Gm0;

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

                WRResistance = Math.Pow(Math.Abs(_MEd2) / _MRdy, alpha) + Math.Pow(Math.Abs(_MEd1) / _MRdz, beta);
            }
            else if (_classificationSection == 3)
            {
                WRResistance = Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
            }
            else
            {
                WRResistance = Math.Abs(_NEd / _NRd) + Math.Abs(_MEd2) / _MRdy + Math.Abs(_MEd1) / _MRdz;
                throw new Exception("Section class 4 not supported");
            }
        }
        #endregion

        #region BucklingFunction
        protected double NcrEuler(double E, double J, double L0)
        {
            double Ncr = Math.Pow(Math.PI, 2.0) * E * J / (Math.Pow(L0, 2.0));
            return Ncr;
        }

        protected double LambdaSegn(double A, double fy, double Ncr)
        {
            double lambdaSegn = Math.Pow(A * fy / Ncr, 0.5);
            return lambdaSegn;
        }

        protected double Phi(double alpha, double lambda_segn, double beta = 1.0, double lambda_LT0 = 0.2) //default beta=1, lambda_LT0 = 0.2
        {
            double Phi = 0.5 * (1 + alpha * (lambda_segn - lambda_LT0) + beta * Math.Pow(lambda_segn, 2.0));
            return Phi;
        }

        protected double Chi(double Phi, double lambda_segn)
        {
            double Chi = Math.Min(1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - Math.Pow(lambda_segn, 2.0), 0.5)),1.0);
            return Chi;
        }

        protected double ChiLTmod(double Phi, double lambda_segn, double beta = 1.0, double f = 1.0) //beta_default = 1, f=1
        {
            double Chi = 1.0 / (Phi + Math.Pow(Math.Pow(Phi, 2.0) - beta * Math.Pow(lambda_segn, 2.0), 0.5));
            Chi = Math.Min(Chi, 1.0);
            Chi = Math.Min(Chi, 1.0 / Math.Pow(lambda_segn, 2.0));

            double ChiLTMod = Math.Min(Chi/f, 1.0);
            ChiLTMod = Math.Min(ChiLTMod, 1.0 / Math.Pow(lambda_segn, 2.0));
            return ChiLTMod;
        }

        protected double NcrT(double iy, double iz, double y0, double z0, double E, double G, double It, double Jw, double L0T)  //EN 1993-1-3 eq 6.33a
        {
            /*
             * y0 and z0 = coordinates of shear center in respect of the centroid gross section
             */
            double i0 = Math.Pow(Math.Pow(iy, 2.0) + Math.Pow(iz, 2.0) + Math.Pow(y0, 2.0) + Math.Pow(z0, 2.0), 0.5);
            double Ncr_T = 1.0 / Math.Pow(i0, 2.0) * (G * It + Math.Pow(Math.PI, 2.0) * E * Jw / Math.Pow(L0T, 2.0));
            return Ncr_T;
        }

        protected double NcrTF(double iy, double iz, double y0, double Ncr_y, double Ncr_z, double Ncr_T) //EN 1993-1-3 eq 6.35
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

        protected double McrLT(double L, double Jt, double Jw, double Jz,   double E, double G, double psi, SupportCondition? supportCondition = null, LoadCondition? loadCondition = null,  double zg = 0, double k = 1.0, double kw = 1.0)
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

                if (supportCondition != null && loadCondition != null)
                {
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
                    else //Restrained-Fix
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
                    }
                } else //Beam not directly loaded but with bending moment at the ends
                {
                    C1 = Math.Min(1.77 - 1.04 * psi + 0.27 * psi * psi, 2.6);
                    //C1 = Math.Min(1.88 - 1.40 * psi + 0.52 * psi * psi, 2.7);
                    C2 = 0;
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

        protected void GetImperfectionFactor(out double alphay, out double alphaz)
        {
            Dictionary<string, double> SectionBucklingCurves = new Dictionary<string, double>();
            SectionBucklingCurves.Add("a0", 0.13);
            SectionBucklingCurves.Add("a", 0.21);
            SectionBucklingCurves.Add("b", 0.34);
            SectionBucklingCurves.Add("c", 0.49);
            SectionBucklingCurves.Add("d", 0.76);

            alphay = 1;
            alphaz = 1;
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
                                alphay = SectionBucklingCurves["a0"];
                                alphaz = SectionBucklingCurves["a0"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                alphay = SectionBucklingCurves["a"];
                                alphay = SectionBucklingCurves["b"];
                            }
                        }
                        else if (sec.ThicknessBottomFlange <= 100.0 && sec.ThicknessTopFlange <= 100.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                alphay = SectionBucklingCurves["a"];
                                alphaz = SectionBucklingCurves["a"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                alphay = SectionBucklingCurves["b"];
                                alphay = SectionBucklingCurves["c"];
                            }
                        }
                    }
                    else // H/B <= 1.2
                    {
                        if (sec.ThicknessBottomFlange <= 100.0 && sec.ThicknessTopFlange <= 100.0)
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                alphay = SectionBucklingCurves["a"];
                                alphaz = SectionBucklingCurves["a"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                alphay = SectionBucklingCurves["b"];
                                alphay = SectionBucklingCurves["c"];
                            }
                        }
                        else //thicknessflanges > 100 mm 
                        {
                            if (sec.Material.Name.Contains("460"))
                            {
                                alphay = SectionBucklingCurves["c"];
                                alphaz = SectionBucklingCurves["c"];
                            }
                            else //steel S235, 275, 355, 420
                            {
                                alphay = SectionBucklingCurves["d"];
                                alphay = SectionBucklingCurves["d"];
                            }
                        }
                    }
                } else
                {
                    if (sec.ThicknessTopFlange <= 40.0 & sec.ThicknessBottomFlange <= 40.0)
                    {
                        if (sec.Material.Name.Contains("460"))
                        {
                            alphay = SectionBucklingCurves["b"];
                            alphaz = SectionBucklingCurves["c"];
                        }
                        else //steel S235, 275, 355, 420
                        {
                            alphay = SectionBucklingCurves["b"];
                            alphay = SectionBucklingCurves["v"];
                        }
                    } else
                    {
                        if (sec.Material.Name.Contains("460"))
                        {
                            alphay = SectionBucklingCurves["c"];
                            alphaz = SectionBucklingCurves["d"];
                        }
                        else //steel S235, 275, 355, 420
                        {
                            alphay = SectionBucklingCurves["c"];
                            alphay = SectionBucklingCurves["d"];
                        }
                    }
                }
            } else if (typeShape == typeof(SectionRHS) || typeShape == typeof(SectionCHS))
            {
                bool isColdFormed ;
                if (typeShape == typeof(SectionRHS))
                {
                    isColdFormed = ((SectionRHS)_sec).IsColdFormed;
                } else
                {
                    isColdFormed = ((SectionCHS)_sec).IsColdFormed;
                }

                if (isColdFormed)
                {
                    alphay = SectionBucklingCurves["c"];
                    alphaz = SectionBucklingCurves["c"];
                } else //Hot Finished
                {
                    if (_sec.Material.Name.Contains("460"))
                    {
                        alphay = SectionBucklingCurves["a0"];
                        alphaz = SectionBucklingCurves["a0"];
                    }
                    else //steel S235, 275, 355, 420
                    {
                        alphay = SectionBucklingCurves["a"];
                        alphaz = SectionBucklingCurves["a"];
                    }
                }
            } else if (typeShape == typeof(SectionC) || typeShape == typeof(SectionT) || typeShape == typeof(SectionRectangular) || typeShape == typeof(SectionCircular))
            {
                alphay = SectionBucklingCurves["c"];
                alphaz = SectionBucklingCurves["c"];
            } else if (typeShape == typeof(SectionL))
            {
                alphay = SectionBucklingCurves["b"];
                alphaz = SectionBucklingCurves["b"];
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

        protected double f(double lambda_segn_LT, SupportCondition? supportCondition = null, LoadCondition? loadCondition = null, double psi = 1)
        {
            double kc;
            if (supportCondition != null && loadCondition != null)
            {
                if (supportCondition == SupportCondition.FixHinge && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.94;
                } else if (supportCondition == SupportCondition.Restrained && loadCondition ==  LoadCondition.Constant)
                {
                    kc = 0.90;
                } else if (supportCondition == SupportCondition.Restrained_Hinged && loadCondition == LoadCondition.Constant)
                {
                    kc = 0.91;
                } else if (supportCondition == SupportCondition.FixHinge && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.86;
                } else if (supportCondition == SupportCondition.Restrained && loadCondition == LoadCondition.SingleForce)
                {
                    kc = 0.77;
                } else if (supportCondition == SupportCondition.Restrained_Hinged && loadCondition == LoadCondition.SingleForce)
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

            double f = Math.Min(1.0, 1 - 0.5 * (1 - kc) * (1 - 2 * Math.Pow(lambda_segn_LT - 0.8, 2.0)));
            return f;
        }
        #endregion
    }
}
