using GPC.Geometry;
using GPC.Model.Sections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.EuroCode
{
    public class Class4Section
    {
        List<ECPlate> _plates;

        public Class4Section(Section sect)
        {
            Type typeSect = sect.GetType();
            if (typeSect == typeof(SectionH))
            {
                SectionH sec = (SectionH)sect;
                ECPlate bottomLeft = new ECPlate(sec.Plates[0], sec.ThicknessWeb / 2.0, 0);
                ECPlate bottomRight = new ECPlate(sec.Plates[1], sec.ThicknessWeb / 2.0, 0);
                ECPlate Web = new ECPlate(sec.Plates[2], 0, 0);
                ECPlate TopLeft = new ECPlate(sec.Plates[3], sec.ThicknessWeb / 2.0, 0);
                ECPlate TopRight = new ECPlate(sec.Plates[4], sec.ThicknessWeb / 2.0, 0);
                
                _plates.Add(bottomLeft);
                _plates.Add(bottomRight);
                _plates.Add(Web);
                _plates.Add(TopLeft);
                _plates.Add(TopRight);
            } else
            {
                throw new Exception("Section 4 of this type not yet supported");
            }
        }

        public List<ECPlate> Plates => _plates;

        public void Calc(double N, double My, double Mz)
        {
            double Aeffk = 0;;
            int iter = 0;
            while (Math.Abs(Aeff - Aeffk) > 0.005 * Aeffk && iter <= 10)
            {
                iter++;

                Aeffk = Aeff;
                for (int i = 0; i < Plates.Count; i++)
                {
                    double aEff = Aeff;
                    double j1Eff = J1eff;
                    double j2Eff = J2eff;

                    //calculation sigma in initial point always active
                    Point2d p0 = Plates[i].FirstPointActive;
                    double sigmaInitialPoint = N / aEff + My / j2Eff * (Centroid.Y - p0.Y) + Mz / j1Eff * (Centroid.X - p0.X);

                    //calculation of sigma in the active point
                    Point2d p1 = Plates[i].LastPointActive;
                    double sigmaLastPointActive = N / aEff + My / j2Eff * (Centroid.Y - p1.Y) + Mz / j1Eff * (Centroid.X - p1.X);
                    Plates[i].SetSigma(sigmaInitialPoint, sigmaLastPointActive);
                }
            }
        }

        public double Aeff
        {
            get
            {
                double area = 0;
                for (int i = 0; i < _plates.Count; i++)
                {
                    area = area + _plates[i].Aeff;
                }
                return area;
            }
        }

        public Point2d Centroid
        {
            get
            {
                double area = Aeff;
                double Sx = 0;
                double Sy = 0;

                for (int i = 0; i < _plates.Count; i++)
                {
                    Sx = Sx + _plates[i].Aeff * _plates[i].CentroidEff.X;
                    Sy = Sy + _plates[i].Aeff * _plates[i].CentroidEff.Y;
                }

                return new Point2d(Sx/area, Sy/area);
            }
        }

        public double J2eff
        {
            get
            {
                double J = 0;
                Point2d centroid = Centroid;

                for (int i = 0; i < _plates.Count; i++)
                {
                    J = J + _plates[i].JyEffCentroid + _plates[i].Aeff * Math.Pow(_plates[i].CentroidEff.Y - centroid.Y,2.0);
                }

                return J;
            }
        }

        public double Weff2
        {
            get {
                double Weff2 = 0;
                double Jeff2 = J2eff;
                double yg = Centroid.Y;
                for (int i = 0; i < _plates.Count; i++)
                {
                    if (_plates[i].FirstPointActive != null)
                    {
                        Weff2 = Math.Max(Weff2, Jeff2 / Math.Abs(_plates[i].FirstPointActive.Y - yg));
                    }
                    if (_plates[i].LastPointActive != null)
                    {
                        Weff2 = Math.Max(Weff2, Jeff2 / Math.Abs(_plates[i].LastPointActive.Y - yg));
                    }
                }
                return Weff2;
            }
        }

        public double J1eff
        {
            get
            {
                double J = 0;
                Point2d centroid = Centroid;

                for (int i = 0; i < _plates.Count; i++)
                {
                    J = J + _plates[i].JzEffCentroid + _plates[i].Aeff * Math.Pow(_plates[i].CentroidEff.X - centroid.X, 2.0);
                }

                return J;
            }
        }

        public double Weff1
        {
            get
            {
                double Weff1 = 0;
                double Jeff1 = J1eff;
                double xg = Centroid.X;
                for (int i = 0; i < _plates.Count; i++)
                {
                    if (_plates[i].FirstPointActive != null)
                    {
                        Weff1 = Math.Max(Weff1, Jeff1 / Math.Abs(_plates[i].FirstPointActive.X - xg));
                    }
                    if (_plates[i].LastPointActive != null)
                    {
                        Weff1 = Math.Max(Weff1, Jeff1 / Math.Abs(_plates[i].LastPointActive.X - xg));
                    }
                }
                return Weff1;
            }
        }
    }

    public class ECPlate
    {
        double _t;
        double _B;
        Plate.TypePlate _type;

        double _removeLengthSide1;
        double _removeLengthSide2;

        double _fy;

        Point2d _initialPoint;
        Point2d _endPoint;

        Point2d _pInitialEff1;
        Point2d _pFinalEff1;

        Point2d _pInitialEff2;
        Point2d _pFinalEff2;

        public ECPlate(double t, double x0, double y0, double x1, double y1, double fy, Plate.TypePlate typePlate, double removeLengthSide1, double removeLengthSide2) : this(t, new Point2d(x0, y0), new Point2d(x1, y1), fy, typePlate, removeLengthSide1, removeLengthSide2)
        {
        }
        public ECPlate(double t, Point2d initialPoint, Point2d endPoint, double fy, Plate.TypePlate typePlate, double removeLengthSide1, double removeLengthSide2)
        {
            _initialPoint = new Point2d(initialPoint);
            _endPoint = new Point2d(endPoint);

            _t = t;
            _type = typePlate;
            _B = Math.Sqrt(Math.Pow(_endPoint.X - _initialPoint.X, 2.0) + Math.Pow(_endPoint.Y - _initialPoint.Y, 2.0));
            _fy = fy;

            _removeLengthSide1 = removeLengthSide1;
            _removeLengthSide2 = removeLengthSide2;

            if (_type == Plate.TypePlate.inner)
            {
                _pInitialEff1 = _initialPoint;
                _pFinalEff1 = new Point2d((_endPoint.X - _initialPoint.X) / 2.0, (_endPoint.Y - _initialPoint.Y) / 2.0);

                _pFinalEff2 = _pFinalEff1;
                _pInitialEff2 = _endPoint;
            }
            else
            {
                _pInitialEff1 = _initialPoint;
                _pFinalEff1 = _endPoint;

                _pFinalEff2 = null;
                _pInitialEff2 = null;
            }
        }

        public ECPlate(Plate p, double removeLengthSide1, double removeLengthSide2) : this(p.Thickness, p.InitialPoint, p.EndPoint, p.Fyk, p.GetTypePlate, removeLengthSide1, removeLengthSide2)
        {
            
        }

        protected Point2d[] CentroidsEff
        {
            get
            {
                Point2d[] centroids = new Point2d[2];
                centroids[0] = new Point2d((_pInitialEff1.X + _pFinalEff1.X) / 2.0, (_pInitialEff1.Y + _pFinalEff1.Y) / 2.0);
                if (_pInitialEff2 != null)
                {
                    centroids[1] = new Point2d((_pInitialEff2.X + _pFinalEff2.X) / 2.0, (_pInitialEff2.Y + _pFinalEff2.Y) / 2.0);
                }
                return centroids;
            }
        }

        public Point2d CentroidEff
        {
            get
            {
                Point2d[] centroids = CentroidsEff;
                double[] bEff = Beff;

                double xg;
                double yg;
                if (_pInitialEff2 != null)
                {
                    xg = (_t * bEff[0] * centroids[0].X + _t * bEff[1] * centroids[1].X) / (_t * bEff[0] + _t * bEff[1]);
                    yg = (_t * bEff[0] * centroids[0].Y + _t * bEff[1] * centroids[1].Y) / (_t * bEff[0] + _t * bEff[1]);
                }
                else
                {
                    xg = (_t * bEff[0] * centroids[0].X) / (_t * bEff[0]);
                    yg = (_t * bEff[0] * centroids[0].Y) / (_t * bEff[0]);
                }

                Point2d centroid = new Point2d(xg, yg);
                return centroid;
            }
        }

        public double Aeff
        {
            get
            {
                double[] beff = Beff;
                return _t * (beff[0] + beff[1]);
            }
        }

        public double J2EffCentroid
        {
            get
            {
                double[] beff = Beff;
                double J1 = 1.0 / 12.0 * _t * Math.Pow(beff[0], 3.0);
                double J2 = 1.0 / 12.0 * _t * Math.Pow(beff[1], 3.0);

                Point2d centroid0 = CentroidEff;

                Point2d[] centroids = CentroidsEff;
                double d1 = Math.Sqrt(Math.Pow(centroid0.X - centroids[0].X, 2.0) + Math.Pow(centroid0.Y - centroids[0].Y, 2.0));
                double d2;
                if (_pInitialEff2 != null)
                {
                    d2 = Math.Sqrt(Math.Pow(centroid0.X - centroids[1].X, 2.0) + Math.Pow(centroid0.Y - centroids[1].Y, 2.0));
                }
                else
                {
                    d2 = 0;
                }

                return J1 + (beff[0] * _t) * Math.Pow(d1, 2.0) + J2 + (beff[1] * _t) * Math.Pow(d2, 2.0);
            }
        }

        public double JzEffCentroid
        {
            get
            {
                double dy = _endPoint.Y - _initialPoint.Y;
                double dx = _endPoint.X - _initialPoint.X;

                if (dy != 0 && dx == 0) //vertical plate
                {
                    return J1EffCentroid;
                }
                else if (dx != 0 && dy == 0) //horizontal plate
                {
                    return J2EffCentroid;
                }
                else
                {
                    throw new Exception("Oblique plate not yet supported");
                }
            }
        }

        public double JyEffCentroid
        {
            get
            {
                double dy = _endPoint.Y - _initialPoint.Y;
                double dx = _endPoint.X - _initialPoint.X;

                if (dy != 0 && dx == 0) //vertical plate
                {
                    return J2EffCentroid;
                }
                else if (dx != 0 && dy == 0) //horizontal plate
                {
                    return J1EffCentroid;
                }
                else
                {
                    throw new Exception("Oblique plate not yet supported");
                }
            }
        }

        public double J1EffCentroid
        {
            get
            {
                double[] beff = Beff;
                double J1 = 1.0 / 12.0 * beff[0] * Math.Pow(_t, 3.0);
                double J2 = 1.0 / 12.0 * beff[1] * Math.Pow(_t, 3.0);

                return J1 + J2;
            }
        }

        public double[] Beff
        {
            get
            {
                double[] beff = new double[2];
                beff[0] = Math.Sqrt(Math.Pow(_pFinalEff1.X - _pInitialEff1.X, 2.0) + Math.Pow(_pFinalEff1.Y - _pInitialEff1.Y, 2.0));
                if (_pInitialEff2 != null)
                {
                    beff[1] = Math.Sqrt(Math.Pow(_pFinalEff2.X - _pInitialEff2.X, 2.0) + Math.Pow(_pFinalEff2.Y - _pInitialEff2.Y, 2.0));
                }
                return beff;
            }
        }

        public double B
        {
            get
            {
                return _B;
            }
        }

        protected double GetKSigma(double psi, double sigma0, double sigmaX)
        {
            if (psi > 1)
            {
                throw new Exception("something wrong with psi");
            }

            if (_type == Plate.TypePlate.inner)
            {
                if (psi == 1)
                {
                    return 4;
                }
                else if (psi > 0 && psi < 1)
                {
                    return 8.2 / (1.05 + psi);
                }
                else if (psi == 0)
                {
                    return 7.81;
                }
                else if (psi < 0 && psi > -1)
                {
                    return 7.81 - 6.29 * psi + 9.78 * psi * psi;
                }
                else if (psi == -1)
                {
                    return 23.9;
                }
                else if (psi > -3 && psi < -1)
                {
                    return 5.98 * (1 - psi) * (1 - psi);
                }
                else
                {
                    throw new Exception("out of range");
                }
            }
            else //Outer
            {
                if (sigma0 > sigmaX)
                {
                    if (psi == 1)
                    {
                        return 0.43;
                    }
                    else if (psi == 0)
                    {
                        return 0.57;
                    }
                    else if (psi == -1)
                    {
                        return 0.85;
                    }
                    else if (psi >= -3 && psi <= 1)
                    {
                        return 0.57 - 0.21 * psi + 0.07 * psi * psi;
                    }
                    else
                    {
                        throw new Exception("out of range psi");
                    }
                }
                else
                {
                    if (psi == 1)
                    {
                        return 0.43;
                    }
                    else if (psi > 0 && psi < 1)
                    {
                        return 0.578 / (psi + 0.34);
                    }
                    else if (psi == 0)
                    {
                        return 1.7;
                    }
                    else if (psi < 0 && psi > -1)
                    {
                        return 1.7 - 5 * psi + 17.1 * psi * psi;
                    }
                    else if (psi == -1)
                    {
                        return 23.8;
                    }
                    else
                    {
                        throw new Exception("out of range psi");
                    }
                }
            }
        }

        protected double GetLambdaP(double ksigma, double fy)
        {
            double epsilon = Math.Sqrt(235 / fy);
            double b = _B - _removeLengthSide1 - _removeLengthSide2;
            return b / _t / (28.4 * epsilon * Math.Sqrt(ksigma));
        }

        protected void CalcBEff(double lambdaP, double psi, double sigmaX0, double sigmaXvar)
        {
            if (_type == Plate.TypePlate.inner)
            {
                double lambdaPLimit = 0.5 + Math.Sqrt(0.085 - 0.055 * psi);
                if (lambdaP <= lambdaPLimit)
                {
                    double rho = 1;
                    //-> no change
                }
                else
                {
                    double rho = Math.Min((lambdaP - 0.055 * (3 + psi)) / (lambdaP * lambdaP), 1);
                    if (psi == 1)
                    {
                        double beff = rho * (_B - _removeLengthSide1 - _removeLengthSide2);
                        double beff1 = 0.5 * beff;
                        double beff2 = 0.5 * beff;

                        double xP = (_endPoint.X - _initialPoint.X) / _B * (beff1 + _removeLengthSide1) + _initialPoint.X;
                        double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff1 + _removeLengthSide1) + _initialPoint.Y;
                        _pInitialEff1 = _initialPoint;
                        _pFinalEff1 = new Point2d(xP, yP);

                        xP = -(_endPoint.X - _initialPoint.X) / _B * (beff2 + _removeLengthSide2) + _endPoint.X;
                        yP = -(_endPoint.Y - _initialPoint.Y) / _B * (beff2 + _removeLengthSide2) + _endPoint.Y;
                        _pInitialEff2 = _endPoint;
                        _pFinalEff2 = new Point2d(xP, yP);
                    }
                    else if (psi >= 0 && psi < 1)
                    {
                        double beff = rho * (_B - _removeLengthSide1 - _removeLengthSide2);
                        double beff1 = (2.0 / (5.0 - psi)) * beff;
                        double beff2 = beff - beff1;

                        if (sigmaX0 < sigmaXvar)
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff1 + _removeLengthSide1) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff1 + _removeLengthSide1) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            xP = -(_endPoint.X - _initialPoint.X) / _B * (beff2 + _removeLengthSide2) + _endPoint.X;
                            yP = -(_endPoint.Y - _initialPoint.Y) / _B * (beff2 + _removeLengthSide2) + _endPoint.Y;
                            _pInitialEff2 = _endPoint;
                            _pFinalEff2 = new Point2d(xP, yP);
                        }
                        else
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff2 + _removeLengthSide2) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff2 + _removeLengthSide2) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            xP = -(_endPoint.X - _initialPoint.X) / _B * (beff1 + _removeLengthSide1) + _endPoint.X;
                            yP = -(_endPoint.Y - _initialPoint.Y) / _B * (beff1 + _removeLengthSide1) + _endPoint.Y;
                            _pInitialEff2 = _endPoint;
                            _pFinalEff2 = new Point2d(xP, yP);
                        }
                    }
                    else if (psi < 0)
                    {
                        double bT = (_B - _removeLengthSide1 - _removeLengthSide2) / (1.0 + Math.Abs(psi)) * Math.Abs(psi);
                        double bC = (_B - _removeLengthSide1 - _removeLengthSide2) - bT;

                        double beff = rho * bC;
                        double beff1 = 0.4 * beff;
                        double beff2 = 0.6 * beff;

                        if (sigmaX0 < sigmaXvar)
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff1 + _removeLengthSide1) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff1 + _removeLengthSide1) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            xP = -(_endPoint.X - _initialPoint.X) / _B * (beff2 + bT + _removeLengthSide2) + _endPoint.X;
                            yP = -(_endPoint.Y - _initialPoint.Y) / _B * (beff2 + bT + _removeLengthSide2) + _endPoint.Y;
                            _pInitialEff2 = _endPoint;
                            _pFinalEff2 = new Point2d(xP, yP);
                        }
                        else
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff2 + bT + _removeLengthSide2) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff2 + bT + _removeLengthSide2) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            xP = -(_endPoint.X - _initialPoint.X) / _B * (beff1 + _removeLengthSide1) + _endPoint.X;
                            yP = -(_endPoint.Y - _initialPoint.Y) / _B * (beff1 + _removeLengthSide1) + _endPoint.Y;
                            _pInitialEff2 = _endPoint;
                            _pFinalEff2 = new Point2d(xP, yP);
                        }
                    }
                    else
                    {
                        throw new Exception("psi not supported");
                    }
                }
            }
            else if (_type == Plate.TypePlate.outer)
            {
                if (lambdaP <= 0.748)
                {
                    double rho = 1;
                    //--> no changes
                }
                else
                {
                    double rho = Math.Min((lambdaP - 0.188) / (lambdaP * lambdaP), 1);
                    if (psi >= 0 && psi <= 1)
                    {
                        double beff = rho * (_B - _removeLengthSide1 - _removeLengthSide2);
                        double xP = (_endPoint.X - _initialPoint.X) / _B * (beff + _removeLengthSide1) + _initialPoint.X;
                        double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff + _removeLengthSide1) + _initialPoint.Y;
                        _pInitialEff1 = _initialPoint;
                        _pFinalEff1 = new Point2d(xP, yP);

                        _pInitialEff2 = null;
                        _pFinalEff2 = null;
                    }
                    else if (psi < 0)
                    {
                        double bT = (_B - _removeLengthSide1 - _removeLengthSide2) / (1.0 + Math.Abs(psi)) * Math.Abs(psi);
                        double bC = (_B - _removeLengthSide1 - _removeLengthSide2) - bT;

                        double beff = rho * bC;
                        if (sigmaX0 < 0 && sigmaXvar < 0)
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff + _removeLengthSide1) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff + _removeLengthSide1) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            _pInitialEff2 = null;
                            _pFinalEff2 = null;
                        }
                        else if (sigmaX0 > 0 && sigmaXvar < 0)
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (bT + beff + _removeLengthSide1) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (bT + beff + _removeLengthSide1) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            _pInitialEff2 = null;
                            _pFinalEff2 = null;
                        }
                        else if (sigmaX0 < 0 && sigmaXvar > 0)
                        {
                            double xP = (_endPoint.X - _initialPoint.X) / _B * (beff + _removeLengthSide1) + _initialPoint.X;
                            double yP = (_endPoint.Y - _initialPoint.Y) / _B * (beff + _removeLengthSide1) + _initialPoint.Y;
                            _pInitialEff1 = _initialPoint;
                            _pFinalEff1 = new Point2d(xP, yP);

                            xP = -(_endPoint.X - _initialPoint.X) / _B * (bT + _removeLengthSide2) + _endPoint.X;
                            yP = -(_endPoint.Y - _initialPoint.Y) / _B * (bT + _removeLengthSide2) + _endPoint.Y;

                            _pInitialEff2 = _endPoint;
                            _pFinalEff2 = new Point2d(xP, yP);
                        }
                        else
                        {
                            throw new Exception("distribution stress not recognized");
                        }
                    }
                }
            }
            else
            {
                throw new Exception("type of plate not supported");
            }
        }

        public void SetSigma(double sigma0, double sigma2)
        {
            if (sigma0 >= 0 && sigma2 >= 0)
            {
                //reset -> plate completely effective
                if (_type == Plate.TypePlate.inner)
                {
                    _pInitialEff1 = _initialPoint;
                    _pFinalEff1 = new Point2d((_endPoint.X - _initialPoint.X) / 2.0, (_endPoint.Y - _initialPoint.Y) / 2.0);

                    _pFinalEff2 = _pFinalEff1;
                    _pInitialEff2 = _endPoint;
                }
                else
                {
                    _pInitialEff1 = _initialPoint;
                    _pFinalEff1 = _endPoint;

                    _pFinalEff2 = null;
                    _pInitialEff2 = null;
                }
                return;
            }

            double sigmaMin = Math.Min(sigma0, sigma2);
            double sigmaMax = Math.Max(sigma0, sigma2);

            //sigma > 0 compression
            double psi = -sigmaMax / -sigmaMin;

            double ksigma = GetKSigma(psi, sigma0, sigma2);

            double lambdap = GetLambdaP(ksigma, _fy);

            CalcBEff(lambdap, psi, sigma0, sigma2);
        }

        public Point2d FirstPointActive
        {
            get
            {
                if (_removeLengthSide1 == 0)
                {
                    return _initialPoint;
                } else
                {
                    //calc coordinates
                    var delta = _endPoint - _initialPoint;
                    if (delta.X == 0 && delta.Y > 0) //vertical
                    {
                        return new Point2d(_initialPoint.X, _initialPoint.Y + _removeLengthSide1);
                    } else if (delta.X == 0 && delta.Y < 0) //vertical
                    {
                        return new Point2d(_initialPoint.X, _initialPoint.Y - _removeLengthSide1);
                    } else if (delta.X > 0 && delta.Y == 0) //horiz
                    {
                        return new Point2d(_initialPoint.X + _removeLengthSide1, _initialPoint.Y);
                    } else if (delta.X < 0 && delta.Y == 0) //horiz
                    {
                        return new Point2d(_initialPoint.X - _removeLengthSide1, _initialPoint.Y);
                    }
                    else { //obliqual
                        double angle = Math.Atan(delta.Y / delta.X);
                        
                        return new Point2d(_initialPoint.X + _removeLengthSide1 * Math.Cos(angle), _initialPoint.Y + _removeLengthSide1 * Math.Sin(angle));
                    } 
                }
            }
        }
        public Point2d LastPointActive
        {
            get
            {
                if (_pInitialEff2 != null)
                {
                    if (_pInitialEff2 == _endPoint)
                    {
                        //calc coordinates
                        var delta = _endPoint - _initialPoint;
                        if (delta.X == 0 && delta.Y > 0) //vertical
                        {
                            return new Point2d(_endPoint.X, _endPoint.Y - _removeLengthSide2);
                        }
                        else if (delta.X == 0 && delta.Y < 0) //vertical
                        {
                            return new Point2d(_endPoint.X, _endPoint.Y + _removeLengthSide2);
                        }
                        else if (delta.X > 0 && delta.Y == 0) //horiz
                        {
                            return new Point2d(_endPoint.X - _removeLengthSide2, _endPoint.Y);
                        }
                        else if (delta.X < 0 && delta.Y == 0) //horiz
                        {
                            return new Point2d(_endPoint.X + _removeLengthSide2, _endPoint.Y);
                        }
                        else
                        { //obliqual
                            double angle = Math.Atan(delta.Y / delta.X);

                            return new Point2d(_endPoint.X - _removeLengthSide2 * Math.Cos(angle), _endPoint.Y - _removeLengthSide2 * Math.Sin(angle));
                        }
                    }
                    else
                    {
                        return _pInitialEff2;
                    }
                } else if (_pFinalEff1 != null)
                {
                    return _pFinalEff1;
                } else
                {
                    throw new Exception("point?");
                }
            }
        }
    }
}
