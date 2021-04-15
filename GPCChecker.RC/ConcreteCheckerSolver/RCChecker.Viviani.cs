//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using GPC.Model;
//using GPC.Geometry;

//namespace GPC.Checker.ReinforcedConcrete
//{
//    public partial class RCChecker
//    {

//        #region Elastic

//        /// <summary>
//        /// Routine per calcolo tensioni in SLE elastico con parzializzazione sezione
//        /// </summary>
//        /// <param name="vtN">sforzo normale</param>
//        /// <param name="vmxNmm">momento mx</param>
//        /// <param name="vmyNmm">momento my</param>
//        /// <param name="n">numero barre</param>
//        /// <param name="m">numero vertici</param>
//        /// <param name="nom">fattore omogeneizzazione</param>
//        /// <param name="ameMm2">area barre mm2</param>
//        /// <param name="xmeMm">posizione barre x</param>
//        /// <param name="ymeMm">posizione barre x</param>
//        /// <param name="xveMm">posizione vertici cls x</param>
//        /// <param name="yveMm">posizione vertici cls y</param>
//        /// <param name="isTractionConcrete">dice se il cls lavora a trazione</param>
//        /// <param name="sssMPa">tensioni vertici cls</param>
//        /// <param name="tttMPa">tensioni barre acciaio</param>
//        /// <param name="z0">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
//        /// <param name="z1">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
//        /// <param name="z2">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
//        private void GetElasticTensionsViviani(double vtN,
//                                               double vmxNmm,
//                                               double vmyNmm,
//                                               int n,
//                                               int m,
//                                               double nom,
//                                               double[] ameMm2,
//                                               double[] xmeMm,
//                                               double[] ymeMm,
//                                               double[] xveMm,
//                                               double[] yveMm,
//                                               bool isTractionConcrete,
//                                               out double[] sssMPa,
//                                               out double[] tttMPa,
//                                               out double z0,
//                                               out double z1,
//                                               out double z2)
//        {
//            //    '  Variabili:
//            //' nom= coefficiente di omogeneizzazione
//            //' n= numero delle armature metalliche
//            //' m= numero dei vertici del calcestruzzo (NUMERARE IN SENSO ANTI-ORARIO)
//            //' ame(.)= vettore delle aree metalliche
//            //' xme(.)= vettore delle ascisse delle aree metalliche
//            //' yme(.)= vettore delle ordinate delle aree metalliche
//            //' xve(.)= vettore delle ascisse dei vertici di calcestruzzo
//            //' yve(.)= vettore delle ordinate dei vertici di calcestruzzo
//            //' sss(.)= vettore delle tensioni nel calcestruzzo (MPa)
//            //' ttt(.)= vettore delle tensioni nelle armature (MPa)
//            //' vt= sforzo normale (KN)(positivo di compressione)
//            //' vmx,vmy = momenti flettenti (kN cm)
//            //‘ CONVENZIONI COME GELFI
//            //'
//            //'
//            double ff, fx, fy, jx, jy, ic, aa, sx, sy, ix, iy, xy;
//            double[] uuu, vvv;
//            double ai, h, a1, a2, st, s0, s1, s2, atot, ta1;
//            int n0, m0;//, it, jmax, jmin;

//            uuu = new double[2 * m + 1];//buffer 
//            vvv = new double[2 * m + 1];//buffer 
//            sssMPa = new double[m + 1];
//            tttMPa = new double[n + 1];
//            //'
//            //'
//            //'  Convenzioni dei segni come GELFI
//            //'
//            vtN = -vtN;
//            vmxNmm = -vmxNmm;
//            //vmy = vmy;

//            ff = 0.0;
//            fx = 0.0;
//            fy = 0.0;
//            jx = 0.0;
//            jy = 0.0;
//            ic = 0.0;
//            //'
//            //'   caratteristiche inerziali omogeneizzate
//            //'
//            for (int i = 1; i <= n; i++)
//            {
//                xmeMm[i] = -xmeMm[i];

//                ff = ff + nom * ameMm2[i];
//                fx = fx + nom * ameMm2[i] * ymeMm[i];
//                fy = fy + nom * ameMm2[i] * xmeMm[i];
//                jx = jx + nom * ameMm2[i] * Math.Pow(ymeMm[i], 2);
//                jy = jy + nom * ameMm2[i] * Math.Pow(xmeMm[i], 2);
//                ic = ic + nom * ameMm2[i] * xmeMm[i] * ymeMm[i];
//            }
//            //'
//            //'    ricerca dell'asse neutro
//            //'
//            //'  inizializzazione delle variabili
//            //'
//            for (int i = 1; i <= m; i++)
//            {
//                xveMm[i] = -xveMm[i];
//                uuu[i] = xveMm[i];
//                vvv[i] = yveMm[i];
//            }

//            aa = 0.0;
//            sx = 0.0;
//            sy = 0.0;
//            ix = 0.0;
//            iy = 0.0;
//            xy = 0.0;
//            xveMm[0] = xveMm[m];
//            yveMm[0] = yveMm[m];
//            uuu[0] = uuu[m];
//            vvv[0] = vvv[m];
//            m0 = m;
//            //        '
//            //        '  inizio iterazioni
//            //        '
//            //        ' routine
//            if (m0 >= 2)//If m0< 2 Then// GoTo 100
//            {
//                for (int i = 0; i < m0; i++)
//                {
//                    ai = (uuu[i + 1] * vvv[i] - uuu[i] * vvv[i + 1]) / 2.0;
//                    aa = aa + ai;
//                    sx = sx + ai * (vvv[i] + vvv[i + 1]) / 3.0;
//                    sy = sy + ai * (uuu[i] + uuu[i + 1]) / 3.0;
//                    ix = ix + ai * (Math.Pow(vvv[i], 2) + vvv[i] * vvv[i + 1] + Math.Pow(vvv[i + 1], 2)) / 6.0;
//                    iy = iy + ai * (Math.Pow(uuu[i], 2) + uuu[i] * uuu[i + 1] + Math.Pow(uuu[i + 1], 2)) / 6.0;
//                    xy = xy + ai * (uuu[i] * vvv[i] + uuu[i] * vvv[i + 1] / 2.0 + uuu[i + 1] * vvv[i] / 2.0 + uuu[i + 1] * vvv[i + 1]) / 6.0;
//                }
//            }

//            //        ' return
//            //        '
//            //        '
//            //100:    
//            aa = aa + ff;
//            sx = sx + fx;
//            sy = sy + fy;
//            ix = ix + jx;
//            iy = iy + jy;
//            xy = xy + ic;
//            //        '
//            //        '
//            //        ' routine 
//            aa = Math.Sqrt(aa);
//            sy = sy / aa;
//            sx = sx / aa;
//            iy = Math.Sqrt(iy - Math.Pow(sy, 2));
//            xy = (xy - sx * sy) / iy;
//            ix = Math.Sqrt(ix - Math.Pow(sx, 2) - Math.Pow(xy, 2));
//            z0 = vtN / aa;
//            z1 = (vmyNmm - z0 * sy) / iy;
//            z2 = (vmxNmm - z0 * sx - z1 * xy) / ix;
//            z2 = z2 / ix;
//            z1 = (z1 - z2 * xy) / iy;
//            z0 = (z0 - z1 * sy - z2 * sx) / aa;
//            for (int i = 1; i <= m; i++)
//            {
//                sssMPa[i] = z0 + z1 * xveMm[i] + z2 * yveMm[i];
//            }
//            sssMPa[0] = sssMPa[m];
//            //        ' return
//            //        '
//            //        '
//            //200:    
//            bool canExit = false;
//            do
//            {
//                s0 = z0;
//                s1 = z1;
//                s2 = z2;
//                n0 = 0;
//                m0 = -1;
//                for (int i = 0; i < m; i++)
//                {
//                    h = Math.Sqrt(Math.Pow(xveMm[i + 1] - xveMm[i], 2) + Math.Pow(yveMm[i + 1] - yveMm[i], 2));
//                    a1 = (xveMm[i + 1] - xveMm[i]) / h;
//                    a2 = (yveMm[i + 1] - yveMm[i]) / h;
//                    if (isTractionConcrete || sssMPa[i] <= 0.0)
//                    {
//                        m0 = m0 + 1;
//                        uuu[m0] = xveMm[i];
//                        vvv[m0] = yveMm[i];
//                    }
//                    if (isTractionConcrete || sssMPa[i] * sssMPa[i + 1] >= 0.0) continue;// Then GoTo 101
//                    m0 = m0 + 1;
//                    st = h * sssMPa[i] / (sssMPa[i] - sssMPa[i + 1]);
//                    uuu[m0] = xveMm[i] + a1 * st;
//                    vvv[m0] = yveMm[i] + a2 * st;
//                    n0 = n0 + 1;
//                    //101:    Next i
//                }
//                //        '
//                m0 = m0 + 1;
//                uuu[m0] = uuu[0];
//                vvv[m0] = vvv[0];
//                aa = 0.0;
//                sx = 0.0;
//                sy = 0.0;
//                ix = 0.0;
//                iy = 0.0;
//                xy = 0.0;
//                //        '  routine
//                //1001: 
//                if (m0 >= 2)  //If m0< 2 Then GoTo 1002
//                {
//                    for (int i = 0; i < m0; i++)
//                    {
//                        ai = (uuu[i + 1] * vvv[i] - uuu[i] * vvv[i + 1]) / 2.0;
//                        aa = aa + ai;
//                        sx = sx + ai * (vvv[i] + vvv[i + 1]) / 3.0;
//                        sy = sy + ai * (uuu[i] + uuu[i + 1]) / 3.0;
//                        ix = ix + ai * (Math.Pow(vvv[i], 2) + vvv[i] * vvv[i + 1] + Math.Pow(vvv[i + 1], 2)) / 6.0;
//                        iy = iy + ai * (Math.Pow(uuu[i], 2) + uuu[i] * uuu[i + 1] + Math.Pow(uuu[i + 1], 2)) / 6.0;
//                        xy = xy + ai * (uuu[i] * vvv[i] + uuu[i] * vvv[i + 1] / 2.0 + uuu[i + 1] * vvv[i] / 2.0 + uuu[i + 1] * vvv[i + 1]) / 6.0;
//                    }
//                    //        ' return
//                }
//                //1002:  
//                aa = aa + ff;
//                sx = sx + fx;
//                sy = sy + fy;
//                ix = ix + jx;
//                iy = iy + jy;
//                xy = xy + ic;
//                //        ' routine
//                aa = Math.Sqrt(aa);
//                sy = sy / aa;
//                sx = sx / aa;
//                iy = Math.Sqrt(iy - Math.Pow(sy, 2));
//                xy = (xy - sx * sy) / iy;
//                ix = Math.Sqrt(ix - Math.Pow(sx, 2) - Math.Pow(xy, 2));
//                z0 = vtN / aa;
//                z1 = (vmyNmm - z0 * sy) / iy;
//                z2 = (vmxNmm - z0 * sx - z1 * xy) / ix;
//                z2 = z2 / ix;
//                z1 = (z1 - z2 * xy) / iy;
//                z0 = (z0 - z1 * sy - z2 * sx) / aa;
//                for (int i = 1; i <= m; i++)
//                {
//                    sssMPa[i] = z0 + z1 * xveMm[i] + z2 * yveMm[i];
//                }
//                sssMPa[0] = sssMPa[m];
//                //        ' return
//                //201:
//                canExit = true;
//                if (Math.Abs(z0 - s0) > 0.00001) { canExit = false; }
//                if (Math.Abs(z1 - s1) > 0.00001) { canExit = false; }
//                if (Math.Abs(z2 - s2) > 0.00001) { canExit = false; }//Then GoTo 200
//            } while (!canExit);


//            atot = 0.0;
//            for (int j = 1; j <= m; j++)
//            {
//                //            '
//                xveMm[j] = -xveMm[j];
//                //            '
//                //sss[j] = sss[j] * 10;MPa
//                if ((!isTractionConcrete) && (sssMPa[j] > 0.0))
//                {
//                    sssMPa[j] = 0.0;
//                    continue;//  GoTo 103
//                }
//                //103:    Next j
//            }

//            for (int j = 1; j <= n; j++)
//            {
//                ta1 = nom * (z0 + z1 * xmeMm[j] + z2 * ymeMm[j]);
//                atot = atot + ameMm2[j];
//                //ta1 = ta1 * 10;//MPa
//                tttMPa[j] = ta1;
//                xmeMm[j] = -xmeMm[j];
//            }
//            //        '
//            //        '     USCITE
//            z1 *= -1;
//        }

//        #endregion

//        #region Plastic

//        // NB: porting c# di fortran code. arrays must be based on first index=1!
//        // NOTE:
//        //- manca possibilità di definire i limiti di deformazione
//        //- manca limitazione a 0.8NMaxRd per stati di compressione senza M

//        //c
//        //c------------------------------------------------------------------c
//        //c SUBROUTINE  PER IL   CALCOLO DELLE  CARATTERISTICHE c
//        //c ULTIME   PER UNA   SEZIONE QUALSIASI                      c
//        //c------------------------------------------------------------------c
//        //c
//        //      subroutine  limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl,
//        //     1ic, fys, fysl, fck, gs, gsl, gc, eta, teta, e, x, y, z, b, h,
//        //     1kvc, kvp, ndkvc, ndkvp, ipc, ndpc, npc, aux, ndaux)
//        //      dimension vc(ndvc), vs(ndvs), kvp(ndkvp), kvc(ndkvc), ipc(ndpc),
//        //     1aux(ndaux), vsl(ndvsl)
//        //      double precision vc, vs, vsl, fys, fysl, fck, gs, gsl, gc, eta, teta,
//        //     1e, x, y, z, b, h, aux, w2, w0, chi, cmax, cmin, dmin, dmax, dmin1, dmax1,
//        //     1epsc, epst, s, c, xa, ya, xb, yb, af, sig, epsa, epsb, w1, waux, xf, yf, v1,
//        //     1eps, xc, yc
//        //      double precision depsil, dsgmaf, dsgmac
//        //c
//        //c   descrizione delle variabili:
//        //c   VC = vettore organizzato in gruppi di tre elementi, contenente
//        //c       i dati relativi ai vertici della sezione in calcestruzzo.
//        //c   NVC = numero dei gruppi di VC(vertici)
//        //c   VS = vettore organizzato in gruppi di tre elementi, contenente
//        //c       i dati relativi alle armature metalliche puntiformi:
//        //c        As - X - Y
//        //c   NVS = numero dei gruppi di VS
//        //c   VSL = vettore organizzato in gruppi di cinque elementi, contenente
//        //c        i dati relativi alle armature metalliche lineari:
//        //c        As - Xi - Yi - Xf - Yf
//        //c   NVSL = numero dei gruppi di VSL
//        //c   IPC = vettore organizzato in gruppi di elementi che descrivono
//        //c        una particolare partizione convessa.In particolare il primo
//        //c        indice riporta il numero dei vertici della partizione
//        //c        considerata.
//        //c   NPC = numero delle parti convesse della figura.
//        //c   AUX, KVP, KVC = vettori di lavoro
//        //c   NDAUX, NDKVP, NDKVC = dimensioni dei vettori sopra
//        //c   IC = indice di zona di rottura 1 - 2 - 3 - 4 - 5 - 6
//        //c   B = lato parallelo all'asse X
//        //c   H = "           "         Y
//        //c   vse = vettore contenente, per ogni barra di armatura, modulo elastico, pretensione e fyk
//        //c    Z = SFORZO NORMALE, positivo se di compressione
//        //c    Y = MOM.FLETTENTE attorno all'asse y, positivo se antiorario
//        //c    X = MOM.FLETTENTE attorno all'asse x, positivo se antiorario
//        //c   FYS = tensione di snervamento dell'acciaio  puntiforme
//        //c   FYSL = "               "          "         lineare
//        //c   FCK = tensione caratteristica del calcestruzzo(cilindrica)
//        //c   GC = coefficiente riduttivo per il cls. (1.5 con EC, 1 con AASHTO)
//        //c   GS = "               "     per l'acciaio  puntiforme. (1.15 con EC, 1 con AASHTO)
//        //c   GSL = "               "           "        lineare. (1.05 con EC, 1 con AASHTO)
//        //c   E = modulo elastico dell'acciaio lineare (per le armature vedi vse)
//        //c   ETA = primo parametro
//        //c   TETA = secondo parametro
//        //c   N.B.: i valori di epsilon sono moltiplicati per mille
//        //    epcCMax valore della deformazione max del cls in valore assoluto (-0.0035 con EC)
//        //    fiTensAASHTO: valore di fi a trazione per norma AASHTO (0.9 con aashto, 1 con EC)
//        //    fiCompAASHTO: valore di fi compressione per norma AASHTO (0.75 con aashto, 1 con EC)
//        //    epsCl: limite deformazione acciaio per considerare fiComp (0.002 per AASHTO)
//        //    epsTl: limite deformazione acciaio per considerare fiTens (0.005 per AASHTO)
//        //    alfaCC: fattore riduzione resist cls (0.85 per EC, 1 per AASHTO)
//        //    epsSu: deformazione ultima dell'acciaio
//        private void GetPlasticResistancesCAViviani(double[] vc, double[] vs, double[] vsl, int nvc, int nvs, int nvsl, int ic,
//                                                     double fysl, double fck, double gs, double gsl, double gc, double eta, double teta,
//                                                     double esl, out double x, out double y, out double z, double b, double h, double[] vse,
//                                                     int[] ipc, int npc,
//                                                     double epsCMin, double fiTensAASHTO, double fiCompAASHTO, double alfaCC, bool useLimit34AASHTO,
//                                                     double epsSu)
//        {
//            double w2, w0, chi, cmax, cmin, dmin, dmax, dmin1, dmax1, epsc, s, c, xa, ya, xb, yb, af, sig, epsa, epsb,
//                   w1, xf, yf, v1, eps, xc, yc;

//            const double ksi60 = 413.68;
//            const double ksi100 = 689.476;
//            const double ksi75 = 517.107;
//            double[] aux = new double[nvc * 10];
//            int[] kvc = new int[nvc * 10];
//            int[] kvp = new int[nvc * 10];
//            double epsCMaxAbs1000, epsCl1000, epsTl1000;
//            double epsCl, epsTl;
//            double fi;
//            double epsSu1000;
//            bool isPrestressed;
//            double minFys, esOnMin;

//            //c
//            //c
//            //c---- scelta della zona di rottura
//            //c
//            x = 0;
//            y = 0;
//            z = 0;
//            dmax = double.MinValue;//0;
//            dmin = double.MaxValue;// 0;
//            xc = 0;
//            yc = 0;
//            s = Math.Sin(teta);
//            c = Math.Cos(teta);
//            epsCMaxAbs1000 = -epsCMin * 1000;


//            isPrestressed = false;
//            minFys = double.MaxValue;
//            esOnMin = double.MaxValue;
//            for (int i = 1; i < nvs; i++)
//            {
//                int j1 = 3 * (i - 1) + 1;
//                if (minFys > vse[j1 + 2])
//                {
//                    minFys = vse[j1 + 2];
//                    esOnMin = vse[j1];
//                }
//                if (Math.Abs(vse[j1 + 1]) > 0.1)
//                {
//                    isPrestressed = true;
//                }
//            }

//            if (isPrestressed)
//            {
//                epsCl = 0.002;
//                epsTl = 0.005;
//            }
//            else
//            {

//                if (minFys <= ksi60)
//                {
//                    epsCl = Math.Min(0.002, minFys / esOnMin);
//                }
//                else if (minFys >= ksi100)
//                {
//                    epsCl = 0.004;
//                }
//                else
//                {
//                    double lower, upper;
//                    lower = Math.Min(0.002, minFys / esOnMin);
//                    upper = 0.004;
//                    epsCl = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(ksi60, ksi100, lower, upper, minFys);
//                }

//                if (minFys <= ksi75)
//                {
//                    epsTl = 0.005;
//                }
//                else if (minFys >= ksi100)
//                {
//                    epsTl = 0.008;
//                }
//                else
//                {
//                    double lower, upper;
//                    lower = 0.005;
//                    upper = 0.008;
//                    epsTl = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(ksi75, ksi100, lower, upper, minFys);
//                }
//            }

//            /// deformazioni in ‰ (per mille)
//            epsCl1000 = epsCl * 1000;
//            epsTl1000 = epsTl * 1000;
//            epsSu1000 = epsSu * 1000;

//            if (useLimit34AASHTO && epsTl1000 >= epsSu1000)
//            {
//                throw new NotSupportedException("In AASHTO epsSu cannot be < epsTl = " + epsTl.ToString("F2"));
//            }

//            /// CAMPO DI RESISTENZA 1 
//            //      goto(10,20,30,40,50,60),ic
//            if (ic == 1)
//            {
//                //c
//                //c---- sezione interamente tesa.
//                //c
//                //c ----campo di rottura n.1
//                //c
//                //10    do 11 j = 1,nvs
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vs(j1 + 1) * c - vs(j1) * s
//                //      if (w1.le.dmin) then
//                //                      dmin = w1
//                //                    xc = vs(j1)
//                //                    yc = vs(j1 + 1)
//                //                    goto 11
//                //      endif
//                //      if (w1.ge.dmax) dmax = w1
//                //11    continue
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vs[j1 + 1] * c - vs[j1] * s;
//                    if (w1 <= dmin)
//                    {
//                        dmin = w1;
//                        xc = vs[j1];
//                        yc = vs[j1 + 1];
//                        continue;//goto 11
//                    }
//                    if (w1 >= dmax)
//                    {
//                        dmax = w1;
//                    }
//                }

//                //      do 13 j = 1,nvsl
//                //        j1 = 5 * (j - 1) + 2
//                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
//                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
//                //      if (w1.le.dmin) then
//                //                      dmin = w1
//                //                    xc = vsl(j1)
//                //                    yc = vsl(j1 + 1)
//                //                    goto 14
//                //      endif
//                //      if (w1.ge.dmax) dmax = w1
//                //14    if (w2.le.dmin) then
//                //                      dmin = w2
//                //                    xc = vsl(j1 + 2)
//                //                    yc = vsl(j1 + 3)
//                //                    goto 13
//                //      endif
//                //      if (w2.ge.dmax) dmax = w2
//                //13    continue
//                for (int j = 1; j <= nvsl; j++)
//                {
//                    int j1 = 5 * (j - 1) + 2;
//                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
//                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
//                    if (w1 <= dmin)
//                    {
//                        dmin = w1;
//                        xc = vsl[j1];
//                        yc = vsl[j1 + 1];
//                    }
//                    else if (w1 >= dmax)
//                    {
//                        dmax = w1;
//                    }
//                    if (w2 <= dmin)
//                    {
//                        dmin = w2;
//                        xc = vsl[j1 + 2];
//                        yc = vsl[j1 + 3];
//                    }
//                    else if (w2 >= dmax)
//                    {
//                        dmax = w2;
//                    }
//                }

//                //      dmax1 = 0.
//                //      do 12 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax1) dmax1 = w1
//                //12    continue
//                //      cmin = (10.- fys / e * 1000./ gs) / (dmax - dmin)
//                //      cmax = 10./ (dmax1 - dmin)
//                //      chi = cmin + eta * (cmax - cmin)
//                //      v1 = 10.
//                //      epsc = v1 - chi * (dmax1 - dmin)
//                //      if (epsc.lt.0.0001.and.eta.le.1.)then
//                //                                       chi = cmax
//                //                                      epsc = 0.
//                //      endif
//                //      w0 = dmax1 + epsc / chi
//                //      w2 = w0 + 2./ chi
//                //      goto 100
//                dmax1 = 0;
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax1) 
//                        dmax1 = w1;
//                }


//                cmin = (epsSu1000 - minFys / esOnMin * 1000.0 / gs) / (dmax - dmin);
//                cmax = epsSu1000 / (dmax1 - dmin);
//                chi = cmin + eta * (cmax - cmin);
//                v1 = epsSu1000;
//                epsc = v1 - chi * (dmax1 - dmin);
//                if (epsc < 0.0001 && eta <= 1)
//                {
//                    chi = cmax;
//                    epsc = 0;
//                }
//                //c W2, W0 = parametri per le rette con eps = 2 / 1000 e eps = 0.
//                w0 = dmax1 + epsc / chi;
//                w2 = w0 + 2.0 / chi;
//                fi = fiTensAASHTO;
//            }


//            /// CAMPO DI RESISTENZA 2
//            else if (ic == 2)
//            {
//                //c
//                //c ----sezione parzializzata con armatura tesa al massimo allungamento
//                //c
//                //c---- campo di rottura n.2
//                //c
//                //20    do 21 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax) dmax = w1
//                //21    continue
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax) dmax = w1;
//                }

//                //      do 22 j = 1,nvs
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vs(j1 + 1) * c - vs(j1) * s
//                //      if (w1.le.dmin) then
//                //                      dmin = w1
//                //                    xc = vs(j1)
//                //                    yc = vs(j1 + 1)
//                //      endif
//                //22    continue
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vs[j1 + 1] * c - vs[j1] * s;
//                    if (w1 <= dmin)
//                    {
//                        dmin = w1;
//                        xc = vs[j1];
//                        yc = vs[j1 + 1];
//                    }
//                }

//                //      do 23 j = 1,nvsl
//                //        j1 = 5 * (j - 1) + 2
//                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
//                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
//                //      if (w1.le.dmin) then
//                //                      dmin = w1
//                //                    xc = vsl(j1)
//                //                    yc = vsl(j1 + 1)
//                //      endif
//                //      if (w2.le.dmin) then
//                //                      dmin = w2
//                //                    xc = vsl(j1 + 2)
//                //                    yc = vsl(j1 + 3)
//                //      endif
//                //23    continue
//                for (int j = 1; j <= nvsl; j++)
//                {
//                    int j1 = 5 * (j - 1) + 2;
//                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
//                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
//                    if (w1 <= dmin)
//                    {
//                        dmin = w1;
//                        xc = vsl[j1];
//                        yc = vsl[j1 + 1];
//                    }
//                    if (w2 <= dmin)
//                    {
//                        dmin = w2;
//                        xc = vsl[j1 + 2];
//                        yc = vsl[j1 + 3];
//                    }
//                }

//                //      cmax = 13.5 / (dmax - dmin)
//                //      cmin = 10./ (dmax - dmin)
//                //      chi = cmin + eta * (cmax - cmin)
//                //      v1 = 10.
//                //      epsc = v1 - (dmax - dmin) * chi
//                //      w0 = dmax + epsc / chi
//                //      w2 = w0 + 2./ chi
//                //      goto 100
//                cmax = (epsSu1000 + epsCMaxAbs1000) / (dmax - dmin);
//                cmin = epsSu1000 / (dmax - dmin);
//                ///inclinazione superficie di rottura dipende da che campo di rottura sono e dall'immersione nel campo di rottura
//                chi = cmin + eta * (cmax - cmin); 

//                v1 = epsSu1000;
//                epsc = v1 - (dmax - dmin) * chi;
//                /// asse neutro valutato dal lembro piu compesso
//                w0 = dmax + epsc / chi; 
//                w2 = w0 + 2.0 / chi;
//                fi = fiTensAASHTO;
//            }

//            /// CAMPO DI RESISTENZA 3
//            else if (ic == 3)
//            {
//                //c
//                //c ----sezione parzializzata
//                //c
//                //c
//                //c ----campo di rottura n.3
//                //c
//                //30    do 31 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax) then
//                //                      dmax = w1
//                //                    xc = vc(j1)
//                //                    yc = vc(j1 + 1)
//                //      endif
//                //31    continue
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax)
//                    {
//                        dmax = w1;
//                        xc = vc[j1];
//                        yc = vc[j1 + 1];
//                    }
//                }

//                //      do 32 j = 1,nvs
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vs(j1 + 1) * c - vs(j1) * s
//                //      if (w1.le.dmin) dmin = w1
//                //32    continue
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vs[j1 + 1] * c - vs[j1] * s;
//                    if (w1 <= dmin) dmin = w1;
//                }

//                //      do 33 j = 1,nvsl
//                //        j1 = 5 * (j - 1) + 2
//                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
//                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
//                //      if (w1.le.dmin) dmin = w1
//                //      if (w2.le.dmin) dmin = w2
//                //33    continue
//                for (int j = 1; j <= nvsl; j++)
//                {
//                    int j1 = 5 * (j - 1) + 2;
//                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
//                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
//                    if (w1 <= dmin) dmin = w1;
//                    if (w2 <= dmin) dmin = w2;
//                }

//                //      v1 = -3.5
//                //      cmin = (fys / gs / e * 1000.+ 3.5) / (dmax - dmin)
//                //      cmax = 13.5 / (dmax - dmin)
//                //      chi = cmax + eta * (cmin - cmax)
//                //      w0 = dmax + v1 / chi
//                //      w2 = w0 + 2./ chi
//                //      goto 100
//                v1 = -epsCMaxAbs1000;
//                if (useLimit34AASHTO)
//                {
//                    cmin = (epsCl1000 + epsCMaxAbs1000) / (dmax - dmin);
//                }
//                else
//                {
//                    cmin = (minFys / gs / esOnMin * 1000 + epsCMaxAbs1000) / (dmax - dmin);
//                }
//                cmax = (epsSu1000 + epsCMaxAbs1000) / (dmax - dmin);
//                chi = cmax + eta * (cmin - cmax);
//                w0 = dmax + v1 / chi;
//                w2 = w0 + 2.0 / chi;
//                double et = chi * (dmax - dmin) - epsCMaxAbs1000;

//                if (et <= epsCl1000)
//                {
//                    fi = fiCompAASHTO;
//                }
//                else if (et >= epsTl1000)
//                {
//                    fi = fiTensAASHTO;
//                }
//                else
//                {
//                    if (Math.Abs(epsTl1000 - epsCl1000) < 0.00001)
//                    {
//                        fi = fiCompAASHTO;
//                    }
//                    else
//                    {
//                        fi = fiCompAASHTO + (fiTensAASHTO - fiCompAASHTO) * (et - epsCl1000) / (epsTl1000 - epsCl1000);
//                    }
//                }
//            }



//            /// CAMPO DI RESISTENZA 4
//            else if (ic == 4)
//            {
//                //c
//                //c ----campo di rottura n.4
//                //c
//                //40    do 41 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax) then
//                //                      dmax = w1
//                //                    xc = vc(j1)
//                //                    yc = vc(j1 + 1)
//                //      endif
//                //41    continue
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax)
//                    {
//                        dmax = w1;
//                        xc = vc[j1];
//                        yc = vc[j1 + 1];
//                    }
//                }

//                //      do 42 j = 1,nvs
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vs(j1 + 1) * c - vs(j1) * s
//                //      if (w1.le.dmin) dmin = w1
//                //42    continue
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vs[j1 + 1] * c - vs[j1] * s;
//                    if (w1 <= dmin) dmin = w1;
//                }

//                //      do 43 j = 1,nvsl
//                //        j1 = 5 * (j - 1) + 2
//                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
//                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
//                //      if (w1.le.dmin) dmin = w1
//                //      if (w2.le.dmin) dmin = w2
//                //43    continue
//                for (int j = 1; j <= nvsl; j++)
//                {
//                    int j1 = 5 * (j - 1) + 2;
//                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
//                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
//                    if (w1 <= dmin) dmin = w1;
//                    if (w2 <= dmin) dmin = w2;
//                }

//                //      v1 = -3.5
//                //      cmax = (fys / gs / e * 1000.+ 3.5) / (dmax - dmin)
//                //      cmin = 3.5 / (dmax - dmin)
//                //      chi = cmax + eta * (cmin - cmax)
//                //      w0 = dmax + v1 / chi
//                //      w2 = w0 + 2./ chi
//                //      goto 100
//                v1 = -epsCMaxAbs1000;
//                if (useLimit34AASHTO)
//                {
//                    cmax = (epsCl1000 + epsCMaxAbs1000) / (dmax - dmin);
//                }
//                else
//                {
//                    cmax = (minFys / gs / esOnMin * 1000 + epsCMaxAbs1000) / (dmax - dmin);
//                }
//                cmin = epsCMaxAbs1000 / (dmax - dmin);
//                chi = cmax + eta * (cmin - cmax);
//                w0 = dmax + v1 / chi;
//                w2 = w0 + 2.0 / chi;
//                fi = fiCompAASHTO;
//            }


//            /// CAMPO DI RESISTENZA 5
//            else if (ic == 5)
//            {
//                //c
//                //c ----sezione parzializzata, armatura metallica tutta compressa
//                //c
//                //c
//                //c---- campo di rottura n.5
//                //c
//                //50    do 51 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax) then
//                //                      dmax = w1
//                //                    xc = vc(j1)
//                //                    yc = vc(j1 + 1)
//                //      endif
//                //      if (w1.le.dmin) dmin = w1
//                //51    continue
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax)
//                    {
//                        dmax = w1;
//                        xc = vc[j1];
//                        yc = vc[j1 + 1];
//                    }
//                    if (w1 <= dmin) dmin = w1;
//                }

//                //      dmin1 = 0.
//                //      do 52 j = 1,nvs
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vs(j1 + 1) * c - vs(j1) * s
//                //      if (w1.le.dmin1) dmin1 = w1
//                //52    continue
//                dmin1 = 0;
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vs[j1 + 1] * c - vs[j1] * s;
//                    if (w1 <= dmin1) dmin1 = w1;
//                }

//                //      do 53 j = 1,nvsl
//                //        j1 = 5 * (j - 1) + 2
//                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
//                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
//                //      if (w1.le.dmin1) dmin1 = w1
//                //      if (w2.le.dmin1) dmin1 = w2
//                //53    continue
//                for (int j = 1; j <= nvsl; j++)
//                {
//                    int j1 = 5 * (j - 1) + 2;
//                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
//                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
//                    if (w1 <= dmin1) dmin1 = w1;
//                    if (w2 <= dmin1) dmin1 = w2;
//                }

//                //      v1 = -3.5
//                //      cmax = 3.5 / (dmax - dmin1)
//                //      cmin = 3.5 / (dmax - dmin)
//                //      chi = cmax + eta * (cmin - cmax)
//                //      w0 = dmax + v1 / chi
//                //      w2 = w0 + 2./ chi
//                //      goto 100
//                v1 = -epsCMaxAbs1000;
//                cmax = epsCMaxAbs1000 / (dmax - dmin1);
//                cmin = epsCMaxAbs1000 / (dmax - dmin);
//                chi = cmax + eta * (cmin - cmax);
//                w0 = dmax + v1 / chi;
//                w2 = w0 + 2.0 / chi;
//                fi = fiCompAASHTO;
//            }


//            /// CAMPO DI RESISTENZA 6
//            else if (ic == 6)
//            {
//                //c
//                //c ----sezione interamente compressa
//                //c
//                //c ----campo di rottura n.6
//                //c
//                //60    do 61 j = 1,nvc
//                //        j1 = 3 * (j - 1) + 2
//                //      w1 = vc(j1 + 1) * c - vc(j1) * s
//                //      if (w1.ge.dmax) dmax = w1
//                //      if (w1.le.dmin) dmin = w1
//                //61    continue
//                for (int j = 1; j <= nvc; j++)
//                {
//                    int j1 = 3 * (j - 1) + 2;
//                    w1 = vc[j1 + 1] * c - vc[j1] * s;
//                    if (w1 >= dmax) dmax = w1;
//                    if (w1 <= dmin) dmin = w1;
//                }

//                //c---- calcolo del punto di rotazione
//                //     xc = -3./ 7.* (dmax - dmin) + dmax
//                //      w2 = xc
//                //      yc = xc * c
//                //      xc = -xc * s
//                //      v1 = -2.
//                //      cmax = 3.5 / (dmax - dmin)
//                //      cmin = 0.
//                //      chi = cmax * (1.- eta)
//                //      w1 = 1.
//                //      if (dabs(chi).gt.0.0001)w1 = chi
//                //      w0 = w2 - 2./ w1

//                //xc = -3.0 / 7.0 * (dmax - dmin) + dmax; modificato per introduzione epscMax
//                xc = -(epsCMaxAbs1000 - 2) / epsCMaxAbs1000 * (dmax - dmin) + dmax;
//                w2 = xc;
//                yc = xc * c;
//                xc = -xc * s;
//                v1 = -2;
//                cmax = epsCMaxAbs1000 / (dmax - dmin);
//                cmin = 0;
//                chi = cmax * (1 - eta);
//                w1 = 1;
//                if (Math.Abs(chi) > 0.0001) w1 = chi;
//                w0 = w2 - 2.0 / w1;
//                fi = fiCompAASHTO;
//            }
//            else
//            {
//                throw new NotSupportedException("Campo di rottura inesistente");
//            }

//            //c
//            //c ----calcolo delle sollecitazioni ultime
//            //c
//            //c---- calcolo del solido tensionale esteso all'area di calcestruzzo
//            //c
//            //c
//            //100   call tecls(vc, nvc, ndvc, ipc, npc, ndpc, kvc, ndkvc, kvp, ndkvp,
//            //     1aux, ndaux, xc, yc, chi, v1, s, c, w2, w0, fck, gc, b, h, eta, teta, x, y, z)

//            tecls(ref vc, ref nvc, ref ipc, ref npc, ref kvc, ref kvp, ref aux, ref xc, ref yc, ref chi, ref v1, ref s,
//                  ref c, ref w2, ref w0, ref fck, ref gc, ref b, ref h, ref eta, ref teta, ref x, ref y, ref z, ref alfaCC);

//            //c
//            //c ----integrazione delle armature metalliche
//            //c
//            //c---- armature puntiformi
//            //c
//            //110   if (nvs.lt.1)goto 120
//            //      do 201 j = 1,nvs
//            //        j1 = 3 * (j - 1) + 1
//            //      af = vs(j1)
//            //      xf = vs(j1 + 1)
//            //      yf = vs(j1 + 2)
//            //      eps = depsil(xc, yc, s, c, chi, v1, xf, yf)
//            //      sig = dsgmaf(fys, gs, eps, e, fck, gc)
//            //      if (sig.lt.0.)sig = sig + dsgmac(fck, gc, eps)
//            //      z = z - af * sig / b / h
//            //      y = y - af * sig * xf / b / h / b
//            //201   x = x + af * sig * yf / b / h / h
//            if (!(nvs < 1))
//            {
//                for (int j = 1; j <= nvs; j++)
//                {
//                    int j1 = 3 * (j - 1) + 1;
//                    double ej, sigma0, fy;
//                    af = vs[j1];
//                    xf = vs[j1 + 1];
//                    yf = vs[j1 + 2];
//                    ej = vse[j1];
//                    sigma0 = vse[j1 + 1];
//                    fy = vse[j1 + 2];
//                    eps = depsil(xc, yc, s, c, chi, v1, xf, yf);
//                    sig = dsgmaf(fy, gs, eps, ej, fck, gc, sigma0);
//                    if (sig < 0) sig = sig + dsgmac(fck, gc, eps, alfaCC);
//                    z = z - af * sig / b / h;
//                    y = y - af * sig * xf / b / h / b;
//                    x = x + af * sig * yf / b / h / h;
//                }
//            }

//            //c
//            //c ----armature lineari
//            //c
//            //120   if (nvsl.lt.1)goto 500
//            //      nel = 3
//            //      do 121 j = 1,nvsl
//            //        j1 = 5 * (j - 1) + 1
//            //      af = vsl(j1)
//            //      xa = vsl(j1 + 1)
//            //      ya = vsl(j1 + 2)
//            //      xb = vsl(j1 + 3)
//            //      yb = vsl(j1 + 4)
//            //      epsa = depsil(xc, yc, s, c, chi, v1, xa, ya)
//            //      epsb = depsil(xc, yc, s, c, chi, v1, xb, yb)
//            //      if (epsa.le.epsb) goto 122
//            //c---- scambio di A con B
//            //     w1 = xa
//            //      xa = xb
//            //      xb = w1
//            //      w1 = ya
//            //      ya = yb
//            //      yb = w1
//            //      w1 = epsa
//            //      epsa = epsb
//            //      epsb = w1
//            //122   aux(1) = -fysl / e / gsl * 1000.
//            //      aux(2) = -aux(1)
//            //      aux(3) = 10.
//            //      call ilgs(xa, ya, epsa, xb, yb, epsb, nel, aux, ndaux, x, y, z,
//            //     1xc, yc, s, c, chi, v1, fysl, gsl, e, fck, gc, af, b, h)
//            //      aux(1) = -3.5
//            //      aux(2) = -2.
//            //      aux(3) = 0.
//            //121   call ilgc(xa, ya, epsa, xb, yb, epsb, nel, aux, ndaux, x, y, z,
//            //     1xc, yc, s, c, chi, v1, fck, gc, af, b, h)
//            //500   return
//            //      end
//            //c


//            if (!(nvsl < 1))
//            {
//                int nel = 3;
//                for (int j = 1; j <= nvsl; j++)//121
//                {
//                    int j1 = 5 * (j - 1) + 1;
//                    af = vsl[j1];
//                    xa = vsl[j1 + 1];
//                    ya = vsl[j1 + 2];
//                    xb = vsl[j1 + 3];
//                    yb = vsl[j1 + 4];
//                    epsa = depsil(xc, yc, s, c, chi, v1, xa, ya);
//                    epsb = depsil(xc, yc, s, c, chi, v1, xb, yb);
//                    //      if (epsa.le.epsb) goto 122
//                    if (!(epsa <= epsb))
//                    {
//                        //c---- scambio di A con B
//                        w1 = xa;
//                        xa = xb;
//                        xb = w1;
//                        w1 = ya;
//                        ya = yb;
//                        yb = w1;
//                        w1 = epsa;
//                        epsa = epsb;
//                        epsb = w1;
//                    }
//                    aux[1] = -fysl / esl / gsl * 1000;//122
//                    aux[2] = -aux[1];
//                    aux[3] = epsSu1000;
//                    ilgs(ref xa, ref ya, ref epsa, ref xb, ref yb, ref epsb, ref nel, ref aux, ref x, ref y, ref z,
//                         ref xc, ref yc, ref s, ref c, ref chi, ref v1, ref fysl, ref gsl, ref esl, ref fck, ref gc, ref af, ref b, ref h);
//                    aux[1] = -epsCMaxAbs1000;
//                    aux[2] = -2;
//                    aux[3] = 0;
//                    ilgc(ref xa, ref ya, ref epsa, ref xb, ref yb, ref epsb, ref nel, ref aux, ref x, ref y, ref z,
//                         ref xc, ref yc, ref s, ref c, ref chi, ref v1, ref fck, ref gc, ref af, ref b, ref h, ref alfaCC);
//                }
//            }
//            x *= fi;
//            y *= fi;
//            z *= fi;
//            //500   return
//            //      end
//            //c
//        }

//        //c-------------------------------------------------------------------c
//        //c Subroutine  per integrare il solido delle tensioni sull'intera  c
//        //c superficie di calcestruzzo.                                     c
//        //c-------------------------------------------------------------------c
//        //c
//        //      subroutine tecls(vc, nvc, ndvc, ipc, npc, ndpc, kvc, ndkvc, kvp, ndkvp,
//        //     1aux, ndaux, xc, yc, chi, v1, s, c, w2, w0, fck, gc, b, h, eta, teta, x, y, z)
//        private void tecls(ref double[] vc, ref int nvc, ref int[] ipc, ref int npc, ref int[] kvc, ref int[] kvp,
//                           ref double[] aux, ref double xc, ref double yc, ref double chi, ref double v1, ref double s,
//                           ref double c, ref double w2, ref double w0, ref double fck, ref double gc, ref double b, ref double h,
//                           ref double eta, ref double teta, ref double x, ref double y, ref double z, ref double alfaCc)

//        {
//            double x1, x2, x3, y1, y2, y3, epst, epsc, xv, yv, w1, cs;
//            //c
//            //c---- Descrizione delle variabili:
//            //            c VC = vettore contenente i vertici della sezione in cls.
//            //        c NVC = numero dei vertici in cls.
//            //     c IPC = vettore contenente gli indici delle parti convesse
//            //c NPC = numero delle parti convesse
//            //c      KVC = vettore contenente i vertici con tensione parabolica
//            //c KVP = vettore contenente i vertici con tensione costante
//            //c AUX = vettore di lavoro
//            //c NDVC, NDPC, NDKVC, NDKVP, NDAUX = dimensioni dei vettori corr.
//            //c XC, YC, CHI, V1 = parametri del piano di deformazione
//            //c S, C, ETA, TETA = parametri di posizione dell'asse neutro
//            //c W2, W0 = parametri per le rette con eps = 2 / 1000 e eps = 0.
//            //  c      FCK,GC = parametri di resistenza del calcestruzzo
//            //c B, H = parametri di adimensionalizzazione
//            //c X, Y, Z = caratteristiche della sollecitazione.
//            //c
//            //      dimension ipc(ndpc),aux(ndaux),kvc(ndkvc),kvp(ndkvp),vc(ndvc)
//            //c
//            //c ---trasferimento di ogni singola parte convessa
//            //c     in un vettore di lavoro.
//            //c
//            //      inpc = 1
//            //      ni = 1

//            int inpc = 1;
//            int ni = 1;
//            do//70
//            {
//                //70    n1 = ni + 1
//                int n1 = ni + 1;

//                //c
//                //c ---calcolo del segno del solido tensionale
//                //c
//                //      cs = dble(ipc(ni))
//                //      cs = cs / dabs(cs)
//                //      n2 = ni + iabs(ipc(ni))
//                //      ni = n2 + 1
//                //      i0 = 0
//                cs = ipc[ni];
//                cs = cs / Math.Abs(cs);
//                int n2 = ni + Math.Abs(ipc[ni]);
//                ni = n2 + 1;
//                int i0 = 0;

//                //      do 10 i = n1,n2
//                //        i0 = i0 + 1
//                //      i1 = 3 * (ipc(i) - 1) + 2
//                //      j1 = 3 * (i0 - 1) + 2
//                //      aux(j1) = vc(i1)
//                //10    aux(j1 + 1) = vc(i1 + 1)
//                for (int i = n1; i <= n2; i++)
//                {
//                    i0 = i0 + 1;
//                    int i1 = 3 * (ipc[i] - 1) + 2;
//                    int j1 = 3 * (i0 - 1) + 2;
//                    aux[j1] = vc[i1];
//                    aux[j1 + 1] = vc[i1 + 1];
//                }


//                //c
//                //c ----calcolo dei valori estremi di deformazione della partizione
//                //c     convessa
//                //c
//                //      naux = n2 - n1 + 1
//                //      epsc = 20.
//                //      epst = -4.
//                int naux = n2 - n1 + 1;
//                epsc = 20;
//                epst = -4;

//                //      do 20 i = 1,naux
//                //        i1 = 3 * (i - 1) + 2
//                //      xv = aux(i1)
//                //      yv = aux(i1 + 1)
//                //      w1 = depsil(xc, yc, s, c, chi, v1, xv, yv)
//                //      if (w1.le.epsc) then
//                //                      ivc = i
//                //                    epsc = w1
//                //      endif
//                //      if (w1.ge.epst) then
//                //                      ivt = i
//                //                    epst = w1
//                //      endif
//                //20    continue
//                int ivc = 0, ivt = 0;
//                for (int i = 1; i <= naux; i++)
//                {
//                    int i1 = 3 * (i - 1) + 2;
//                    xv = aux[i1];
//                    yv = aux[i1 + 1];
//                    w1 = depsil(xc, yc, s, c, chi, v1, xv, yv);
//                    if (w1 <= epsc)
//                    {
//                        ivc = i;
//                        epsc = w1;
//                    }
//                    if (w1 >= epst)
//                    {
//                        ivt = i;
//                        epst = w1;
//                    }
//                }

//                //c
//                //c ----calcolo del tipo di funzione da integrare (da 1 a 6)
//                //c
//                //c     IZ = 1  partizione interamente tesa.
//                // c      "  2       "     parzialmente compressa parabolicamente.
//                // c      "  3       "     interamente     "            ".
//                // c      "  4       "         "       compressa con tratti costanti
//                //c e tratti parabolici.
//                //c      "  5       "     interamente compressa in modo costante.
//                //c      "  6       "     parzialmente compressa con tratti costanti
//                //c                       e tratti parabolici.
//                //c
//                //      if (epsc.ge.- 0.001) then
//                //                       iz = 1
//                //                    goto 50
//                //      endif
//                int iz;
//                if (epsc >= -0.001)
//                {
//                    iz = 1;
//                }
//                else
//                {
//                    //      if (epst.gt.0..and.epsc.ge.- 2.)then
//                    //                                      iz = 2
//                    //                                    goto 30
//                    //      endif
//                    //      if (epst.le.0..and.epsc.ge.- 2.)then
//                    //                                      iz = 3
//                    //                                    goto 30
//                    //      endif
//                    //      if (epst.gt.0.)then
//                    //                     iz = 6
//                    //                    goto 30
//                    //      endif
//                    //      if (epst.le.- 2.) then
//                    //                        iz = 5
//                    //                     goto 30
//                    //      endif
//                    //      iz = 4
//                    if (epst > 0 && epsc >= -2)
//                    {
//                        iz = 2;
//                    }
//                    else if (epst <= 0 && epsc >= -2)
//                    {
//                        iz = 3;
//                    }
//                    else if (epst > 0)
//                    {
//                        iz = 6;
//                    }
//                    else if (epst <= -2)
//                    {
//                        iz = 5;
//                    }
//                    else
//                    {
//                        iz = 4;
//                    }

//                    //c
//                    //c ----chiamata della routine zone per la divisione della partizione
//                    //c convessa
//                    //c
//                    //30    nsp = 0
//                    //      nsc = 0
//                    //      call zone(aux, naux, ndaux, epsc, epst, w2, w0, kvc, kvp, nsp, nsc,
//                    //     1ndkvc, ndkvp, ivc, ivt, s, c, iz)
//                    int nsp = 0;
//                    int nsc = 0;
//                    zone(ref aux, ref naux, ref epsc, ref epst, ref w2, ref w0, ref kvc, ref kvp, ref nsp, ref nsc, ref ivc, ref ivt,
//                         ref s, ref c, ref iz);

//                    //c
//                    //c ----integrazione della parte con tensione costante
//                    //c
//                    //      if (nsc.lt.1)goto 40
//                    if (!(nsc < 1))
//                    {
//                        //      j1 = 3 * (kvc(1) - 1) + 2
//                        //      x1 = aux(j1)
//                        //      y1 = aux(j1 + 1)
//                        //      nsc1 = nsc - 1

//                        int j1 = 3 * (kvc[1] - 1) + 2;
//                        x1 = aux[j1];
//                        y1 = aux[j1 + 1];
//                        int nsc1 = nsc - 1;

//                        //      do 41 j = 2,nsc1
//                        //        j2 = 3 * (kvc(j) - 1) + 2
//                        //      j3 = 3 * (kvc(j + 1) - 1) + 2
//                        //      x2 = aux(j2)
//                        //      y2 = aux(j2 + 1)
//                        //      x3 = aux(j3)
//                        //      y3 = aux(j3 + 1)
//                        //41    call cost(x1, y1, x2, y2, x3, y3, fck, gc, x, y, z, b, h, cs)
//                        for (int j = 2; j <= nsc1; j++)
//                        {
//                            int j2 = 3 * (kvc[j] - 1) + 2;
//                            int j3 = 3 * (kvc[j + 1] - 1) + 2;
//                            x2 = aux[j2];
//                            y2 = aux[j2 + 1];
//                            x3 = aux[j3];
//                            y3 = aux[j3 + 1];
//                            cost(ref x1, ref y1, ref x2, ref y2, ref x3, ref y3, ref fck, ref gc, ref x, ref y, ref z, ref b, ref h, ref cs, ref alfaCc);
//                        }
//                    }

//                    //c
//                    //c---- integrazione della parte con tensione parabolica
//                    //c
//                    //40    if (nsp.lt.1)goto 50
//                    if (!(nsp < 1))
//                    {
//                        //      j1 = 3 * (kvp(1) - 1) + 2
//                        //      x1 = aux(j1)
//                        //      y1 = aux(j1 + 1)
//                        //      nsp1 = nsp - 1
//                        int j1 = 3 * (kvp[1] - 1) + 2;
//                        x1 = aux[j1];
//                        y1 = aux[j1 + 1];
//                        int nsp1 = nsp - 1;

//                        //      do 42 j = 2,nsp1
//                        //        j2 = 3 * (kvp(j) - 1) + 2
//                        //      j3 = 3 * (kvp(j + 1) - 1) + 2
//                        //      x2 = aux(j2)
//                        //      y2 = aux(j2 + 1)
//                        //      x3 = aux(j3)
//                        //      y3 = aux(j3 + 1)
//                        //42    call gauss(x1, y1, x2, y2, x3, y3, xc, yc, eta, teta, s, c, chi,
//                        //     1v1, fck, gc, x, y, z, b, h, cs)
//                        for (int j = 2; j <= nsp1; j++)
//                        {
//                            int j2 = 3 * (kvp[j] - 1) + 2;
//                            int j3 = 3 * (kvp[j + 1] - 1) + 2;
//                            x2 = aux[j2];
//                            y2 = aux[j2 + 1];
//                            x3 = aux[j3];
//                            y3 = aux[j3 + 1];
//                            gauss(ref x1, ref y1, ref x2, ref y2, ref x3, ref y3, ref xc, ref yc, ref eta, ref teta, ref s,
//                                  ref c, ref chi, ref v1, ref fck, ref gc, ref x, ref y, ref z, ref b, ref h, ref cs, ref alfaCc);
//                        }
//                    }
//                }

//                //c
//                //c ---cambio di partizione convessa
//                //c
//                //50    inpc = inpc + 1
//                //      if (inpc.le.npc) goto 70
//                //100   return
//                //      end
//                inpc = inpc + 1;
//                if (!(inpc <= npc)) return;
//            } while (true == true);
//        }


//        //c
//        //c-------------------------------------------------------------------c
//        //c Subroutine per determinare i vertici delle zone compresse c
//        //c con tensione costante e parabolica                           c
//        //c-------------------------------------------------------------------c
//        //      subroutine zone(vc, nvc, ndvc, epsc, epst, w2, w0, kvc, kvp, ivp, ivc,
//        //     1ndkvc, ndkvp, ic, it, s, c, iz)
//        private void zone(ref double[] vc, ref int nvc, ref double epsc, ref double epst, ref double w2,
//                          ref double w0, ref int[] kvc, ref int[] kvp, ref int ivp, ref int ivc,
//                          ref int ic, ref int it, ref double s, ref double c, ref int iz)
//        {
//            double w1, x1, x2, y1, y2;
//            //c
//            //c---- descrizione delle variabili di output:
//            //c KVP = rappresenta un vettore di numeri interi che contiene
//            //c      l'ordine dei vertici della sezione che sono compressi
//            //c con tensione parabolica.
//            //c KVC = rappresenta un vettore di numeri interi che contiene
//            //c      l'ordine dei vertici della sezione compressi con tensione
//            //c costante.
//            //c IVP = numero dei vertici di KVP
//            //c IVC = numero dei vertici di KVC
//            //c
//            //      dimension vc(ndvc),kvc(ndkvc),kvp(ndkvp)
//            //c
//            //c ----scelta del tipo di suddivisione
//            //c
//            //      goto(200,20,30,50,40,60),iz
//            if (iz == 1)
//            {
//                return;//200
//            }
//            else if (iz == 2)
//            {
//                //c
//                //c ----sezione parzializzata con tensione solo parabolica
//                //c
//                //c
//                //c ----zona con tensione parabolica
//                //c
//                //20    kvp(1) = ic
//                //      n1 = ic - 1
//                //      ivc = 0
//                //      ivp = 1
//                //      iw = nvc

//                kvp[1] = ic;
//                int n1 = ic - 1;
//                int n2 = 0;
//                ivc = 0;
//                ivp = 1;
//                int iw = nvc;

//                bool canExit = true;
//                do//231
//                {
//                    //231   ivp = ivp + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                              kvp(ivp) = n2
//                    //                              goto 231
//                    //      endif
//                    canExit = true;
//                    ivp = ivp + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvp[ivp] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      kvp(ivp) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                kvp[ivp] = iw;
//                int jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//232
//                {
//                    canExit = true;
//                    //232   n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)goto 232
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1) canExit = false;
//                } while (!canExit);

//                //      iw = iw + 1
//                //      ivp = ivp + 1
//                //      kvp(ivp) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                ivp = ivp + 1;
//                kvp[ivp] = iw;
//                jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//233
//                {
//                    //233   if (n2.eq.ic) goto 200
//                    //      ivp = ivp + 1
//                    //      kvp(ivp) = n2
//                    //      n2 = n2 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      goto 233
//                    if (n2 == ic) return;
//                    ivp = ivp + 1;
//                    kvp[ivp] = n2;
//                    n2 = n2 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                } while (true == true);
//            }
//            else if (iz == 3)
//            {
//                //c ----sezione interamente compressa in modo parabolico
//                //c
//                //30    ivp = nvc
//                //      do 31 j = 1,nvc
//                //31    kvp(j) = j
//                //      goto 200
//                //c
//                ivp = nvc;
//                for (int j = 1; j <= nvc; j++)
//                {
//                    kvp[j] = j;
//                }
//                return;
//            }
//            else if (iz == 4)
//            {
//                //c ----sezione interamente compressa cost + parab
//                //c
//                //50    n1 = ic - 1
//                //      ivc = 1
//                //      ivp = 1
//                //      iw = nvc
//                int n1 = ic - 1;
//                int n2 = 0;
//                ivc = 1;
//                ivp = 1;
//                int iw = nvc;

//                //c
//                //c ----zona con tensione costante
//                //c
//                //      kvc(1) = ic
//                kvc[1] = ic;

//                bool canExit = false;
//                do//211
//                {
//                    canExit = true;
//                    //211   ivc = ivc + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                              kvc(ivc) = n2
//                    //                              goto 211
//                    //      endif
//                    ivc = ivc + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvc[ivc] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      kvc(ivc) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                kvc[ivc] = iw;
//                int jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                //c
//                //c ----zona con tensione parabolica
//                //c
//                //      kvp(1) = iw
//                //      ivp = ivp + 1
//                //      kvp(ivp) = n2
//                kvp[1] = iw;
//                ivp = ivp + 1;
//                kvp[ivp] = n2;

//                do//212
//                {
//                    canExit = true;
//                    //212   ivp = ivp + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                            kvp(ivp) = n2
//                    //                              goto 212
//                    //      endif
//                    ivp = ivp + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvp[ivp] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      ivc = ivc + 1
//                //      kvc(ivc) = iw
//                //      kvp(ivp) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                ivc = ivc + 1;
//                kvc[ivc] = iw;
//                kvp[ivp] = iw;
//                jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//213
//                {
//                    //213   if (n2.eq.ic) goto 200
//                    //      ivc = ivc + 1
//                    //      kvc(ivc) = n2
//                    //      n2 = n2 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      goto 213
//                    if (n2 == ic) return;
//                    ivc = ivc + 1;
//                    kvc[ivc] = n2;
//                    n2 = n2 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                } while (true == true);
//            }
//            else if (iz == 5)
//            {
//                //c
//                //c---- sezione interamente compressa in modo costante
//                //c
//                //40    ivc = nvc
//                //      do 41 j = 1,nvc
//                //41    kvc(j) = j
//                //      goto 200
//                //c

//                ivc = nvc;
//                for (int j = 1; j <= nvc; j++)
//                {
//                    kvc[j] = j;
//                }
//                return;
//            }
//            else if (iz == 6)
//            {
//                //c
//                //c ----sezione parzializzata con zona a tensione costante
//                //c
//                //60    n1 = ic - 1
//                //      ivc = 1
//                //      ivp = 1
//                //      iw = nvc
//                int n1 = ic - 1;
//                int n2 = 0;
//                ivc = 1;
//                ivp = 1;
//                int iw = nvc;

//                //c
//                //c ----zona con tensione costante
//                //c
//                //      kvc(1) = ic
//                kvc[1] = ic;

//                bool canExit = false;
//                do//221
//                {
//                    canExit = true;
//                    //221   ivc = ivc + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                              kvc(ivc) = n2
//                    //                              goto 221
//                    //      endif

//                    ivc = ivc + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvc[ivc] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      kvc(ivc) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                //      n1 = n1 - 1
//                iw = iw + 1;
//                kvc[ivc] = iw;
//                int jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);
//                n1 = n1 - 1;

//                //c
//                //c ----zona con tensione parabolica
//                //c
//                //      kvp(1) = iw
//                kvp[1] = iw;

//                do//222
//                {
//                    canExit = true;
//                    //222   ivp = ivp + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                              kvp(ivp) = n2
//                    //                              goto 222
//                    //      endif
//                    ivp = ivp + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvp[ivp] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      kvp(ivp) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                kvp[ivp] = iw;
//                jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//223
//                {
//                    canExit = true;
//                    //223   n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)goto 223


//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1) canExit = false;
//                } while (!canExit);

//                //      iw = iw + 1
//                //      ivp = ivp + 1
//                //      n1 = n1 - 1
//                //      kvp(ivp) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                ivp = ivp + 1;
//                n1 = n1 - 1;
//                kvp[ivp] = iw;
//                jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//322
//                {
//                    canExit = true;
//                    //322   ivp = ivp + 1
//                    //      n1 = n1 + 1
//                    //      if (n1.gt.nvc) n1 = n1 - nvc
//                    //      n2 = n1 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      j1 = 3 * (n1 - 1) + 2
//                    //      j2 = 3 * (n2 - 1) + 2
//                    //      x1 = vc(j1)
//                    //      y1 = vc(j1 + 1)
//                    //      x2 = vc(j2)
//                    //      y2 = vc(j2 + 1)
//                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
//                    //      if (w1.lt.0..or.w1.ge.1.)then
//                    //                              kvp(ivp) = n2
//                    //                              goto 322
//                    //      endif
//                    ivp = ivp + 1;
//                    n1 = n1 + 1;
//                    if (n1 > nvc) n1 = n1 - nvc;
//                    n2 = n1 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                    int j1 = 3 * (n1 - 1) + 2;
//                    int j2 = 3 * (n2 - 1) + 2;
//                    x1 = vc[j1];
//                    y1 = vc[j1 + 1];
//                    x2 = vc[j2];
//                    y2 = vc[j2 + 1];
//                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
//                    if (w1 < 0 || w1 >= 1)
//                    {
//                        kvp[ivp] = n2;
//                        canExit = false;
//                    }
//                } while (!canExit);

//                //      iw = iw + 1
//                //      kvp(ivp) = iw
//                //      ivc = ivc + 1
//                //      kvc(ivc) = iw
//                //      jw = 3 * (iw - 1) + 2
//                //      vc(jw) = x1 + w1 * (x2 - x1)
//                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
//                iw = iw + 1;
//                kvp[ivp] = iw;
//                ivc = ivc + 1;
//                kvc[ivc] = iw;
//                jw = 3 * (iw - 1) + 2;
//                vc[jw] = x1 + w1 * (x2 - x1);
//                vc[jw + 1] = y1 + w1 * (y2 - y1);

//                do//224
//                {
//                    //224   if (n2.eq.ic) goto 200
//                    //      ivc = ivc + 1
//                    //      kvc(ivc) = n2
//                    //      n2 = n2 + 1
//                    //      if (n2.gt.nvc) n2 = n2 - nvc
//                    //      goto 224
//                    if (n2 == ic) return;
//                    ivc = ivc + 1;
//                    kvc[ivc] = n2;
//                    n2 = n2 + 1;
//                    if (n2 > nvc) n2 = n2 - nvc;
//                } while (true == true);
//            }
//            else
//            {
//                throw new NotSupportedException("Zona non conosciuta");
//            }
//            //200  return
//            //      end
//        }

//        //c
//        //c ----function per determinare l'intersezione di un segmento
//        //c con una retta.
//        //c
//        //      function dlam(a, b, c, xi, yi, xj, yj)
//        private double dlam(double a, double b, double c, double xi, double yi, double xj, double yj)
//        {
//            double dlam, w1, w2, w3;
//            //      w1 = a * xi - b * yi + c
//            //      w3 = a * xj - b * yj + c
//            //      w2 = -a * (xj - xi) + b * (yj - yi)
//            w1 = a * xi - b * yi + c;
//            w3 = a * xj - b * yj + c;
//            w2 = -a * (xj - xi) + b * (yj - yi);

//            //      if (dabs(w1).lt.0.001)then
//            //                           dlam = 0.001
//            //                          return
//            //      endif
//            if (Math.Abs(w1) < 0.001)
//            {
//                dlam = 0.001;
//                return dlam;
//            }

//            //      if (dabs(w3).lt.0.001)then
//            //                           dlam = 1.001
//            //                          return
//            //      endif
//            if (Math.Abs(w3) < 0.001)
//            {
//                dlam = 1.001;
//                return dlam;
//            }

//            //      if (dabs(w2).lt.0.001)then
//            //                           dlam = 3.
//            //                          return
//            //      endif
//            if (Math.Abs(w2) < 0.001)
//            {
//                dlam = 3;
//                return dlam;
//            }

//            //      dlam = w1 / w2
//            //      return
//            //      end
//            dlam = w1 / w2;
//            return dlam;
//        }


//        //c---------------------------------------------------------------- - c
//        //c SUBROUTINE PER IL CALCOLO INTEGRALE DELLE TENSIONI         c
//        //c      VARIABILI PARABOLICAMENTE SU UN DOMINIO TRIANGOLARE (cls.) c
//        //c---------------------------------------------------------------- - c

//        // subroutine gauss(x1, y1, x2, y2, x3, y3, xc, yc, eta, teta, steta, cteta,
//        //1chi, v1, fck, gc, x, y, z, b, h, cs)
//        private void gauss(ref double x1, ref double y1, ref double x2, ref double y2, ref double x3, ref double y3,
//                           ref double xc, ref double yc, ref double eta, ref double teta, ref double steta, ref double cteta,
//                            ref double chi, ref double v1, ref double fck, ref double gc, ref double x, ref double y,
//                            ref double z, ref double b, ref double h, ref double cs, ref double alfaCc)
//        {
//            double wx, wy, wz, a, w1, w2, w3, xs, ys, eps, fs, w5, w4;

//            // wx = 0.
//            // wy = 0.
//            // wz = 0.
//            wx = 0;
//            wy = 0;
//            wz = 0;

//            //c
//            //c---- area del triangolo
//            //c

//            // a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1))

//            // a = a / b / h * cs
//            a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1));

//            a = a / b / h * cs;

//            //c---- primo punto semplice

//            // w1 = x2 - x1

//            // w2 = x3 - x1

//            // w3 = y2 - y1

//            // w4 = y3 - y1

//            // xs = x1 + w1 / 3.+ w2 / 3.
//            // ys = y1 + w3 / 3.+ w4 / 3.
//            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

//            // fs = dsgmac(fck, gc, eps)

//            // w5 = fs * (-0.56250)

//            // wx = wx - w5 * ys / h

//            // wy = wy + w5 * xs / b

//            // wz = wz + w5
//            w1 = x2 - x1;
//            w2 = x3 - x1;
//            w3 = y2 - y1;
//            w4 = y3 - y1;
//            xs = x1 + w1 / 3 + w2 / 3;
//            ys = y1 + w3 / 3 + w4 / 3;
//            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
//            fs = dsgmac(fck, gc, eps, alfaCc);
//            w5 = fs * (-0.56250);
//            wx = wx - w5 * ys / h;
//            wy = wy + w5 * xs / b;
//            wz = wz + w5;

//            //c---- secondo punto semplice

//            // xs = x1 + w1 * 0.2 + w2 * 0.2

//            // ys = y1 + w3 * 0.2 + w4 * 0.2

//            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

//            // fs = dsgmac(fck, gc, eps)

//            // w5 = fs * 0.52083333

//            // wx = wx - w5 * ys / h

//            // wy = wy + w5 * xs / b

//            // wz = wz + w5
//            xs = x1 + w1 * 0.2 + w2 * 0.2;
//            ys = y1 + w3 * 0.2 + w4 * 0.2;
//            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
//            fs = dsgmac(fck, gc, eps, alfaCc);
//            w5 = fs * 0.52083333;
//            wx = wx - w5 * ys / h;
//            wy = wy + w5 * xs / b;
//            wz = wz + w5;

//            //c---- terzo punto semplice

//            // xs = x1 + w1 * 0.6 + w2 * 0.2

//            // ys = y1 + w3 * 0.6 + w4 * 0.2

//            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

//            // fs = dsgmac(fck, gc, eps)

//            // w5 = fs * 0.52083333

//            // wx = wx - w5 * ys / h

//            // wy = wy + w5 * xs / b

//            // wz = wz + w5
//            xs = x1 + w1 * 0.6 + w2 * 0.2;
//            ys = y1 + w3 * 0.6 + w4 * 0.2;
//            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
//            fs = dsgmac(fck, gc, eps, alfaCc);
//            w5 = fs * 0.52083333;
//            wx = wx - w5 * ys / h;
//            wy = wy + w5 * xs / b;
//            wz = wz + w5;

//            //c---- quarto punto semplice

//            // xs = x1 + w1 * 0.2 + w2 * 0.6

//            // ys = y1 + w3 * 0.2 + w4 * 0.6

//            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

//            // fs = dsgmac(fck, gc, eps)

//            // w5 = fs * 0.52083333

//            // wx = wx - w5 * ys / h

//            // wy = wy + w5 * xs / b

//            // wz = wz + w5
//            xs = x1 + w1 * 0.2 + w2 * 0.6;
//            ys = y1 + w3 * 0.2 + w4 * 0.6;
//            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
//            fs = dsgmac(fck, gc, eps, alfaCc);
//            w5 = fs * 0.52083333;
//            wx = wx - w5 * ys / h;
//            wy = wy + w5 * xs / b;
//            wz = wz + w5;

//            //c---- complessivamente:
//            // x = wx * a + x

//            // y = wy * a + y

//            // z = wz * a + z
//            //      return
//            //      end
//            x = wx * a + x;
//            y = wy * a + y;
//            z = wz * a + z;
//        }




//        //c---------------------------------------------------------------- - c
//        //c SUBROUTINE PER IL CALCOLO INTEGRALE DELLE TENSIONI         c
//        //c      COSTANTI SU UN DOMINIO TRIANGOLARE(calcestruzzo)        c
//        //c-----------------------------------------------------------------c
//        //      subroutine cost(x1, y1, x2, y2, x3, y3, fck, gc, x, y, z, b, h, cs)
//        private void cost(ref double x1, ref double y1, ref double x2, ref double y2, ref double x3, ref double y3,
//                          ref double fck, ref double gc, ref double x, ref double y, ref double z, ref double b, ref double h,
//                          ref double cs, ref double alfaCc)
//        {
//            double a, xg, yg, w1;
//            //c
//            //c---- area del triangolo
//            //c
//            //      a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1))
//            //      a = a / b / h * cs
//            a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1));
//            a = a / b / h * cs;

//            //c
//            //c---- baricentro del triangolo
//            //c
//            //      xg = (x1 + x2 + x3) / 3.
//            //      yg = (y1 + y2 + y3) / 3.
//            xg = (x1 + x2 + x3) / 3.0;
//            yg = (y1 + y2 + y3) / 3.0;

//            //c
//            //c---- tensione di compressione
//            //c
//            //      w1 = 0.85 / gc
//            w1 = alfaCc / gc;

//            //c---- complessivamente:
//            //      z = w1 * a + z
//            //      y = w1 * a * xg / b + y
//            //      x = -w1 * a * yg / h + x
//            //      return
//            //      end
//            z = w1 * a + z;
//            y = w1 * a * xg / b + y;
//            x = -w1 * a * yg / h + x;
//        }



//        //c
//        //c------------------------------------------------------------------c
//        //c FUNCTION  DI UTILITA'                                  c
//        //c------------------------------------------------------------------c
//        //c
//        //c
//        //c ----function per il calcolo delle deformazioni
//        //c
//        //      function depsil(xc, yc, steta, cteta, chi, v1, xi, yi)
//        private double depsil(double xc, double yc, double steta, double cteta, double chi, double v1, double xi, double yi)
//        {
//            double di, depsil;
//            //      di = (yi - yc) * cteta - (xi - xc) * steta
//            //      depsil = v1 - chi * di
//            //      return
//            //      end
//            di = (yi - yc) * cteta - (xi - xc) * steta;
//            depsil = v1 - chi * di;
//            return depsil;
//            //      end
//        }

//        //c
//        //c ----function per il calcolo delle tensioni nell'acciaio
//        //c
//        //      function dsgmaf(fys, gs, eps, e, fck, gc)
//        private double dsgmaf(double fys, double gs, double eps, double e, double fck, double gc, double sigma0)
//        {
//            double b, a, dsgmaf;
//            //      b = fys / gs / fck
//            //      a = e * eps / 1000./ fck
//            //      dsgmaf = a
//            //      if (dabs(a).gt.b) dsgmaf = b * dabs(a) / a
//            //      return
//            //      end
//            b = fys / gs / fck;
//            a = e * eps / 1000.0 / fck + sigma0 / fck;
//            dsgmaf = a;
//            if (Math.Abs(a) > b) dsgmaf = b * Math.Abs(a) / a;
//            return dsgmaf;
//        }


//        //c
//        //c ----function per il calcolo delle tensioni nel calcestruzzo
//        //c
//        //      function dsgmac(fck, gc, eps)
//        private double dsgmac(double fck, double gc, double eps, double alfaCc)
//        {
//            double dsgmac;
//            //      dsgmac = 0.85 / gc
//            //      if (eps + 2.) 100,100,50
//            //50    dsgmac = -0.85 / gc * (eps + eps * *2 / 4.)
//            //      if (eps.ge.0)dsgmac = 0.
//            //100   return
//            //      end
//            dsgmac = alfaCc / gc;
//            if (eps + 2 <= 0)
//            {
//                return dsgmac;
//            }
//            else
//            {
//                dsgmac = -alfaCc / gc * (eps + Math.Pow(eps, 2) / 4.0);
//                if (eps >= 0) dsgmac = 0;
//                return dsgmac;
//            }
//        }

//        //c
//        //c
//        //c
//        //c  routine per l'integrazione lineare di una funzione cubica
//        //c con il metodo dei punti di Gauss. (acciaio)
//        //c
//        //      subroutine ilgs(xa, ya, ea, xb, yb, eb, nel, aux, ndaux, x, y, z,
//        //     1xc, yc, steta, cteta, chi, v1, fys, gs, e, fck, gc, steel, b, h)
//        //      double precision xa,ya,ea,xb,yb,eb,aux,x,y,z,xc,yc,steta,
//        //     1cteta,chi,v1,fys,gs,e,fck,gc,steel,b,h,w11,w22,w0,x1,x2,y1,y2,
//        //     1e1,sx,sy,wx,wy,wz,w1
//        //      double precision dsgmaf,depsil
//        //      dimension aux(ndaux)
//        //c
//        //c ----descrizione delle variabili:
//        //c
//        //c    XA = ascissa del primo punto iniziale (con epsilon minore)
//        //c YA = ordinata    "            "            "
//        //c EA = epsilon al punto A
//        //c    XB = ascissa del secondo punto(con epsilon maggiore)
//        //c YB = ordinata    "            "            "
//        //c EB = epsilon al punto B
//        //c    NEL = numero delle parti separate del diagramma costitutivo
//        //c STEEL = superficie lineare totale
//        //c AUX = vettore ausiliario di dimensione ndaux contenente i valori
//        //c        di epsilon dividenti il diagramma costitutivo.
//        //c X = caratteristica di sollecitazione flettente secondo X
//        //c    Y = "                  "               "         "    Y
//        //c    Z = "                  "            normale
//        //c    N.B.: le altre variabili sono analoghe alla LIMQ
//        //c
//        private void ilgs(ref double xa, ref double ya, ref double ea, ref double xb, ref double yb, ref double eb,
//                          ref int nel, ref double[] aux, ref double x, ref double y, ref double z, ref double xc,
//                          ref double yc, ref double steta, ref double cteta, ref double chi, ref double v1, ref double fys,
//                          ref double gs, ref double e, ref double fck, ref double gc, ref double steel, ref double b, ref double h)
//        {
//            double w11, w22, w0, x1, x2, y1, y2, e1, sx, sy, wx, wy, wz, w1;

//            //      w11 = xa
//            //      w22 = ya
//            //      i = 1
//            w11 = xa;
//            w22 = ya;
//            int i = 1;
//            do
//            {
//                //20    e1 = aux(i)
//                e1 = aux[i];
//                //      if (e1.lt.ea) goto 30
//                if (!(e1 < ea))
//                {
//                    //      if (e1.ge.eb) then
//                    //                    x1 = xa
//                    //                  y1 = ya
//                    //                  x2 = xb
//                    //                  y2 = yb
//                    //                  goto 10
//                    //      endif
//                    //      x1 = xa
//                    //      y1 = ya
//                    //      x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea)
//                    //      y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea)
//                    if (e1 >= eb)
//                    {
//                        x1 = xa;
//                        y1 = ya;
//                        x2 = xb;
//                        y2 = yb;
//                    }
//                    else
//                    {
//                        x1 = xa;
//                        y1 = ya;
//                        x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea);
//                        y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea);
//                    }

//                    //10    w0 = dsqrt((x2 - x1) * *2 + (y1 - y2) * *2) / dsqrt((w11 - xb) * *2 + (w22 - yb) * *2)
//                    //      w0 = w0 * steel / 2.
//                    w0 = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y1 - y2, 2)) /
//                         Math.Sqrt(Math.Pow(w11 - xb, 2) + Math.Pow(w22 - yb, 2));
//                    w0 = w0 * steel / 2.0;

//                    //c
//                    //c---- primo punto semplice
//                    //c
//                    //      sx = x1 + (1.- 0.77459) / 2.* (x2 - x1)
//                    //      sy = y1 + (1.- 0.77459) / 2.* (y2 - y1)
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.555555
//                    //      wx = w1 * sy / b / h / h
//                    //      wy = -w1 * sx / b / h / b
//                    //      wz = -w1 / b / h
//                    sx = x1 + (1 - 0.77459) / 2.0 * (x2 - x1);
//                    sy = y1 + (1 - 0.77459) / 2.0 * (y2 - y1);
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.555555;
//                    wx = w1 * sy / b / h / h;
//                    wy = -w1 * sx / b / h / b;
//                    wz = -w1 / b / h;

//                    //c
//                    //c ----secondo punto semplice
//                    //c
//                    //      sx = (x1 + x2) / 2.
//                    //      sy = (y1 + y2) / 2.
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.88888
//                    //      wx = w1 * sy / b / h / h + wx
//                    //      wy = -w1 * sx / b / h / b + wy
//                    //      wz = -w1 / b / h + wz
//                    sx = (x1 + x2) / 2.0;
//                    sy = (y1 + y2) / 2.0;
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.88888;
//                    wx = w1 * sy / b / h / h + wx;
//                    wy = -w1 * sx / b / h / b + wy;
//                    wz = -w1 / b / h + wz;

//                    //c
//                    //c ----terzo punto semplice
//                    //c
//                    //      sx = x1 + 1.77459 / 2.* (x2 - x1)
//                    //      sy = y1 + 1.77459 / 2.* (y2 - y1)
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.555555
//                    //      x = w1 * sy / b / h / h + wx + x
//                    //      y = -w1 * sx / b / h / b + wy + y
//                    //      z = -w1 / b / h + wz + z
//                    sx = x1 + 1.77459 / 2 * (x2 - x1);
//                    sy = y1 + 1.77459 / 2 * (y2 - y1);
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.555555;
//                    x = w1 * sy / b / h / h + wx + x;
//                    y = -w1 * sx / b / h / b + wy + y;
//                    z = -w1 / b / h + wz + z;

//                    //c
//                    //c ----fine dell'integrazione
//                    //c
//                    //      if (((xb - x2) * *2 + (yb - y2) * *2).lt.0.001)goto 100
//                    //      xa = x2
//                    //      ya = y2
//                    if (Math.Pow(xb - x2, 2) + Math.Pow(yb - y2, 2) < 0.001) break;
//                    xa = x2;
//                    ya = y2;
//                }

//                //30    i = i + 1
//                //      if (i.gt.nel) goto 100
//                i++;
//                if (i > nel) break;

//                //      goto 20
//            } while (true == true);

//            //100   xa = w11
//            //      ya = w22
//            //      return
//            //      end
//            xa = w11;
//            ya = w22;
//        }


//        //c
//        //c
//        //c routine per l'integrazione lineare di una funzione cubica
//        //c con il metodo dei punti di Gauss. (calcestruzzo)
//        //c
//        //      subroutine ilgc(xa, ya, ea, xb, yb, eb, nel, aux, ndaux, x, y, z,
//        //     1xc, yc, steta, cteta, chi, v1, fck, gc, cls, b, h)
//        //c
//        //c ----descrizione delle variabili:
//        //c
//        //c    XA = ascissa del primo punto iniziale (con epsilon minore)
//        //c YA = ordinata    "            "            "
//        //c EA = epsilon al punto A
//        //c    XB = ascissa del secondo punto(con epsilon maggiore)
//        //c YB = ordinata    "            "            "
//        //c EB = epsilon al punto B
//        //c    NEL = numero delle parti separate del diagramma costitutivo
//        //c CLS = superficie da togliere
//        //c AUX = vettore ausiliario di dimensione ndaux contenente i valori
//        //c        di epsilon dividenti il diagramma costitutivo.
//        //c X = caratteristica di sollecitazione flettente secondo X
//        //c    Y = "                  "               "         "    Y
//        //c    Z = "                  "            normale
//        //c    N.B.: le altre variabili sono analoghe alla LIMQ
//        //c
//        private void ilgc(ref double xa,
//                          ref double ya,
//                          ref double ea,
//                          ref double xb,
//                          ref double yb,
//                          ref double eb,
//                          ref int nel,
//                          ref double[] aux,
//                          ref double x,
//                          ref double y,
//                          ref double z,
//                          ref double xc,
//                          ref double yc,
//                          ref double steta,
//                          ref double cteta,
//                          ref double chi,
//                          ref double v1,
//                          ref double fck,
//                          ref double gc,
//                          ref double cls,
//                          ref double b,
//                          ref double h,
//                          ref double alfaCc)
//        {
//            double w11, w22, w0, x1, x2, y1, y2, e1, sx, sy, wx, wy, wz, w1;
//            w11 = xa;
//            w22 = ya;
//            int i = 1;
//            do
//            {
//                //20    e1 = aux(i)
//                e1 = aux[i];

//                //      if (e1.le.ea) goto 30
//                if (!(e1 <= ea))
//                {
//                    //      if (e1.ge.eb) then
//                    //                    x1 = xa
//                    //                  y1 = ya
//                    //                  x2 = xb
//                    //                  y2 = yb
//                    //                  goto 10
//                    //      endif
//                    //      x1 = xa
//                    //      y1 = ya
//                    //      x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea)
//                    //      y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea)
//                    if (e1 >= eb)
//                    {
//                        x1 = xa;
//                        y1 = ya;
//                        x2 = xb;
//                        y2 = yb;
//                    }
//                    else
//                    {
//                        x1 = xa;
//                        y1 = ya;
//                        x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea);
//                        y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea);
//                    }

//                    //10    w0 = dsqrt((x2 - x1) * *2 + (y1 - y2) * *2) / dsqrt((w11 - xb) * *2 + (w22 - yb) * *2)
//                    //      w0 = w0 * cls / 2.
//                    w0 = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y1 - y2, 2)) /
//                         Math.Sqrt(Math.Pow(w11 - xb, 2) + Math.Pow(w22 - yb, 2));
//                    w0 = w0 * cls / 2;

//                    //c
//                    //c---- primo punto semplice
//                    //c
//                    //      sx = x1 + (1.- 0.77459) / 2.* (x2 - x1)
//                    //      sy = y1 + (1.- 0.77459) / 2.* (y2 - y1)
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.555555
//                    //      wx = w1 * sy / b / h / h
//                    //      wy = -w1 * sx / b / h / b
//                    //      wz = -w1 / b / h
//                    sx = x1 + (1 - 0.77459) / 2.0 * (x2 - x1);
//                    sy = y1 + (1 - 0.77459) / 2.0 * (y2 - y1);
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.555555;
//                    wx = w1 * sy / b / h / h;
//                    wy = -w1 * sx / b / h / b;
//                    wz = -w1 / b / h;

//                    //c
//                    //c ----secondo punto semplice
//                    //c
//                    //      sx = (x1 + x2) / 2.
//                    //      sy = (y1 + y2) / 2.
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.88888
//                    //      wx = w1 * sy / b / h / h + wx
//                    //      wy = -w1 * sx / b / h / b + wy
//                    //      wz = -w1 / b / h + wz
//                    sx = (x1 + x2) / 2.0;
//                    sy = (y1 + y2) / 2.0;
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.88888;
//                    wx = w1 * sy / b / h / h + wx;
//                    wy = -w1 * sx / b / h / b + wy;
//                    wz = -w1 / b / h + wz;

//                    //c
//                    //c ----terzo punto semplice
//                    //c
//                    //      sx = x1 + 1.77459 / 2.* (x2 - x1)
//                    //      sy = y1 + 1.77459 / 2.* (y2 - y1)
//                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
//                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.555555
//                    //      x = w1 * sy / b / h / h + wx + x
//                    //      y = -w1 * sx / b / h / b + wy + y
//                    //      z = -w1 / b / h + wz + z
//                    sx = x1 + 1.77459 / 2.0 * (x2 - x1);
//                    sy = y1 + 1.77459 / 2.0 * (y2 - y1);
//                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
//                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.555555;
//                    x = w1 * sy / b / h / h + wx + x;
//                    y = -w1 * sx / b / h / b + wy + y;
//                    z = -w1 / b / h + wz + z;

//                    //c
//                    //c ----fine dell'integrazione
//                    //c
//                    //      if (((xb - x2) * *2 + (yb - y2) * *2).lt.0.001)goto 100
//                    //      xa = x2
//                    //      ya = y2
//                    if (Math.Pow(xb - x2, 2) + Math.Pow(yb - y2, 2) < 0.001) return;
//                    xa = x2;
//                    ya = y2;
//                }
//                //30    i = i + 1
//                //      if (i.gt.nel) goto 100
//                i++;
//                if (i > nel) return;
//                //      goto 20
//            } while (true == true);
//            //100   return
//            //      end
//        }

//        #endregion

//        #region Limit point

//        //c
//        //c
//        //c PROGRAMMA  PER LA RICERCA DELL’INTERSEZIONE DI UN VETTORE
//        //c CON UNA SUPERFICIE LIMITE GENERICA
//        //c
//        //c
//        //      dimension vst(12),ise(12)
//        //      data ise, vst/ 1,2,1,3,2,4,3,5,4,6,5,6,3.1415,10 * 0.0,3.1415 /
//        //    c Descrizione delle variabili
//        //    c vst – vettore con valori per il cambio di campo di rottura, 12 elementi così composti:
//        //            c    3.1415  0.0  0.0  0.0  0.0  0.0  0.0  0.0  0.0  0.0 0.0 3.1415
//        //c ise – vettore con valori per il cambio di campo di rottura, 12 elementi così composti:
//        //            c   1  2  1  3  2  4  3  5  4  6  5  6
//        //c
//        //c    b, h parametri di adimensionalizzazione dei valori limite
//        //c   eta0 – parametro iniziale eta
//        //c   teta0 – parametro iniziale teta
//        //c   ic0 – parametro iniziale per campo di rottura
//        //c eta – parametro eta
//        //c teta – parametro teta
//        //c ic – parametro per campo di rottura
//        //c deta – incremento per derivazione numerica(0.05)
//        //c dteta – incremento per derivazione numerica(0.05)
//        //c err  -errore di linearità da considerare nell’incremento delle variabili(0.1)
//        //c dumax -massimo valore ammesso per il primo parametro
//        //c dvmax – massimo valore ammesso per il secondo parametro
//        //c   nite – numero iterazioni
//        //c vl, vm, vn   -versore del vettore di verifica uscente dall’origine e passante per PEd
//        //c   xd, yd, zd – Punto di verifica PEd
//        //c

//        private void GetLimitPointViviani(double eta0, double teta0, int ic0,
//                                          double[] vc, double[] vs, double[] vsl, int nvc, int nvs, int nvsl,
//                                          double fysl, double fck, double gs, double gsl, double gc,
//                                          double esl, double b, double h, double[] vse, int[] ipc,
//                                          int npc, double xd, double yd, double zd, double dumax, double dvmax,
//                                          double epsCMin, double fiTensAASHTO, double fiCompAASHTO, bool useLimit34AASHTO, double alfaCC,   //added for AASHTO
//                                          double epsSu,
//                                          out double xi, out double yi, out double zi, out double teta, out double eta, out int ic)
//        {
//            const int MAX_ITERS = 50;
//            int[] ise = { 0, 1, 2, 1, 3, 2, 4, 3, 5, 4, 6, 5, 6 };//added initial 0
//            double[] vst = { 0, 3.1415, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 3.1415 };//added initial 0
//            int nite;
//            double eta1, deta, dteta, teta1;
//            //Point3d dirV;
//            Vector3d dirV;
//            double vl, vm, vn, err;

//            //c
//            //c METODO DI RISOLUZIONE
//            //c
//            //c
//            //c----calcolo delle caratteristiche ultime del punto Pi
//            //c
//            nite = 0;
//            eta = eta0;
//            teta = teta0;
//            ic = ic0;
//            deta = 0.05;
//            dteta = 0.05;
//            err = 0.1;
//            //dirV = GPC.Geometry.Geom.Direction(new Point3d(xd, yd, zd));
//            dirV = new Vector3d(xd, yd, zd);
//            //dirV.Unitize();
//            vl = dirV.X;
//            vm = dirV.Y;
//            vn = dirV.Z;

//            do//55
//            {
//                //55    call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
//                //     1gs,gsl,gc,eta,teta,e,xi,yi,zi,b,h,kvc,kvp,ndkvc,ndkvp,
//                //     1ipc,ndpc,npc,aux,ndaux)

//                //nite = nite + 1
//                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta, esl, out xi, out yi, out zi,
//                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);
//                nite = nite + 1;
//                if (nite > MAX_ITERS) throw new NotSupportedException("GetLimitPointViviani not converged");

//                //c
//                //c ----calcolo delle derivate parziali
//                //c
//                // eta1 = eta - deta
//                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
//                //1gs,gsl,gc,eta1,teta,e,xiie,yiie,ziie,b,h,kvc,kvp,ndkvc,ndkvp,
//                //1ipc,ndpc,npc,aux,ndaux)
//                // eta1 = eta + deta
//                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
//                //1gs,gsl,gc,eta1,teta,e,xise,yise,zise,b,h,kvc,kvp,ndkvc,ndkvp,
//                //1ipc,ndpc,npc,aux,ndaux)
//                // dxe = (xise - xiie) / deta / 2.
//                // dye = (yise - yiie) / deta / 2.
//                // dze = (zise - ziie) / deta / 2.
//                // teta1 = teta - dteta
//                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
//                //1gs,gsl,gc,eta,teta1,e,xiit,yiit,ziit,b,h,kvc,kvp,ndkvc,ndkvp,
//                //1ipc,ndpc,npc,aux,ndaux)
//                // teta1 = teta + dteta
//                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
//                //1gs,gsl,gc,eta,teta1,e,xist,yist,zist,b,h,kvc,kvp,ndkvc,ndkvp,
//                //1ipc,ndpc,npc,aux,ndaux)
//                // dxt = (xist - xiit) / dteta / 2.
//                // dyt = (yist - yiit) / dteta / 2.
//                // dzt = (zist - ziit) / dteta / 2
//                double xiie, yiie, ziie, xise, yise, zise, dxe, dye, dze, xiit, yiit, ziit, xist, yist, zist, dxt, dyt, dzt;

//                eta1 = eta - deta;
//                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta1, teta, esl, out xiie, out yiie, out ziie,
//                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

//                eta1 = eta + deta;
//                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta1, teta, esl, out xise, out yise, out zise,
//                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

//                dxe = (xise - xiie) / deta / 2.0;
//                dye = (yise - yiie) / deta / 2.0;
//                dze = (zise - ziie) / deta / 2.0;

//                teta1 = teta - dteta;
//                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta1, esl, out xiit, out yiit, out ziit,
//                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

//                teta1 = teta + dteta;
//                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta1, esl, out xist, out yist, out zist,
//                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

//                dxt = (xist - xiit) / dteta / 2.0;
//                dyt = (yist - yiit) / dteta / 2.0;
//                dzt = (zist - ziit) / dteta / 2.0;

//                //c
//                //c ----calcolo del vettore parallelo al gradiente
//                //c      della funzione di vincolo
//                //c
//                //      vj1 = (dye * dzt - dze * dyt)
//                //      vj2 = (dxt * dze - dxe * dzt)
//                //      vj3 = (dxe * dyt - dye * dxt)
//                //      w1 = xi - xd
//                //      w2 = yi - yd
//                //      w3 = zi - zd
//                //      waux = vj1 * vl + vj2 * vm + vj3 * vn
//                //      if (dabs(waux).lt.0.00001)waux = 0.0001
//                //      du = vn * (w2 * dxt - w1 * dyt) + vm * (w1 * dzt - w3 * dxt) + vl * (w3 * dyt - w2 * dzt)
//                //      du = du / waux
//                //      dv = vn * (w1 * dye - w2 * dxe) + vm * (w3 * dxe - w1 * dze) + vl * (w2 * dze - w3 * dye)
//                //      dv = dv / waux
//                //      waux = dnorma(vj1, vj2, vj3)
//                //      u2 = vj2 / waux
//                //      u3 = vj3 / waux
//                //      u1 = vj1 / waux

//                double vj1, vj2, vj3, w1, w2, w3, waux, du, dv, u1, u2, u3;

//                vj1 = dye * dzt - dze * dyt;
//                vj2 = dxt * dze - dxe * dzt;
//                vj3 = dxe * dyt - dye * dxt;
//                w1 = xi - xd;
//                w2 = yi - yd;
//                w3 = zi - zd;
//                waux = vj1 * vl + vj2 * vm + vj3 * vn;
//                if (Math.Abs(waux) < 0.00001) waux = 0.0001;
//                du = vn * (w2 * dxt - w1 * dyt) + vm * (w1 * dzt - w3 * dxt) + vl * (w3 * dyt - w2 * dzt);
//                du = du / waux;
//                dv = vn * (w1 * dye - w2 * dxe) + vm * (w3 * dxe - w1 * dze) + vl * (w2 * dze - w3 * dye);
//                dv = dv / waux;
//                waux = dnorma(vj1, vj2, vj3);
//                u2 = vj2 / waux;
//                u3 = vj3 / waux;
//                u1 = vj1 / waux;

//                //c
//                //c ----calcolo della distanza
//                //c
//                //      waux = dnorma(w1, w2, w3) * *2 - dscal(w1, w2, w3, vl, vm, vn) * *2
//                //      vns = dsqrt(dabs(waux))
//                //      s1 = xi + dxe * du + dxt * dv
//                //      s2 = yi + dye * du + dyt * dv
//                //      s3 = zi + dze * du + dzt * dv
//                double vns, s1, s2, s3;

//                waux = Math.Pow(dnorma(w1, w2, w3), 2.0) - Math.Pow(dscal(w1, w2, w3, vl, vm, vn), 2.0);
//                vns = Math.Sqrt(Math.Abs(waux));
//                s1 = xi + dxe * du + dxt * dv;
//                s2 = yi + dye * du + dyt * dv;
//                s3 = zi + dze * du + dzt * dv;

//                //c
//                //c ----controllo dell'orientamento del vettore DU-DV
//                //c
//                //      s1 = s1 - xd
//                //      s2 = s2 - yd
//                //      s3 = s3 - zd
//                //      vss = dscal(s1, s2, s3, vl, vm, vn)
//                //      if (dabs(vss).lt.0.00001)goto 208
//                //      vss = vss / dabs(vss)
//                //      du = du * vss
//                //      dv = dv * vss
//                double vss;

//                s1 = s1 - xd;
//                s2 = s2 - yd;
//                s3 = s3 - zd;
//                vss = dscal(s1, s2, s3, vl, vm, vn);
//                if (Math.Abs(vss) >= 0.00001)//goto208
//                {
//                    vss = vss / Math.Abs(vss);
//                    du = du * vss;
//                    dv = dv * vss;
//                }

//                //c
//                //c ----controllo di convergenza
//                //c
//                //208   if ((dabs(du) + dabs(dv)).lt.1.d - 6)goto 67
//                if ((Math.Abs(du) + Math.Abs(dv)) < 1e-6) return;

//                //c
//                //c ----calcolo del massimo errore di linearita`
//                //c
//                //      eu = dabs((xiie + xise) / 2.- xi)
//                //      if (dabs(eu).lt.0.0001)eu = 0.0001
//                //      eu = dsqrt(err * dabs(xi) / eu)
//                //      ev = dabs((xiit + xist) / 2.- xi)
//                //      if (dabs(ev).lt.0.0001)ev = 0.0001
//                //      ev = dsqrt(err * dabs(xi) / ev)
//                //      eu1 = dabs((yiie + yise) / 2.- yi)
//                //      if (dabs(eu1).lt.0.0001)eu1 = 0.0001
//                //      eu1 = dsqrt(err * dabs(yi) / eu1)
//                //      ev1 = dabs((yiit + yist) / 2.- yi)
//                //      if (dabs(ev1).lt.0.0001)ev1 = 0.0001
//                //      ev1 = dsqrt(err * dabs(yi) / ev1)
//                //      if (eu1.gt.eu) eu = eu1
//                //      if (ev1.gt.ev) ev = ev1
//                //      eu1 = dabs((ziie + zise) / 2.- zi)
//                //      if (dabs(eu1).lt.0.0001)eu1 = 0.0001
//                //      eu1 = dsqrt(err * dabs(zi) / eu1)
//                //      ev1 = dabs((ziit + zist) / 2.- zi)
//                //      if (dabs(ev1).lt.0.0001)ev1 = 0.0001
//                //      ev1 = dsqrt(err * dabs(zi) / ev1)
//                //      if (eu1.gt.eu) eu = eu1
//                //      if (ev1.gt.ev) ev = ev1
//                //      au = 0.00001
//                //      if (dabs(du).gt.0.)au = dabs(du) / deta
//                //      av = 0.00001
//                //      if (dabs(dv).gt.0.)av = dabs(dv) / dteta
//                //      cl = 1.
//                //      if (cl.gt.eu / au) cl = eu / au
//                //      if (cl.gt.ev / av) cl = ev / av
//                //      du = cl * du
//                //      dv = cl * dv
//                double eu, ev, eu1, ev1, au, av, cl;

//                eu = Math.Abs((xiie + xise) / 2.0 - xi);
//                if (Math.Abs(eu) < 0.0001) eu = 0.0001;
//                eu = Math.Sqrt(err * Math.Abs(xi) / eu);
//                ev = Math.Abs((xiit + xist) / 2.0 - xi);
//                if (Math.Abs(ev) < 0.0001) ev = 0.0001;
//                ev = Math.Sqrt(err * Math.Abs(xi) / ev);
//                eu1 = Math.Abs((yiie + yise) / 2.0 - yi);
//                if (Math.Abs(eu1) < 0.0001) eu1 = 0.0001;
//                eu1 = Math.Sqrt(err * Math.Abs(yi) / eu1);
//                ev1 = Math.Abs((yiit + yist) / 2.0 - yi);
//                if (Math.Abs(ev1) < 0.0001) ev1 = 0.0001;
//                ev1 = Math.Sqrt(err * Math.Abs(yi) / ev1);
//                if (eu1 > eu) eu = eu1;
//                if (ev1 > ev) ev = ev1;
//                eu1 = Math.Abs((ziie + zise) / 2.0 - zi);
//                if (Math.Abs(eu1) < 0.0001) eu1 = 0.0001;
//                eu1 = Math.Sqrt(err * Math.Abs(zi) / eu1);
//                ev1 = Math.Abs((ziit + zist) / 2.0 - zi);
//                if (Math.Abs(ev1) < 0.0001) ev1 = 0.0001;
//                ev1 = Math.Sqrt(err * Math.Abs(zi) / ev1);
//                if (eu1 > eu) eu = eu1;
//                if (ev1 > ev) ev = ev1;
//                au = 0.00001;
//                if (Math.Abs(du) > 0.0) au = Math.Abs(du) / deta;
//                av = 0.00001;
//                if (Math.Abs(dv) > 0.0) av = Math.Abs(dv) / dteta;
//                cl = 1.0;
//                if (cl > eu / au) cl = eu / au;
//                if (cl > ev / av) cl = ev / av;
//                du = cl * du;
//                dv = cl * dv;

//                //c
//                //c ----controllo della lunghezza del passo
//                //c
//                //c ----controllo della variabile teta
//                //      if (dabs(dv).gt.dvmax) then
//                //                            du = dvmax / dabs(dv) * du
//                //                          dv = dvmax * dabs(dv) / dv
//                //      endif
//                if (Math.Abs(dv) > dvmax)
//                {
//                    du = dvmax / Math.Abs(dv) * du;
//                    dv = dvmax * Math.Abs(dv) / dv;
//                }

//                //c ----controllo della variabile eta
//                //      if (dabs(du).gt.dumax) then
//                //                            du = dumax * dabs(du) / du
//                //                          dv = dumax / dabs(du) * dv
//                //      endif
//                if (Math.Abs(du) > dumax)
//                {
//                    du = dumax * Math.Abs(du) / du;
//                    dv = dumax / Math.Abs(du) * dv;
//                }

//                //c ----controllo della variabile eta(passaggio ad un altro campo)
//                //      cc = 0.
//                //      eta1 = eta + du
//                //      if (eta1.gt.1.)then
//                //                     waux = 1.- eta
//                //                    dv = waux / du * dv
//                //                    du = waux
//                //                    cc = 1.3
//                //      endif
//                //      if (eta1.lt.0.)then
//                //                     dv = -eta / du * dv
//                //                    du = -eta
//                //                    cc = -0.3
//                //      endif
//                double cc;

//                cc = 0.0;
//                eta1 = eta + du;
//                if (eta1 > 1)
//                {
//                    waux = 1 - eta;
//                    dv = waux / du * dv;
//                    du = waux;
//                    cc = 1.3;
//                }
//                if (eta1 < 0.0)
//                {
//                    dv = -eta / du * dv;
//                    du = -eta;
//                    cc = -0.3;
//                }

//                //c
//                //c---- calcolo dei nuovi Du Dv per il passo successivo
//                //c
//                //      eta = eta + du
//                //      teta = teta + dv
//                eta = eta + du;
//                teta = teta + dv;

//                //c
//                //c ----cambiamento, se necessario, del campo di rottura
//                //c
//                //      if (cc.lt.- 0.1) then
//                //                      eta = 1.
//                //                      teta = teta + vst(2 * ic - 1)
//                //                   ic = ise(2 * ic - 1)
//                //      endif
//                //      if (cc.gt.1.1)then
//                //                    eta = 0.
//                //                    teta = teta + vst(2 * ic)
//                //                   ic = ise(2 * ic)
//                //      endif
//                if (cc < -0.1)
//                {
//                    eta = 1.0;
//                    teta = teta + vst[2 * ic - 1];
//                    ic = ise[2 * ic - 1];
//                }
//                if (cc > 1.1)
//                {
//                    eta = 0.0;
//                    teta = teta + vst[2 * ic];
//                    ic = ise[2 * ic];
//                }
//                //      goto 55
//            } while (true == true);

//            //c
//            //c ----uscita delle caratteristiche ultime
//            //c    zi,xi,yi,ic,eta,teta
//            //c
//            //67  end
//        }

//        //c
//        //c function per determinare la norma di un vettore
//        //c
//        //      function dnorma(x1, y1, z1)
//        //      double precision x1,y1,z1,dnorma
//        //      dnorma = dsqrt(x1 * x1 + y1 * y1 + z1 * z1)
//        //      return
//        //      end
//        private double dnorma(double x1, double y1, double z1)
//        {
//            return Math.Sqrt(x1 * x1 + y1 * y1 + z1 * z1);
//        }

//        //c
//        //c    function per determinare il prodotto scalare tra due vettori
//        //c
//        //      function dscal(x1, y1, z1, x2, y2, z2)
//        //      double precision x1,y1,x2,y2,z1,z2,vscal
//        //      dscal = x1 * x2 + y1 * y2 + z1 * z2
//        //      return
//        //      end
//        private double dscal(double x1, double y1, double z1, double x2, double y2, double z2)
//        {
//            return x1 * x2 + y1 * y2 + z1 * z2;
//        }

//        #endregion
//    }
//}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.ReinforcedConcrete
{
    public partial class RCChecker : GPC.Checker.Common.Checker
    {

        #region Elastic

        /// <summary>
        /// Routine per calcolo tensioni in SLE elastico con parzializzazione sezione
        /// </summary>
        /// <param name="vtN">sforzo normale</param>
        /// <param name="vmxNmm">momento mx</param>
        /// <param name="vmyNmm">momento my</param>
        /// <param name="n">numero barre</param>
        /// <param name="m">numero vertici</param>
        /// <param name="nom">fattore omogeneizzazione</param>
        /// <param name="ameMm2">area barre mm2</param>
        /// <param name="xmeMm">posizione barre x</param>
        /// <param name="ymeMm">posizione barre x</param>
        /// <param name="xveMm">posizione vertici cls x</param>
        /// <param name="yveMm">posizione vertici cls y</param>
        /// <param name="isTractionConcrete">dice se il cls lavora a trazione</param>
        /// <param name="sssMPa">tensioni vertici cls</param>
        /// <param name="tttMPa">tensioni barre acciaio</param>
        /// <param name="z0">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
        /// <param name="z1">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
        /// <param name="z2">coordinate param piano tensione: sigmaCls=z0+x*z1+y*z2</param>
        private void GetElasticTensionsViviani(double vtN,
                                               double vmxNmm,
                                               double vmyNmm,
                                               int n,
                                               int m,
                                               double nom,
                                               double[] ameMm2,
                                               double[] xmeMm,
                                               double[] ymeMm,
                                               double[] xveMm,
                                               double[] yveMm,
                                               bool isTractionConcrete,
                                               out double[] sssMPa,
                                               out double[] tttMPa,
                                               out double z0,
                                               out double z1,
                                               out double z2)
        {
            //    '  Variabili:
            //' nom= coefficiente di omogeneizzazione
            //' n= numero delle armature metalliche
            //' m= numero dei vertici del calcestruzzo (NUMERARE IN SENSO ANTI-ORARIO)
            //' ame(.)= vettore delle aree metalliche
            //' xme(.)= vettore delle ascisse delle aree metalliche
            //' yme(.)= vettore delle ordinate delle aree metalliche
            //' xve(.)= vettore delle ascisse dei vertici di calcestruzzo
            //' yve(.)= vettore delle ordinate dei vertici di calcestruzzo
            //' sss(.)= vettore delle tensioni nel calcestruzzo (MPa)
            //' ttt(.)= vettore delle tensioni nelle armature (MPa)
            //' vt= sforzo normale (KN)(positivo di compressione)
            //' vmx,vmy = momenti flettenti (kN cm)
            //‘ CONVENZIONI COME GELFI
            //'
            //'
            double ff, fx, fy, jx, jy, ic, aa, sx, sy, ix, iy, xy;
            double[] uuu, vvv;
            double ai, h, a1, a2, st, s0, s1, s2, atot, ta1;
            int n0, m0;//, it, jmax, jmin;

            uuu = new double[2 * m + 1];//buffer 
            vvv = new double[2 * m + 1];//buffer 
            sssMPa = new double[m + 1];
            tttMPa = new double[n + 1];
            //'
            //'
            //'  Convenzioni dei segni come GELFI
            //'
            vtN = -vtN;
            vmxNmm = -vmxNmm;
            //vmy = vmy;

            ff = 0.0;
            fx = 0.0;
            fy = 0.0;
            jx = 0.0;
            jy = 0.0;
            ic = 0.0;
            //'
            //'   caratteristiche inerziali omogeneizzate
            //'
            for (int i = 1; i <= n; i++)
            {
                xmeMm[i] = -xmeMm[i];

                ff = ff + nom * ameMm2[i];
                fx = fx + nom * ameMm2[i] * ymeMm[i];
                fy = fy + nom * ameMm2[i] * xmeMm[i];
                jx = jx + nom * ameMm2[i] * System.Math.Pow(ymeMm[i], 2);
                jy = jy + nom * ameMm2[i] * System.Math.Pow(xmeMm[i], 2);
                ic = ic + nom * ameMm2[i] * xmeMm[i] * ymeMm[i];
            }
            //'
            //'    ricerca dell'asse neutro
            //'
            //'  inizializzazione delle variabili
            //'
            for (int i = 1; i <= m; i++)
            {
                xveMm[i] = -xveMm[i];
                uuu[i] = xveMm[i];
                vvv[i] = yveMm[i];
            }

            aa = 0.0;
            sx = 0.0;
            sy = 0.0;
            ix = 0.0;
            iy = 0.0;
            xy = 0.0;
            xveMm[0] = xveMm[m];
            yveMm[0] = yveMm[m];
            uuu[0] = uuu[m];
            vvv[0] = vvv[m];
            m0 = m;
            //        '
            //        '  inizio iterazioni
            //        '
            //        ' routine
            if (m0 >= 2)//If m0< 2 Then// GoTo 100
            {
                for (int i = 0; i < m0; i++)
                {
                    ai = (uuu[i + 1] * vvv[i] - uuu[i] * vvv[i + 1]) / 2.0;
                    aa = aa + ai;
                    sx = sx + ai * (vvv[i] + vvv[i + 1]) / 3.0;
                    sy = sy + ai * (uuu[i] + uuu[i + 1]) / 3.0;
                    ix = ix + ai * (System.Math.Pow(vvv[i], 2) + vvv[i] * vvv[i + 1] + System.Math.Pow(vvv[i + 1], 2)) / 6.0;
                    iy = iy + ai * (System.Math.Pow(uuu[i], 2) + uuu[i] * uuu[i + 1] + System.Math.Pow(uuu[i + 1], 2)) / 6.0;
                    xy = xy + ai * (uuu[i] * vvv[i] + uuu[i] * vvv[i + 1] / 2.0 + uuu[i + 1] * vvv[i] / 2.0 + uuu[i + 1] * vvv[i + 1]) / 6.0;
                }
            }

            //        ' return
            //        '
            //        '
            //100:    
            aa = aa + ff;
            sx = sx + fx;
            sy = sy + fy;
            ix = ix + jx;
            iy = iy + jy;
            xy = xy + ic;
            //        '
            //        '
            //        ' routine 
            aa = System.Math.Sqrt(aa);
            sy = sy / aa;
            sx = sx / aa;
            iy = System.Math.Sqrt(iy - System.Math.Pow(sy, 2));
            xy = (xy - sx * sy) / iy;
            ix = System.Math.Sqrt(ix - System.Math.Pow(sx, 2) - System.Math.Pow(xy, 2));
            z0 = vtN / aa;
            z1 = (vmyNmm - z0 * sy) / iy;
            z2 = (vmxNmm - z0 * sx - z1 * xy) / ix;
            z2 = z2 / ix;
            z1 = (z1 - z2 * xy) / iy;
            z0 = (z0 - z1 * sy - z2 * sx) / aa;
            for (int i = 1; i <= m; i++)
            {
                sssMPa[i] = z0 + z1 * xveMm[i] + z2 * yveMm[i];
            }
            sssMPa[0] = sssMPa[m];
            //        ' return
            //        '
            //        '
            //200:    
            bool canExit = false;
            do
            {
                s0 = z0;
                s1 = z1;
                s2 = z2;
                n0 = 0;
                m0 = -1;
                for (int i = 0; i < m; i++)
                {
                    h = System.Math.Sqrt(System.Math.Pow(xveMm[i + 1] - xveMm[i], 2) + System.Math.Pow(yveMm[i + 1] - yveMm[i], 2));
                    a1 = (xveMm[i + 1] - xveMm[i]) / h;
                    a2 = (yveMm[i + 1] - yveMm[i]) / h;
                    if (isTractionConcrete || sssMPa[i] <= 0.0)
                    {
                        m0 = m0 + 1;
                        uuu[m0] = xveMm[i];
                        vvv[m0] = yveMm[i];
                    }
                    if (isTractionConcrete || sssMPa[i] * sssMPa[i + 1] >= 0.0) continue;// Then GoTo 101
                    m0 = m0 + 1;
                    st = h * sssMPa[i] / (sssMPa[i] - sssMPa[i + 1]);
                    uuu[m0] = xveMm[i] + a1 * st;
                    vvv[m0] = yveMm[i] + a2 * st;
                    n0 = n0 + 1;
                    //101:    Next i
                }
                //        '
                m0 = m0 + 1;
                uuu[m0] = uuu[0];
                vvv[m0] = vvv[0];
                aa = 0.0;
                sx = 0.0;
                sy = 0.0;
                ix = 0.0;
                iy = 0.0;
                xy = 0.0;
                //        '  routine
                //1001: 
                if (m0 >= 2)  //If m0< 2 Then GoTo 1002
                {
                    for (int i = 0; i < m0; i++)
                    {
                        ai = (uuu[i + 1] * vvv[i] - uuu[i] * vvv[i + 1]) / 2.0;
                        aa = aa + ai;
                        sx = sx + ai * (vvv[i] + vvv[i + 1]) / 3.0;
                        sy = sy + ai * (uuu[i] + uuu[i + 1]) / 3.0;
                        ix = ix + ai * (System.Math.Pow(vvv[i], 2) + vvv[i] * vvv[i + 1] + System.Math.Pow(vvv[i + 1], 2)) / 6.0;
                        iy = iy + ai * (System.Math.Pow(uuu[i], 2) + uuu[i] * uuu[i + 1] + System.Math.Pow(uuu[i + 1], 2)) / 6.0;
                        xy = xy + ai * (uuu[i] * vvv[i] + uuu[i] * vvv[i + 1] / 2.0 + uuu[i + 1] * vvv[i] / 2.0 + uuu[i + 1] * vvv[i + 1]) / 6.0;
                    }
                    //        ' return
                }
                //1002:  
                aa = aa + ff;
                sx = sx + fx;
                sy = sy + fy;
                ix = ix + jx;
                iy = iy + jy;
                xy = xy + ic;
                //        ' routine
                aa = System.Math.Sqrt(aa);
                sy = sy / aa;
                sx = sx / aa;
                iy = System.Math.Sqrt(iy - System.Math.Pow(sy, 2));
                xy = (xy - sx * sy) / iy;
                ix = System.Math.Sqrt(ix - System.Math.Pow(sx, 2) - System.Math.Pow(xy, 2));
                z0 = vtN / aa;
                z1 = (vmyNmm - z0 * sy) / iy;
                z2 = (vmxNmm - z0 * sx - z1 * xy) / ix;
                z2 = z2 / ix;
                z1 = (z1 - z2 * xy) / iy;
                z0 = (z0 - z1 * sy - z2 * sx) / aa;
                for (int i = 1; i <= m; i++)
                {
                    sssMPa[i] = z0 + z1 * xveMm[i] + z2 * yveMm[i];
                }
                sssMPa[0] = sssMPa[m];
                //        ' return
                //201:
                canExit = true;
                if (System.Math.Abs(z0 - s0) > 0.00001) { canExit = false; }
                if (System.Math.Abs(z1 - s1) > 0.00001) { canExit = false; }
                if (System.Math.Abs(z2 - s2) > 0.00001) { canExit = false; }//Then GoTo 200
            } while (!canExit);


            atot = 0.0;
            for (int j = 1; j <= m; j++)
            {
                //            '
                xveMm[j] = -xveMm[j];
                //            '
                //sss[j] = sss[j] * 10;MPa
                if ((!isTractionConcrete) && (sssMPa[j] > 0.0))
                {
                    sssMPa[j] = 0.0;
                    continue;//  GoTo 103
                }
                //103:    Next j
            }

            for (int j = 1; j <= n; j++)
            {
                ta1 = nom * (z0 + z1 * xmeMm[j] + z2 * ymeMm[j]);
                atot = atot + ameMm2[j];
                //ta1 = ta1 * 10;//MPa
                tttMPa[j] = ta1;
                xmeMm[j] = -xmeMm[j];
            }
            //        '
            //        '     USCITE
            z1 *= -1;
        }

        #endregion

        #region Plastic

        // NB: porting c# di fortran code. arrays must be based on first index=1!
        // NOTE:
        //- manca possibilità di definire i limiti di deformazione
        //- manca limitazione a 0.8NMaxRd per stati di compressione senza M

        //c
        //c------------------------------------------------------------------c
        //c SUBROUTINE  PER IL   CALCOLO DELLE  CARATTERISTICHE c
        //c ULTIME   PER UNA   SEZIONE QUALSIASI                      c
        //c------------------------------------------------------------------c
        //c
        //      subroutine  limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl,
        //     1ic, fys, fysl, fck, gs, gsl, gc, eta, teta, e, x, y, z, b, h,
        //     1kvc, kvp, ndkvc, ndkvp, ipc, ndpc, npc, aux, ndaux)
        //      dimension vc(ndvc), vs(ndvs), kvp(ndkvp), kvc(ndkvc), ipc(ndpc),
        //     1aux(ndaux), vsl(ndvsl)
        //      double precision vc, vs, vsl, fys, fysl, fck, gs, gsl, gc, eta, teta,
        //     1e, x, y, z, b, h, aux, w2, w0, chi, cmax, cmin, dmin, dmax, dmin1, dmax1,
        //     1epsc, epst, s, c, xa, ya, xb, yb, af, sig, epsa, epsb, w1, waux, xf, yf, v1,
        //     1eps, xc, yc
        //      double precision depsil, dsgmaf, dsgmac
        //c
        //c   descrizione delle variabili:
        //c   VC = vettore organizzato in gruppi di tre elementi, contenente
        //c       i dati relativi ai vertici della sezione in calcestruzzo.
        //c   NVC = numero dei gruppi di VC(vertici)
        //c   VS = vettore organizzato in gruppi di tre elementi, contenente
        //c       i dati relativi alle armature metalliche puntiformi:
        //c        As - X - Y
        //c   NVS = numero dei gruppi di VS
        //c   VSL = vettore organizzato in gruppi di cinque elementi, contenente
        //c        i dati relativi alle armature metalliche lineari:
        //c        As - Xi - Yi - Xf - Yf
        //c   NVSL = numero dei gruppi di VSL
        //c   IPC = vettore organizzato in gruppi di elementi che descrivono
        //c        una particolare partizione convessa.In particolare il primo
        //c        indice riporta il numero dei vertici della partizione
        //c        considerata.
        //c   NPC = numero delle parti convesse della figura.
        //c   AUX, KVP, KVC = vettori di lavoro
        //c   NDAUX, NDKVP, NDKVC = dimensioni dei vettori sopra
        //c   IC = indice di zona di rottura 1 - 2 - 3 - 4 - 5 - 6
        //c   B = lato parallelo all'asse X
        //c   H = "           "         Y
        //c   vse = vettore contenente, per ogni barra di armatura, modulo elastico, pretensione e fyk
        //c    Z = SFORZO NORMALE, positivo se di compressione
        //c    Y = MOM.FLETTENTE attorno all'asse y, positivo se antiorario
        //c    X = MOM.FLETTENTE attorno all'asse x, positivo se antiorario
        //c   FYS = tensione di snervamento dell'acciaio  puntiforme
        //c   FYSL = "               "          "         lineare
        //c   FCK = tensione caratteristica del calcestruzzo(cilindrica)
        //c   GC = coefficiente riduttivo per il cls. (1.5 con EC, 1 con AASHTO)
        //c   GS = "               "     per l'acciaio  puntiforme. (1.15 con EC, 1 con AASHTO)
        //c   GSL = "               "           "        lineare. (1.05 con EC, 1 con AASHTO)
        //c   E = modulo elastico dell'acciaio lineare (per le armature vedi vse)
        //c   ETA = primo parametro
        //c   TETA = secondo parametro
        //c   N.B.: i valori di epsilon sono moltiplicati per mille
        //    epcCMax valore della deformazione max del cls in valore assoluto (-0.0035 con EC)
        //    fiTensAASHTO: valore di fi a trazione per norma AASHTO (0.9 con aashto, 1 con EC)
        //    fiCompAASHTO: valore di fi compressione per norma AASHTO (0.75 con aashto, 1 con EC)
        //    epsCl: limite deformazione acciaio per considerare fiComp (0.002 per AASHTO)
        //    epsTl: limite deformazione acciaio per considerare fiTens (0.005 per AASHTO)
        //    alfaCC: fattore riduzione resist cls (0.85 per EC, 1 per AASHTO)
        //    epsSu: deformazione ultima dell'acciaio
        private void GetPlasticResistancesCAViviani(double[] vc, double[] vs, double[] vsl, int nvc, int nvs, int nvsl, int ic,
                                                     double fysl, double fck, double gs, double gsl, double gc, double eta, double teta,
                                                     double esl, out double x, out double y, out double z, double b, double h, double[] vse,
                                                     int[] ipc, int npc,
                                                     double epsCMin, double fiTensAASHTO, double fiCompAASHTO, double alfaCC, bool useLimit34AASHTO,
                                                     double epsSu)
        {
            double w2, w0, chi, cmax, cmin, dmin, dmax, dmin1, dmax1, epsc, s, c, xa, ya, xb, yb, af, sig, epsa, epsb,
                   w1, xf, yf, v1, eps, xc, yc;

            const double ksi60 = 413.68;
            const double ksi100 = 689.476;
            const double ksi75 = 517.107;
            double[] aux = new double[nvc * 10];
            int[] kvc = new int[nvc * 10];
            int[] kvp = new int[nvc * 10];
            double epsCMaxAbs1000, epsCl1000, epsTl1000;
            double epsCl, epsTl;
            double fi;
            double epsSu1000;
            bool isPrestressed;
            double minFys, esOnMin;

            //c
            //c
            //c---- scelta della zona di rottura
            //c
            x = 0;
            y = 0;
            z = 0;
            dmax = double.MinValue;//0;
            dmin = double.MaxValue;// 0;
            xc = 0;
            yc = 0;
            s = System.Math.Sin(teta);
            c = System.Math.Cos(teta);
            epsCMaxAbs1000 = -epsCMin * 1000;



            isPrestressed = false;
            minFys = double.MaxValue;
            esOnMin = double.MaxValue;
            for (int i = 1; i < nvs; i++)
            {
                int j1 = 3 * (i - 1) + 1;
                if (minFys > vse[j1 + 2])
                {
                    minFys = vse[j1 + 2];
                    esOnMin = vse[j1];
                }
                if (System.Math.Abs(vse[j1 + 1]) > 0.1)
                {
                    isPrestressed = true;
                }
            }

            if (isPrestressed)
            {
                epsCl = 0.002;
                epsTl = 0.005;
            }
            else
            {

                if (minFys <= ksi60)
                {
                    epsCl = System.Math.Min(0.002, minFys / esOnMin);
                }
                else if (minFys >= ksi100)
                {
                    epsCl = 0.004;
                }
                else
                {
                    
                    double lower, upper;
                    lower = System.Math.Min(0.002, minFys / esOnMin);
                    upper = 0.004;
                    epsCl = Utilities.Maths.Interpolation.GetLinearInterpolation(ksi60, ksi100, lower, upper, minFys);
                }

                if (minFys <= ksi75)
                {
                    epsTl = 0.005;
                }
                else if (minFys >= ksi100)
                {
                    epsTl = 0.008;
                }
                else
                {
                    double lower, upper;
                    lower = 0.005;
                    upper = 0.008;
                    epsTl = Utilities.Maths.Interpolation.GetLinearInterpolation(ksi75, ksi100, lower, upper, minFys);
                }
            }

            epsCl1000 = epsCl * 1000;
            epsTl1000 = epsTl * 1000;
            epsSu1000 = epsSu * 1000;

            if (useLimit34AASHTO && epsTl1000 >= epsSu1000)
            {
                throw new NotSupportedException("In AASHTO epsSu cannot be < epsTl = " + epsTl.ToString("F2"));
            }

            //      goto(10,20,30,40,50,60),ic
            if (ic == 1)
            {
                //c
                //c---- sezione interamente tesa.
                //c
                //c ----campo di rottura n.1
                //c
                //10    do 11 j = 1,nvs
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vs(j1 + 1) * c - vs(j1) * s
                //      if (w1.le.dmin) then
                //                      dmin = w1
                //                    xc = vs(j1)
                //                    yc = vs(j1 + 1)
                //                    goto 11
                //      endif
                //      if (w1.ge.dmax) dmax = w1
                //11    continue
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vs[j1 + 1] * c - vs[j1] * s;
                    if (w1 <= dmin)
                    {
                        dmin = w1;
                        xc = vs[j1];
                        yc = vs[j1 + 1];
                        continue;//goto 11
                    }
                    if (w1 >= dmax)
                    {
                        dmax = w1;
                    }
                }

                //      do 13 j = 1,nvsl
                //        j1 = 5 * (j - 1) + 2
                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
                //      if (w1.le.dmin) then
                //                      dmin = w1
                //                    xc = vsl(j1)
                //                    yc = vsl(j1 + 1)
                //                    goto 14
                //      endif
                //      if (w1.ge.dmax) dmax = w1
                //14    if (w2.le.dmin) then
                //                      dmin = w2
                //                    xc = vsl(j1 + 2)
                //                    yc = vsl(j1 + 3)
                //                    goto 13
                //      endif
                //      if (w2.ge.dmax) dmax = w2
                //13    continue
                for (int j = 1; j <= nvsl; j++)
                {
                    int j1 = 5 * (j - 1) + 2;
                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
                    if (w1 <= dmin)
                    {
                        dmin = w1;
                        xc = vsl[j1];
                        yc = vsl[j1 + 1];
                    }
                    else if (w1 >= dmax)
                    {
                        dmax = w1;
                    }
                    if (w2 <= dmin)
                    {
                        dmin = w2;
                        xc = vsl[j1 + 2];
                        yc = vsl[j1 + 3];
                    }
                    else if (w2 >= dmax)
                    {
                        dmax = w2;
                    }
                }

                //      dmax1 = 0.
                //      do 12 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax1) dmax1 = w1
                //12    continue
                //      cmin = (10.- fys / e * 1000./ gs) / (dmax - dmin)
                //      cmax = 10./ (dmax1 - dmin)
                //      chi = cmin + eta * (cmax - cmin)
                //      v1 = 10.
                //      epsc = v1 - chi * (dmax1 - dmin)
                //      if (epsc.lt.0.0001.and.eta.le.1.)then
                //                                       chi = cmax
                //                                      epsc = 0.
                //      endif
                //      w0 = dmax1 + epsc / chi
                //      w2 = w0 + 2./ chi
                //      goto 100
                dmax1 = 0;
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax1) dmax1 = w1;
                }
                cmin = (epsSu1000 - minFys / esOnMin * 1000.0 / gs) / (dmax - dmin);
                cmax = epsSu1000 / (dmax1 - dmin);
                chi = cmin + eta * (cmax - cmin);
                v1 = epsSu1000;
                epsc = v1 - chi * (dmax1 - dmin);
                if (epsc < 0.0001 && eta <= 1)
                {
                    chi = cmax;
                    epsc = 0;
                }
                w0 = dmax1 + epsc / chi;
                w2 = w0 + 2.0 / chi;
                fi = fiTensAASHTO;
            }
            else if (ic == 2)
            {
                //c
                //c ----sezione parzializzata con armatura tesa al massimo allungamento
                //c
                //c---- campo di rottura n.2
                //c
                //20    do 21 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax) dmax = w1
                //21    continue
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax) dmax = w1;
                }

                //      do 22 j = 1,nvs
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vs(j1 + 1) * c - vs(j1) * s
                //      if (w1.le.dmin) then
                //                      dmin = w1
                //                    xc = vs(j1)
                //                    yc = vs(j1 + 1)
                //      endif
                //22    continue
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vs[j1 + 1] * c - vs[j1] * s;
                    if (w1 <= dmin)
                    {
                        dmin = w1;
                        xc = vs[j1];
                        yc = vs[j1 + 1];
                    }
                }

                //      do 23 j = 1,nvsl
                //        j1 = 5 * (j - 1) + 2
                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
                //      if (w1.le.dmin) then
                //                      dmin = w1
                //                    xc = vsl(j1)
                //                    yc = vsl(j1 + 1)
                //      endif
                //      if (w2.le.dmin) then
                //                      dmin = w2
                //                    xc = vsl(j1 + 2)
                //                    yc = vsl(j1 + 3)
                //      endif
                //23    continue
                for (int j = 1; j <= nvsl; j++)
                {
                    int j1 = 5 * (j - 1) + 2;
                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
                    if (w1 <= dmin)
                    {
                        dmin = w1;
                        xc = vsl[j1];
                        yc = vsl[j1 + 1];
                    }
                    if (w2 <= dmin)
                    {
                        dmin = w2;
                        xc = vsl[j1 + 2];
                        yc = vsl[j1 + 3];
                    }
                }

                //      cmax = 13.5 / (dmax - dmin)
                //      cmin = 10./ (dmax - dmin)
                //      chi = cmin + eta * (cmax - cmin)
                //      v1 = 10.
                //      epsc = v1 - (dmax - dmin) * chi
                //      w0 = dmax + epsc / chi
                //      w2 = w0 + 2./ chi
                //      goto 100
                cmax = (epsSu1000 + epsCMaxAbs1000) / (dmax - dmin);
                cmin = epsSu1000 / (dmax - dmin);
                chi = cmin + eta * (cmax - cmin);
                v1 = epsSu1000;
                epsc = v1 - (dmax - dmin) * chi;
                w0 = dmax + epsc / chi;
                w2 = w0 + 2.0 / chi;
                fi = fiTensAASHTO;
            }
            else if (ic == 3)
            {
                //c
                //c ----sezione parzializzata
                //c
                //c
                //c ----campo di rottura n.3
                //c
                //30    do 31 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax) then
                //                      dmax = w1
                //                    xc = vc(j1)
                //                    yc = vc(j1 + 1)
                //      endif
                //31    continue
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax)
                    {
                        dmax = w1;
                        xc = vc[j1];
                        yc = vc[j1 + 1];
                    }
                }

                //      do 32 j = 1,nvs
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vs(j1 + 1) * c - vs(j1) * s
                //      if (w1.le.dmin) dmin = w1
                //32    continue
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vs[j1 + 1] * c - vs[j1] * s;
                    if (w1 <= dmin) dmin = w1;
                }

                //      do 33 j = 1,nvsl
                //        j1 = 5 * (j - 1) + 2
                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
                //      if (w1.le.dmin) dmin = w1
                //      if (w2.le.dmin) dmin = w2
                //33    continue
                for (int j = 1; j <= nvsl; j++)
                {
                    int j1 = 5 * (j - 1) + 2;
                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
                    if (w1 <= dmin) dmin = w1;
                    if (w2 <= dmin) dmin = w2;
                }

                //      v1 = -3.5
                //      cmin = (fys / gs / e * 1000.+ 3.5) / (dmax - dmin)
                //      cmax = 13.5 / (dmax - dmin)
                //      chi = cmax + eta * (cmin - cmax)
                //      w0 = dmax + v1 / chi
                //      w2 = w0 + 2./ chi
                //      goto 100
                v1 = -epsCMaxAbs1000;
                if (useLimit34AASHTO)
                {
                    cmin = (epsCl1000 + epsCMaxAbs1000) / (dmax - dmin);
                }
                else
                {
                    cmin = (minFys / gs / esOnMin * 1000 + epsCMaxAbs1000) / (dmax - dmin);
                }
                cmax = (epsSu1000 + epsCMaxAbs1000) / (dmax - dmin);
                chi = cmax + eta * (cmin - cmax);
                w0 = dmax + v1 / chi;
                w2 = w0 + 2.0 / chi;
                double et = chi * (dmax - dmin) - epsCMaxAbs1000;

                if (et <= epsCl1000)
                {
                    fi = fiCompAASHTO;
                }
                else if (et >= epsTl1000)
                {
                    fi = fiTensAASHTO;
                }
                else
                {
                    if (System.Math.Abs(epsTl1000 - epsCl1000) < 0.00001)
                    {
                        fi = fiCompAASHTO;
                    }
                    else
                    {
                        fi = fiCompAASHTO + (fiTensAASHTO - fiCompAASHTO) * (et - epsCl1000) / (epsTl1000 - epsCl1000);
                    }
                }
            }
            else if (ic == 4)
            {
                //c
                //c ----campo di rottura n.4
                //c
                //40    do 41 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax) then
                //                      dmax = w1
                //                    xc = vc(j1)
                //                    yc = vc(j1 + 1)
                //      endif
                //41    continue
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax)
                    {
                        dmax = w1;
                        xc = vc[j1];
                        yc = vc[j1 + 1];
                    }
                }

                //      do 42 j = 1,nvs
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vs(j1 + 1) * c - vs(j1) * s
                //      if (w1.le.dmin) dmin = w1
                //42    continue
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vs[j1 + 1] * c - vs[j1] * s;
                    if (w1 <= dmin) dmin = w1;
                }

                //      do 43 j = 1,nvsl
                //        j1 = 5 * (j - 1) + 2
                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
                //      if (w1.le.dmin) dmin = w1
                //      if (w2.le.dmin) dmin = w2
                //43    continue
                for (int j = 1; j <= nvsl; j++)
                {
                    int j1 = 5 * (j - 1) + 2;
                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
                    if (w1 <= dmin) dmin = w1;
                    if (w2 <= dmin) dmin = w2;
                }

                //      v1 = -3.5
                //      cmax = (fys / gs / e * 1000.+ 3.5) / (dmax - dmin)
                //      cmin = 3.5 / (dmax - dmin)
                //      chi = cmax + eta * (cmin - cmax)
                //      w0 = dmax + v1 / chi
                //      w2 = w0 + 2./ chi
                //      goto 100
                v1 = -epsCMaxAbs1000;
                if (useLimit34AASHTO)
                {
                    cmax = (epsCl1000 + epsCMaxAbs1000) / (dmax - dmin);
                }
                else
                {
                    cmax = (minFys / gs / esOnMin * 1000 + epsCMaxAbs1000) / (dmax - dmin);
                }
                cmin = epsCMaxAbs1000 / (dmax - dmin);
                chi = cmax + eta * (cmin - cmax);
                w0 = dmax + v1 / chi;
                w2 = w0 + 2.0 / chi;
                fi = fiCompAASHTO;
            }
            else if (ic == 5)
            {
                //c
                //c ----sezione parzializzata, armatura metallica tutta compressa
                //c
                //c
                //c---- campo di rottura n.5
                //c
                //50    do 51 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax) then
                //                      dmax = w1
                //                    xc = vc(j1)
                //                    yc = vc(j1 + 1)
                //      endif
                //      if (w1.le.dmin) dmin = w1
                //51    continue
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax)
                    {
                        dmax = w1;
                        xc = vc[j1];
                        yc = vc[j1 + 1];
                    }
                    if (w1 <= dmin) dmin = w1;
                }

                //      dmin1 = 0.
                //      do 52 j = 1,nvs
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vs(j1 + 1) * c - vs(j1) * s
                //      if (w1.le.dmin1) dmin1 = w1
                //52    continue
                dmin1 = 0;
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vs[j1 + 1] * c - vs[j1] * s;
                    if (w1 <= dmin1) dmin1 = w1;
                }

                //      do 53 j = 1,nvsl
                //        j1 = 5 * (j - 1) + 2
                //      w1 = vsl(j1 + 1) * c - vsl(j1) * s
                //      w2 = vsl(j1 + 3) * c - vsl(j1 + 2) * s
                //      if (w1.le.dmin1) dmin1 = w1
                //      if (w2.le.dmin1) dmin1 = w2
                //53    continue
                for (int j = 1; j <= nvsl; j++)
                {
                    int j1 = 5 * (j - 1) + 2;
                    w1 = vsl[j1 + 1] * c - vsl[j1] * s;
                    w2 = vsl[j1 + 3] * c - vsl[j1 + 2] * s;
                    if (w1 <= dmin1) dmin1 = w1;
                    if (w2 <= dmin1) dmin1 = w2;
                }

                //      v1 = -3.5
                //      cmax = 3.5 / (dmax - dmin1)
                //      cmin = 3.5 / (dmax - dmin)
                //      chi = cmax + eta * (cmin - cmax)
                //      w0 = dmax + v1 / chi
                //      w2 = w0 + 2./ chi
                //      goto 100
                v1 = -epsCMaxAbs1000;
                cmax = epsCMaxAbs1000 / (dmax - dmin1);
                cmin = epsCMaxAbs1000 / (dmax - dmin);
                chi = cmax + eta * (cmin - cmax);
                w0 = dmax + v1 / chi;
                w2 = w0 + 2.0 / chi;
                fi = fiCompAASHTO;
            }
            else if (ic == 6)
            {
                //c
                //c ----sezione interamente compressa
                //c
                //c ----campo di rottura n.6
                //c
                //60    do 61 j = 1,nvc
                //        j1 = 3 * (j - 1) + 2
                //      w1 = vc(j1 + 1) * c - vc(j1) * s
                //      if (w1.ge.dmax) dmax = w1
                //      if (w1.le.dmin) dmin = w1
                //61    continue
                for (int j = 1; j <= nvc; j++)
                {
                    int j1 = 3 * (j - 1) + 2;
                    w1 = vc[j1 + 1] * c - vc[j1] * s;
                    if (w1 >= dmax) dmax = w1;
                    if (w1 <= dmin) dmin = w1;
                }

                //c---- calcolo del punto di rotazione
                //     xc = -3./ 7.* (dmax - dmin) + dmax
                //      w2 = xc
                //      yc = xc * c
                //      xc = -xc * s
                //      v1 = -2.
                //      cmax = 3.5 / (dmax - dmin)
                //      cmin = 0.
                //      chi = cmax * (1.- eta)
                //      w1 = 1.
                //      if (dabs(chi).gt.0.0001)w1 = chi
                //      w0 = w2 - 2./ w1

                //xc = -3.0 / 7.0 * (dmax - dmin) + dmax; modificato per introduzione epscMax
                xc = -(epsCMaxAbs1000 - 2) / epsCMaxAbs1000 * (dmax - dmin) + dmax;
                w2 = xc;
                yc = xc * c;
                xc = -xc * s;
                v1 = -2;
                cmax = epsCMaxAbs1000 / (dmax - dmin);
                cmin = 0;
                chi = cmax * (1 - eta);
                w1 = 1;
                if (System.Math.Abs(chi) > 0.0001) w1 = chi;
                w0 = w2 - 2.0 / w1;
                fi = fiCompAASHTO;
            }
            else
            {
                throw new NotSupportedException("Campo di rottura inesistente");
            }

            //c
            //c ----calcolo delle sollecitazioni ultime
            //c
            //c---- calcolo del solido tensionale esteso all'area di calcestruzzo
            //c
            //c
            //100   call tecls(vc, nvc, ndvc, ipc, npc, ndpc, kvc, ndkvc, kvp, ndkvp,
            //     1aux, ndaux, xc, yc, chi, v1, s, c, w2, w0, fck, gc, b, h, eta, teta, x, y, z)

            tecls(ref vc, ref nvc, ref ipc, ref npc, ref kvc, ref kvp, ref aux, ref xc, ref yc, ref chi, ref v1, ref s,
                  ref c, ref w2, ref w0, ref fck, ref gc, ref b, ref h, ref eta, ref teta, ref x, ref y, ref z, ref alfaCC);

            //c
            //c ----integrazione delle armature metalliche
            //c
            //c---- armature puntiformi
            //c
            //110   if (nvs.lt.1)goto 120
            //      do 201 j = 1,nvs
            //        j1 = 3 * (j - 1) + 1
            //      af = vs(j1)
            //      xf = vs(j1 + 1)
            //      yf = vs(j1 + 2)
            //      eps = depsil(xc, yc, s, c, chi, v1, xf, yf)
            //      sig = dsgmaf(fys, gs, eps, e, fck, gc)
            //      if (sig.lt.0.)sig = sig + dsgmac(fck, gc, eps)
            //      z = z - af * sig / b / h
            //      y = y - af * sig * xf / b / h / b
            //201   x = x + af * sig * yf / b / h / h
            if (!(nvs < 1))
            {
                for (int j = 1; j <= nvs; j++)
                {
                    int j1 = 3 * (j - 1) + 1;
                    double ej, sigma0, fy;
                    af = vs[j1];
                    xf = vs[j1 + 1];
                    yf = vs[j1 + 2];
                    ej = vse[j1];
                    sigma0 = vse[j1 + 1];
                    fy = vse[j1 + 2];
                    eps = depsil(xc, yc, s, c, chi, v1, xf, yf);
                    sig = dsgmaf(fy, gs, eps, ej, fck, gc, sigma0);
                    if (sig < 0) sig = sig + dsgmac(fck, gc, eps, alfaCC);
                    z = z - af * sig / b / h;
                    y = y - af * sig * xf / b / h / b;
                    x = x + af * sig * yf / b / h / h;
                }
            }

            //c
            //c ----armature lineari
            //c
            //120   if (nvsl.lt.1)goto 500
            //      nel = 3
            //      do 121 j = 1,nvsl
            //        j1 = 5 * (j - 1) + 1
            //      af = vsl(j1)
            //      xa = vsl(j1 + 1)
            //      ya = vsl(j1 + 2)
            //      xb = vsl(j1 + 3)
            //      yb = vsl(j1 + 4)
            //      epsa = depsil(xc, yc, s, c, chi, v1, xa, ya)
            //      epsb = depsil(xc, yc, s, c, chi, v1, xb, yb)
            //      if (epsa.le.epsb) goto 122
            //c---- scambio di A con B
            //     w1 = xa
            //      xa = xb
            //      xb = w1
            //      w1 = ya
            //      ya = yb
            //      yb = w1
            //      w1 = epsa
            //      epsa = epsb
            //      epsb = w1
            //122   aux(1) = -fysl / e / gsl * 1000.
            //      aux(2) = -aux(1)
            //      aux(3) = 10.
            //      call ilgs(xa, ya, epsa, xb, yb, epsb, nel, aux, ndaux, x, y, z,
            //     1xc, yc, s, c, chi, v1, fysl, gsl, e, fck, gc, af, b, h)
            //      aux(1) = -3.5
            //      aux(2) = -2.
            //      aux(3) = 0.
            //121   call ilgc(xa, ya, epsa, xb, yb, epsb, nel, aux, ndaux, x, y, z,
            //     1xc, yc, s, c, chi, v1, fck, gc, af, b, h)
            //500   return
            //      end
            //c


            if (!(nvsl < 1))
            {
                int nel = 3;
                for (int j = 1; j <= nvsl; j++)//121
                {
                    int j1 = 5 * (j - 1) + 1;
                    af = vsl[j1];
                    xa = vsl[j1 + 1];
                    ya = vsl[j1 + 2];
                    xb = vsl[j1 + 3];
                    yb = vsl[j1 + 4];
                    epsa = depsil(xc, yc, s, c, chi, v1, xa, ya);
                    epsb = depsil(xc, yc, s, c, chi, v1, xb, yb);
                    //      if (epsa.le.epsb) goto 122
                    if (!(epsa <= epsb))
                    {
                        //c---- scambio di A con B
                        w1 = xa;
                        xa = xb;
                        xb = w1;
                        w1 = ya;
                        ya = yb;
                        yb = w1;
                        w1 = epsa;
                        epsa = epsb;
                        epsb = w1;
                    }
                    aux[1] = -fysl / esl / gsl * 1000;//122
                    aux[2] = -aux[1];
                    aux[3] = epsSu1000;
                    ilgs(ref xa, ref ya, ref epsa, ref xb, ref yb, ref epsb, ref nel, ref aux, ref x, ref y, ref z,
                         ref xc, ref yc, ref s, ref c, ref chi, ref v1, ref fysl, ref gsl, ref esl, ref fck, ref gc, ref af, ref b, ref h);
                    aux[1] = -epsCMaxAbs1000;
                    aux[2] = -2;
                    aux[3] = 0;
                    ilgc(ref xa, ref ya, ref epsa, ref xb, ref yb, ref epsb, ref nel, ref aux, ref x, ref y, ref z,
                         ref xc, ref yc, ref s, ref c, ref chi, ref v1, ref fck, ref gc, ref af, ref b, ref h, ref alfaCC);
                }
            }
            x *= fi;
            y *= fi;
            z *= fi;
            //500   return
            //      end
            //c
        }

        //c-------------------------------------------------------------------c
        //c Subroutine  per integrare il solido delle tensioni sull'intera  c
        //c superficie di calcestruzzo.                                     c
        //c-------------------------------------------------------------------c
        //c
        //      subroutine tecls(vc, nvc, ndvc, ipc, npc, ndpc, kvc, ndkvc, kvp, ndkvp,
        //     1aux, ndaux, xc, yc, chi, v1, s, c, w2, w0, fck, gc, b, h, eta, teta, x, y, z)
        private void tecls(ref double[] vc, ref int nvc, ref int[] ipc, ref int npc, ref int[] kvc, ref int[] kvp,
                           ref double[] aux, ref double xc, ref double yc, ref double chi, ref double v1, ref double s,
                           ref double c, ref double w2, ref double w0, ref double fck, ref double gc, ref double b, ref double h,
                           ref double eta, ref double teta, ref double x, ref double y, ref double z, ref double alfaCc)

        {
            double x1, x2, x3, y1, y2, y3, epst, epsc, xv, yv, w1, cs;
            //c
            //c---- Descrizione delle variabili:
            //            c VC = vettore contenente i vertici della sezione in cls.
            //        c NVC = numero dei vertici in cls.
            //     c IPC = vettore contenente gli indici delle parti convesse
            //c NPC = numero delle parti convesse
            //c      KVC = vettore contenente i vertici con tensione parabolica
            //c KVP = vettore contenente i vertici con tensione costante
            //c AUX = vettore di lavoro
            //c NDVC, NDPC, NDKVC, NDKVP, NDAUX = dimensioni dei vettori corr.
            //c XC, YC, CHI, V1 = parametri del piano di deformazione
            //c S, C, ETA, TETA = parametri di posizione dell'asse neutro
            //c W2, W0 = parametri per le rette con eps = 2 / 1000 e eps = 0.
            //  c      FCK,GC = parametri di resistenza del calcestruzzo
            //c B, H = parametri di adimensionalizzazione
            //c X, Y, Z = caratteristiche della sollecitazione.
            //c
            //      dimension ipc(ndpc),aux(ndaux),kvc(ndkvc),kvp(ndkvp),vc(ndvc)
            //c
            //c ---trasferimento di ogni singola parte convessa
            //c     in un vettore di lavoro.
            //c
            //      inpc = 1
            //      ni = 1

            int inpc = 1;
            int ni = 1;
            do//70
            {
                //70    n1 = ni + 1
                int n1 = ni + 1;

                //c
                //c ---calcolo del segno del solido tensionale
                //c
                //      cs = dble(ipc(ni))
                //      cs = cs / dabs(cs)
                //      n2 = ni + iabs(ipc(ni))
                //      ni = n2 + 1
                //      i0 = 0
                cs = ipc[ni];
                cs = cs / System.Math.Abs(cs);
                int n2 = ni + System.Math.Abs(ipc[ni]);
                ni = n2 + 1;
                int i0 = 0;

                //      do 10 i = n1,n2
                //        i0 = i0 + 1
                //      i1 = 3 * (ipc(i) - 1) + 2
                //      j1 = 3 * (i0 - 1) + 2
                //      aux(j1) = vc(i1)
                //10    aux(j1 + 1) = vc(i1 + 1)
                for (int i = n1; i <= n2; i++)
                {
                    i0 = i0 + 1;
                    int i1 = 3 * (ipc[i] - 1) + 2;
                    int j1 = 3 * (i0 - 1) + 2;
                    aux[j1] = vc[i1];
                    aux[j1 + 1] = vc[i1 + 1];
                }


                //c
                //c ----calcolo dei valori estremi di deformazione della partizione
                //c     convessa
                //c
                //      naux = n2 - n1 + 1
                //      epsc = 20.
                //      epst = -4.
                int naux = n2 - n1 + 1;
                epsc = 20;
                epst = -4;

                //      do 20 i = 1,naux
                //        i1 = 3 * (i - 1) + 2
                //      xv = aux(i1)
                //      yv = aux(i1 + 1)
                //      w1 = depsil(xc, yc, s, c, chi, v1, xv, yv)
                //      if (w1.le.epsc) then
                //                      ivc = i
                //                    epsc = w1
                //      endif
                //      if (w1.ge.epst) then
                //                      ivt = i
                //                    epst = w1
                //      endif
                //20    continue
                int ivc = 0, ivt = 0;
                for (int i = 1; i <= naux; i++)
                {
                    int i1 = 3 * (i - 1) + 2;
                    xv = aux[i1];
                    yv = aux[i1 + 1];
                    w1 = depsil(xc, yc, s, c, chi, v1, xv, yv);
                    if (w1 <= epsc)
                    {
                        ivc = i;
                        epsc = w1;
                    }
                    if (w1 >= epst)
                    {
                        ivt = i;
                        epst = w1;
                    }
                }

                //c
                //c ----calcolo del tipo di funzione da integrare (da 1 a 6)
                //c
                //c     IZ = 1  partizione interamente tesa.
                // c      "  2       "     parzialmente compressa parabolicamente.
                // c      "  3       "     interamente     "            ".
                // c      "  4       "         "       compressa con tratti costanti
                //c e tratti parabolici.
                //c      "  5       "     interamente compressa in modo costante.
                //c      "  6       "     parzialmente compressa con tratti costanti
                //c                       e tratti parabolici.
                //c
                //      if (epsc.ge.- 0.001) then
                //                       iz = 1
                //                    goto 50
                //      endif
                int iz;
                if (epsc >= -0.001)
                {
                    iz = 1;
                }
                else
                {
                    //      if (epst.gt.0..and.epsc.ge.- 2.)then
                    //                                      iz = 2
                    //                                    goto 30
                    //      endif
                    //      if (epst.le.0..and.epsc.ge.- 2.)then
                    //                                      iz = 3
                    //                                    goto 30
                    //      endif
                    //      if (epst.gt.0.)then
                    //                     iz = 6
                    //                    goto 30
                    //      endif
                    //      if (epst.le.- 2.) then
                    //                        iz = 5
                    //                     goto 30
                    //      endif
                    //      iz = 4
                    if (epst > 0 && epsc >= -2)
                    {
                        iz = 2;
                    }
                    else if (epst <= 0 && epsc >= -2)
                    {
                        iz = 3;
                    }
                    else if (epst > 0)
                    {
                        iz = 6;
                    }
                    else if (epst <= -2)
                    {
                        iz = 5;
                    }
                    else
                    {
                        iz = 4;
                    }

                    //c
                    //c ----chiamata della routine zone per la divisione della partizione
                    //c convessa
                    //c
                    //30    nsp = 0
                    //      nsc = 0
                    //      call zone(aux, naux, ndaux, epsc, epst, w2, w0, kvc, kvp, nsp, nsc,
                    //     1ndkvc, ndkvp, ivc, ivt, s, c, iz)
                    int nsp = 0;
                    int nsc = 0;
                    zone(ref aux, ref naux, ref epsc, ref epst, ref w2, ref w0, ref kvc, ref kvp, ref nsp, ref nsc, ref ivc, ref ivt,
                         ref s, ref c, ref iz);

                    //c
                    //c ----integrazione della parte con tensione costante
                    //c
                    //      if (nsc.lt.1)goto 40
                    if (!(nsc < 1))
                    {
                        //      j1 = 3 * (kvc(1) - 1) + 2
                        //      x1 = aux(j1)
                        //      y1 = aux(j1 + 1)
                        //      nsc1 = nsc - 1

                        int j1 = 3 * (kvc[1] - 1) + 2;
                        x1 = aux[j1];
                        y1 = aux[j1 + 1];
                        int nsc1 = nsc - 1;

                        //      do 41 j = 2,nsc1
                        //        j2 = 3 * (kvc(j) - 1) + 2
                        //      j3 = 3 * (kvc(j + 1) - 1) + 2
                        //      x2 = aux(j2)
                        //      y2 = aux(j2 + 1)
                        //      x3 = aux(j3)
                        //      y3 = aux(j3 + 1)
                        //41    call cost(x1, y1, x2, y2, x3, y3, fck, gc, x, y, z, b, h, cs)
                        for (int j = 2; j <= nsc1; j++)
                        {
                            int j2 = 3 * (kvc[j] - 1) + 2;
                            int j3 = 3 * (kvc[j + 1] - 1) + 2;
                            x2 = aux[j2];
                            y2 = aux[j2 + 1];
                            x3 = aux[j3];
                            y3 = aux[j3 + 1];
                            cost(ref x1, ref y1, ref x2, ref y2, ref x3, ref y3, ref fck, ref gc, ref x, ref y, ref z, ref b, ref h, ref cs, ref alfaCc);
                        }
                    }

                    //c
                    //c---- integrazione della parte con tensione parabolica
                    //c
                    //40    if (nsp.lt.1)goto 50
                    if (!(nsp < 1))
                    {
                        //      j1 = 3 * (kvp(1) - 1) + 2
                        //      x1 = aux(j1)
                        //      y1 = aux(j1 + 1)
                        //      nsp1 = nsp - 1
                        int j1 = 3 * (kvp[1] - 1) + 2;
                        x1 = aux[j1];
                        y1 = aux[j1 + 1];
                        int nsp1 = nsp - 1;

                        //      do 42 j = 2,nsp1
                        //        j2 = 3 * (kvp(j) - 1) + 2
                        //      j3 = 3 * (kvp(j + 1) - 1) + 2
                        //      x2 = aux(j2)
                        //      y2 = aux(j2 + 1)
                        //      x3 = aux(j3)
                        //      y3 = aux(j3 + 1)
                        //42    call gauss(x1, y1, x2, y2, x3, y3, xc, yc, eta, teta, s, c, chi,
                        //     1v1, fck, gc, x, y, z, b, h, cs)
                        for (int j = 2; j <= nsp1; j++)
                        {
                            int j2 = 3 * (kvp[j] - 1) + 2;
                            int j3 = 3 * (kvp[j + 1] - 1) + 2;
                            x2 = aux[j2];
                            y2 = aux[j2 + 1];
                            x3 = aux[j3];
                            y3 = aux[j3 + 1];
                            gauss(ref x1, ref y1, ref x2, ref y2, ref x3, ref y3, ref xc, ref yc, ref eta, ref teta, ref s,
                                  ref c, ref chi, ref v1, ref fck, ref gc, ref x, ref y, ref z, ref b, ref h, ref cs, ref alfaCc);
                        }
                    }
                }

                //c
                //c ---cambio di partizione convessa
                //c
                //50    inpc = inpc + 1
                //      if (inpc.le.npc) goto 70
                //100   return
                //      end
                inpc = inpc + 1;
                if (!(inpc <= npc)) return;
            } while (true == true);
        }


        //c
        //c-------------------------------------------------------------------c
        //c Subroutine per determinare i vertici delle zone compresse c
        //c con tensione costante e parabolica                           c
        //c-------------------------------------------------------------------c
        //      subroutine zone(vc, nvc, ndvc, epsc, epst, w2, w0, kvc, kvp, ivp, ivc,
        //     1ndkvc, ndkvp, ic, it, s, c, iz)
        private void zone(ref double[] vc, ref int nvc, ref double epsc, ref double epst, ref double w2,
                          ref double w0, ref int[] kvc, ref int[] kvp, ref int ivp, ref int ivc,
                          ref int ic, ref int it, ref double s, ref double c, ref int iz)
        {
            double w1, x1, x2, y1, y2;
            //c
            //c---- descrizione delle variabili di output:
            //c KVP = rappresenta un vettore di numeri interi che contiene
            //c      l'ordine dei vertici della sezione che sono compressi
            //c con tensione parabolica.
            //c KVC = rappresenta un vettore di numeri interi che contiene
            //c      l'ordine dei vertici della sezione compressi con tensione
            //c costante.
            //c IVP = numero dei vertici di KVP
            //c IVC = numero dei vertici di KVC
            //c
            //      dimension vc(ndvc),kvc(ndkvc),kvp(ndkvp)
            //c
            //c ----scelta del tipo di suddivisione
            //c
            //      goto(200,20,30,50,40,60),iz
            if (iz == 1)
            {
                return;//200
            }
            else if (iz == 2)
            {
                //c
                //c ----sezione parzializzata con tensione solo parabolica
                //c
                //c
                //c ----zona con tensione parabolica
                //c
                //20    kvp(1) = ic
                //      n1 = ic - 1
                //      ivc = 0
                //      ivp = 1
                //      iw = nvc

                kvp[1] = ic;
                int n1 = ic - 1;
                int n2 = 0;
                ivc = 0;
                ivp = 1;
                int iw = nvc;

                bool canExit = true;
                do//231
                {
                    //231   ivp = ivp + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                              kvp(ivp) = n2
                    //                              goto 231
                    //      endif
                    canExit = true;
                    ivp = ivp + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvp[ivp] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      kvp(ivp) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                kvp[ivp] = iw;
                int jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//232
                {
                    canExit = true;
                    //232   n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)goto 232
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1) canExit = false;
                } while (!canExit);

                //      iw = iw + 1
                //      ivp = ivp + 1
                //      kvp(ivp) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                ivp = ivp + 1;
                kvp[ivp] = iw;
                jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//233
                {
                    //233   if (n2.eq.ic) goto 200
                    //      ivp = ivp + 1
                    //      kvp(ivp) = n2
                    //      n2 = n2 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      goto 233
                    if (n2 == ic) return;
                    ivp = ivp + 1;
                    kvp[ivp] = n2;
                    n2 = n2 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                } while (true == true);
            }
            else if (iz == 3)
            {
                //c ----sezione interamente compressa in modo parabolico
                //c
                //30    ivp = nvc
                //      do 31 j = 1,nvc
                //31    kvp(j) = j
                //      goto 200
                //c
                ivp = nvc;
                for (int j = 1; j <= nvc; j++)
                {
                    kvp[j] = j;
                }
                return;
            }
            else if (iz == 4)
            {
                //c ----sezione interamente compressa cost + parab
                //c
                //50    n1 = ic - 1
                //      ivc = 1
                //      ivp = 1
                //      iw = nvc
                int n1 = ic - 1;
                int n2 = 0;
                ivc = 1;
                ivp = 1;
                int iw = nvc;

                //c
                //c ----zona con tensione costante
                //c
                //      kvc(1) = ic
                kvc[1] = ic;

                bool canExit = false;
                do//211
                {
                    canExit = true;
                    //211   ivc = ivc + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                              kvc(ivc) = n2
                    //                              goto 211
                    //      endif
                    ivc = ivc + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvc[ivc] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      kvc(ivc) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                kvc[ivc] = iw;
                int jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                //c
                //c ----zona con tensione parabolica
                //c
                //      kvp(1) = iw
                //      ivp = ivp + 1
                //      kvp(ivp) = n2
                kvp[1] = iw;
                ivp = ivp + 1;
                kvp[ivp] = n2;

                do//212
                {
                    canExit = true;
                    //212   ivp = ivp + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                            kvp(ivp) = n2
                    //                              goto 212
                    //      endif
                    ivp = ivp + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvp[ivp] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      ivc = ivc + 1
                //      kvc(ivc) = iw
                //      kvp(ivp) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                ivc = ivc + 1;
                kvc[ivc] = iw;
                kvp[ivp] = iw;
                jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//213
                {
                    //213   if (n2.eq.ic) goto 200
                    //      ivc = ivc + 1
                    //      kvc(ivc) = n2
                    //      n2 = n2 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      goto 213
                    if (n2 == ic) return;
                    ivc = ivc + 1;
                    kvc[ivc] = n2;
                    n2 = n2 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                } while (true == true);
            }
            else if (iz == 5)
            {
                //c
                //c---- sezione interamente compressa in modo costante
                //c
                //40    ivc = nvc
                //      do 41 j = 1,nvc
                //41    kvc(j) = j
                //      goto 200
                //c

                ivc = nvc;
                for (int j = 1; j <= nvc; j++)
                {
                    kvc[j] = j;
                }
                return;
            }
            else if (iz == 6)
            {
                //c
                //c ----sezione parzializzata con zona a tensione costante
                //c
                //60    n1 = ic - 1
                //      ivc = 1
                //      ivp = 1
                //      iw = nvc
                int n1 = ic - 1;
                int n2 = 0;
                ivc = 1;
                ivp = 1;
                int iw = nvc;

                //c
                //c ----zona con tensione costante
                //c
                //      kvc(1) = ic
                kvc[1] = ic;

                bool canExit = false;
                do//221
                {
                    canExit = true;
                    //221   ivc = ivc + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                              kvc(ivc) = n2
                    //                              goto 221
                    //      endif

                    ivc = ivc + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvc[ivc] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      kvc(ivc) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                //      n1 = n1 - 1
                iw = iw + 1;
                kvc[ivc] = iw;
                int jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);
                n1 = n1 - 1;

                //c
                //c ----zona con tensione parabolica
                //c
                //      kvp(1) = iw
                kvp[1] = iw;

                do//222
                {
                    canExit = true;
                    //222   ivp = ivp + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                              kvp(ivp) = n2
                    //                              goto 222
                    //      endif
                    ivp = ivp + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvp[ivp] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      kvp(ivp) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                kvp[ivp] = iw;
                jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//223
                {
                    canExit = true;
                    //223   n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w0, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)goto 223


                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w0, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1) canExit = false;
                } while (!canExit);

                //      iw = iw + 1
                //      ivp = ivp + 1
                //      n1 = n1 - 1
                //      kvp(ivp) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                ivp = ivp + 1;
                n1 = n1 - 1;
                kvp[ivp] = iw;
                jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//322
                {
                    canExit = true;
                    //322   ivp = ivp + 1
                    //      n1 = n1 + 1
                    //      if (n1.gt.nvc) n1 = n1 - nvc
                    //      n2 = n1 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      j1 = 3 * (n1 - 1) + 2
                    //      j2 = 3 * (n2 - 1) + 2
                    //      x1 = vc(j1)
                    //      y1 = vc(j1 + 1)
                    //      x2 = vc(j2)
                    //      y2 = vc(j2 + 1)
                    //      w1 = dlam(s, c, w2, x1, y1, x2, y2)
                    //      if (w1.lt.0..or.w1.ge.1.)then
                    //                              kvp(ivp) = n2
                    //                              goto 322
                    //      endif
                    ivp = ivp + 1;
                    n1 = n1 + 1;
                    if (n1 > nvc) n1 = n1 - nvc;
                    n2 = n1 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                    int j1 = 3 * (n1 - 1) + 2;
                    int j2 = 3 * (n2 - 1) + 2;
                    x1 = vc[j1];
                    y1 = vc[j1 + 1];
                    x2 = vc[j2];
                    y2 = vc[j2 + 1];
                    w1 = dlam(s, c, w2, x1, y1, x2, y2);
                    if (w1 < 0 || w1 >= 1)
                    {
                        kvp[ivp] = n2;
                        canExit = false;
                    }
                } while (!canExit);

                //      iw = iw + 1
                //      kvp(ivp) = iw
                //      ivc = ivc + 1
                //      kvc(ivc) = iw
                //      jw = 3 * (iw - 1) + 2
                //      vc(jw) = x1 + w1 * (x2 - x1)
                //      vc(jw + 1) = y1 + w1 * (y2 - y1)
                iw = iw + 1;
                kvp[ivp] = iw;
                ivc = ivc + 1;
                kvc[ivc] = iw;
                jw = 3 * (iw - 1) + 2;
                vc[jw] = x1 + w1 * (x2 - x1);
                vc[jw + 1] = y1 + w1 * (y2 - y1);

                do//224
                {
                    //224   if (n2.eq.ic) goto 200
                    //      ivc = ivc + 1
                    //      kvc(ivc) = n2
                    //      n2 = n2 + 1
                    //      if (n2.gt.nvc) n2 = n2 - nvc
                    //      goto 224
                    if (n2 == ic) return;
                    ivc = ivc + 1;
                    kvc[ivc] = n2;
                    n2 = n2 + 1;
                    if (n2 > nvc) n2 = n2 - nvc;
                } while (true == true);
            }
            else
            {
                throw new NotSupportedException("Zona non conosciuta");
            }
            //200  return
            //      end
        }

        //c
        //c ----function per determinare l'intersezione di un segmento
        //c con una retta.
        //c
        //      function dlam(a, b, c, xi, yi, xj, yj)
        private double dlam(double a, double b, double c, double xi, double yi, double xj, double yj)
        {
            double dlam, w1, w2, w3;
            //      w1 = a * xi - b * yi + c
            //      w3 = a * xj - b * yj + c
            //      w2 = -a * (xj - xi) + b * (yj - yi)
            w1 = a * xi - b * yi + c;
            w3 = a * xj - b * yj + c;
            w2 = -a * (xj - xi) + b * (yj - yi);

            //      if (dabs(w1).lt.0.001)then
            //                           dlam = 0.001
            //                          return
            //      endif
            if (System.Math.Abs(w1) < 0.001)
            {
                dlam = 0.001;
                return dlam;
            }

            //      if (dabs(w3).lt.0.001)then
            //                           dlam = 1.001
            //                          return
            //      endif
            if (System.Math.Abs(w3) < 0.001)
            {
                dlam = 1.001;
                return dlam;
            }

            //      if (dabs(w2).lt.0.001)then
            //                           dlam = 3.
            //                          return
            //      endif
            if (System.Math.Abs(w2) < 0.001)
            {
                dlam = 3;
                return dlam;
            }

            //      dlam = w1 / w2
            //      return
            //      end
            dlam = w1 / w2;
            return dlam;
        }


        //c---------------------------------------------------------------- - c
        //c SUBROUTINE PER IL CALCOLO INTEGRALE DELLE TENSIONI         c
        //c      VARIABILI PARABOLICAMENTE SU UN DOMINIO TRIANGOLARE (cls.) c
        //c---------------------------------------------------------------- - c

        // subroutine gauss(x1, y1, x2, y2, x3, y3, xc, yc, eta, teta, steta, cteta,
        //1chi, v1, fck, gc, x, y, z, b, h, cs)
        private void gauss(ref double x1, ref double y1, ref double x2, ref double y2, ref double x3, ref double y3,
                           ref double xc, ref double yc, ref double eta, ref double teta, ref double steta, ref double cteta,
                            ref double chi, ref double v1, ref double fck, ref double gc, ref double x, ref double y,
                            ref double z, ref double b, ref double h, ref double cs, ref double alfaCc)
        {
            double wx, wy, wz, a, w1, w2, w3, xs, ys, eps, fs, w5, w4;

            // wx = 0.
            // wy = 0.
            // wz = 0.
            wx = 0;
            wy = 0;
            wz = 0;

            //c
            //c---- area del triangolo
            //c

            // a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1))

            // a = a / b / h * cs
            a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1));

            a = a / b / h * cs;

            //c---- primo punto semplice

            // w1 = x2 - x1

            // w2 = x3 - x1

            // w3 = y2 - y1

            // w4 = y3 - y1

            // xs = x1 + w1 / 3.+ w2 / 3.
            // ys = y1 + w3 / 3.+ w4 / 3.
            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

            // fs = dsgmac(fck, gc, eps)

            // w5 = fs * (-0.56250)

            // wx = wx - w5 * ys / h

            // wy = wy + w5 * xs / b

            // wz = wz + w5
            w1 = x2 - x1;
            w2 = x3 - x1;
            w3 = y2 - y1;
            w4 = y3 - y1;
            xs = x1 + w1 / 3 + w2 / 3;
            ys = y1 + w3 / 3 + w4 / 3;
            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
            fs = dsgmac(fck, gc, eps, alfaCc);
            w5 = fs * (-0.56250);
            wx = wx - w5 * ys / h;
            wy = wy + w5 * xs / b;
            wz = wz + w5;

            //c---- secondo punto semplice

            // xs = x1 + w1 * 0.2 + w2 * 0.2

            // ys = y1 + w3 * 0.2 + w4 * 0.2

            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

            // fs = dsgmac(fck, gc, eps)

            // w5 = fs * 0.52083333

            // wx = wx - w5 * ys / h

            // wy = wy + w5 * xs / b

            // wz = wz + w5
            xs = x1 + w1 * 0.2 + w2 * 0.2;
            ys = y1 + w3 * 0.2 + w4 * 0.2;
            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
            fs = dsgmac(fck, gc, eps, alfaCc);
            w5 = fs * 0.52083333;
            wx = wx - w5 * ys / h;
            wy = wy + w5 * xs / b;
            wz = wz + w5;

            //c---- terzo punto semplice

            // xs = x1 + w1 * 0.6 + w2 * 0.2

            // ys = y1 + w3 * 0.6 + w4 * 0.2

            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

            // fs = dsgmac(fck, gc, eps)

            // w5 = fs * 0.52083333

            // wx = wx - w5 * ys / h

            // wy = wy + w5 * xs / b

            // wz = wz + w5
            xs = x1 + w1 * 0.6 + w2 * 0.2;
            ys = y1 + w3 * 0.6 + w4 * 0.2;
            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
            fs = dsgmac(fck, gc, eps, alfaCc);
            w5 = fs * 0.52083333;
            wx = wx - w5 * ys / h;
            wy = wy + w5 * xs / b;
            wz = wz + w5;

            //c---- quarto punto semplice

            // xs = x1 + w1 * 0.2 + w2 * 0.6

            // ys = y1 + w3 * 0.2 + w4 * 0.6

            // eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys)

            // fs = dsgmac(fck, gc, eps)

            // w5 = fs * 0.52083333

            // wx = wx - w5 * ys / h

            // wy = wy + w5 * xs / b

            // wz = wz + w5
            xs = x1 + w1 * 0.2 + w2 * 0.6;
            ys = y1 + w3 * 0.2 + w4 * 0.6;
            eps = depsil(xc, yc, steta, cteta, chi, v1, xs, ys);
            fs = dsgmac(fck, gc, eps, alfaCc);
            w5 = fs * 0.52083333;
            wx = wx - w5 * ys / h;
            wy = wy + w5 * xs / b;
            wz = wz + w5;

            //c---- complessivamente:
            // x = wx * a + x

            // y = wy * a + y

            // z = wz * a + z
            //      return
            //      end
            x = wx * a + x;
            y = wy * a + y;
            z = wz * a + z;
        }




        //c---------------------------------------------------------------- - c
        //c SUBROUTINE PER IL CALCOLO INTEGRALE DELLE TENSIONI         c
        //c      COSTANTI SU UN DOMINIO TRIANGOLARE(calcestruzzo)        c
        //c-----------------------------------------------------------------c
        //      subroutine cost(x1, y1, x2, y2, x3, y3, fck, gc, x, y, z, b, h, cs)
        private void cost(ref double x1, ref double y1, ref double x2, ref double y2, ref double x3, ref double y3,
                          ref double fck, ref double gc, ref double x, ref double y, ref double z, ref double b, ref double h,
                          ref double cs, ref double alfaCc)
        {
            double a, xg, yg, w1;
            //c
            //c---- area del triangolo
            //c
            //      a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1))
            //      a = a / b / h * cs
            a = 0.5 * ((y3 + y1) * (x3 - x1) - (y3 + y2) * (x3 - x2) - (y2 + y1) * (x2 - x1));
            a = a / b / h * cs;

            //c
            //c---- baricentro del triangolo
            //c
            //      xg = (x1 + x2 + x3) / 3.
            //      yg = (y1 + y2 + y3) / 3.
            xg = (x1 + x2 + x3) / 3.0;
            yg = (y1 + y2 + y3) / 3.0;

            //c
            //c---- tensione di compressione
            //c
            //      w1 = 0.85 / gc
            w1 = alfaCc / gc;

            //c---- complessivamente:
            //      z = w1 * a + z
            //      y = w1 * a * xg / b + y
            //      x = -w1 * a * yg / h + x
            //      return
            //      end
            z = w1 * a + z;
            y = w1 * a * xg / b + y;
            x = -w1 * a * yg / h + x;
        }



        //c
        //c------------------------------------------------------------------c
        //c FUNCTION  DI UTILITA'                                  c
        //c------------------------------------------------------------------c
        //c
        //c
        //c ----function per il calcolo delle deformazioni
        //c
        //      function depsil(xc, yc, steta, cteta, chi, v1, xi, yi)
        private double depsil(double xc, double yc, double steta, double cteta, double chi, double v1, double xi, double yi)
        {
            double di, depsil;
            //      di = (yi - yc) * cteta - (xi - xc) * steta
            //      depsil = v1 - chi * di
            //      return
            //      end
            di = (yi - yc) * cteta - (xi - xc) * steta;
            depsil = v1 - chi * di;
            return depsil;
            //      end
        }

        //c
        //c ----function per il calcolo delle tensioni nell'acciaio
        //c
        //      function dsgmaf(fys, gs, eps, e, fck, gc)
        private double dsgmaf(double fys, double gs, double eps, double e, double fck, double gc, double sigma0)
        {
            double b, a, dsgmaf;
            //      b = fys / gs / fck
            //      a = e * eps / 1000./ fck
            //      dsgmaf = a
            //      if (dabs(a).gt.b) dsgmaf = b * dabs(a) / a
            //      return
            //      end
            b = fys / gs / fck;
            a = e * eps / 1000.0 / fck + sigma0 / fck;
            dsgmaf = a;
            if (System.Math.Abs(a) > b) dsgmaf = b * System.Math.Abs(a) / a;
            return dsgmaf;
        }


        //c
        //c ----function per il calcolo delle tensioni nel calcestruzzo
        //c
        //      function dsgmac(fck, gc, eps)
        private double dsgmac(double fck, double gc, double eps, double alfaCc)
        {
            double dsgmac;
            //      dsgmac = 0.85 / gc
            //      if (eps + 2.) 100,100,50
            //50    dsgmac = -0.85 / gc * (eps + eps * *2 / 4.)
            //      if (eps.ge.0)dsgmac = 0.
            //100   return
            //      end
            dsgmac = alfaCc / gc;
            if (eps + 2 <= 0)
            {
                return dsgmac;
            }
            else
            {
                dsgmac = -alfaCc / gc * (eps + System.Math.Pow(eps, 2) / 4.0);
                if (eps >= 0) dsgmac = 0;
                return dsgmac;
            }
        }

        //c
        //c
        //c
        //c  routine per l'integrazione lineare di una funzione cubica
        //c con il metodo dei punti di Gauss. (acciaio)
        //c
        //      subroutine ilgs(xa, ya, ea, xb, yb, eb, nel, aux, ndaux, x, y, z,
        //     1xc, yc, steta, cteta, chi, v1, fys, gs, e, fck, gc, steel, b, h)
        //      double precision xa,ya,ea,xb,yb,eb,aux,x,y,z,xc,yc,steta,
        //     1cteta,chi,v1,fys,gs,e,fck,gc,steel,b,h,w11,w22,w0,x1,x2,y1,y2,
        //     1e1,sx,sy,wx,wy,wz,w1
        //      double precision dsgmaf,depsil
        //      dimension aux(ndaux)
        //c
        //c ----descrizione delle variabili:
        //c
        //c    XA = ascissa del primo punto iniziale (con epsilon minore)
        //c YA = ordinata    "            "            "
        //c EA = epsilon al punto A
        //c    XB = ascissa del secondo punto(con epsilon maggiore)
        //c YB = ordinata    "            "            "
        //c EB = epsilon al punto B
        //c    NEL = numero delle parti separate del diagramma costitutivo
        //c STEEL = superficie lineare totale
        //c AUX = vettore ausiliario di dimensione ndaux contenente i valori
        //c        di epsilon dividenti il diagramma costitutivo.
        //c X = caratteristica di sollecitazione flettente secondo X
        //c    Y = "                  "               "         "    Y
        //c    Z = "                  "            normale
        //c    N.B.: le altre variabili sono analoghe alla LIMQ
        //c
        private void ilgs(ref double xa, ref double ya, ref double ea, ref double xb, ref double yb, ref double eb,
                          ref int nel, ref double[] aux, ref double x, ref double y, ref double z, ref double xc,
                          ref double yc, ref double steta, ref double cteta, ref double chi, ref double v1, ref double fys,
                          ref double gs, ref double e, ref double fck, ref double gc, ref double steel, ref double b, ref double h)
        {
            double w11, w22, w0, x1, x2, y1, y2, e1, sx, sy, wx, wy, wz, w1;

            //      w11 = xa
            //      w22 = ya
            //      i = 1
            w11 = xa;
            w22 = ya;
            int i = 1;
            do
            {
                //20    e1 = aux(i)
                e1 = aux[i];
                //      if (e1.lt.ea) goto 30
                if (!(e1 < ea))
                {
                    //      if (e1.ge.eb) then
                    //                    x1 = xa
                    //                  y1 = ya
                    //                  x2 = xb
                    //                  y2 = yb
                    //                  goto 10
                    //      endif
                    //      x1 = xa
                    //      y1 = ya
                    //      x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea)
                    //      y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea)
                    if (e1 >= eb)
                    {
                        x1 = xa;
                        y1 = ya;
                        x2 = xb;
                        y2 = yb;
                    }
                    else
                    {
                        x1 = xa;
                        y1 = ya;
                        x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea);
                        y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea);
                    }

                    //10    w0 = dsqrt((x2 - x1) * *2 + (y1 - y2) * *2) / dsqrt((w11 - xb) * *2 + (w22 - yb) * *2)
                    //      w0 = w0 * steel / 2.
                    w0 = System.Math.Sqrt(System.Math.Pow(x2 - x1, 2) + System.Math.Pow(y1 - y2, 2)) /
                         System.Math.Sqrt(System.Math.Pow(w11 - xb, 2) + System.Math.Pow(w22 - yb, 2));
                    w0 = w0 * steel / 2.0;

                    //c
                    //c---- primo punto semplice
                    //c
                    //      sx = x1 + (1.- 0.77459) / 2.* (x2 - x1)
                    //      sy = y1 + (1.- 0.77459) / 2.* (y2 - y1)
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.555555
                    //      wx = w1 * sy / b / h / h
                    //      wy = -w1 * sx / b / h / b
                    //      wz = -w1 / b / h
                    sx = x1 + (1 - 0.77459) / 2.0 * (x2 - x1);
                    sy = y1 + (1 - 0.77459) / 2.0 * (y2 - y1);
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.555555;
                    wx = w1 * sy / b / h / h;
                    wy = -w1 * sx / b / h / b;
                    wz = -w1 / b / h;

                    //c
                    //c ----secondo punto semplice
                    //c
                    //      sx = (x1 + x2) / 2.
                    //      sy = (y1 + y2) / 2.
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.88888
                    //      wx = w1 * sy / b / h / h + wx
                    //      wy = -w1 * sx / b / h / b + wy
                    //      wz = -w1 / b / h + wz
                    sx = (x1 + x2) / 2.0;
                    sy = (y1 + y2) / 2.0;
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.88888;
                    wx = w1 * sy / b / h / h + wx;
                    wy = -w1 * sx / b / h / b + wy;
                    wz = -w1 / b / h + wz;

                    //c
                    //c ----terzo punto semplice
                    //c
                    //      sx = x1 + 1.77459 / 2.* (x2 - x1)
                    //      sy = y1 + 1.77459 / 2.* (y2 - y1)
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmaf(fys, gs, w1, e, fck, gc) * w0 * 0.555555
                    //      x = w1 * sy / b / h / h + wx + x
                    //      y = -w1 * sx / b / h / b + wy + y
                    //      z = -w1 / b / h + wz + z
                    sx = x1 + 1.77459 / 2 * (x2 - x1);
                    sy = y1 + 1.77459 / 2 * (y2 - y1);
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmaf(fys, gs, w1, e, fck, gc, 0) * w0 * 0.555555;
                    x = w1 * sy / b / h / h + wx + x;
                    y = -w1 * sx / b / h / b + wy + y;
                    z = -w1 / b / h + wz + z;

                    //c
                    //c ----fine dell'integrazione
                    //c
                    //      if (((xb - x2) * *2 + (yb - y2) * *2).lt.0.001)goto 100
                    //      xa = x2
                    //      ya = y2
                    if (System.Math.Pow(xb - x2, 2) + System.Math.Pow(yb - y2, 2) < 0.001) break;
                    xa = x2;
                    ya = y2;
                }

                //30    i = i + 1
                //      if (i.gt.nel) goto 100
                i++;
                if (i > nel) break;

                //      goto 20
            } while (true == true);

            //100   xa = w11
            //      ya = w22
            //      return
            //      end
            xa = w11;
            ya = w22;
        }


        //c
        //c
        //c routine per l'integrazione lineare di una funzione cubica
        //c con il metodo dei punti di Gauss. (calcestruzzo)
        //c
        //      subroutine ilgc(xa, ya, ea, xb, yb, eb, nel, aux, ndaux, x, y, z,
        //     1xc, yc, steta, cteta, chi, v1, fck, gc, cls, b, h)
        //c
        //c ----descrizione delle variabili:
        //c
        //c    XA = ascissa del primo punto iniziale (con epsilon minore)
        //c YA = ordinata    "            "            "
        //c EA = epsilon al punto A
        //c    XB = ascissa del secondo punto(con epsilon maggiore)
        //c YB = ordinata    "            "            "
        //c EB = epsilon al punto B
        //c    NEL = numero delle parti separate del diagramma costitutivo
        //c CLS = superficie da togliere
        //c AUX = vettore ausiliario di dimensione ndaux contenente i valori
        //c        di epsilon dividenti il diagramma costitutivo.
        //c X = caratteristica di sollecitazione flettente secondo X
        //c    Y = "                  "               "         "    Y
        //c    Z = "                  "            normale
        //c    N.B.: le altre variabili sono analoghe alla LIMQ
        //c
        private void ilgc(ref double xa,
                          ref double ya,
                          ref double ea,
                          ref double xb,
                          ref double yb,
                          ref double eb,
                          ref int nel,
                          ref double[] aux,
                          ref double x,
                          ref double y,
                          ref double z,
                          ref double xc,
                          ref double yc,
                          ref double steta,
                          ref double cteta,
                          ref double chi,
                          ref double v1,
                          ref double fck,
                          ref double gc,
                          ref double cls,
                          ref double b,
                          ref double h,
                          ref double alfaCc)
        {
            double w11, w22, w0, x1, x2, y1, y2, e1, sx, sy, wx, wy, wz, w1;
            w11 = xa;
            w22 = ya;
            int i = 1;
            do
            {
                //20    e1 = aux(i)
                e1 = aux[i];

                //      if (e1.le.ea) goto 30
                if (!(e1 <= ea))
                {
                    //      if (e1.ge.eb) then
                    //                    x1 = xa
                    //                  y1 = ya
                    //                  x2 = xb
                    //                  y2 = yb
                    //                  goto 10
                    //      endif
                    //      x1 = xa
                    //      y1 = ya
                    //      x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea)
                    //      y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea)
                    if (e1 >= eb)
                    {
                        x1 = xa;
                        y1 = ya;
                        x2 = xb;
                        y2 = yb;
                    }
                    else
                    {
                        x1 = xa;
                        y1 = ya;
                        x2 = xa + (xb - xa) * (e1 - ea) / (eb - ea);
                        y2 = ya + (yb - ya) * (e1 - ea) / (eb - ea);
                    }

                    //10    w0 = dsqrt((x2 - x1) * *2 + (y1 - y2) * *2) / dsqrt((w11 - xb) * *2 + (w22 - yb) * *2)
                    //      w0 = w0 * cls / 2.
                    w0 = System.Math.Sqrt(System.Math.Pow(x2 - x1, 2) + System.Math.Pow(y1 - y2, 2)) /
                         System.Math.Sqrt(System.Math.Pow(w11 - xb, 2) + System.Math.Pow(w22 - yb, 2));
                    w0 = w0 * cls / 2;

                    //c
                    //c---- primo punto semplice
                    //c
                    //      sx = x1 + (1.- 0.77459) / 2.* (x2 - x1)
                    //      sy = y1 + (1.- 0.77459) / 2.* (y2 - y1)
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.555555
                    //      wx = w1 * sy / b / h / h
                    //      wy = -w1 * sx / b / h / b
                    //      wz = -w1 / b / h
                    sx = x1 + (1 - 0.77459) / 2.0 * (x2 - x1);
                    sy = y1 + (1 - 0.77459) / 2.0 * (y2 - y1);
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.555555;
                    wx = w1 * sy / b / h / h;
                    wy = -w1 * sx / b / h / b;
                    wz = -w1 / b / h;

                    //c
                    //c ----secondo punto semplice
                    //c
                    //      sx = (x1 + x2) / 2.
                    //      sy = (y1 + y2) / 2.
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.88888
                    //      wx = w1 * sy / b / h / h + wx
                    //      wy = -w1 * sx / b / h / b + wy
                    //      wz = -w1 / b / h + wz
                    sx = (x1 + x2) / 2.0;
                    sy = (y1 + y2) / 2.0;
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.88888;
                    wx = w1 * sy / b / h / h + wx;
                    wy = -w1 * sx / b / h / b + wy;
                    wz = -w1 / b / h + wz;

                    //c
                    //c ----terzo punto semplice
                    //c
                    //      sx = x1 + 1.77459 / 2.* (x2 - x1)
                    //      sy = y1 + 1.77459 / 2.* (y2 - y1)
                    //      w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy)
                    //      w1 = dsgmac(fck, gc, w1) * w0 * 0.555555
                    //      x = w1 * sy / b / h / h + wx + x
                    //      y = -w1 * sx / b / h / b + wy + y
                    //      z = -w1 / b / h + wz + z
                    sx = x1 + 1.77459 / 2.0 * (x2 - x1);
                    sy = y1 + 1.77459 / 2.0 * (y2 - y1);
                    w1 = depsil(xc, yc, steta, cteta, chi, v1, sx, sy);
                    w1 = dsgmac(fck, gc, w1, alfaCc) * w0 * 0.555555;
                    x = w1 * sy / b / h / h + wx + x;
                    y = -w1 * sx / b / h / b + wy + y;
                    z = -w1 / b / h + wz + z;

                    //c
                    //c ----fine dell'integrazione
                    //c
                    //      if (((xb - x2) * *2 + (yb - y2) * *2).lt.0.001)goto 100
                    //      xa = x2
                    //      ya = y2
                    if (System.Math.Pow(xb - x2, 2) + System.Math.Pow(yb - y2, 2) < 0.001) return;
                    xa = x2;
                    ya = y2;
                }
                //30    i = i + 1
                //      if (i.gt.nel) goto 100
                i++;
                if (i > nel) return;
                //      goto 20
            } while (true == true);
            //100   return
            //      end
        }

        #endregion

        #region Limit point

        //c
        //c
        //c PROGRAMMA  PER LA RICERCA DELL’INTERSEZIONE DI UN VETTORE
        //c CON UNA SUPERFICIE LIMITE GENERICA
        //c
        //c
        //      dimension vst(12),ise(12)
        //      data ise, vst/ 1,2,1,3,2,4,3,5,4,6,5,6,3.1415,10 * 0.0,3.1415 /
        //    c Descrizione delle variabili
        //    c vst – vettore con valori per il cambio di campo di rottura, 12 elementi così composti:
        //            c    3.1415  0.0  0.0  0.0  0.0  0.0  0.0  0.0  0.0  0.0 0.0 3.1415
        //c ise – vettore con valori per il cambio di campo di rottura, 12 elementi così composti:
        //            c   1  2  1  3  2  4  3  5  4  6  5  6
        //c
        //c    b, h parametri di adimensionalizzazione dei valori limite
        //c   eta0 – parametro iniziale eta
        //c   teta0 – parametro iniziale teta
        //c   ic0 – parametro iniziale per campo di rottura
        //c eta – parametro eta
        //c teta – parametro teta
        //c ic – parametro per campo di rottura
        //c deta – incremento per derivazione numerica(0.05)
        //c dteta – incremento per derivazione numerica(0.05)
        //c err  -errore di linearità da considerare nell’incremento delle variabili(0.1)
        //c dumax -massimo valore ammesso per il primo parametro
        //c dvmax – massimo valore ammesso per il secondo parametro
        //c   nite – numero iterazioni
        //c vl, vm, vn   -versore del vettore di verifica uscente dall’origine e passante per PEd
        //c   xd, yd, zd – Punto di verifica PEd
        //c

        private void GetLimitPointViviani(double eta0, double teta0, int ic0,
                                          double[] vc, double[] vs, double[] vsl, int nvc, int nvs, int nvsl,
                                          double fysl, double fck, double gs, double gsl, double gc,
                                          double esl, double b, double h, double[] vse, int[] ipc,
                                          int npc, double xd, double yd, double zd, double dumax, double dvmax,
                                          double epsCMin, double fiTensAASHTO, double fiCompAASHTO, bool useLimit34AASHTO, double alfaCC,   //added for AASHTO
                                          double epsSu,
                                          out double xi, out double yi, out double zi, out double teta, out double eta, out int ic)
        {
            const int MAX_ITERS = 50;
            int[] ise = { 0, 1, 2, 1, 3, 2, 4, 3, 5, 4, 6, 5, 6 };//added initial 0
            double[] vst = { 0, 3.1415, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 3.1415 };//added initial 0
            int nite;
            double eta1, deta, dteta, teta1;
            Point3d dirV;
            double vl, vm, vn, err;

            //c
            //c METODO DI RISOLUZIONE
            //c
            //c
            //c----calcolo delle caratteristiche ultime del punto Pi
            //c
            nite = 0;
            eta = eta0;
            teta = teta0;
            ic = ic0;
            deta = 0.05;
            dteta = 0.05;
            err = 0.1;
            
            dirV = Geom.Direction(new Point3d(xd, yd, zd));
            vl = dirV.X;
            vm = dirV.Y;
            vn = dirV.Z;

            do//55
            {
                //55    call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
                //     1gs,gsl,gc,eta,teta,e,xi,yi,zi,b,h,kvc,kvp,ndkvc,ndkvp,
                //     1ipc,ndpc,npc,aux,ndaux)

                //nite = nite + 1
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta, esl, out xi, out yi, out zi,
                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);
                nite = nite + 1;
                if (nite > MAX_ITERS) throw new NotSupportedException("GetLimitPointViviani not converged");

                //c
                //c ----calcolo delle derivate parziali
                //c
                // eta1 = eta - deta
                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
                //1gs,gsl,gc,eta1,teta,e,xiie,yiie,ziie,b,h,kvc,kvp,ndkvc,ndkvp,
                //1ipc,ndpc,npc,aux,ndaux)
                // eta1 = eta + deta
                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
                //1gs,gsl,gc,eta1,teta,e,xise,yise,zise,b,h,kvc,kvp,ndkvc,ndkvp,
                //1ipc,ndpc,npc,aux,ndaux)
                // dxe = (xise - xiie) / deta / 2.
                // dye = (yise - yiie) / deta / 2.
                // dze = (zise - ziie) / deta / 2.
                // teta1 = teta - dteta
                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
                //1gs,gsl,gc,eta,teta1,e,xiit,yiit,ziit,b,h,kvc,kvp,ndkvc,ndkvp,
                //1ipc,ndpc,npc,aux,ndaux)
                // teta1 = teta + dteta
                // call limq(vc, ndvc, vs, ndvs, vsl, ndvsl, nvc, nvs, nvsl, ic, fys, fysl, fck,
                //1gs,gsl,gc,eta,teta1,e,xist,yist,zist,b,h,kvc,kvp,ndkvc,ndkvp,
                //1ipc,ndpc,npc,aux,ndaux)
                // dxt = (xist - xiit) / dteta / 2.
                // dyt = (yist - yiit) / dteta / 2.
                // dzt = (zist - ziit) / dteta / 2
                double xiie, yiie, ziie, xise, yise, zise, dxe, dye, dze, xiit, yiit, ziit, xist, yist, zist, dxt, dyt, dzt;

                eta1 = eta - deta;
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta1, teta, esl, out xiie, out yiie, out ziie,
                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

                eta1 = eta + deta;
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta1, teta, esl, out xise, out yise, out zise,
                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

                dxe = (xise - xiie) / deta / 2.0;
                dye = (yise - yiie) / deta / 2.0;
                dze = (zise - ziie) / deta / 2.0;

                teta1 = teta - dteta;
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta1, esl, out xiit, out yiit, out ziit,
                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

                teta1 = teta + dteta;
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gs, gsl, gc, eta, teta1, esl, out xist, out yist, out zist,
                                               b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSu);

                dxt = (xist - xiit) / dteta / 2.0;
                dyt = (yist - yiit) / dteta / 2.0;
                dzt = (zist - ziit) / dteta / 2.0;

                //c
                //c ----calcolo del vettore parallelo al gradiente
                //c      della funzione di vincolo
                //c
                //      vj1 = (dye * dzt - dze * dyt)
                //      vj2 = (dxt * dze - dxe * dzt)
                //      vj3 = (dxe * dyt - dye * dxt)
                //      w1 = xi - xd
                //      w2 = yi - yd
                //      w3 = zi - zd
                //      waux = vj1 * vl + vj2 * vm + vj3 * vn
                //      if (dabs(waux).lt.0.00001)waux = 0.0001
                //      du = vn * (w2 * dxt - w1 * dyt) + vm * (w1 * dzt - w3 * dxt) + vl * (w3 * dyt - w2 * dzt)
                //      du = du / waux
                //      dv = vn * (w1 * dye - w2 * dxe) + vm * (w3 * dxe - w1 * dze) + vl * (w2 * dze - w3 * dye)
                //      dv = dv / waux
                //      waux = dnorma(vj1, vj2, vj3)
                //      u2 = vj2 / waux
                //      u3 = vj3 / waux
                //      u1 = vj1 / waux

                double vj1, vj2, vj3, w1, w2, w3, waux, du, dv, u1, u2, u3;

                vj1 = dye * dzt - dze * dyt;
                vj2 = dxt * dze - dxe * dzt;
                vj3 = dxe * dyt - dye * dxt;
                w1 = xi - xd;
                w2 = yi - yd;
                w3 = zi - zd;
                waux = vj1 * vl + vj2 * vm + vj3 * vn;
                if (System.Math.Abs(waux) < 0.00001) waux = 0.0001;
                du = vn * (w2 * dxt - w1 * dyt) + vm * (w1 * dzt - w3 * dxt) + vl * (w3 * dyt - w2 * dzt);
                du = du / waux;
                dv = vn * (w1 * dye - w2 * dxe) + vm * (w3 * dxe - w1 * dze) + vl * (w2 * dze - w3 * dye);
                dv = dv / waux;
                waux = dnorma(vj1, vj2, vj3);
                u2 = vj2 / waux;
                u3 = vj3 / waux;
                u1 = vj1 / waux;

                //c
                //c ----calcolo della distanza
                //c
                //      waux = dnorma(w1, w2, w3) * *2 - dscal(w1, w2, w3, vl, vm, vn) * *2
                //      vns = dsqrt(dabs(waux))
                //      s1 = xi + dxe * du + dxt * dv
                //      s2 = yi + dye * du + dyt * dv
                //      s3 = zi + dze * du + dzt * dv
                double vns, s1, s2, s3;

                waux = System.Math.Pow(dnorma(w1, w2, w3), 2.0) - System.Math.Pow(dscal(w1, w2, w3, vl, vm, vn), 2.0);
                vns = System.Math.Sqrt(System.Math.Abs(waux));
                s1 = xi + dxe * du + dxt * dv;
                s2 = yi + dye * du + dyt * dv;
                s3 = zi + dze * du + dzt * dv;

                //c
                //c ----controllo dell'orientamento del vettore DU-DV
                //c
                //      s1 = s1 - xd
                //      s2 = s2 - yd
                //      s3 = s3 - zd
                //      vss = dscal(s1, s2, s3, vl, vm, vn)
                //      if (dabs(vss).lt.0.00001)goto 208
                //      vss = vss / dabs(vss)
                //      du = du * vss
                //      dv = dv * vss
                double vss;

                s1 = s1 - xd;
                s2 = s2 - yd;
                s3 = s3 - zd;
                vss = dscal(s1, s2, s3, vl, vm, vn);
                if (System.Math.Abs(vss) >= 0.00001)//goto208
                {
                    vss = vss / System.Math.Abs(vss);
                    du = du * vss;
                    dv = dv * vss;
                }

                //c
                //c ----controllo di convergenza
                //c
                //208   if ((dabs(du) + dabs(dv)).lt.1.d - 6)goto 67
                if ((System.Math.Abs(du) + System.Math.Abs(dv)) < 1e-6) return;

                //c
                //c ----calcolo del massimo errore di linearita`
                //c
                //      eu = dabs((xiie + xise) / 2.- xi)
                //      if (dabs(eu).lt.0.0001)eu = 0.0001
                //      eu = dsqrt(err * dabs(xi) / eu)
                //      ev = dabs((xiit + xist) / 2.- xi)
                //      if (dabs(ev).lt.0.0001)ev = 0.0001
                //      ev = dsqrt(err * dabs(xi) / ev)
                //      eu1 = dabs((yiie + yise) / 2.- yi)
                //      if (dabs(eu1).lt.0.0001)eu1 = 0.0001
                //      eu1 = dsqrt(err * dabs(yi) / eu1)
                //      ev1 = dabs((yiit + yist) / 2.- yi)
                //      if (dabs(ev1).lt.0.0001)ev1 = 0.0001
                //      ev1 = dsqrt(err * dabs(yi) / ev1)
                //      if (eu1.gt.eu) eu = eu1
                //      if (ev1.gt.ev) ev = ev1
                //      eu1 = dabs((ziie + zise) / 2.- zi)
                //      if (dabs(eu1).lt.0.0001)eu1 = 0.0001
                //      eu1 = dsqrt(err * dabs(zi) / eu1)
                //      ev1 = dabs((ziit + zist) / 2.- zi)
                //      if (dabs(ev1).lt.0.0001)ev1 = 0.0001
                //      ev1 = dsqrt(err * dabs(zi) / ev1)
                //      if (eu1.gt.eu) eu = eu1
                //      if (ev1.gt.ev) ev = ev1
                //      au = 0.00001
                //      if (dabs(du).gt.0.)au = dabs(du) / deta
                //      av = 0.00001
                //      if (dabs(dv).gt.0.)av = dabs(dv) / dteta
                //      cl = 1.
                //      if (cl.gt.eu / au) cl = eu / au
                //      if (cl.gt.ev / av) cl = ev / av
                //      du = cl * du
                //      dv = cl * dv
                double eu, ev, eu1, ev1, au, av, cl;

                eu = System.Math.Abs((xiie + xise) / 2.0 - xi);
                if (System.Math.Abs(eu) < 0.0001) eu = 0.0001;
                eu = System.Math.Sqrt(err * System.Math.Abs(xi) / eu);
                ev = System.Math.Abs((xiit + xist) / 2.0 - xi);
                if (System.Math.Abs(ev) < 0.0001) ev = 0.0001;
                ev = System.Math.Sqrt(err * System.Math.Abs(xi) / ev);
                eu1 = System.Math.Abs((yiie + yise) / 2.0 - yi);
                if (System.Math.Abs(eu1) < 0.0001) eu1 = 0.0001;
                eu1 = System.Math.Sqrt(err * System.Math.Abs(yi) / eu1);
                ev1 = System.Math.Abs((yiit + yist) / 2.0 - yi);
                if (System.Math.Abs(ev1) < 0.0001) ev1 = 0.0001;
                ev1 = System.Math.Sqrt(err * System.Math.Abs(yi) / ev1);
                if (eu1 > eu) eu = eu1;
                if (ev1 > ev) ev = ev1;
                eu1 = System.Math.Abs((ziie + zise) / 2.0 - zi);
                if (System.Math.Abs(eu1) < 0.0001) eu1 = 0.0001;
                eu1 = System.Math.Sqrt(err * System.Math.Abs(zi) / eu1);
                ev1 = System.Math.Abs((ziit + zist) / 2.0 - zi);
                if (System.Math.Abs(ev1) < 0.0001) ev1 = 0.0001;
                ev1 = System.Math.Sqrt(err * System.Math.Abs(zi) / ev1);
                if (eu1 > eu) eu = eu1;
                if (ev1 > ev) ev = ev1;
                au = 0.00001;
                if (System.Math.Abs(du) > 0.0) au = System.Math.Abs(du) / deta;
                av = 0.00001;
                if (System.Math.Abs(dv) > 0.0) av = System.Math.Abs(dv) / dteta;
                cl = 1.0;
                if (cl > eu / au) cl = eu / au;
                if (cl > ev / av) cl = ev / av;
                du = cl * du;
                dv = cl * dv;

                //c
                //c ----controllo della lunghezza del passo
                //c
                //c ----controllo della variabile teta
                //      if (dabs(dv).gt.dvmax) then
                //                            du = dvmax / dabs(dv) * du
                //                          dv = dvmax * dabs(dv) / dv
                //      endif
                if (System.Math.Abs(dv) > dvmax)
                {
                    du = dvmax / System.Math.Abs(dv) * du;
                    dv = dvmax * System.Math.Abs(dv) / dv;
                }

                //c ----controllo della variabile eta
                //      if (dabs(du).gt.dumax) then
                //                            du = dumax * dabs(du) / du
                //                          dv = dumax / dabs(du) * dv
                //      endif
                if (System.Math.Abs(du) > dumax)
                {
                    du = dumax * System.Math.Abs(du) / du;
                    dv = dumax / System.Math.Abs(du) * dv;
                }

                //c ----controllo della variabile eta(passaggio ad un altro campo)
                //      cc = 0.
                //      eta1 = eta + du
                //      if (eta1.gt.1.)then
                //                     waux = 1.- eta
                //                    dv = waux / du * dv
                //                    du = waux
                //                    cc = 1.3
                //      endif
                //      if (eta1.lt.0.)then
                //                     dv = -eta / du * dv
                //                    du = -eta
                //                    cc = -0.3
                //      endif
                double cc;

                cc = 0.0;
                eta1 = eta + du;
                if (eta1 > 1)
                {
                    waux = 1 - eta;
                    dv = waux / du * dv;
                    du = waux;
                    cc = 1.3;
                }
                if (eta1 < 0.0)
                {
                    dv = -eta / du * dv;
                    du = -eta;
                    cc = -0.3;
                }

                //c
                //c---- calcolo dei nuovi Du Dv per il passo successivo
                //c
                //      eta = eta + du
                //      teta = teta + dv
                eta = eta + du;
                teta = teta + dv;

                //c
                //c ----cambiamento, se necessario, del campo di rottura
                //c
                //      if (cc.lt.- 0.1) then
                //                      eta = 1.
                //                      teta = teta + vst(2 * ic - 1)
                //                   ic = ise(2 * ic - 1)
                //      endif
                //      if (cc.gt.1.1)then
                //                    eta = 0.
                //                    teta = teta + vst(2 * ic)
                //                   ic = ise(2 * ic)
                //      endif
                if (cc < -0.1)
                {
                    eta = 1.0;
                    teta = teta + vst[2 * ic - 1];
                    ic = ise[2 * ic - 1];
                }
                if (cc > 1.1)
                {
                    eta = 0.0;
                    teta = teta + vst[2 * ic];
                    ic = ise[2 * ic];
                }
                //      goto 55
            } while (true == true);

            //c
            //c ----uscita delle caratteristiche ultime
            //c    zi,xi,yi,ic,eta,teta
            //c
            //67  end
        }

        //c
        //c function per determinare la norma di un vettore
        //c
        //      function dnorma(x1, y1, z1)
        //      double precision x1,y1,z1,dnorma
        //      dnorma = dsqrt(x1 * x1 + y1 * y1 + z1 * z1)
        //      return
        //      end
        private double dnorma(double x1, double y1, double z1)
        {
            return System.Math.Sqrt(x1 * x1 + y1 * y1 + z1 * z1);
        }

        //c
        //c    function per determinare il prodotto scalare tra due vettori
        //c
        //      function dscal(x1, y1, z1, x2, y2, z2)
        //      double precision x1,y1,x2,y2,z1,z2,vscal
        //      dscal = x1 * x2 + y1 * y2 + z1 * z2
        //      return
        //      end
        private double dscal(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            return x1 * x2 + y1 * y2 + z1 * z2;
        }

        #endregion
    }
}


