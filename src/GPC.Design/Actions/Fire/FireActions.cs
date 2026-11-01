using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Fire;
using C=GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Fire;

/// <summary>Thermal boundary actions only. No conduction, section temperature, strength or fire resistance solver.</summary>
public static class FireActions
{
    private const string En="EN 1991-1-2:2002 §§3.1–3.2";
    private const string Ntc="NTC 2018 §3.6.1.5.1, espressioni 3.6.2–3.6.4";
    public static FireActionsResult Calculate(FireActionsInput i)
    {
        Validate(i);string reference=i.Standard==ClimateStandard.Ntc2018?Ntc:En;
        var r=new FireActionsResult{Summary=C.Result(i.Standard,reference,"Temperatura dei gas e condizioni termiche alle facce. Flussi opzionali su temperature superficiali prescritte; nessun campo termico interno o verifica R/REI.",i.NationalParameters)};
        r.Summary.Notes.Clear();r.Summary.Notes.Add("Le curve nominali descrivono i gas e non comprendono raffreddamento. La storia assegnata può includerlo: tempi e temperature devono coprire l’intera durata, senza extrapolazione. L’interpolazione è lineare fra i punti.");
        r.Summary.Notes.Add("Il foglio non risolve la propagazione del calore. La temperatura superficiale deve provenire da un’analisi termica oppure essere un valore di riferimento dichiarato. Una superficie mantenuta a 20 °C serve soltanto a confrontare flussi su tale riferimento: non rappresenta l’evoluzione di una sezione reale.");
        r.Summary.Notes.Add("Flusso positivo entrante nella superficie. Convezione e irraggiamento sono sommati con il loro segno, anche nel raffreddamento. L’irraggiamento usa θr=θg solo con conferma dell’ipotesi; costante σ=5,67·10⁻⁸ W/(m² K⁴) e conversione +273 come nell’espressione EN 3.3.");
        r.Summary.Notes.Add("Le facce sono segmenti assegnati: la geometria serve a identificare l’esposizione e l’area estrusa. Non viene dedotta la schermatura né verificata la chiusura del contorno. Faccia non esposta: αc=4 con irraggiamento separato oppure 9 con irraggiamento già incluso; adiabatico: flusso nullo.");
        r.Summary.Notes.Add("Durata, scenario e condizioni al contorno: "+i.FireScenarioSource+". Parametri termici: "+i.BoundaryParametersSource+". Fonte dei parametri nazionali: "+i.NationalParameters);
        if(i.Standard==ClimateStandard.Ntc2018)r.Summary.Notes.Add("Curve nominali NTC 2018; coefficienti di scambio e modello del flusso esplicitamente da EN 1991-1-2:2002. Durata richiesta, resistenza al fuoco, protezioni e combinazione eccezionale richiedono valutazioni separate.");
        r.Summary.Parameters.Add(C.V("tend","Durata dello scenario",i.DurationMinutes,"min","assegnata",reference));r.Summary.Parameters.Add(C.V("Δt","Passo massimo di campionamento",i.TimeStepMinutes,"min","punti delle storie e tempi di osservazione aggiunti esattamente","Campionamento, non passo di un solutore termico"));
        double[] observation=new[]{0d,15,20,30,45,60,90,120,180,240,360,i.DurationMinutes}.Where(t=>t<=i.DurationMinutes).Distinct().OrderBy(t=>t).ToArray();
        var times=new SortedSet<double>(observation);int count=(int)Math.Ceiling(i.DurationMinutes/i.TimeStepMinutes);
        for(int n=0;n<=count;n++)times.Add(Math.Min(i.DurationMinutes,n*i.TimeStepMinutes));
        if(i.Curve==FireCurve.AssignedHistory)foreach(var point in i.GasTemperatures.Where(p=>p.TimeMinutes<=i.DurationMinutes))times.Add(point.TimeMinutes);
        if(i.CalculateBoundaryFlux)foreach(var p in i.SurfaceTemperatures.Where(p=>p.TimeMinutes<=i.DurationMinutes))times.Add(p.TimeMinutes);
        foreach(double time in times)r.GasHistory.Add(new FireTemperaturePoint{TimeMinutes=time,TemperatureC=i.Curve==FireCurve.AssignedHistory?Interpolate(i.GasTemperatures.Select(p=>(p.TimeMinutes,p.TemperatureC)).ToArray(),time):NominalTemperature(i.Curve,time)});
        for(int faceIndex=0;faceIndex<i.FireFaces.Count;faceIndex++)
        {
            var face=i.FireFaces[faceIndex];double dx=face.EndXM-face.StartXM,dy=face.EndYM-face.StartYM;
            double alpha=face.Exposure==FireExposure.Adiabatic?0:face.AssignedConvection?face.ConvectionWm2K:face.Exposure==FireExposure.Unexposed?(face.CombinedUnexposedTransfer?9:4):i.Curve==FireCurve.Hydrocarbon?50:25;
            var f=new FireFaceResult{Name=face.Name,Exposure=face.Exposure,LengthM=Math.Sqrt(dx*dx+dy*dy),ConvectionWm2K=alpha};f.AreaM2=f.LengthM*i.LengthM;
            r.Summary.Parameters.Add(C.V(face.Name+" · αc","Coefficiente di scambio",alpha,"W/(m² K)",face.AssignedConvection?"assegnato":face.CombinedUnexposedTransfer?"convezione e irraggiamento inclusi":"valore EN per esposizione/curva",En));
            r.Summary.Parameters.Add(C.V(face.Name+" · A","Area della faccia estrusa",f.AreaM2,"m²","lunghezza segmento × lunghezza elemento","Geometria assegnata"));
            var history=i.SurfaceTemperatures.Where(p=>p.Face==faceIndex+1).Select(p=>(p.TimeMinutes,p.TemperatureC)).ToArray();
            foreach(var gas in r.GasHistory)
            {
                double env=face.Exposure==FireExposure.Exposed?gas.TemperatureC:i.AmbientC;var p=new FireFluxPoint{TimeMinutes=gas.TimeMinutes,GasC=env};
                if(face.Exposure==FireExposure.Adiabatic){p.ConvectionWm2=p.RadiationWm2=p.NetWm2=0;}
                else if(i.CalculateBoundaryFlux)
                {
                    double surface=face.SurfaceState==FireSurfaceState.ConstantReference?face.ReferenceSurfaceC:Interpolate(history,gas.TimeMinutes);
                    double conv=alpha*(env-surface),rad=face.Exposure==FireExposure.Unexposed&&face.CombinedUnexposedTransfer?0:face.ConfigurationFactor*face.MemberEmissivity*(face.Exposure==FireExposure.Exposed?i.FireEmissivity:1)*5.67e-8*(Math.Pow(env+273,4)-Math.Pow(surface+273,4));
                    p.SurfaceC=surface;p.ConvectionWm2=conv;p.RadiationWm2=rad;p.NetWm2=conv+rad;C.Range(p.NetWm2.Value,-double.MaxValue,double.MaxValue,"Flusso netto");
                }
                f.History.Add(p);
            }
            r.Faces.Add(f);if(i.CalculateBoundaryFlux&&face.Exposure!=FireExposure.Adiabatic)r.Summary.Notes.Add(face.Name+" · temperatura superficiale: "+face.SurfaceSource);
        }
        foreach(double time in observation)
        {
            var gas=r.GasHistory.Single(p=>p.TimeMinutes==time);var c=C.Case(r.Summary,$"t = {time:G8} min","Temperature e azioni al contorno nell’istante selezionato; nessuna temperatura della sezione calcolata.");
            c.Values.Add(C.V("t","Tempo",time,"min","assegnato",reference));c.Values.Add(C.V("θg","Temperatura dei gas",gas.TemperatureC,"°C",Formula(i.Curve),reference));
            foreach(var f in r.Faces)
            {
                var p=f.History.Single(v=>v.TimeMinutes==time);
                if(p.SurfaceC.HasValue)c.Values.Add(C.V(f.Name+" · θm","Temperatura superficiale prescritta",p.SurfaceC.Value,"°C","dato assegnato/interpolato","Fonte della temperatura superficiale"));
                if(p.NetWm2.HasValue)
                {
                    c.Values.Add(C.V(f.Name+" · hc","Convezione, positiva entrante",p.ConvectionWm2!.Value,"W/m²","αc (θg − θm)",En));
                    c.Values.Add(C.V(f.Name+" · hr","Irraggiamento, positivo entrante",p.RadiationWm2!.Value,"W/m²","Φ εm εf σ [(θr+273)⁴ − (θm+273)⁴]",En));
                    c.Values.Add(C.V(f.Name+" · hnet","Flusso netto entrante",p.NetWm2.Value,"W/m²","hc + hr (adiabatico: 0)",En));
                }
            }
        }
        return r;
    }
    public static double NominalTemperature(FireCurve curve,double timeMinutes)
    {
        C.EnumValue(curve);C.Range(timeMinutes,0,1440,"Tempo");
        return curve switch{FireCurve.Standard=>20+345*Math.Log10(8*timeMinutes+1),FireCurve.External=>20+660*(1-.687*Math.Exp(-.32*timeMinutes)-.313*Math.Exp(-3.8*timeMinutes)),FireCurve.Hydrocarbon=>20+1080*(1-.325*Math.Exp(-.167*timeMinutes)-.675*Math.Exp(-2.5*timeMinutes)),_=>throw new ArgumentException("Una storia assegnata richiede i punti del progetto.")};
    }
    private static string Formula(FireCurve curve)=>curve switch{FireCurve.Standard=>"20 + 345 log10(8t+1)",FireCurve.External=>"20 + 660 [1 − 0,687 exp(−0,32t) − 0,313 exp(−3,8t)]",FireCurve.Hydrocarbon=>"20 + 1080 [1 − 0,325 exp(−0,167t) − 0,675 exp(−2,5t)]",_=>"Interpolazione lineare della storia assegnata"};
    private static double Interpolate((double Time,double Temperature)[] points,double time)
    {
        for(int n=1;n<points.Length;n++)if(time<=points[n].Time)return C.Lerp(time,points[n-1].Time,points[n].Time,points[n-1].Temperature,points[n].Temperature);
        throw new ArgumentException("La storia non copre il tempo richiesto.");
    }
    private static void History((double Time,double Temperature)[] points,double duration,string name)
    {
        C.Require(points.Length>=2&&points[0].Time==0&&points[points.Length-1].Time>=duration,name+": almeno due punti, inizio a zero e copertura dell’intera durata.");
        for(int n=0;n<points.Length;n++){C.Range(points[n].Time,0,double.MaxValue,"Tempo "+name);C.Range(points[n].Temperature,-272,2500,"Temperatura "+name);if(n>0)C.Require(points[n].Time>points[n-1].Time,name+": tempi strettamente crescenti.");}
    }
    private static void Validate(FireActionsInput i)
    {
        if(i is null)throw new ArgumentNullException(nameof(i));C.National(i.Standard,i.NationalParameters);C.EnumValue(i.Curve);C.Range(i.DurationMinutes,.01,1440,"Durata");C.Range(i.TimeStepMinutes,.01,10,"Passo temporale");C.Require(i.DurationMinutes/i.TimeStepMinutes<=20000,"Ridurre il numero di intervalli a 20000.");C.Positive(i.LengthM,"Lunghezza estrusa");C.Range(i.AmbientC,-100,100,"Temperatura ambiente");C.Range(i.FireEmissivity,0,1,"Emissività del fuoco");
        C.Require(!string.IsNullOrWhiteSpace(i.FireScenarioSource)&&!string.IsNullOrWhiteSpace(i.BoundaryParametersSource),"Documentare scenario, durata e fonte dei parametri termici (emissività, scambio, esposizione).");
        C.Require(i.FireFaces is {Count:>0}&&i.FireFaces.Count<=50&&i.FireFaces.All(f=>f is not null),"Definire 1–50 facce.");C.Require(i.FireFaces.Select(f=>f.Name).Distinct().Count()==i.FireFaces.Count,"Nomi delle facce duplicati.");
        C.Require(i.GasTemperatures is not null&&i.GasTemperatures.All(p=>p is not null)&&i.SurfaceTemperatures is not null&&i.SurfaceTemperatures.All(p=>p is not null),"Storie termiche non valide.");
        if(i.Curve==FireCurve.AssignedHistory)History(i.GasTemperatures.Select(p=>(p.TimeMinutes,p.TemperatureC)).ToArray(),i.DurationMinutes,"gas");
        if(i.CalculateBoundaryFlux)C.Require(i.PrescribedSurfaceStateConfirmed,"Confermare l’uso di temperature superficiali prescritte, non calcolate dal foglio.");
        for(int index=0;index<i.FireFaces.Count;index++)
        {
            var f=i.FireFaces[index];C.EnumValue(f.Exposure);C.EnumValue(f.SurfaceState);C.Require(!string.IsNullOrWhiteSpace(f.Name),"Nome faccia mancante.");foreach(double v in new[]{f.StartXM,f.StartYM,f.EndXM,f.EndYM})C.Range(v,-1e6,1e6,"Coordinata faccia");C.Require(f.StartXM!=f.EndXM||f.StartYM!=f.EndYM,"Una faccia non può avere lunghezza nulla.");
            C.Range(f.MemberEmissivity,0,1,"Emissività della superficie");C.Range(f.ConfigurationFactor,0,1,"Fattore di configurazione");C.Range(f.ConvectionWm2K,0,1000,"Coefficiente convettivo");C.Range(f.ReferenceSurfaceC,-272,2500,"Temperatura superficiale");
            C.Require(!f.CombinedUnexposedTransfer||f.Exposure==FireExposure.Unexposed&&!f.AssignedConvection,"Scambio combinato disponibile solo per faccia non esposta con α=9 automatico.");
            if(i.Curve==FireCurve.AssignedHistory&&f.Exposure==FireExposure.Exposed)C.Require(f.AssignedConvection,"La curva assegnata richiede il coefficiente convettivo assegnato per ogni faccia esposta.");
            if(i.CalculateBoundaryFlux&&f.Exposure!=FireExposure.Adiabatic)
            {
                C.Require(!string.IsNullOrWhiteSpace(f.SurfaceSource),"Documentare temperatura superficiale della faccia "+f.Name);
                if(f.Exposure==FireExposure.Exposed)C.Require(i.RadiationEqualsGasConfirmed,"Confermare l’ipotesi di temperatura radiante uguale a quella dei gas.");
                if(f.SurfaceState==FireSurfaceState.AssignedHistory)History(i.SurfaceTemperatures.Where(p=>p.Face==index+1).Select(p=>(p.TimeMinutes,p.TemperatureC)).ToArray(),i.DurationMinutes,"superficie "+f.Name);
            }
        }
        foreach(var p in i.SurfaceTemperatures){C.Require(p.Face>=1&&p.Face<=i.FireFaces.Count,"Indice faccia della temperatura superficiale fuori elenco.");C.Range(p.TimeMinutes,0,double.MaxValue,"Tempo superficie");C.Range(p.TemperatureC,-272,2500,"Temperatura superficie");}
    }
}
