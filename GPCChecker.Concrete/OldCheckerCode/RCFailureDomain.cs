//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using GPC.Checkers.Common;
//using GPC.Utilities;
//using GPC.Model;
//using GPC.Geometry;
//using GPC.Model.Sections;

//namespace GPC.Checkers.ReinforcedConcrete
//{
//    public class RCFailureDomain
//    {
//        private const int LEVEL_CURVE_NUMBER = 200;

//        private readonly ConcreteSectionShape _section;
//        private readonly double _gammaC;
//        private readonly double _alfaCC;
//        private readonly double _gammaS;
//        private readonly double _epsCU;
//        private readonly double _epsC2;
//        private readonly double _epsUd;
//        private readonly double _pureCompressionReductionCoeff;
//        private readonly double _parabolaExponent;

//        //Raw datas
//        private readonly List<List<double>> _nRds, _mxRds, _myRds;
//        private readonly List<List<int>> _campoIndexes;

//        //Level curves
//        private readonly double[] _levelNRds;
//        private readonly List<Point2d>[] _levelMRds;

//        internal RCFailureDomain(ConcreteSectionShape section,
//                                double gammaC,
//                                double alfaCC,
//                                double gammaS,
//                                double epsCU,
//                                double epsC2,
//                                double epsUd,
//                                double pureCompressionReductionCoeff,
//                                double parabolaExponent,
//                                List<List<double>> nRds,
//                                List<List<double>> mxRds,
//                                List<List<double>> myRds,
//                                List<List<int>> campoIndexes)
//        {
//            _section = section;
//            _gammaC = gammaC;
//            _alfaCC = alfaCC;
//            _gammaS = gammaS;
//            _epsCU = epsCU;
//            _epsC2 = epsC2;
//            _epsUd = epsUd;
//            _pureCompressionReductionCoeff = pureCompressionReductionCoeff;
//            _parabolaExponent = parabolaExponent;
//            _nRds = nRds;
//            _mxRds = mxRds;
//            _myRds = myRds;
//            _campoIndexes = campoIndexes;
//            CalculateLevelCurves(LEVEL_CURVE_NUMBER, out _levelNRds, out _levelMRds);
//        }

//        /// <summary>
//        /// Get raw points of domains
//        /// </summary>
//        /// <returns></returns>
//        public List<DomainPoint> GetRawPoints()
//        {
//            List<DomainPoint> result;
//            result = new List<DomainPoint>();
//            for (int i = 0; i < _nRds.Count; i++)
//            {
//                for (int j = 0; j < _nRds[i].Count; j++)
//                {
//                    result.Add(new DomainPoint(_nRds[i][j], _mxRds[i][j], _myRds[i][j], _campoIndexes[i][j]));
//                }
//            }
//            return result;
//        }

//        private void CalculateLevelCurves(int deltaNumber,
//                                          out double[] nRds,
//                                          out List<Point2d>[] levelMRds)
//        {
//            const double TOLL = 0.001;
//            double maxNRd, minNRd;
//            double deltaN;
//            int[] startingCount;

//            maxNRd = _nRds[0][0];
//            minNRd = _nRds[0][_nRds[0].Count - 1];
//            deltaN = (maxNRd - minNRd) / deltaNumber;
//            nRds = new double[deltaNumber + 1];
//            levelMRds = new List<Point2d>[deltaNumber + 1];
//            startingCount = new int[_nRds.Count];

//            for (int levelCount = 0; levelCount < deltaNumber + 1; levelCount++)
//            {
//                double nRdLevel;
//                List<Point2d> mRds;
//                nRdLevel = maxNRd - levelCount * deltaN;
//                nRds[levelCount] = nRdLevel;
//                mRds = new List<Point2d>();
//                for (int rotCount = 0; rotCount < _nRds.Count; rotCount++)
//                {
//                    List<double> bufferNRd, bufferMxRd, bufferMyRd;
//                    bufferNRd = new List<double>();
//                    bufferMxRd = new List<double>();
//                    bufferMyRd = new List<double>();
//                    for (int i = startingCount[rotCount]; i < _nRds[rotCount].Count - 1; i++)
//                    {
//                        double nrd, nextNrd;
//                        nrd = _nRds[rotCount][i];
//                        nextNrd = _nRds[rotCount][i + 1];
//                        if (nrd >= nRdLevel - TOLL &&
//                            nextNrd <= nRdLevel + TOLL)
//                        {
//                            double mrdx, mrdy;
//                            if (System.Math.Abs(nrd - nextNrd) < TOLL)
//                            {
//                                mrdx = _mxRds[rotCount][i];
//                                mrdy = _myRds[rotCount][i];
//                            }
//                            else
//                            {
//                                mrdx = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(nrd, nextNrd, _mxRds[rotCount][i], _mxRds[rotCount][i + 1], nRdLevel);
//                                mrdy = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(nrd, nextNrd, _myRds[rotCount][i], _myRds[rotCount][i + 1], nRdLevel);
//                            }

//                            mRds.Add(new Point2d(mrdx, mrdy));
//                            startingCount[rotCount] = i;
//                            break;
//                        }
//                    }
//                }
//                levelMRds[levelCount] = mRds;
//            }
//        }


//        #region ConstantN

//        /// <summary>
//        /// Get the resistance considering nEd=nRd
//        /// </summary>
//        /// <param name="nEd">axial action, >0=traction centered in centroid</param>
//        /// <param name="mxEd">moment around x-x axes, >0 if right handed</param>
//        /// <param name="myEd">moment around y-y axes, >0 if right handed</param>
//        /// <param name="mxRd">resisting moment around x-x axes, >0 if right handed</param>
//        /// <param name="myRd">resisting moment around y-y axes, >0 if right handed</param>
//        /// <param name="cs">safety coefficient</param>
//        public void GetResistanceConstantN(double nEd,
//                                           double mxEd,
//                                           double myEd,
//                                           out double mxRd,
//                                           out double myRd,
//                                           out double cs)
//        {
//            const double TOLL = 0.01;
//            if (nEd > _levelNRds[0] - TOLL)
//            {
//                mxRd = 0;
//                myRd = 0;
//                cs = System.Math.Abs(_levelNRds[0] / nEd);
//            }
//            else if (nEd < _levelNRds[_levelNRds.Length - 1] + TOLL)
//            {
//                mxRd = 0;
//                myRd = 0;
//                cs = System.Math.Abs(_levelNRds[_levelNRds.Length - 1] / nEd);
//            }
//            else
//            {
//                for (int levelIndex = 0; levelIndex < _levelNRds.Length - 1; levelIndex++)
//                {
//                    if (nEd <= _levelNRds[levelIndex] && nEd >= _levelNRds[levelIndex + 1])
//                    {
//                        //Ocio: qui si considera il fatto che la curva di livello può non essere centrata sull'origine
//                        //vedi caso di sezione armata in maniera fortemente asimmetrica con sforzo Ned pari quasi alla resistenza a trazione
//                        //NB: QUESTO CASO PER ESEMPIO IL GELFI LO SBAGLIA!

//                        //Point2d vectorEd;
//                        //Point2d mEdDir;
//                        Vector2d vectorEd;
//                        Vector2d mEdDir;
//                        double mEdLength;

//                        //adjusting mxed and myed i case of wrong datas
//                        if (System.Math.Abs(mxEd) < TOLL && System.Math.Abs(myEd) < TOLL)
//                        {
//                            mxEd = 10;
//                        }
//                        //vectorEd = new Point2d(mxEd, myEd);
//                        //mEdLength = Geom.GetLength(vectorEd);
//                        //mEdDir = vectorEd / mEdLength;

//                        vectorEd = new Vector2d(mxEd, myEd);
//                        mEdLength = vectorEd.Length;
//                        mEdDir = new Vector2d(vectorEd.X / mEdLength, vectorEd.Y / mEdLength);
//                        //found level
//                        //getting upper and lower mRds
//                        BoundingBox1d bboxUpper, bboxLower, bbox;
//                        bboxUpper = GetIntersectionBBox(_levelMRds[levelIndex], mEdDir);
//                        bboxLower = GetIntersectionBBox(_levelMRds[levelIndex + 1], mEdDir);

//                        bbox = new BoundingBox1d();
//                        //Gestisco situazioni anonale in cui il superiore o l'inferiore non hanno intersezione
//                        if (!(bboxUpper.IsEmpty || bboxLower.IsEmpty))
//                        {
//                            double alfa;
//                            alfa = System.Math.Abs((nEd - _levelNRds[levelIndex]) / (_levelNRds[levelIndex + 1] - _levelNRds[levelIndex]));
//                            bbox.Update(bboxUpper.Min + alfa * (bboxLower.Min - bboxUpper.Min));
//                            bbox.Update(bboxUpper.Max + alfa * (bboxLower.Max - bboxUpper.Max));
//                            GetCs(bbox, mEdDir, mEdLength, out cs, out mxRd, out myRd);
//                        }
//                        else
//                        {
//                            mxRd = 0;
//                            myRd = 0;
//                            cs = 0;
//                        }
//                        return;
//                    }
//                }
//                throw new NotSupportedException("Why are you here??");
//            }
//        }

//        #endregion

//        #region ConstMxMy

//        /// <summary>
//        /// Get the resistance considering mxed/myed=mxrd/myrd
//        /// </summary>
//        /// <param name="nEd">axial action, >0=traction centered in barycentre</param>
//        /// <param name="mxEd">moment around x-x axes, >0 if right handed</param>
//        /// <param name="myEd">moment around y-y axes, >0 if right handed</param>
//        /// <param name="nRd">resisting axial force, >0 =traction</param>
//        /// <param name="mxRd">resisting moment around x-x axes, >0 if right handed</param>
//        /// <param name="myRd">resisting moment around y-y axes, >0 if right handed</param>
//        /// <param name="cs">safety coefficient</param>
//        public void GetResistanceConstMxMy(double nEd,
//                                            double mxEd,
//                                            double myEd,
//                                            out double nRd,
//                                            out double mxRd,
//                                            out double myRd,
//                                            out double cs)
//        {
//            const double TOLL = 0.01;
//            if (System.Math.Abs(mxEd) < TOLL && System.Math.Abs(myEd) < TOLL)
//            {
//                //moving on N Axes
//                BoundingBox1d nRdBBox;
//                //Point2d nEdVect, nEdDir;
//                Point2d nEdVect;
//                Vector2d nEdDir;
//                double nedLength;
//                double buffer;

//                if (System.Math.Abs(nEd) < TOLL)
//                {
//                    nEd = 1;
//                }
//                //nEdVect = new Point2d(nEd, 0);
//                //nedLength = System.Math.Abs(nEd);
//                //nEdDir = nEdVect / nedLength;
//                nEdVect = new Point2d(nEd, 0);
//                nedLength = System.Math.Abs(nEd);
//                nEdDir = new Vector2d(nEdVect);
//                nEdDir.Unitize();

//                nRdBBox = new BoundingBox1d();
//                if (nEd < 0)
//                {
//                    nRdBBox.Update(-_levelNRds[0]);
//                    nRdBBox.Update(-_levelNRds[_levelNRds.Length - 1]);
//                }
//                else
//                {
//                    nRdBBox.Update(_levelNRds[0]);
//                    nRdBBox.Update(_levelNRds[_levelNRds.Length - 1]);
//                }

//                GetCs(nRdBBox, nEdDir, nedLength, out cs, out nRd, out buffer);

//                mxRd = 0;
//                myRd = 0;
//            }
//            else
//            {
//                Point2d mEd;
//                Vector2d mEdDir;
//                double mEdLength;
//                List<double> intersectedNRds;
//                List<BoundingBox1d> levelBBoxs;

//                //cutting in medDir
//                mEd = new Point2d(mxEd, myEd);
//                mEdLength = new Vector2d(mEd).Length;
//                mEdDir = new Vector2d(mEd) / mEdLength;
//                intersectedNRds = new List<double>();
//                levelBBoxs = new List<BoundingBox1d>();
//                for (int i = 0; i < _levelNRds.Length; i++)
//                {
//                    BoundingBox1d levelBBox;
//                    levelBBox = GetIntersectionBBox(_levelMRds[i], mEdDir);
//                    if (!levelBBox.IsEmpty)
//                    {
//                        intersectedNRds.Add(_levelNRds[i]);
//                        levelBBoxs.Add(levelBBox);
//                    }
//                }

//                if (intersectedNRds.Count == 0)
//                {
//                    //non c'è intersezione.
//                    mxRd = 0;
//                    myRd = 0;
//                    nRd = 0;
//                    cs = 0;
//                }
//                else
//                {
//                    List<Point2d> mxyPolygon;
//                    //building polygon

//                    mxyPolygon = new List<Point2d>();
//                    for (int i = 0; i < intersectedNRds.Count; i++)
//                    {
//                        mxyPolygon.Add(new Point2d(levelBBoxs[i].Max, intersectedNRds[i]));
//                    }
//                    for (int i = intersectedNRds.Count - 1; i >= 0; i--)
//                    {
//                        mxyPolygon.Add(new Point2d(levelBBoxs[i].Min, intersectedNRds[i]));
//                    }

//                    //moving mEd to costant mx/my plane
//                    Point2d vEd;
//                    Vector2d vEdDir;
//                    double vEdLength;
//                    //vEd = new Point2d(Geom.GetLength(mEd), nEd);
//                    //vEdLength = Geom.GetLength(vEd);
//                    //vEdDir = vEd / vEdLength;
//                    vEd = new Point2d(new Vector2d(mEd).Length, nEd);
//                    vEdLength = new Vector2d(vEd).Length;
//                    vEdDir = new Vector2d(vEd);
//                    vEdDir.Unitize();
//                    //intersection in constant mx/my plane
//                    BoundingBox1d intersection;
//                    intersection = GetIntersectionBBox(mxyPolygon, vEdDir);

//                    if (intersection.IsEmpty)
//                    {
//                        mxRd = 0;
//                        myRd = 0;
//                        nRd = 0;
//                        cs = 0;
//                    }
//                    else
//                    {
//                        double xRd;
//                        GetCs(intersection, vEdDir, mEdLength, out cs, out xRd, out nRd);
//                        mxRd = xRd * mEdDir.X;
//                        myRd = xRd * mEdDir.Y;
//                    }
//                }

//            }
//        }

//        #endregion

//        private BoundingBox1d GetIntersectionBBox(List<Point2d> polygon,
//                                                  Vector2d dir)
//        {
//            BoundingBox1d result;
//            Vector2d nextmRdDir = null;
//            Point2d nextMRd = null;
//            result = new BoundingBox1d();


//            for (int i = 0; i < polygon.Count; i++)
//            {
//                const double PROD_TOLL = 1e-6;
//                Vector2d mRdDir = new Vector2d(0,0);
//                Point2d mRd;
//                double prodI;
//                double prodNext;
//                double lengthNextMrd = 0;
//                if (nextmRdDir != null)
//                {
//                    mRd = nextMRd;
//                    mRdDir = nextmRdDir;
//                }
//                else
//                {
//                    double lengthMRd;
//                    mRd = polygon[i];
//                    lengthMRd = new Vector2d(mRd).Length;
//                    if (lengthMRd < PROD_TOLL)
//                    {
//                        mRdDir = new Vector2d(Point2d.Origin);
//                    }
//                    else
//                    {
//                        mRdDir = mRd / lengthMRd;
//                    }
//                }
//                if (i == polygon.Count - 1)
//                {
//                    nextMRd = polygon[0];
//                }
//                else
//                {
//                    nextMRd = polygon[i + 1];
//                }
//                //lengthNextMrd = Geom.GetLength(nextMRd); 
//                lengthNextMrd = new Vector2d(nextMRd).Length;
//                if (lengthNextMrd < PROD_TOLL)
//                {
//                    nextmRdDir = new Vector2d(Point2d.Origin);
//                }
//                else
//                {
//                    nextmRdDir = nextMRd / lengthNextMrd;            
//                    //nextmRdDir.Unitize();
//                }

//                //searched int points have a point at right and one at left
//                //prodI = dir ^ mRdDir;
//                //prodNext = dir ^ nextmRdDir;
//                prodI = dir ^ mRdDir;
//                prodNext = dir ^ nextmRdDir;

//                if (System.Math.Abs(prodI) < PROD_TOLL)
//                {
//                    result.Update(mRd * dir);
//                }
//                else if (System.Math.Abs(prodNext) < PROD_TOLL)
//                {
//                    result.Update(nextMRd * dir);
//                }
//                else if (prodI * prodNext < 0)
//                {
//                    Line2d line1 = new Line2d(Point2d.Origin, new Point2d(dir.X, dir.Y));
//                    Line2d line2 = new Line2d(new Point2d(mRd.X, mRd.Y), new Point2d(nextMRd.X, nextMRd.Y));
//                    //if (!Geom.GetLineIntersection(Point2d.Origin, dir, mRd, nextMRd, out Point2d inters))
//                    if (!line1.GetIntersection(line2, out Point2d inters))
//                    {
//                        throw new NotSupportedException("Unable to find mRd");
//                    }
//                    result.Update(inters * dir);
//                }
//            }
//            return result;
//        }

//        private void GetCs(BoundingBox1d bbox,
//                           Vector2d dirEd,
//                           double lengthEd,
//                           out double cs,
//                           out double xRd,
//                           out double yRd)
//        {
//            Point2d result;
//            if (bbox.Max * bbox.Min <= 0)
//            {
//                //Situazione standard. origine contenuta in bbox. il max è il punto resistente
//                result = bbox.Max * (new Point2d(dirEd.X, dirEd.Y));
//                //cs = Math.Abs(bbox.Max) / lengthEd;
//                cs = lengthEd / Math.Abs(bbox.Max);
//            }
//            else
//            {
//                //il bbox può essere interamente positivo o interamente negativo
//                if (bbox.Max <= 0)
//                {
//                    //completamente fuori anche come direzione
//                    result = Point2d.Origin;
//                    cs = 0;
//                }
//                else
//                {
//                    //intersezione tra vettore sollecitante e dominio esterna a origine!
//                    //bbox nella stessa direzione del vettore sollecitante
//                    if (System.Math.Abs(lengthEd - bbox.Min) > System.Math.Abs(lengthEd - bbox.Max))
//                    {
//                        //più vicino a max
//                        result = bbox.Max * (new Point2d(dirEd.X, dirEd.Y));
//                        //cs = bbox.Max / lengthEd;
//                        cs = bbox.Max / lengthEd;
//                    }
//                    else
//                    {
//                        //più vicino a min
//                        result = bbox.Min * (new Point2d(dirEd.X, dirEd.Y));
//                        //cs = lengthEd / bbox.Min;
//                        cs =  bbox.Min / lengthEd;
//                    }
//                }
//            }
//            xRd = result.X;
//            yRd = result.Y;
//        }

//        public class DomainPoint
//        {
//            public readonly double NRd;
//            public readonly double MxRd;
//            public readonly double MyRd;
//            public readonly int CampoIndex;

//            internal DomainPoint(double nRd,
//                                 double mxRd,
//                                 double myRd,
//                                 int campoIndex)
//            {
//                NRd = nRd;
//                MxRd = mxRd;
//                MyRd = myRd;
//                CampoIndex = campoIndex;
//            }
//        }
//    }
//}
