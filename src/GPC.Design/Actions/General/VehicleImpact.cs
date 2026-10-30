using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.General;

public static class VehicleImpact
{
    public static ClimateResult Calculate(VehicleImpactInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));National(x.Standard,x.NationalParameters);EnumValue(x.Target);EnumValue(x.Road);
        bool ntc=x.Standard==ClimateStandard.Ntc2018;string reference=ntc?"NTC 2018 §3.6.3.3.1":"EN 1991-1-7:2006 §§4.3–4.4";
        var r=Result(x.Standard,"EN 1991-1-7:2006","Urti veicolari: forze statiche equivalenti di progetto, casi alternativi",x.NationalParameters);r.Notes[0]="Forze eccezionali di progetto Ad, già pronte per la combinazione eccezionale. Non applicare i coefficienti delle azioni variabili ordinarie.";
        double force=x.Road switch{ImpactRoad.Motorway=>1000,ImpactRoad.Rural=>750,ImpactRoad.Urban=>500,ImpactRoad.CarParking=>50,_=>150};
        if(x.Target==ImpactTarget.Superstructure&&!ntc)force=x.Road switch{ImpactRoad.Motorway=>500,ImpactRoad.Rural=>375,ImpactRoad.Urban=>250,_=>75};
        double transverse=force/2;
        if(x.AssignedImpactForce){Positive(x.ImpactForceKn,"Forza assegnata");Range(x.TransverseForceKn,0,double.MaxValue,"Forza trasversale");Require(!string.IsNullOrWhiteSpace(x.ForceSource),"Documentare le forze assegnate.");force=x.ImpactForceKn;transverse=x.TransverseForceKn;r.Notes.Add("Forze assegnate: "+x.ForceSource);}
        double height,areaH,areaW;
        if(x.Target==ImpactTarget.Forklift)
        {force=5*Positive(x.ForkliftWeightKn,"Peso carrello e massimo carico");height=.75;var c=Case(r,"Carrello elevatore","Forza orizzontale nella direzione più gravosa.");c.Values.Add(V("F","Forza d’urto",force,"kN","5 W",reference));c.Values.Add(V("h","Quota di applicazione",height,"m","0,75 m",reference));r.Notes.Add("Il peso W comprende carrello e massimo carico trasportabile. La verifica della risposta dinamica/deformabilità resta distinta.");return r;}
        if(x.Target==ImpactTarget.SupportingMember)
        {
            bool car=x.Road==ImpactRoad.CarParking;Positive(x.MemberWidthM,"Larghezza membratura");height=car?.5:ntc?1.25:Range(x.EnTruckImpactHeightM,.5,1.5,"Quota impatto autocarro EN");areaH=car?.25:.5;areaW=Math.Min(1.5,x.MemberWidthM);
            foreach(var pair in new[]{("Parallelo alla marcia",force),("Ortogonale alla marcia",transverse)})
            {var c=Case(r,pair.Item1,"Caso non simultaneo all’altra direzione.");c.Values.Add(V("F","Forza orizzontale",pair.Item2,"kN",x.AssignedImpactForce?"Valore assegnato":"Tabella forze equivalenti",reference));c.Values.Add(V("M","Momento rispetto al piano viario",pair.Item2*height,"kN·m","F h",reference));}
            r.Notes.Add(ntc?"Autocarri: quota NTC 1,25 m; automobili: 0,50 m.":"Autocarri: esaminare l’intervallo di quote 0,50–1,50 m (o quote maggiori in presenza di protezioni specifiche); il foglio calcola la quota selezionata, non un inviluppo strutturale automatico.");
        }
        else
        {
            Positive(x.ClearanceM,"Franco netto");double h0=ntc?5:Positive(x.EnFullImpactClearanceM,"Franco per urto pieno"),h1=ntc?6:Positive(x.EnNoImpactClearanceM,"Franco senza urto");Require(h1>h0&&h1-h0<=1.0000001,"EN: intervallo di riduzione positivo e non superiore a 1 m.");
            double reduction=Clamp((h1-x.ClearanceM)/(h1-h0),0,1);force*=reduction;height=x.ClearanceM;areaH=areaW=.25;
            r.Parameters.Add(V("r","Riduzione per franco",reduction,"−","1 fino ad h0; (h1−h)/(h1−h0); 0 oltre h1",reference));
            var front=Case(r,"Prospetto verticale","Forza orizzontale sulla superficie verticale.");front.Values.Add(V("Fx","Urto frontale",force,"kN","r Fd,x",reference));
            var soffit=Case(r,"Intradosso · 10° verso l’alto","Caso alternativo sul piano inferiore: componenti della stessa risultante F.");soffit.Values.Add(V("Fx","Componente orizzontale",force*Math.Cos(Math.PI/18),"kN","F cos(10°)",reference));soffit.Values.Add(V("Fz","Componente verso l’alto",force*Math.Sin(Math.PI/18),"kN","F sin(10°)",reference));
            r.Notes.Add("Il franco è quello netto futuro al punto d’impatto, considerando ripavimentazioni, deformazioni e cedimenti. NTC: impalcato nel suo insieme, §5.1.3.13. EN: eventuali urti trasversali da definire per il progetto.");
        }
        r.Parameters.Add(V("h","Quota impatto",height,"m","Rispetto al piano di marcia",reference));r.Parameters.Add(V("a","Altezza impronta",areaH,"m","Impronta rettangolare",reference));r.Parameters.Add(V("b","Larghezza impronta",areaW,"m","min(1,50 m, larghezza membratura) per piedritti",reference));
        r.Notes.Add("Non comprende azioni sulle barriere di sicurezza e relativi ancoraggi, urti ferroviari/navali, incendio, esplosioni o analisi dinamiche d’impatto.");return r;
    }
}
