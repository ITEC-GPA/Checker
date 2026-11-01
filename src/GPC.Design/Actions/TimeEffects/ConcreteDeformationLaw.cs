using GPC.Model.Actions.Climate;
using GPC.Model.Materials;
using C = GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.TimeEffects;

/// <summary>EN 1992-1-1:2004 §§3.1.2–3.1.4 and Annex B; optional NTC 2018 Table 11.2.Va drying values.
/// Constant 20 °C and RH, normal-weight concrete, h0 >= 100 mm. Shortening negative.</summary>
public sealed class ConcreteDeformationLaw
{
    private readonly ConcreteMaterialEN1992 material;
    public double FckMpa { get; }
    public double HumidityPercent { get; }
    public double NotionalSizeMm { get; }
    public double DryingStartDays { get; }
    public ConcreteMaterialEuropeanCommon.CementTypes Cement { get; }
    public double Kh { get; }
    public double DryingInfinity { get; }
    public double AutogenousInfinity { get; }
    public double Ecm28Mpa => material.Ecm;
    public double CreepReferenceModulusMpa => 1.05 * Ecm28Mpa;

    public ConcreteDeformationLaw(ClimateStandard standard,double fckMpa,ConcreteMaterialEuropeanCommon.CementTypes cement,
        double humidityPercent,double notionalSizeMm,double dryingStartDays)
    {
        C.EnumValue(standard);C.EnumValue(cement);C.Range(fckMpa,standard==ClimateStandard.Ntc2018?20:12,standard==ClimateStandard.Ntc2018?80:90,"fck");
        C.Range(humidityPercent,40,100,"Umidità relativa");C.Range(notionalSizeMm,100,double.MaxValue,"h0 (campo del modello: almeno 100 mm)");C.Range(dryingStartDays,0,double.MaxValue,"Inizio essiccamento");
        FckMpa=fckMpa;Cement=cement;HumidityPercent=humidityPercent;NotionalSizeMm=notionalSizeMm;DryingStartDays=dryingStartDays;
        material=new ConcreteMaterialEN1992("Effetti differiti",fckMpa,ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,cementType:cement);
        Kh=notionalSizeMm<=200?C.Lerp(notionalSizeMm,100,200,1,.85):notionalSizeMm<=300?C.Lerp(notionalSizeMm,200,300,.85,.75):notionalSizeMm<500?C.Lerp(notionalSizeMm,300,500,.75,.7):.7;
        if(standard==ClimateStandard.Ntc2018)DryingInfinity=Kh*NtcDrying(fckMpa,humidityPercent);
        else
        {
            double a1=cement==ConcreteMaterialEuropeanCommon.CementTypes.ClassS?3:cement==ConcreteMaterialEuropeanCommon.CementTypes.ClassR?6:4;
            double a2=cement==ConcreteMaterialEuropeanCommon.CementTypes.ClassS?.13:cement==ConcreteMaterialEuropeanCommon.CementTypes.ClassR?.11:.12;
            DryingInfinity=-Kh*.85*(220+110*a1)*Math.Exp(-a2*(fckMpa+8)/10)*1e-6*1.55*(1-Math.Pow(humidityPercent/100,3));
        }
        AutogenousInfinity=-2.5*(fckMpa-10)*1e-6;
    }
    public double EcmAt(double ageDays){Age(ageDays);return material.GetEcm(material.Fcm,ageDays);}
    public double FckAt(double ageDays)
    {Age(ageDays);C.Require(ageDays>3,"La stima della resistenza richiede t > 3 giorni; per età inferiori servono prove.");return ageDays>=28?FckMpa:Math.Max(0,material.GetBetaCC(ageDays)*(FckMpa+8)-8);}
    public double FctmAt(double ageDays)
    {Age(ageDays);return material.GetFctm(ageDays);}
    public double DryingAt(double ageDays)
    {Age(ageDays);double dt=Math.Max(0,ageDays-DryingStartDays);return DryingInfinity*dt/(dt+.04*Math.Pow(NotionalSizeMm,1.5));}
    public double AutogenousAt(double ageDays){Age(ageDays);return AutogenousInfinity*(1-Math.Exp(-.2*Math.Sqrt(ageDays)));}
    public double ShrinkageAt(double ageDays)=>DryingAt(ageDays)+AutogenousAt(ageDays);
    public ConcreteTimeEffects CreepAtLoading(double loadingAgeDays)=>new(FckMpa,Cement,HumidityPercent,NotionalSizeMm,loadingAgeDays);
    private static void Age(double age)=>C.Positive(age,"Età del calcestruzzo");
    private static double NtcDrying(double fck,double humidity)
    {
        // NTC 2018 Table 11.2.Va, shortening in per mille; bilinear interpolation, no extrapolation.
        double[] strengths={20,40,60,80},rh={20,40,60,80,90,100};
        double[,] strains={{-.62,-.58,-.49,-.30,-.17,0},{-.48,-.46,-.38,-.24,-.13,0},{-.38,-.36,-.30,-.19,-.10,0},{-.30,-.28,-.24,-.15,-.07,0}};
        int a=0,b=0;while(a<strengths.Length-2&&fck>strengths[a+1])a++;while(b<rh.Length-2&&humidity>rh[b+1])b++;
        double lo=C.Lerp(humidity,rh[b],rh[b+1],strains[a,b],strains[a,b+1]),hi=C.Lerp(humidity,rh[b],rh[b+1],strains[a+1,b],strains[a+1,b+1]);
        return C.Lerp(fck,strengths[a],strengths[a+1],lo,hi)*.001;
    }
}
