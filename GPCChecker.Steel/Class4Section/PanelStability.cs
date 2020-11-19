/*  ========================================================================= *\
   Project:    GPC Checker - Class4 - Panels Stability
   Date:       02/002/2019

	File:      
	Author:    Vochescu Robert


    Overview:  Class 4 - Plate Effective Section Calculation
\* ========================================================================= */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.Class4Section
{
    public class PanelEffectiveProperties
    {
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
            double pi = Math.PI;
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

        public static double[] Stiffened_1FlatStiffenerMiddle_PlateCalc(Code code, double t, double b, double ts, double bs, double Ldiaf, double phi, double ksigma, StiffenerType stiffType, double fy = 355, double E = 210000, double ni = 0.3)
        {
            double[] OutputParameters = new double[36];

            double alfainstC = 0;
            if (stiffType == StiffenerType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (stiffType == StiffenerType.Open)
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

            string CheckStiffener = "";
            double stLimTorsional = 5.3 * fy * E;
            if ((Itst / Ipst) >= stLimTorsional)
            {
                CheckStiffener = "Stiffness torsional requirement checked";
            }
            else
            {
                CheckStiffener = "Stiffness torsional requirement NOT checked";
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

        public static double[] Stiffened_1FlatStiffener_PlateCalc(Code code, double t, double b, double b1, double ts, double bs, double phi, double ksigma, double Ldiaf, StiffenerType stiffType, int row, double fy = 355, double E = 210000, double ni = 0.3)
        {
            double[] OutputParameters = new double[36];
            double b2 = b - b1;

            double alfainstC = 0;
            if (stiffType == StiffenerType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (stiffType == StiffenerType.Open)
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

            string CheckStiffener = "";
            double stLimTorsional = 5.3 * fy * E;
            if ((Itst / Ipst) >= stLimTorsional)
            {
                CheckStiffener = "Stiffness torsional requirement checked";
            }
            else
            {
                CheckStiffener = "Stiffness torsional requirement NOT checked";
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

    public class FlatStiffener
    {
        #region FIELD_CONSTRUCTOR
        public FlatStiffener(Code __code, double __ts, double __bs, double __t, double __b1, double __b2, double __b1eff, double __b2eff, double __Ldiaf, StiffenerType __stiffType, double __fy = 355, double __E = 210000, double __ni = 0.3)
        {
            _ts = __ts;
            _bs = __bs;
            _b1 = __b1;
            _b2 = __b2;
            _t = __t;
            _b1eff = __b1eff;
            _b2eff = __b2eff;
            _Ldiaf = __Ldiaf;
            _fy = __fy;
            _E = __E;
            _ni = __ni;
            _code = __code;

            CalcStiffenerProperties();
        }
        #endregion

        #region FIELD_DECONSTRUCTOR
        #endregion

        #region FIELD_METHODS
        private void CalcStiffenerProperties()
        {
            double alfainstC = 0;
            if (_stiffType == StiffenerType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (_stiffType == StiffenerType.Open)
            {
                alfainstC = 0.49;
            }

            double pi = Math.PI;

            // Stiffener Static Properties (alone)
            _Ist = (Math.Pow(bs, 3.0) * ts) / 12.0;
            _Ast = bs * ts;
            _Itst = bs * Math.Pow(ts, 3) / 3;
            _Ipst = Math.Pow(bs, 3) * ts / 3 + bs * Math.Pow(ts, 3) / 12;

            // Stiffener Static Properties with AdicentParts
            /// Gross Properties
            double hwgr = b1 / 2 + b2 / 2;
            _Airrgr = (b1 + b2) * t / 2 + _Ast;
            _yirrgr = (hwgr * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / _Airrgr;
            _Iirrgr = (_Ist + _Ast * Math.Pow((bs / 2 + t - _yirrgr), 2)) + (hwgr * Math.Pow(t, 3) / 12 + hwgr * t * Math.Pow((t / 2 - _yirrgr), 2));
            _rIrrgr = Math.Sqrt(_Iirrgr / _Airrgr);
            _Lambdacgr = Ldiaf / _rIrrgr;
            _Lambdac = _Lambdacgr;

            /// Effective Properties
            double _hw1eff = b1eff + b2eff;
            double _Aw1eff = _hw1eff * t;
            _Airreff = _Aw1eff + _Ast;
            _yirreff = (_hw1eff * t * t / 2.0 + bs * ts * (bs / 2.0 + t)) / _Airreff;
            _Iirreff = (_Ist + _Ast * Math.Pow((bs / 2 + t - _yirreff), 2)) + (_hw1eff * Math.Pow(t, 3) / 12 + _hw1eff * t * Math.Pow((t / 2 - _yirreff), 2));
            _rIrreff = Math.Sqrt(Iirreff / _Airreff);
            _Lambdaceff = Ldiaf / _rIrreff;

            _tlim = 5.3 * fy / E;
            _ted = _Itst / _Ipst;
            if ((_Itst / _Ipst) >= _tlim)
            {
                _TorsionalCheck = true;
            }
            else
            {
                _TorsionalCheck = false;
            }

            /// COLUMN-LIKE BUCKLING
            _Lambdac = Ldiaf / _rIrrgr;
            _SigmaCrc = (Math.Pow(pi, 2.0) * E) / Math.Pow(_Lambdac, 2.0);
            _e1 = _yirrgr - (t / 2.0);
            _e2 = t + (bs / 2.0) - _yirrgr;
            _e = Math.Max(_e1, _e2);
            _alfae = alfainstC + 0.09 / (_rIrrgr / _e);
            _lambdacAdimIrr = Math.Sqrt((fy * _Airreff) / (_SigmaCrc * _Airrgr));
            _FiInst = 0.5 * (1.0 + (_alfae * (_lambdacAdimIrr - 0.2)) + Math.Pow(_lambdacAdimIrr, 2.0));
            _Chic = 1.0 / (_FiInst + Math.Sqrt(Math.Pow(_FiInst, 2.0) - Math.Pow(_lambdacAdimIrr, 2.0)));
        }
        #endregion

        #region FIELD_VARIABLES
        double _ts;
        double _bs;
        double _b1;
        double _b2;
        double _t;
        double _b1eff;
        double _b2eff;
        double _Ldiaf;
        double _fy;
        double _E;
        double _ni;
        Code _code;
        StiffenerType _stiffType;

        bool _TorsionalCheck;
        double _Ast;
        double _Ist;
        double _Itst;
        double _Ipst;

        double _yirrgr;
        double _Airrgr;
        double _Iirrgr;
        double _rIrrgr;

        double _yirreff;
        double _Airreff;
        double _Iirreff;
        double _rIrreff;

        double _Lambdac;
        double _SigmaCrc;
        double _lambdacAdimIrr;
        double _FiInst;
        double _e1;
        double _e2;
        double _e;
        double _alfae;
        double _Chic;

        double _ted;
        double _tlim;

        double _Lambdacgr;
        double _Lambdaceff;
        #endregion

        #region FIELD_PROPERTIES
        public double ts
        {
            get => _ts;
            private set => _ts = value;
        }
        public double bs
        {
            get => _bs;
            private set => _bs = value;
        }
        public double b1
        {
            get => _b1;
            private set => _b1 = value;
        }
        public double b2
        {
            get => _b2;
            private set => _b2 = value;
        }
        public double t
        {
            get => _t;
            private set => _t = value;
        }
        public double b1eff
        {
            get => _b1eff;
            private set => _b1eff = value;
        }
        public double b2eff
        {
            get => _b2eff;
            private set => _b2eff = value;
        }
        public double Ldiaf
        {
            get => _Ldiaf;
            private set => _Ldiaf = value;
        }
        public double fy
        {
            get => _fy;
            private set => _fy = value;
        }
        public double E
        {
            get => _E;
            private set => _E = value;
        }
        public double ni
        {
            get => _ni;
            private set => _ni = value;
        }
        public Code code
        {
            get => _code;
            private set => _code = value;
        }
        public StiffenerType StiffType
        {
            get => _stiffType;
            private set => _stiffType = value;
        }
        public bool TorsionalCheck
        {
            get => _TorsionalCheck;
            private set => _TorsionalCheck = value;
        }

        public double Ast
        {
            get => _Ast;
            private set => _Ast = value;
        }

        public double Ist
        {
            get => _Ist;
            private set => _Ist = value;
        }

        public double Itst
        {
            get => _Itst;
            private set => _Itst = value;
        }

        public double Ipst
        {
            get => _Ipst;
            private set => _Ipst = value;
        }

        public double Yirrgr
        {
            get => _yirrgr;
            set => _yirrgr = value;
        }

        public double Yirreff
        {
            get => _yirreff;
            private set => _yirreff = value;
        }

        public double Airreff
        {
            get => _Airreff;
            private set => _Airreff = value;
        }

        public double Iirreff
        {
            get => _Iirreff;
            private set => _Iirreff = value;
        }

        public double RhoIrreff
        {
            get => _rIrreff;
            private set => _rIrreff = value;
        }

        public double Airrgr
        {
            get => _Airrgr;
            set => _Airrgr = value;
        }
        public double Iirrgr
        {
            get => _Iirrgr;
            set => _Iirrgr = value;
        }
        public double RhoIrrgr
        {
            get => _rIrrgr;
            set => _rIrrgr = value;
        }
        public double Lambdac
        {
            get => _Lambdac;
            private set => _Lambdac = value;
        }
        public double SigmaCrc
        {
            get => _SigmaCrc;
            private set => _SigmaCrc = value;
        }
        public double LambdacAdimIrr
        {
            get => _lambdacAdimIrr;
            private set => _lambdacAdimIrr = value;
        }
        public double FiInst
        {
            get => _FiInst;
            private set => _FiInst = value;
        }
        public double Ecc_e1
        {
            get => _e1;
            private set => _e1 = value;
        }
        public double Ecc_e2
        {
            get => _e2;
            private set => _e2 = value;
        }
        public double Ecc_e
        {
            get => _e;
            private set => _e = value;
        }
        public double Alfa_e
        {
            get => _alfae;
            private set => _alfae = value;
        }
        public double Chic
        {
            get => _Chic;
            private set => _Chic = value;
        }
        public double ted
        {
            get => _ted;
            private set => _ted = value;
        }
        public double tlim
        {
            get => _tlim;
            private set => _tlim = value;
        }
        public double Lambdacgr
        {
            get => _Lambdacgr;
            private set => _Lambdacgr = value;
        }
        public double Lambdaceff
        {
            get => _Lambdaceff;
            private set => _Lambdaceff = value;
        }
        #endregion
    }

    public class UnstiffenedPanel
    {
        #region FIELD_CONSTRUCTOR
        public UnstiffenedPanel(Code __code, double __t, double __b, double __phi, double __ksigma, double __fy = 355, double __E = 210000, double __ni = 0.3, double __Ldiaf = 1000, bool __IsUnstiffened = false)
        {
            _code = __code;
            _t = __t;
            _b = __b;
            _phi = __phi;
            _ksigma = __ksigma;
            _fy = __fy;
            _E = __E;
            _ni = __ni;
            _Ldiaf = __Ldiaf;
            _alfae = 0.21; /// buckling curve a

            _IsUnstiffened = __IsUnstiffened;

            CalcPanelProperties();
        }
        #endregion

        #region FIELD_DECONSTRUCTOR
        #endregion

        #region FIELD_METHODS
        private void CalcPanelProperties()
        {
            double beff = _b;
            double bLIM = 0;
            double pi = Math.PI;
            double epsilon = Math.Sqrt(235 / _fy);

            _rho = 1;
            _rhoc = 1;
            _lambdap = 1;
            _be1 = 0;
            _be2 = 0;
            _ytop = 0;
            _ybottom = 0;
            _btop = 0;
            _bbottom = 0;


            /// Sub-Panel maximum lenght
            if (_code == Code.Eurocode)
            {
                if (_phi == 1)
                {
                    bLIM = 42.0 * epsilon * _t;
                }
                if (_phi == -1)
                {
                    bLIM = 124 * epsilon * _t;
                }
                if (_phi > -1)
                {
                    bLIM = (42.0 * epsilon) / (0.67 + 0.33 * _phi) * _t;
                }
                if (_phi < -1)
                {
                    bLIM = ((62.0 * epsilon) * (1 - _phi) * Math.Sqrt(-phi)) * _t;
                }
            }
            else if (_code == Code.ECP205_2001_ASD)
            {
                if (_phi == 1)
                {
                    bLIM = (64.0 * (_t / 10) / Math.Sqrt(_fy / 98.07)) * 10;
                }
                if (_phi == -1)
                {
                    bLIM = (190 * (_t / 10) / Math.Sqrt(_fy / 98.07)) * 10;
                }
                if (_phi > -1)
                {
                    bLIM = ((190 * (_t / 10) / Math.Sqrt(_fy / 98.07)) / (2 + phi)) * 10;
                }
                if (_phi < -1) /// Come da Eurocodice -> non c'è da ECP205
                {
                    bLIM = ((62.0 * epsilon) * (1 - _phi) * Math.Sqrt(-phi)) * _t;
                }
            }


            if (code == Code.Eurocode || code == Code.ECP205_2001_ASD)
            {
                if (_phi < 1.0 && _phi > 0.0)
                {
                    _ksigma = 8.2 / (1.05 + _phi);
                }
                else if (_phi == 0.0)
                {
                    _ksigma = 7.8;
                }
                else if (_phi < 0.0 && _phi > -1.0)
                {
                    _ksigma = 7.81 - 6.29 * _phi + 9.78 * _phi * _phi;
                }
                else if (_phi == -1.0)
                {
                    _ksigma = 23.9;
                }
                else if (_phi > -3.0 && _phi < -1.0)
                {
                    _ksigma = 5.98 * (1.0 - _phi) * (1.0 - _phi);
                }
            }

            if (bLIM <= _b)
            {
                /// Generic Values
                double lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * _phi); ;

                /// Sub-Panel properties

                if (_code == Code.Eurocode)
                {
                    _Sigmacrp = _ksigma * (pi * pi * E) / (12.0 * (1 - ni * ni)) * (t / b) * (t / b);
                    //_lambdap = (b / t) / (28.4 * epsilon * Math.Sqrt(_ksigma));
                    _lambdap = Math.Sqrt(fy / _Sigmacrp);

                }
                else if (_code == Code.ECP205_2001_ASD)
                {
                    _lambdap = (_b / _t) * Math.Sqrt((_fy / 98.07) / _ksigma) / 44;
                }

                if (_code == Code.Eurocode)
                {
                    if (_lambdap > lambdapLIM)
                    {
                        rho = (_lambdap - 0.055 * (3 + _phi)) / Math.Pow(_lambdap, 2.0);
                    }
                    else
                    {
                        rho = 1;
                    }
                }
                else if (_code == Code.ECP205_2001_ASD)
                {
                    rho = ((_lambdap - 0.15) - 0.05 * _phi) / Math.Pow(_lambdap, 2.0);

                    if (rho > 1 || rho < 0)
                    {
                        rho = 1;
                    }
                }
            }

            /// Column-Like Behaviour
            _Sigmacrc = 0;
            _lambdac = 0;
            _FiInst = 0;
            _chic = 0;
            _csi = 0;

            if (_IsUnstiffened == true)
            {
                double A = _b * _t;
                double I = _b * Math.Pow(_t, 3) / 12;
                _Sigmacrc = Math.Pow(3.14, 2) * I / (A * Math.Pow(_Ldiaf, 2));
                _lambdac = Math.Sqrt(_fy / _Sigmacrc);
                _FiInst = 0.5 * (1.0 + (_alfae * (_lambdac - 0.2)) + Math.Pow(_lambdac, 2.0));
                _chic = 1.0 / (_FiInst + Math.Sqrt(Math.Pow(_FiInst, 2.0) - Math.Pow(_lambdac, 2.0)));

                _csi = _Sigmacrc - 1;
                if (_csi > 1)
                {
                    _csi = 1;
                }
                if (_csi < 0)
                {
                    _csi = 0;
                }

                _rhoc = (rho - _chic) * _csi * (2 - _csi) + _chic;
            }


            /// beff calculation
            if (_phi >= 0)
            {
                beff = rho * _b;
                _ybottom = 0;
                _ytop = _b;
                _be1 = 2 / (5 - _phi) * beff;
                _be2 = beff - _be1;
                _btop = _be1;
                _bbottom = _be2;
            }
            else if (_phi < 0)
            {
                _ybottom = _b * Math.Abs(_phi) / (1 + Math.Abs(_phi));
                _ytop = _b - _ybottom;
                /// rho application
                beff = rho * _ytop;
                /// rhoc application
                _be1 = 0.4 * beff;
                _be2 = 0.6 * beff;
                _btop = _be1;
                _bbottom = _be2 + _ybottom;
            }

            if (_rho >= 1)
            {
                _be1 = _b / 2;
                _be2 = _b / 2;
                _btop = _b / 2;
                _bbottom = _b / 2;
            }

            _Atotgross = _b * _t;
            _Atoteff = (btop + _bbottom) * _t;
            _teff = _Atoteff / _b;
            _CU = _Atoteff / _Atotgross;
        }
        #endregion

        #region FIELD_VARIABLES
        private double _b;
        private double _t;
        private double _phi;
        private double _chic;
        private double _csi;
        private double _rho;
        private double _rhoc;
        private double _ksigma;
        private double _lambdap;
        private double _Sigmacrp;
        private double _Sigmacrc;
        private double _Ldiaf;
        private double _FiInst;
        private double _lambdac;
        private double _alfae;

        private bool _IsUnstiffened;

        /// TOP: compression fy
        private double _ytop;
        private double _ybottom;
        private double _be1;
        private double _be2;
        private double _btop;
        private double _bbottom;
        private double _teff;

        private double _Atotgross;
        private double _Atoteff;
        private double _CU;

        private double _fy;
        private double _E;
        private double _ni;
        private Code _code;
        #endregion

        #region FIELD_PROPERTIES
        public double t
        {
            get => _t;
            private set => _t = value;
        }
        public double b
        {
            get => _b;
            private set => _b = value;
        }

        public double lambdac
        {
            get => _lambdac;
            private set => _lambdac = value;
        }

        public double alfae
        {
            get => _alfae;
            private set => _alfae = value;
        }

        public double phi
        {
            get => _phi;
            private set => _phi = value;
        }
        public double rho
        {
            get => _rho;
            private set => _rho = value;
        }

        public double rhoc
        {
            get => _rhoc;
            private set => _rhoc = value;
        }

        public double Ldiaf
        {
            get => _Ldiaf;
            private set => _Ldiaf = value;
        }

        public double FiInst
        {
            get => _FiInst;
            private set => _FiInst = value;
        }

        public double chic
        {
            get => _chic;
            private set => _chic = value;
        }

        public double csi
        {
            get => _csi;
            private set => _csi = value;
        }

        public double ksigma
        {
            get => _ksigma;
            private set => _ksigma = value;
        }
        public double lambdap
        {
            get => _lambdap;
            private set => _lambdap = value;
        }
        public double Sigmacrp
        {
            get => _Sigmacrp;
            private set => _Sigmacrp = value;
        }

        public double Sigmacrc
        {
            get => _Sigmacrc;
            private set => _Sigmacrc = value;
        }

        public double ytop
        {
            get => _ytop;
            private set => _ytop = value;
        }
        public double ybottom
        {
            get => _ybottom;
            private set => _ybottom = value;
        }
        public double be1
        {
            get => _be1;
            private set => _be1 = value;
        }
        public double be2
        {
            get => _be2;
            private set => _be2 = value;
        }
        public double btop
        {
            get => _btop;
            private set => _btop = value;
        }
        public double bbottom
        {
            get => _bbottom;
            private set => _bbottom = value;
        }
        public double teff
        {
            get => _teff;
            private set => _teff = value;
        }
        public double fy
        {
            get => _fy;
            private set => _fy = value;
        }
        public double E
        {
            get => _E;
            private set => _E = value;
        }
        public double ni
        {
            get => _ni;
            private set => _ni = value;
        }
        public Code code
        {
            get => _code;
            private set => _code = value;
        }
        public double Atotgross
        {
            get => _Atotgross;
            private set => _Atotgross = value;
        }

        public double Atoteff
        {
            get => _Atoteff;
            private set => _Atoteff = value;
        }
        public double CU
        {
            get => _CU;
            private set => _CU = value;
        }

        public bool IsUnstiffened
        {
            get => _IsUnstiffened;
            private set => _IsUnstiffened = value;
        }
        #endregion
    }

    public class StiffenedPanel
    {
        /// __StiffenerArray[0] - STIFFENER1: sta tra __PanelArray[0] e __PanelArray[1];
        ///        /// __StiffenerArray[0] - STIFFENER1: sta tra __PanelArray[0] e __PanelArray[2];

        #region FIELD_CONSTRUCTORS
        public StiffenedPanel(Code __code, int __stiffNum, List<UnstiffenedPanel> __PanelArray, List<FlatStiffener> __StiffenerArray, double phi, double __fy = 355, double __E = 210000, double __ni = 0.3, double __b = 0)
        {
            _PanelArray = new List<UnstiffenedPanel>();
            _PanelArray = __PanelArray;
            _StiffenerArray = new List<FlatStiffener>();
            _StiffenerArray = __StiffenerArray;
            _code = __code;
            _stiffNum = __stiffNum;
            _fy = __fy;
            _E = __E;
            _ni = __ni;


            _b = __b;
            if (__stiffNum == 1 && __b == 0)
            {
                _b = _b + __PanelArray[0].b;
                _b = _b + __PanelArray[1].b;
            }
            else if (__stiffNum == 2 && __b == 0)
            {
                _b = _b + __PanelArray[0].b;
                _b = _b + __PanelArray[1].b;
                _b = _b + __PanelArray[2].b;
            }
            CalcPanelProperties(phi);
        }
        #endregion

        #region FIELD_DECONSTRUCTORS

        #endregion

        #region FIELD_METHODS
        private void CalcPanelProperties(double phi)
        {
            /// One single Stiffener
            if (_stiffNum == 1)
            {
                double[] PlateBucklingOutput = CalcPlateBucklingSingleFlatStiffener(_PanelArray[0], _PanelArray[1], _StiffenerArray[0], 0.00, 0.00);

                _LdiafLIM = PlateBucklingOutput[0];
                _ab = PlateBucklingOutput[1];
                _Rhop = PlateBucklingOutput[2];
                _BetaAc = PlateBucklingOutput[3];
                _LambdaP = PlateBucklingOutput[4];
                _SigmaCrp = PlateBucklingOutput[5];
                _Sigmacsl = PlateBucklingOutput[6];
                _Sigmacsl1 = PlateBucklingOutput[7];
                _Sigmacsl2 = PlateBucklingOutput[8];


                double aeffloc = _StiffenerArray[0].Airreff;
                double ac = _StiffenerArray[0].Airrgr;

                double[] Output = CalcPlateReductionFactor(_SigmaCrp, _StiffenerArray[0].SigmaCrc, _StiffenerArray[0].Chic, aeffloc, ac, phi);

                _BetaAc = Output[0];
                _LambdaP = Output[1];
                _LambdaPLIM = Output[2];
                _Rhop = Output[3];
                _Chsi = Output[4];
                _Rhoc = Output[5];

                //_Atotgross = _PanelArray[0].b / 2 + _PanelArray[0].b / 2 + _StiffenerArray[0].Airrgr;
                //_Atoteff = _StiffenerArray[0].Airreff * _Rhoc;
                _Atoteff = _StiffenerArray[0].Airreff * _Rhoc + _PanelArray[0].btop * _PanelArray[0].t + _PanelArray[1].bbottom * _PanelArray[1].t;
                _teff = _Atoteff / _b;
                //_CU = _Atoteff / _Atoteff;
            }
            else if (_stiffNum == 2)
            {
                double[] PlateBucklingOutput1 = CalcPlateBucklingSingleFlatStiffener(_PanelArray[0], _PanelArray[1], _StiffenerArray[0], 0.00, 0.00);
                double[] PlateBucklingOutput2 = CalcPlateBucklingSingleFlatStiffener(_PanelArray[1], _PanelArray[2], _StiffenerArray[1], 0.00, 0.00);
                double airr = _StiffenerArray[0].Airrgr + _StiffenerArray[1].Airrgr;
                double iirr = _StiffenerArray[0].Iirrgr + _StiffenerArray[1].Iirrgr;

                double[] PlateBucklingOutput3 = { };

                if (phi == 1)
                {
                    PlateBucklingOutput3 = CalcPlateBucklingSingleFlatStiffener(_PanelArray[3], _PanelArray[3], _StiffenerArray[2], airr, iirr);
                }
                else
                {
                    PlateBucklingOutput3 = CalcPlateBucklingSingleFlatStiffener(_PanelArray[0], _PanelArray[3], _StiffenerArray[2], airr, iirr);
                }

                double aeffloc = _StiffenerArray[0].Airreff + _StiffenerArray[1].Airreff;
                double ac = _StiffenerArray[0].Airrgr + _StiffenerArray[1].Airrgr;

                double SigmaCrp1 = PlateBucklingOutput1[5];
                double SigmaCrp2 = PlateBucklingOutput2[5];
                double SigmaCrp3 = PlateBucklingOutput3[5];

                _SigmaCrp = 1e9;

                if (SigmaCrp1 < _SigmaCrp)
                {
                    _SigmaCrp = SigmaCrp1;

                    _LdiafLIM = PlateBucklingOutput1[0];
                    _ab = PlateBucklingOutput1[1];
                    _Rhop = PlateBucklingOutput1[2];
                    _BetaAc = PlateBucklingOutput1[3];
                    _LambdaP = PlateBucklingOutput1[4];
                    _SigmaCrp = PlateBucklingOutput1[5];
                    _Sigmacsl = PlateBucklingOutput1[6];
                    _Sigmacsl1 = PlateBucklingOutput1[7];
                    _Sigmacsl2 = PlateBucklingOutput1[8];

                }
                if (SigmaCrp2 < _SigmaCrp)
                {
                    _SigmaCrp = SigmaCrp2;

                    _LdiafLIM = PlateBucklingOutput2[0];
                    _ab = PlateBucklingOutput2[1];
                    _Rhop = PlateBucklingOutput2[2];
                    _BetaAc = PlateBucklingOutput2[3];
                    _LambdaP = PlateBucklingOutput2[4];
                    _SigmaCrp = PlateBucklingOutput2[5];
                    _Sigmacsl = PlateBucklingOutput2[6];
                    _Sigmacsl1 = PlateBucklingOutput2[7];
                    _Sigmacsl2 = PlateBucklingOutput2[8];
                }
                if (SigmaCrp3 < _SigmaCrp)
                {
                    _SigmaCrp = SigmaCrp3;

                    _LdiafLIM = PlateBucklingOutput3[0];
                    _ab = PlateBucklingOutput3[1];
                    _Rhop = PlateBucklingOutput3[2];
                    _BetaAc = PlateBucklingOutput3[3];
                    _LambdaP = PlateBucklingOutput3[4];
                    _SigmaCrp = PlateBucklingOutput3[5];
                    _Sigmacsl = PlateBucklingOutput3[6];
                    _Sigmacsl1 = PlateBucklingOutput3[7];
                    _Sigmacsl2 = PlateBucklingOutput3[8];
                }

                double sigmaCrc = Math.Min(_StiffenerArray[0].SigmaCrc, _StiffenerArray[1].SigmaCrc);

                double[] Output = CalcPlateReductionFactor(_SigmaCrp, sigmaCrc, _StiffenerArray[0].Chic, aeffloc, ac, phi);
                _BetaAc = Output[0];
                _LambdaP = Output[1];
                _LambdaPLIM = Output[2];
                _Rhop = Output[3];
                _Chsi = Output[4];
                _Rhoc = Output[5];

                //_Atoteff = aeffloc * _Rhoc;
                _Atoteff = aeffloc * _Rhoc + _PanelArray[0].btop * _PanelArray[0].t + _PanelArray[2].bbottom * _PanelArray[2].t;
                _teff = _Atoteff / _b;
            }
        }


        private double[] CalcPlateReductionFactor(double SigmaCrp, double SigmaCrc, double Chic, double Aeffloc, double Ac, double phi)
        {
            double[] Output = new double[6];
            double rhop = 1;
            double BetaAc = 0;

            BetaAc = Aeffloc / Ac;
            LambdaP = Math.Sqrt(BetaAc * _fy / SigmaCrp);

            Output[0] = BetaAc;
            Output[1] = LambdaP;

            /// Generic Values
            double lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * phi);

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

            Output[0] = BetaAc;
            Output[1] = LambdaP;
            Output[2] = lambdapLIM;
            Output[3] = rhop;
            Output[4] = chsi;
            Output[5] = rhoc;

            return Output;
        }

        private double[] CalcPlateBucklingSingleFlatStiffener(UnstiffenedPanel panel1, UnstiffenedPanel panel2, FlatStiffener stiffener, double __Airrgr = 0.00, double _Iirrgr = 0.00)
        {
            double[] Output = new double[9];

            /// PLATE-LIKE BUCKLING
            double b = panel1.b + panel2.b;
            double t = panel1.t;
            double b11 = panel1.b;
            double b22 = panel2.b;

            double iirrgr = 0.00;
            if (_Iirrgr == 0.00 || _Iirrgr < 0.00)
            {
                iirrgr = stiffener.Iirrgr;
            }
            else if (_Iirrgr > 0.00)
            {
                iirrgr = _Iirrgr;
            }

            //double airrgr = stiffener.Airrgr;
            //__Airrgr = stiffener.Airrgr;

            double airrgr = 0.00;

            if (__Airrgr == 0.00)
            {
                airrgr = stiffener.Airrgr;
            }
            else if (__Airrgr > 0.00)
            {
                airrgr = __Airrgr;
            }

            //if (_Airrgr == 0)
            //{

            //}
            //if (_Airrgr > 0)
            //{
            //    airrgr = _Airrgr;
            //}

            double ldiaf = stiffener.Ldiaf;
            double airreff = stiffener.Airreff;
            double sigmaCrc = stiffener.SigmaCrc;
            double chic = stiffener.Chic;

            double bstiff = stiffener.bs;
            double tstiff = stiffener.ts;

            double ldiaffLIM = 0;
            double ab = ldiaf / b;
            double rhop = 1;
            double betaAc = 0;
            double lambdaP = 0;

            double sigmaCrp = 0;
            double sigmacsl = 0;
            double sigmacsl1 = 0;
            double sigmacsl2 = 0;

            double atoteff = 0;
            double atotgross = 0;
            double cu = 0;


            ldiaffLIM = 4.33 * Math.Pow((((iirrgr * Math.Pow(b11, 2.0)) * Math.Pow(b22, 2.0)) / Math.Pow(t, 3.0)) / b, 0.25);
            ab = stiffener.Ldiaf / b;
            sigmaCrp = 0;
            sigmacsl = 0;
            sigmacsl1 = ((1.05 * _E) / airrgr) * (Math.Sqrt((iirrgr * Math.Pow(t, 3.0)) * b)) / (b11 * b22);
            sigmacsl2 = (Math.Pow(Math.PI, 2) * E * iirrgr / (airrgr * Math.Pow(ldiaf, 2))) + (E * Math.Pow(t, 3) * b * Math.Pow(ldiaf, 2)) / (4 * Math.Pow(Math.PI, 2) * (1 - ni * ni) * airrgr * (b11 * b11) * (b22 * b22));

            if (ldiaf >= ldiaffLIM)
            {
                sigmacsl = sigmacsl1;
                sigmaCrp = sigmacsl;
            }
            else
            {
                sigmacsl = sigmacsl2;
                sigmaCrp = sigmacsl;
            }

            Output[0] = ldiaffLIM;
            Output[1] = ab;
            Output[2] = rhop;
            Output[3] = betaAc;
            Output[4] = lambdaP;
            Output[5] = sigmaCrp;
            Output[6] = sigmacsl;
            Output[7] = sigmacsl1;
            Output[8] = sigmacsl2;

            return Output;
        }
        #endregion

        #region FIELD_VARIABLES
        private List<UnstiffenedPanel> _PanelArray;
        private List<FlatStiffener> _StiffenerArray;


        private double _fy;
        private double _E;
        private double _ni;
        private Code _code;
        private int _stiffNum;

        private double _b;
        private double _t;
        private double _LdiafLIM;
        private double _ab;
        private double _Rhop;
        private double _BetaAc;
        private double _LambdaP;
        private double _LambdaPLIM;
        private double _Chsi;
        private double _Rhoc;

        private double _SigmaCrp;
        private double _Sigmacsl;
        private double _Sigmacsl1;
        private double _Sigmacsl2;

        private double _Atoteff;
        private double _teff;
        private double _Atotgross;
        private double _CU;
        #endregion

        #region FIELD_PARAMETERS
        public List<UnstiffenedPanel> PanelArray
        {
            get => _PanelArray;
        }
        public List<FlatStiffener> StiffenerArray
        {
            get => _StiffenerArray;
        }
        public double fy
        {
            get => _fy;
            private set => _fy = value;
        }
        public int stiffNum
        {
            get => _stiffNum;
            private set => _stiffNum = value;
        }

        public double E
        {
            get => _E;
            private set => _E = value;
        }
        public double ni
        {
            get => _ni;
            private set => _ni = value;
        }
        public Code code
        {
            get => _code;
            private set => _code = value;
        }
        public double CU
        {
            get => _CU;
            private set => _CU = value;
        }
        public double Atoteff
        {
            get => _Atoteff;
            private set => _Atoteff = value;
        }
        public double teff
        {
            get => _teff;
            private set => _teff = value;
        }
        public double Atotgross
        {
            get => _Atotgross;
            private set => _Atotgross = value;
        }
        public double b
        {
            get => _b;
            private set => _b = value;
        }
        public double t
        {
            get => _t;
            private set => _t = value;
        }
        public double LdiafLIM
        {
            get => _LdiafLIM;
            private set => _LdiafLIM = value;
        }
        public double ab
        {
            get => _ab;
            private set => _ab = value;
        }
        public double Rhop
        {
            get => _Rhop;
            private set => _Rhop = value;
        }
        public double Chsi
        {
            get => _Chsi;
            private set => _Chsi = value;
        }

        public double Rhoc
        {
            get => _Rhoc;
            private set => _Rhoc = value;
        }
        public double BetaAc
        {
            get => _BetaAc;
            private set => _BetaAc = value;
        }
        public double LambdaP
        {
            get => _LambdaP;
            private set => _LambdaP = value;
        }
        public double LambdaPLIM
        {
            get => _LambdaPLIM;
            private set => _LambdaPLIM = value;
        }
        public double SigmaCrp
        {
            get => _SigmaCrp;
            private set => _SigmaCrp = value;
        }
        public double Sigmacsl
        {
            get => _Sigmacsl;
            private set => _Sigmacsl = value;
        }
        public double Sigmacsl1
        {
            get => _Sigmacsl1;
            private set => _Sigmacsl1 = value;
        }
        public double Sigmacsl2
        {
            get => _Sigmacsl2;
            private set => _Sigmacsl2 = value;
        }
        #endregion
    }

    public enum Code { Eurocode, ECP205_2001_ASD };
    public enum StiffenerType { Open, Closed };
}
