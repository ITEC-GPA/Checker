using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.PanelsStability
{
    public class UnstiffenedPanel
    {        /// Local Coordinates Panel
             ///             
             /// (0, hw)  ____   (+tw, + hw)
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |        
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             ///         |    |
             /// (0, 0)  |____| (+tw, 0)
             


        #region Variables
        protected double _b;
        protected double _t;
        protected double _phi;
        protected double _chic;
        protected double _csi;
        protected double _rho;
        protected double _rhoc;
        protected double _ksigma;
        protected double _lambdap;
        protected double _lambdapLIM;
        protected double _Sigmacrp;
        protected double _Sigmacrc;
        protected double _SigmaE;
        protected double _Ldiaf;
        protected double _FiInst;
        protected double _lambdac;
        protected double _alfae;

        protected bool _IsUnstiffened;
        protected PlateSupportType _plateSupportType;

        /// TOP: compression fy
        protected double _ytop;
        protected double _ybottom;
        protected double _be1;
        protected double _be2;
        protected double _btop;
        protected double _bbottom;
        protected double _teff;
        protected double _bLIM;
        protected double _beff;

        protected double _Atotgross;
        protected double _Atoteff;
        protected double _CU;

        protected double _fy;
        protected double _E;
        protected double _ni;
        protected Code _code;
        #endregion

        #region Properties
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
        public double lambdapLIM
        {
            get => _lambdapLIM;
            private set => _lambdapLIM = value;
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
        public double SigmaE
        {
            get => _SigmaE;
            private set => _SigmaE = value;
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
        public double bLIM
        {
            get => _bLIM;
            private set => _bLIM = value;
        }
        public double beff
        {
            get => _beff;
            private set => _beff = value;
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

        public PlateSupportType PlateSupportType
        {
            get => _plateSupportType;
            private set => _plateSupportType = value;
        }
        
        #endregion

        #region Public Constructors
        public UnstiffenedPanel(Code __code, double __t, double __b, double __phi, double __ksigma, double __fy = 355, double __E = 210000, double __ni = 0.3, double __Ldiaf = 1000, 
            PlateSupportType plateSupportType = PlateSupportType.Internal, bool tipInCompression = true)
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
            _plateSupportType = plateSupportType;
            _IsUnstiffened = true;

            CalcPanelProperties(tipInCompression);
        }
        #endregion

        #region FIELD_DECONSTRUCTOR
        #endregion

        #region FIELD_METHODS
        private void CalcPanelProperties(bool tipInCompression = true)
        {
            _beff = _b;
            _bLIM = 0;
            double pi = Math.PI;
            double epsilon = Math.Sqrt(235 / _fy);
            _SigmaE = Math.Pow(Math.PI, 2.0) *_E /(12.0 * (1 - _ni *_ni)) * Math.Pow((_t / _b), 2.0);

            _rho = 1;
            _rhoc = 1;
            _lambdap = 1;
            _be1 = 0;
            _be2 = 0;
            _ytop = 0;
            _ybottom = 0;
            _btop = 0;
            _bbottom = 0;

            if(PlateSupportType == PlateSupportType.Internal)
            {
                /// Sub-Panel maximum lenght
                if (_code == Code.Eurocode)
                {
                    if (_phi == 1)
                    {
                        _bLIM = 42.0 * epsilon * _t;
                    }
                    if (_phi == -1)
                    {
                        _bLIM = 124 * epsilon * _t;
                    }
                    if (_phi > -1)
                    {
                        _bLIM = (42.0 * epsilon) / (0.67 + 0.33 * _phi) * _t;
                    }
                    if (_phi < -1)
                    {
                        _bLIM = ((62.0 * epsilon) * (1 - _phi) * Math.Sqrt(-phi)) * _t;
                    }
                }
                else if (_code == Code.ECP205_2001_ASD)
                {
                    if (_phi == 1)
                    {
                        _bLIM = (64.0 * (_t / 10) / Math.Sqrt(_fy / 98.07)) * 10;
                    }
                    if (_phi == -1)
                    {
                        _bLIM = (190 * (_t / 10) / Math.Sqrt(_fy / 98.07)) * 10;
                    }
                    if (_phi > -1)
                    {
                        _bLIM = ((190 * (_t / 10) / Math.Sqrt(_fy / 98.07)) / (2 + phi)) * 10;
                    }
                    if (_phi < -1) /// Come da Eurocodice -> non c'è da ECP205
                    {
                        _bLIM = ((62.0 * epsilon) * (1 - _phi) * Math.Sqrt(-phi)) * _t;
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

                if (_bLIM <= _b)
                {
                    /// Generic Values
                    _lambdapLIM = 0.5 + Math.Sqrt(0.085 - 0.055 * _phi); ;

                    /// Panel properties
                    if (_code == Code.Eurocode)
                    {
                        //_Sigmacrp = _ksigma * (pi * pi * E) / (12.0 * (1 - ni * ni)) * (t / b) * (t / b);
                        _Sigmacrp = _ksigma * _SigmaE;
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
                            _rho = (_lambdap - 0.055 * (3 + _phi)) / Math.Pow(_lambdap, 2.0);
                        }
                        else
                        {
                            _rho = 1;
                        }
                    }
                    else if (_code == Code.ECP205_2001_ASD)
                    {
                        _rho = Math.Min(((_lambdap - 0.15) - 0.05 * _phi) / Math.Pow(_lambdap, 2.0), 1.0);

                        //if (_rho > 1 || _rho < 0)
                        //{
                        //    _rho = 1;
                        //}
                    }
                }

                /// Column-Like Behaviour
                _Sigmacrc = 0;
                _lambdac = 0;
                _FiInst = 0;
                _chic = 0;
                _csi = 0;
                _rhoc = 0;

                double A = _b * _t;
                double I = _b * Math.Pow(_t, 3) / 12;

                if (_Ldiaf != 0)
                {
                    _Sigmacrc = Math.Pow(Math.PI, 2) * I *_E /(A * Math.Pow(_Ldiaf, 2));
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
                    _rhoc = (_rho - _chic) * _csi * (2 - _csi) + _chic;
                }

                /// beff calculation
                if (_phi >= 0)
                {
                    beff = _rho * _b;
                    _ybottom = 0;
                    _ytop = _b;
                    _be1 = 2 / (5 - _phi) * beff;
                    _be2 = beff - _be1;
                    _btop = _be1;
                    _bbottom = _be2;
                    _beff = _btop + _bbottom;
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
                    _beff = _btop + _bbottom;
                }

                if (_rho >= 1)
                {
                    _be1 = _b / 2;
                    _be2 = _b / 2;
                    _btop = _b / 2;
                    _bbottom = _b / 2;
                    _beff = _btop + _bbottom;
                }

                _Atotgross = _b * _t;
                _Atoteff = (btop + _bbottom) * _t;
                _teff = _Atoteff / _b;
                _CU = _Atoteff / _Atotgross;
            }

            if (PlateSupportType == PlateSupportType.Outstand)
            {
                if (_code == Code.Eurocode)
                {
                    _ksigma = 0.43;
                    // Calculate kSigma
                    if(tipInCompression == true)
                    {
                        if (_phi == 1.0)
                        {
                            _ksigma = 0.43;
                        }
                        else if (_phi == 0.0)
                        {
                            _ksigma = 0.57;
                        }
                        else if (_phi == -1.0)
                        {
                            _ksigma = 0.85;
                        }
                        else if (_phi <= 1.0 && _phi >= -3.0)
                        {
                            _ksigma = 0.57 - 0.21 * _phi + 0.07 * _phi * _phi;
                        }
                    }
                    else if (tipInCompression == false)
                    {
                        if (_phi == 1.0)
                        {
                            _ksigma = 0.43;
                        }
                        else if (_phi == 0.0)
                        {
                            _ksigma = 1.70;
                        }
                        else if (_phi == -1.0)
                        {
                            _ksigma = 23.8;
                        }
                        else if (_phi > 0.0 && _phi < 1.0)
                        {
                            _ksigma = 0.578 / (_phi + 0.34);
                        }
                        else if (_phi > -1.0 && _phi < 0.0)
                        {
                            _ksigma = 1.70 - 5.0 * _phi + 17.1*_phi * _phi;
                        }
                    }

                    // Calculate Blim to Class 3
                    if (_phi == 1)
                    {
                        _bLIM = 14 * _t * epsilon;
                    }
                    else
                    {
                        _bLIM = 21 * _t * epsilon * Math.Sqrt(_ksigma);
                    }

                    // Calculate Lambda Limit
                    _lambdapLIM = 0.748;

                    // Column-Like Behaviour
                    _Sigmacrc = 0;
                    _lambdac = 0;
                    _FiInst = 0;
                    _chic = 0;
                    _csi = 0;
                    _rhoc = 0;

                    // Plate Like behaviour
                    _Sigmacrp = _ksigma * _SigmaE;
                    _lambdap = Math.Sqrt(fy / _Sigmacrp);

                    if (_lambdap > lambdapLIM)
                    {
                        _rho = Math.Min((_lambdap - 0.188) / (_lambdap * _lambdap), 1.00);
                    }
                    else
                    {
                        _rho = 1;
                    }

                    // Calculate Effective Width
                    if (tipInCompression == true)
                    {
                        /// beff calculation
                        if (_phi >= 0)
                        {
                            _beff = _rho * _b;
                            _ybottom = beff;
                            _ytop = 0.0;
                            _be1 = 0.0;
                            _be2 = beff;
                            _btop = 0.0;
                            _bbottom = _be2;
                        }
                        else if (_phi < 0)
                        {
                            _ybottom = _b * Math.Abs(_phi) / (1 + Math.Abs(_phi));
                            _ytop = _b - _ybottom;
                            _be1 = 0.0;
                            _be2 = _rho * _b / (1 - _phi);
                            _btop = 0.0;
                            _bbottom = _be2 + _ybottom;
                            _beff = _bbottom;
                        }
                        if (_rho >= 1)
                        {
                            _be1 = 0.0;
                            _be2 = _b;
                            _btop = 0.0;
                            _bbottom = _b;
                            _beff = _b;
                        }
                    }
                    else if (tipInCompression == false)
                    {
                        /// beff calculation
                        if (_phi >= 0)
                        {
                            _beff = _rho * _b;
                            _ybottom = beff;
                            _ytop = 0.0;
                            _be1 = 0.0;
                            _be2 = beff;
                            _btop = 0.0;
                            _bbottom = _beff;
                        }
                        else if (_phi < 0)
                        {
                            _ybottom = _b * Math.Abs(_phi) / (1 + Math.Abs(_phi));
                            _ytop = _b - _ybottom;
                            _be1 = 0.0;
                            _be2 = _rho * _b / (1 - _phi);
                            _btop = _be1;
                            _bbottom = _be2 + _ybottom;
                            _beff = _bbottom;
                        }

                        if (_rho >= 1)
                        {
                            _be1 = 0.0;
                            _be2 = _b;
                            _btop = 0.0;
                            _bbottom = _b;
                            _beff = _b;
                        }
                    }

                    _Atotgross = _b * _t;
                    _Atoteff = (btop + _bbottom) * _t;
                    _teff = _Atoteff / _b;
                    _CU = _Atoteff / _Atotgross;
                }
            }
        }
        #endregion
    }
}
