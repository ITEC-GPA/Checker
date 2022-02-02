using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;

#if NEVER

namespace GPCChecker.Steel.PanelsStability
{
    public class PanelShearStability
    {
#region Variables
        protected double _tw;
        protected double _hw;
        protected double _twLim;
        protected double _Ldiaf;
        protected double _alfa;

        protected double _E;
        protected double _ni;
        protected double _fy;
        protected double _sigmaE;
        protected double _epsilon;
        protected double _eta;
        protected double _ktau;
        protected double _tauCr;
        protected double _lambdaw;
        protected double _chiw;

        protected WebStiffedType _webStiffenedType;
        protected StiffenerTransversalType _transverseStiffenerType;
#endregion

#region Properties
        public double Tw => _tw;
        public double Hw => _hw;
        public double twLim => _twLim;
        public double LDiaf => _Ldiaf;
        public double Alfa => _alfa;
        public double SigmaE => _sigmaE;
        public double Epsilon => _epsilon;
        public double Eta => _eta;
        public double Ktau => _ktau;
        public double TauCr => _tauCr;
        public double Lambdaw => _lambdaw;
        public double Chiw => _chiw;
#endregion

#region Commands
#endregion

#region Public Costructors
        public PanelShearStability(double hw, double tw, double Ldiaf, double E, double ni, double fy, 
            WebStiffedType webStiffenedType = WebStiffedType.Unstiffened,
            StiffenerTransversalType transverseStiffenerType = StiffenerTransversalType.Rigid)       
        {
            _tw = tw;
            _hw = hw;
            _Ldiaf = Ldiaf;
            _E = E;
            _ni = ni;
            _fy = fy;
            _webStiffenedType = webStiffenedType;
            _transverseStiffenerType = transverseStiffenerType;

            CalculateShearPanelStability();
        }
#endregion

#region Public Methods Specific
        protected void CalculateShearPanelStability()
        {
            _epsilon = Math.Sqrt(235 / _fy);
            _sigmaE = Math.Pow(Math.PI, 2.0) * _E / (12.0 * (1 - _ni * _ni)) * Math.Pow((_tw / _hw), 2.0);
            _alfa = _Ldiaf / _hw;

            /// Eta Factor Calculation
            if(_fy <= 460)
            {
                _eta = 1.2;
            }
            else
            {
                _eta = 1.0;
            }
            /// Calculation of ktau coefficient
            /// For Panels without longitudinal stiffeners and with rigid transverse only
            _ktau = 0.0;
            _lambdaw = 0.0;
            if ( _Ldiaf != 0)
            {
                if (_alfa < 1.00)
                {
                    _ktau = 4.00 + 5.34 * Math.Pow((_hw / _Ldiaf), 2.0);
                }
                else if (_alfa > 1.00)
                {
                    _ktau = 5.34 + 4.00 * Math.Pow((_hw / _Ldiaf), 2.0);
                }

                _lambdaw = _hw / (37.4 * _tw * _epsilon * Math.Sqrt(_ktau));
            }
            else
            {
                _lambdaw = _hw / (86.4 * _tw * _epsilon);
                _ktau = _fy / Math.Pow(_lambdaw, 2.0) * Math.Pow(0.76, 2.0) / _sigmaE;
            }


            /// Calculation of critical tangential stress
            _tauCr = _ktau * _sigmaE;

            /// Limit of Web Thickness to avoid buckling
            if (_webStiffenedType == WebStiffedType.Unstiffened)
            {
                _twLim = _hw / (72 * _epsilon / _eta);
            }
            else if (_webStiffenedType == WebStiffedType.Stiffened)
            {
                _twLim = _hw / (31 * _epsilon / _eta * Math.Sqrt(_ktau));
            }
            

            /// Calculation of web slenderness
            _chiw = 0.0;
            if (_transverseStiffenerType == StiffenerTransversalType.NonRigid)
            {
                if (_lambdaw < (0.83 /_eta))
                {
                    _chiw = _eta;
                }
                else if(_lambdaw >= (0.83 / _eta))
                {
                    _chiw = 0.83 / _lambdaw;
                }              
            }
            else if (_transverseStiffenerType == StiffenerTransversalType.Rigid)
            {
                if (_lambdaw < (0.83 / _eta))
                {
                    _chiw = _eta;
                }
                else if (_lambdaw >= (0.83 / _eta) && _lambdaw < 1.08)
                {
                    _chiw = 0.83 / _lambdaw;
                }
                else if (_lambdaw >= 1.08)
                {
                    _chiw = 1.37 /(0.7 + _lambdaw);
                }
            }
        }
#endregion


#region Protected Methods Specific
#endregion

#region Public Methods Override
#endregion
    }
}

#endif