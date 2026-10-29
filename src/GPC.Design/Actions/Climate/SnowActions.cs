using GPC.Model.Actions.Climate;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Climate;

/// <summary>Characteristic roof snow loads, vertical and referred to the horizontal projection.</summary>
public static class SnowActions
{
    public static double NtcGroundLoad(SnowZone zone, double altitudeM)
    {
        EnumValue(zone); Range(altitudeM, -500, 1500, "Quota NTC [m]");
        if (altitudeM <= 200) return zone == SnowZone.III ? .6 : zone == SnowZone.II ? 1 : 1.5;
        double a = zone == SnowZone.AlpineI ? 1.39 : zone == SnowZone.MediterraneanI ? 1.35 : zone == SnowZone.II ? .85 : .51;
        double b = zone == SnowZone.AlpineI ? 728 : zone == SnowZone.MediterraneanI ? 602 : 481;
        return a * (1 + Math.Pow(altitudeM / b, 2));
    }
    public static double Shape(double pitchDeg, bool guards)
    { Range(pitchDeg, 0, 90, "Inclinazione falda [°]"); double mu = pitchDeg <= 30 ? .8 : pitchDeg < 60 ? .8 * (60 - pitchDeg) / 30 : 0; return guards ? Math.Max(.8, mu) : mu; }

    public static ClimateResult Calculate(SnowInput x)
    {
        if (x is null) throw new ArgumentNullException(nameof(x));
        var r = Result(x.Standard, SnowEn, "Neve ordinaria: carichi verticali per area in proiezione orizzontale.", x.NationalParameters);
        EnumValue(x.Zone); EnumValue(x.Roof); EnumValue(x.Exposure); Range(x.AltitudeM, -500, 9000, "Quota [m]");
        bool ntc = x.Standard == ClimateStandard.Ntc2018;
        Require(ntc || x.AssignedGroundLoad, "EN: assegnare sk dalla normativa nazionale del sito.");
        Require(x.AltitudeM <= 1500 || x.AssignedGroundLoad, "Oltre 1500 m assegnare il carico da studio locale.");
        double sk = x.AssignedGroundLoad ? Positive(x.GroundLoadKnM2, "Carico neve al suolo [kN/m²]") : NtcGroundLoad(x.Zone, x.AltitudeM);
        if (ntc && x.AltitudeM > 1500) Require(sk >= NtcGroundLoad(x.Zone, 1500), "Il carico locale non può essere inferiore al valore NTC a 1500 m.");
        double ce = x.Exposure == SnowExposure.Normal ? 1 : x.Exposure == SnowExposure.Windswept ? (ntc ? .9 : .8) : (ntc ? 1.1 : 1.2);
        Positive(x.ThermalCoefficient, "Ct"); if (x.ThermalCoefficient != 1) Require(!string.IsNullOrWhiteSpace(x.ThermalJustification), "Ct diverso da 1: documentare lo studio termico e la sua applicabilità.");
        string reference = ntc ? "NTC 2018 §3.4" : SnowEn + " §§4–5";
        r.Parameters.Add(V("sk", "Carico caratteristico al suolo", sk, "kN/m²", x.AssignedGroundLoad ? "valore assegnato" : "zonazione e quota", reference));
        r.Parameters.Add(V("Ce", "Coefficiente di esposizione", ce, "–", "categoria topografica", ntc ? "NTC tab. 3.4.I" : "EN tab. 5.1"));
        r.Parameters.Add(V("Ct", "Coefficiente termico", x.ThermalCoefficient, "–", x.ThermalJustification, reference));
        double q = sk * ce * x.ThermalCoefficient;
        void Load(ClimateCase c, string zone, double mu, string shape = "uniforme")
        { c.Values.Add(V("μ · " + zone, shape, mu, "–", "coefficiente di forma", reference)); c.Values.Add(V("s · " + zone, "Carico verticale · " + shape, q * mu, "kN/m²", "μ · Ce · Ct · sk", reference)); }
        double left = Shape(x.PitchLeftDeg, x.SnowGuards), right = Shape(x.PitchRightDeg, x.SnowGuards);
        switch (x.Roof)
        {
            case SnowRoof.Flat: Load(Case(r, "Copertura piana"), "copertura", .8); break;
            case SnowRoof.Monopitch: Load(Case(r, "Falda singola"), "falda", left); break;
            case SnowRoof.Duopitch:
                var balanced = Case(r, "Neve non accumulata"); Load(balanced, "sinistra", left); Load(balanced, "destra", right);
                var driftRight = Case(r, "Accumulo verso destra"); Load(driftRight, "sinistra", .5 * left); Load(driftRight, "destra", right);
                var driftLeft = Case(r, "Accumulo verso sinistra"); Load(driftLeft, "sinistra", left); Load(driftLeft, "destra", .5 * right); break;
            case SnowRoof.MultiSpan:
                Require(x.PitchLeftDeg <= 60 && x.PitchRightDeg <= 60, "Coperture multiple: oltre 60° serve uno studio specifico.");
                var full = Case(r, "Falde multiple · non accumulata"); Load(full, "sinistra", left); Load(full, "destra", right);
                double mu2 = .8 + .8 * Math.Min((x.PitchLeftDeg + x.PitchRightDeg) / 2, 30) / 30;
                var valley = Case(r, "Accumulo nelle valli", "Distribuzione triangolare nelle falde interne: zero al colmo, picco μ2 nella valle; falde esterne con μ1. EN fig. 5.4.");
                Load(valley, "valle", mu2, "picco triangolare"); Load(valley, "falda esterna sinistra", left); Load(valley, "falda esterna destra", right); break;
            case SnowRoof.Cylindrical:
                Positive(x.WidthM, "Luce cilindrica [m]"); Range(x.RiseM, 0, x.WidthM / 2, "Freccia [m]");
                Require(!x.SnowGuards, "Copertura cilindrica con fermaneve: coefficienti da studio specifico/appendice nazionale.");
                double mu3 = Math.Min(2, .2 + 10 * x.RiseM / x.WidthM);
                Load(Case(r, "Cilindrica · non accumulata", "Solo tratto con tangente β ≤ 60°; oltre, μ = 0."), "tratto caricato", .8);
                foreach (bool reverse in new[] { false, true })
                {
                    var cyl = Case(r, "Cilindrica · accumulo " + (reverse ? "sinistro" : "destro"), "EN fig. 5.6: due triangoli su ls/2; zero agli estremi e al centro, picchi a ls/4 e 3ls/4. Tratto caricato β ≤ 60°.");
                    Load(cyl, "picco sinistro", mu3 * (reverse ? 1 : .5), "picco triangolare"); Load(cyl, "picco destro", mu3 * (reverse ? .5 : 1), "picco triangolare");
                }
                break;
            case SnowRoof.LowerRoof:
                Positive(x.WidthM, "Larghezza copertura inferiore b2 [m]"); Positive(x.UpperWidthM, "Larghezza copertura superiore b1 [m]"); Positive(x.StepHeightM, "Dislivello h [m]"); Positive(x.SnowDensityKnM3, "Peso specifico neve [kN/m³]"); Range(x.SlidingCoefficient, 0, 100, "μs");
                Require(x.PitchLeftDeg <= 15 || x.SlidingCoefficient > 0, "Falda superiore oltre 15°: assegnare μs ricavato dal 50% del carico massimo sulla falda adiacente (EN §5.3.6).");
                double ls = Clamp(2 * x.StepHeightM, 5, 15), muw = Clamp(Math.Min((x.UpperWidthM + x.WidthM) / (2 * x.StepHeightM), x.SnowDensityKnM3 * x.StepHeightM / sk), .8, 4);
                double peak = muw + (x.PitchLeftDeg > 15 ? x.SlidingCoefficient : 0);
                r.Parameters.Add(V("ls", "Lunghezza teorica accumulo", ls, "m", "min(15; max(5; 2h))", "EN §5.3.6"));
                r.Parameters.Add(V("μw", "Accumulo da vento", muw, "–", "min((b1+b2)/(2h); γh/sk), limiti 0,8…4", "EN §5.3.6"));
                Load(Case(r, "Copertura inferiore · non accumulata"), "copertura", .8);
                var lower = Case(r, "Copertura inferiore · accumulo", "Distribuzione lineare dal picco presso il salto fino a μ=0,8 a distanza ls, poi uniforme; troncamento al bordo se b2<ls.");
                Load(lower, "salto x=0", peak); Load(lower, "bordo x=b2", Lerp(Math.Min(x.WidthM, ls), 0, ls, peak, .8)); break;
        }
        if (x.Roof >= SnowRoof.MultiSpan) r.Notes.Add("Geometria e accumuli secondo " + SnowEn + " §§5.3.4–5.3.6 (documento integrativo per NTC). Limiti raccomandati EN; accumuli eccezionali dell’appendice B e neve sporgente non inclusi.");
        r.Notes.Add("Gli accumuli locali presso ostacoli/parapetti e lo scivolamento su elementi di protezione richiedono casi aggiuntivi; non sommare i casi alternativi.");
        return r;
    }
}
