using GPC.Model.Actions.Climate;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Climate;

public static class ThermalActions
{
    public static (double min,double max) AirTemperatures(ThermalSiteInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));
        National(x.Standard,x.NationalParameters); Range(x.AltitudeM,-500,9000,"Quota [m]");
        if(x.AssignedTemperatures)
        { Range(x.MinimumAirC,-100,100,"Tmin [°C]");Range(x.MaximumAirC,x.MinimumAirC,150,"Tmax [°C]");return(x.MinimumAirC,x.MaximumAirC); }
        Require(x.Standard==ClimateStandard.Ntc2018,"EN: assegnare Tmin/Tmax del sito da dati nazionali riferiti al periodo di ritorno richiesto.");
        Require(x.NtcZone>=1 && x.NtcZone<=4,"Zona termica NTC: I…IV.");
        double[,] c={{-15,.004,42,.006},{-8,.006,42,.002},{-8,.007,42,.0003},{-2,.009,42,.002}};
        int i=x.NtcZone-1;return(c[i,0]-c[i,1]*x.AltitudeM,c[i,2]-c[i,3]*x.AltitudeM);
    }
    private static ClimateResult Start(ThermalSiteInput site,string scope)
    {
        var r=Result(site.Standard,ThermalEn,scope,site.NationalParameters);var air=AirTemperatures(site);
        string source=site.Standard==ClimateStandard.Ntc2018&&!site.AssignedTemperatures?"NTC §3.5.2 · periodo 50 anni":"Dati climatici assegnati";
        r.Parameters.Add(V("Tmin","Temperatura minima aria all’ombra",air.min,"°C",site.AssignedTemperatures?"assegnata":"funzione zona e quota",source));
        r.Parameters.Add(V("Tmax","Temperatura massima aria all’ombra",air.max,"°C",site.AssignedTemperatures?"assegnata":"funzione zona e quota",source));return r;
    }
    private static void Kinematics(ClimateCase c,double uniform,double difference,double alpha,double length,double depth,string reference)
    {
        c.Values.Add(V("ΔTu","Componente uniforme (espansione positiva)",uniform,"°C","Tmedio − T0",reference));
        c.Values.Add(V("ΔTM","Differenza lineare (esterno/sopra meno interno/sotto)",difference,"°C","Tsup − Tinf",reference));
        c.Values.Add(V("εT","Deformazione termica libera",alpha*uniform,"–","α · ΔTu","Cinematica termica"));
        c.Values.Add(V("ΔL","Variazione libera di lunghezza",1000*alpha*uniform*length,"mm","1000 · α · ΔTu · L[m]","Cinematica termica"));
        c.Values.Add(V("κT","Curvatura termica libera",alpha*difference/depth,"1/m","α · ΔTM / h","Cinematica termica"));
    }
    public static ClimateResult Building(BuildingThermalInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x)); EnumValue(x.Method);EnumValue(x.Material);
        Positive(x.LengthM,"Lunghezza [m]");Positive(x.ThicknessM,"Spessore [m]");Range(x.ExpansionPerKelvin,double.Epsilon,.001,"Dilatazione α [1/K]");Range(x.InitialTemperatureC,-100,150,"T0 [°C]");
        double alpha=x.AssignedExpansionCoefficient?x.ExpansionPerKelvin:x.Material==ThermalMaterial.Steel?.000012:.00001;
        var r=Start(x.Site,"Azione termica su elemento di edificio; dilatazione e curvatura libere. Reazioni vincolari da analisi strutturale.");
        if(x.Method==BuildingThermalMethod.SimplifiedNtc)
        {
            Require(x.Site.Standard==ClimateStandard.Ntc2018,"Il metodo semplificato ±ΔT è NTC §3.5.5, non un metodo EN.");
            Require(x.SimplifiedApplicabilityConfirmed,"NTC §3.5.5: confermare che l’azione termica non è fondamentale per sicurezza o efficienza funzionale.");
            double dt=x.Material==ThermalMaterial.ReinforcedConcrete?(x.Exposed?15:10):(x.Exposed?25:15);
            foreach(int sign in new[]{-1,1})Kinematics(Case(r,sign>0?"Riscaldamento":"Raffreddamento"),sign*dt,0,alpha,x.LengthM,x.ThicknessM,"NTC tab. 3.5.II");
            r.Notes.Add("Metodo semplificato NTC: intervalli uniformi convenzionali. Non determina gradienti né sostituisce lo studio termico quando l’azione è fondamentale.");
        }
        else
        {
            Require(!string.IsNullOrWhiteSpace(x.ProfileSource),"Indicare la fonte del profilo termico/delle proprietà e della radiazione solare.");
            void Profile(string name,double outer,double inner)
            {
                Range(outer,-100,250,"Temperatura esterna superficie [°C]");Range(inner,-100,250,"Temperatura interna superficie [°C]");
                var c=Case(r,name,"Profilo lineare in uno strato omogeneo: media aritmetica delle superfici; differenza esterno−interno.");
                c.Values.Add(V("Tse","Superficie esterna",outer,"°C","profilo assegnato/trasmissione stazionaria", "EN §5.3, appendice D"));
                c.Values.Add(V("Tsi","Superficie interna",inner,"°C","profilo assegnato/trasmissione stazionaria", "EN §5.3, appendice D"));
                Kinematics(c,(outer+inner)/2-x.InitialTemperatureC,outer-inner,alpha,x.LengthM,x.ThicknessM,"NTC §3.5.4 / EN §5.2");
            }
            if(x.Method==BuildingThermalMethod.AssignedSurfaceProfile)
            {Profile("Estate · profilo assegnato",x.SummerOuterSurfaceC,x.SummerInnerSurfaceC);Profile("Inverno · profilo assegnato",x.WinterOuterSurfaceC,x.WinterInnerSurfaceC);}
            else
            {
                Positive(x.ConductivityWmK,"Conduttività λ [W/(m K)]");Range(x.InnerResistanceM2KW,0,100,"Rsi [m²K/W]");Range(x.OuterResistanceM2KW,0,100,"Rse [m²K/W]");Range(x.SolarIncrementC,0,100,"Incremento solare [°C]");
                double rt=x.InnerResistanceM2KW+x.ThicknessM/x.ConductivityWmK+x.OuterResistanceM2KW;var a=AirTemperatures(x.Site);
                r.Parameters.Add(V("Rtot","Resistenza termica totale",rt,"m²K/W","Rsi + h/λ + Rse","EN appendice D (D.2)"));
                void Layer(string name,double outside,double inside)
                {Range(inside,-100,150,"Temperatura aria interna [°C]");double flux=(outside-inside)/rt;Profile(name,outside-flux*x.OuterResistanceM2KW,inside+flux*x.InnerResistanceM2KW);}
                Layer("Estate · strato omogeneo",a.max+x.SolarIncrementC,x.SummerInnerAirC);Layer("Inverno · strato omogeneo",a.min,x.WinterInnerAirC);
            }
            r.Notes.Add("Fonte profilo: "+x.ProfileSource+". Modello stazionario monostrato senza ponti termici: pareti multistrato, transitori e profili non lineari richiedono analisi specifica.");
        }
        r.Parameters.Add(V("α","Coefficiente di dilatazione",alpha,"1/K",x.AssignedExpansionCoefficient?"assegnato":"materiale del progetto","NTC tab. 3.5.III / EN appendice C"));
        return r;
    }
    public static ClimateResult Bridge(BridgeThermalInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));EnumValue(x.Deck);EnumValue(x.Surfacing);
        Positive(x.LengthM,"Lunghezza [m]");Positive(x.DepthM,"Altezza impalcato [m]");Positive(x.WidthM,"Larghezza [m]");Range(x.ExpansionPerKelvin,double.Epsilon,.001,"α [1/K]");Range(x.OmegaN,0,1,"ωN");Range(x.OmegaM,0,1,"ωM");Range(x.HorizontalDifferenceC,0,100,"Differenza trasversale [°C]");
        var r=Start(x.Site,"Impalcati da ponte: componente uniforme e gradiente verticale equivalente lineare (Approccio 1), simultaneità e movimenti di appoggi/giunti.");
        r.Standard+=" · metodo " + ThermalEn + " §6.1, Approccio 1";
        double alpha=x.AssignedExpansionCoefficient?x.ExpansionPerKelvin:x.Deck<=BridgeDeck.Composite?.000012:.00001;
        r.Parameters.Add(V("α","Coefficiente di dilatazione",alpha,"1/K",x.AssignedExpansionCoefficient?"assegnato":"materiale impalcato","NTC tab. 3.5.III / EN appendice C"));
        var air=AirTemperatures(x.Site);int type=x.Deck<=BridgeDeck.SteelTrussOrPlate?0:x.Deck==BridgeDeck.Composite?1:2;
        double tmin,tmax;
        if(x.AssignedBridgeTemperatures){tmin=Range(x.MinimumBridgeC,-100,150,"Te,min");tmax=Range(x.MaximumBridgeC,tmin,200,"Te,max");}
        else
        {
            Range(air.min,-50,0,"Tmin nel diagramma EN fig. 6.1");Range(air.max,0,50,"Tmax nel diagramma EN fig. 6.1");
            tmin=air.min+new[]{-3,4,8}[type];tmax=air.max+new[]{16,4,2}[type]-(x.Deck==BridgeDeck.SteelTrussOrPlate?3:0);
        }
        Range(x.InitialTemperatureC,tmin,tmax,"Temperatura iniziale T0");
        double heat,cool,kh=1,kc=1;
        if(x.AssignedGradients){heat=Range(x.HeatingDifferenceC,0,100,"ΔTM,heat");cool=Range(x.CoolingDifferenceC,0,100,"ΔTM,cool");}
        else
        {
            double[] h={18,18,15,10,15,15},c={13,13,18,5,8,8};
            double[,] bare={{.7,.9},{.9,1},{.8,1.1}},water={{1.6,.6},{1.1,.9},{1.5,1}},ballast={{.6,1.4},{.8,1.2},{.6,1}};
            if(x.Surfacing==BridgeSurfacing.Unsurfaced){kh=bare[type,0];kc=bare[type,1];}
            else if(x.Surfacing==BridgeSurfacing.Waterproofed){kh=water[type,0];kc=water[type,1];}
            else if(x.Surfacing==BridgeSurfacing.Ballast750){kh=ballast[type,0];kc=ballast[type,1];}
            else
            {
                Require(x.SurfacingMm==50 || x.SurfacingMm==100 || x.SurfacingMm==150,"Tabella EN 6.2: spessori 50, 100 o 150 mm; per altri spessori assegnare gradienti documentati.");
                if(x.SurfacingMm>50){kh=type==0?.7:type==1?1:x.SurfacingMm==100?.7:.5;kc=type==0?1.2:1;}
            }
            heat=h[(int)x.Deck]*kh;cool=c[(int)x.Deck]*kc;
        }
        double expansion=tmax-x.InitialTemperatureC,contraction=x.InitialTemperatureC-tmin;
        void Add(string s,string d,double v,string formula,string reference)=>r.Parameters.Add(V(s,d,v,"°C",formula,reference));
        Add("Te,min","Minima uniforme impalcato",tmin,x.AssignedBridgeTemperatures?"assegnata":"Tmin + offset tipologia","EN fig. 6.1");Add("Te,max","Massima uniforme impalcato",tmax,x.AssignedBridgeTemperatures?"assegnata":"Tmax + offset tipologia","EN fig. 6.1");
        Add("ΔTN,exp","Intervallo espansione",expansion,"Te,max − T0","EN (6.2)");Add("ΔTN,con","Intervallo contrazione (modulo)",contraction,"T0 − Te,min","EN (6.1)");
        Add("ΔTM,heat","Riscaldamento superiore",heat,x.AssignedGradients?"assegnato":"tabella 6.1 · ksur,heat","EN tabelle 6.1–6.2");Add("ΔTM,cool","Raffreddamento superiore (modulo)",cool,x.AssignedGradients?"assegnato":"tabella 6.1 · ksur,cool","EN tabelle 6.1–6.2");
        r.Parameters.Add(V("ksur,heat","Fattore pavimentazione riscaldamento",kh,"–",x.AssignedGradients?"non applicato al gradiente assegnato":"tipologia e pavimentazione","EN tab. 6.2"));r.Parameters.Add(V("ksur,cool","Fattore pavimentazione raffreddamento",kc,"–",x.AssignedGradients?"non applicato al gradiente assegnato":"tipologia e pavimentazione","EN tab. 6.2"));
        foreach(var uniform in new[]{(name:"Espansione",value:expansion),(name:"Contrazione",value:-contraction)})
        {
            Kinematics(Case(r,uniform.name),uniform.value,0,alpha,x.LengthM,x.DepthM,"EN §6.1.3");
            foreach(var gradient in new[]{(name:"sopra caldo",value:heat),(name:"sopra freddo",value:-cool)})
            {
                Kinematics(Case(r,uniform.name+" + "+gradient.name+" · gradiente dominante"),x.OmegaN*uniform.value,gradient.value,alpha,x.LengthM,x.DepthM,"EN (6.3): ΔTM + ωN ΔTN");
                Kinematics(Case(r,uniform.name+" + "+gradient.name+" · uniforme dominante"),uniform.value,x.OmegaM*gradient.value,alpha,x.LengthM,x.DepthM,"EN (6.4): ωM ΔTM + ΔTN");
            }
        }
        Kinematics(Case(r,"Solo gradiente · sopra caldo"),0,heat,alpha,x.LengthM,x.DepthM,"EN §6.1.4.1");Kinematics(Case(r,"Solo gradiente · sopra freddo"),0,-cool,alpha,x.LengthM,x.DepthM,"EN §6.1.4.1");
        double allowance=x.BearingSettingTemperatureKnown?10:20;
        Kinematics(Case(r,"Appoggi/giunti · espansione","Maggiorazione raccomandata "+allowance+" °C; lunghezza efficace dal punto fisso."),expansion+allowance,0,alpha,x.LengthM,x.DepthM,"EN §6.1.3.3 nota 2");
        Kinematics(Case(r,"Appoggi/giunti · contrazione","Maggiorazione raccomandata "+allowance+" °C; lunghezza efficace dal punto fisso."),-contraction-allowance,0,alpha,x.LengthM,x.DepthM,"EN §6.1.3.3 nota 2");
        var horizontal=Case(r,"Gradiente orizzontale ±","Applicare entrambi i versi quando una faccia è più esposta al sole.");
        horizontal.Values.Add(V("|ΔTM,y|","Differenza trasversale",x.HorizontalDifferenceC,"°C","assegnata (raccomandato 5 °C)","EN §6.1.4.3"));horizontal.Values.Add(V("|κy|","Curvatura trasversale libera",alpha*x.HorizontalDifferenceC/x.WidthM,"1/m","α · |ΔTM,y| / b","Cinematica termica"));
        r.Notes.Add((x.Site.Standard==ClimateStandard.Ntc2018?"NTC §§3.5 e 5.1.3.9: dati climatici italiani; metodo degli impalcati EN come documento integrativo. ":"EN 1991-1-5:2003+AC:2009: dati climatici assegnati secondo la fonte nazionale dichiarata. ")+"Approccio 1 lineare; verificare scelta del metodo e parametri dell’appendice nazionale applicabile.");
        r.Notes.Add("Per cassoni e pile considerare inoltre differenze fra facce interne/esterne e fra elementi (EN §§6.1.4.4, 6.1.6, 6.2). Non sono inclusi interazione binario–ponte, attrito o rigidezza dei vincoli.");
        return r;
    }
}
