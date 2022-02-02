using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Materials;

#if NEVER

namespace GPCChecker.Steel.PanelsStability
{
    public class StiffenedPanel
    {
        /// Local Coordinates Panel
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
        ///         |    |_______
        ///         |    |_______|
        ///         |    |
        ///         |    |        
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |_______
        ///         |    |_______|  (+tw+bs, +a11)
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |
        ///         |    |
        /// (0, 0)  |____| (+tw, 0)

#region Variable
        protected List<UnstiffenedPanel> _PanelArray;
        protected List<FlatStiffener> _StiffenerArray;

        protected double _fy;
        protected double _E;
        protected double _ni;
        protected Code _code;
        protected int _stiffNum;

        protected double _b;
        protected double _t;
        protected double _LdiafLIM;
        protected double _alfa;
        protected double _Rhop;
        protected double _BetaAc;
        protected double _LambdaP;
        protected double _LambdaPLIM;
        protected double _Chsi;
        protected double _Rhoc;

        protected double _ksigma;

        protected double _SigmaCrp;
        protected double _Sigmacsl;
        protected double _Sigmacsl1;
        protected double _Sigmacsl2;
        protected double _SigmaE;

        protected double _Atoteff;
        protected double _teff;
        protected double _Atotgross;
        protected double _CU;
#endregion

#region Properties
        public List<UnstiffenedPanel> PanelArray
        {
            get => _PanelArray;
        }
        public List<FlatStiffener> StiffenerArray
        {
            get => _StiffenerArray;
        }
        public double ksigma
        {
            get => _ksigma;
            private set => _ksigma = value;
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
        public Code Code
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
        public double Alfa
        {
            get => _alfa;
            private set => _alfa = value;
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
        public double SigmacE
        {
            get => _SigmaE;
            private set => _SigmaE = value;
        }
#endregion

#region Public Constructors
        public StiffenedPanel(Code code, int stiffNum, List<UnstiffenedPanel> panelarray, List<FlatStiffener> __StiffenerArray, double phi, double __fy = 355, double __E = 210000, double __ni = 0.3, double b = 0)
        {
            _PanelArray = new List<UnstiffenedPanel>();
            _PanelArray = panelarray;
            _StiffenerArray = new List<FlatStiffener>();
            _StiffenerArray = __StiffenerArray;
            _code = code;
            _stiffNum = stiffNum;
            _fy = __fy;
            _E = __E;
            _ni = __ni;

            _b = b;
            _t = panelarray[0].t;
            if (_stiffNum == 1 && _b == 0)
            {
                _b = _b + _PanelArray[0].b;
                _b = _b + _PanelArray[1].b;
            }
            else if (_stiffNum == 2 && _b == 0)
            {
                _b = _b + _PanelArray[0].b;
                _b = _b + _PanelArray[1].b;
                _b = _b + _PanelArray[2].b;
            }
            else if (_stiffNum >= 3 && _b != 0)
            {

            }
            CalcPanelProperties(phi);
        }
#endregion

#region FIELD_DECONSTRUCTORS

#endregion

#region FIELD_METHODS
        private void CalcPanelProperties(double phi)
        {
            _SigmaE = Math.Pow(Math.PI, 2.0) * _E / (12.0 * (1 - _ni * _ni)) * Math.Pow((_t / _b), 2.0);

            /// One single Stiffener
            if (_stiffNum == 1)
            {
                double[] PlateBucklingOutput = CalcPlateBucklingSingleFlatStiffener(_PanelArray[0], _PanelArray[1], _StiffenerArray[0], 0.00, 0.00);

                _LdiafLIM = PlateBucklingOutput[0];
                _alfa = PlateBucklingOutput[1];
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
                    _alfa = PlateBucklingOutput1[1];
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
                    _alfa = PlateBucklingOutput2[1];
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
                    _alfa = PlateBucklingOutput3[1];
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
            else if (_stiffNum >= 3)
            {
                //double Isl = 0;
                double Ip = _b * Math.Pow(_t, 3.0) / 10.92;

                double Asl = _StiffenerArray[0].Ast;
                double Ap = _b*_t;
                double delta = Asl / Ap;


                if (_alfa > 0.5)
                {
                    if(_alfa <= Math.Pow(_alfa, (1/4)))
                    {
                        //_ksigma = 2 * (() - 1) / (_alfa * _alfa) ;
                    }
                    else if(_alfa > Math.Pow(_alfa, (1 / 4)))
                    {

                    }

                }


                _SigmaCrp = _ksigma * _SigmaE;
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

            if (Code == Code.Eurocode)
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
            else if (Code == Code.ECP205_2001_ASD)
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

            //double atoteff = 0;
            //double atotgross = 0;
            //double cu = 0;


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
    }
}
#endif