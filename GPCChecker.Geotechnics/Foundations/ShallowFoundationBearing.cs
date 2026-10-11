using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Foundations;

public enum FoundationBearingMethod { Eurocode7, Terzaghi, Meyerhof, Hansen, Vesic }
public enum FoundationShape { Rectangle, Strip, Circle }
public enum FoundationSoilCondition { Drained, Undrained }
public enum FoundationCompression { GeneralShear, TerzaghiLocalShear, VesicDensity }
public enum FoundationDesignProfile { Ntc2018, Characteristic }

/// <summary>
/// Homogeneous bearing stratum; horizontal ground and base. Units: mm, N, N mm, MPa, N/mm³, radians.
/// Actions are already combined DESIGN resultants at the underside, including foundation/cover weight
/// and the appropriate water actions. Drained: N and q effective; undrained: N and q total.
/// Do not subtract base pore pressure from N in the undrained total-stress expression.
/// Strip actions refer to a 1000 mm run, with no longitudinal shear or eccentricity.
/// No settlement, sliding, structural or seismic check is implied by the result.
/// </summary>
public sealed class ShallowFoundationInput
{
    public FoundationBearingMethod Method { get; set; }
    public FoundationShape Shape { get; set; }
    public FoundationSoilCondition Condition { get; set; }
    public FoundationCompression Compression { get; set; }
    public FoundationDesignProfile Profile { get; set; }
    public double Width { get; set; } = 2000;
    public double Length { get; set; } = 3000;
    public double Depth { get; set; } = 1000;
    public double FrictionAngle { get; set; } = Math.PI / 6;
    public double Cohesion { get; set; }
    public double UndrainedStrength { get; set; } = .075;
    public double UnitWeight { get; set; } = 18e-6;
    public double SaturatedUnitWeight { get; set; } = 20e-6;
    public double CoverUnitWeight { get; set; } = 18e-6;
    public double CoverSaturatedUnitWeight { get; set; } = 20e-6;
    public bool WaterEnabled { get; set; }
    public double WaterDepth { get; set; } = 3000;
    public double WaterUnitWeight { get; set; } = 9.81e-6;
    public double Surcharge { get; set; }
    public double Normal { get; set; } = 600000;
    public double HorizontalX { get; set; }
    public double HorizontalY { get; set; }
    /// <summary>Moment about X, causing eccentricity along Y (Length).</summary>
    public double MomentX { get; set; }
    /// <summary>Moment about Y, causing eccentricity along X (Width).</summary>
    public double MomentY { get; set; }
    public bool IncludeDepthFactors { get; set; } = true;
    /// <summary>Required for drained EN 1997-1 Annex D: base friction δ ≥ φ/2.</summary>
    public bool RoughBase { get; set; } = true;
    public double RelativeDensity { get; set; } = .5;
}

public sealed class FoundationTerm
{
    public double N { get; internal set; }
    public double Shape { get; internal set; } = 1;
    public double Depth { get; internal set; } = 1;
    public double Inclination { get; internal set; } = 1;
    public double Pressure { get; internal set; }
}

public sealed class ShallowFoundationResult
{
    public string Method { get; internal set; } = "";
    public string Reference { get; internal set; } = "";
    public string Profile { get; internal set; } = "";
    public string Status { get; internal set; } = "";
    public double EffectiveWidth { get; internal set; }
    public double EffectiveLength { get; internal set; }
    public double EffectiveArea { get; internal set; }
    public double EccentricityX { get; internal set; }
    public double EccentricityY { get; internal set; }
    public double Overburden { get; internal set; }
    public double BearingUnitWeight { get; internal set; }
    public double AdoptedFrictionAngle { get; internal set; }
    public double AdoptedCohesion { get; internal set; }
    public double ResistanceFactor { get; internal set; }
    public double UltimatePressure { get; internal set; }
    public double NetUltimatePressure { get; internal set; }
    public double DesignPressure { get; internal set; }
    public double DemandPressure { get; internal set; }
    public double Resistance { get; internal set; }
    public double? Utilization { get; internal set; }
    public FoundationTerm Cohesion { get; } = new();
    public FoundationTerm Surcharge { get; } = new();
    public FoundationTerm Weight { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Separate method families, no modifications to the wall or seismic engines.
/// EN 1997-1:2004/AC:2009 Annex D; Terzaghi Nγ table: USACE EM 1110-1-1905 (1992), Table 4-1.
/// Generalized methods and their restrictions are documented in docs/metodi/fondazioni-superficiali.md.
/// </summary>
public static class ShallowFoundationBearing
{
    public const string ContractVersion = "1";
    public static ShallowFoundationResult Calculate(ShallowFoundationInput x)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        Validate(x);
        var r = new ShallowFoundationResult { Method = x.Method.ToString() };
        double b = x.Width, l = x.Shape == FoundationShape.Strip ? 1000 : x.Shape == FoundationShape.Circle ? b : x.Length;
        double ex = x.MomentY / x.Normal, ey = x.MomentX / x.Normal;
        double be = b - 2 * Math.Abs(ex), le = l - 2 * Math.Abs(ey);
        if (be <= 0 || le <= 0) throw new ArgumentException("Risultante esterna alla base: dimensioni efficaci non positive.");
        if (x.Shape == FoundationShape.Circle && (ex != 0 || ey != 0))
            throw new NotSupportedException("Fondazione circolare eccentrica: geometria efficace non disponibile; non usare B′L′.");
        if (x.Shape == FoundationShape.Strip && (ey != 0 || x.HorizontalY != 0))
            throw new ArgumentException("Fondazione nastriforme: My e Hx trasversali; Mx e Hy devono essere nulli. Azioni per metro.");
        double area = x.Shape == FoundationShape.Circle ? Math.PI * b * b / 4 : be * le;
        double hx = Math.Abs(x.HorizontalX), hy = Math.Abs(x.HorizontalY);
        // Preserve axis association when the effective dimensions change order.
        if (x.Shape == FoundationShape.Rectangle && be > le)
        { (be, le) = (le, be); (b, l) = (l, b); (hx, hy) = (hy, hx); }
        double ratio = x.Shape == FoundationShape.Strip ? 0 : be / le;
        double h = Hypot(hx, hy);
        double q = x.Surcharge + CoverPressure(x);
        double gamma = x.UnitWeight;
        if (x.WaterEnabled)
        {
            double dryFraction = Math.Max(0, Math.Min(1, (x.WaterDepth - x.Depth) / be));
            gamma = dryFraction * x.UnitWeight + (1 - dryFraction) * (x.SaturatedUnitWeight - x.WaterUnitWeight);
            r.Warnings.Add(x.Condition == FoundationSoilCondition.Drained
                ? "Falda idrostatica: γ medio nello spessore B′ sotto la base; N efficace deve comprendere già la sottospinta."
                : "Non drenato a tensioni totali: q usa γsat; N totale non va ridotto della pressione interstiziale al piano di posa.");
        }
        double phi = x.Condition == FoundationSoilCondition.Undrained ? 0 : x.FrictionAngle;
        double c = x.Condition == FoundationSoilCondition.Undrained ? x.UndrainedStrength : x.Cohesion;
        if (x.Compression == FoundationCompression.TerzaghiLocalShear)
        { c *= 2d / 3; phi = Math.Atan(2d / 3 * Math.Tan(phi)); }
        if (x.Compression == FoundationCompression.VesicDensity)
            phi = Math.Atan((2d / 3 + x.RelativeDensity - .75 * x.RelativeDensity * x.RelativeDensity) * Math.Tan(phi));
        if (x.Compression != FoundationCompression.GeneralShear)
            r.Warnings.Add("Riduzione dei parametri per rottura locale selezionata dal progettista; nessuna diagnosi automatica del meccanismo.");
        if (x.Profile == FoundationDesignProfile.Ntc2018)
        {
            var combo = new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.ShallowFoundationBearing, GeotechnicalSituation.PersistentTransient).Single();
            phi = Math.Atan(Math.Tan(phi) / combo.Materials.TanFrictionAngle);
            c /= x.Condition == FoundationSoilCondition.Undrained ? combo.Materials.UndrainedShearStrength : combo.Materials.EffectiveCohesion;
            r.ResistanceFactor = combo.ResistanceFactor;
            r.Profile = "NTC 2018 §6.4.2.1 · A1+M1+R3 · azioni A1 già combinate";
        }
        else { r.ResistanceFactor = 1; r.Profile = "Confronto caratteristico · nessuna verifica normativa"; }
        r.EffectiveWidth = be; r.EffectiveLength = le; r.EffectiveArea = area;
        r.EccentricityX = ex; r.EccentricityY = ey; r.Overburden = q; r.BearingUnitWeight = gamma;
        r.AdoptedFrictionAngle = phi; r.AdoptedCohesion = c;
        if (Math.Abs(ex) > x.Width / 3 || Math.Abs(ey) > x.Length / 3)
            r.Warnings.Add("Grande eccentricità: EN 1997-1 §6.5.4 richiede precauzioni speciali; la sola portanza non conclude la verifica.");
        if (x.Method == FoundationBearingMethod.Terzaghi)
            Terzaghi(x, r, phi, c, q, gamma, be, ratio, h);
        else if (x.Condition == FoundationSoilCondition.Undrained)
            Undrained(x, r, c, q, b, ratio, h, hx, hy, area);
        else
            Drained(x, r, phi, c, q, gamma, b, be, le, ratio, h, hx, hy, area);
        r.UltimatePressure = r.Cohesion.Pressure + r.Surcharge.Pressure + r.Weight.Pressure;
        if (!Finite(r.UltimatePressure) || r.UltimatePressure < 0) throw new ArgumentException("Resistenza fuori dal dominio del metodo.");
        r.NetUltimatePressure = r.UltimatePressure - q;
        r.DesignPressure = r.UltimatePressure / r.ResistanceFactor;
        r.Resistance = r.DesignPressure * area;
        r.DemandPressure = x.Normal / area;
        r.Utilization = r.Resistance > 0 ? x.Normal / r.Resistance : (double?)null;
        if (!Finite(area) || area <= 0 || !Finite(r.Resistance) || !Finite(r.DemandPressure) || r.Utilization.HasValue && !Finite(r.Utilization.Value))
            throw new ArgumentException("Dimensioni o azioni fuori dall'intervallo numerico rappresentabile.");
        r.Status = r.Resistance >= x.Normal ? "Verificata" : "Non verificata";
        if (x.Profile == FoundationDesignProfile.Characteristic) r.Status = "Confronto caratteristico";
        r.Warnings.Add("Terreno omogeneo, piano campagna e base orizzontali. Esclusi sisma, cedimenti, scorrimento, stabilità globale e verifiche strutturali.");
        return r;
    }

    static void Drained(ShallowFoundationInput x, ShallowFoundationResult r, double phi, double c, double q, double gamma,
        double b, double be, double le, double ratio, double h, double hx, double hy, double area)
    {
        if (phi <= 0) throw new ArgumentException("Il caso drenato con φ′=0 non è implementato. Il ramo non drenato è utilizzabile soltanto quando il problema richiede cu, non sostituendo automaticamente c′ con cu.");
        double t = Math.Tan(phi), sin = Math.Sin(phi);
        double logNq = Math.PI * t + Log1p(2 * sin / (1 - sin));
        double nqMinusOne = Expm1(logNq), nq = nqMinusOne + 1, nc = nqMinusOne / t;
        var fc = r.Cohesion; var fq = r.Surcharge; var fg = r.Weight;
        fc.N = nc; fq.N = nq;
        double k = x.IncludeDepthFactors ? DepthRatio(x.Depth / b) : 0;
        if (x.Method == FoundationBearingMethod.Eurocode7)
        {
            if (!x.RoughBase) throw new NotSupportedException("EN 1997-1 allegato D: richiede base sufficientemente ruvida, δ ≥ φ′/2.");
            fg.N = 2 * nqMinusOne * t;
            fq.Shape = 1 + ratio * sin; fg.Shape = 1 - .3 * ratio;
            fc.Shape = 1 + ratio * sin * nq / nqMinusOne;
            r.Reference = "EN 1997-1:2004/AC:2009, allegato D.4";
        }
        else if (x.Method == FoundationBearingMethod.Meyerhof)
        {
            double kp = Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
            fc.Shape = 1 + .2 * kp * ratio;
            // USACE Table 4-3 explicitly supplies the missing 0°–10° interpolation.
            double limit = 10 * Math.PI / 180;
            double shapeK = phi < limit ? Math.Pow(Math.Tan(Math.PI / 4 + limit / 2), 2) * phi / limit : kp;
            double depthK = phi < limit ? Math.Tan(Math.PI / 4 + limit / 2) * phi / limit : Math.Sqrt(kp);
            fq.Shape = fg.Shape = 1 + .1 * shapeK * ratio;
            double depth = x.IncludeDepthFactors ? x.Depth / b : 0;
            fc.Depth = 1 + .2 * Math.Sqrt(kp) * depth;
            fq.Depth = fg.Depth = 1 + .1 * depthK * depth;
            fg.N = nqMinusOne * Math.Tan(1.4 * phi);
            double theta = Math.Atan2(h, x.Normal);
            fc.Inclination = fq.Inclination = Square(1 - theta / (Math.PI / 2));
            fg.Inclination = theta <= phi ? Square(1 - theta / phi) : 0;
            if (h > 0 && ratio > 0) throw new NotSupportedException("Meyerhof: carico inclinato limitato alla fondazione nastriforme, per non combinare fattori di forma e inclinazione controversi.");
            r.Reference = "Meyerhof (1963); USACE EM 1110-1-1905 (1992), tab. 4-3";
        }
        else
        {
            bool hansen = x.Method == FoundationBearingMethod.Hansen;
            fg.N = hansen ? 1.5 * nqMinusOne * t : 2 * (nq + 1) * t;
            fq.Shape = 1 + ratio * (hansen ? sin : t);
            fc.Shape = 1 + ratio * (hansen ? sin / t : 1) * nq / nc;
            fg.Shape = 1 - .4 * ratio;
            fq.Depth = 1 + 2 * t * Square(1 - sin) * k;
            fc.Depth = 1 + 2 * Square(1 - sin) * k * (t + 1 / nc);
            if (hansen && h > 0)
                throw new NotSupportedException("Brinch Hansen: il ramo con carico inclinato richiede la revisione della formulazione direzionale; non viene sostituito con coefficienti di altri metodi.");
            r.Reference = hansen ? "Brinch Hansen (1970), fattori correlati sc/sq e dc/dq; solo carico normale" : "Vesić (1975), formulazione generalizzata con fattori correlati dc/dq";
        }
        if (x.Method is FoundationBearingMethod.Eurocode7 or FoundationBearingMethod.Vesic)
        {
            double m = Exponent(ratio, h, hx, hy, x.Shape);
            // H*t / (N*t + A*c) avoids cot(phi) overflow near phi=0.
            double fraction = h * t / (x.Normal * t + area * c);
            if (fraction >= 1) throw new ArgumentException("Carico tangenziale fuori dal dominio della formula di inclinazione.");
            fq.Inclination = Math.Pow(1 - fraction, m);
            fg.Inclination = Math.Pow(1 - fraction, m + 1);
            double delta = Expm1(m * Log1p(-fraction));
            fc.Inclination = 1 + delta * nq / nqMinusOne;
            if (fc.Inclination < 0) throw new ArgumentException("Fattore di inclinazione della coesione negativo: resistenza fuori campo.");
        }
        fc.Pressure = c * fc.N * fc.Shape * fc.Depth * fc.Inclination;
        fq.Pressure = q * fq.N * fq.Shape * fq.Depth * fq.Inclination;
        fg.Pressure = .5 * gamma * be * fg.N * fg.Shape * fg.Depth * fg.Inclination;
    }

    static void Undrained(ShallowFoundationInput x, ShallowFoundationResult r, double cu, double q, double b, double ratio,
        double h, double hx, double hy, double area)
    {
        var f = r.Cohesion;
        f.N = Math.PI + 2;
        f.Shape = 1 + .2 * ratio;
        double k = x.IncludeDepthFactors ? DepthRatio(x.Depth / b) : 0;
        if (x.Method == FoundationBearingMethod.Hansen)
        {
            if (h > 0) throw new NotSupportedException("Hansen non drenato con carico inclinato: formula del fattore di forma da riscontrare sulla fonte originale.");
            // Additive expression, deliberately not the product of shape and depth factors.
            f.Shape = .2 * ratio; f.Depth = .4 * k; f.Inclination = 0;
            f.Pressure = cu * f.N * (1 + f.Shape + f.Depth);
            r.Reference = "Brinch Hansen (1970), non drenato, correzioni additive; carico normale";
        }
        else
        {
            if (x.Method == FoundationBearingMethod.Eurocode7)
            {
                if (h > area * cu) throw new ArgumentException("EN 1997-1 D.3: H deve essere ≤ A′ cu,d.");
                f.Inclination = .5 + .5 * Math.Sqrt(1 - h / (area * cu));
                r.Reference = "EN 1997-1:2004/AC:2009, allegato D.3";
            }
            else if (x.Method == FoundationBearingMethod.Meyerhof)
            {
                if (h > 0 && ratio > 0) throw new NotSupportedException("Meyerhof inclinato: solo fondazione nastriforme.");
                f.Depth = 1 + .2 * (x.IncludeDepthFactors ? x.Depth / b : 0);
                f.Inclination = Square(1 - Math.Atan2(h, x.Normal) / (Math.PI / 2));
                r.Reference = "Meyerhof (1963), non drenato";
            }
            else
            {
                f.Depth = 1 + .4 * k;
                f.Inclination = 1 - Exponent(ratio, h, hx, hy, x.Shape) * h / (area * cu * f.N);
                if (f.Inclination < 0) throw new ArgumentException("Vesić non drenato: fattore di inclinazione negativo.");
                r.Reference = "Vesić (1975), non drenato, base e terreno orizzontali";
            }
            f.Pressure = cu * f.N * f.Shape * f.Depth * f.Inclination;
        }
        r.Surcharge.N = 1; r.Surcharge.Pressure = q;
    }

    static void Terzaghi(ShallowFoundationInput x, ShallowFoundationResult r, double phi, double c, double q, double gamma, double be, double ratio, double h)
    {
        if (h != 0 || x.MomentX != 0 || x.MomentY != 0 || (x.Shape == FoundationShape.Rectangle && x.Width != x.Length))
            throw new NotSupportedException("Terzaghi: carico verticale centrato; fondazione nastriforme, quadrata o circolare.");
        if (x.Depth > x.Width) throw new NotSupportedException("Terzaghi: questo foglio limita D/B a 1.");
        var fc = r.Cohesion; var fq = r.Surcharge; var fg = r.Weight;
        fc.Shape = x.Shape == FoundationShape.Strip ? 1 : 1.3;
        fg.Shape = x.Shape == FoundationShape.Strip ? 1 : x.Shape == FoundationShape.Circle ? .6 : .8;
        if (phi == 0) { fq.N = 1; fc.N = x.RoughBase ? 1 + 1.5 * Math.PI : 2 + Math.PI; }
        else
        {
            if (!x.RoughBase) throw new NotSupportedException("Terzaghi drenato: implementata la sola base ruvida.");
            double logNq = (1.5 * Math.PI - phi) * Math.Tan(phi) - Log1p(-Math.Sin(phi));
            fq.N = Math.Exp(logNq); fc.N = Expm1(logNq) / Math.Tan(phi);
            fg.N = TerzaghiGamma(phi * 180 / Math.PI);
            r.Warnings.Add("Nγ di Terzaghi: interpolazione lineare della tabella USACE 4-1 (1992), non la formula incompleta del manuale di riferimento.");
        }
        fc.Pressure = c * fc.N * fc.Shape;
        fq.Pressure = q * fq.N;
        fg.Pressure = .5 * gamma * be * fg.N * fg.Shape;
        r.Reference = "Terzaghi (1943); Nγ tabulato in USACE EM 1110-1-1905 (1992), tab. 4-1 e 4-2";
    }

    public static double TerzaghiGamma(double degrees)
    {
        if (!Finite(degrees) || degrees < 0 || degrees > 50) throw new ArgumentOutOfRangeException(nameof(degrees));
        double[] a = { 0,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,35,36,38,40,42,44,45,46,48,50 };
        double[] n = { 0,.2,.4,.6,.9,1.2,1.7,2.3,3,3.9,4.9,5.8,7.8,11.7,15.7,19.7,27.9,36,42.4,52,80,100.4,180,257,297.5,420,780.1,1153.2 };
        int i = Array.FindIndex(a, v => v >= degrees);
        if (i == 0 || a[i] == degrees) return n[i];
        return n[i-1] + (n[i] - n[i-1]) * (degrees-a[i-1]) / (a[i]-a[i-1]);
    }
    static double CoverPressure(ShallowFoundationInput x)
    {
        if (!x.WaterEnabled) return x.Depth * x.CoverUnitWeight;
        double dry = Math.Min(x.Depth, x.WaterDepth);
        double submerged = x.CoverSaturatedUnitWeight - (x.Condition == FoundationSoilCondition.Drained ? x.WaterUnitWeight : 0);
        return dry * x.CoverUnitWeight + (x.Depth-dry) * submerged;
    }
    static double Exponent(double ratio, double h, double hx, double hy, FoundationShape shape)
    {
        if (shape == FoundationShape.Strip) return 2;
        if (h == 0) return (2 + ratio) / (1 + ratio);
        return (2 + ratio) / (1 + ratio) * Square(hx / h) + (1 + 2 * ratio) / (1 + ratio) * Square(hy / h);
    }
    static double DepthRatio(double x) => x <= 1 ? x : Math.Atan(x);
    static double Square(double x) => x * x;
    static double Hypot(double x, double y) { double max = Math.Max(x,y); return max == 0 ? 0 : max * Math.Sqrt(Square(x/max)+Square(y/max)); }
    static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    static double Expm1(double x)
    {
        if (Math.Abs(x) > 1e-5) return Math.Exp(x)-1;
        return x * (1 + x / 2 * (1 + x / 3 * (1 + x / 4 * (1 + x / 5))));
    }
    static double Log1p(double x)
    {
        if (Math.Abs(x) > 1e-5) return Math.Log(1+x);
        return x * (1 - x / 2 + x*x / 3 - x*x*x / 4 + x*x*x*x / 5);
    }
    static void Validate(ShallowFoundationInput x)
    {
        if (!Enum.IsDefined(typeof(FoundationBearingMethod),x.Method) || !Enum.IsDefined(typeof(FoundationShape),x.Shape)
            || !Enum.IsDefined(typeof(FoundationSoilCondition),x.Condition) || !Enum.IsDefined(typeof(FoundationCompression),x.Compression)
            || !Enum.IsDefined(typeof(FoundationDesignProfile),x.Profile)) throw new ArgumentException("Metodo o impostazione non riconosciuti.");
        if (new[] {x.Width,x.Length,x.Depth,x.FrictionAngle,x.Cohesion,x.UndrainedStrength,x.UnitWeight,x.SaturatedUnitWeight,
            x.CoverUnitWeight,x.CoverSaturatedUnitWeight,x.WaterDepth,x.WaterUnitWeight,x.Surcharge,x.Normal,x.HorizontalX,x.HorizontalY,
            x.MomentX,x.MomentY,x.RelativeDensity}.Any(v=>!Finite(v))) throw new ArgumentException("Tutti i dati devono essere numeri finiti.");
        if (x.Width<=0 || x.Shape == FoundationShape.Rectangle && x.Length<=0 || x.Depth<0 || x.Normal<=0 || x.UnitWeight<=0
            || x.CoverUnitWeight<=0 || x.Surcharge<0
            || x.Condition == FoundationSoilCondition.Drained && (x.Cohesion<0 || x.FrictionAngle<0 || x.FrictionAngle>50*Math.PI/180))
            throw new ArgumentException("Controllare geometria, risultante compressiva, resistenze e pesi di volume (0 ≤ φ′ ≤ 50°).");
        if (x.WaterEnabled && (x.WaterDepth<0 || x.WaterUnitWeight<=0 || x.SaturatedUnitWeight<=x.WaterUnitWeight || x.CoverSaturatedUnitWeight<=x.WaterUnitWeight
            || x.SaturatedUnitWeight<x.UnitWeight || x.CoverSaturatedUnitWeight<x.CoverUnitWeight))
            throw new ArgumentException("Falda: profondità non negativa e pesi saturi maggiori del peso dell'acqua.");
        if (x.Condition == FoundationSoilCondition.Undrained && x.UndrainedStrength<=0) throw new ArgumentException("Condizioni non drenate: cu deve essere positivo.");
        if (x.Condition == FoundationSoilCondition.Undrained && x.Compression != FoundationCompression.GeneralShear)
            throw new NotSupportedException("Le riduzioni dei parametri implementate riguardano c′ e tan φ′ in condizioni drenate, non cu.");
        if (x.Compression == FoundationCompression.VesicDensity && (x.Condition != FoundationSoilCondition.Drained || x.Cohesion != 0 || x.RelativeDensity<0 || x.RelativeDensity>2d/3))
            throw new ArgumentException("Riduzione Vesić per densità: solo sabbie drenate, c′=0 e 0 ≤ Dr ≤ 2/3.");
    }
}
