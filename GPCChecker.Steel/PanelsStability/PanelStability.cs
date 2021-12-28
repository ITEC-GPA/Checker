/*  ========================================================================= *\
   Project:    GPC Checker - Class4 - Panels Stability
   Date:       02/002/2019

	File:      
	Author:    Vochescu Robert


    Overview:  Class 4 - Plate Effective Section Calculation
\* ========================================================================= */

#if NEVER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.PanelsStability
{
    public class PanelEffectiveProperties
    {
        #region Variables
        protected double _teff;
        protected double _beff;
        protected double _aeff;
        protected double _chic;
        protected double _rhop;
        protected double _rhoc;
        #endregion

        #region Properties
        public  double teff => _teff;
        public double Beff => _beff;
        public double Aeff => _aeff;
        public double Chic => _chic;
        public double Rhop => _rhop;
        public double Rhoc => _rhoc;
        #endregion

        #region Public Constructors
        public PanelEffectiveProperties ()
        {

        }
        #endregion

        #region Public Methods Specific
        #endregion


        //public static double[] FlatStiffener(Code code, double ts, double bs, double t, double b1, double b2, double b1eff, double b2eff, double Ldiaf, StiffenerType stiffType, double fy = 355, double E = 210000, double ni = 0.3)
        //{
        //    double[] OutputParameters = new double[19];

        //    double alfainstC = 0;
        //    if (stiffType == StiffenerType.Closed)
        //    {
        //        alfainstC = 0.34;
        //    }
        //    else if (stiffType == StiffenerType.Open)
        //    {
        //        alfainstC = 0.49;
        //    }

        //    double pi = Math.PI;

        //    // Stiffener Static Properties (alone)
        //    double Ist = (Math.Pow(bs, 3.0) * ts) / 12.0;
        //    double Ast = bs * ts;
        //    double Itst = bs * Math.Pow(ts, 3) / 3;
        //    double Ipst = Math.Pow(bs, 3) * ts / 3 + bs * Math.Pow(ts, 3) / 12;

        //    // Stiffener Static Properties with AdicentParts
        //    /// Gross Properties
        //    double hwgr = b1 / 2 + b2 / 2;
        //    double Airrgr = (b1 + b2) * t / 2 + Ast;
        //    double yirrgr = (hwgr * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / Airrgr;
        //    double Iirrgr = (Ist + Ast * Math.Pow((bs / 2 + t - yirrgr), 2)) + (hwgr * Math.Pow(t, 3) / 12 + hwgr * t * Math.Pow((t / 2 - yirrgr), 2));
        //    double rIrrgr = Math.Sqrt(Iirrgr / Airrgr);

        //    /// Effective Properties
        //    double hw1eff = b1eff + b2eff;
        //    double Aw1eff = hw1eff * t;
        //    double Airreff = Aw1eff + Ast;
        //    double yirreff = (hw1eff * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / Airreff;
        //    double Iirreff = (Ist + Ast * Math.Pow((bs / 2 + t - yirreff), 2)) + (hw1eff * Math.Pow(t, 3) / 12 + hw1eff * t * Math.Pow((t / 2 - yirreff), 2));
        //    double rIrreff = Math.Sqrt(Iirreff / Airreff);

        //    string CheckStiffener = "";
        //    double stLimTorsional = 5.3 * fy * E;
        //    if ((Itst / Ipst) >= stLimTorsional)
        //    {
        //        CheckStiffener = "Stiffness torsional requirement checked";
        //    }
        //    else
        //    {
        //        CheckStiffener = "Stiffness torsional requirement NOT checked";
        //    }

        //    /// COLUMN-LIKE BUCKLING
        //    double Lambdac = Ldiaf / rIrrgr;
        //    double SigmaCrc = (Math.Pow(pi, 2.0) * E) / Math.Pow(Lambdac, 2.0);
        //    double e1 = yirrgr - (t / 2.0);
        //    double e2 = t + (bs / 2.0) - yirrgr;
        //    double e = Math.Max(e1, e2);
        //    double alfae = alfainstC + 0.09 / (rIrrgr / e);
        //    double lambdacIrr = Math.Sqrt((fy * Airreff) / (SigmaCrc * Airrgr));
        //    double FiInst = 0.5 * (1.0 + (alfae * (lambdacIrr - 0.2)) + Math.Pow(lambdacIrr, 2.0));
        //    double Chic = 1.0 / (FiInst + Math.Sqrt(Math.Pow(FiInst, 2.0) - Math.Pow(lambdacIrr, 2.0)));

        //    OutputParameters[0] = ts;     ///ts
        //    OutputParameters[1] = bs;     ///bs
        //    OutputParameters[2] = Ast;     ///Ast
        //    OutputParameters[3] = Ist;     ///Ist
        //    OutputParameters[4] = Itst;     ///Itst
        //    OutputParameters[5] = Ipst;     ///Ipst
        //    OutputParameters[6] = yirrgr;     ///yirrgr
        //    OutputParameters[7] = Airreff;     ///Airreff
        //    OutputParameters[8] = Airrgr;     ///Airrgr
        //    OutputParameters[9] = Iirrgr;     ///Iirrgr
        //    OutputParameters[10] = rIrrgr;     ///rIrrgr
        //    OutputParameters[11] = Lambdac;     ///Lambdac
        //    OutputParameters[12] = SigmaCrc;     ///SigmaCrc
        //    OutputParameters[13] = lambdacIrr;     ///lambdacIrr
        //    OutputParameters[14] = FiInst;     ///FiInst
        //    OutputParameters[15] = e1;     ///e1
        //    OutputParameters[16] = e2;     ///e2
        //    OutputParameters[17] = alfae;     ///alfae
        //    OutputParameters[18] = Chic;     ///Chic

        //    return OutputParameters;
        //}

        public static double[] Unstiffened_PlateCalc(Code code, double t, double b, double phi, double ksigma, double fy = 355, double E = 210000, double ni = 0.3)
        {
            double[] Output = new double[6];

            double beff = b;
            double beff1 = 0;
            //double pi = Math.PI;
            double epsilon = Math.Sqrt(235 / fy);

            double rho = 1;
            double lambdap = 1;

            double be1 = 0;
            double be2 = 0;

            double ytop = 0;
            double ybottom = 0;

            double btop = 0;
            double bbottom = 0;

            /// Sub-Panel maximum lenght
            if (code == Code.Eurocode && ksigma == 4)
            {
                double blimEC3 = 42.0 * epsilon * t;
                if (b > blimEC3)
                {
                    beff1 = 42.0 * epsilon * t;
                }
            }
            else if (code == Code.ECP205_2001_ASD && ksigma == 4)
            {
                double blimECP205 = (64.0 * (t / 10) / Math.Sqrt(fy / 98.07)) * 10;
                if (b > blimECP205)
                {
                    beff1 = (64.0 * (t / 10) / Math.Sqrt(fy / 98.07)) * 10;
                }
            }


            if (beff1 <= b)
            {
                /// Generic Values
                double lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * phi); ;

                /// Sub-Panel properties

                if (code == Code.Eurocode)
                {
                    lambdap = (b / t) / (28.4 * epsilon * Math.Sqrt(ksigma));
                }
                else if (code == Code.ECP205_2001_ASD)
                {
                    lambdap = (b / t) * Math.Sqrt((fy / 98.07) / ksigma) / 44;
                }

                if (code == Code.Eurocode)
                {
                    if (lambdap > lambdapLIM)
                    {
                        rho = (lambdap - 0.055 * (3 + phi)) / Math.Pow(lambdap, 2.0);
                    }
                    else
                    {
                        rho = 1;
                    }
                }
                else if (code == Code.ECP205_2001_ASD)
                {
                    rho = ((lambdap - 0.15) - 0.05 * phi) / Math.Pow(lambdap, 2.0);

                    if (rho > 1)
                    {
                        rho = 1;
                    }
                }

                beff = rho * b;
            }


            if (phi >= 0)
            {
                ybottom = 0;
                ytop = b;
                be1 = 2 / (5 - phi) * beff;
                be2 = beff - be1;
                btop = be1;
                bbottom = be2;
            }
            else if (phi < 0)
            {
                ybottom = b * Math.Abs(phi) / (1 + Math.Abs(phi));
                ytop = b - ybottom;
                be1 = 0.4 * beff;
                be2 = 0.6 * beff;
                btop = be1;
                bbottom = be2 + ybottom;
            }

            Output[0] = beff;
            Output[1] = rho;
            Output[2] = be1;
            Output[3] = be2;
            Output[4] = btop;
            Output[5] = bbottom;

            return Output;
        }

        public static double[] Stiffened_1FlatStiffenerMiddle_PlateCalc(Code code, double t, double b, double ts, double bs, double Ldiaf, double phi, double ksigma, StiffenerLongitudinalType stiffType, double fy = 355, double E = 210000, double ni = 0.3)
        {
            double[] OutputParameters = new double[36];

            double alfainstC = 0;
            if (stiffType == StiffenerLongitudinalType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (stiffType == StiffenerLongitudinalType.Open)
            {
                alfainstC = 0.49;
            }

            //double rho = 0;
            double pi = Math.PI;
            double epsilon = Math.Sqrt(235 / fy);

            /// Sub-Panel maximum lenght
            if (code == Code.Eurocode)
            {
                if (bs > 14 * epsilon * ts)
                {
                    bs = 14 * epsilon * ts;
                }
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                if (bs > (21 * ts * Math.Sqrt(fy / 98.07)) * 10)
                {
                    bs = (21 * ts * Math.Sqrt(fy / 98.07)) * 10;
                }
            }


            /// Generic Values
            double lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * phi); ;

            /// Sub-Panel properties
            double lambdap1 = 1;
            if (code == Code.Eurocode)
            {
                lambdap1 = ((b / 2.0) / t) / (28.4 * epsilon * Math.Sqrt(ksigma));
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                lambdap1 = ((b / 2.0) / t) * Math.Sqrt(fy * 98.07 / ksigma) / 44;
            }

            double rho1 = 1;
            if (code == Code.Eurocode)
            {
                if (lambdap1 > lambdapLIM)
                {
                    rho1 = (lambdap1 - 0.055 * (3 + phi)) / Math.Pow(lambdap1, 2.0);
                }
                else
                {
                    rho1 = 1;
                }
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                rho1 = ((lambdap1 - 0.15) - 0.05 * phi) / Math.Pow(lambdap1, 2.0);

                if (rho1 > 1)
                {
                    rho1 = 1;
                }
            }

            /// Stiffener Static Properties (alone)
            double Isl = (Math.Pow(bs, 3.0) * ts) / 12.0;
            double Aslg = bs * ts;

            double Itst = bs * Math.Pow(ts, 3) / 3;
            double Ipst = Math.Pow(bs, 3) * ts / 3 + bs * Math.Pow(ts, 3) / 12;
            double b1 = bs;
            double hw1 = b / 2.0;
            double hw1eff = (rho1 * b) / 2.0;
            double Aw1eff = hw1eff * t;
            double Airreff = Aw1eff + Aslg;
            double Airrgr = b * t / 2 + Aslg;

            double yirrgr = (hw1 * t * t / 2.0 + bs * ts * (b1 / 2.0 + t)) / Airrgr;
            double Iirrgr = (Isl + Aslg * Math.Pow((bs / 2 + t - yirrgr), 2)) + (hw1 * Math.Pow(t, 3) / 12 + hw1 * t * Math.Pow((t / 2 - yirrgr), 2));
            double rIrrgr = Math.Sqrt(Iirrgr / Airrgr);

            double yirreff = (hw1eff * t * t / 2.0 + bs * ts * (b1 / 2.0 + t)) / Airreff;
            double Iirreff = (Isl + Aslg * Math.Pow((bs / 2 + t - yirreff), 2)) + (hw1eff * Math.Pow(t, 3) / 12 + hw1eff * t * Math.Pow((t / 2 - yirreff), 2));
            double rIrreff = Math.Sqrt(Iirreff / Airreff);

            //string CheckStiffener;
            double stLimTorsional = 5.3 * fy * E;
            if ((Itst / Ipst) >= stLimTorsional)
            {
                //CheckStiffener = "Stiffness torsional requirement checked";
            }
            else
            {
                //CheckStiffener = "Stiffness torsional requirement NOT checked";
            }

            /// COLUMN-LIKE BUCKLING
            double Lambdac = Ldiaf / rIrrgr;
            double SigmaCrc = (Math.Pow(pi, 2.0) * E) / Math.Pow(Lambdac, 2.0);
            double e1 = yirrgr - (t / 2.0);
            double e2 = t + (bs / 2.0) - yirrgr;
            double e = Math.Max(e1, e2);
            double alfae = alfainstC + 0.09 / (rIrrgr / e);
            double lambdacIrr = Math.Sqrt((fy * Airreff) / (SigmaCrc * Airrgr));
            double FiInst = 0.5 * (1.0 + (alfae * (lambdacIrr - 0.2)) + Math.Pow(lambdacIrr, 2.0));
            double Chic = 1.0 / (FiInst + Math.Sqrt(Math.Pow(FiInst, 2.0) - Math.Pow(lambdacIrr, 2.0)));

            /// PLATE-LIKE BUCKLING
            double b11 = b / 2.0;
            double b22 = b11;
            double alfaC = 4.33 * Math.Pow((((Iirrgr * Math.Pow(b11, 2.0)) * Math.Pow(b22, 2.0)) / Math.Pow(t, 3.0)) / b, 0.25);
            double ab = Ldiaf / b;
            double SigmaCrp = 0;
            double Sigmacsl = 0;
            double Sigmacsl1 = ((1.05 * E) / Airrgr) * (Math.Sqrt((Iirrgr * Math.Pow(t, 3.0)) * b)) / (b11 * b22);
            double Sigmacsl2 = (Math.Pow(pi, 2) * E * Iirrgr / (Airrgr * Math.Pow(Ldiaf, 2))) + (E * Math.Pow(t, 3) * b * Math.Pow(Ldiaf, 2)) / (4 * Math.Pow(pi, 2) * (1 - ni * ni) * Airrgr * (b11 * b11) * (b22 * b22));
            //double Sigmacsl2 = SigmaCrc + (E * Math.Pow(tw, 3) * hw * Math.Pow(Ldiaf, 2)) / (4 * Math.Pow(pi, 2) * (1 - ni * ni) * Airrgr * (b11 * b11) * (b22 * b22));
            //double Sigmacsl2 = (E * Math.Pow(tw, 3) * hw * Math.Pow(Ldiaf, 2)) ;
            //double Sigmacsl2 =  (4 * Math.Pow(pi, 2) * (1 - ni * ni) * Airrgr * (b11 * b11) * (b22 * b22));


            if (Ldiaf >= alfaC)
            {
                Sigmacsl = Sigmacsl1;
                SigmaCrp = Sigmacsl;
            }
            else
            {
                Sigmacsl = Sigmacsl2;
                SigmaCrp = Sigmacsl;
            }

            double BetaAc = Airreff / Airrgr;
            double LambdaP = Math.Sqrt(BetaAc * fy / SigmaCrp);

            double rhop = 1;
            if (code == Code.Eurocode)
            {
                if (LambdaP > lambdapLIM)
                {
                    rhop = (LambdaP - 0.055 * (3 + phi)) / Math.Pow(LambdaP, 2.0);
                }
                else
                {
                    rhop = 1;
                }
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                rhop = ((LambdaP - 0.15) - 0.05 * phi) / Math.Pow(LambdaP, 2.0);
                if (rhop > 1)
                {
                    rhop = 1;
                }
            }
            double rhoc = 0;
            double chsi = SigmaCrp / SigmaCrc - 1;

            if (chsi > 1)
            {
                chsi = 1;
            }
            if (chsi < 0)
            {
                chsi = 0;
            }

            rhoc = (rhop - Chic) * chsi * (2 - chsi) + Chic;

            double Aeffstot = rhoc * Airreff + hw1eff * t;
            double CU = Aeffstot / (t * b + bs * ts);

            //return rhoTot;
            OutputParameters[0] = lambdap1;
            OutputParameters[1] = rho1;
            OutputParameters[2] = bs;
            OutputParameters[3] = Isl;
            OutputParameters[4] = Aslg;
            OutputParameters[5] = Itst;
            OutputParameters[6] = Ipst;
            OutputParameters[7] = yirrgr;
            OutputParameters[8] = Airreff;
            OutputParameters[9] = Airrgr;
            OutputParameters[10] = Iirrgr;
            OutputParameters[11] = rIrrgr;

            OutputParameters[12] = yirreff;
            OutputParameters[13] = Airreff;
            OutputParameters[14] = Iirreff;
            OutputParameters[15] = rIrreff;

            OutputParameters[16] = Lambdac;
            OutputParameters[17] = SigmaCrc;
            OutputParameters[18] = lambdacIrr;
            OutputParameters[19] = FiInst;
            OutputParameters[20] = e1;
            OutputParameters[21] = e2;
            OutputParameters[22] = alfae;
            OutputParameters[23] = Chic;
            OutputParameters[24] = ab;
            OutputParameters[25] = alfaC;
            OutputParameters[26] = Sigmacsl1;
            OutputParameters[27] = Sigmacsl2;
            OutputParameters[28] = Sigmacsl;
            OutputParameters[29] = BetaAc;
            OutputParameters[30] = LambdaP;
            OutputParameters[31] = rhop;
            OutputParameters[32] = chsi;
            OutputParameters[33] = rhoc;
            OutputParameters[34] = Aeffstot;
            OutputParameters[35] = CU;

            return OutputParameters;
        }

        public static double[] Stiffened_1FlatStiffener_PlateCalc(Code code, double t, double b, double b1, double ts, double bs, double phi, double ksigma, double Ldiaf, StiffenerLongitudinalType stiffType, int row, double fy = 355, double E = 210000, double ni = 0.3)
        {
            double[] OutputParameters = new double[36];
            double b2 = b - b1;

            double alfainstC = 0;
            if (stiffType == StiffenerLongitudinalType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (stiffType == StiffenerLongitudinalType.Open)
            {
                alfainstC = 0.49;
            }

            //double rho = 0;
            double pi = Math.PI;
            //double epsilon = Math.Sqrt(235 / fy);
            double epsilon = 0.814;

            ///// Sub-Panel maximum lenght
            //if (code == Code.Eurocode)
            //{
            //    if (bs > 14 * epsilon * ts)
            //    {
            //        bs = 14 * epsilon * ts;
            //    }
            //}
            //else if (code == Code.ECP205_2001_ASD)
            //{
            //    if (bs > (21 * ts * Math.Sqrt(fy / 98.07)) * 10)
            //    {
            //        bs = (21 * ts * Math.Sqrt(fy / 98.07)) * 10;
            //    }
            //}


            /// Generic Values
            double lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * phi); ;

            /// Sub-Panels properties

            double lambdapb1 = 1;
            double lambdapb2 = 1;
            if (code == Code.Eurocode)
            {
                lambdapb1 = (b1 / t) / (28.4 * epsilon * Math.Sqrt(ksigma));
                lambdapb2 = (b2 / t) / (28.4 * epsilon * Math.Sqrt(ksigma));
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                lambdapb1 = (b1 / t) * Math.Sqrt(fy / 98.07 / ksigma) / 44;
                lambdapb2 = (b2 / t) * Math.Sqrt(fy / 98.07 / ksigma) / 44;
            }

            double rhob1 = 1;
            double rhob2 = 1;
            if (code == Code.Eurocode)
            {
                if (lambdapb1 > lambdapLIM)
                {
                    rhob1 = (lambdapb1 - 0.055 * (3 + phi)) / Math.Pow(lambdapb1, 2.0);
                }
                else
                {
                    rhob1 = 1;
                }
                if (lambdapb2 > lambdapLIM)
                {
                    rhob2 = (lambdapb2 - 0.055 * (3 + phi)) / Math.Pow(lambdapb2, 2.0);
                }
                else
                {
                    rhob2 = 1;
                }
            }
            else if (code == Code.ECP205_2001_ASD)
            {
                rhob1 = (lambdapb1 - 0.15 - 0.05 * phi) / Math.Pow(lambdapb1, 2.0);
                rhob2 = (lambdapb2 - 0.15 - 0.05 * phi) / Math.Pow(lambdapb2, 2.0);

                if (rhob1 > 1)
                {
                    rhob1 = 1;
                }

                if (rhob2 > 1)
                {
                    rhob2 = 1;
                }
            }

            // Stiffener Static Properties
            //double[] Panel1 = Unstiffened_PlateCalc(code, t, b1, phi, ksigma, fy, E, ni);
            //double[] StiffenerProperties = FlatStiffener(code, ts, bs, t, b1, b2, Panel1[], b2topeff, Ldiaf, phi, ksigma, stiffType, fy, E, ni);

            double Ist = (Math.Pow(bs, 3.0) * ts) / 12.0;
            double Ast = bs * ts;
            double Itst = bs * Math.Pow(ts, 3) / 3;
            double Ipst = Math.Pow(bs, 3) * ts / 3 + bs * Math.Pow(ts, 3) / 12;


            // Stiffener Static Properties with AdicentParts
            /// Gross Properties
            double hwgr = b1 / 2 + b2 / 2;
            double Airrgr = b * t / 2 + Ast;
            double yirrgr = (hwgr * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / Airrgr;
            double Iirrgr = (Ist + Ast * Math.Pow((bs / 2 + t - yirrgr), 2)) + (hwgr * Math.Pow(t, 3) / 12 + hwgr * t * Math.Pow((t / 2 - yirrgr), 2));
            double rIrrgr = Math.Sqrt(Iirrgr / Airrgr);

            /// Effective Properties
            double hw1eff = rhob1 * b1 / 2 + rhob2 * b2 / 2;
            double Aw1eff = hw1eff * t;
            double Airreff = Aw1eff + Ast;
            double yirreff = (hw1eff * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / Airreff;
            double Iirreff = (Ist + Ast * Math.Pow((bs / 2 + t - yirreff), 2)) + (hw1eff * Math.Pow(t, 3) / 12 + hw1eff * t * Math.Pow((t / 2 - yirreff), 2));
            double rIrreff = Math.Sqrt(Iirreff / Airreff);

            //string CheckStiffener;
            double stLimTorsional = 5.3 * fy * E;
            if ((Itst / Ipst) >= stLimTorsional)
            {
                //CheckStiffener = "Stiffness torsional requirement checked";
            }
            else
            {
                //CheckStiffener = "Stiffness torsional requirement NOT checked";
            }

            /// COLUMN-LIKE BUCKLING
            double Lambdac = Ldiaf / rIrrgr;
            double SigmaCrc = (Math.Pow(pi, 2.0) * E) / Math.Pow(Lambdac, 2.0);
            double e1 = yirrgr - (t / 2.0);
            double e2 = t + (bs / 2.0) - yirrgr;
            double e = Math.Max(e1, e2);
            double alfae = alfainstC + 0.09 / (rIrrgr / e);
            double lambdacIrr = Math.Sqrt((fy * Airreff) / (SigmaCrc * Airrgr));
            double FiInst = 0.5 * (1.0 + (alfae * (lambdacIrr - 0.2)) + Math.Pow(lambdacIrr, 2.0));
            double Chic = 1.0 / (FiInst + Math.Sqrt(Math.Pow(FiInst, 2.0) - Math.Pow(lambdacIrr, 2.0)));

            /// PLATE-LIKE BUCKLING
            double b11 = b / 2.0;
            double b22 = b11;
            double alfaC = 4.33 * Math.Pow((((Iirrgr * Math.Pow(b11, 2.0)) * Math.Pow(b22, 2.0)) / Math.Pow(t, 3.0)) / b, 0.25);
            double ab = Ldiaf / b;
            double SigmaCrp = 0;
            double Sigmacsl = 0;
            double Sigmacsl1 = ((1.05 * E) / Airrgr) * (Math.Sqrt((Iirrgr * Math.Pow(t, 3.0)) * b)) / (b11 * b22);
            double Sigmacsl2 = (Math.Pow(pi, 2) * E * Iirrgr / (Airrgr * Math.Pow(Ldiaf, 2))) + (E * Math.Pow(t, 3) * b * Math.Pow(Ldiaf, 2)) / (4 * Math.Pow(pi, 2) * (1 - ni * ni) * Airrgr * (b11 * b11) * (b22 * b22));

            if (Ldiaf >= alfaC)
            {
                Sigmacsl = Sigmacsl1;
                SigmaCrp = Sigmacsl;
            }
            else
            {
                Sigmacsl = Sigmacsl2;
                SigmaCrp = Sigmacsl;
            }

            double BetaAc = Airreff / Airrgr;
            double LambdaP = Math.Sqrt(BetaAc * fy / SigmaCrp);

            double rhop = 1;
            if (code == Code.Eurocode)
            {
                if (LambdaP > lambdapLIM)
                {
                    rhop = (LambdaP - 0.055 * (3 + phi)) / Math.Pow(LambdaP, 2.0);
                }
                else
                {
                    rhop = 1;
                }
            }
            else if (code == Code.ECP205_2001_ASD)
            {

                rhop = (LambdaP - 0.15 - 0.05 * phi) / Math.Pow(LambdaP, 2.0);

                if (rhop > 1)
                {
                    rhop = 1;
                }
            }
            double rhoc = 0;
            double chsi = SigmaCrp / SigmaCrc - 1;

            if (chsi > 1)
            {
                chsi = 1;
            }
            if (chsi < 0)
            {
                chsi = 0;
            }

            rhoc = (rhop - Chic) * chsi * (2 - chsi) + Chic;

            double Aeffstot = rhoc * Airreff + hw1eff * t;
            double CU = Aeffstot / (t * b + bs * ts);

            //return rhoTot;
            OutputParameters[0] = lambdapb1;
            OutputParameters[1] = rhob1;
            OutputParameters[2] = bs;
            OutputParameters[3] = Ist;
            OutputParameters[4] = Ast;
            OutputParameters[5] = Itst;
            OutputParameters[6] = Ipst;
            OutputParameters[7] = yirrgr;
            OutputParameters[8] = Airreff;
            OutputParameters[9] = Airrgr;
            OutputParameters[10] = Iirrgr;
            OutputParameters[11] = rIrrgr;


            OutputParameters[12] = yirreff;
            OutputParameters[13] = Airreff;
            OutputParameters[14] = Iirreff;
            OutputParameters[15] = rIrreff;

            OutputParameters[16] = Lambdac;
            OutputParameters[17] = SigmaCrc;
            OutputParameters[18] = lambdacIrr;
            OutputParameters[19] = FiInst;
            OutputParameters[20] = e1;
            OutputParameters[21] = e2;
            OutputParameters[22] = alfae;
            OutputParameters[23] = Chic;
            OutputParameters[24] = ab;
            OutputParameters[25] = alfaC;
            OutputParameters[26] = Sigmacsl1;
            OutputParameters[27] = Sigmacsl2;
            OutputParameters[28] = Sigmacsl;
            OutputParameters[29] = BetaAc;
            OutputParameters[30] = LambdaP;
            OutputParameters[31] = rhop;
            OutputParameters[32] = chsi;
            OutputParameters[33] = rhoc;
            OutputParameters[34] = Aeffstot;
            OutputParameters[35] = CU;

            return OutputParameters;
        }
    }
}

#endif