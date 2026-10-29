using GPC.Model.Actions.Climate;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Climate;

public static class WindClimate
{
    private static readonly double[,] Zones = { {25,1000,.40}, {25,750,.45}, {27,500,.37}, {28,500,.36}, {28,750,.40}, {28,500,.36}, {28,1000,.54}, {30,1500,.50}, {31,500,.32} };
    public static double NtcBaseSpeed(int zone, double altitudeM)
    {
        Require(zone >= 1 && zone <= 9, "Zona vento NTC: 1…9."); Range(altitudeM, -500, 1500, "Quota NTC [m]");
        double a0 = Zones[zone-1,1], ca = altitudeM <= a0 ? 1 : 1 + Zones[zone-1,2] * (altitudeM/a0 - 1);
        return Zones[zone-1,0] * ca;
    }
    public static ClimateResult Calculate(WindSiteInput x, double heightM)
    {
        if (x is null) throw new ArgumentNullException(nameof(x));
        var r = Result(x.Standard, WindEn, "Clima e pressione cinetica di picco fino a 200 m dal suolo.", x.NationalParameters);
        Range(heightM, 0, 200, "Altezza sul terreno [m]"); Range(x.AltitudeM, -500, 9000, "Quota sul mare [m]"); Range(x.ReturnYears, 2, 10000, "Periodo di ritorno [anni]");
        Positive(x.TopographyFactor, "Coefficiente topografico/orografico"); Positive(x.AirDensityKgM3, "Densità aria [kg/m³]");
        bool ntc = x.Standard == ClimateStandard.Ntc2018;
        Require(ntc || x.AssignedBaseSpeed, "EN: assegnare vb,0 dal dato nazionale, con correzione altimetrica già inclusa.");
        Require(x.AltitudeM <= 1500 || x.AssignedBaseSpeed, "Quota oltre 1500 m: assegnare la velocità da studio locale.");
        double vb = x.AssignedBaseSpeed ? Positive(x.BaseSpeedMs, "Velocità base [m/s]") : NtcBaseSpeed(x.NtcZone, x.AltitudeM);
        if (ntc && x.AltitudeM > 1500) Require(vb >= NtcBaseSpeed(x.NtcZone, 1500), "Velocità locale inferiore al minimo NTC a 1500 m.");
        double z0, zmin, kr, prob, vr, ki;
        if (ntc)
        {
            Require(x.ExposureCategory >= 1 && x.ExposureCategory <= 5, "Categoria esposizione NTC: I…V (1…5).");
            double[,] terrain = {{.17,.01,2},{.19,.05,4},{.20,.10,5},{.22,.30,8},{.23,.70,12}};
            int i = x.ExposureCategory-1; kr=terrain[i,0]; z0=terrain[i,1]; zmin=terrain[i,2];
            prob = x.ReturnYears == 50 ? 1 : .75*Math.Sqrt(1-.2*Math.Log(-Math.Log(1-1/x.ReturnYears)));
            vr=vb*prob; ki=1;
        }
        else
        {
            Require(x.ExposureCategory >= 0 && x.ExposureCategory <= 4, "Categoria terreno EN: 0…I…IV (0…4).");
            double[,] terrain = {{.003,1},{.01,1},{.05,2},{.3,5},{1,10}};
            z0=terrain[x.ExposureCategory,0]; zmin=terrain[x.ExposureCategory,1]; kr=.19*Math.Pow(z0/.05,.07);
            Range(x.ProbabilityK, 0, 1, "K probabilità"); Range(x.ProbabilityExponent, 0, 2, "n probabilità");
            Positive(x.DirectionFactor,"cdir"); Positive(x.SeasonFactor,"cseason"); Positive(x.TurbulenceFactor,"kI");
            prob=Math.Pow((1-x.ProbabilityK*Math.Log(-Math.Log(1-1/x.ReturnYears)))/(1-x.ProbabilityK*Math.Log(-Math.Log(.98))),x.ProbabilityExponent);
            vr=vb*x.DirectionFactor*x.SeasonFactor*prob; ki=x.TurbulenceFactor;
        }
        double log=Math.Log(Math.Max(heightM,zmin)/z0), co=x.TopographyFactor;
        double vm=vr*kr*co*log, iv=ki/(co*log), qb=.5*x.AirDensityKgM3*vr*vr/1000, qp=(1+7*iv)*.5*x.AirDensityKgM3*vm*vm/1000;
        string reference=ntc ? "NTC §§3.3.1–3.3.7" : "EN §§4.2–4.5";
        void Add(string s,string d,double v,string u,string f)=>r.Parameters.Add(V(s,d,v,u,f,reference));
        Add("vb","Velocità base",vb,"m/s",x.AssignedBaseSpeed?"assegnata":"vb,0 · ca"); Add("cprob","Coefficiente periodo di ritorno",prob,"–",ntc?"0,75√[1−0,2 ln(−ln(1−1/TR))]; 1 per TR=50":"[(1−K ln(−ln(1−1/TR)))/(1−K ln(−ln(0,98)))]^n");
        Add("vr","Velocità di riferimento",vr,"m/s",ntc?"vb · cprob":"vb,0 · cdir · cseason · cprob"); Add("z0","Rugosità",z0,"m","categoria terreno"); Add("zmin","Quota minima",zmin,"m","categoria terreno"); Add("kr","Fattore terreno",kr,"–","tabella NTC / 0,19(z0/0,05)^0,07 EN"); Add("z","Quota di riferimento",heightM,"m","assegnata");
        Add("vm","Velocità media",vm,"m/s","vr · kr · co · ln(max(z,zmin)/z0)"); Add("Iv","Intensità turbolenza",iv,"–","kI/[co · ln(max(z,zmin)/z0)]"); Add("qb","Pressione cinetica base",qb,"kN/m²","ρ · vr² / 2000"); Add("ce","Coefficiente esposizione",qp/qb,"–","qp/qb"); Add("qp","Pressione cinetica di picco",qp,"kN/m²","(1+7Iv) · ρ · vm² / 2000");
        r.Notes.Add("Categoria di esposizione, topografia e parametri del sito devono corrispondere alla direzione esaminata; non sono dedotti dalla sola zona geografica.");
        return r;
    }
    internal static double Peak(WindSiteInput site,double z)=>Calculate(site,z).Parameters.Single(p=>p.Symbol=="qp").Value;
}
