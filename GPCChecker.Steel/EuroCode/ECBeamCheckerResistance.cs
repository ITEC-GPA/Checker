using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;
using GPC.Model.Sections;

namespace GPC.Checker.Steel.EuroCode
{
    public class ECBeamCheckerResistance
    {
        #region Variables
        Section _sec;
        int _classificationSection;
        Annex _annex;

        double _NEd;
        double _VEd1;
        double _VEd2;
        double _MEd1;
        double _MEd2;
        double _TEd;

        double _NRd;
        double _VRdy;
        double _VRdz;
        double _MRdy;
        double _MRdz;
        double _TRd;
        #endregion

        #region Properties
        public double WRAxial { get; }
        public double WRShear1 { get; }
        public double WRShear2 { get; }
        public double WRBending2 { get; }
        public double WRBending1 { get; }
        public double WRTorsion { get; }
        public double WRResistance { get; }
        #endregion

        public ECBeamCheckerResistance(Section sec, double NEd, double V1Ed, double V2Ed, double M1Ed, double M2Ed, double TEd)
        {
            #region Variables
            _sec = sec;

            _NEd = NEd;
            _VEd1 = V1Ed;
            _VEd2 = V2Ed;
            _MEd1 = M1Ed;
            _MEd2 = M2Ed;
            _TEd = TEd;
            #endregion            

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

            }
            #endregion
        }

        #region internalfunction
        public double GetNtRd(double Anet)
        {
            double A = _sec.Area;
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double fu = ((SteelMaterial)_sec.Material).Fu;
            double gm0 = _annex.Gm0;
            double gm2 = _annex.Gm2;

            double NtRd = Math.Min(A * fy / gm0, 0.9 * Anet * fu / gm2);
            return NtRd;
        }

        public double GetNcRd()
        {
            double A = _sec.Area;
            double fy = ((SteelMaterial)_sec.Material).Fyk;
            double gm0 = _annex.Gm0;

            double NcRd = A * fy / gm0;
            return NcRd;
        }

        public void GetVRdTRd(out double VRdy, out double VRdz, out double TRd)
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
            } else if (typeShape == typeof(SectionCircular))
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

            if (typeShape == typeof(SectionCircular))
            {
                //Bredt
                SectionCircular sec = (SectionCircular)_sec;
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
                } else if (typeShape == typeof(SectionCircular)) { 
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

        public void GetMRd(out double MRdNy, out double MRdNz)
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
                if (MEdy > 0) { //fibre tese inferiori 
                    Wy = _sec.Wel22Max;
                } else
                {
                    Wy = _sec.Wel22Min;
                }

                if (MEdz > 0)
                {               //fibre "tese inferiori"
                    Wz = _sec.Wel11Max;
                }
                else
                {
                    Wz = _sec.Wel11Min;
                }
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
            } else if (typeShape == typeof(SectionCircular))
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

        public void GetWrCombined(out double WRResistance)
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
                else if (typeShape == typeof(SectionCircular))
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
    }
}
