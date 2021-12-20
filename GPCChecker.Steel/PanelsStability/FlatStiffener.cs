using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if NEVER

namespace GPCChecker.Steel.PanelsStability
{
    public class FlatStiffener
    {
#region Public Constructors
        public FlatStiffener(Code __code, double __ts, double __bs, double __t, double __b1, double __b2, double __b1eff, double __b2eff, double __Ldiaf, StiffenerLongitudinalType __stiffType, double __fy = 355, double __E = 210000, double __ni = 0.3)
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

#region Public Deconstructors
#endregion

#region Private Methods
        private void CalcStiffenerProperties()
        {
            double alfainstC = 0;
            if (_stiffType == StiffenerLongitudinalType.Closed)
            {
                alfainstC = 0.34;
            }
            else if (_stiffType == StiffenerLongitudinalType.Open)
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

#region Variables
        protected double _ts;
        protected double _bs;
        protected double _b1;
        protected double _b2;
        protected double _t;
        protected double _b1eff;
        protected double _b2eff;
        protected double _Ldiaf;
        protected double _fy;
        protected double _E;
        protected double _ni;
        protected Code _code;
        protected StiffenerLongitudinalType _stiffType;

        protected bool _TorsionalCheck;
        protected double _Ast;
        protected double _Ist;
        protected double _Itst;
        protected double _Ipst;

        protected double _yirrgr;
        protected double _Airrgr;
        protected double _Iirrgr;
        protected double _rIrrgr;

        protected double _yirreff;
        protected double _Airreff;
        protected double _Iirreff;
        protected double _rIrreff;

        protected double _Lambdac;
        protected double _SigmaCrc;
        protected double _lambdacAdimIrr;
        protected double _FiInst;
        protected double _e1;
        protected double _e2;
        protected double _e;
        protected double _alfae;
        protected double _Chic;

        protected double _ted;
        protected double _tlim;

        protected double _Lambdacgr;
        protected double _Lambdaceff;
#endregion

#region Properties
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
        public StiffenerLongitudinalType StiffType
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
}

#endif