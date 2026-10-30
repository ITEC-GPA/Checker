using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.General;

public static class RoadTrafficActions
{
    public static ClimateResult Calculate(RoadTrafficInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));National(x.Standard,x.NationalParameters);
        bool ntc=x.Standard==ClimateStandard.Ntc2018;string reference=ntc?"NTC 2018 §5.1.3.3, Tab. 5.1.I–II":"EN 1991-2:2003 §§4.2.3–4.3.3, Tab. 4.1–4.2";
        Range(x.CarriagewayWidthM,3,60,"Larghezza carrabile");Range(x.LoadedLengthM,1.2,300,"Lunghezza caricata");Range(x.FootwayWidthM,0,20,"Larghezza marciapiede");Range(x.PavementThicknessM,0,2,"Spessore pavimentazione");Range(x.SlabThicknessM,0,3,"Spessore soletta");
        double[] aq=ntc?[1,1,1]:[x.AlphaQ1,x.AlphaQ2,x.AlphaQ3];double alphaq1=ntc?1:x.Alphaq1,alphaqo=ntc?1:x.AlphaqOther,alphaqr=ntc?1:x.AlphaqRemaining,beta=ntc?1:x.BetaQ;
        foreach(double f in aq.Concat(new[]{alphaq1,alphaqo,alphaqr,beta}))Range(f,.01,5,"Fattore di adattamento traffico");
        var r=Result(x.Standard,"EN 1991-2:2003","Ponti stradali ordinari: corsie convenzionali, LM1/schema 1, asse locale, frenatura, centrifuga e marciapiedi",x.NationalParameters);
        var lanes=Bridges.RoadTraffic.Ntc2018(x.CarriagewayWidthM);int count=lanes.Lanes.Count;double laneWidth=lanes.Lanes[0].Width;
        r.Parameters.Add(V("n","Corsie convenzionali",count,"−","w<5,4: 1; 5,4≤w<6: 2; w≥6: Int(w/3)",reference));
        r.Parameters.Add(V("w1","Larghezza corsia",laneWidth,"m","3 m oppure w/2",reference));double remaining=x.CarriagewayWidthM-count*laneWidth;
        r.Parameters.Add(V("wr","Zona rimanente",remaining,"m","w − n w1",reference));
        double sumTandem=0,sumUdl=0;
        foreach(var lane in lanes.Lanes)
        {
            int i=lane.Number;double axle=lane.AxleKn*(i<=3?aq[i-1]:1),q=lane.UniformKnPerSquareMetre*(i==1?alphaq1:alphaqo);sumTandem+=2*axle;sumUdl+=q*lane.Width*x.LoadedLengthM;
            var c=Case(r,"Corsia "+i,"LM1/schema 1: tandem completo e carico distribuito sulle parti sfavorevoli. Il totale qui usa l’intera lunghezza assegnata.");
            c.Values.Add(V("Qasse","Carico per asse",axle,"kN","αQ,i Qik",reference));c.Values.Add(V("Qruota","Carico per ruota",axle/2,"kN","Qasse/2",reference));c.Values.Add(V("TS","Tandem a due assi",2*axle,"kN","2 Qasse",reference));c.Values.Add(V("q","Carico distribuito",q,"kN/m²","αq,i qik",reference));c.Values.Add(V("UDL","Risultante distribuita assegnata",q*lane.Width*x.LoadedLengthM,"kN","q wi L",reference));
        }
        double rem=2.5*alphaqr*remaining*x.LoadedLengthM;sumUdl+=rem;
        var residual=Case(r,"Zona rimanente","Carico distribuito nelle sole porzioni sfavorevoli.");residual.Values.Add(V("qr","Intensità",2.5*alphaqr,"kN/m²","αqr × 2,5",reference));residual.Values.Add(V("Qr","Risultante assegnata",rem,"kN","qr wr L",reference));
        var all=Case(r,"LM1 / schema 1 · totale assegnato","Somma caratteristica sulla lunghezza interamente caricata; non è un inviluppo delle sollecitazioni.");all.Values.Add(V("ΣTS","Tandem",sumTandem,"kN","Σ 2 αQi Qik",reference));all.Values.Add(V("ΣUDL","Distribuiti e zona rimanente",sumUdl,"kN","Σ qi wi L + qr wr L",reference));
        double braking=Clamp(.6*(2*300*aq[0])+.1*9*alphaq1*laneWidth*x.LoadedLengthM,180*aq[0],900);
        string horizontal=ntc?"NTC 2018 §§5.1.3.5–5.1.3.6":"EN 1991-2:2003 §4.4";
        foreach(int sign in new[]{-1,1})
        {
            var c=Case(r,sign>0?"Frenatura +":"Frenatura − / accelerazione","A livello della pavimentazione, lungo l’asse della corsia più sfavorevole.");c.Values.Add(V("Qlong","Forza longitudinale",sign*braking,"kN","limiti [180 αQ1, 900]; 0,6(2 αQ1 Q1k)+0,1 αq1 q1k w1 L",horizontal));c.Values.Add(V("qlong","Distribuzione longitudinale equivalente",sign*braking/x.LoadedLengthM,"kN/m","Qlong/L",horizontal));
            if(!ntc)c.Values.Add(V("Qskew","Trasversale da frenatura obliqua, ove pertinente",.25*braking,"kN","± 0,25 |Qlong|; simultanea a Qlong",horizontal));
        }
        double centrifugal=0;if(!x.StraightBridge){Positive(x.RadiusM,"Raggio");centrifugal=x.RadiusM<200?.2*sumTandem:x.RadiusM<=1500?40*sumTandem/x.RadiusM:0;}
        Case(r,"Centrifuga","Forza radiale verso l’esterno della curva a livello viario.").Values.Add(V("Qcentr","Forza centrifuga",centrifugal,"kN","0,2 Qv (R<200); 40 Qv/R (200≤R≤1500); 0 oltre",horizontal));
        double joint=1;
        if(x.AddJointAmplification)
        {if(ntc){joint=Range(x.NtcJointFactor,1,3,"Fattore addizionale al giunto");Require(!string.IsNullOrWhiteSpace(x.JointSource),"NTC: documentare la valutazione dell’incremento al giunto.");}else{Range(x.JointDistanceM,0,double.MaxValue,"Distanza dal giunto");joint=Math.Max(1,1.3*(1-x.JointDistanceM/26));}}
        var local=Case(r,"LM2 / schema 2 · locale","Asse singolo alternativo a LM1, oppure singola ruota se più gravosa. Incremento al giunto applicato solo a questo caso locale.");
        local.Values.Add(V("φgiunto","Amplificazione aggiuntiva",joint,"−",ntc?"Assegnata da studio specifico":"max(1; 1,3(1−D/26))",ntc?"NTC §5.1.3.4":"EN §§4.3.3(3), 4.6.1(6)"));
        local.Values.Add(V("Qasse","Asse singolo",400*beta*joint,"kN","400 βQ φgiunto",reference));local.Values.Add(V("Qruota","Singola ruota",200*beta*joint,"kN","200 βQ φgiunto",reference));
        double spread=2*x.PavementThicknessM+x.SlabThicknessM,a=.35+spread,b=.6+spread;
        local.Values.Add(V("ax","Impronta diffusa longitudinale",a,"m","0,35 + 2 hpav + hsoletta",reference));local.Values.Add(V("ay","Impronta diffusa trasversale",b,"m","0,60 + 2 hpav + hsoletta",reference));local.Values.Add(V("p","Pressione diffusa singola ruota",200*beta*joint/(a*b),"kN/m²","Qruota/(ax ay)",reference));
        var foot=Case(r,"Marciapiedi e folla","Casi distinti: folla caratteristica, carico concomitante a LM1 e concentrato locale. Non sommarli tutti.");
        foot.Values.Add(V("qfolla","Folla caratteristica",5,"kN/m²","Valore nominale",ntc?"NTC schema 5":"EN §4.3.5 LM4"));foot.Values.Add(V("qconcom","Marciapiede concomitante a LM1",ntc?2.5:3,"kN/m²","Valore di combinazione del gruppo",ntc?"NTC Tab. 5.1.IV":"EN Tab. 4.4a nota b"));foot.Values.Add(V("Qfolla","Risultante sul marciapiede assegnato",5*x.FootwayWidthM*x.LoadedLengthM,"kN","5 bmarciapiede L",reference));
        foot.Values.Add(V("Qlocale","Concentrato locale su marciapiede protetto",10,"kN","Impronta 0,10 × 0,10 m",ntc?"NTC schema 4":"EN §5.3.2.2"));
        if(ntc)foot.Values.Add(V("Qnonprotetto","Concentrato su marciapiede non protetto",150,"kN","Impronta 0,40 × 0,40 m; caso alternativo", "NTC schema 3"));
        r.Notes.Add("Una sola carreggiata, larghezza utile fra cordoli/barriere. La numerazione esprime gravosità, non posizione fissa: occorre esaminare disposizione trasversale, corsie caricate e posizioni longitudinali sfavorevoli. Il modulo di ripartizione consente di assegnarle esplicitamente.");
        r.Notes.Add("Tandem LM1: interasse assi 1,20 m, interasse ruote 2,00 m, impronte 0,40 × 0,40 m. Dinamica ordinaria inclusa. Diffusione locale a 45° fino al piano medio della soletta; non verifica punzonamento, flessione o sovrapposizione delle impronte.");
        r.Notes.Add(ntc?"Gruppi Tab. 5.1.IV: 1 (verticali caratteristici + marciapiedi 2,5); 2a (verticali frequenti + frenatura); 2b (verticali frequenti + centrifuga). Per schema 1: ψ1 tandem=0,75, distribuiti=0,40. Gruppi alternativi.":"Gruppi EN Tab. 4.4a alternativi: gr1a LM1 + marciapiedi 3; gr1b LM2; gr2 LM1 frequente + azioni orizzontali; gr3 pedoni; gr4 folla. La composizione normativa completa con altre azioni richiede EN 1990 Allegato A2 e relativa appendice nazionale.");
        r.Notes.Add("Esclusi trasporti eccezionali, modelli di fatica, ponti ferroviari, passerelle con risposta dinamica pedonale e schemi per luci oltre 300 m. Valori EN di prima generazione: coefficienti α/β assegnati, raccomandazioni restanti da confermare nella fonte nazionale.");return r;
    }
}
