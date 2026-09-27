namespace GPC.Checkers.CompositeBridge.Viviani;

/// <summary>
/// Method of prof. Viviani for the steel-concrete composite section of bridges (program DT-NTC2008 "SezioneComposta"): kept as the history
/// of the results and for the comparison with the linear calculation of the library. The calculation is the one of the decompiled program,
/// rewritten to be readable without changing the formulas, their order and the branches: the results are identical to the program
/// (the test VivianiEquivalenceTests compares them with the mechanical extraction of the original on thousands of cases).
/// </summary>
/// <remarks>
/// <para>Elastic analysis: steel section with the effective web (EN 1993-1-5 4.4, only the web: two strips be1 from the top and be2 from the
/// bottom, a fixed number of iterations), sections homogenized with n0 and ninf; if the top of the slab is in tension under the long and
/// short term actions the cracked section (steel and reinforcement) is used for both. Stresses at 6 points with the actions of the three
/// stages (the γ of the actions only with <see cref="VivianiInput.FactoredElasticStresses"/>).</para>
/// <para>Plastic analysis: plastic moment with the γ of the actions, positive or negative, class of the web from the plastic neutral axis,
/// reduction of the fyd of the web if T Ed &gt; 0.5 Vpl (EN 1993-1-1 6.2.8).</para>
/// <para>The observations on the method (possible errors and imprecisions) are in NOTE-metodo-Viviani.md; they are not corrected here.</para>
/// </remarks>
public static class VivianiMethod
{
    /// <summary>Calculates the section</summary>
    /// <param name="input">The data, in the units of the program</param>
    /// <returns>The results shown by the program</returns>
    public static VivianiResult Calculate(VivianiInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var calculation = new Calculation(input);
        calculation.Run();
        return calculation.Result();
    }

    /// <summary>
    /// The state of the calculation: the variables of the program, with readable names (the original ones in the comments). Some variables are
    /// kept between the steps as in the program (e.g. the plastic neutral axis of the first pass when the second one does not calculate it)
    /// </summary>
    private sealed class Calculation
    {
        private readonly VivianiInput _in;

        // geometry [cm] (hs, bs, rs, afs, tps, bps, haw, taw, tpi, bpi)
        private readonly double _hs, _bs, _rs, _afs, _tps, _bps, _haw, _tpi, _bpi;
        private double _taw;
        // modular ratios (n0, ninf) and design strengths (fcd, fydar, fydps, fyda1, fyda, fydpi)
        private readonly double _n0, _ninf, _fcd, _fydRebar, _fydTop, _fydWeb, _fydBottom;
        private double _fydWebReduced;
        // actions (sninf, stinf, sminf / snninf, stninf, smninf / snn0, stn0, smn0) and coefficients of the elastic stresses (coeffg1, coeffg2, coeffq)
        private readonly VivianiActions _g1, _g2, _q;
        private readonly double _coeffG1 = 1.0, _coeffG2 = 1.0, _coeffQ = 1.0;
        // levels of the points from the bottom of the steel (y1 ... y6)
        private readonly double _y1, _y2, _y3, _y4, _y5, _y6;

        // effective web (be1, be2), plate buckling (psi, ksig, lamp, ro, beff), classes (cle, clp, cte, ctr, ctp), iterations (iter, iterp)
        private double _be1, _be2, _psi, _kSigma, _lambdaP, _rho, _bEff;
        private int _elasticClass, _plasticClass, _iteration, _shearReductionApplied;
        private double _elasticLimit, _plasticLimit, _webSlenderness;

        // sections: steel (ainf, yginf, jxinf, w3inf ... w6inf), used for the long and short term actions (aninf, ygninf, jxninf, w1ninf ... htninf;
        // an0 ... htn0)
        private double _aSteel, _ygSteel, _jSteel, _w3Steel, _w4Steel, _w5Steel, _w6Steel;
        private Composite _longTerm, _shortTerm;
        private double _slabTopStress;   // par1

        // stresses (sig1 ... sig6, tau4, tau5, sigid3 ... sigid6, taub) and neutral axes (yel, ypl)
        private double _sig1, _sig2, _sig3, _sig4, _sig5, _sig6, _tau4, _tau5, _sigId3, _sigId4, _sigId5, _sigId6, _shearFlow;
        private double _yElastic, _yPlastic;

        // plastic analysis (mtot, ttot, mpl, nibeta, ue, nr, u1, u2, u3, ua)
        private double _mEd, _tEd, _mPl, _utilization, _epsilon, _nr, _u1, _u2, _u3, _ua;

        // colours of the fields (TextBox1, 13, 24, 47, 51, 53)
        private VivianiColor _colorTw, _colorUtilization, _colorAf, _colorFydWeb, _colorElasticClass, _colorPlasticClass;

        /// <summary>A homogenized section: area, centroid, inertia, moduli at the points (W1 of the concrete multiplied by n), J / S of the shear flow</summary>
        private readonly struct Composite
        {
            public readonly double A, Yg, J, W1, W2, W3, W4, W5, W6, Ht;

            public Composite(double a, double yg, double j, double w1, double w2, double w3, double w4, double w5, double w6, double ht)
            {
                A = a; Yg = yg; J = j; W1 = w1; W2 = w2; W3 = w3; W4 = w4; W5 = w5; W6 = w6; Ht = ht;
            }
        }

        public Calculation(VivianiInput input)
        {
            _in = input;
            _hs = input.SlabHeight; _bs = input.SlabWidth; _rs = input.RebarCover; _afs = input.RebarArea;
            _tps = input.TopFlangeThickness; _bps = input.TopFlangeWidth; _haw = input.WebHeight; _taw = input.WebThickness;
            _tpi = input.BottomFlangeThickness; _bpi = input.BottomFlangeWidth;
            _n0 = input.ShortTermModularRatio; _ninf = input.LongTermModularRatio;
            _fydRebar = input.RebarFyd; _fcd = input.Fcd; _fydTop = input.TopFlangeFyd; _fydWeb = input.WebFyd; _fydBottom = input.BottomFlangeFyd;
            _g1 = input.SteelOnly; _g2 = input.LongTerm; _q = input.ShortTerm;
            if (input.FactoredElasticStresses)
            {
                _coeffG1 = input.GammaG1;
                _coeffG2 = input.GammaG2;
                _coeffQ = input.GammaQ;
            }
            _y1 = _tpi + _haw + _tps + _hs;
            _y2 = _y1 - _rs;
            _y3 = _y1 - _hs;
            _y4 = _y3 - _tps;
            _y5 = _y4 - _haw;
            _y6 = 0.0;
            _colorTw = VivianiColor.Black;
            _colorUtilization = VivianiColor.Black;
            _colorAf = VivianiColor.Black;
            _colorFydWeb = VivianiColor.Green;
            _colorElasticClass = VivianiColor.Black;
            _colorPlasticClass = VivianiColor.Black;
        }

        public void Run()
        {
            ElasticAnalysis();
            PlasticAnalysis();
        }

        #region Elastic analysis

        /// <summary>Iterations of the effective web: properties and stresses with the current web, then the class and the new web</summary>
        private void ElasticAnalysis()
        {
            _webSlenderness = _haw / _taw;
            _be1 = _haw / 2.0;
            _be2 = _haw / 2.0;
            _fydWebReduced = _fydWeb;
            while (true)
            {
                SteelSection();
                CompositeSections();
                Stresses();
                if (_in.Iterations == 0)
                    return;
                if (_elasticClass == 4 & _iteration >= _in.Iterations)
                    return;
                _iteration = checked(_iteration + 1);
                if (!EffectiveWeb())
                    return;
            }
        }

        /// <summary>Steel section with the effective strips of the web</summary>
        private void SteelSection()
        {
            _aSteel = _bps * _tps + _be1 * _taw + _be2 * _taw + _tpi * _bpi;
            _ygSteel = (_bps * _tps * (_tpi + _haw + _tps / 2.0) + _be1 * _taw * (_tpi + _haw - _be1 / 2.0) + _be2 * _taw * (_tpi + _be2 / 2.0) + _tpi * _bpi * _tpi / 2.0) / _aSteel;
            double ownInertia = Math.Pow(_tpi, 3.0) * _bpi / 12.0 + Math.Pow(_be2, 3.0) * _taw / 12.0 + Math.Pow(_be1, 3.0) * _taw / 12.0 + Math.Pow(_tps, 3.0) * _bps / 12.0;
            double transport = _bps * _tps * Math.Pow(_tpi + _haw + _tps / 2.0 - _ygSteel, 2.0) + _be1 * _taw * Math.Pow(_tpi + _haw - _be1 / 2.0 - _ygSteel, 2.0)
                + _be2 * _taw * Math.Pow(_tpi + _be2 / 2.0 - _ygSteel, 2.0) + _bpi * _tpi * Math.Pow(_tpi / 2.0 - _ygSteel, 2.0);
            _jSteel = ownInertia + transport;
            _w3Steel = _jSteel / (_ygSteel - _y3);
            _w4Steel = _jSteel / (_ygSteel - _y4);
            _w5Steel = _jSteel / (_ygSteel - _y5);
            _w6Steel = _jSteel / (_ygSteel - _y6);
        }

        /// <summary>Section homogenized with the modular ratio n (slab and reinforcement on the steel section)</summary>
        private Composite Homogenized(double n)
        {
            double a = _aSteel + _bs * _hs / n + _afs;
            double yg = (_aSteel * _ygSteel + _afs * _y2 + _bs * _hs / n * (_y1 - _hs / 2.0)) / a;
            double ownInertia = _jSteel + Math.Pow(_hs, 3.0) * _bs / (12.0 * n);
            double transport = _aSteel * Math.Pow(_ygSteel - yg, 2.0) + _bs * _hs / n * Math.Pow(_y1 - _hs / 2.0 - yg, 2.0) + _afs * Math.Pow(_y2 - yg, 2.0);
            double j = ownInertia + transport;
            double staticMoment = _bs * _hs / n * (_y1 - _hs / 2.0 - yg) + _afs * (_y2 - yg);
            return new Composite(a, yg, j, j / (yg - _y1) * n, j / (yg - _y2), j / (yg - _y3), j / (yg - _y4), j / (yg - _y5), j / (yg - _y6), j / staticMoment);
        }

        /// <summary>Cracked section: steel and reinforcement</summary>
        private Composite Cracked()
        {
            double a = _aSteel + _afs;
            double yg = (_aSteel * _ygSteel + _afs * _y2) / a;
            double transport = _aSteel * Math.Pow(_ygSteel - yg, 2.0) + _afs * Math.Pow(_y2 - yg, 2.0);
            double j = _jSteel + transport;
            double staticMoment = _afs * (_y2 - yg);
            return new Composite(a, yg, j, 0.0, j / (yg - _y2), j / (yg - _y3), j / (yg - _y4), j / (yg - _y5), j / (yg - _y6), j / staticMoment);
        }

        /// <summary>The sections of the long and short term actions: the cracked one for both if the top of the slab is in tension</summary>
        private void CompositeSections()
        {
            Composite longTerm = Homogenized(_ninf);
            Composite shortTerm = Homogenized(_n0);
            Composite cracked = Cracked();
            _longTerm = longTerm;
            _shortTerm = shortTerm;
            // stress of the concrete at the top of the slab of the uncracked sections [kN/cm²]
            _slabTopStress = _g2.N * _coeffG2 / (_longTerm.A * _ninf) + _q.N * _coeffQ / (_shortTerm.A * _n0) + _coeffG2 * _g2.M * 100.0 / _longTerm.W1 + _coeffQ * _q.M * 100.0 / _shortTerm.W1;
            if (_slabTopStress > 1E-05 | _hs <= 0.0)
            {
                _longTerm = cracked;
                _shortTerm = cracked;
            }
        }

        /// <summary>Stresses at the 6 points [MPa] (×10 from kN/cm²), shear stress of the web and shear flow on the connection</summary>
        private void Stresses()
        {
            if (_slabTopStress < 1E-05 & _hs > 0.0)
                _sig1 = (_coeffG2 * _g2.N / (_longTerm.A * _ninf) + _coeffQ * _q.N / (_shortTerm.A * _n0) + _coeffG2 * _g2.M * 100.0 / _longTerm.W1 + _coeffQ * _q.M * 100.0 / _shortTerm.W1) * 10.0;
            else
                _sig1 = 0.0;
            if (_hs > 0.0)
                _sig2 = (_coeffG2 * _g2.N / _longTerm.A + _coeffQ * _q.N / _shortTerm.A + _coeffG2 * _g2.M * 100.0 / _longTerm.W2 + _coeffQ * _q.M * 100.0 / _shortTerm.W2) * 10.0;
            else
                _sig2 = 0.0;
            _sig3 = SteelStress(_w3Steel, _longTerm.W3, _shortTerm.W3);
            _sig4 = SteelStress(_w4Steel, _longTerm.W4, _shortTerm.W4);
            _sig5 = SteelStress(_w5Steel, _longTerm.W5, _shortTerm.W5);
            _sig6 = SteelStress(_w6Steel, _longTerm.W6, _shortTerm.W6);
            _tau4 = Math.Abs(_coeffG1 * _g1.T + _coeffG2 * _g2.T + _coeffQ * _q.T) * 10.0 / ((_be1 + _be2) * _taw);
            _tau5 = _tau4;
            _sigId3 = Math.Abs(_sig3);
            _sigId4 = Math.Sqrt(Math.Pow(_sig4, 2.0) + 3.0 * Math.Pow(_tau4, 2.0));
            _sigId6 = Math.Abs(_sig6);
            _sigId5 = Math.Sqrt(Math.Pow(_sig5, 2.0) + 3.0 * Math.Pow(_tau5, 2.0));
            _shearFlow = Math.Abs(_coeffG2 * _g2.T / _longTerm.Ht + _coeffQ * _q.T / _shortTerm.Ht);
        }

        /// <summary>Stress of a point of the steel: the three stages on their sections</summary>
        private double SteelStress(double wSteel, double wLongTerm, double wShortTerm) =>
            (_coeffG1 * _g1.N / _aSteel + _coeffG2 * _g2.N / _longTerm.A + _coeffQ * _q.N / _shortTerm.A + _coeffG1 * _g1.M * 100.0 / wSteel + _coeffG2 * _g2.M * 100.0 / wLongTerm
            + _coeffQ * _q.M * 100.0 / wShortTerm) * 10.0;

        /// <summary>
        /// Class of the web from the stresses σ4 and σ5 and, in class 4, the new effective strips (EN 1993-1-5 table 4.1)
        /// </summary>
        /// <returns>True to iterate again; false at the end (class 3, web in tension)</returns>
        private bool EffectiveWeb()
        {
            if (_sig4 < 0.0 & _sig5 < 0.0)
            {
                // web all compressed: be1 on the more compressed edge
                if (_sig4 < _sig5)
                {
                    _psi = _sig5 / _sig4;
                    _elasticLimit = 42.0 * Math.Sqrt(235.0 / _fydWebReduced) / (0.67 + 0.33 * _psi);
                    if (_elasticLimit >= _webSlenderness)
                        return ClassThree();
                    _elasticClass = 4;
                    _kSigma = 8.2 / (1.05 + _psi);
                    ReductionFactor();
                    _bEff = _rho * _haw;
                    _be1 = 2.0 / (5.0 - _psi) * _bEff;
                    _be2 = _bEff - _be1;
                }
                else
                {
                    _psi = _sig4 / _sig5;
                    _elasticLimit = 42.0 * Math.Sqrt(235.0 / _fydWebReduced) / (0.67 + 0.33 * _psi);
                    if (_elasticLimit >= _webSlenderness)
                        return ClassThree();
                    _elasticClass = 4;
                    _kSigma = 8.2 / (1.05 + _psi);
                    ReductionFactor();
                    _bEff = _rho * _haw;
                    _be2 = 2.0 / (5.0 - _psi) * _bEff;
                    _be1 = _bEff - _be2;
                }
                return true;
            }
            if (_sig4 > 0.0 & _sig5 > 0.0)
                return ClassThree();
            if (_sig4 < 0.0)
            {
                // top of the web compressed, bottom in tension
                _psi = _sig5 / _sig4;
                _yElastic = _sig5 * _haw / (_sig5 - _sig4) + _tpi;
                _elasticLimit = BendingLimit();
                if (_elasticLimit >= _webSlenderness)
                    return ClassThree();
                _elasticClass = 4;
                _colorElasticClass = VivianiColor.Blue;
                _kSigma = BendingBucklingFactor();
                ReductionFactor();
                _bEff = _rho * _haw / (1.0 - _psi);
                _be1 = 0.4 * _bEff;
                _be2 = 0.6 * _bEff - _psi / (1.0 - _psi) * _haw;
                return true;
            }
            if (_sig5 >= 0.0)
                return false;
            // bottom of the web compressed, top in tension
            _psi = _sig4 / _sig5;
            _yElastic = _sig5 * _haw / (_sig5 - _sig4) + _tpi;
            _elasticLimit = BendingLimit();
            if (_elasticLimit > _webSlenderness)
                return ClassThree();
            _elasticClass = 4;
            _colorElasticClass = VivianiColor.Aquamarine;
            _kSigma = BendingBucklingFactor();
            ReductionFactor();
            _bEff = _rho * _haw / (1.0 - _psi);
            _be2 = 0.4 * _bEff;
            _be1 = 0.6 * _bEff - _psi / (1.0 - _psi) * _haw;
            return true;
        }

        private bool ClassThree()
        {
            _elasticClass = 3;
            _colorElasticClass = VivianiColor.Green;
            return false;
        }

        /// <summary>c/t limit of class 3 of an internal part with ψ &lt; 0</summary>
        private double BendingLimit() => _psi > -1.0
            ? 42.0 * Math.Sqrt(235.0 / _fydWebReduced) / (0.67 + 0.33 * _psi)
            : 62.0 * Math.Sqrt(235.0 / _fydWebReduced) * (1.0 - _psi) * Math.Sqrt(-1.0 * _psi);

        /// <summary>kσ of an internal part with ψ &lt; 0 (the value of ψ = -3 below -3)</summary>
        private double BendingBucklingFactor()
        {
            double k = 0.0;
            if (_psi < 0.0 & _psi > -1.0)
                k = 7.81 - 6.29 * _psi + 9.78 * _psi * _psi;
            if (_psi <= -1.0 & _psi > -3.0)
                k = 5.98 * Math.Pow(1.0 - _psi, 2.0);
            if (_psi <= -3.0)
                k = 95.68;
            return k;
        }

        /// <summary>λp and ρ of the web (ρ = 1 up to λp = 0.673, at most 1)</summary>
        private void ReductionFactor()
        {
            _lambdaP = _haw / (28.4 * _taw * Math.Sqrt(235.0 / _fydWebReduced) * Math.Sqrt(_kSigma));
            _rho = 1.0;
            if (_lambdaP > 0.673)
                _rho = (_lambdaP - 0.055 * (3.0 + _psi)) / Math.Pow(_lambdaP, 2.0);
            if (_rho > 1.0)
                _rho = 1.0;
        }

        #endregion

        #region Plastic analysis

        /// <summary>Shear check, plastic moment, interaction with the shear and utilization</summary>
        private void PlasticAnalysis()
        {
            _mEd = _in.GammaG1 * _g1.M + _in.GammaG2 * _g2.M + _in.GammaQ * _q.M;
            _tEd = _in.GammaG1 * _g1.T + _in.GammaG2 * _g2.T + _in.GammaQ * _q.T;
            _u1 = _haw * _taw * _fydWebReduced / Math.Sqrt(3.0) / 10.0;
            if (_tEd > _u1)
            {
                // the program shows Tw = 0 in red and does not calculate the plastic moment
                _taw = 0.0;
                _mPl = 0.0;
                _colorTw = VivianiColor.Red;
            }
            else
            {
                _epsilon = Math.Sqrt(235.0 / _fydWebReduced);
                while (true)
                {
                    if (_mEd >= 0.0)
                        PositivePlasticMoment();
                    else if (_mEd < 0.0)
                        NegativePlasticMoment();
                    if (_mPl == 0.0)
                        break;
                    _u1 = _haw * _taw * _fydWebReduced / Math.Sqrt(3.0) / 10.0;
                    if (_tEd > 0.5 * _u1 & _shearReductionApplied == 0)
                    {
                        // reduced yield strength of the web (EN 1993-1-1 6.2.8) and new plastic moment
                        _fydWebReduced = _fydWeb * (1.0 - Math.Pow(2.0 * _tEd / _u1 - 1.0, 2.0));
                        _colorFydWeb = VivianiColor.Red;
                        _shearReductionApplied = 1;
                        continue;
                    }
                    break;
                }
            }
            if (_mPl == 0.0)
            {
                _utilization = 1.0;
            }
            else
            {
                _colorUtilization = VivianiColor.Green;
                _utilization = _mEd / _mPl;
                if (_utilization >= 1.0)
                    _colorUtilization = VivianiColor.Red;
            }
        }

        /// <summary>Plastic moment with the slab compressed (M Ed ≥ 0): neutral axis in the slab, at the reinforcement, in the top flange, in the web or in the bottom flange</summary>
        private void PositivePlasticMoment()
        {
            _nr = _afs * _fydRebar + _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom;
            if (_bs * _rs * _fcd > _nr)
            {
                // neutral axis in the slab above the reinforcement
                _plasticClass = 1;
                _plasticLimit = 1000.0;
                _u1 = _nr / (_bs * _fcd);
                _mPl = _bs * _u1 * _fcd * _u1 / 2.0;
                _mPl += _afs * _fydRebar * (_rs - _u1);
                _mPl += _bps * _tps * _fydTop * (_hs + _tps / 2.0 - _u1);
                _mPl += _haw * _taw * _fydWebReduced * (_hs + _tps + _haw / 2.0 - _u1);
                _mPl += _tpi * _bpi * _fydBottom * (_hs + _tps + _haw + _tpi / 2.0 - _u1);
                _mPl /= 1000.0;
                _yPlastic = _tpi + _haw + _tps + _hs - _u1;
                return;
            }
            _nr = _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom;
            if (_bs * _rs * _fcd + _afs * _fydRebar > _nr)
            {
                // neutral axis at the reinforcement
                _plasticClass = 1;
                _plasticLimit = 1000.0;
                _u1 = _rs;
                _mPl = _bs * _u1 * _fcd * _u1 / 2.0;
                _mPl += _bps * _tps * _fydTop * (_hs + _tps / 2.0 - _u1);
                _mPl += _haw * _taw * _fydWebReduced * (_hs + _tps + _haw / 2.0 - _u1);
                _mPl += _tpi * _bpi * _fydBottom * (_hs + _tps + _haw + _tpi / 2.0 - _u1);
                _mPl /= 1000.0;
                _yPlastic = _tpi + _haw + _tps + _hs - _u1;
                return;
            }
            _nr = _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom;
            if (_bs * _hs * _fcd + _afs * _fydRebar > _nr)
            {
                // neutral axis in the slab below the reinforcement
                _plasticClass = 1;
                _plasticLimit = 1000.0;
                _u1 = (_nr - _afs * _fydRebar) / (_bs * _fcd);
                _mPl = _bs * _u1 * _fcd * _u1 / 2.0;
                _mPl += _afs * _fydRebar * (_u1 - _rs);
                _mPl += _bps * _tps * _fydTop * (_hs + _tps / 2.0 - _u1);
                _mPl += _haw * _taw * _fydWebReduced * (_hs + _tps + _haw / 2.0 - _u1);
                _mPl += _tpi * _bpi * _fydBottom * (_hs + _tps + _haw + _tpi / 2.0 - _u1);
                _mPl /= 1000.0;
                _yPlastic = _tpi + _haw + _tps + _hs - _u1;
                return;
            }
            // neutral axis in the steel: half of the total resistance
            _nr = (_bs * _hs * _fcd + _afs * _fydRebar + _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom) / 2.0;
            if (_bs * _hs * _fcd + _afs * _fydRebar + _bps * _tps * _fydTop > _nr)
            {
                // in the top flange
                _plasticClass = 1;
                _plasticLimit = 1000.0;
                _u1 = (_nr - _afs * _fydRebar - _bs * _hs * _fcd) / (_bps * _fydTop);
                _mPl = _bs * _hs * _fcd * (_hs / 2.0 + _u1);
                _mPl += _afs * _fydRebar * (_hs + _u1 - _rs);
                _mPl += _bps * _u1 * _fydTop * _u1 / 2.0;
                _mPl += _bps * (_tps - _u1) * _fydTop * (_tps - _u1) / 2.0;
                _mPl += _haw * _taw * _fydWebReduced * (_tps + _haw / 2.0 - _u1);
                _mPl += _tpi * _bpi * _fydBottom * (_tps + _haw + _tpi / 2.0 - _u1);
                _mPl /= 1000.0;
                _yPlastic = _tpi + _haw + _tps - _u1;
            }
            else if (_bs * _hs * _fcd + _afs * _fydRebar + _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced > _nr)
            {
                // in the web: u2 compressed from the top, class of the web from the compressed fraction
                _u2 = (_nr - _bs * _hs * _fcd - _afs * _fydRebar - _bps * _tps * _fydTop) / (_fydWebReduced * _taw);
                _u3 = _haw - _u2;
                _ua = _u2 / _haw;
                if (!PlasticWebClass())
                    return;
                _mPl = _bs * _hs * _fcd * (_hs / 2.0 + _tps + _u2);
                _mPl += _afs * _fydRebar * (_hs + _tps + _u2 - _rs);
                _mPl += _bps * _tps * _fydTop * (_tps / 2.0 + _u2);
                _mPl += _u2 * _taw * _fydWebReduced * _u2 / 2.0;
                _mPl += _u3 * _taw * _fydWebReduced * _u3 / 2.0;
                _mPl += _tpi * _bpi * _fydBottom * (_u3 + _tpi / 2.0);
                _mPl /= 1000.0;
                _yPlastic = _tpi + _haw - _u2;
            }
            else if (_webSlenderness > 38.0 * _epsilon)
            {
                // in the bottom flange with the web all compressed and slender
                _plasticClass = -1;
                _mPl = 0.0;
                _colorPlasticClass = VivianiColor.Red;
            }
            else
            {
                // in the bottom flange: u1 in tension from the bottom (the program does not divide this moment by 1000)
                _plasticClass = 2;
                _plasticLimit = _webSlenderness;
                if (_webSlenderness <= 33.0 * _epsilon)
                    _plasticClass = 1;
                _u1 = (_bs * _hs * _fcd + _afs * _fydRebar + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom + _tps * _bps * _fydTop) / 2.0 / (_fydBottom * _bpi);
                _mPl = _bpi * _u1 * _fydBottom * _u1 / 2.0;
                _mPl += _bpi * (_tpi - _u1) * _fydBottom * (_tpi - _u1) / 2.0;
                _mPl += _haw * _taw * _fydWebReduced * (_haw / 2.0 + _tpi - _u1);
                _mPl += _tps * _bps * _fydTop * (_tps / 2.0 + _haw + _tpi - _u1);
                _mPl += _afs * _fydRebar * (_tpi + _haw + _tps + _hs - _rs - _u1);
                _mPl += _bs * _hs * _fcd * (_tpi + _haw + _tps + _hs / 2.0 - _u1);
                _yPlastic = _u1;
            }
        }

        /// <summary>Plastic moment with the slab in tension (M Ed &lt; 0): only steel and reinforcement</summary>
        private void NegativePlasticMoment()
        {
            _nr = (_afs * _fydRebar + _bps * _tps * _fydTop + _haw * _taw * _fydWebReduced + _bpi * _tpi * _fydBottom) / 2.0;
            if (_bpi * _tpi * _fydBottom > _nr)
            {
                // neutral axis in the bottom flange, u1 compressed from the bottom
                _plasticClass = 1;
                _plasticLimit = 1000.0;
                _u1 = _nr / (_bpi * _fydBottom);
                _mPl = _bpi * _u1 * _fydBottom * _u1 / 2.0;
                _mPl += _bpi * (_tpi - _u1) * _fydBottom * (_tpi - _u1) / 2.0;
                _mPl += _haw * _taw * _fydWebReduced * (_haw / 2.0 + _tpi - _u1);
                _mPl += _tps * _bps * _fydTop * (_tps / 2.0 + _haw + _tpi - _u1);
                _mPl += _afs * _fydRebar * (_tpi + _haw + _tps + _hs - _rs - _u1);
                _mPl = -_mPl / 1000.0;
                _yPlastic = _u1;
            }
            else if (_bpi * _tpi * _fydBottom + _haw * _taw * _fydWebReduced > _nr)
            {
                // in the web: u3 compressed from the bottom
                _colorPlasticClass = VivianiColor.Green;
                _u3 = (_nr - _bpi * _tpi * _fydBottom) / (_fydWebReduced * _taw);
                _u2 = _haw - _u3;
                _ua = _u3 / _haw;
                if (!PlasticWebClass())
                    return;
                _mPl += _afs * _fydRebar * (_hs + _tps + _u2 - _rs);
                _mPl += _bps * _tps * _fydTop * (_tps / 2.0 + _u2);
                _mPl += _u2 * _taw * _fydWebReduced * _u2 / 2.0;
                _mPl += _u3 * _taw * _fydWebReduced * _u3 / 2.0;
                _mPl += _tpi * _bpi * _fydBottom * (_u3 + _tpi / 2.0);
                _mPl = -_mPl / 1000.0;
                _yPlastic = _tpi + _u3;
            }
            else if (_bpi * _tpi * _fydBottom + _tps * _fydTop * _bps + _taw * _haw * _fydWebReduced > _nr)
            {
                // in the top flange with the web all compressed
                if (_webSlenderness > 38.0 * _epsilon)
                {
                    _plasticClass = -1;
                    _mPl = 0.0;
                    _colorPlasticClass = VivianiColor.Red;
                }
                else
                {
                    _plasticClass = 2;
                    _plasticLimit = _webSlenderness;
                    if (_webSlenderness <= 33.0 * _epsilon)
                        _plasticClass = 1;
                    _u1 = (_nr - _afs * _fydRebar) / (_fydTop * _bps);
                    _mPl = _bpi * _tpi * _fydBottom * (_tpi / 2.0 + _haw + _tps - _u1);
                    _mPl += _haw * _taw * _fydWebReduced * (_haw / 2.0 + _tps - _u1);
                    _mPl += (_tps - _u1) * (_tps - _u1) / 2.0 * _bps * _fydTop;
                    _mPl += _u1 * _bps * _fydTop * _u1 / 2.0;
                    _mPl += _afs * _fydRebar * (_hs - _rs + _u1);
                    _mPl = -_mPl / 1000.0;
                    _yPlastic = _tpi + _haw + _tps - _u1;
                }
            }
            else if (_webSlenderness > 38.0 * _epsilon)
            {
                _plasticClass = -1;
                _mPl = 0.0;
                _colorPlasticClass = VivianiColor.Red;
            }
            else
            {
                // all the steel compressed: neutral axis at the top of the steel
                _plasticClass = 2;
                _plasticLimit = _webSlenderness;
                if (_webSlenderness <= 33.0 * _epsilon)
                    _plasticClass = 1;
                _u1 = _hs - _rs;
                _mPl = _bpi * _tpi * _fydBottom * (_tpi / 2.0 + _haw + _tps);
                _mPl += _haw * _taw * _fydWebReduced * (_haw / 2.0 + _tps);
                _mPl += _tps * _tps / 2.0 * _bps * _fydTop;
                _mPl += _nr * _u1;
                _mPl = -_mPl / 1000.0;
                _yPlastic = _tpi + _haw + _tps;
            }
        }

        /// <summary>
        /// Class 1 or 2 of the web cut by the plastic neutral axis (compressed fraction ua, EN 1993-1-1 table 5.2)
        /// </summary>
        /// <returns>False in class 3 or 4: the plastic moment is not calculated (0)</returns>
        private bool PlasticWebClass()
        {
            _plasticClass = 0;
            _colorPlasticClass = VivianiColor.Red;
            _mPl = 0.0;
            _plasticLimit = 396.0 * _epsilon / (13.0 * _ua - 1.0);
            if (_ua <= 0.5)
                _plasticLimit = 36.0 * _epsilon / _ua;
            if (_plasticLimit >= _webSlenderness)
            {
                _plasticClass = 1;
                _colorPlasticClass = VivianiColor.Green;
                return true;
            }
            _plasticLimit = 456.0 * _epsilon / (13.0 * _ua - 1.0);
            if (_ua <= 0.5)
                _plasticLimit = 41.5 * _epsilon / _ua;
            if (_plasticLimit < _webSlenderness)
                return false;
            _plasticClass = 2;
            _colorPlasticClass = VivianiColor.Green;
            return true;
        }

        #endregion

        public VivianiResult Result() => new()
        {
            SteelSection = new(_aSteel, _ygSteel, _jSteel),
            LongTermSection = new(_longTerm.A, _longTerm.Yg, _longTerm.J),
            ShortTermSection = new(_shortTerm.A, _shortTerm.Yg, _shortTerm.J),
            CrackedSlab = _slabTopStress > 1E-05 | _hs <= 0.0,
            Sigma1 = _sig1, Sigma2 = _sig2, Sigma3 = _sig3, Sigma4 = _sig4, Sigma5 = _sig5, Sigma6 = _sig6,
            SigmaId3 = _sigId3, SigmaId4 = _sigId4, SigmaId5 = _sigId5, SigmaId6 = _sigId6,
            Tau = _tau4, ShearFlow = _shearFlow,
            MEd = _mEd, TEd = _tEd, MRd = _mPl, Utilization = _utilization,
            ElasticNeutralAxis = _yElastic, PlasticNeutralAxis = _yPlastic,
            WebSlenderness = _webSlenderness, ElasticLimit = _elasticLimit, PlasticLimit = _plasticLimit,
            ElasticClass = _elasticClass, PlasticClass = _plasticClass,
            EffectiveWebTop = _be1, EffectiveWebBottom = _be2, IterationsPerformed = _iteration,
            WebFydForBending = _fydWebReduced,
            ShearExceedsWebResistance = _colorTw == VivianiColor.Red, ShownWebThickness = _taw,
            WebThicknessColor = _colorTw, UtilizationColor = _colorUtilization, RebarAreaColor = _colorAf, WebFydColor = _colorFydWeb,
            ElasticClassColor = _colorElasticClass, PlasticClassColor = _colorPlasticClass
        };
    }
}
