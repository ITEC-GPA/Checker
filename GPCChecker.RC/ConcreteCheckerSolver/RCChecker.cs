using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Common;
using GPC.Utilities;
using GPC.Utilities.Maths;
using GPC.Model.Elements;
using GPC.Geometry;
using GPC.Model.Sections;
using GPC.Model.Materials;


namespace GPC.Checker.ReinforcedConcrete
{
    public partial class RCChecker : GPC.Checker.Common.Checker
    {
        public RCChecker() : base()
        {
        }

        #region Elastic


        protected override string GetCheckerName()
        {
            return "Concrete Checker";
        }

        /// <summary>
        /// Gives the elastic tensions of a RC section      
        /// </summary>
        /// <param name="section">the section to check</param>
        /// <param name="mxx">bending moment AROUND xx axes, positive if give tension upper (y less 0) in Nmm</param>
        /// <param name="myy">bending moment AROUND yy axes, positive if give tension left (x greater 0) in Nmm</param>
        /// <param name="nAxial">axial force, >0 = traction in default units in N centered on barycentre</param>
        /// <param name="barTensions">tension in bars MPa</param>
        /// <param name="clsTensions">tension in cls vertexes MPa</param>
        /// <param name="dirNeutral">neutrl axes direction: point on left of dir=compression</param>
        /// <param name="p0">point on Neutral axes</param>
        /// <param name="nOmogeneizz">omogeneization coeff (15)</param>
        ///  <param name="isTractionConcrete">say if concrete work on traction or not</param>
        public void GetElasticTensions(ConcreteSectionShape section,
                                       double mxx,
                                       double myy,
                                       double nAxial,
                                       out List<KeyValuePair<Rebar, double>> barTensions,
                                       out List<KeyValuePair<Point2d, double>> clsTensions,
                                       out Point2d dirNeutral,
                                       out Point2d p0,
                                       double nOmogeneizz = 15,
                                       bool isTractionConcrete = false)
        {
            int n, m;
            double[] ame, xme, yme, xve, yve, sss, ttt;
            double vt, vmx, vmy;
            double z0, z1, z2;

            List<double> xList, yList, areaList;

            xList = new List<double>();
            yList = new List<double>();
            areaList = new List<double>();

            //Bar datas
            foreach (Rebar bar in section.Rebars)
            {
                xList.Add(bar.Position.X);
                yList.Add(bar.Position.Y);
                areaList.Add(bar.EffectiveArea);
            }

            n = areaList.Count;
            ame = new double[n + 1];
            xme = new double[n + 1];
            yme = new double[n + 1];
            for (int i = 0; i < n; i++)
            {
                ame[i + 1] = areaList[i];
                xme[i + 1] = xList[i];
                yme[i + 1] = yList[i];
            }

            List<Point2d> points = RCChecker.GetPoints(section.Shapes);

            m = points.Count;
            xve = new double[m + 1];
            yve = new double[m + 1];
            for (int i = 0; i < m; i++)
            {
                xve[i + 1] = points[i].X;
                yve[i + 1] = points[i].Y;
            }


            Shapes shapes = new Shapes(section.Shapes);
            shapes.GetAreaBarycentre(out double xG, out double yG, out double buffer);
            double xg = xG;
            double yg = yG;

            //Considering bar prestress
            foreach (Rebar bar in section.Rebars)
            {
                if (bar.Material.Epsilon0 != 0)
                {
                    double nAdd = bar.Material.Epsilon0 * bar.Material.ElasticModulus * bar.Diameter * bar.Diameter * System.Math.PI / 4;
                    nAxial -= nAdd;



                    mxx -= nAdd * (bar.Position.Y - yg);
                    myy += nAdd * (bar.Position.X - xg);

                    //mxx -= nAdd * (bar.Position.Y - section.Centroid.Y);
                    //myy += nAdd * (bar.Position.X - section.Centroid.X);
                }
            }

            //moving soll from barycentre
            double mx, my;
            //mx = mxx + nAxial * section.Centroid.Y;
            //my = myy - nAxial * section.Centroid.X;
            mx = mxx + nAxial * yg;
            my = myy - nAxial * xg;

            //Soll convenzione Gelfi
            vt = -nAxial;
            vmx = -mx;
            vmy = my;

            GetElasticTensionsViviani(vt, vmx, vmy, n, m, nOmogeneizz, ame, xme, yme, xve, yve, isTractionConcrete, out sss, out ttt, out z0, out z1, out z2);

            barTensions = new List<KeyValuePair<Rebar, double>>();
            for (int i = 0; i < n; i++)
            {
                Rebar bar = section.Rebars[i];
                double stress = ttt[i + 1];
                if (bar.Material.Epsilon0 != 0)
                    stress += bar.Material.Epsilon0 * bar.Material.ElasticModulus;
                barTensions.Add(new KeyValuePair<Rebar, double>(bar, stress));
            }

            clsTensions = new List<KeyValuePair<Point2d, double>>();
            for (int i = 0; i < m; i++)
            {
                clsTensions.Add(new KeyValuePair<Point2d, double>(new Point2d(xve[i + 1],
                                                                              yve[i + 1]), sss[i + 1]));
            }

            //deformation plane epsilon=a0+x*a1+y*a2
            double a0, a1, a2;
            //a0 = z0 / section.Section.ElasticModulusE;
            //a1 = z1 / section.Section.ElasticModulusE;
            //a2 = z2 / section.Section.ElasticModulusE;

            a0 = z0 / section.ConcreteMat.ElasticModulus;
            a1 = z1 / section.ConcreteMat.ElasticModulus;
            a2 = z2 / section.ConcreteMat.ElasticModulus;

            Point2d gradient;
            //gradient: increase epsilon direction=from compression to tension
            gradient = new Point2d(z1, z2);
            gradient.Scale(1 / new Vector2d(gradient).Length);
            //left of dirNeutral=compression

            //dirNeutral = Geom.Normal(gradient, true);
            Vector2d vector = ((Vector2d)gradient).Normal(true);
            dirNeutral = new Point2d(vector.X, vector.Y);

            if (System.Math.Abs(z2) > 1e-10)
            {
                p0 = new Point2d(0, -z0 / z2);
            }
            else if (System.Math.Abs(z1) > 1e-10)
            {
                p0 = new Point2d(-z0 / z1, 0);
            }
            else
            {
                //no neutral axes
                p0 = null;
                dirNeutral = null;
            }
        }

        #endregion

        #region PlasticResistanceViviani

        /// <summary>
        /// Get the resistance at ULS of a section
        /// </summary>
        ///  /// <summary>
        /// Get the resistance considering mxed/myed=mxrd/myrd
        /// </summary>
        /// <param name="cs">safety coefficient</param>
        /// <param name="nEd">axial action, >0=traction, centered on section barycentre</param>
        /// <param name="mxEd">moment around x-x axes, >0 if right handed</param>
        /// <param name="myEd">moment around y-y axes, >0 if right handed</param>
        /// <param name="section">section to check</param>
        /// <param name="nRd">resisting axial force, >0 =traction</param>
        /// <param name="mxRd">resisting moment around x-x axes, >0 if right handed</param>
        /// <param name="myRd">resisting moment around y-y axes, >0 if right handed</param>
        /// <param name="cs">safety coefficient</param>
        /// <param name="gammaC">partial safety coefficient cls</param>
        /// <param name="gammaS">partial safety coefficient rebars</param>
        /// <param name="epsCMin">massima def cls (-0.0035)</param>
        /// <param name="epsCMin">massima def acciaio (0.067)</param>
        /// <param name="fiTensAASHTO">reduction factor traction AASHTO</param>
        /// <param name="fiCompAASHTO">reduction factor compression AASHTO</param>
        /// <param name="useLimit34AASHTO">say if using AASHTO witch modify the 3-4 camp</param>
        /// <param name="alfaCC">fattore riduzione resist cls per EC</param>
        public void GetLimitPoint(double nEd,
                                  double mxEd,
                                  double myEd,
                                  ConcreteSectionShape section,
                                  out double nRd,
                                  out double mxRd,
                                  out double myRd,
                                  out double cs,
                                  double gammaC,
                                  double gammaS,
                                  double epsCMin,
                                  double epsSMax,
                                  double fiTensAASHTO,
                                  double fiCompAASHTO,
                                  bool useLimit34AASHTO,
                                  double alfaCC)
        {
            const double DMAX = 0.1;
            double[] vc, vs, vsl, vse;
            int nvc, nvs, nvsl;
            double fysl, fck, e, b, h;
            int[] ipc;
            int npc;
            Point2d dirNeural, p0;
            List<KeyValuePair<Rebar, double>> barTensions;
            List<KeyValuePair<Point2d, double>> clsTensions;
            int ic0;
            double eta0, teta0;

            if (System.Math.Abs(nEd) < 0.0001 &&
                System.Math.Abs(mxEd) < 0.001 &&
                System.Math.Abs(myEd) < 0.001)
            {
                nEd = 1;
            }

            GetVivianiCheckDatas(section, out vc, out vs, out vsl, out nvc, out nvs, out nvsl, out fysl, out fck, out e, out b, out h,
                                 out ipc, out npc, out vse);

            //starting solution: starting from elastic neutrl axes
            GetElasticTensions(section, mxEd, myEd, nEd, out barTensions, out clsTensions, out dirNeural, out p0, 6, false);
            if (p0 != null && dirNeural != null)
            {
                double maxY, minY;
                Trans2d t;
                //teta0: angle of neutral axes
                teta0 = System.Math.Atan2(dirNeural.Y, dirNeural.X);
                //getting campo index
                maxY = double.MinValue;
                minY = double.MaxValue;
                t = new Trans2d(p0, p0 + dirNeural, p0);
                foreach (Shape2d s in section.Shapes)
                {
                    Point2d localP;
                    foreach (Point2d p in s.Fill)
                    {
                        localP = t.PointLocal(p);
                        maxY = System.Math.Max(maxY, localP.Y);
                        minY = System.Math.Min(minY, localP.Y);
                    }
                    if (s.Holes != null)
                    {
                        foreach (Polygon2d hole in s.Holes)
                        {
                            foreach (Point2d p in hole)
                            {
                                localP = t.PointLocal(p);
                                maxY = System.Math.Max(maxY, localP.Y);
                                minY = System.Math.Min(minY, localP.Y);
                            }
                        }
                    }
                }
                ic0 = 3;
                eta0 = 0.5;
                //if (maxY > 0 && minY > 0)
                //{
                //    //all compressed, neutral axes is below section
                //    ic0 = 6;
                //    eta0 = 0.5;
                //}
                //else if (maxY < 0 && minY < 0)
                //{
                //    //all tensile, neutral axes above section
                //    ic0 = 1;
                //    eta0 = 0.5;
                //}
                //else
                //{
                //    ic0 = 3;
                //    eta0 = 0.5;
                //}
            }
            else
            {
                //no neutral axes
                if (nEd > 0)
                {
                    //all tension
                    ic0 = 1;
                    teta0 = 0;
                    eta0 = 0;
                }
                else
                {
                    //all compressed
                    ic0 = 6;
                    teta0 = 0;
                    eta0 = 1;
                }
            }

            double xd, yd, zd, xi, yi, zi, teta, eta;
            int ic;

            GetAdimensionalForce(nEd, mxEd, myEd, b, h, fck, out xd, out yd, out zd);
            GetLimitPointViviani(eta0, teta0, ic0, vc, vs, vsl, nvc, nvs, nvsl, fysl, fck, gammaS, 0, gammaC, 0, b, h, vse, ipc, npc, xd,
                                 yd, zd, DMAX, DMAX, epsCMin, fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, epsSMax,
                                 out xi, out yi, out zi, out teta, out eta, out ic);

            GetUltimateResistances(xi, yi, zi, b, h, fck, out nRd, out mxRd, out myRd);

            if (System.Math.Abs(nEd) > 0.1)
            {
                //cs = nRd / nEd;
                cs = nEd / nRd;
            }
            else if (System.Math.Abs(mxEd) > 0.1)
            {
                //cs = mxRd / mxEd;
                cs = mxEd / mxRd;
            }
            else if (System.Math.Abs(myEd) > 0.1)
            {
                //cs = myEd / myEd;
                cs = myEd / myRd;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Failure domain ricavato da procedura Viviani
        /// </summary>
        public RCFailureDomain GetFailureDomainMV(ConcreteSectionShape section,
                                                 double gammaC,
                                                 double gammaS,
                                                 double epsCMin,
                                                 double epsSMax,
                                                 double fiTensAASHTO,
                                                 double fiCompAASHTO,
                                                 bool useLimit34AASHTO,
                                                 double alfaCC,
                                                 int numberRotations = 80,
                                                 int number1 = 10,
                                                 int number2 = 20,
                                                 int number3 = 20,
                                                 int number4 = 20,
                                                 int number5 = 20,
                                                 int number6 = 10)
        {
            double[] vc, vs, vsl, vse;
            int nvc, nvs, nvsl;
            double fysl, fck, esl, b, h;
            int[] ipc;
            int npc;

            GetVivianiCheckDatas(section, out vc, out vs, out vsl, out nvc, out nvs, out nvsl, out fysl, out fck, out esl, out b, out h,
                                 out ipc, out npc, out vse);

            double deltaAngle = 2 * System.Math.PI / numberRotations;

            List<List<double>> nRds, mxRds, myRds;
            List<List<int>> campoIndexes;
            nRds = new List<List<double>>();
            mxRds = new List<List<double>>();
            myRds = new List<List<double>>();
            campoIndexes = new List<List<int>>();


            for (int i = 0; i < numberRotations; i++)
            {
                double teta = deltaAngle * i;
                List<double> bufferMxRds, bufferMyRds, bufferNRds;
                List<int> bufferCampoIndexes;

                bufferMxRds = new List<double>();
                bufferMyRds = new List<double>();
                bufferNRds = new List<double>();
                bufferCampoIndexes = new List<int>();

                GetCampoResistances(number1, vc, vs, vsl, nvc, nvs, nvsl, 1, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                GetCampoResistances(number2, vc, vs, vsl, nvc, nvs, nvsl, 2, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                GetCampoResistances(number3, vc, vs, vsl, nvc, nvs, nvsl, 3, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                GetCampoResistances(number4, vc, vs, vsl, nvc, nvs, nvsl, 4, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                GetCampoResistances(number5, vc, vs, vsl, nvc, nvs, nvsl, 5, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                GetCampoResistances(number6, vc, vs, vsl, nvc, nvs, nvsl, 6, fysl, fck, gammaS, 0, gammaC, teta, esl, b, h, ipc, npc, epsCMin, epsSMax,
                                    fiTensAASHTO, fiCompAASHTO, useLimit34AASHTO, alfaCC, vse, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);
                nRds.Add(bufferNRds);
                mxRds.Add(bufferMxRds);
                myRds.Add(bufferMyRds);
                campoIndexes.Add(bufferCampoIndexes);
            }

            return new RCFailureDomain(section, gammaC, 0.85, gammaS, -0.0035, -0.002, 0.01, 1, 2, nRds, mxRds, myRds, campoIndexes);
        }

        private void GetCampoResistances(int number,
                                         double[] vc, double[] vs, double[] vsl, int nvc, int nvs, int nvsl, int ic,
                                         double fysl, double fck, double gammaS, double gammaSL, double gammaC, double teta,
                                         double esl, double b, double h, int[] ipc, int npc,
                                         double epsCMin, double epsSMax, double fiTensAASHTO, double fiCompAASHTO, bool useLimit34AASHTO,
                                         double alfaCC, double[] vse,
                                         ref List<double> nRds, ref List<double> mxRds, ref List<double> myRds, ref List<int> campoIndexes)
        {
            int last;
            if (ic == 6)
            {
                last = number;
            }
            else
            {
                last = number - 1;
            }
            for (int j = 0; j <= last; j++)
            {
                double x, y, z;
                double nRd, mxRd, myRd;
                double eta;

                eta = Convert.ToDouble(j) / number;
                GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, ic, fysl, fck, gammaS, 0, gammaC, eta, teta, esl,
                                               out x, out y, out z, b, h, vse, ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO,
                                               epsSMax);
                GetUltimateResistances(x, y, z, b, h, fck, out nRd, out mxRd, out myRd);
                nRds.Add(nRd);
                mxRds.Add(mxRd);
                myRds.Add(myRd);
                campoIndexes.Add(ic);
            }
        }

        /// <summary>
        /// Resistenza sezione CA da procedura Viviani
        /// </summary>
        public void GetPlasticResistance(ConcreteSectionShape section,
                                         int campoIndex, double gammaS, double gammaC,
                                         double epsCMin, double epsSMax, double fiTensAASHTO, double fiCompAASHTO, bool useLimit34AASHTO, double alfaCC,
                                         double eta, double teta,
                                         out double mxRd, out double myRd, out double nRd)
        {
            double[] vc, vs, vsl, vse;
            int nvc, nvs, nvsl;
            double fys, fysl, fck, esl, x, y, z, b, h;
            int[] ipc;
            int npc;

            GetVivianiCheckDatas(section, out vc, out vs, out vsl, out nvc, out nvs, out nvsl, out fysl, out fck, out esl, out b, out h,
                                 out ipc, out npc, out vse);
            GetPlasticResistancesCAViviani(vc, vs, vsl, nvc, nvs, nvsl, campoIndex, fysl, fck, gammaS, 0, gammaC, eta, teta, esl,
                                           out x, out y, out z, b, h, vse,
                                           ipc, npc, epsCMin, fiTensAASHTO, fiCompAASHTO, alfaCC, useLimit34AASHTO, epsSMax);
            GetUltimateResistances(x, y, z, b, h, fck, out nRd, out mxRd, out myRd);
        }

        private void GetUltimateResistances(double x,
                                            double y,
                                            double z,
                                            double b,
                                            double h,
                                            double fck,
                                            out double nRd,
                                            out double mxRd,
                                            out double myRd)
        {
            nRd = -z * b * h * fck;//per MV positivo se di compressione
            mxRd = x * b * h * h * fck;//per MV positivo se antiorario,ovvero destrogiro
            myRd = y * b * b * h * fck;//per MV positivo se antiorario, ovvero destrogiro
        }

        private void GetAdimensionalForce(double nEd,
                                          double mxEd,
                                          double myEd,
                                           double b,
                                           double h,
                                           double fck,
                                           out double x,
                                           out double y,
                                           out double z)
        {
            z = -nEd / (b * h * fck);//per MV positivo se di compressione
            x = mxEd / (b * h * h * fck);//per MV positivo se antiorario,ovvero destrogiro
            y = myEd / (b * b * h * fck);//per MV positivo se antiorario, ovvero destrogiro
        }

        private void GetVivianiCheckDatas(ConcreteSectionShape checkingSection,
                                          out double[] vc, out double[] vs, out double[] vsl,
                                          out int nvc, out int nvs, out int nvsl,
                                          out double fysl, out double fck, out double esl,
                                          out double b, out double h, out int[] ipc, out int npc, out double[] vse)
        {
            List<double> vcList, vsList, vseList;
            List<int> ipcList;
            BoundingBox2d bbox;

            //NOTE:
            //- manca limitazione a 0.8NMaxRd per stati di compressione senza M

            //getting g

            Shapes shapes = new Shapes(checkingSection.Shapes);
            shapes.GetAreaBarycentre(out double xG, out double yG, out double buffer);

            //checking section formed by convex shapes. If not, triangulate the polygon
            vcList = new List<double>();
            ipcList = new List<int>();
            nvc = 0;
            npc = 0;
            bbox = new BoundingBox2d();
            foreach (Shape2d s in checkingSection.Shapes)
            {
                FillPolygonDatas(s.Fill, xG, yG, false, bbox, ref ipcList, ref npc, ref nvc, ref vcList);
                if (s.Holes != null)
                {
                    foreach (Polygon2d hole in s.Holes)
                    {
                        FillPolygonDatas(hole, xG, yG, true, bbox, ref ipcList, ref npc, ref nvc, ref vcList);
                    }
                }
            }
            //basing on 0 index
            vcList.Insert(0, 0);
            vc = vcList.ToArray();
            ipcList.Insert(0, 0);
            ipc = ipcList.ToArray();
            fck = checkingSection.ConcreteMat.Fck;

            //bars
            nvs = checkingSection.Rebars.Count;
            vsList = new List<double>();
            vseList = new List<double>();
            if (nvs > 0)
            {
                foreach (Rebar bar in checkingSection.Rebars)
                {
                    /// Area efficace
                    vsList.Add(bar.EffectiveArea);
                    /// Posizione baricentro in X
                    vsList.Add(bar.Position.X - xG);
                    /// Posizione baricentro in Y
                    vsList.Add(bar.Position.Y - yG);
                    /// Modulo Elastico
                    vseList.Add(bar.Material.ElasticModulus);
                    /// Prentensione
                    //vseList.Add(bar.Material.Epsilon0 * bar.Material.ElasticModulus);
                    vseList.Add(0);
                    /// Tensione Ultima
                    vseList.Add(bar.Material.Fu);
                }
            }
            vsList.Insert(0, 0);
            vs = vsList.ToArray();
            vseList.Insert(0, 0);
            vse = vseList.ToArray();

            //Linear steel areas (actually not implemented)
            nvsl = 0;
            vsl = new double[0];
            fysl = 0;
            esl = 0;

            b = bbox.Size.X;
            h = bbox.Size.Y;
        }

        private void FillPolygonDatas(Polygon2d poly, double xG, double yG, bool isHole, BoundingBox2d bbox,
                                     ref List<int> ipcList, ref int npc, ref int nvc, ref List<double> vcList)
        {
            bool isRightHandOrdered;

            isRightHandOrdered = poly.IsRightHandOrdered();
            bbox.Update(poly);
            if (IsPolygonConvex(poly, isRightHandOrdered))
            {
                ipcList.Add(poly.Count * (isHole ? -1 : 1));
                npc++;
                if (isRightHandOrdered)
                {
                    for (int i = 0; i <= poly.Count - 1; i++)
                    {
                        nvc++;
                        vcList.Add(nvc);
                        vcList.Add(poly[i].X - xG);
                        vcList.Add(poly[i].Y - yG);
                        ipcList.Add(nvc);
                    }
                }
                else
                {
                    for (int i = poly.Count - 1; i >= 0; i--)
                    {
                        nvc++;
                        vcList.Add(nvc);
                        vcList.Add(poly[i].X - xG);
                        vcList.Add(poly[i].Y - yG);
                        ipcList.Add(nvc);
                    }
                }
            }
            else
            {
                Mesh mesh;
                mesh = Mesh.Triangulate(poly, null);

                foreach (MeshFace s in mesh.Faces)
                {
                    Polygon2d triangle = new Polygon2d();
                    if(s.IsTriangle == true)
                    {
                        triangle.Add(mesh.Vertices[s.A - 1].Point.X, mesh.Vertices[s.A - 1].Point.Y);
                        triangle.Add(mesh.Vertices[s.B - 1].Point.X, mesh.Vertices[s.B - 1].Point.Y);
                        triangle.Add(mesh.Vertices[s.C - 1].Point.X, mesh.Vertices[s.C - 1].Point.Y);
                    }
                    else if(s.IsQuad == true)
                    {
                        //triangle.Add(mesh.Vertices[s.A - 1].Point.X, mesh.Vertices[s.A - 1].Point.Y);
                        //triangle.Add(mesh.Vertices[s.B - 1].Point.X, mesh.Vertices[s.B - 1].Point.Y);
                        //triangle.Add(mesh.Vertices[s.C - 1].Point.X, mesh.Vertices[s.C - 1].Point.Y);
                        //triangle.Add(mesh.Vertices[s.D - 1].Point.X, mesh.Vertices[s.D - 1].Point.Y);
                    }

                    ipcList.Add(triangle.Count * (isHole ? -1 : 1));
                    npc++;
                    isRightHandOrdered = triangle.IsRightHandOrdered();
                    if ((isRightHandOrdered && (!isHole)) || ((!isRightHandOrdered) && isHole))
                    {
                        for (int i = 0; i <= triangle.Count - 1; i++)
                        {
                            nvc++;
                            vcList.Add(nvc);
                            vcList.Add(triangle[i].X - xG);
                            vcList.Add(triangle[i].Y - yG);
                            ipcList.Add(nvc);
                        }
                    }
                    else
                    {
                        for (int i = triangle.Count - 1; i >= 0; i--)
                        {
                            nvc++;
                            vcList.Add(nvc);
                            vcList.Add(triangle[i].X - xG);
                            vcList.Add(triangle[i].Y - yG);
                            ipcList.Add(nvc);
                        }
                    }
                }
            }
        }

        private bool IsPolygonConvex(Polygon2d poly, bool isRightHandOrdered)
        {
            //Checking convexity
            for (int i = 0; i < poly.Count; i++)
            {
                Point2d p, nextP, nextnextP;
                p = poly[i];
                nextP = poly.GetNextPoint(i);
                nextnextP = poly.GetNextPoint(poly.GetNextIndex(i));
                //Point2d rightOrto = Geom.Normal(Geom.Direction(p, nextP), isRightHandOrdered);
                //Point2d rightOrto = ((Vector2d)(p - nextP)).Normal(isRightHandOrdered);
                Vector2d vector1 = ((Vector2d)(nextP - p));
                Vector2d vector2 = vector1.Normal(isRightHandOrdered);
                vector2.Unitize();
                Point2d rightOrto = new Point2d(vector2.X, vector2.Y);
                if ((nextnextP - p) * rightOrto < -0.001)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region PlasticResistanceEN

        /// <summary>
        /// Get failure domain
        /// </summary>
        /// <param name="checkingSection">section</param>
        /// <param name="gammaC">gammaC</param>
        /// <param name="alfaCC">alfaCC</param>
        /// <param name="gammaS">gammaS</param>
        /// <param name="epsCU">ultimate deform cls</param>
        /// <param name="epsC2">epsC2 cls</param>
        /// <param name="epsUd">ultimate defrom steel</param>
        /// <param name="pureCompressionReductionCoeff">reduction for simply compression section</param>
        /// <param name="parabolaExponent">exponent of parabola</param>
        /// <param name="numberRotations">rotations of neutral axes</param>
        /// <param name="number1">number of step campo1</param>
        /// <param name="number2">number of step campo2</param>
        /// <param name="number3">number of step campo3</param>
        /// <param name="number4">number of step campo4</param>
        /// <param name="number5">number of step campo5</param>
        /// <param name="number6">number of step campo6</param>
        /// <returns></returns>
        public RCFailureDomain GetFailureDomainEN(ConcreteSectionShape checkingSection,
                                                 double gammaC = 1.5,
                                                 double alfaCC = 0.85,
                                                 double gammaS = 1.15,
                                                 double epsCU = -0.0035,
                                                 double epsC2 = -0.002,
                                                 double epsUd = 0.067,
                                                 double pureCompressionReductionCoeff = 0.8,
                                                 double parabolaExponent = 2,
                                                 int numberRotations = 80,
                                                 int number1 = 10,
                                                 int number2 = 20,
                                                 int number3 = 20,
                                                 int number4 = 20,
                                                 int number5 = 20,
                                                 int number6 = 10)
        {
            //throw new NotSupportedException("TO TEST. la rotazione limite nelle sezioni pretese non è corretta! Considera uso del calcolatore di viviani (nella cartella viviani)");

            double deltaAngle;
            List<Point2d> baricentricPoints;
            Polygon2d localPolygon;
            Rebars baricentricBars;
            double fcd;
            double xG, yG, area;
            List<List<double>> nRds, mxRds, myRds;
            List<List<int>> campoIndexes;

            deltaAngle = 2 * System.Math.PI / numberRotations;
            baricentricPoints = GetPoints(checkingSection.Shapes);

            //Moving to barycentre
            //checkingSection.Shapes.GetAreaBarycentre(out xG, out yG, out area);

            Shapes shapes = new Shapes(checkingSection.Shapes);
            shapes.GetAreaBarycentre(out xG, out yG, out area);

            for (int i = 0; i < baricentricPoints.Count; i++)
            {
                baricentricPoints[i] = new Point2d(baricentricPoints[i].X - xG, baricentricPoints[i].Y - yG);
            }
            baricentricBars = new Rebars();
            foreach (Rebar bar in checkingSection.Rebars)
            {
                baricentricBars.AddRebar(bar.Diameter, bar.EffectiveArea, new Point2d(0,0), new Point2d(0, 0), new Point2d(bar.Position.X - xG, bar.Position.Y - yG), bar.Material, Guid.Empty);
            }

            fcd = checkingSection.ConcreteMat.Fck * alfaCC / gammaC;

            nRds = new List<List<double>>();
            mxRds = new List<List<double>>();
            myRds = new List<List<double>>();
            campoIndexes = new List<List<int>>();
            for (int rotIndex = 0; rotIndex < numberRotations; rotIndex++)
            {
                double angle = deltaAngle * rotIndex;
                double minY, maxY, minYs, maxYs, maxEyd, minEyd;
                List<KeyValuePair<Point2d, Rebar>> localBars;
                Trans2d trans = new Trans2d(Point2d.Origin, new Point2d(System.Math.Cos(angle), System.Math.Sin(angle)), Point2d.Origin);
                List<double> bufferMxRds, bufferMyRds, bufferNRds;
                List<int> bufferCampoIndexes;

                //moving to rotated
                minY = double.MaxValue;
                maxY = double.MinValue;
                localPolygon = new Polygon2d();
                foreach (Point2d p in baricentricPoints)
                {
                    Point2d localP;
                    localP = trans.PointLocal(p);
                    localPolygon.Add(localP);
                    minY = System.Math.Min(minY, localP.Y);
                    maxY = System.Math.Max(maxY, localP.Y);
                }
                localBars = new List<KeyValuePair<Point2d, Rebar>>();
                minYs = double.MaxValue;
                maxYs = double.MinValue;
                maxEyd = double.MinValue;
                minEyd = double.MaxValue;
                foreach (Rebar bar in baricentricBars)
                {
                    Point2d localP;
                    double epsYd;
                    localP = trans.PointLocal(bar.Position);
                    localBars.Add(new KeyValuePair<Point2d, Rebar>(localP, bar));
                    minYs = System.Math.Min(minYs, localP.Y);
                    maxYs = System.Math.Max(maxYs, localP.Y);
                    epsYd = bar.Material.Fu / gammaS / bar.Material.ElasticModulus;
                    maxEyd = System.Math.Max(epsYd, maxEyd);
                    minEyd = System.Math.Min(epsYd, minEyd);
                }

                double heigth = maxY - minY;
                double d = maxY - minYs;
                double startingY;
                if (maxYs - minYs > 0.001)
                {
                    //startingY = minYs + (maxYs - minYs) / (epsUd - maxEyd) * epsUd;
                    startingY = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(epsUd, maxEyd, minYs, maxYs, 0);
                }
                else
                {
                    startingY = maxY + 3 * heigth;
                }

                bufferMxRds = new List<double>();
                bufferMyRds = new List<double>();
                bufferNRds = new List<double>();
                bufferCampoIndexes = new List<int>();

                //Campo 1
                double lowerY, upperY;
                double deltaY;
                double y0, epsilon0;
                double nRd, mxRd, myRd;
                double yNeutral;
                upperY = startingY;
                lowerY = maxY;
                deltaY = (upperY - lowerY) / number1;
                y0 = minYs;
                epsilon0 = epsUd;
                for (int i = 0; i < number1; i++)
                {
                    yNeutral = upperY - i * deltaY;
                    mxRd = 0;
                    myRd = 0;
                    nRd = 0;
                    AddUltimateResistanceBars(localBars, yNeutral, y0, epsilon0, gammaS, fcd, epsC2, parabolaExponent, epsCU, ref nRd, ref mxRd, ref myRd);
                    //cls teso
                    bufferNRds.Add(nRd);
                    bufferMxRds.Add(mxRd);
                    bufferMyRds.Add(myRd);
                    bufferCampoIndexes.Add(1);
                }

                //Campo 2
                upperY = maxY;
                lowerY = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(epsCU, epsUd, maxY, minYs, 0);
                deltaY = (upperY - lowerY) / number2;
                y0 = minYs;
                epsilon0 = epsUd;
                AddUltimateResistanceBarsCls(number2, upperY, deltaY, localBars, y0, epsilon0, gammaS, localPolygon, maxY, epsC2, epsCU, trans, fcd,
                             parabolaExponent, 2, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                //Campo 3
                upperY = lowerY;
                lowerY = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(epsCU, minEyd, maxY, minYs, 0);
                deltaY = (upperY - lowerY) / number3;
                y0 = maxY;
                epsilon0 = epsCU;
                AddUltimateResistanceBarsCls(number3, upperY, deltaY, localBars, y0, epsilon0, gammaS, localPolygon, maxY, epsC2, epsCU, trans, fcd,
                                             parabolaExponent, 3, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                //Campo 4
                upperY = lowerY;
                lowerY = minYs;
                deltaY = (upperY - lowerY) / number4;
                y0 = maxY;
                epsilon0 = epsCU;
                AddUltimateResistanceBarsCls(number4, upperY, deltaY, localBars, y0, epsilon0, gammaS, localPolygon, maxY, epsC2, epsCU, trans, fcd,
                                             parabolaExponent, 4, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                //Campo 5
                upperY = lowerY;
                lowerY = minY;
                deltaY = (upperY - lowerY) / number5;
                y0 = maxY;
                epsilon0 = epsCU;
                AddUltimateResistanceBarsCls(number5, upperY, deltaY, localBars, y0, epsilon0, gammaS, localPolygon, maxY, epsC2, epsCU, trans, fcd,
                                             parabolaExponent, 5, ref bufferNRds, ref bufferMxRds, ref bufferMyRds, ref bufferCampoIndexes);

                //Campo 6
                upperY = lowerY;
                lowerY = minY - 10 * heigth;
                deltaY = (upperY - lowerY) / number5;
                y0 = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(minY, maxY, 0, epsCU, epsC2);
                epsilon0 = epsC2;
                for (int i = 0; i < number6; i++)
                {
                    double epsTop;
                    Polygon2d parabolicPolygon;
                    yNeutral = upperY - i * deltaY;
                    mxRd = 0;
                    myRd = 0;
                    nRd = 0;
                    AddUltimateResistanceBars(localBars, yNeutral, y0, epsilon0, gammaS, fcd, epsC2, parabolaExponent, epsCU, ref nRd, ref mxRd, ref myRd);
                    epsTop = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(yNeutral, y0, 0, epsilon0, maxY);
                    Polygon2d rectangular;
                    double yEpsC2;
                    yEpsC2 = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(epsilon0, 0, y0, yNeutral, epsC2);
                    if (epsTop < epsC2)
                    {
                        rectangular = localPolygon.GetLeftPolygon(new Point2d(0, yEpsC2), new Point2d(100, yEpsC2));
                        parabolicPolygon = localPolygon.GetRightPolygon(new Point2d(0, yEpsC2), new Point2d(100, yEpsC2));
                    }
                    else
                    {
                        rectangular = null;
                        parabolicPolygon = localPolygon;
                    }
                    AddUltimateResistanceConc(parabolicPolygon, rectangular, trans, yNeutral, fcd, yEpsC2, epsC2, parabolaExponent,
                                              ref nRd, ref mxRd, ref myRd);
                    bufferNRds.Add(nRd);
                    bufferMxRds.Add(mxRd);
                    bufferMyRds.Add(myRd);
                    bufferCampoIndexes.Add(6);

                }

                //limiting nRds to minimum admissible
                double minNRds;
                minNRds = -area * fcd * pureCompressionReductionCoeff;
                foreach (Rebar b in checkingSection.Rebars)
                {
                    minNRds -= b.EffectiveArea * b.Material.Fu / gammaS;
                }
                for (int i = 0; i < bufferNRds.Count; i++)
                {
                    bufferNRds[i] = System.Math.Max(bufferNRds[i], minNRds);
                }

                nRds.Add(bufferNRds);
                mxRds.Add(bufferMxRds);
                myRds.Add(bufferMyRds);
                campoIndexes.Add(bufferCampoIndexes);
            }
            return new RCFailureDomain(checkingSection, gammaC, alfaCC, gammaS, epsCU, epsC2, epsUd, pureCompressionReductionCoeff, parabolaExponent,
                                       nRds, mxRds, myRds, campoIndexes);
        }

        private double GetEpsilon(double y,
                                  double yNeutralAxes,
                                  double y0,
                                  double epsilon0)
        {
            return GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(y0, yNeutralAxes, epsilon0, 0, y);
        }

        private double GetBarN(Rebar bar,
                               double epsilon,
                               double gammaS,
                               double fcd,
                               double epsC2,
                               double parabolaExponent,
                               double epsCU)
        {
            double epsyd, fyd;
            double totalBarDeform;
            double sigmaC;
            fyd = bar.Material.Fu / gammaS;
            epsyd = fyd / bar.Material.ElasticModulus;
            totalBarDeform = epsilon + bar.Material.Epsilon0;
            sigmaC = GetConcreteStress(epsilon, fcd, epsC2, parabolaExponent, epsCU);
            if (System.Math.Abs(totalBarDeform) < epsyd)
            {
                return (totalBarDeform * bar.Material.ElasticModulus - sigmaC) * bar.EffectiveArea;
            }
            else
            {
                return (fyd * System.Math.Sign(totalBarDeform) - sigmaC) * bar.EffectiveArea;
            }
        }

        private void AddUltimateResistanceBarsCls(int number,
                                                  double upperY,
                                                  double deltaY,
                                                  List<KeyValuePair<Point2d, Rebar>> localBars,
                                                  double y0,
                                                  double epsilon0,
                                                  double gammaS,
                                                  Polygon2d localPolygon,
                                                  double maxY,
                                                  double epsC2,
                                                  double epsCU,
                                                  Trans2d trans,
                                                  double fcd,
                                                  double parabolaExponent,
                                                  int campoIndex,
                                                  ref List<double> nRds,
                                                  ref List<double> mxRds,
                                                  ref List<double> myRds,
                                                  ref List<int> campoIndexes)
        {
            for (int i = 0; i < number; i++)
            {
                double epsTop;
                double yNeutral;
                Polygon2d parabolicPolygon;
                double nRd, mxRd, myRd;
                yNeutral = upperY - i * deltaY;
                mxRd = 0;
                myRd = 0;
                nRd = 0;
                AddUltimateResistanceBars(localBars, yNeutral, y0, epsilon0, gammaS, fcd, epsC2, parabolaExponent, epsCU, ref nRd, ref mxRd, ref myRd);
                epsTop = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(yNeutral, y0, 0, epsilon0, maxY);
                parabolicPolygon = localPolygon.GetLeftPolygon(new Point2d(0, yNeutral), new Point2d(100, yNeutral));
                if (parabolicPolygon != null && parabolicPolygon.Count > 2)
                {
                    Polygon2d rectangular;
                    double yEpsC2;
                    rectangular = null;
                    yEpsC2 = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(epsilon0, 0, y0, yNeutral, epsC2);
                    if (epsTop < epsC2)
                    {
                        rectangular = parabolicPolygon.GetLeftPolygon(new Point2d(0, yEpsC2), new Point2d(100, yEpsC2));
                        parabolicPolygon = parabolicPolygon.GetRightPolygon(new Point2d(0, yEpsC2), new Point2d(100, yEpsC2));
                    }
                    AddUltimateResistanceConc(parabolicPolygon, rectangular, trans, yNeutral, fcd, yEpsC2, epsC2, parabolaExponent,
                                              ref nRd, ref mxRd, ref myRd);
                }
                nRds.Add(nRd);
                mxRds.Add(mxRd);
                myRds.Add(myRd);
                campoIndexes.Add(campoIndex);
            }
        }

        private void AddUltimateResistanceBars(List<KeyValuePair<Point2d, Rebar>> localBars,
                                               double y,
                                               double y0,
                                               double epsilon0,
                                               double gammaS,
                                               double fcd,
                                               double epsC2,
                                               double parabolaExponent,
                                               double epsCU,
                                               ref double nRd,
                                               ref double mxRd,
                                               ref double myRd)
        {
            foreach (KeyValuePair<Point2d, Rebar> kvp in localBars)
            {
                double epsilon;
                double deltaN;
                epsilon = GetEpsilon(kvp.Key.Y, y, y0, epsilon0);
                deltaN = GetBarN(kvp.Value, epsilon, gammaS, fcd, epsC2, parabolaExponent, epsCU);
                nRd += deltaN;
                mxRd += deltaN * kvp.Value.Position.Y;
                myRd -= deltaN * kvp.Value.Position.X;
            }
        }

        private double GetParabolicConcStress(double yNeutral,
                                              double fcd,
                                              double yEpsc2,
                                              double epsC2,
                                              double parabolaExponent,
                                              double y)
        {
            return GetParabolicConcStress(GetEpsilon(y, yNeutral, yEpsc2, epsC2), fcd, epsC2, parabolaExponent);
        }

        private double GetConcreteStress(double epsilon,
                                         double fcd,
                                         double epsC2,
                                         double parabolaExponent,
                                         double epsCU)
        {
            if (epsilon < epsCU)
            {
                return 0;
            }
            else if (epsilon < epsC2)
            {
                return -fcd;
            }
            else if (epsilon < 0)
            {
                return GetParabolicConcStress(epsilon, fcd, epsC2, parabolaExponent);
            }
            else
            {
                return 0;
            }
        }

        private double GetParabolicConcStress(double epsilon,
                                              double fcd,
                                              double epsC2,
                                              double parabolaExponent)
        {
            return -fcd * (1 - System.Math.Pow(1 - epsilon / epsC2, parabolaExponent));
        }

        private void AddUltimateResistanceConc(Polygon2d parabolicPolygon,
                                               Polygon2d rectangular,
                                               Trans2d trans,
                                               double yNeutral,
                                               double fcd,
                                               double yEpsC2,
                                               double epsC2,
                                               double parabolaExponent,
                                               ref double nRd,
                                               ref double mxRd,
                                               ref double myRd)
        {
            double localNrd, localMxRd, localMyRd;
            localNrd = 0;
            localMxRd = 0;
            localMyRd = 0;
            if (parabolicPolygon != null && parabolicPolygon.Count > 2)
            {
                Point2d p1 = parabolicPolygon[0];
                for (int j = 1; j < parabolicPolygon.Count - 1; j++)
                {
                    Point2d p2, p3;
                    p2 = parabolicPolygon[j];
                    p3 = parabolicPolygon.GetNextPoint(j);
                    GetParabolicIntegralOverTriangle(p1, p2, p3, yNeutral, fcd, yEpsC2, epsC2, parabolaExponent, ref localNrd, ref localMxRd,
                                                      ref localMyRd);
                }
            }

            if (rectangular != null && rectangular.Count > 2)
            {
                Point2d p1 = rectangular[0];
                for (int j = 1; j < rectangular.Count; j++)
                {
                    Point2d p2, p3;
                    p2 = rectangular[j];
                    p3 = rectangular.GetNextPoint(j);
                    GetConstIntegralOverTriangle(p1, p2, p3, fcd, ref localNrd, ref localMxRd, ref localMyRd);
                }
            }
            nRd += localNrd;
            Point2d bufferGlobalM;
            bufferGlobalM = trans.PointGlobal(localMxRd, localMyRd);
            mxRd += bufferGlobalM.X;
            myRd += bufferGlobalM.Y;
        }

        #region Integrals

        private static double[] PIntegral = new double[]
                                            {
                                            6.943184420297371E-002,
                                            6.943184420297371E-002,
                                            6.943184420297371E-002,
                                            6.943184420297371E-002,
                                            6.943184420297371E-002,
                                            0.330009478207572,
                                            0.330009478207572,
                                            0.330009478207572,
                                            0.330009478207572,
                                            0.669990521792428,
                                            0.669990521792428,
                                            0.669990521792428,
                                            0.930568155797026,
                                            0.930568155797026
                                            };
        private static double[] QIntegral = new double[]
                                           {
                                           4.365302387072518E-002,
                                            0.214742881469342,
                                            0.465284077898513,
                                            0.715825274327684,
                                            0.886915131926301,
                                            4.651867752656094E-002,
                                            0.221103222500738,
                                            0.448887299291690,
                                            0.623471844265867,
                                            3.719261778493340E-002,
                                            0.165004739103786,
                                            0.292816860422638,
                                            1.467267513102734E-002,
                                            5.475916907194637E-002
                                           };
        private static double[] Weigths = Weigths = new double[]
                                            {
                                            1.917346464706755E-002,
                                            3.873334126144628E-002,
                                            4.603770904527855E-002,
                                            3.873334126144628E-002,
                                            1.917346464706755E-002,
                                            3.799714764789616E-002,
                                            7.123562049953998E-002,
                                            7.123562049953998E-002,
                                            3.799714764789616E-002,
                                            2.989084475992800E-002,
                                            4.782535161588505E-002,
                                            2.989084475992800E-002,
                                            6.038050853208200E-003,
                                            6.038050853208200E-003
                                            };

        private void GetParabolicIntegralOverTriangle(Point2d p1,
                                                      Point2d p2,
                                                      Point2d p3,
                                                      double yNeutral,
                                                      double fcd,
                                                      double yEpsc2,
                                                      double epsC2,
                                                      double parabolaExponent,
                                                      ref double localNrd,
                                                      ref double localMxRd,
                                                      ref double localMyRd)
        {
            double coeff = GetCoeff(p1, p2, p3);
            if (System.Math.Abs(coeff) > 1e-6)
            {
                double bufferNRd, bufferMxRd, bufferMyRd;
                bufferNRd = 0;
                bufferMxRd = 0;
                bufferMyRd = 0;
                for (int i = 0; i < PIntegral.Length; i++)
                {
                    double x = GetXIntegral(p1, p2, p3, i);
                    double y = GetYIntegral(p1, p2, p3, i);
                    double sigma = GetParabolicConcStress(yNeutral, fcd, yEpsc2, epsC2, parabolaExponent, y);

                    bufferNRd += Weigths[i] * sigma;
                    bufferMxRd += Weigths[i] * sigma * y;
                    bufferMyRd -= Weigths[i] * sigma * x;
                }

                bufferNRd *= coeff;
                bufferMxRd *= coeff;
                bufferMyRd *= coeff;
                localNrd += bufferNRd;
                localMxRd += bufferMxRd;
                localMyRd += bufferMyRd;
            }
        }

        private void GetConstIntegralOverTriangle(Point2d p1,
                                                  Point2d p2,
                                                  Point2d p3,
                                                  double fcd,
                                                  ref double localNrd,
                                                  ref double localMxRd,
                                                  ref double localMyRd)
        {
            double coeff = GetCoeff(p1, p2, p3);
            if (System.Math.Abs(coeff) > 1e-6)
            {
                double bufferNRd, bufferMxRd, bufferMyRd;
                bufferNRd = 0;
                bufferMxRd = 0;
                bufferMyRd = 0;
                for (int i = 0; i < PIntegral.Length; i++)
                {
                    double x = GetXIntegral(p1, p2, p3, i);
                    double y = GetYIntegral(p1, p2, p3, i);
                    double sigma = -fcd;

                    bufferNRd += Weigths[i] * sigma;
                    bufferMxRd += Weigths[i] * sigma * y;
                    bufferMyRd -= Weigths[i] * sigma * x;
                }
                bufferNRd *= coeff;
                bufferMxRd *= coeff;
                bufferMyRd *= coeff;
                localNrd += bufferNRd;
                localMxRd += bufferMxRd;
                localMyRd += bufferMyRd;
            }
        }

        private double GetXIntegral(Point2d p1,
                                    Point2d p2,
                                    Point2d p3,
                                    int i)
        {
            return p1.X + (p2.X - p1.X) * PIntegral[i] + (p3.X - p1.X) * QIntegral[i];
        }

        private double GetYIntegral(Point2d p1,
                                    Point2d p2,
                                    Point2d p3,
                                    int i)
        {
            return p1.Y + (p2.Y - p1.Y) * PIntegral[i] + (p3.Y - p1.Y) * QIntegral[i];
        }

        private double GetCoeff(Point2d p1,
                                Point2d p2,
                                Point2d p3)
        {
            return (p2.X - p1.X) * (p3.Y - p1.Y) - (p3.X - p1.X) * (p2.Y - p1.Y);
        }

        #endregion

        #endregion

        private static List<Point2d> GetPoints(List<Shape2d> ss)
        {
            List<Point2d> result;
            result = new List<Point2d>();
            //creating coordinates
            foreach (Shape2d s in ss)
            {
                Polygon2d buffer;
                if (s.Fill.IsRightHandOrdered())
                {
                    buffer = s.Fill;
                }
                else
                {
                    buffer = new Polygon2d(s.Fill);
                    buffer.Reverse();
                }
                foreach (Point2d p in buffer)
                {
                    result.Add(p);
                }
                result.Add(buffer[0]);
                if (s.Holes != null)
                {
                    foreach (Polygon2d hole in s.Holes)
                    {
                        if (!hole.IsRightHandOrdered())
                        {
                            buffer = hole;
                        }
                        else
                        {
                            buffer = new Polygon2d(hole);
                            buffer.Reverse();
                        }
                        foreach (Point2d p in buffer)
                        {
                            result.Add(p);
                        }
                        result.Add(buffer[0]);
                        result.Add(s.Fill[0]);
                    }
                }
            }
            //adding closure point
            for (int i = ss.Count - 2; i > 0; i--)
            {
                result.Add(ss[i].Fill[0]);
            }
            return result;
        }

    }
}
