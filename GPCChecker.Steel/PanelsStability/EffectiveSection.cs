/*  ========================================================================= *\
   Project:    GPC Checker - Class4 - Effective Section
   Date:       02/02/2019

	File:      
	Author:    Vochescu Robert


    Overview:  Class 4 - Effective Section Properties
\* ========================================================================= */

#if NEVER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.PanelsStability
{
    public class EffectiveSection
    {
        public EffectiveSection(Code __code, double __B1, double __B2, double __B3, double __B4, double __tf, double __H, double __H1, double __tw, int __stiffNum, double __ts, double __bs, double __a1, double __a3, double __Ldiaf, bool __IsUnstiffened = false)
        {
            _code = __code;
            _B1 = __B1;
            _B2 = __B2;
            _B3 = __B3;
            _B4 = __B4;
            _tf = __tf;
            _H = __H;
            _H1 = __H1;
            _tw = __tw;
            _stiffNum = __stiffNum;
            _ts = __ts;
            _bs = __bs;
            _a1 = __a1;
            _a3 = __a3;
            _Ldiaf = __Ldiaf;

            _IsUnstiffened = __IsUnstiffened;

            BOX_EffectiveSection();
        }

        private void BOX_EffectiveSection()
        {
            double fy = 355;
            double E = 210000;
            double ni = 0.3;
            double[] Output = new double[4];


            #region PURE-COMPRESSION_PANELS
            //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            double phiComp = 1;
            double ksigmaComp = 4;
            //  UNSTIFFENED FLANGES_PURE-COMPRESSION
            if (_stiffNum == 0 && _bs == 0 && _ts == 0)
            {
                //_IsUnstiffened = true;
            }
            /// Upper flange
            double B11 = _B2 - 2 * _tw;
            UnstiffenedPanel TopFlangeCompression = new UnstiffenedPanel(_code, _tf, B11, phiComp, ksigmaComp, fy, E, ni, _Ldiaf);
            /// Bottom flange
            double B22 = _B2 - 2 * _tw;
            UnstiffenedPanel BottomFlangeCompression = new UnstiffenedPanel(_code, _tf, B22, phiComp, ksigmaComp, fy, E, ni, _Ldiaf);
            //  UNSTIFFENED WEBS_PURE-COMPRESSION
            double h = _H - 2 * _tf;
            UnstiffenedPanel WebsCompression = new UnstiffenedPanel(_code, _tw, h, phiComp, ksigmaComp, fy, E, ni, _Ldiaf);

            //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            //  STIFFENED_1_WEBS_PURE-COMPRESSION
            /// WEB Panel1
            UnstiffenedPanel Stiff_1_P1 = new UnstiffenedPanel(_code, _tw, (_H - 2 * _tf) / 2, phiComp, ksigmaComp, fy, E, ni);
            /// WEB Panel2
            UnstiffenedPanel Stiff_1_P2 = new UnstiffenedPanel(_code, _tw, (_H - 2 * _tf) / 2, phiComp, ksigmaComp, fy, E, ni);
            /// Single Stiffener
            FlatStiffener Stiff_1_Stiffener1 = new FlatStiffener(_code, _ts, _bs, Stiff_1_P1.t, Stiff_1_P1.b, Stiff_1_P2.b, Stiff_1_P1.bbottom, Stiff_1_P2.btop, _Ldiaf, StiffenerLongitudinalType.Open, fy, E, ni);
            /// 
            List<UnstiffenedPanel> Stiff_1_PanelArray = new List<UnstiffenedPanel>();
            Stiff_1_PanelArray.Add(Stiff_1_P1);
            Stiff_1_PanelArray.Add(Stiff_1_P2);
            List<FlatStiffener> Stiff_1_StiffenerArray = new List<FlatStiffener>();
            Stiff_1_StiffenerArray.Add(Stiff_1_Stiffener1);
            StiffenedPanel Stiff_1_StiffPanel = new StiffenedPanel(_code, 1, Stiff_1_PanelArray, Stiff_1_StiffenerArray, 1);
            //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            //  STIFFENED_2_WEBS_PURE-COMPRESSION
            /// WEB Panel1 - Uniform Compression
            UnstiffenedPanel Stiff_2_P1 = new UnstiffenedPanel(_code, _tw, _a1, phiComp, ksigmaComp, fy, E, ni);
            /// WEB Panel2 - Uniform Compression
            UnstiffenedPanel Stiff_2_P2 = new UnstiffenedPanel(_code, _tw, (_H - _tf - _a1 - _a3), phiComp, ksigmaComp, fy, E, ni);
            /// WEB Panel3s - Unform Compression
            UnstiffenedPanel Stiff_2_P3 = new UnstiffenedPanel(_code, _tw, _a3, phiComp, ksigmaComp, fy, E, ni);
            /// WEB Panel 1a-1b - Uniform Compression
            UnstiffenedPanel Stiff_2_WPMiddlec = new UnstiffenedPanel(_code, _tw, (_H - _tf) / 2, phiComp, ksigmaComp, fy, E, ni);
            /// Stiffener 1
            FlatStiffener Stiff_2_FlatStiffener1 = new FlatStiffener(_code, _ts, _bs, Stiff_2_P1.t, Stiff_2_P1.b, Stiff_2_P2.b, Stiff_2_P1.bbottom, Stiff_2_P2.btop, _Ldiaf, StiffenerLongitudinalType.Open, fy, E, ni);
            /// Stiffener 2
            FlatStiffener Stiff_2_FlatStiffener2 = new FlatStiffener(_code, _ts, _bs, Stiff_2_P2.t, Stiff_2_P2.b, Stiff_2_P3.b, Stiff_2_P2.bbottom, Stiff_2_P3.btop, _Ldiaf, StiffenerLongitudinalType.Open, fy, E, ni);
            /// Stiffener Lumped
            FlatStiffener Stiff_2_FlatStiffenerLumped = new FlatStiffener(_code, _ts, _bs, Stiff_2_P2.t, (_H - _bs) / 2.0, (_H - _tf) / 2.0, Stiff_2_WPMiddlec.bbottom, Stiff_2_WPMiddlec.btop, _Ldiaf, StiffenerLongitudinalType.Open, fy, E, ni);
            /// Arrays of Panels and Stiffeners
            List<UnstiffenedPanel> Stiff_2_PanelArray = new List<UnstiffenedPanel>();
            Stiff_2_PanelArray.Add(Stiff_2_P1);
            Stiff_2_PanelArray.Add(Stiff_2_P2);
            Stiff_2_PanelArray.Add(Stiff_2_P3);
            Stiff_2_PanelArray.Add(Stiff_2_WPMiddlec);
            List<FlatStiffener> Stiff_2_StiffenerArray = new List<FlatStiffener>();
            Stiff_2_StiffenerArray.Add(Stiff_2_FlatStiffener1);
            Stiff_2_StiffenerArray.Add(Stiff_2_FlatStiffener2);
            Stiff_2_StiffenerArray.Add(Stiff_2_FlatStiffenerLumped);
            StiffenedPanel Stiff_2_StiffPanel = new StiffenedPanel(_code, 2, Stiff_2_PanelArray, Stiff_2_StiffenerArray, phiComp);
            #endregion

            #region FLEXURE_PANELS
            double phiflex = -1;
            UnstiffenedPanel TopFlangePureBending = new UnstiffenedPanel(_code, _tf, B11, phiflex, ksigmaComp, fy, E, ni);
            #endregion

            double a1_phi = 0.4445479;
            UnstiffenedPanel WP1_bXX = new UnstiffenedPanel(_code, _tw, _a1, a1_phi, 4, fy, E, ni);
            /// WEB Panel2
            double a2_phi = -296.055 / 444.5479;
            UnstiffenedPanel WP2_bXX = new UnstiffenedPanel(_code, _tw, (_H - _tf - _a1 - _a3), a2_phi, 4, fy, E, ni);

            /*double ciao = 0;
            double ciao1 = 0;*/

            #region CALC_PROPERTIES
            CalcBoxGrossProperties();
            CalcEffectivePropertiesCompression(TopFlangeCompression, WebsCompression, Stiff_1_StiffPanel, Stiff_2_StiffPanel);

            CalcEffectivePropertiesFlexural(_stiffNum, TopFlangeCompression, TopFlangePureBending, WebsCompression, Stiff_1_StiffPanel, Stiff_1_StiffPanel, Stiff_2_StiffPanel, Stiff_2_StiffenerArray);
            #endregion
        }


        public void CalcEffectivePropertiesCompression(UnstiffenedPanel FlangesPureComp, UnstiffenedPanel WebsComp, StiffenedPanel Stiffened_1_Panel, StiffenedPanel Stiffened_2_Panel)
        {
            double h1w = 0;
            //double h2w = 0;
            //double h3w = 0;
            //double h4w = 0;
            //double h5w = 0;
            double h6w = 0;
            //double htw = 0;
            //double hcw = 0;
            //double hbw = 0;
            double t_w = 0;
            double B2effbottom = 0;
            double B2efftop = 0;
            double t_f = 0;
            double Aeffpanel = 0;

            if (_stiffNum == 0 && _ts == 0 && _bs == 0)
            {
                h1w = WebsComp.btop;
                //h2w = 0;
                //h3w = 0;
                //h4w = 0;
                //h5w = 0;
                h6w = WebsComp.bbottom;

                t_w = WebsComp.t;
                t_f = FlangesPureComp.t;

                B2effbottom = (FlangesPureComp.be1 + FlangesPureComp.be2 + 2 * t_w + _B1 + _B3);
                B2efftop = (FlangesPureComp.be1 + FlangesPureComp.be2 + 2 * t_w + _B4 + _B4);

                double Aweff = (h1w + h6w) * t_w;
                double Aftopeff = B2efftop * t_f;
                double Afbottomeff = B2effbottom * t_f;

                _Aeff = Aftopeff + Afbottomeff + 2 * Aweff;

                // xg = (b2 * tp * (b2 / 2.0 + b1) + h * tw1 * (tw1 / 2.0 + b1) + h * twe * (b1 + b2 - twe / 2.0) + tp / 2.0 * (b1 + b2 + b3) ^ 2) / area
                // jy = tp * b2 ^ 3 / 12.0 + h * tw1 ^ 3 / 12.0 + h * twe ^ 3 / 12.0 + tp * (b1 + b2 + b3) ^ 3 / 12.0
                // jy = jy + b2 * tp * (b1 + b2 / 2.0 - xg) ^ 2
                // jy = jy + h * tw1 * (b1 + tw1 / 2.0 - xg) ^ 2
                // jy = jy + h * twe * (b1 + b2 - twe / 2.0 - xg) ^ 2
                // jy = jy + (b1 + b2 + b3) * tp * ((b1 + b2 + b3) / 2.0 - xg) ^ 2
                _xgeff = 0;
                _xgeff = _xgeff + (2 * FlangesPureComp.be1 * t_f * (FlangesPureComp.be1 + t_w + _B1)); /// Flange be1
                _xgeff = _xgeff + (2 * FlangesPureComp.be2 * t_f * (_B2 - FlangesPureComp.be2 / 2 + _B1)); /// Flange be2


                _tweff = Aweff / (_H - 2 * _tf);

                if (_tweff > _tw)
                {
                    _tweff = _tw;
                }

                //if(_tweff > (Math.Floor(_tweff)+ 0.5))
                //{
                //    _tweff = Math.Floor(_tweff) + 0.5;
                //}
                //else
                //{
                //    _tweff = Math.Floor(_tweff);
                //}

                _tfeff = Math.Min(Aftopeff / _B2, Afbottomeff / _B2);


                if (_tfeff > _tf)
                {
                    _tfeff = _tf;
                }
                //if (_tfeff > (Math.Floor(_tfeff) + 0.5))
                //{
                //    _tfeff = Math.Floor(_tfeff) + 0.5;
                //}
                //else
                //{
                //    _tfeff = Math.Floor(_tfeff);
                //}

                _Heff = _H;
                _Beff = _B2;

                _Aeff = _H * _B2 - (_H - 2 * _tfeff) * (_B2 - 2 * _tweff);

                _IXXeqeff = _Beff * Math.Pow(_Heff, 3.0) / 12.0 - (_Beff - 2 * _tweff) * Math.Pow((_Heff - 2 * _tfeff), 3.0) / 12.0;
                _IYYeqeff = Math.Pow(_Beff, 3.0) * _Heff / 12.0 - Math.Pow((_Beff - 2 * _tweff), 3) * (_Heff - 2 * _tfeff) / 12.0;

                _IXXeff = _IXXeqeff;
                _IYYeff = _IYYeqeff;
            }
            if (_stiffNum == 1)
            {
                #region CALC_EFFECTIVE_AREA
                #endregion
                h1w = Stiffened_1_Panel.PanelArray[0].btop;
                //h2w = 0;
                //h3w = 0;
                //h4w = 0;
                //h5w = 0;
                h6w = Stiffened_1_Panel.PanelArray[1].bbottom;
                //htw = 0;
                //hcw = 0;
                //hbw = 0;
                t_w = Stiffened_1_Panel.PanelArray[0].t;
                t_f = FlangesPureComp.t;
                Aeffpanel = Stiffened_1_Panel.Atoteff;

                B2effbottom = (FlangesPureComp.be1 + FlangesPureComp.be1 + 2 * t_w + _B1 + _B3);
                B2efftop = (FlangesPureComp.be1 + FlangesPureComp.be1 + 2 * t_w + _B4 + _B4);

                t_f = FlangesPureComp.t;
                double Aweff = ((h1w + h6w) * t_w + Aeffpanel);
                double Aftopeff = B2efftop * t_f;
                double Afbottomeff = B2effbottom * t_f;

                _Aeff = Aftopeff + Afbottomeff + 2 * Aweff;

                #region CALC_EFFECTIVE_PROPERTIES
                _tweff = Aweff / (_H - 2 * _tf);

                if (_tweff > _tw)
                {
                    _tweff = _tw;
                }

                //if(_tweff > (Math.Floor(_tweff)+ 0.5))
                //{
                //    _tweff = Math.Floor(_tweff) + 0.5;
                //}
                //else
                //{
                //    _tweff = Math.Floor(_tweff);
                //}

                _tfeff = RoundDown(Math.Min(Aftopeff / _B2, Afbottomeff / _B2), 1);

                if (_tfeff > _tf)
                {
                    _tfeff = _tf;
                }

                //if (_tfeff > (Math.Floor(_tfeff) + 0.5))
                //{
                //    _tfeff = Math.Floor(_tfeff) + 0.5;
                //}
                //else
                //{
                //    _tfeff = Math.Floor(_tfeff);
                //}

                _Heff = _H;
                _Beff = _B2;

                _Aeff = _H * _B2 - (_H - 2 * _tfeff) * (_B2 - 2 * _tweff);

                _IXXeqeff = _Beff * Math.Pow(_Heff, 3.0) / 12.0 - (_Beff - 2 * _tweff) * Math.Pow((_Heff - 2 * _tfeff), 3.0) / 12.0;
                _IYYeqeff = Math.Pow(_Beff, 3.0) * _Heff / 12.0 - Math.Pow((_Beff - 2 * _tweff), 3) * (_Heff - 2 * _tfeff) / 12.0;
                #endregion
            }
            if (_stiffNum == 2)
            {
                #region CALC_EFFECTIVE_AREA
                h1w = Stiffened_2_Panel.PanelArray[0].btop;
                //h2w = 0;
                //h3w = 0;
                //h4w = 0;
                //h5w = 0;
                h6w = Stiffened_2_Panel.PanelArray[2].bbottom;
                //htw = 0;
                //hcw = 0;
                //hbw = 0;
                t_w = Stiffened_2_Panel.PanelArray[0].t;
                t_f = FlangesPureComp.t;
                //Aeffpanel = Stiffened_2_Panel.Atoteff/* * Stiffened_2_Panel.Rhoc*/;
                B2effbottom = (FlangesPureComp.be1 + FlangesPureComp.be1 + 2 * t_w + _B1 + _B3);
                B2efftop = (FlangesPureComp.be1 + FlangesPureComp.be1 + 2 * t_w + _B4 + _B4);
                Aeffpanel = 2 * Stiffened_2_Panel.Atoteff;
                double Aftopeff = B2efftop * t_f;
                double Afbottomeff = B2effbottom * t_f;
                double Aweff = 2.0 * (h1w + h6w) * t_w + Aeffpanel;
                _Aeff = Aftopeff + Afbottomeff + Aweff;
                #endregion

                #region CALC_EFFECTIVE_PROPERTIES
                _tweff = (Stiffened_2_Panel.Atoteff + (h1w + h6w) * t_w) / (_H);

                if (_tweff > _tw)
                {
                    _tweff = _tw;
                }

                //if (_tweff > (Math.Floor(_tweff) + 0.5))
                //{
                //    _tweff = Math.Floor(_tweff) + 0.5;
                //}
                //else
                //{
                //    _tweff = Math.Floor(_tweff);
                //}

                _tfeff = Math.Min(Aftopeff / _B2, Afbottomeff / _B2);

                if (_tfeff > _tf)
                {
                    _tfeff = _tf;
                }

                //if (_tfeff > (Math.Floor(_tfeff) + 0.5))
                //{
                //    _tfeff = Math.Floor(_tfeff) + 0.5;
                //}
                //else
                //{
                //    _tfeff = Math.Floor(_tfeff);
                //}
                _Aeff = _H * _B2 - (_H - 2 * _tfeff) * (_B2 - 2 * _tweff);



                _Heff = _H /*+ _tf*/;
                _Beff = _B2;
                _IXXeqeff = _Beff * Math.Pow(_Heff, 3.0) / 12.0 - (_Beff - 2 * _tweff) * Math.Pow((_Heff - 2 * _tfeff), 3.0) / 12.0;
                _IYYeqeff = Math.Pow(_Beff, 3.0) * _Heff / 12.0 - Math.Pow((_Beff - 2 * _tweff), 3) * (_Heff - 2 * _tfeff) / 12.0;
                #endregion
            }
            _Alfa_A = _Aeff / _Agross;
        }

        private double RoundDown(double number, int decimalPlaces)
        {
            return Math.Floor(number * Math.Pow(10, decimalPlaces)) / Math.Pow(10, decimalPlaces);
        }

        public void CalcEffectivePropertiesFlexural(int NumStiff, UnstiffenedPanel TopFlangePureCompression, UnstiffenedPanel TopFlangePureBending, UnstiffenedPanel UnstiffWebInPureFlexure, StiffenedPanel StiffWebInPureFlexure, StiffenedPanel StiffWebInPureCompression_1_Stiff, StiffenedPanel StiffWebInPureCompression_2_Stiff, List<FlatStiffener> Stiff_2_StiffenerArray)
        {
            double tfeq = (TopFlangePureCompression.be1 + TopFlangePureCompression.be1 + 2 * _tw + _B4 + _B4) * _tf / (_B1 + _B4 + _B4);
            double[] FirstIterationBoxProps = CalcBoxProperties(_B1, _B2, _B3, _B4, tfeq, _H, _H1, _tw, _stiffNum, _ts, _bs, _a1, _a3);

            double tpse = TopFlangePureCompression.rho * TopFlangePureCompression.t;
            double h1w = 0;
            double h2w = 0;
            double h3w = 0;
            double h4w = 0;
            double h5w = 0;
            double h6w = 0;
            //double htw = 0;
            //double hcw = 0;
            //double hbw = 0;
            double t_w = 0;

            double B2top = 0;
            double B2bottom = 0;
            double t_f = 0;
            //double rhoc = 0;

            //double t_feq = 0;
            //double t_weq = 0;

            //double _Alor = 0;
            //double _SxG = 0;
            //double _SyG = 0;
            //double _Ixx = 0;
            //double _Iyy = 0;
            //double _xG = 0;
            //double _yG = 0;

            double tweqgr = 0;
            double tweqeff = 0;

            /*double tfeqgr1 = 0;
            double tfeqeq1 = 0;
            double tfeqgr2 = 0;
            double tfeqeq2 = 0;*/

            double area = 0;
            double xg = 0;
            double yg = 0;
            //double Ixxeff = 0;
            //double Iyyeff = 0;

            double Aeffpanel = 0;

            if (/*NumStiff == 0 ||*/ NumStiff == 1)
            {

                #region CALC_IXXeff

                // Calcolo IXXeff
                /// Web in Flexure
                h1w = 0;
                h2w = 0;
                h3w = 0;
                h4w = 0;
                h5w = 0;
                h6w = 0;
                t_w = UnstiffWebInPureFlexure.t;

                yg = 0;
                xg = 0;

                /// Top flange in Flexure
                B2top = TopFlangePureCompression.btop + _tw;
                B2bottom = TopFlangePureCompression.bbottom + _tw;
                t_f = TopFlangePureCompression.t;

                double H = _H - _tf;

                // ITER 0
                /// Area Calculation - ITER 0
                area = 0;
                area = area + B2top * t_f + B2bottom * t_f + _B4 * _tf + _B4 * _tf; ///top flange
                area = area + (_B2 * t_f) + (_B1 + _B3) * t_f; /// bottom flange
                area = area + 2 * (H * t_w); /// Web: is not considered the "stiffening" effect of the single stiffener since are on the neutral axis
                area = area + 2 * NumStiff * _ts * _bs; /// Web - stiffeners

                /// Centroid calculation -ITER 0
                yg = 0;
                double d1 = (_H + t_f - t_f / 2);
                yg = yg + (B2top * t_f + B2bottom * t_f) * d1; /// top flange
                double d2 = (t_f / 2);
                yg = yg + ((_B1 + _B2 + _B3) * t_f) * d2; /// bottom flange
                double d3 = (H / 2 + t_f);
                yg = yg + 2 * (H * t_w) * d3; /// web - bbottom
                yg = yg + 2 * NumStiff * (_bs * _ts) * d3; /// web - stiffeners: middle of the web
                yg = yg / area;

                /// Calculation of the Web in flexure (no stiffener considered since is on the neutral axis)
                double ytop = (H - yg + _tf);
                double ybottom = H - ytop;
                double phi = -ybottom / ytop;
                UnstiffenedPanel bWebUST = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tw, H, phi, 4);

                // ITER 1
                h1w = bWebUST.btop;
                h6w = bWebUST.bbottom;

                /// Area Calculation - ITER 1
                area = 0;
                area = area + B2top * t_f + B2bottom * t_f + _B4 * _tf + _B4 * _tf; ///top flange
                area = area + (_B2 * t_f) + (_B1 + _B3) * t_f; /// bottom flange
                area = area + 2 * ((h1w + h6w) * t_w); /// Web: is not considered the "stiffening" effect of the single stiffener since are on the neutral axis
                area = area + 2 * NumStiff * _ts * _bs; /// Web - stiffeners


                /// Centroid calculation - ITER 1
                yg = 0;
                d1 = (_H + t_f - t_f / 2);
                yg = yg + (B2top * t_f + B2bottom * t_f) * d1; /// top flange
                d2 = (t_f / 2);
                yg = yg + ((_B1 + _B2 + _B3) * t_f) * d2; /// bottom flange
                d3 = (H - h1w / 2 + t_f);
                yg = yg + 2 * (h1w * t_w) * d3; /// web - btop
                double d4 = (h6w / 2 + t_f);
                yg = yg + 2 * (h6w * t_w) * d4; /// web - bbottom
                double d5 = (H / 2 + _tf);
                yg = yg + 2 * NumStiff * (_bs * _ts) * d5; /// web - stiffeners: middle of the web
                yg = yg / area;


                /// Effective Inertia Calculation
                _IXXeff = 0.0;
                _IXXeff = _IXXeff + (B2top * Math.Pow(t_f, 3.0) / 12.0 + B2bottom * Math.Pow(t_f, 3) / 12.0); /// top compression flange
                _IXXeff = _IXXeff + (_B1 + _B2 + _B3) * Math.Pow(t_f, 3.0) / 12.0; /// bottom tension flange
                _IXXeff = _IXXeff + 2 * (Math.Pow(h1w, 3.0) * t_w / 12.0 + Math.Pow(h6w, 3.0) * t_w / 12.0);
                _IXXeff = _IXXeff + 2 * NumStiff * (_bs * Math.Pow(_ts, 3.0) / 12.0);

                _IXXeff = _IXXeff + (B2top * t_f + B2bottom * t_f) * Math.Pow((d1 - yg), 2.0);
                _IXXeff = _IXXeff + ((_B1 + _B2 + _B3) * t_f) * Math.Pow((d2 - yg), 2.0);
                _IXXeff = _IXXeff + 2.0 * (h6w * t_w) * Math.Pow(((h6w / 2.0 + t_f) - yg), 2.0);
                _IXXeff = _IXXeff + 2.0 * (h1w * t_w) * Math.Pow(((H - h1w / 2.0 + t_f) - yg), 2.0);
                _IXXeff = _IXXeff + 2.0 * NumStiff * (_bs * _ts) * Math.Pow(((H / 2.0 + t_f) - yg), 2.0);
                #endregion

                #region CALC_IYYeff
                // Calcolo 
                /// Web in Compression
                h1w = StiffWebInPureCompression_1_Stiff.PanelArray[0].btop;
                h2w = 0;
                h3w = 0;
                h4w = 0;
                h5w = 0;
                h6w = StiffWebInPureCompression_1_Stiff.PanelArray[1].bbottom;


                /// Equivalent thickness of the web
                /// Equivalent thickness
                double Awegr = NumStiff * (_bs * _ts) + H * _tw;
                double Aweff = StiffWebInPureCompression_1_Stiff.Atoteff + StiffWebInPureCompression_1_Stiff.PanelArray[0].btop * _tw + StiffWebInPureCompression_1_Stiff.PanelArray[1].bbottom * _tw;
                tweqgr = Awegr / H;
                tweqeff = Aweff / H;

                Aeffpanel = 0;
                Aeffpanel = StiffWebInPureCompression_1_Stiff.Atoteff;

                yg = 0;
                /// Top flange in Flexure
                B2top = TopFlangePureBending.btop;
                B2bottom = TopFlangePureBending.bbottom;
                t_f = TopFlangePureBending.t;

                // ITER 0 
                /// Area Calculation -ITER 0
                area = 0;
                area = area + (_B2 + _B4 + _B4) * _tf; ///top flange
                area = area + (_B1 + _B2 + _B3) * _tf; /// bottom flange
                area = area + (h1w * t_w + h6w * _tw) + Aeffpanel; /// Compression Web
                area = area + H * tweqgr; /// effective area of the stiffened web (the stiffeners area is already contained)

                /// Centroid calculation - ITER 0
                xg = 0;
                xg = (_B2 + _B4 + _B4) * t_f * (_B2 / 2.0 + _B1); /// top flange flexural
                xg = xg + H * tweqgr * (_B1 + _B2 - tweqgr / 2.0); /// tension web
                xg = xg + H * tweqeff * (_B1 + tweqeff / 2.0); /// Compression Web
                xg = xg + t_f / 2.0 * Math.Pow((_B1 + _B2 + _B3), 2); /// 
                xg = xg / area;

                /// Calculation of the flange in flexure
                double ytop1 = (xg - _B1) - tweqeff;
                double ybottom1 = (_B2 - tweqeff - tweqgr) - ytop1;
                double phi1 = -ybottom1 / ytop1;
                UnstiffenedPanel UNFlangeBending = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tf, (_B2 - tweqeff - tweqgr), phi1, 4);

                _IYYeff = 0;
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(UNFlangeBending.btop + tweqeff, 3.0) / 12.0; /// Flange compressione side
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(UNFlangeBending.bbottom + tweqgr, 3.0) / 12.0; /// flange tensione side
                _IYYeff = _IYYeff + H * Math.Pow(tweqgr, 3.0) / 12.0; /// tension web
                _IYYeff = _IYYeff + H * Math.Pow(tweqeff, 3.0) / 12.0; /// compression web
                _IYYeff = _IYYeff + t_f * Math.Pow(_B1, 3.0) / 12.0;
                _IYYeff = _IYYeff + t_f * Math.Pow(_B3, 3.0) / 12.0;
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(_B4, 3.0) / 12.0;

                double test = _B2 - tweqeff - tweqgr;

                double d12 = (UNFlangeBending.btop / 2.0 + _B1 + tweqeff - xg);
                _IYYeff = _IYYeff + 2.0 * (t_f * (UNFlangeBending.btop + tweqeff)) * Math.Pow(d12, 2.0); /// Flange compressione side
                double d22 = ((_B2 - tweqgr - UNFlangeBending.bbottom / 2.0) + _B1 - xg);
                _IYYeff = _IYYeff + 2.0 * (t_f * (UNFlangeBending.bbottom + tweqgr)) * Math.Pow(d22, 2.0); /// flange tensione side
                double d32 = (_B2 + _B1 - tweqgr / 2.0 - xg);
                _IYYeff = _IYYeff + (H * tweqgr) * Math.Pow(d32, 2.0); /// tension web
                double d42 = (_B1 + tweqeff / 2.0 - xg);
                _IYYeff = _IYYeff + (H * tweqeff) * Math.Pow(d42, 2.0); /// compression web
                double d52 = (_B1 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B1) * Math.Pow(d52, 2.0);
                double d6 = (_B1 + _B2 + _B3 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B3) * Math.Pow(d6, 2.0);
                double d7 = (_B4 / 2.0 - _B1 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B4) * Math.Pow(d7, 2.0);
                double d8 = (_B4 / 2.0 + _B1 + _B2 - xg);
                _IYYeff = _IYYeff + (t_f * _B4) * Math.Pow(d8, 2.0);
                #endregion

            }
            else if (NumStiff == 2)
            {
                #region CALC_IXX_EFF
                h1w = UnstiffWebInPureFlexure.btop;
                h2w = 0;
                h3w = 0;
                h4w = 0;
                h5w = 0;
                h6w = UnstiffWebInPureFlexure.bbottom;
                t_w = _tw;
                t_f = _tf;

                /// Top flange in Flexure
                B2top = TopFlangePureCompression.btop + _tw;
                B2bottom = TopFlangePureCompression.bbottom + _tw;
                t_f = TopFlangePureCompression.t;

                double H = _H - _tf;

                //ITER 0
                /// Area Calculation - ITER 0
                area = 0;
                area = area + B2top * t_f + B2bottom * t_f + _B4 * _tf + _B4 * _tf; ///top flange
                area = area + (_B2 * t_f) + (_B1 + _B3) * t_f; /// bottom flange
                area = area + 2 * (H * t_w); /// Web: is not considered the "stiffening" effect of the single stiffener since are on the neutral axis
                area = area + 2 * NumStiff * _ts * _bs; /// Web - stiffeners

                /// Centroid calculation - ITER 0
                yg = 0;
                double d1 = (_H + t_f - t_f / 2);
                yg = yg + (B2top * t_f + B2bottom * t_f) * d1; /// top flange
                double d2 = (t_f / 2);
                yg = yg + ((_B1 + _B2 + _B3) * t_f) * d2; /// bottom flange
                double d3 = (H / 2 + t_f);
                yg = yg + 2 * (H * t_w) * d3; /// web - bbottom
                yg = yg + 2 * NumStiff * (_bs * _ts) * d3; /// web - stiffeners: middle of the web
                yg = yg / area;

                /// Stiffened Web in compression
                double a1 = _a1;
                double a2 = (_H - _tf - _a1 - _a3);
                double a3 = _a3;

                double psiP1 = ((a2 + a1 + _tf) - yg) / ((_H - _tf) - yg); /// Panel 1
                double psiP2 = -(yg - (a1 + _tf)) / (a1 + a2 + _tf - yg); /// Panel 2
                double psiP3 = (yg - (a3 + _tf)) / (yg - _tf);  /// Panel 3
                double psiP4 = -((a2 + a3 + _tf) - yg) / (yg - _tf); /// Panel 4 (lumped)
                double psiALL = -(yg - _tf) / (_H - yg); /// all the web

                List<UnstiffenedPanel> PanelArray = new List<UnstiffenedPanel>();
                UnstiffenedPanel Panel1 = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tw, a1, psiP1, 4);
                PanelArray.Add(Panel1);
                UnstiffenedPanel Panel2 = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tw, a2, psiP2, 4);
                PanelArray.Add(Panel2);
                UnstiffenedPanel Panel3 = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tw, a3, psiP3, 4);
                PanelArray.Add(Panel3);
                UnstiffenedPanel Panel4 = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tw, a2 + a3, psiP4, 4);
                PanelArray.Add(Panel4);

                List<FlatStiffener> StiffenerArray = new List<FlatStiffener>();

                double P1bottom = Math.Min(Panel1.bbottom, Panel1.b / 2);
                double P2top = Math.Min(Panel2.btop, Panel2.b / 2);
                FlatStiffener Stiff1 = new FlatStiffener(_code, _ts, _bs, Panel1.t, Panel1.b, Panel2.b, P1bottom, P2top, _Ldiaf, StiffenerLongitudinalType.Open);
                StiffenerArray.Add(Stiff1);
                double P2bottom = Math.Min(Panel2.bbottom, Panel2.b / 2);
                double P3top = Math.Min(Panel3.btop, Panel3.b / 2);
                FlatStiffener Stiff2 = new FlatStiffener(_code, _ts, _bs, Panel2.t, Panel2.b, Panel3.b, P2bottom, P3top, _Ldiaf, StiffenerLongitudinalType.Open);
                StiffenerArray.Add(Stiff2);
                P1bottom = Math.Min(Panel1.bbottom, Panel1.b / 2);
                double P4top = Math.Min(Panel4.btop, Panel4.b / 2);
                FlatStiffener Stiff3 = new FlatStiffener(_code, _ts, _bs, Panel4.t, Panel1.b, Panel3.b, P1bottom, P4top, _Ldiaf, StiffenerLongitudinalType.Open);
                StiffenerArray.Add(Stiff3);
                StiffenedPanel bWEBST = new StiffenedPanel(_code, 2, PanelArray, StiffenerArray, psiALL); /// Stiffened Panel
                double rho = bWEBST.Rhoc;

                //ITER 1
                h1w = Panel1.btop;
                h2w = Panel1.bbottom;
                h3w = Panel2.btop;
                h4w = Panel2.bbottom;
                h5w = Panel3.btop;
                h6w = Panel3.bbottom;

                double heff = h2w + h3w;
                double hedge = h1w + h4w + h5w + h6w;

                /// Area Calculation - ITER 1
                area = 0;
                area = area + B2top * t_f + B2bottom * t_f + _B4 * _tf + _B4 * _tf; ///top flange
                area = area + (_B2 * t_f) + (_B1 + _B3) * t_f; /// bottom flange
                area = area + 2 * (heff * t_w * rho); /// Web on the compressed stiffener
                area = area + 2 * (hedge * t_w);  /// Web on the tension stiffener
                area = area + NumStiff * _ts * _bs;  /// Web - stiffeners in tension side
                area = area + NumStiff * _ts * _bs * rho; /// Web - stiffeners compressed

                /// Centroid calculation - ITER 1
                yg = 0;
                double h1 = (_H + t_f - t_f / 2);
                yg = yg + (B2top * t_f + B2bottom * t_f) * h1; /// top flange
                double h2 = (t_f / 2);
                yg = yg + ((_B1 + _B2 + _B3) * t_f) * h2; /// bottom flange

                double h3 = (H - h1w / 2 + _tf);
                yg = yg + 2 * (h1w * t_w) * h3; /// hw1

                double h4 = (a2 + a3 + h2w / 2 + _tf);
                yg = yg + 2 * (h2w * t_w * rho) * h4; /// hw2

                double h5 = (a2 + a3 - h3w / 2 + _tf);
                yg = yg + 2 * (h3w * t_w * rho) * h5; /// hw3

                double h6 = (a3 + h4w / 2 + _tf);
                yg = yg + 2 * (h4w * t_w) * h6; /// hw4

                double h7 = (a3 - h5w / 2 + _tf);
                yg = yg + 2 * (h5w * t_w) * h7; /// hw5

                double h8 = (h6w / 2 + _tf);
                yg = yg + 2 * (h6w * t_w) * h8; /// hw6

                double h9 = (a2 + a3 + _tf);
                yg = yg + NumStiff * (_bs * _ts) * h9 * rho; /// Compressed stiffeners

                double h10 = (a3 + _tf);
                yg = yg + NumStiff * (_bs * _ts) * h10; /// Compressed stiffeners

                yg = yg / area;

                /// Calcolo IXXeff (About horizontal X-X axis)
                _IXXeff = 0;
                _IXXeff = _IXXeff + B2top * Math.Pow(_tf, 3.0) / 12.0 + B2bottom * Math.Pow(_tf, 3.0) / 12.0 + _B4 * Math.Pow(_tf, 3.0) / 12.0 + _B4 * Math.Pow(_tf, 3.0) / 12.0; /// top flange
                _IXXeff = _IXXeff + (_B1 + _B2 + _B3) * Math.Pow(_tf, 3.0) / 12.0; /// bottom flange
                _IXXeff = _IXXeff + 2 * Math.Pow(h1w, 3.0) * t_w / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(h2w, 3.0) * t_w * rho / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(h3w, 3.0) * t_w * rho / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(h4w, 3.0) * t_w / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(h5w, 3.0) * t_w / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(h6w, 3.0) * t_w / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(_ts, 3.0) * _bs / 12.0;
                _IXXeff = _IXXeff + 2 * Math.Pow(_ts, 3.0) * _bs * rho / 12.0;

                _IXXeff = _IXXeff + (B2top * t_f + B2bottom * _tf) * Math.Pow((h1 - yg), 2.0); /// top flange
                _IXXeff = _IXXeff + ((_B1 + _B2 + _B3) * _tf) * Math.Pow((h2 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * (h1w * _tw) * Math.Pow((h3 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * rho * (h2w * _tw) * Math.Pow((h4 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * rho * (h3w * _tw) * Math.Pow((h5 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * (h4w * _tw) * Math.Pow((h6 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * (h5w * _tw) * Math.Pow((h7 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * (h6w * _tw) * Math.Pow((h8 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * rho * (_bs * _ts) * Math.Pow((h9 - yg), 2.0);
                _IXXeff = _IXXeff + 2 * (_bs * _ts) * Math.Pow((h10 - yg), 2.0);
                #endregion


                #region CALC_IYY_EFF
                /// Area Calculation
                h1w = StiffWebInPureCompression_2_Stiff.PanelArray[0].btop;
                h2w = 0;
                h3w = 0;
                h4w = 0;
                h5w = 0;
                h6w = StiffWebInPureCompression_2_Stiff.PanelArray[2].bbottom;

                double Awegr = (2 * _bs * _ts + (_H - t_f) * t_w); /// gross area of the tension web
                double Aweff = (h1w + h6w) * t_w + StiffWebInPureCompression_2_Stiff.Atoteff; /// effective area of the compression web
                double Afeff = (_B2 + _B4 + _B4) * t_f + (_B2 + _B1 + _B3) * t_f; /// gross area of the flanges
                                                                                  /// Equivalent thickness
                H = (_H - t_f);
                tweqgr = Awegr / H;
                tweqeff = Aweff / H;

                /// Area Calculation
                area = 0;
                area = area + Awegr + Aweff;
                area = area + Afeff;

                /// Centroid calculation
                xg = 0;
                xg = (_B2 + _B4 + _B4) * t_f * (_B2 / 2.0 + _B1); /// top flange flexural
                xg = xg + H * tweqgr * (_B1 + _B2 - tweqgr / 2.0); /// tension web
                xg = xg + H * tweqeff * (_B1 + tweqeff / 2.0); /// Compression Web
                xg = xg + t_f / 2.0 * Math.Pow((_B1 + _B2 + _B3), 2); /// 
                xg = xg / area;

                /// Calculation of the flange in flexure
                double ytop = (xg - _B1) - tweqeff;
                double ybottom = (_B2 - tweqeff - tweqgr) - ytop;
                double phi = -ybottom / ytop;
                UnstiffenedPanel UNFlangeBending = new UnstiffenedPanel(Code.ECP205_2001_ASD, _tf, (_B2 - tweqeff - tweqgr), phi, 4);

                _IYYeff = 0;
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(UNFlangeBending.btop + tweqeff, 3.0) / 12.0; /// Flange compressione side
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(UNFlangeBending.bbottom + tweqgr, 3.0) / 12.0; /// flange tensione side
                _IYYeff = _IYYeff + H * Math.Pow(tweqgr, 3.0) / 12.0; /// tension web
                _IYYeff = _IYYeff + H * Math.Pow(tweqeff, 3.0) / 12.0; /// compression web
                _IYYeff = _IYYeff + t_f * Math.Pow(_B1, 3.0) / 12.0;
                _IYYeff = _IYYeff + t_f * Math.Pow(_B3, 3.0) / 12.0;
                _IYYeff = _IYYeff + 2.0 * t_f * Math.Pow(_B4, 3.0) / 12.0;

                double test = _B2 - tweqeff - tweqgr;

                double d12 = (UNFlangeBending.btop / 2.0 + _B1 + tweqeff - xg);
                _IYYeff = _IYYeff + 2.0 * (t_f * (UNFlangeBending.btop + tweqeff)) * Math.Pow(d12, 2.0); /// Flange compressione side
                double d22 = ((_B2 - tweqgr - UNFlangeBending.bbottom / 2.0) + _B1 - xg);
                _IYYeff = _IYYeff + 2.0 * (t_f * (UNFlangeBending.bbottom + tweqgr)) * Math.Pow(d22, 2.0); /// flange tensione side
                double d32 = (_B2 + _B1 - tweqgr / 2.0 - xg);
                _IYYeff = _IYYeff + (H * tweqgr) * Math.Pow(d32, 2.0); /// tension web
                double d4 = (_B1 + tweqeff / 2.0 - xg);
                _IYYeff = _IYYeff + (H * tweqeff) * Math.Pow(d4, 2.0); /// compression web
                double d5 = (_B1 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B1) * Math.Pow(d5, 2.0);
                double d6 = (_B1 + _B2 + _B3 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B3) * Math.Pow(d6, 2.0);
                double d7 = (_B4 / 2.0 - _B1 / 2.0 - xg);
                _IYYeff = _IYYeff + (t_f * _B4) * Math.Pow(d7, 2.0);
                double d8 = (_B4 / 2.0 + _B1 + _B2 - xg);
                _IYYeff = _IYYeff + (t_f * _B4) * Math.Pow(d8, 2.0);
                #endregion
            }
        }

        private void CalcBoxGrossProperties()
        {
            double[] Properties = new double[5];
            Properties = CalcBoxProperties(_B1, _B2, _B3, _B4, _tf, _H, _H1, _tw, _stiffNum, _ts, _bs, _a1, _a3);
            _Agross = Properties[0];
            _IXXgross = Properties[3];
            _IYYgross = Properties[4];
        }

        public double[] CalcBoxProperties(double B1, double B2, double B3, double B4, double tf, double H, double H1, double tw, int stiffNum, double ts, double bs, double a1, double a3)
        {
            double[] Properties = new double[5];

            double _Alor = 0;
            double _SxG = 0;
            double _SyG = 0;
            double _Ixx = 0;
            double _Iyy = 0;
            double _xG = 0;
            double _yG = 0;


            /// Area Calculation
            _Alor = (B2 + 2 * B4) * tf + (B2 + B1 + B3) * tf + 2 * (H - tf) * tw + 2 * H1 * tw + 2 * _stiffNum * ts * bs;


            if (_stiffNum == 2)
            {
                /// Centroid along Y axis
                _SxG = 0.0;
                _SxG = _SxG + B2 * tf * ((H - tf) + tf + tf / 2.0); /// Top flange
                _SxG = _SxG + ((B2 + B1 + B3) * tf) * (tf / 2.0); /// Bottom flange
                _SxG = _SxG + 2.0 * ((H - tf) * tw) * (tf + (H - tf) / 2); /// Webs
                _SxG = _SxG + 2.0 * (H1 * tw) * (-H1 / 2.0); /// H1 pieces
                _SxG = _SxG + 2.0 * (ts * bs) * ((H - tf - a1) + tf); /// Stiffeners near top flange
                _SxG = _SxG + 2.0 * (ts * bs) * (a3 + tf); /// Stiffeners near top flange
                _yG = _SxG / _Alor;

                /// Centroid along X axis
                _SyG = 0.0;
                _SyG = _SyG + ((H - tf + H1) * tw) * (tw / 2 + (B2 - tw)); /// Web SX
                _SyG = _SyG + ((H - tf + H1) * tw) * (tw / 2); /// Web DX
                _SyG = _SyG + 2 * (B2 * tf) * (B2 / 2); /// Flanges
                _SyG = _SyG + (B1 * tf) * (B1 / 2 + B2); /// B1 flange
                _SyG = _SyG + (B3 * tf) * (-B3 / 2); /// B3 flange
                _SyG = _SyG + (B4 * tf) * (B4 / 2 + B2); /// B4 X (-) flange
                _SyG = _SyG + (B4 * tf) * (-B4 / 2); /// B4 X (-) flange
                _SyG = _SyG + 2.0 * (ts * bs) * ((B2 - tf - bs / 2)); /// X(-) stiffeners
                _SyG = _SyG + 2.0 * (ts * bs) * (bs / 2 + tf); /// X(-) stiffeners
                _xG = _SyG / _Alor;

                /// Inertia around XX axis
                _Ixx = 0.0;
                double Htoty = H + tf;
                double dist1y = (Htoty - _yG - tf / 2);
                _Ixx = _Ixx + 1.0 / 12.0 * (B2 + B4 + B4) * Math.Pow(tf, 3.0) + ((B2 + B4 + B4) * tf) * Math.Pow(dist1y, 2); /// Top Flange
                double dist2y = (_yG - tf / 2.0);
                _Ixx = _Ixx + 1.0 / 12.0 * (B2 + B1 + B3) * Math.Pow(tf, 3.0) + ((B2 + B1 + B3) * tf) * Math.Pow(dist2y, 2); /// Bottom Flange
                double dist3y = ((H - tf) / 2.0 - _yG + tf);
                _Ixx = _Ixx + 2.0 * 1.0 / 12.0 * (tw) * Math.Pow((H - tf), 3.0) + 2.0 * ((H - tf) * tw) * Math.Pow(dist3y, 2); /// Webs
                double dist4y = (H1 / 2 + _yG);
                _Ixx = _Ixx + 2.0 * 1.0 / 12.0 * (tw) * Math.Pow(H1, 3.0) + 2.0 * (H1 * tw) * Math.Pow(dist4y, 2); /// Additional Web H1
                double dist5y = (((H - tf - a1) + tf) - _yG);
                _Ixx = _Ixx + 2.0 * 1.0 / 12.0 * (bs) * Math.Pow(ts, 3.0) + 2.0 * (ts * bs) * Math.Pow(dist5y, 2.0); /// Top flange stiffeners
                double disty = (a3 + tf - _yG);
                _Ixx = _Ixx + 2.0 * 1.0 / 12.0 * (bs) * Math.Pow(ts, 3.0) + 2.0 * (ts * bs) * Math.Pow(disty, 2.0); /// Bottom flange stiffeners

                /// Inertia around YY axis
                _Iyy = 0;
                double Htotx2 = B2;
                double dist1x = (B2 - tw / 2) - _xG;
                _Iyy = _Iyy + 1.0 / 12.0 * (H - tf + H1) * Math.Pow(tw, 3.0) + ((H - tf + H1) * tw) * Math.Pow(dist1x, 2.0); /// Web SX
                double dist2x = _xG - tw / 2;
                _Iyy = _Iyy + 1.0 / 12.0 * (H - tf + H1) * Math.Pow(tw, 3.0) + ((H - tf + H1) * tw) * Math.Pow(dist2x, 2.0); /// Web DX
                double dist3x = B2 / 2 - _xG;
                _Iyy = _Iyy + 2.0 * 1.0 / 12.0 * (tf) * Math.Pow(B2, 3.0) + 2 * (B2 * tf) * Math.Pow(dist3x, 2.0); /// Flanges
                double dist4x = B1 / 2 + B2 - _xG;
                _Iyy = _Iyy + 1.0 / 12.0 * (tf) * Math.Pow(B1, 3.0) + (B1 * tf) * Math.Pow(dist4x, 2.0); /// Additiona B1
                double dist5x = _xG + B3 / 2;
                _Iyy = _Iyy + 1.0 / 12.0 * (tf) * Math.Pow(B3, 3.0) + (B3 * tf) * Math.Pow(dist5x, 2.0); /// Additional B3
                double dist6x = (B2 - tw - bs / 2) - _xG;
                _Iyy = _Iyy + 2.0 * 1.0 / 12.0 * (ts) * Math.Pow(bs, 3.0) + 2 * (bs * ts) * Math.Pow(dist6x, 2.0); /// Stiffeners
                double dist7x = _xG - tw - bs / 2;
                _Iyy = _Iyy + 2.0 * 1.0 / 12.0 * (ts) * Math.Pow(bs, 3.0) + 2 * (bs * ts) * Math.Pow(dist7x, 2.0); /// Stiffeners 
            }


            Properties[0] = _Alor;
            Properties[1] = _xG;
            Properties[2] = _yG;
            Properties[3] = _Ixx;
            Properties[4] = _Iyy;

            return Properties;
        }

        #region CONSTRUCTORS
        #endregion

        #region DECONSTRUCTORS
        #endregion

        #region METHODS
        #endregion

        #region VARIABLES
        private double _Agross;
        private double _Aeff;
        private double _IXXgross;
        private double _IXXeff;
        private double _IYYgross;
        private double _IYYeff;

        private double _Alfa_A;
        private double _Alfa_Ixx;
        private double _Alfa_Iyy;

        private double _xgeff;
        /*
        private double _ygeff;
        */

        private double _Heff = 0;
        private double _Beff = 0;
        private double _tfeff = 0;
        private double _tweff = 0;
        private double _IXXeqeff;
        private double _IYYeqeff;


        private Code _code;
        private double _B1;
        private double _B2;
        private double _B3;
        private double _B4;
        private double _tf;
        private double _H;
        private double _H1;
        private double _tw;
        private int _stiffNum;
        private double _ts;
        private double _bs;
        private double _a1;
        private double _a3;
        private double _Ldiaf;

        private bool _IsUnstiffened;

        #endregion

        #region PROPERTIES
        public double Agross
        {
            get => _Agross;
            private set => _Agross = value;
        }
        public double Aeff
        {
            get => _Aeff;
            private set => _Aeff = value;
        }
        public double IXXgross
        {
            get => _IXXgross;
            private set => _IXXgross = value;
        }
        public double IXXeff
        {
            get => _IXXeff;
            private set => _IXXeff = value;
        }
        public double IYYgross
        {
            get => _IYYgross;
            private set => _IYYgross = value;
        }
        public double IYYeff
        {
            get => _IYYeff;
            private set => _IYYeff = value;
        }
        public double Alfa_A
        {
            get => _Alfa_A;
            private set => _Alfa_A = value;
        }
        public double Alfa_Ixx
        {
            get => _Alfa_Ixx;
            private set => _Alfa_Ixx = value;
        }
        public double Alfa_Iyy
        {
            get => _Alfa_Iyy;
            private set => _Alfa_Iyy = value;
        }
        public double Heff
        {
            get => _Heff;
            private set => _Heff = value;
        }
        public double Beff
        {
            get => _Beff;
            private set => _Beff = value;
        }
        public double tfeff
        {
            get => _tfeff;
            private set => _tfeff = value;
        }
        public double tweff
        {
            get => _tweff;
            private set => _tweff = value;
        }
        public double IXXeqeff
        {
            get => _IXXeqeff;
            private set => _IXXeqeff = value;
        }
        public double IYYeqeff
        {
            get => _IYYeqeff;
            private set => _IYYeqeff = value;
        }
        #endregion
    }


}

#endif