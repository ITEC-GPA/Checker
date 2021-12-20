using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if NEVER

namespace GPCChecker.Steel.PanelsStability
{
    public class TransverseStiffener
    {
#region Variables
        protected double _t1;
        protected double _b1;
        protected double _t2;
        protected double _b2;
        protected double _hw;
        protected double _tw;
        protected double _Ldiaf;
        protected double _w0;
        protected double _iw;
        protected double _ist;
        protected double _ipst;
        protected double _itst;
        protected double _alfa;


        protected double _yg;
        protected StiffenerTransversalType _stiffenerTransversalType;
        protected double _sigmaCrst;
        protected double _sigmaCrstLIM;

        protected double _fy;
        protected double _E;
        protected double _epsilon;
        protected double _gammaM1;

        /// Minimum Requirements for Direct Stress
        protected double _nEd;
        protected double _istminDirStress;
        protected double _sigmaM;
        protected double _sigmaCrc;
        protected double _sigmaCrp;
        protected double _u;
        protected double _emax;
        /// Minimim Requirements for END Rigid Post 
        protected double _vEd;
        protected double _lambdaw;
        protected double _emin;
        protected double _aemin;
        protected double _e;
        protected double _ae;
        /// Minimim Requirements for INTERMEDIATE Rigid Post
        protected double _istminShear;
        protected double _nstten;



#endregion

#region Properties
        public double T1 => _t1;
        public double B1 => _b1;
        public double T2 => _t2;
        public double B2 => _b2;
        public double Hw => _hw;
        public double Tw => _tw;
        public double LDiaf => _Ldiaf;

        public double Iw => _iw;
        public double Ist => _ist;
        public double Ipst => _ipst;
        public double Itst => _itst;


        public double Fy => _fy;
        public double E => _E;
        public double epsilon => _epsilon;
        public double gammaM1 => _gammaM1;


        /// Minimum Requirements for Direct Stress
        public double NEd => _nEd;
        public double IstminDirStress => _istminDirStress;
        public double SigmaM => _sigmaM;
        public double SigmaCrc => _sigmaCrc;
        public double SigmaCrp => _sigmaCrp;
        public double SigmaCrst => _sigmaCrst;
        public double SigmaCrstLIM => _sigmaCrstLIM;
        public double u => _u;
        public double Emax => _emax;
        /// Minimim Requirements for END Rigid Post 
        public double VEd => _vEd;
        public double Lambdaw => _lambdaw;
        public double emin => _emin;
        public double Aemin => _aemin;
        public double e => _e;
        public double Ae => _ae;
        /// Minimim Requirements for INTERMEDIATE Rigid Post
        public double IstminShear => _istminShear;
        public double Nstten => _nstten;
#endregion

#region Commands
#endregion

#region Public Costructors
        public TransverseStiffener(double t1, double b1, double t2, double b2, double hw, double tw, double Ldiaf,
            double fy, double E,
            double nEd, double vEd,
            double sigmaCrc, double sigmaCrp,
            double lambdaw,
            StiffenerTransversalType stiffenerTransversalType = StiffenerTransversalType.DoubleSided,
            double gammaM1 = 1.00)
        {
            _t1 = t1;
            _b1 = b1;
            _t2 = t2;
            _b2 = b2;
            _hw = hw;
            _tw = tw;
            _Ldiaf = Ldiaf;
            _alfa = _Ldiaf / _hw;

            _fy = fy;
            _E = E;
            _gammaM1 = gammaM1;

            _nEd = nEd;
            _vEd = vEd;

            _sigmaCrc = sigmaCrc;
            _sigmaCrp = sigmaCrp;
            _lambdaw = lambdaw;

            _stiffenerTransversalType = stiffenerTransversalType;

            CalculateStiffenerProperties();
        }
#endregion

#region Public Methods Specific       
#endregion

#region Protected Methods Specific
        protected void CalculateStiffenerProperties()
        {
            /// Geometric Properties
            _e = _Ldiaf;
            _ae = _t1 * _b1 + _t2 * _b2;
            _epsilon = Math.Sqrt(235 / _fy);

            double G = _E / (2.0 * (1.0 + 0.3));
            double beffw = Math.Min(15.0 * _epsilon * _tw, _Ldiaf / 2.0);

            if (_stiffenerTransversalType == StiffenerTransversalType.SingleSided)
            {

                double area = (2*beffw * _tw) + (_t1 *_b1) + (_t2 * _b2);
                _yg = ((2.0 * beffw * _tw) * (_tw / 2.0) + (_t1 * _b1) * (_tw + _b1 / 2.0) + (_t2 * _b2) * (_tw + _b1 + _t2 / 2.0)) / area;

                _ist = (2.0 * beffw * _tw) * Math.Pow((_tw / 2.0 - _yg), 2.0) + 1.0 / 12.0 * (2.0 * beffw) * Math.Pow(_tw, 3.0) +
                    (_t1 * _b1) * Math.Pow((_tw + _b1 / 2.0 - _yg), 2.0) + 1.0 / 12.0 * _t1 * Math.Pow(_b1, 3.0) +
                    (_t2 * _b2) * Math.Pow(((_tw + _b1 + _t2 / 2.0) - _yg), 2.0) + 1.0 / 12.0 * _b2 * Math.Pow(_t2, 3.0);

                _emax = (_tw +_b1 + _t2) - _yg;
            }
            else if (_stiffenerTransversalType == StiffenerTransversalType.DoubleSided)
            {
                _yg = 0.0;
                _emax = 0.0;

                _ist = 1.0 / 12.0 * (2 * (beffw - _tw)) * Math.Pow(_tw, 3.0) +
                        1.0 / 12.0 * _t1 * Math.Pow((2*_b1+_tw), 3.0) +
                        2* ((_t2 * _b2) * Math.Pow(((_tw/2.0 + _b1 + _t2 / 2.0)), 2.0) + 1.0 / 12.0 * _b2 * Math.Pow(_t2, 3.0));
            }

            /// Flat Shape
            if (_t2 == 0.0 || _b2 == 0.0)
            {
                _iw = 0.0;

                _itst = _b1 * Math.Pow(_t1, 3) / 3;
                _ipst = Math.Pow(_b1, 3) * _t1 / 3 + _b1 * Math.Pow(_t1, 3) / 12 +
                    (Math.Pow(_b2, 3) * _t2 / 3 + (_b2*_t2) * Math.Pow((_t2/2.0 + _b1), 2.0)) + _b2 * Math.Pow(_t1, 3) / 12;

                _sigmaCrst = _itst / _ipst * _E / 5.3 * 2;
                _sigmaCrstLIM = 2.0 * _fy;
            }
            else
            {
                /// Warping Costants according to (Bleich 1952, Picard and Beaulieu 1991)
                //_iw = Math.Pow(_b2, 3.0) *Math.Pow(_t2,3.0)/144 + Math.Pow(_b1, 3.0) * Math.Pow(_t1, 3.0) / 36.0;
                _iw = Math.Pow(_b2, 3.0) * _t2 * Math.Pow((_b1 + _t2 / 2.0), 2.0) / 12.0;
                _itst = _b1 * Math.Pow(_t1, 3.0) / 3.0 + _b2 * Math.Pow(_t2, 3.0) / 3.0;
                _ipst = Math.Pow(_b1, 3.0) * _t1 / 3.0 + _b1 * Math.Pow(_t1, 3.0) / 12.0 +
                    1.0 / 12.0 * Math.Pow(_b2, 3.0) * _t2 + 1.0 / 12.0 * Math.Pow(_t2, 3.0) * _b2 + (_b2 * _t2) * Math.Pow((_b1 + _t2 / 2.0), 2.0);
                _sigmaCrst = 1 / _ipst * (Math.Pow(Math.PI, 2.0) * _E * _iw /Math.Pow(_hw, 2.0) + G * _itst); 

                _sigmaCrstLIM = 6.0 * _fy;
            }
            /// Tree Shape

            /// Direct Stress Requirements
            _w0 = Math.Min(_hw / 300.0, _Ldiaf / 300.0);
            double sigmaRatio = (_sigmaCrc / _sigmaCrp);
            if (sigmaRatio < 0.5)
            {
                sigmaRatio = 0.5;
            }
            else if (sigmaRatio > 1.00)
            {
                sigmaRatio = 1.00;
            }
            _sigmaM = sigmaRatio * ((_nEd *1000) / _hw) * (1 / _Ldiaf + 1 / _Ldiaf);

            _u = Math.Max((Math.Pow(Math.PI, 2.0) * _E * _emax) / ((_fy * 300.0 * _hw) / _gammaM1), 1.00);
            _istminDirStress = _sigmaM / _E * Math.Pow(_hw / Math.PI, 4.0) * (1 + _w0 * 300 / _hw * _u);

            /// Shear Stress Requirements
            _emin = 0.1 * _hw;
            _aemin = 4.0 * _hw * Math.Pow(_tw, 2.0) / _emin;
            _istminShear = 0.0;
            if (_alfa < Math.Sqrt(2.0))
            {
                _istminShear = 1.5 * Math.Pow(_hw, 3.0) * Math.Pow(_tw, 3.0) / Math.Pow(_Ldiaf, 2.0);

            }
            else
            {
                _istminShear = 0.75 * _hw * Math.Pow(_tw, 3.0);
            }
            _nstten = ((_vEd*1000) - 1 / Math.Pow(_lambdaw, 2.0) * _tw * _hw * _fy / Math.Sqrt(3) / 1.00)/1000;
        }
#endregion

#region Public Methods Override
#endregion
    }
}

#endif