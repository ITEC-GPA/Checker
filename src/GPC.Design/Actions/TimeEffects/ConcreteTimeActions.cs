using GPC.Model.Actions.Climate;
using GPC.Model.Actions.TimeEffects;
using GPC.Model.Materials;
using C=GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.TimeEffects;

/// <summary>Linear ageing viscoelasticity by stress-increment superposition. Optional isolated axial bar with end spring.
/// This is not a cracked reinforced-concrete section or a frame solver.</summary>
public static class ConcreteTimeActions
{
    public const string EnReference="EN 1992-1-1:2004 §§3.1.2–3.1.4, Appendice B";
    private const string Compatibility="Barra assiale integra: ε = εcs + Σ Δσ J(t,ti); N = Aσ; N = P + K(δ−u)";
    public static TimeEffectsResult Calculate(TimeEffectsInput input)
    {
        if(input is null)throw new ArgumentNullException(nameof(input));Validate(input);
        double h0=ConcreteTimeEffects.NotionalSize(input.AreaMm2,input.DryingPerimeterMm);
        var law=new ConcreteDeformationLaw(input.Standard,input.FckMpa,input.Cement,input.RelativeHumidityPercent,h0,input.DryingStartDays);
        var coarse=Solve(input,law,input.TimeSteps);
        var history=input.Restraint==AxialRestraint.Free?coarse:Solve(input,law,input.TimeSteps*2);
        double error=0;
        if(input.Restraint!=AxialRestraint.Free)
            foreach(var c in coarse){var fine=history.OrderBy(p=>Math.Abs(p.AgeDays-c.AgeDays)).First();error=Math.Max(error,Math.Abs(fine.StressMpa-c.StressMpa));}
        double stressScale=history.Max(p=>Math.Abs(p.StressMpa));
        var r=new TimeEffectsResult{History=history,NotionalSizeMm=h0,Kh=law.Kh,DryingShrinkageInfinity=law.DryingInfinity,AutogenousShrinkageInfinity=law.AutogenousInfinity,MaxStressDiscretizationMpa=error,Converged=error<=.01+.01*stressScale,WithinLinearRange=history.All(p=>p.WithinLinearRange)};
        r.Summary.Standard=input.Standard==ClimateStandard.Ntc2018?"NTC 2018 §§11.2.10.6–11.2.10.7; leggi temporali EN 1992-1-1:2004":EnReference;
        r.Summary.Scope="Calcestruzzo ordinario a 20 °C, umidità costante e h0 ≥ 100 mm. Deformazioni positive in allungamento, tensioni positive in trazione. Barra assiale integra, senza armatura o fessurazione.";
        r.Summary.Parameters.Add(C.V("h0","Dimensione fittizia",h0,"mm","2 Ac / u",EnReference));
        r.Summary.Parameters.Add(C.V("kh","Coefficiente dimensionale del ritiro",law.Kh,"−","Interpolazione della tabella",input.Standard==ClimateStandard.Ntc2018?"NTC 2018 Tab. 11.2.Vb":"EN 1992-1-1:2004 Tab. 3.3"));
        r.Summary.Parameters.Add(C.V("εcd,∞","Ritiro da essiccamento finale",law.DryingInfinity*1e6,"µε","kh εcd,0",input.Standard==ClimateStandard.Ntc2018?"NTC 2018 Tab. 11.2.Va":"EN 1992-1-1:2004 (B.11)–(B.12)"));
        r.Summary.Parameters.Add(C.V("εca,∞","Ritiro autogeno finale",law.AutogenousInfinity*1e6,"µε","−2,5(fck−10)·10⁻⁶",EnReference));
        r.Summary.Parameters.Add(C.V("Ec","Modulo tangente di riferimento per φ",law.CreepReferenceModulusMpa,"MPa","1,05 Ecm(28)",EnReference));
        foreach(var e in input.StressEvents)
        {
            var creep=law.CreepAtLoading(e.AgeDays);
            r.Summary.Parameters.Add(C.V("φ∞ · "+e.Name,"Coefficiente finale per il singolo incremento",creep.CreepInfinity,"−","φRH β(fcm) β(t0)",EnReference));
            r.Summary.Parameters.Add(C.V("E(t0) · "+e.Name,"Modulo all’applicazione dell’incremento",law.EcmAt(e.AgeDays),"MPa","Ecm(t0)",EnReference));
        }
        r.Summary.Parameters.Add(C.V("Δσmesh","Differenza massima con griglia dimezzata",error,"MPa","max |σ(2n) − σ(n)|","Controllo numerico, tolleranza 0,01 MPa + 1% max|σ|"));
        foreach(var observation in input.Observations)
        {
            var p=history.Single(h=>h.AgeDays==observation.AgeDays);var c=C.Case(r.Summary,observation.Name,$"t = {p.AgeDays:G8} giorni · {(p.WithinLinearRange?"campo lineare integro":"previsione lineare fuori campo: fessurazione o viscosità non lineare da valutare")}");
            Add(c,"t","Età",p.AgeDays,"giorni","assegnata");Add(c,"Ecm(t)","Modulo secante all’età t",p.EcmMpa,"MPa","[fcm(t)/fcm]⁰·³ Ecm(28)");Add(c,"fck(t)","Resistenza caratteristica stimata",p.FckMpa,"MPa","fcm(t)−8 fino a 28 giorni; poi fck");
            Add(c,"εcd","Ritiro essiccamento dal getto",p.DryingShrinkage*1e6,"µε","βds kh εcd,0");Add(c,"εca","Ritiro autogeno dal getto",p.AutogenousShrinkage*1e6,"µε","[1−exp(−0,2√t)] εca,∞");
            Add(c,"Δεcs","Ritiro dalla configurazione di riferimento",p.RelativeShrinkage*1e6,"µε","εcs(t)−εcs(tref), se abilitato");Add(c,"εel,lib","Deformazione elastica senza vincolo",p.FreeElasticStrain*1e6,"µε","Σ Δσest / Ecm(ti)");Add(c,"εcc,lib","Deformazione viscosa senza vincolo",p.FreeCreepStrain*1e6,"µε","Σ Δσest φ(t,ti) / Ec(28)");
            Add(c,"εlib","Deformazione totale senza vincolo",p.FreeStrain*1e6,"µε","Δεcs+εel,lib+εcc,lib");Add(c,"ulib","Spostamento senza vincolo",p.FreeDisplacementMm,"mm","L εlib");Add(c,"u","Spostamento effettivo della barra",p.ActualDisplacementMm,"mm","L ε");
            Add(c,"δ","Spostamento relativo assegnato agli appoggi",p.PrescribedDisplacementMm,"mm","Interpolazione lineare della storia assegnata");Add(c,"σ","Tensione effettiva nella barra",p.StressMpa,"MPa",Compatibility);Add(c,"N","Sforzo normale della barra",p.AxialForceKn,"kN","A σ / 1000");Add(c,"R","Forza del vincolo sul nodo destro",p.SupportForceKn,"kN","N − P; positiva verso +x");
            foreach(var e in input.StressEvents)Add(c,"φ · "+e.Name,"Coefficiente di viscosità del singolo incremento",law.CreepAtLoading(e.AgeDays).AtAge(p.AgeDays).CreepCoefficient,"−","φ0 [(t−t0)/(βH+t−t0)]⁰·³; zero prima del carico");
        }
        r.Summary.Notes.Add("La configurazione priva di tensioni è quella a tref. Il ritiro già avvenuto prima di tref non genera reazioni. Ogni evento è un incremento della tensione esterna P/A; positivo in trazione. Il valore al giorno dell’evento include il salto elastico.");
        r.Summary.Notes.Add("Modello fisico del vincolo: estremità sinistra fissa; estremità destra soggetta a P e, se presente, a una molla assiale K collegata a un appoggio con spostamento δ. Distanza imposta: u=δ e la reazione assorbe anche P. Libero: N=P; gli spostamenti assegnati agli appoggi non si applicano.");
        r.Summary.Notes.Add("J(t,ti)=1/Ecm(ti)+φ(t,ti)/[1,05 Ecm(28)]. Superposizione lineare a incrementi, non modulo efficace unico. La soluzione vincolata usa 2n intervalli logaritmici e include esattamente osservazioni, carichi e vertici della storia di spostamento.");
        r.Summary.Notes.Add("Il controllo di campo confronta la trazione con fctm(t) e la compressione con 0,45 fck(t), anche nei punti intermedi. L’uso dello stesso nucleo viscoso in trazione e nelle variazioni di segno è un’ipotesi reologica lineare. Il superamento segnala che la previsione integra non rappresenta la risposta reale: occorre un modello di fessurazione o viscosità non lineare.");
        if(input.Standard==ClimateStandard.Ntc2018)r.Summary.Notes.Add("Ritiro da essiccamento: tabella NTC 2018 11.2.Va, interpolata senza estrapolazione per 20≤fck≤80 MPa. Evoluzione autogena, maturazione e viscosità: EN 1992-1-1:2004; il ricorso a un modello più preciso per la viscosità è ammesso da NTC §11.2.10.7. La tabella NTC del ritiro non distingue il cemento, che agisce invece su maturazione e viscosità.");
        else r.Summary.Notes.Add("Parametri nazionali/progettuali: "+input.NationalParameters+". Formule EN di prima generazione, non dell’edizione 2023.");
        if(!r.Converged)r.Summary.Notes.Insert(0,"ATTENZIONE: convergenza temporale insufficiente. Aumentare gli intervalli temporali; i risultati vincolati non sono consolidati.");
        if(!r.WithinLinearRange)r.Summary.Notes.Insert(0,"ATTENZIONE: limiti del modello lineare integro superati nella storia. Risultati di previsione, non verifica della sezione.");
        return r;
    }
    private static void Add(ClimateCase c,string symbol,string description,double value,string unit,string formula)=>c.Values.Add(C.V(symbol,description,value,unit,formula,EnReference));
    private static void Validate(TimeEffectsInput i)
    {
        C.National(i.Standard,i.NationalParameters);C.EnumValue(i.Restraint);C.Positive(i.AreaMm2,"Area");C.Positive(i.DryingPerimeterMm,"Perimetro esposto");C.Positive(i.LengthM,"Lunghezza");C.Range(i.ReferenceAgeDays,3.001,100000,"Età di riferimento (>3 giorni)");C.Range(i.TimeSteps,20,1000,"Intervalli temporali");
        if(i.Restraint==AxialRestraint.EndSpring)C.Positive(i.EndSpringKnMm,"Rigidezza molla");
        C.Require(i.Observations is {Count:>0}&&i.Observations.All(o=>o is not null),"Inserire le età di osservazione.");C.Require(i.StressEvents is not null&&i.StressEvents.All(e=>e is not null)&&i.Displacements is not null&&i.Displacements.All(d=>d is not null),"Storia non valida.");
        double max=i.Observations!.Max(o=>o.AgeDays);foreach(var o in i.Observations){C.Require(!string.IsNullOrWhiteSpace(o.Name),"Denominare le osservazioni.");C.Range(o.AgeDays,i.ReferenceAgeDays,100000,"Età di osservazione");}
        foreach(var e in i.StressEvents){C.Range(e.AgeDays,i.ReferenceAgeDays,max,"Età del carico");C.Range(e.IncrementMpa,-1000,1000,"Incremento di tensione");}
        double previous=i.ReferenceAgeDays;foreach(var d in i.Displacements!){C.Range(d.AgeDays,i.ReferenceAgeDays,max,"Età spostamento");C.Require(d.AgeDays>previous,"Gli spostamenti devono seguire tref ed essere ordinati per età crescente; δ(tref)=0.");previous=d.AgeDays;C.Range(d.DisplacementMm,-double.MaxValue,double.MaxValue,"Spostamento");}
        C.Require(i.Restraint!=AxialRestraint.Free||i.Displacements.Count==0,"Gli spostamenti degli appoggi richiedono un vincolo: rimuovere la storia o selezionare il modello vincolato.");
    }
    private sealed record Increment(double Age,double Stress,ConcreteTimeEffects Creep,double Modulus);
    private static List<ConcreteHistoryPoint> Solve(TimeEffectsInput i,ConcreteDeformationLaw law,int count)
    {
        double end=i.Observations.Max(o=>o.AgeDays),start=i.ReferenceAgeDays;
        var grid=new SortedSet<double>{start,end};for(int n=1;n<count;n++)grid.Add(start*Math.Exp(Math.Log(end/start)*n/count));
        foreach(var t in i.Observations.Select(o=>o.AgeDays).Concat(i.StressEvents.Select(e=>e.AgeDays)).Concat(i.Displacements.Select(d=>d.AgeDays)))grid.Add(t);
        var external=i.StressEvents.Select(e=>new Increment(e.AgeDays,e.IncrementMpa,law.CreepAtLoading(e.AgeDays),law.EcmAt(e.AgeDays))).ToArray();
        var history=new List<ConcreteHistoryPoint>();var actual=new List<Increment>();double previousStress=0,sh0=law.ShrinkageAt(start),length=i.LengthM*1000;
        double springCompliance=i.Restraint==AxialRestraint.EndSpring?i.AreaMm2/(i.EndSpringKnMm*1000*length):0;
        foreach(double age in grid)
        {
            double elastic=0,creep=0,externalStress=0;
            foreach(var e in external.Where(e=>e.Age<=age)){elastic+=e.Stress/e.Modulus;if(i.IncludeCreep)creep+=e.Stress*e.Creep.AtAge(age).CreepCoefficient/law.CreepReferenceModulusMpa;externalStress+=e.Stress;}
            double shrink=i.IncludeShrinkage?law.ShrinkageAt(age)-sh0:0,delta=Displacement(i,age),stress,actualElastic=elastic,actualCreep=creep;
            if(i.Restraint==AxialRestraint.Free)stress=externalStress;
            else
            {
                actualElastic=0;actualCreep=0;
                foreach(var e in actual){actualElastic+=e.Stress/e.Modulus;if(i.IncludeCreep)actualCreep+=e.Stress*e.Creep.AtAge(age).CreepCoefficient/law.CreepReferenceModulusMpa;}
                double modulus=law.EcmAt(age),increment=(delta/length+(externalStress-previousStress)*springCompliance-shrink-actualElastic-actualCreep)/(1/modulus+springCompliance);
                stress=previousStress+increment;actualElastic+=increment/modulus;
                actual.Add(new(age,increment,law.CreepAtLoading(age),modulus));previousStress=stress;
            }
            double fck=law.FckAt(age),fctm=law.FctmAt(age),strain=shrink+actualElastic+actualCreep;
            var point=new ConcreteHistoryPoint{AgeDays=age,EcmMpa=law.EcmAt(age),FckMpa=fck,DryingShrinkage=law.DryingAt(age),AutogenousShrinkage=law.AutogenousAt(age),RelativeShrinkage=shrink,FreeElasticStrain=elastic,FreeCreepStrain=creep,FreeStrain=shrink+elastic+creep,ActualElasticStrain=actualElastic,ActualCreepStrain=actualCreep,ActualStrain=strain,StressMpa=stress,ExternalStressMpa=externalStress,AxialForceKn=stress*i.AreaMm2/1000,SupportForceKn=(stress-externalStress)*i.AreaMm2/1000,FreeDisplacementMm=(shrink+elastic+creep)*length,ActualDisplacementMm=strain*length,PrescribedDisplacementMm=delta,MeanTensileStrengthMpa=fctm,WithinLinearRange=stress<=fctm&&stress>=-.45*fck};
            C.Range(point.ActualDisplacementMm,-double.MaxValue,double.MaxValue,"Spostamento calcolato");history.Add(point);
        }
        return history;
    }
    private static double Displacement(TimeEffectsInput i,double age)
    {
        double previous=i.ReferenceAgeDays,value=0;
        foreach(var p in i.Displacements){if(age<=p.AgeDays)return C.Lerp(age,previous,p.AgeDays,value,p.DisplacementMm);previous=p.AgeDays;value=p.DisplacementMm;}
        return value;
    }
}
